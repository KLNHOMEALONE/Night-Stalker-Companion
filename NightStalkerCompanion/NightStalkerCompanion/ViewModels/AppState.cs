using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightStalkerCompanion.Services;
using PardApi;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// The device state shared by every screen: one snapshot, one active group, one
/// write status line. Registered as a singleton in DI — screens must not keep
/// their own copy of the same numbers, or "Ballistics" and "Reticle" would argue
/// about which group is active.
/// </summary>
public partial class AppState : ObservableObject, IDisposable
{
    private readonly IPardService scope;
    private readonly Timer ageTicker;

    public AppState(IPardService scope)
    {
        this.scope = scope;
        // "read N seconds ago" has to advance by itself: otherwise the status line
        // lies while the user sits on a screen and touches nothing.
        ageTicker = new Timer(_ => OnMainThread(() => OnPropertyChanged(nameof(AgeText))),
            null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    [ObservableProperty]
    private SightingSnapshot snapshot = SightingSnapshot.None;

    /// <summary>A poll or a write in flight. Screens freeze their fields while true.</summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// True when a saved snapshot is on screen because the device is not reachable.
    /// Every screen has to say so in words instead of quietly passing the numbers off
    /// as live.
    /// </summary>
    [ObservableProperty]
    private bool isUsingSample;

    [ObservableProperty]
    private WriteVerdict verdict = WriteVerdict.Idle;

    /// <summary>What failed in the last poll. Empty when all is well.</summary>
    [ObservableProperty]
    private string? fault;

    /// <summary>Active group index. Null until it has been read.</summary>
    public int? ActiveGroupIndex => Snapshot.ActiveGroupIndex;

    public Group? ActiveGroup
        => ActiveGroupIndex is int i && Groups.TryFromIndex(i, out var g) ? g : null;

    /// <summary>The "прочитано N с назад" caption, or its honest absence.</summary>
    public string AgeText
    {
        get
        {
            if (Snapshot.Profile is null && Snapshot.Reticle is null)
                return "данные не прочитаны";
            double s = Snapshot.AgeSeconds;
            if (s < 60) return $"прочитано {s:0} с назад";
            TimeSpan t = TimeSpan.FromSeconds(s);
            return t.TotalHours < 1
                ? $"прочитано {t.TotalMinutes:0} мин назад"
                : $"прочитано {t.TotalHours:0.#} ч назад";
        }
    }

    /// <summary>Which read commands produced this snapshot.</summary>
    public string SourceText => Snapshot.SourceLabel;

    public bool IsLive => Snapshot.IsLive;

    /// <summary>Poll the device; when it is not there, fall back to the snapshot and say so.</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        IsBusy = true;
        Fault = null;
        try
        {
            // Probe first. Without this, a scope that simply is not on the network
            // costs four parallel 5-second timeouts, and the dashboard spends those
            // five seconds showing dashes as if it had measured nothing instead of
            // having not asked yet.
            if (!await scope.IsReachableAsync(ct))
                throw new ScopeUnreachableException(
                    "прибор не отвечает на " + PardApi.PardClient.DefaultHost);

            Snapshot = await scope.ReadAsync(ct);
            IsUsingSample = false;
        }
        catch (Exception e)
        {
            Snapshot = SampleScope.Snapshot();
            IsUsingSample = true;
            Fault = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Walk groups A..E. This writes to the device (switching the active group), so
    /// it stays an explicit command with its own button on one screen only.
    /// </summary>
    [RelayCommand]
    private async Task ReadGroupsAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (IsUsingSample)
        {
            Verdict = SampleScope.NoDevice("марки всех групп", "6009 ×5");
            return;
        }

        IsBusy = true;
        try
        {
            var found = await scope.ReadAllGroupsAsync(ct);
            Snapshot = Snapshot with { GroupReticles = found };
            Verdict = new WriteVerdict(WriteStage.Confirmed, "марки групп A…E", "6009",
                Reads: found.Count, Detail: "активная группа возвращена на место");
            Snapshot = await scope.ReadAsync(ct);
        }
        catch (Exception e)
        {
            Verdict = new WriteVerdict(WriteStage.Failed, "марки групп A…E", "6009", Detail: e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Write a profile field into the active group and read that group back. The
    /// verdict always lands in <see cref="Verdict" /> — no screen may show "saved"
    /// before that.
    /// </summary>
    public async Task<WriteVerdict> WriteFieldAsync(string field, string value, CancellationToken ct = default)
    {
        if (IsUsingSample)
            return Keep(SampleScope.NoDevice(field, value));

        int group = ActiveGroupIndex ?? 0;
        IsBusy = true;
        Verdict = new WriteVerdict(WriteStage.Writing, field, value, GroupIndex: group);
        try
        {
            return Keep(await scope.WriteFieldAsync(field, value, group, ct));
        }
        catch (Exception e)
        {
            return Keep(new WriteVerdict(WriteStage.Failed, field, value, GroupIndex: group, Detail: e.Message));
        }
        finally
        {
            await RefreshQuietlyAsync(ct);
            IsBusy = false;
        }
    }

    /// <summary>Write several fields at once (the ballistics screen's "записать").</summary>
    public async Task<WriteVerdict> WriteFieldsAsync(
        IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        if (IsUsingSample)
            return Keep(SampleScope.NoDevice("профиль", string.Join(", ", values.Keys)));

        int group = ActiveGroupIndex ?? 0;
        var batch = values.Count == 1 ? values.Keys.First() : $"{values.Count} поля";
        IsBusy = true;
        Verdict = new WriteVerdict(WriteStage.Writing, batch, "", GroupIndex: group);
        try
        {
            return Keep(await scope.WriteFieldsAsync(values, group, ct));
        }
        catch (Exception e)
        {
            return Keep(new WriteVerdict(WriteStage.Failed, batch, "", GroupIndex: group, Detail: e.Message));
        }
        finally
        {
            await RefreshQuietlyAsync(ct);
            IsBusy = false;
        }
    }

    /// <summary>Write a group's reticle. Read back the same group, never "whatever it lands on".</summary>
    public async Task<WriteVerdict> WriteReticleAsync(
        int groupIndex, int style, int? color, int? posX, int? posY, CancellationToken ct = default)
    {
        if (IsUsingSample)
            return Keep(SampleScope.NoDevice("марка", style.ToString()));

        IsBusy = true;
        Verdict = new WriteVerdict(WriteStage.Writing, "марка", style.ToString(), GroupIndex: groupIndex);
        try
        {
            return Keep(await scope.WriteReticleAsync(groupIndex, style, color, posX, posY, ct));
        }
        catch (Exception e)
        {
            return Keep(new WriteVerdict(WriteStage.Failed, "марка", style.ToString(),
                GroupIndex: groupIndex, Detail: e.Message));
        }
        finally
        {
            await RefreshQuietlyAsync(ct);
            IsBusy = false;
        }
    }

    /// <summary>Switch the active group (a write of BallisticData).</summary>
    public async Task<WriteVerdict> SelectGroupAsync(int groupIndex, CancellationToken ct = default)
    {
        if (IsUsingSample)
            return Keep(SampleScope.NoDevice("BallisticData", groupIndex.ToString()));

        IsBusy = true;
        Verdict = new WriteVerdict(WriteStage.Writing, FieldPolicy.BallisticData,
            groupIndex.ToString(), GroupIndex: groupIndex);
        try
        {
            var verdict = await scope.SelectGroupAsync(groupIndex, ct);
            Snapshot = await scope.ReadAsync(ct);
            return Keep(verdict);
        }
        catch (Exception e)
        {
            return Keep(new WriteVerdict(WriteStage.Failed, FieldPolicy.BallisticData,
                groupIndex.ToString(), GroupIndex: groupIndex, Detail: e.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshQuietlyAsync(CancellationToken ct)
    {
        try { Snapshot = await scope.ReadAsync(ct); }
        catch (Exception e) { Fault = e.Message; }
    }

    private WriteVerdict Keep(WriteVerdict v)
    {
        Verdict = v;
        return v;
    }

    partial void OnSnapshotChanged(SightingSnapshot value)
    {
        OnPropertyChanged(nameof(AgeText));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(ActiveGroupIndex));
        OnPropertyChanged(nameof(ActiveGroup));
    }

    private static void OnMainThread(Action a) => MainThread.BeginInvokeOnMainThread(a);

    public void Dispose() => ageTicker.Dispose();
}
