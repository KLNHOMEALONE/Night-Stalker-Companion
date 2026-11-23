using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// Base for screen view models: the shared state, plus the couple of captions
/// every screen repeats. The point of the base is the sample banner — every
/// screen must be able to say "the scope is not connected, these are saved
/// numbers", and a screen that forgets it is lying by omission.
/// </summary>
public abstract partial class ScopePageViewModel : ObservableObject
{
    protected ScopePageViewModel(AppState state)
    {
        State = state;
        // Subscribed once, here rather than on every appearance: a screen is created
        // once and appears many times, and a handler added per appearance is a leak
        // that also makes the captions fire several times per change.
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.Snapshot) or nameof(AppState.IsUsingSample)
                or nameof(AppState.IsBusy))
                RaiseDerived();
        };
    }

    /// <summary>Shared device state; the write status bar binds straight to it.</summary>
    public AppState State { get; }

    /// <summary>
    /// The banner shown when a saved snapshot stands in for the device. Null when
    /// the numbers are live, so the banner simply is not there.
    /// </summary>
    public string? SampleNotice => State.IsUsingSample
        ? State.Snapshot.SourceLabel + ". Числа на экране — из сохранённого профиля, а не из прицела: "
          + "списки медиа и запись недоступны."
        : null;

    /// <summary>"прочитано N с назад · 6019, 6009, 3014" — one place, every screen.</summary>
    public string ReadoutText => $"{State.AgeText} · {State.SourceText}";

    public bool IsBusy => State.IsBusy;

    /// <summary>
    /// Build name as the tail after the last underscore: the firmware answers
    /// "PARD_Night Stalker 4K_EN_20241212" and the status bar has room for neither.
    /// While the first poll is in flight the bar says so — an empty cell reads as a
    /// measurement of nothing rather than as a question not yet answered.
    /// </summary>
    public string FirmwareShort
    {
        get
        {
            var name = State.Snapshot.Firmware;
            if (string.IsNullOrWhiteSpace(name))
                return State.IsBusy ? "опрос…" : "нет связи";
            int cut = name.LastIndexOf('_');
            return cut >= 0 && cut + 1 < name.Length ? name[(cut + 1)..] : name;
        }
    }

    /// <summary>Unit system the profile is in. The device stores metric; yards are display.</summary>
    public Services.UnitSystem UnitSys =>
        State.Snapshot.Profile is { } p && (int)p.Number("DistanceUnit").Value != 0
            ? Services.UnitSystem.Imperial
            : Services.UnitSystem.Metric;

    /// <summary>
    /// "Back" goes one screen up. It works through <see cref="Routes"/> rather than a
    /// route string in the markup: where exactly the button leads is a decision, and it is
    /// covered by tests, which the markup is not.
    /// </summary>
    [RelayCommand]
    private async Task BackAsync() => await Shell.Current.GoToAsync(Routes.Back);

    /// <summary>
    /// "Device" returns to the start screen in one tap from any depth. That is exactly why it
    /// is an absolute route to the tab and not a <see cref="BackCommand"/> in a loop: a screen
    /// does not know how many screens lie on top of it.
    /// </summary>
    [RelayCommand]
    private async Task GoHomeAsync() => await Shell.Current.GoToAsync(Routes.Home);

    protected void RaiseDerived()
    {
        OnPropertyChanged(nameof(SampleNotice));
        OnPropertyChanged(nameof(ReadoutText));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(FirmwareShort));
        OnPropertyChanged(nameof(UnitSys));
    }

    /// <summary>Poll on entry.</summary>
    public virtual async Task AppearingAsync(CancellationToken ct = default)
        => await State.RefreshCommand.ExecuteAsync(ct);

    /// <summary>The profile's raw number as text, or an honest dash.</summary>
    protected string Number(string field, string unit)
    {
        var profile = State.Snapshot.Profile;
        if (profile is null || !profile.Raw.ContainsKey(field))
            return "—";
        var n = profile.Number(field);
        return n.IsEmpty ? "—" : $"{n} {unit}".TrimEnd();
    }
}
