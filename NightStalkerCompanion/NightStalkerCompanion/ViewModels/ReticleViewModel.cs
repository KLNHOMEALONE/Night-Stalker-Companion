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

    /// <summary>
    /// The reticle this screen edits: this group's, recognised by the GroupId the scope put in
    /// the answer. Not “the active group's reticle”: a style write switches the active group, and
    /// pairing the reticle with whatever BallisticData says afterwards left the screen holding
    /// nothing — the highlighted tile went blank and the tapped one never lit, because the screen
    /// had decided it was no longer looking at this group.
    /// </summary>
    public Reticle? Edited => State.Snapshot.ReticleOf(targetGroup);

    /// <summary>Whether this group's reticle is known at all — the tiles and steppers need it.</summary>
    public bool IsOnTarget => Edited is not null;

    public string GroupCaption =>
        PardApi.Groups.TryFromIndex(targetGroup, out var g) ? $"группа {g.Name()}" : "группа ?";

    public string ColorCaption => Edited is { } r ? ReticleInfo.ColorLabel(r.Color) : "—";

    public int PosX => Edited?.PosX ?? 0;
    public int PosY => Edited?.PosY ?? 0;

    // The input field is a draft: until the screen has written something it holds what the
    // user typed; after any write (and after "-" / "+", which write immediately) the draft
    // is thrown away and the field shows the device number again. Otherwise the screen
    // would show one thing while the device held another.
    private string? xInput;
    private string? yInput;

    /// <summary>
    /// Offset X as the input field sees it: the value being typed, and while nobody is
    /// typing, the value written in the scope.
    /// </summary>
    public string XInput
    {
        get => xInput ?? OffsetInput.Format(PosX);
        set => SetProperty(ref xInput, value);
    }

    /// <summary>Offset Y, the same thing.</summary>
    public string YInput
    {
        get => yInput ?? OffsetInput.Format(PosY);
        set => SetProperty(ref yInput, value);
    }

    /// <summary>
    /// Offsets are firmware units. The scale in centimetres per 100 m is unknown to
    /// the firmware, so the screen shows the raw number instead of inventing
    /// "mm per 100 m" — the mock-up's tag says exactly this.
    /// </summary>
    public string OffsetTag =>
        "Смещение задаётся в единицах прошивки и пока не откалибровано в сантиметры "
        + "на 100 м: масштаб в прошивке неизвестен, поэтому показываем как есть. "
        + $"Y при этом держится в пределах ±{OffsetLimits.YLimit}, у X предела нет — "
        + "сколько прицел примет, столько он и держит.";

    private string? offsetRefusal;

    /// <summary>
    /// Why the last attempt to write an offset failed. Empty when there is nothing to refuse.
    /// This is not the write verdict and it does not live by its rules: the card stays on
    /// screen through <see cref="Rebuild"/> so the user has time to read it, and goes away
    /// on its own once the next attempt succeeds.
    /// </summary>
    public string? OffsetRefusal
    {
        get => offsetRefusal;
        private set
        {
            if (SetProperty(ref offsetRefusal, value))
                OnPropertyChanged(nameof(HasOffsetRefusal));
        }
    }

    public bool HasOffsetRefusal => !string.IsNullOrEmpty(offsetRefusal);

    /// <summary>
    /// The refusal is shared by both ways in, the field and the steppers: the device has one
    /// limit, and it should not matter which side of it you approached from.
    /// </summary>
    private static string Refusal(string axisName, bool fromField) =>
        $"Смещение {axisName} вне предела ±{OffsetLimits.YLimit}: прицел такой величины не держит. "
        + (fromField
            ? "Ничего не записано, поле снова показывает то, что держит прибор."
            : "Дальше степпер не идёт, ничего не записано.");

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
        bool xAxis = parts[0] == "X";
        int delta = int.Parse(parts[1]);
        int moved = (xAxis ? r.PosX : r.PosY) + delta;

        // Without this check the steppers would walk past the very limit the field refuses:
        // eighty taps of "+" from zero reach a Y the scope cannot hold, and that road is
        // exactly the one the limit is supposed to close.
        if (!OffsetLimits.Within(xAxis, moved))
        {
            OffsetRefusal = Refusal(xAxis ? "X" : "Y", fromField: false);
            return;
        }

        OffsetRefusal = null;
        int x = xAxis ? r.PosX + delta : r.PosX;
        int y = xAxis ? r.PosY : r.PosY + delta;
        await State.WriteReticleAsync(targetGroup, r.Style, r.Color, x, y);
        Rebuild();
    }

    /// <summary>
    /// Write the offset typed into the field - one press instead of two hundred "+".
    /// An empty or unreadable field writes nothing: the device keeps the offset it already
    /// has, and the field returns to it rather than lying about it.
    /// </summary>
    [RelayCommand]
    private async Task ApplyOffsetAsync(string axis)
    {
        if (Edited is not { } r) return;

        bool xAxis = axis == "X";
        OffsetRefusal = null;
        if (!OffsetInput.TryParse(xAxis ? XInput : YInput, out int value))
        {
            Rebuild();
            return;
        }

        // The draft is thrown away here too: a number the scope cannot hold must not stay in the
        // field, because the next "Enter" would write it. This differs from malformed input in
        // that the reason has to be said out loud: the device did not reject the value, we did,
        // and quietly putting the old number back would read as nothing having happened.
        if (!OffsetLimits.Within(xAxis, value))
        {
            OffsetRefusal = Refusal(xAxis ? "X" : "Y", fromField: true);
            Rebuild();
            return;
        }

        int x = xAxis ? value : r.PosX;
        int y = xAxis ? r.PosY : value;
        if (x == r.PosX && y == r.PosY)
        {
            Rebuild();
            return;
        }

        await State.WriteReticleAsync(targetGroup, r.Style, r.Color, x, y);
        Rebuild();
    }

    /// <summary>
    /// Flip the sign of what was typed. The iOS numeric keyboard has no minus, so a negative
    /// value has to be reachable without a hardware keyboard.
    /// </summary>
    [RelayCommand]
    private void FlipOffset(string axis)
    {
        bool xAxis = axis == "X";
        if (OffsetInput.TryParse(xAxis ? XInput : YInput, out int value))
        {
            if (xAxis) XInput = OffsetInput.Format(-value);
            else YInput = OffsetInput.Format(-value);
        }
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

        // The input draft lives until any write: from that moment the field has to show
        // the number the device actually holds.
        xInput = yInput = null;
        OnPropertyChanged(nameof(XInput));
        OnPropertyChanged(nameof(YInput));
        OnPropertyChanged(nameof(Readout));
        RaiseDerived();
    }
}

/// <summary>A group chip in the A…E row.</summary>
public sealed record GroupPick(int Index, string Letter, string Sub, bool IsCurrent);

/// <summary>A reticle tile, including the "no reticle" tile with style 0.</summary>
public sealed record StylePick(int Style, string Caption, string? Preview, bool IsCurrent);
