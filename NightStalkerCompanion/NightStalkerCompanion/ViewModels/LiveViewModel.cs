using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// Screen 01 — live view. It is a mode, not a tab: you enter it with a button and
/// leave it with "back", so the bottom of the screen carries a back line rather
/// than navigation.
///
/// What the picture itself is not yet: the scope sends H.264 over RTSP, and the
/// unpacking exists in tools/extract_rtp.py but nothing in the app consumes it.
/// Until it does, the frame area shows the reticle over the mock-up's dark field
/// — the same thing the accepted mock-up draws — and says so.
/// </summary>
public partial class LiveViewModel : ScopePageViewModel
{
    public LiveViewModel(AppState state) : base(state)
        => state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Snapshot))
                Raise();
        };

    public string GroupLetter => State.ActiveGroup?.Name() ?? "—";

    public string ReticleCaption
    {
        get
        {
            var r = State.Snapshot.Reticle;
            if (r is null) return "—";
            return r.IsEnabled ? $"Стиль {r.Style}" : "марки нет";
        }
    }

    /// <summary>
    /// Ranging distance from the scope. No command for it is confirmed: the OSD of
    /// the video window carries Static_Distance{1,2,3} and Static_Drop{,1,2}
    /// (notes/APP_FIELDS.md §reticle), which is the likely source, but that is
    /// reading the overlay out of a stream, not a query. So: a dash, not a guess.
    /// </summary>
    public string DistanceText => "—";

    public string DropText => "—";

    /// <summary>Asset of the reticle the scope is showing, null when it shows none.</summary>
    public string? ReticlePreview => State.Snapshot.Reticle?.PreviewStyle is int s
        ? ReticleInfo.PreviewAsset(s)
        : null;

    /// <summary>Environment line under the picture, from the profile.</summary>
    public string OsdText
    {
        get
        {
            var p = State.Snapshot.Profile;
            if (p is null) return "профиль не прочитан";
            return $"{p.Number("Altitude")} м · {p.Number("Temperature")} °C · "
                 + $"первый ноль {p.Number("ZeroRange")} {Units.DistanceUnit(UnitSys)}";
        }
    }

    /// <summary>
    /// Recording indicator. The app does not start recording and cannot see the
    /// scope's recorder state over the commands we have, so it never claims REC.
    /// </summary>
    public string RecordText => "● REC — состояние записи прибором не читается";

    [RelayCommand]
    private async Task BackAsync() => await Shell.Current.GoToAsync("../");

    private void Raise()
    {
        OnPropertyChanged(nameof(GroupLetter));
        OnPropertyChanged(nameof(ReticleCaption));
        OnPropertyChanged(nameof(ReticlePreview));
        OnPropertyChanged(nameof(OsdText));
        RaiseDerived();
    }
}
