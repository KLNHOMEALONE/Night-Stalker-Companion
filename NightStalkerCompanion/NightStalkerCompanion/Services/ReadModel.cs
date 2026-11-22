using PardApi;

namespace NightStalkerCompanion.Services;

/// <summary>
/// One observation of the device: everything the screens show at the same moment.
/// Immutable and replaced whole — a partial update would let a screen display a
/// mix of two polls.
/// </summary>
public sealed record SightingSnapshot
{
    /// <summary>True when this came from a live poll, false for a saved snapshot.</summary>
    public required bool IsLive { get; init; }

    /// <summary>
    /// Where the numbers came from: "6019, 6009, 3014" for a live poll,
    /// "СНИМОК 22.11 19:33 — прибор не подключён" for a saved one.
    /// Every screen must show this string — without it a snapshot is
    /// indistinguishable from the truth.
    /// </summary>
    public required string SourceLabel { get; init; }

    public DateTimeOffset TakenAt { get; init; } = DateTimeOffset.Now;

    public string? Firmware { get; init; }
    public string? Model { get; init; }

    public BallisticProfile? Profile { get; init; }

    /// <summary>Reticle of the active group (cmd=6009 as it came back).</summary>
    public Reticle? Reticle { get; init; }

    /// <summary>Answer to 3014: command number → current value.</summary>
    public IReadOnlyDictionary<int, int>? StatusBatch { get; init; }

    /// <summary>
    /// Reticles of all five groups. Only obtainable by walking the groups, which
    /// writes to the device, so this is a separate command rather than part of
    /// every poll. Null means "we have not asked yet", not "there are none".
    /// </summary>
    public IReadOnlyDictionary<int, Reticle>? GroupReticles { get; init; }

    public static SightingSnapshot None { get; } = new()
    {
        IsLive = false,
        SourceLabel = "прибор не опрошен",
    };

    /// <summary>Active group index taken from BallisticData; null when there is no profile.</summary>
    public int? ActiveGroupIndex => Profile is null ? null : (int)Profile.Number("BallisticData").Value;

    /// <summary>How many seconds ago the poll happened. Feeds "прочитано N с назад".</summary>
    public double AgeSeconds => Math.Max(0, (DateTimeOffset.Now - TakenAt).TotalSeconds);
}

/// <summary>Stage of the last write. A state of its own, not a boolean "succeeded".</summary>
public enum WriteStage
{
    /// <summary>No write has happened.</summary>
    Idle,

    /// <summary>Command sent, reading back now.</summary>
    Writing,

    /// <summary>Scope read back and the value matches.</summary>
    Confirmed,

    /// <summary>Read back enough times that "not applied" is now a fact.</summary>
    NotApplied,

    /// <summary>Scope read back a different value — we show both.</summary>
    Mismatch,

    /// <summary>The write did not happen: connection, policy, exception.</summary>
    Failed,
}

/// <summary>
/// What the write status bar shows. Carries the group and what the device
/// actually returned, because "saved" without a read-back means nothing on this
/// firmware: &lt;Status&gt;0&lt;/Status&gt; guarantees nothing.
/// User-facing strings stay Russian: they are the app's localization.
/// </summary>
public sealed record WriteVerdict(
    WriteStage Stage,
    string Field,
    string Sent,
    string? ReadBack = null,
    int? GroupIndex = null,
    int Reads = 0,
    string? Detail = null)
{
    public static WriteVerdict Idle { get; } = new(WriteStage.Idle, "", "");

    /// <summary>Group as a letter, the way the scope labels it for the shooter.</summary>
    public string GroupLabel => GroupIndex is int i && i >= 0 && i < Groups.Count
        ? Groups.All[i].Name()
        : "?";

    /// <summary>
    /// Top line of the write status bar. Worded so that "saved" only ever appears
    /// together with a read-back.
    /// </summary>
    public string Headline => Stage switch
    {
        WriteStage.Idle => "Записей ещё не было",
        WriteStage.Writing => $"Пишем {Field}… перечитываем группу {GroupLabel}",
        WriteStage.Confirmed => $"Записано в группу {GroupLabel} · подтверждено · {Field} = {ReadBack}",
        WriteStage.NotApplied => $"{Field} не применилось · три перечитывания · прибор держит {ReadBack ?? "пусто"}",
        WriteStage.Mismatch => $"Прицел вернул другое · {Field}: послали {Sent}, читается {ReadBack}",
        WriteStage.Failed => $"Запись не удалась · {Field}",
        _ => "",
    };

    /// <summary>
    /// Bottom line: what the client actually saw. If reading back took more than
    /// one attempt, or the device answered a different group, that has to be visible.
    /// </summary>
    public string Explanation => Stage switch
    {
        WriteStage.Idle =>
            "После записи приложение перечитывает ту же группу и показывает, сошлось ли. "
            + "Без этого строка «сохранено» ничего не значит: запись переключает активную группу.",
        WriteStage.Writing =>
            "6019 отстаёт на одну операцию, поэтому перечитываем не один раз "
            + $"({Reads}/{PardService.ReadbackAttempts} с шагом {PardService.ReadbackDelay.TotalSeconds:0.#} с).",
        WriteStage.Confirmed =>
            Detail ?? $"Перечитано группой {GroupLabel}, совпало.",
        WriteStage.NotApplied or WriteStage.Mismatch =>
            ((Detail ?? "") + $" Перечитываний: {Reads}. Поле не считается записанным.").Trim(),
        WriteStage.Failed => Detail ?? "Запрос не прошёл.",
        _ => "",
    };
}

/// <summary>One row of the drop table.</summary>
public sealed record DropRow(string Distance, string Drop, string FlightTime, bool IsZero);

/// <summary>
/// One row of the group list: letter, reticle, notes, whether it is active.
/// The text is composed here so that the list on the main screen and the list on
/// the groups screen cannot describe the same group differently.
/// </summary>
public sealed record GroupLine(
    int Index,
    string Letter,
    string ReticleText,
    string Detail,
    bool IsActive,
    int? PreviewStyle)
{
    /// <summary>Preview asset name, or null when the group has no reticle at all.</summary>
    public string? Preview => ReticleInfo.PreviewAsset(PreviewStyle ?? ReticleInfo.OffStyle);

    /// <summary>Right-hand caption of the row.</summary>
    public string Tail => IsActive ? "активная" : "›";

    public static GroupLine Empty(int index, string letter) => new(index, letter, "—", "", false, null);
}
