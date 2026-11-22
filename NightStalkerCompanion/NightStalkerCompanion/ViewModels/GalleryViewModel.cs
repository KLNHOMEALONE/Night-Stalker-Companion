using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// Screen 05 — gallery. Second tab, exactly where the original keeps it; what
/// changed is that the grid carries a style badge and the frame of the active
/// group is highlighted.
///
/// The list itself is empty here for one honest reason: media lives on the memory
/// card. The fast way to read it is a card reader, and the scope's own file
/// listing over Wi-Fi was measured at 9—20 seconds per listing
/// (notes/HANDOFF.md), so it is not wired up yet. The screen says which of the two
/// it is waiting for instead of showing an empty black rectangle.
/// </summary>
public partial class GalleryViewModel : ScopePageViewModel
{
    public GalleryViewModel(AppState state) : base(state)
    {
        RebuildChips();
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Snapshot))
            {
                RaiseDerived();
                RebuildChips();
            }
        };
    }

    private void RebuildChips()
    {
        GroupPicks.Clear();
        for (int i = 0; i < PardApi.Groups.Count; i++)
            GroupPicks.Add(new GroupPick(
                i,
                PardApi.Groups.All[i].Name(),
                State.ActiveGroupIndex == i ? "рамка" : "",
                GroupFilter == i));
    }

    public ObservableCollection<MediaItem> Items { get; } = new();

    /// <summary>A…E filter chips. The active group is marked even with no device.</summary>
    public ObservableCollection<GroupPick> GroupPicks { get; } = new();

    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => Items.Count == 0;

    /// <summary>What the group filter is set to. Empty string means "all".</summary>
    public int? GroupFilter { get; private set; }

    public string EmptyNotice =>
        "Записей пока не видно: список держит карта памяти, а не прицел. "
        + "Быстрее всего читать карту в картридере; по Wi‑Fi перечисление файлов "
        + "отстаёт на 9—20 с и пока не подключено.";

    public string GroupFilterNote => GroupFilter is int i
        ? $"Группа {PardApi.Groups.All[i].Name()}: выделение рамки работает, даже когда прицел не подключён."
        : "Рамка кадра — активная группа прицела: по ней видно, какой маркой снят ролик.";

    [RelayCommand]
    private void FilterAll() => SetFilter(null);

    [RelayCommand]
    private void FilterGroup(int index) => SetFilter(index);

    private void SetFilter(int? index)
    {
        GroupFilter = index;
        OnPropertyChanged(nameof(GroupFilterNote));
        RebuildChips();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await State.RefreshCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
    }
}

/// <summary>One clip or photo on the grid.</summary>
public sealed record MediaItem(string Name, string Caption, int? GroupIndex, string? Preview);
