using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// Screen 02 — the reticle of one group: six previews and a separate "no reticle",
/// colour, offsets.
///
/// Why opening a group costs a write: the reticle is read with cmd=6009, which
/// answers for the active group only, and <c>par=</c> selects nothing. So aiming
/// the screen at group C means selecting C with BallisticData first. The screen
/// does that openly and the write bar reports it — silently copying group A's
/// colour onto group B would be the alternative, and that is worse.
/// </summary>
[QueryProperty("TargetGroup", "group")]
public partial class ReticleViewModel : ScopePageViewModel
{
    private int targetGroup;

    public ReticleViewModel(AppState state) : base(state)
        => state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Snapshot))
                Rebuild();
        };

    public ObservableCollection<GroupPick> GroupPicks { get; } = new();
    public ObservableCollection<StylePick> StylePicks { get; } = new();

    /// <summary>
    /// Which group the screen is aimed at. Arrives as a query parameter from the
    /// group list; setting it may select that group on the device, which is a write.
    /// </summary>
    public int TargetGroup
    {
        get => targetGroup;
        set
        {
            if (SetProperty(ref targetGroup, value))
                _ = AimAsync(value);
        }
    }

    /// <summary>The reticle the screen edits: only ever the active group's.</summary>
    public Reticle? Edited => IsOnTarget ? State.Snapshot.Reticle : null;

    public bool IsOnTarget => State.ActiveGroupIndex == targetGroup;

    public string GroupCaption =>
        PardApi.Groups.TryFromIndex(targetGroup, out var g) ? $"группа {g.Name()}" : "группа ?";

    public string ColorCaption => Edited is { } r ? ReticleInfo.ColorLabel(r.Color) : "—";

    public int PosX => Edited?.PosX ?? 0;
    public int PosY => Edited?.PosY ?? 0;

    /// <summary>
    /// Offsets are firmware units. The scale in centimetres per 100 m is unknown to
    /// the firmware, so the screen shows the raw number instead of inventing
    /// "mm per 100 m" — the mock-up's tag says exactly this.
    /// </summary>
    public string OffsetTag =>
        "Смещение задаётся в единицах прошивки и пока не откалибровано в сантиметры "
        + "на 100 м: масштаб в прошивке неизвестен, поэтому показываем как есть.";

    public string Readout => Edited is { } r
        ? $"стиль {r.Style} · {ReticleInfo.ColorLabel(r.Color)} · X {r.PosX} · Y {r.PosY}"
        : "марка этой группы ещё не прочитана";

    /// <summary>Point the screen at a group; if it is not active, select it first.</summary>
    public async Task AimAsync(int groupIndex, CancellationToken ct = default)
    {
        if (State.IsUsingSample || State.ActiveGroupIndex == groupIndex)
        {
            Rebuild();
            return;
        }

        await State.SelectGroupAsync(groupIndex, ct);
        Rebuild();
    }

    [RelayCommand]
    private async Task PickGroupAsync(int index) => await AimAsync(index);

    /// <summary>Tap a preview: that becomes the group's style. 0 means "no reticle".</summary>
    [RelayCommand]
    private async Task PickStyleAsync(int style)
    {
        if (Edited is null) return;
        await State.WriteReticleAsync(targetGroup, style, null, null, null);
        Rebuild();
    }

    /// <summary>Colour flips between the two values the device has ever shown.</summary>
    [RelayCommand]
    private async Task ToggleColorAsync()
    {
        if (Edited is not { } r) return;
        int next = r.Color == 1 ? 0 : 1;
        await State.WriteReticleAsync(targetGroup, r.Style, next, null, null);
        Rebuild();
    }

    [RelayCommand]
    private async Task NudgeAsync(string axis)
    {
        if (Edited is not { } r) return;
        // "X:+1" and "Y:-1" arrive as one parameter; the axis decides which offset moves.
        string[] parts = axis.Split(':');
        if (parts.Length != 2) return;
        int dx = parts[0] == "X" ? int.Parse(parts[1]) : 0;
        int dy = parts[0] == "Y" ? int.Parse(parts[1]) : 0;
        await State.WriteReticleAsync(targetGroup, r.Style, r.Color, r.PosX + dx, r.PosY + dy);
        Rebuild();
    }

    private void Rebuild()
    {
        GroupPicks.Clear();
        for (int i = 0; i < PardApi.Groups.Count; i++)
        {
            bool current = i == targetGroup;
            GroupPicks.Add(new GroupPick(
                i,
                PardApi.Groups.All[i].Name(),
                State.ActiveGroupIndex == i ? "активная" : "",
                current));
        }

        StylePicks.Clear();
        int? shown = Edited?.Style;
        for (int style = 1; style <= ReticleInfo.MaxStyle; style++)
            StylePicks.Add(new StylePick(style, style.ToString(), ReticleInfo.PreviewAsset(style), shown == style));

        StylePicks.Add(new StylePick(ReticleInfo.OffStyle, "без марки", null, shown == ReticleInfo.OffStyle));

        OnPropertyChanged(nameof(Edited));
        OnPropertyChanged(nameof(IsOnTarget));
        OnPropertyChanged(nameof(GroupCaption));
        OnPropertyChanged(nameof(ColorCaption));
        OnPropertyChanged(nameof(PosX));
        OnPropertyChanged(nameof(PosY));
        OnPropertyChanged(nameof(Readout));
        RaiseDerived();
    }
}

/// <summary>A group chip in the A…E row.</summary>
public sealed record GroupPick(int Index, string Letter, string Sub, bool IsCurrent);

/// <summary>A reticle tile, including the "no reticle" tile with style 0.</summary>
public sealed record StylePick(int Style, string Caption, string? Preview, bool IsCurrent);
