using PardApi;

namespace NightStalkerCompanion.Services;

/// <summary>
/// Talks to a live scope through <see cref="PardClient"/>.
///
/// The one rule of this class: no write is ever considered successful because the
/// device answered nicely. &lt;Status&gt;0&lt;/Status&gt; means nothing on this
/// firmware (notes/CMD_TABLE.md), so after every write the same group is read
/// back — and not once. 6019 lags by one operation, so a single read-back
/// produces a false "not applied" (notes/HANDOFF.md). A verdict of "not applied"
/// is only allowed after <see cref="ReadbackAttempts"/> read-backs.
///
/// Strings shown to the user stay Russian: they are the app's localization.
/// </summary>
public sealed class PardService : IPardService, IDisposable
{
    /// <summary>How many read-backs it takes to be allowed to say "not applied".</summary>
    public const int ReadbackAttempts = 3;

    /// <summary>Delay between read-backs. Shorter catches the 6019 lag.</summary>
    public static readonly TimeSpan ReadbackDelay = TimeSpan.FromMilliseconds(1500);

    private readonly Func<PardClient> clientFactory;
    private PardClient? client;

    public PardService(string host = PardClient.DefaultHost)
        => clientFactory = () => new PardClient(host);

    /// <summary>For tests: build the client yourself (mock scope, echo server).</summary>
    public PardService(Func<PardClient> clientFactory)
        => this.clientFactory = clientFactory;

    private PardClient Client => client ??= clientFactory();

    /// <summary>
    /// How long to wait for the probe. Deliberately much shorter than the client's
    /// 5 s: when the scope is not on the network, waiting the full timeout on four
    /// parallel reads leaves the dashboard showing dashes for five seconds before it
    /// can admit it is looking at a saved snapshot.
    /// </summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(1200);

    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probe.CancelAfter(ProbeTimeout);
        try
        {
            var reply = await Client.GetAsync("custom=1&cmd=3012", probe.Token).ConfigureAwait(false);
            return reply.HttpStatus == 200;
        }
        catch (Exception)
        {
            // Timeout, refusal, a captive portal that answers with HTML — all of them
            // mean "not the scope", and the caller says so out loud.
            return false;
        }
    }

    public async Task<SightingSnapshot> ReadAsync(CancellationToken ct = default)
    {
        var errors = new List<string>();

        // Every read stands on its own: over Wi-Fi the scope does not always answer
        // everything at once, and losing the whole screen because the firmware name
        // did not come back is not a good trade.
        var firmwareTask = Safe(() => Client.ModelAsync(ct), "3012 прошивка", errors);
        var profileTask = Safe(() => Client.ReadProfileAsync(ct), "6019 профиль", errors);
        var reticleTask = Safe(() => Client.ReadReticleAsync(ct), "6009 марка", errors);
        var batchTask = Safe(() => Client.ReadStatusBatchAsync(ct), "3014 состояние", errors);
        await Task.WhenAll(firmwareTask, profileTask, reticleTask, batchTask).ConfigureAwait(false);

        var firmware = await firmwareTask.ConfigureAwait(false);
        var profile = await profileTask.ConfigureAwait(false);
        var reticle = await reticleTask.ConfigureAwait(false);
        var batch = await batchTask.ConfigureAwait(false);

        if (profile is null && reticle is null && batch is null)
            throw new ScopeUnreachableException(
                "ни одна команда чтения не прошла: " + string.Join("; ", errors));

        var source = "6019 профиль"
                     + (reticle is null ? "" : ", 6009 марка")
                     + (batch is null ? "" : ", 3014 состояние");
        if (errors.Count > 0)
            source += " · нет: " + string.Join(", ", errors.Select(e => e.Split(':')[0]));

        return new SightingSnapshot
        {
            IsLive = true,
            SourceLabel = source,
            Firmware = firmware,
            Profile = profile,
            Reticle = reticle,
            StatusBatch = batch,
            GroupReticles = GroupCache,
        };
    }

    /// <summary>
    /// Reticles of the five groups survive between polls: they cost a write per
    /// group to obtain, and they do not change on their own.
    /// </summary>
    private IReadOnlyDictionary<int, Reticle>? GroupCache { get; set; }

    public async Task<IReadOnlyDictionary<int, Reticle>> ReadAllGroupsAsync(CancellationToken ct = default)
    {
        var found = new Dictionary<int, Reticle>();

        // The walk writes to the device: reading the reticle of group B means
        // selecting group B by writing BallisticData. Remember where we started so
        // the finally block can put the shooter back on their own group.
        int? origin = null;
        try
        {
            var before = await Client.ReadProfileAsync(ct).ConfigureAwait(false);
            origin = (int)before.Number("BallisticData").Value;
        }
        catch (Exception)
        {
            // No profile means there is nothing to restore; the walk is still worth
            // doing, and the next poll will report the group we ended on.
        }

        try
        {
            for (int g = 0; g < Groups.Count; g++)
            {
                await SelectGroupCoreAsync(g, ct).ConfigureAwait(false);
                found[g] = await Client.ReadReticleAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (origin is int back)
            {
                try { await SelectGroupCoreAsync(back, ct).ConfigureAwait(false); }
                catch (Exception) { /* failing to restore the group is reported by the next read */ }
            }
        }

        GroupCache = found;
        return found;
    }

    public async Task<WriteVerdict> WriteFieldAsync(
        string field, string value, int groupIndex, CancellationToken ct = default)
    {
        if (!FieldPolicy.IsWritable(field))
            return new WriteVerdict(WriteStage.Failed, field, value, GroupIndex: groupIndex,
                Detail: $"поле не пишется этой командой: {FieldPolicy.Reason(field)}");

        var target = new WriteVerdict(WriteStage.Writing, field, value, GroupIndex: groupIndex);

        string? before;
        try
        {
            var profile = await Client.ReadProfileAsync(ct).ConfigureAwait(false);
            before = profile.Raw.TryGetValue(field, out var b) ? b : null;
        }
        catch (Exception e)
        {
            return target with { Stage = WriteStage.Failed, Detail = "не прочитан профиль до записи: " + e.Message };
        }

        try
        {
            // par= does not select a group (verified on the device), so the group is
            // set by the BallisticData field — always last, the way
            // FieldPolicy.WriteOrder does it: any write resets it to 0.
            await Client.GetAsync(
                $"custom=1&cmd=6018&par=0&{Uri.EscapeDataString(field)}={Uri.EscapeDataString(value)}", ct)
                .ConfigureAwait(false);
            await SelectGroupCoreAsync(groupIndex, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return target with { Stage = WriteStage.Failed, Detail = e.Message };
        }

        return await VerifyProfileFieldAsync(target, field, value, groupIndex, before, ct).ConfigureAwait(false);
    }

    public async Task<WriteVerdict> WriteFieldsAsync(
        IReadOnlyDictionary<string, string> values, int groupIndex, CancellationToken ct = default)
    {
        // BallisticData goes last: any write resets it to 0, and it is also how the
        // group is chosen (FieldPolicy.WriteOrder encodes exactly that).
        var fields = FieldPolicy.WriteOrder(values.Keys).ToList();
        var refused = values.Keys.Where(f => !FieldPolicy.IsWritable(f)).ToList();

        if (fields.Count == 0)
            return new WriteVerdict(WriteStage.Failed, "профиль", "", GroupIndex: groupIndex,
                Detail: refused.Count > 0
                    ? "ни одно из полей этой командой не пишется: " + string.Join(", ", refused)
                    : "писать нечего");

        var batch = fields.Count == 1
            ? fields[0]
            : $"{fields[0]} и ещё {fields.Count - 1}";

        foreach (var field in fields)
        {
            var one = await WriteFieldAsync(field, values[field], groupIndex, ct).ConfigureAwait(false);
            if (one.Stage != WriteStage.Confirmed)
                // Stop on the first field the device did not take: continuing would
                // leave half the batch applied and the rest silently skipped.
                return one with { Field = batch, Detail = one.Detail + $" Остановились на {field}." };
        }

        return new WriteVerdict(WriteStage.Confirmed, batch, "",
            ReadBack: $"{fields.Count} пол.",
            GroupIndex: groupIndex,
            Reads: fields.Count,
            Detail: refused.Count > 0
                ? $"Перечитано, совпало. Полей без записи: {string.Join(", ", refused)}."
                : "Перечитано каждой поле, совпало.");
    }

    private async Task<WriteVerdict> VerifyProfileFieldAsync(
        WriteVerdict target, string field, string value, int groupIndex, string? before, CancellationToken ct)
    {
        string? readBack = null;
        int groupSeen = -1;

        for (int i = 1; i <= ReadbackAttempts; i++)
        {
            await Task.Delay(ReadbackDelay, ct).ConfigureAwait(false);
            BallisticProfile profile;
            try
            {
                profile = await Client.ReadProfileAsync(ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                return target with { Stage = WriteStage.Failed, Reads = i, Detail = "перечитывание не удалось: " + e.Message };
            }

            readBack = profile.Raw.TryGetValue(field, out var v) ? v : null;
            groupSeen = (int)profile.Number("BallisticData").Value;

            if (Same(value, readBack) && groupSeen == groupIndex)
                return target with
                {
                    Stage = WriteStage.Confirmed,
                    ReadBack = readBack,
                    Reads = i,
                    Detail = i > 1 ? $"сработало со {i}-го перечитывания — 6019 отстал" : null,
                };
        }

        // Three read-backs disagreed. Only now is a negative verdict allowed to exist.
        bool sameGroup = groupSeen == groupIndex;
        var stage = readBack == before ? WriteStage.NotApplied : WriteStage.Mismatch;
        var detail = sameGroup
            ? null
            : $"перечитали группу {Groups.All[Math.Clamp(groupSeen, 0, Groups.Count - 1)].Name()}, а не "
              + $"{Groups.All[groupIndex].Name()}: запись переключает активную группу";

        return target with
        {
            Stage = stage,
            ReadBack = readBack,
            Reads = ReadbackAttempts,
            GroupIndex = sameGroup ? groupIndex : groupSeen,
            Detail = detail,
        };
    }

    public async Task<WriteVerdict> WriteReticleAsync(
        int groupIndex, int style, int? color, int? posX, int? posY, CancellationToken ct = default)
    {
        var field = groupIndex >= 0 && groupIndex < Groups.Count
            ? $"марка группы {Groups.All[groupIndex].Name()}"
            : "марка";
        var target = new WriteVerdict(WriteStage.Writing, field, style.ToString(), GroupIndex: groupIndex);

        Reticle current;
        try
        {
            current = await Client.ReadReticleAsync(ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return target with { Stage = WriteStage.Failed, Detail = "6009 не прочитана: " + e.Message };
        }

        var desired = current
            .ForGroup(groupIndex)
            .WithStyle(style)
            .WithColor(color ?? current.Color);

        if (posX is int x && posY is int y)
            desired = desired.WithOffsets(x, y);

        try
        {
            // cmd=6001 writes several fields at once and PardClient reads it back
            // internally; 6009 was never observed to lag (notes/APP_FIELDS.md).
            var after = await Client.WriteReticleAsync(desired, ct: ct).ConfigureAwait(false);

            if (after.GroupId != groupIndex)
                return target with
                {
                    Stage = WriteStage.Mismatch,
                    ReadBack = after.Style.ToString(),
                    GroupIndex = after.GroupId,
                    Reads = 1,
                    Detail = $"прицел вернул группу {after.GroupId}, ждали {groupIndex}",
                };

            var wrong = desired.ChangesTo(after);
            return wrong.Count == 0
                ? target with { Stage = WriteStage.Confirmed, ReadBack = after.Style.ToString(), Reads = 1 }
                : target with
                {
                    Stage = WriteStage.Mismatch,
                    ReadBack = after.Style.ToString(),
                    Reads = 1,
                    Detail = "перечитывание показало другое: "
                             + string.Join(", ", wrong.Select(kv => $"{kv.Key}={kv.Value}")),
                };
        }
        catch (ReadbackMismatchException e)
        {
            return target with { Stage = WriteStage.Mismatch, ReadBack = e.ReadBack, Reads = 1, Detail = e.Message };
        }
        catch (Exception e)
        {
            return target with { Stage = WriteStage.Failed, Detail = e.Message };
        }
    }

    public async Task<WriteVerdict> SelectGroupAsync(int groupIndex, CancellationToken ct = default)
    {
        var field = FieldPolicy.BallisticData;
        var value = groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var target = new WriteVerdict(WriteStage.Writing, field, value, GroupIndex: groupIndex);

        try
        {
            await SelectGroupCoreAsync(groupIndex, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return target with { Stage = WriteStage.Failed, Detail = e.Message };
        }

        for (int i = 1; i <= ReadbackAttempts; i++)
        {
            await Task.Delay(ReadbackDelay, ct).ConfigureAwait(false);
            try
            {
                var profile = await Client.ReadProfileAsync(ct).ConfigureAwait(false);
                if ((int)profile.Number("BallisticData").Value == groupIndex)
                    return target with { Stage = WriteStage.Confirmed, ReadBack = value, Reads = i };
            }
            catch (Exception e)
            {
                return target with { Stage = WriteStage.Failed, Reads = i, Detail = e.Message };
            }
        }

        return target with { Stage = WriteStage.NotApplied, Reads = ReadbackAttempts };
    }

    private Task<PardReply> SelectGroupCoreAsync(int groupIndex, CancellationToken ct)
        => Client.GetAsync($"custom=1&cmd=6018&par=0&{FieldPolicy.BallisticData}={groupIndex}", ct);

    /// <summary>
    /// Compared as text, the way BallisticProfile.Diff does it: the device answers
    /// "3.00" where we sent "3.0", and a numeric comparison would stay silent
    /// exactly where it should be noisy.
    /// </summary>
    private static bool Same(string sent, string? readBack)
        => string.Equals(sent, readBack, StringComparison.Ordinal);

    private static async Task<T?> Safe<T>(Func<Task<T>> f, string what, List<string> errors)
    {
        try { return await f().ConfigureAwait(false); }
        catch (Exception e) { errors.Add($"{what}: {e.GetType().Name}"); return default; }
    }

    public void Dispose() => client?.Dispose();
}

/// <summary>The scope answered none of the read commands.</summary>
public sealed class ScopeUnreachableException : Exception
{
    public ScopeUnreachableException(string message) : base(message) { }
}
