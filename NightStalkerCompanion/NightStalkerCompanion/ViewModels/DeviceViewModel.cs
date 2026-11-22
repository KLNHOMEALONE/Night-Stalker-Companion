using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// Screen 00 — the dashboard. It shows numbers, not buttons: the original put a
/// scope picture and three buttons here, hid the battery inside "connection
/// status", and never showed the ballistic profile at all.
/// </summary>
public partial class DeviceViewModel : ScopePageViewModel
{
    public DeviceViewModel(AppState state) : base(state)
    {
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Snapshot))
                Rebuild();
        };
        Rebuild();
    }

    public ObservableCollection<GroupLine> GroupRows { get; } = new();

    /// <summary>Top status bar, left cell.</summary>
    public string GroupLetter => State.ActiveGroup?.Name() ?? "—";

    /// <summary>Top status bar, second cell.</summary>
    public string ReticleCaption
    {
        get
        {
            var r = State.Snapshot.Reticle;
            if (r is null) return "—";
            return r.IsEnabled ? $"Стиль {r.Style}" : "марки нет";
        }
    }

    public string ZeroText => Number("ZeroRange", Units.DistanceUnit(UnitSys));
    public string VelocityText => Number("Velocity", Units.SpeedUnit(UnitSys));

    /// <summary>
    /// Battery. The stock app has a "Battery Life" line, but no command for it was
    /// ever confirmed, so we print a dash rather than a made-up percentage — the
    /// same choice the accepted mock-up documents on screen 00.
    /// </summary>
    public string BatteryText => "заряд —";

    /// <summary>
    /// Distance mode. Read-only: the firmware accepts 1 through cmd=6018 and
    /// rejects 0 and 2, and control of it lives in the POST form we have not
    /// captured yet (notes/HANDOFF.md).
    /// </summary>
    public string DistanceModeText
    {
        get
        {
            var p = State.Snapshot.Profile;
            return p is null || !p.Raw.ContainsKey("DistanceSwitch")
                ? "—"
                : p.Number("DistanceSwitch").ToString();
        }
    }

    /// <summary>
    /// True until the group walk has been run. Reading the other four groups costs
    /// a write each, so it is an explicit command on the groups screen, and until
    /// then the list has to say "not read" instead of repeating the active group.
    /// </summary>
    public bool GroupsRead => State.Snapshot.GroupReticles is { Count: > 0 };

    [RelayCommand]
    private async Task OpenLiveAsync() => await Shell.Current.GoToAsync("live");

    [RelayCommand]
    private async Task OpenBallisticsAsync() => await Shell.Current.GoToAsync("ballistics");

    [RelayCommand]
    private async Task OpenGroupsAsync() => await Shell.Current.GoToAsync("groups");

    /// <summary>Open the reticle screen aimed at one group.</summary>
    [RelayCommand]
    private async Task OpenGroupAsync(int index)
        => await Shell.Current.GoToAsync($"reticle?group={index}");

    private void Rebuild()
    {
        GroupRows.Clear();
        var reticles = State.Snapshot.GroupReticles;

        for (int i = 0; i < PardApi.Groups.Count; i++)
        {
            string letter = PardApi.Groups.All[i].Name();
            bool active = State.ActiveGroupIndex == i;

            if (reticles is not null && reticles.TryGetValue(i, out var r))
            {
                GroupRows.Add(new GroupLine(
                    i,
                    letter,
                    ReticleInfo.Describe(r.Style, r.Color, r.PosX, r.PosY),
                    active ? "активная" : "",
                    active,
                    r.PreviewStyle));
            }
            else
            {
                GroupRows.Add(active && State.Snapshot.Reticle is { } live
                    ? new GroupLine(i, letter,
                        ReticleInfo.Describe(live.Style, live.Color, live.PosX, live.PosY),
                        "активная", true, live.PreviewStyle)
                    : GroupLine.Empty(i, letter));
            }
        }

        OnPropertyChanged(nameof(GroupLetter));
        OnPropertyChanged(nameof(ReticleCaption));
        OnPropertyChanged(nameof(ZeroText));
        OnPropertyChanged(nameof(VelocityText));
        OnPropertyChanged(nameof(FirmwareShort));
        OnPropertyChanged(nameof(DistanceModeText));
        OnPropertyChanged(nameof(UnitSys));
        OnPropertyChanged(nameof(GroupsRead));
        RaiseDerived();
    }
}
