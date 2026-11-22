using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// Screen 03 — the ballistic profile.
///
/// Two rules from the mock-up drive the layout. First, a number appears once: the
/// arc indicator and the duplicate value inside the stepper are gone, so a field
/// is one digit, one unit, one step. Second, one unit system per screen — the
/// firmware stores metric, and yards are a display mode, not a second storage.
///
/// Edits stay as a draft until the user presses "записать"; the write bar then
/// reports what the device said after reading the same group back.
/// </summary>
public partial class BallisticsViewModel : ScopePageViewModel
{
    private readonly Dictionary<string, double> draft = new();

    public BallisticsViewModel(AppState state) : base(state)
        => state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Snapshot))
                Rebuild();
        };

    public ObservableCollection<FieldRow> Rows { get; } = new();
    public ObservableCollection<DropRow> Drops { get; } = new();

    /// <summary>Model caption the drop table is computed with, shown under it.</summary>
    public string DropModelNote => BallisticsCalculator.ModelName
        + (BallisticsCalculator.VerifiedAgainstScope ? "" : ". С показанием прицела не сверено.");

    public int ChangedCount => draft.Count;

    public bool HasChanges => draft.Count > 0;

    public string GroupLabel => State.ActiveGroup is { } g ? g.Name() : "?";

    [RelayCommand]
    private async Task OpenUnitsAsync() => await Shell.Current.GoToAsync("units");

    /// <summary>One step of one field. Argument arrives as "Velocity:+1" or "BulletWt:-1".</summary>
    [RelayCommand]
    private void Step(string argument)
    {
        string[] parts = argument.Split(':');
        if (parts.Length != 2 || !Specs.TryGetValue(parts[0], out var spec))
            return;
        if (!TryStored(parts[0], out double current))
            return;

        // Only −/+ exists on this screen, so the argument carries a direction and the
        // step comes from the field and the unit system: 0.01 g is 0.15 gr, and in
        // imperial the mass has to move by 0.1 gr, not by a recomputed 0.01.
        int direction = parts[1].TrimStart('+', '-').Length > 0 && parts[1].StartsWith('-') ? -1 : 1;
        draft[parts[0]] = current + direction * spec.Step(UnitSys);
        Rebuild();
    }

    [RelayCommand]
    private void Discard()
    {
        draft.Clear();
        Rebuild();
    }

    [RelayCommand]
    private async Task WriteAsync()
    {
        if (draft.Count == 0) return;

        var wire = new Dictionary<string, string>();
        foreach (var (field, value) in draft)
            wire[field] = ToWire(field, value);

        // The draft is dropped before the write, not after: if the device answers a
        // different value, the screen must show the device, not our intention.
        draft.Clear();
        await State.WriteFieldsAsync(wire);
        Rebuild();
    }

    private static readonly Dictionary<string, FieldSpec> Specs = new()
    {
        ["Velocity"] = new("Скорость", Units.Quantity.Speed, Units.SpeedStep, Writable: true),
        ["BulletWt"] = new("Масса пули", Units.Quantity.Mass, Units.MassStep, Writable: true),
        ["BulletBC"] = new("Балл. коэффициент", Units.Quantity.Bc, _ => Units.BcStep(), Writable: true),
        ["ScopeHt"] = new("Высота оптики", Units.Quantity.Length, Units.LengthStep, Writable: true),
        ["ZeroRange"] = new("Первый ноль", Units.Quantity.Distance, Units.DistanceStep, Writable: true),
        ["Altitude"] = new("Над уровнем моря", Units.Quantity.Altitude, Units.DistanceStep, Writable: true),
        ["Temperature"] = new("Температура", Units.Quantity.Temperature, _ => 1.0, Writable: true),
    };

    /// <summary>
    /// Fields the mock-up shows but the profile has no command for. Listing them
    /// here keeps them visible for review instead of quietly dropping a row the
    /// owner approved: the value stays a dash and the action names the reason.
    /// </summary>
    private const string NoCommandAction = "нет команды";

    private bool TryStored(string field, out double value)
    {
        if (draft.TryGetValue(field, out value)) return true;
        var profile = State.Snapshot.Profile;
        if (profile is not null && profile.Raw.ContainsKey(field))
        {
            value = profile.Number(field).Value;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Wire text keeps the device's own number of decimals. This is not cosmetics:
    /// the client compares writes with read-backs as text, and the scope answers
    /// "3.00" where "3.0" was sent — sending our own formatting would read back as
    /// a mismatch on every field.
    /// </summary>
    private string ToWire(string field, double metricValue)
    {
        var profile = State.Snapshot.Profile;
        string original = profile is not null && profile.Raw.TryGetValue(field, out var raw) ? raw : "";
        int decimals = original.Contains('.') ? original.Split('.')[1].Length : 0;
        return metricValue.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    private void Rebuild()
    {
        Rows.Clear();

        // Row 1 of the mock-up: the ballistic calculator itself. There is no field
        // for it in the twelve the profile carries, so it is shown and labelled.
        Rows.Add(new FieldRow("", "Калькулятор", "—", "", false, NoCommandAction));
        Rows.Add(new FieldRow("DistanceUnit", "Единицы",
            UnitSys == UnitSystem.Metric ? "метры" : "ярды", "", false, "›"));

        if (State.Snapshot.Profile is null)
        {
            Rows.Add(new FieldRow("", "Профиль", "—", "не прочитан", false, ""));
        }
        else
        {
            foreach (var (field, spec) in Specs)
            {
                if (!TryStored(field, out double metric))
                    continue;

                double shown = spec.Quantity switch
                {
                    Units.Quantity.Speed => Units.SpeedToShown(metric, UnitSys),
                    Units.Quantity.Mass => Units.MassToShown(metric, UnitSys),
                    Units.Quantity.Length => Units.LengthToShown(metric, UnitSys),
                    Units.Quantity.Distance => Units.DistanceToShown(metric, UnitSys),
                    Units.Quantity.Altitude => Units.AltitudeToShown(metric, UnitSys),
                    Units.Quantity.Temperature => Units.TemperatureToShown(metric, UnitSys),
                    _ => metric,
                };

                string unit = spec.Quantity switch
                {
                    Units.Quantity.Speed => Units.SpeedUnit(UnitSys),
                    Units.Quantity.Mass => Units.MassUnit(UnitSys),
                    Units.Quantity.Length => Units.LengthUnit(UnitSys),
                    Units.Quantity.Distance => Units.DistanceUnit(UnitSys),
                    Units.Quantity.Altitude => Units.AltitudeUnit(UnitSys),
                    Units.Quantity.Temperature => Units.TemperatureUnit(UnitSys),
                    _ => "",
                };

                double step = spec.Step(UnitSys);
                Rows.Add(new FieldRow(
                    field,
                    spec.Label,
                    Units.Format(shown, step),
                    unit,
                    spec.Writable,
                    draft.ContainsKey(field) ? "изменено" : ""));
            }
        }

        Drops.Clear();
        foreach (var row in BallisticsCalculator.Table(
                     BallisticsInput.From(State.Snapshot.Profile), UnitSys))
            Drops.Add(row);

        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(DropModelNote));
        OnPropertyChanged(nameof(GroupLabel));
        RaiseDerived();
    }

    private sealed record FieldSpec(
        string Label,
        Units.Quantity Quantity,
        Func<UnitSystem, double> Step,
        bool Writable);
}

/// <summary>
/// One editable field. A record rather than a live object: the whole list is
/// rebuilt from the snapshot plus the draft, so a row can never drift from either.
/// </summary>
public sealed record FieldRow(
    string Field,
    string Label,
    string Value,
    string Unit,
    bool Writable,
    string Action)
{
    public bool HasUnit => Unit.Length > 0;
}
