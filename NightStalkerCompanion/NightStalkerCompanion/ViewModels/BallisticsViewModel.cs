using System.Collections.ObjectModel;
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
/// Every writable number is typed as well as stepped, and it is typed in the units the
/// screen is showing: whoever reads yards types yards, and making the conversion to metres
/// by hand in a field would be doing the app's arithmetic in the user's head. A typed number
/// is a draft until the user presses «записать»; the conversion to the metric the firmware
/// stores happens once, on the way out, and the write bar then reports what the device said
/// after reading the same group back.
/// </summary>
public partial class BallisticsViewModel : ScopePageViewModel
{
    /// <summary>
    /// Edited fields, held as the text standing in each one: what the user typed, in the units
    /// on screen, is what the draft remembers. A value is stored only once the text parses as a
    /// value of that field's type, so a half-typed «12.» cannot leave as 12, or as 1, or as 120.
    /// </summary>
    private readonly Dictionary<string, string> draft = new();

    /// <summary>The unit system the draft was typed in. A draft cannot survive a change of it.</summary>
    private UnitSystem? draftedIn;

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

    /// <summary>
    /// Where a tapped row leads. One of them leads anywhere: the units row carries the «›" the
    /// mock-up drew for it, and the screen has a screen for it. A field is not opened, it is
    /// edited, and the row that shows no way in does not go anywhere.
    /// </summary>
    [RelayCommand]
    private async Task OpenRowAsync(string field)
    {
        if (field == "DistanceUnit")
            await OpenUnitsAsync();
    }

    /// <summary>One step of one field. Argument arrives as "Velocity:+1" or "BulletWt:-1".</summary>
    [RelayCommand]
    private void Step(string argument)
    {
        string[] parts = argument.Split(':');
        if (parts.Length != 2 || !Specs.TryGetValue(parts[0], out var spec))
            return;
        if (!TryMetric(parts[0], spec, out double current))
            return;

        // Only −/+ exists on this screen, so the argument carries a direction and the
        // step comes from the field and the unit system: 0.01 g is 0.15 gr, and in
        // imperial the mass has to move by 0.1 gr, not by a recomputed 0.01.
        int direction = parts[1].StartsWith('-') ? -1 : 1;
        SetDraft(parts[0], spec, FieldText(spec, current + direction * spec.Step(UnitSys)));
    }

    /// <summary>
    /// What a field holds now that the keyboard has gone away or Enter has been pressed. The
    /// argument is «Velocity:870» — the field and the text in its Entry, taken by the page from
    /// the field itself rather than through the binding, which commits late and would hand this
    /// command the previous text.
    ///
    /// A text that is not a number this field can hold is not remembered at all: the row goes
    /// back to the number the device was read with, which is the one honest number on the
    /// screen. Nothing half-typed reaches the draft, so nothing half-typed can be written.
    /// </summary>
    [RelayCommand]
    private void Commit(string argument)
    {
        string[] parts = argument.Split(':', 2);
        if (parts.Length != 2 || !Specs.TryGetValue(parts[0], out var spec))
            return;

        string text = parts[1].Trim();
        if (text.Length > 0 && ProfileFieldInput.TryParse(text, spec.Type(UnitSys), out _))
            draft[parts[0]] = text;
        else
            draft.Remove(parts[0]);

        Show(parts[0], spec);
        RaiseDraftState();
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
        foreach (var (field, text) in draft)
            // A draft that no longer reads as a value of its field is a draft that should never
            // have been kept; leaving it out is honest, sending the device an empty value is not.
            if (Wire(field, Specs[field], text, out string value))
                wire[field] = value;

        if (wire.Count == 0)
            return;

        // The draft is dropped before the write, not after: if the device answers a
        // different value, the screen must show the device, not our intention.
        draft.Clear();
        await State.WriteFieldsAsync(wire);
        Rebuild();
    }

    /// <summary>
    /// The seven fields this screen edits, each with the label it is shown under, the quantity
    /// it is, the step its −/+ buttons take, and the type a keyboard has to obey.
    ///
    /// The step and the type are both functions of the unit system, and for different reasons:
    /// the step because 0.01 g is 0.15 gr, the type because a bound is a number in the units the
    /// field is shown in — 8 000 of one screen's metres is 26 247 of another's feet. Where a
    /// field has no bound, none is stated: the device has never refused a large muzzle velocity,
    /// and an invented ceiling would refuse a value the sight takes.
    /// </summary>
    private static readonly Dictionary<string, FieldSpec> Specs = new()
    {
        ["Velocity"] = new("Скорость", Units.Quantity.Speed, Units.SpeedStep,
            _ => ProfileFieldType.Whole),
        ["BulletWt"] = new("Масса пули", Units.Quantity.Mass, Units.MassStep,
            _ => ProfileFieldType.Decimal),
        ["BulletBC"] = new("Балл. коэффициент", Units.Quantity.Bc, _ => Units.BcStep(),
            _ => ProfileFieldType.Decimal),
        ["ScopeHt"] = new("Высота оптики", Units.Quantity.Length, Units.LengthStep,
            _ => ProfileFieldType.Decimal),
        ["ZeroRange"] = new("Первый ноль", Units.Quantity.Distance, Units.DistanceStep,
            _ => ProfileFieldType.Whole),
        ["Altitude"] = new("Над уровнем моря", Units.Quantity.Altitude, Units.DistanceStep,
            u => ProfileFieldType.Whole.Between(ProfileFieldLimits.AltitudeMinimum,
                Units.AltitudeToShown(ProfileFieldLimits.AltitudeMaximum, u))),
        ["Temperature"] = new("Температура", Units.Quantity.Temperature, _ => 1.0,
            u => ProfileFieldType.SignedWhole.Between(
                Units.TemperatureToShown(ProfileFieldLimits.TemperatureMinimum, u),
                Units.TemperatureToShown(ProfileFieldLimits.TemperatureMaximum, u))),
    };

    private sealed record FieldSpec(
        string Label,
        Units.Quantity Quantity,
        Func<UnitSystem, double> Step,
        Func<UnitSystem, ProfileFieldType> Type);

    /// <summary>
    /// Fields the mock-up shows but the profile has no command for. Listing them
    /// here keeps them visible for review instead of quietly dropping a row the
    /// owner approved: the value stays a dash and the action names the reason.
    /// </summary>
    private const string NoCommandAction = "нет команды";

    /// <summary>
    /// The value a field stands on, in the metric the firmware stores: what is typed in the
    /// units on screen when the field is edited, what the device said otherwise.
    /// </summary>
    private bool TryMetric(string field, FieldSpec spec, out double metric)
    {
        metric = 0;
        if (draft.TryGetValue(field, out string? text))
        {
            if (!ProfileFieldInput.TryParse(text, spec.Type(UnitSys), out double shown))
                return false;
            metric = Units.ShownToStored(shown, UnitSys, spec.Quantity);
            return true;
        }

        var profile = State.Snapshot.Profile;
        if (profile is not null && profile.Raw.ContainsKey(field))
        {
            metric = profile.Number(field).Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// The text a metric value takes in a field: the units on screen, with as many decimals as
    /// the field's step carries meaning.
    /// </summary>
    private string FieldText(FieldSpec spec, double metric)
        => Units.Format(ToShown(metric, spec.Quantity), spec.Step(UnitSys));

    private double ToShown(double metric, Units.Quantity quantity) => quantity switch
    {
        Units.Quantity.Speed => Units.SpeedToShown(metric, UnitSys),
        Units.Quantity.Mass => Units.MassToShown(metric, UnitSys),
        Units.Quantity.Length => Units.LengthToShown(metric, UnitSys),
        Units.Quantity.Distance => Units.DistanceToShown(metric, UnitSys),
        Units.Quantity.Altitude => Units.AltitudeToShown(metric, UnitSys),
        Units.Quantity.Temperature => Units.TemperatureToShown(metric, UnitSys),
        _ => metric,                                    // the coefficient is never converted
    };

    /// <summary>
    /// The text to send for a field: its value in the metric the firmware stores, printed with the
    /// decimals that were typed, or with the field's own tail where the field was stepped rather
    /// than typed. The tail is formatting — <c>7.1</c> is <c>7.10</c> — so what matters is that the
    /// number leaving is the number standing in the field, converted once, on the way out.
    /// </summary>
    private bool Wire(string field, FieldSpec spec, string text, out string wire)
    {
        wire = "";
        if (!ProfileFieldInput.TryParse(text, spec.Type(UnitSys), out double shown))
            return false;

        var profile = State.Snapshot.Profile;
        string stored = profile is not null && profile.Raw.TryGetValue(field, out var raw) ? raw : "";
        wire = ProfileFieldInput.Wire(
            Units.ShownToStored(shown, UnitSys, spec.Quantity),
            text,
            ProfileFieldInput.StoredDecimals(stored));
        return true;
    }

    /// <summary>Put a value in a field's draft and show it.</summary>
    private void SetDraft(string field, FieldSpec spec, string text)
    {
        draft[field] = text;
        Show(field, spec);
        RaiseDraftState();
    }

    /// <summary>
    /// Make the row say what the draft and the snapshot now say about its field. Only when that
    /// differs from what the row already says: writing the same text into a focused Entry moves
    /// the caret, and a commit runs on every focus loss — including the one that happens while
    /// a value is being typed into the field next to it.
    /// </summary>
    private void Show(string field, FieldSpec spec)
    {
        if (Rows.FirstOrDefault(r => r.Field == field) is not { } row)
            return;

        string text = draft.TryGetValue(field, out string? typed)
            ? typed
            : TryMetric(field, spec, out double metric) ? FieldText(spec, metric) : "—";
        if (row.Text != text)
            row.Text = text;

        string action = draft.ContainsKey(field) ? "изменено" : "";
        if (row.Action != action)
            row.Action = action;
    }

    private void RaiseDraftState()
    {
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(HasChanges));
    }

    private void Rebuild()
    {
        // «70.1» typed in a field meaning millimetres is not the same height once the screen
        // means inches, and it is not the app's place to decide which number the user meant. A
        // change of unit system is a change of what the device says, so the draft goes with it.
        if (draftedIn != UnitSys)
        {
            draft.Clear();
            draftedIn = UnitSys;
        }

        Rows.Clear();

        // Row 1 of the mock-up: the ballistic calculator itself. There is no field
        // for it in the twelve the profile carries, so it is shown and labelled.
        Rows.Add(new FieldRow("", "Калькулятор", "—", action: NoCommandAction));
        Rows.Add(new FieldRow("DistanceUnit", "Единицы",
            UnitSys == UnitSystem.Metric ? "метры" : "ярды", action: "›"));

        if (State.Snapshot.Profile is null)
        {
            Rows.Add(new FieldRow("", "Профиль", "—", unit: "не прочитан"));
        }
        else
        {
            foreach (var (field, spec) in Specs)
            {
                if (!TryMetric(field, spec, out double metric))
                    continue;

                Rows.Add(new FieldRow(
                    field,
                    spec.Label,
                    FieldText(spec, metric),
                    spec.Quantity switch
                    {
                        Units.Quantity.Speed => Units.SpeedUnit(UnitSys),
                        Units.Quantity.Mass => Units.MassUnit(UnitSys),
                        Units.Quantity.Length => Units.LengthUnit(UnitSys),
                        Units.Quantity.Distance => Units.DistanceUnit(UnitSys),
                        Units.Quantity.Altitude => Units.AltitudeUnit(UnitSys),
                        Units.Quantity.Temperature => Units.TemperatureUnit(UnitSys),
                        _ => "",
                    },
                    type: spec.Type(UnitSys),
                    changed: draft.ContainsKey(field)));
            }
        }

        Drops.Clear();
        foreach (var row in BallisticsCalculator.Table(
                     BallisticsInput.From(State.Snapshot.Profile), UnitSys))
            Drops.Add(row);

        RaiseDraftState();
        OnPropertyChanged(nameof(DropModelNote));
        OnPropertyChanged(nameof(GroupLabel));
        RaiseDerived();
    }
}

/// <summary>
/// One row of the screen: a label, a number, a unit, and either a stepper pair, a typed
/// field, or a caption where neither applies.
///
/// A record would be tidier, and the screen used one while a value could only be stepped: the
/// whole list was rebuilt from the snapshot plus the draft, so a row could never drift from
/// either. A field the user types in cannot be rebuilt from underneath itself — a keystroke
/// would replace the text being typed and drop the caret — so these rows are live objects
/// whose number and status are rewritten in place. The rest of the rule holds: the list is
/// still rebuilt whenever the snapshot moves, and a row still cannot show a number that the
/// draft and the snapshot do not agree on.
/// </summary>
public sealed partial class FieldRow : ObservableObject
{
    public FieldRow(
        string field, string label, string text, string unit = "", string action = "",
        ProfileFieldType? type = null, bool changed = false)
    {
        Field = field;
        Label = label;
        this.text = text;
        Unit = unit;
        Writable = type is not null;
        Type = type ?? ProfileFieldType.Whole;
        Action = changed ? "изменено" : action;
    }

    /// <summary>Profile field name; empty for a row that edits nothing.</summary>
    public string Field { get; init; } = "";

    public string Label { get; init; } = "";

    public string Unit { get; init; } = "";

    /// <summary>
    /// The number in the field. Two-way bound: this is where a typed value arrives, and the
    /// keystroke rule that keeps it a number of the right type is
    /// <see cref="Controls.ProfileFieldEntryBehavior"/>, which reads <see cref="Type"/> here.
    /// </summary>
    [ObservableProperty]
    private string text;

    /// <summary>What the row shows beside its number: «›», «изменено», «нет команды».</summary>
    [ObservableProperty]
    private string action;

    /// <summary>Whether this row is a field the user can write to.</summary>
    public bool Writable { get; init; }

    /// <summary>The type of the profile field, and so the rule the field enforces on keystrokes.</summary>
    public ProfileFieldType Type { get; init; } = ProfileFieldType.Whole;

    public bool HasUnit => Unit.Length > 0;

    /// <summary>
    /// The longest text this field can hold. A typing limit rather than a rule about the value:
    /// the value rule is <see cref="Type"/>, and it is checked on every keystroke.
    /// </summary>
    public int MaxLength => ProfileFieldInput.MaxLength;

    /// <summary>What the field sends when the keyboard goes away: «Velocity:» plus the text in it.</summary>
    public string CommitPrefix => Field + ":";

    /// <summary>What the steppers send: «Velocity:+1», «Velocity:-1».</summary>
    public string StepPrefix => Field + ":";
}
