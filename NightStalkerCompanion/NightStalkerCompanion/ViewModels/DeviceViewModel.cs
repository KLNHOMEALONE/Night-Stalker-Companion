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
            // The active group's reticle, by the group the reticle itself names. The two come
            // from different commands and a write moves the active group between them, so asking
            // “what is active” and reading “the last reticle” would sometimes print one group's
            // reticle next to another group's letter.
            var r = State.Snapshot.ReticleOf(State.ActiveGroupIndex ?? -1);
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
    /// True when the reticle of every group is known. Reading one costs a write that selects
    /// that group, so it stays an explicit command, and while some group is unknown the list has
    /// to say so — a “—” next to a letter is a question nobody asked yet, not an empty group.
    /// Groups may share a style and a profile, so knowing three of them says nothing about the
    /// other two: this counts the groups, not the distinct styles.
    /// </summary>
    public bool GroupsRead => PardApi.Groups.All.All(g => State.Snapshot.ReticleOf(g.Par()) is not null);

    [RelayCommand]
    private async Task OpenLiveAsync() => await Shell.Current.GoToAsync("live");

    [RelayCommand]
    private async Task OpenBallisticsAsync() => await Shell.Current.GoToAsync("ballistics");

    /// <summary>Open the reticle screen aimed at one group.</summary>
    [RelayCommand]
    private async Task OpenGroupAsync(int index)
        => await Shell.Current.GoToAsync(Routes.Reticle(index));

    private void Rebuild()
    {
        GroupRows.Clear();

        for (int i = 0; i < PardApi.Groups.Count; i++)
        {
            string letter = PardApi.Groups.All[i].Name();
            bool active = State.ActiveGroupIndex == i;

            // Ask for this group's reticle rather than using the last one read: five groups can
            // hold the same style and the same profile, so nothing but the group the scope named
            // tells their rows apart. A group whose reticle was never read says so (“—”), which
            // is also what it says when the shooter leaves for another group — it has not lost
            // the numbers, it never had them.
            if (State.Snapshot.ReticleOf(i) is { } r)
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
                GroupRows.Add(GroupLine.Empty(i, letter));
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
