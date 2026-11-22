using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// Screen 07 — units. The firmware stores metric; yards are a display mode, so
/// this screen changes one field (DistanceUnit) and shows what every other number
/// becomes. It also states the one thing nobody should silently break: the
/// ballistic coefficient is not a physical constant, and the device keeps the same
/// digits in both modes.
/// </summary>
public partial class UnitsViewModel : ScopePageViewModel
{
    public UnitsViewModel(AppState state) : base(state)
        => state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Snapshot))
                Rebuild();
        };

    public ObservableCollection<ConversionRow> Conversions { get; } = new();

    public bool IsMetric => UnitSys == UnitSystem.Metric;
    public bool IsImperial => UnitSys == UnitSystem.Imperial;

    public string BcTag =>
        "Балл. коэффициент — формально безразмерный, но на деле это число зависит "
        + "от модели сопротивления. Прибор держит одно и то же значение при обоих "
        + "режимах: значит «безразмерный» здесь — условность прибора, а не физика.";

    [RelayCommand]
    private async Task PickMetricAsync() => await ChooseAsync(metric: true);

    [RelayCommand]
    private async Task PickImperialAsync() => await ChooseAsync(metric: false);

    private async Task ChooseAsync(bool metric)
    {
        if (IsMetric == metric) return;
        // DistanceUnit is an ordinary writable profile field: 0 metric, 1 imperial.
        await State.WriteFieldAsync("DistanceUnit", metric ? "0" : "1");
        Rebuild();
    }

    private void Rebuild()
    {
        Conversions.Clear();
        var p = State.Snapshot.Profile;
        if (p is not null)
        {
            Add("Скорость", v => Units.Format(Units.SpeedToShown(v, UnitSystem.Metric), 1),
                v => Units.Format(Units.SpeedToShown(v, UnitSystem.Imperial), 0), "Velocity", "м/с", "ft/s");
            Add("Масса", v => Units.Format(Units.MassToShown(v, UnitSystem.Metric), 0.01),
                v => Units.Format(Units.MassToShown(v, UnitSystem.Imperial), 0.1), "BulletWt", "г", "gr");
            Add("Оптика", v => Units.Format(Units.LengthToShown(v, UnitSystem.Metric), 0.1),
                v => Units.Format(Units.LengthToShown(v, UnitSystem.Imperial), 0.01), "ScopeHt", "мм", "in");
            Add("Пристрелка", v => Units.Format(Units.DistanceToShown(v, UnitSystem.Metric), 1),
                v => Units.Format(Units.DistanceToShown(v, UnitSystem.Imperial), 1), "ZeroRange", "м", "yd");
            Add("Высота", v => Units.Format(Units.AltitudeToShown(v, UnitSystem.Metric), 1),
                v => Units.Format(Units.AltitudeToShown(v, UnitSystem.Imperial), 0), "Altitude", "м", "ft");
            Add("Температура", v => Units.Format(Units.TemperatureToShown(v, UnitSystem.Metric), 1),
                v => Units.Format(Units.TemperatureToShown(v, UnitSystem.Imperial), 0), "Temperature", "°C", "°F");
            Add("Б.K.", v => v.ToString("0.000", CultureInfo.InvariantCulture),
                v => v.ToString("0.000", CultureInfo.InvariantCulture),
                "BulletBC", "", "");
        }

        OnPropertyChanged(nameof(IsMetric));
        OnPropertyChanged(nameof(IsImperial));
        RaiseDerived();
    }

    private void Add(string label, Func<double, string> metric, Func<double, string> imperial,
        string field, string metricUnit, string imperialUnit)
    {
        var p = State.Snapshot.Profile;
        if (p is null || !p.Raw.ContainsKey(field)) return;
        double stored = p.Number(field).Value;
        Conversions.Add(new ConversionRow(label,
            metric(stored), string.IsNullOrEmpty(metricUnit) ? "" : " " + metricUnit,
            imperial(stored), string.IsNullOrEmpty(imperialUnit) ? "" : " " + imperialUnit));
    }
}

/// <summary>One row of the "stored value becomes this" table.</summary>
public sealed record ConversionRow(
    string Label, string Metric, string MetricUnit, string Imperial, string ImperialUnit);
