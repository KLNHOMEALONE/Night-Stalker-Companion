namespace NightStalkerCompanion.Services;

/// <summary>
/// The single entry point to the riflescope for ViewModels.
///
/// The interface talks about what the screens need — a state snapshot, a field
/// write that is verified by reading back, reticles per group — not about
/// protocol commands. Commands (6018, 6019, 6001, 6009, 3014) stay inside the
/// implementation.
/// </summary>
public interface IPardService
{
    /// <summary>
    /// Is the scope reachable at all. Cheap: its only job is to decide whether
    /// we show live numbers or a saved snapshot.
    /// </summary>
    Task<bool> IsReachableAsync(CancellationToken ct = default);

    /// <summary>One poll: profile, active group's reticle, 3014 batch, firmware name.</summary>
    Task<SightingSnapshot> ReadAsync(CancellationToken ct = default);

    /// <summary>
    /// Walk groups A..E: select a group, read 6009. This writes to the device
    /// (selecting a group means writing BallisticData), so screens call it as an
    /// explicit command, never as part of a normal poll.
    /// </summary>
    Task<IReadOnlyDictionary<int, PardApi.Reticle>> ReadAllGroupsAsync(CancellationToken ct = default);

    /// <summary>
    /// Write one profile field into a group and read that same group back.
    /// Implementations must read back more than once: 6019 lags by one operation,
    /// so a single read-back produces a false "not applied" (notes/HANDOFF.md).
    /// </summary>
    Task<WriteVerdict> WriteFieldAsync(
        string field, string value, int groupIndex, CancellationToken ct = default);

    /// <summary>
    /// Write several profile fields in the order the firmware needs (BallisticData
    /// last), each verified by its own read-back. One verdict describes the batch:
    /// the first field that failed decides it.
    /// </summary>
    Task<WriteVerdict> WriteFieldsAsync(
        IReadOnlyDictionary<string, string> values, int groupIndex, CancellationToken ct = default);

    /// <summary>Write a group's reticle (cmd=6001) and read the same group back (cmd=6009).</summary>
    Task<WriteVerdict> WriteReticleAsync(
        int groupIndex, int style, int? color, int? posX, int? posY, CancellationToken ct = default);

    /// <summary>
    /// Switch the active group. Also a write: BallisticData is a profile field.
    /// State is re-read afterwards so no screen is left showing the previous group.
    /// </summary>
    Task<WriteVerdict> SelectGroupAsync(int groupIndex, CancellationToken ct = default);
}
