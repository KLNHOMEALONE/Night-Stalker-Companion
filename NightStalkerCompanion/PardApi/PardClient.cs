using System.Net.Http;
using System.Text.RegularExpressions;

namespace PardApi;

/// <summary>What the scope answered: code, body, envelope parse.</summary>
public sealed record PardReply(int HttpStatus, string Body, int? Cmd, int? Status)
{
    public bool HasEnvelope => Cmd.HasValue && Status.HasValue;
    /// <summary>
    /// The trap that breaks every blind sweep: the scope answers
    /// <c>&lt;Cmd&gt;3027&lt;/Cmd>&lt;Status&gt;0&lt;/Status&gt;</c>
    /// to ANY POST body, including an invented command name. So <c>Status == 0</c> does
    /// not mean "the command was understood", it only means "the hfs module is alive".
    /// The one real sign that a write took effect is reading it back.
    /// </summary>
    public bool IsGenericPostAnswer => Cmd == 3027 && Status == 0;
}

/// <summary>
/// Transport and command parsing for the PARD Night Stalker 4K scope.
///
/// Everything the device offers over Wi-Fi is a GET on <c>http://192.168.1.254</c>:
///   <c>/?custom=1&amp;cmd=N</c>            reading (only reads have a body);
///   <c>/?custom=1&amp;cmd=N&amp;par=P&amp;Field=value</c>   writing;
///   <c>/?custom=1&amp;cmd=N&amp;str=text</c>                 string arguments.
/// The scope does not parse POST, and answers commands without a body with an empty
/// <c>&lt;Status&gt;0&lt;/Status&gt;</c> while still doing something, so the client comes here
/// only with commands whose body has actually been observed, or with a write that
/// is always followed by a read-back.
/// </summary>
public sealed class PardClient : IDisposable
{
    public const string DefaultHost = "192.168.1.254";

    readonly HttpClient http;
    readonly string baseUrl;

    public PardClient(string host = DefaultHost, TimeSpan? timeout = null)
    {
        baseUrl = host.StartsWith("http", StringComparison.Ordinal) ? host.TrimEnd('/')
                                                                   : "http://" + host.TrimEnd('/');
        http = new HttpClient
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
            // The scope holds the connection open and dislikes reuse between
            // rapid requests; keep-alive only gets in the way here.
        };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PardApi/1.0");
        http.DefaultRequestHeaders.ConnectionClose = false;
    }

    static readonly Regex Envelope = new(
        @"<Cmd>(?<cmd>-?\d+)</Cmd>\s*<Status>(?<st>-?\d+)</Status>", RegexOptions.Singleline);

    /// <summary>One GET and one envelope parse. Throws on a network failure.</summary>
    public async Task<PardReply> GetAsync(string query, CancellationToken ct = default)
    {
        var url = baseUrl + "/?" + query.TrimStart('?', '/');
        using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var m = Envelope.Match(body);
        return new PardReply((int)resp.StatusCode, body,
            m.Success ? int.Parse(m.Groups["cmd"].Value) : null,
            m.Success ? int.Parse(m.Groups["st"].Value) : null);
    }

    public Task<string> GetBodyAsync(string query, CancellationToken ct = default)
        => GetAsync(query, ct).ContinueWith(r => r.Result.Body, ct);

    /// <summary>Firmware number. After a failed update the device answers 0.</summary>
    public async Task<int> FirmwareAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=1003", ct).ConfigureAwait(false);
        var m = Regex.Match(r.Body, "<Value>(?<v>-?\\d+)</Value>");
        return m.Success ? int.Parse(m.Groups["v"].Value) : -1;
    }

    /// <summary>Build name, for example <c>PARD_Night Stalker 4K_EN_20241212</c>.</summary>
    public async Task<string> ModelAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=3012", ct).ConfigureAwait(false);
        var m = Regex.Match(r.Body, "<String>(?<v>.*?)</String>", RegexOptions.Singleline);
        return m.Success ? m.Groups["v"].Value : "";
    }

    /// <summary>The whole ballistic profile.</summary>
    public async Task<BallisticProfile> ReadProfileAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=6019", ct).ConfigureAwait(false);
        return BallisticProfile.FromResponse(r.Body);
    }

    /// <summary>
    /// The state of neighbouring commands in a single <c>3014</c>: the scope itself
    /// returns a list of <c>&lt;Cmd&gt;/&lt;Status&gt;</c> pairs, where Status is the current
    /// value. Cheaper than ten separate requests and, more importantly, safe:
    /// this is a read.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, int>> ReadStatusBatchAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=3014", ct).ConfigureAwait(false);
        var result = new Dictionary<int, int>();
        foreach (Match m in Regex.Matches(r.Body,
                     @"<Cmd>(?<c>-?\d+)</Cmd>\s*<Status>(?<s>-?\d+)</Status>", RegexOptions.Singleline))
        {
            var cmd = int.Parse(m.Groups["c"].Value);
            if (cmd == 3014) continue;
            result[cmd] = int.Parse(m.Groups["s"].Value);
        }
        return result;
    }

    /// <summary>The current reticle (cmd=6009).</summary>
    public async Task<Reticle> ReadReticleAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=6009", ct).ConfigureAwait(false);
        return Reticle.Parse(r.Body);
    }

    /// <summary>
    /// Change the reticle. Written as several fields in one command, the way the stock
    /// app does it: <c>cmd=6001&amp;par=&amp;Style=4&amp;Color=1</c>.
    /// The read-back check is mandatory: <c>&lt;Status&gt;0&lt;/Status&gt;</c>
    /// means nothing on this device.
    /// </summary>
    /// <param name="validateStyle">
    /// false when a style outside the confirmed range 0..6 has to be written
    /// (0 is not an error here: that is <see cref="Reticle.OffStyle"/> — the reticle is off).
    /// </param>
    public async Task<Reticle> WriteReticleAsync(
        Reticle target, bool validateStyle = true, CancellationToken ct = default)
    {
        var before = await ReadReticleAsync(ct).ConfigureAwait(false);
        if (validateStyle && (target.Style < 0 || target.Style > Reticle.MaxStyle))
            throw new ArgumentOutOfRangeException(nameof(target),
                $"Style={target.Style} вне подтверждённого диапазона 0..{Reticle.MaxStyle} " +
                $"(0 = {nameof(Reticle.OffStyle)}, марка выключена); " +
                "если он точно верный, пишите с validateStyle: false");

        var changes = before.ChangesTo(target);
        if (changes.Count == 0) return before;

        var q = "custom=1&cmd=6001&par=0&" + string.Join("&",
            changes.Select(kv => $"{kv.Key}={kv.Value}"));
        var reply = await GetAsync(q, ct).ConfigureAwait(false);
        if (!reply.HasEnvelope)
            throw new InvalidOperationException($"нет конверта в ответе на запись марки: {reply.Body}");

        var after = await ReadReticleAsync(ct).ConfigureAwait(false);
        foreach (var kv in changes)
        {
            var got = kv.Key switch
            {
                "Style" => after.Style.ToString(),
                "Color" => after.Color.ToString(),
                "GroupId" => after.GroupId.ToString(),
                "PosX" => after.PosX.ToString(),
                "PosY" => after.PosY.ToString(),
                _ => kv.Value
            };
            if (got != kv.Value)
                throw new ReadbackMismatchException("марка." + kv.Key, kv.Value, got);
        }
        return after;
    }

    /// <summary>Set the style only, quickly, leaving everything else alone.</summary>
    public Task<Reticle> WriteStyleAsync(int style, bool validateStyle = true, CancellationToken ct = default)
        => ReadReticleAsync(ct).ContinueWith(
               r => WriteReticleAsync(r.Result.WithStyle(style), validateStyle, ct), ct).Unwrap();

    /// <summary>
    /// Write one profile field. Always with a read-back: the answer
    /// <c>&lt;Status&gt;0&lt;/Status&gt;</c> guarantees nothing.
    /// </summary>
    /// <param name="group">
    /// The value of <c>par</c>. In live writes the stock app sends <c>par=0</c>;
    /// there is reason to think this is the zeroing group index, and for some fields it
    /// matters (see notes/CMD_TABLE.md, the section "ОТЛОЖЕНО: DistanceSwitch").
    /// </param>
    /// <exception cref="InvalidOperationException">the field is not writable by policy.</exception>
    /// <exception cref="ReadbackMismatchException">the scope returned a different value.</exception>
    public async Task<BallisticProfile> WriteFieldAsync(
        string field, string value, int group = 0, CancellationToken ct = default)
    {
        if (!FieldPolicy.IsWritable(field))
            throw new InvalidOperationException(
                $"поле '{field}' намеренно не пишется этим клиентом: {FieldPolicy.Reason(field)}");

        var before = await ReadProfileAsync(ct).ConfigureAwait(false);
        if (Snapshot is null) Snapshot = before;   // so RestoreAsync works after a single write as well
        var beforeWire = before.Raw.TryGetValue(field, out var b) ? b : "";

        var q = $"custom=1&cmd=6018&par={group}&{Uri.EscapeDataString(field)}={Uri.EscapeDataString(value)}";
        var reply = await GetAsync(q, ct).ConfigureAwait(false);
        if (!reply.HasEnvelope)
            throw new InvalidOperationException(
                $"нет конверта в ответе на запись {field}: {reply.Body}");

        var after = await ReadProfileAsync(ct).ConfigureAwait(false);
        var readBack = after.Raw.TryGetValue(field, out var a) ? a : "";
        if (readBack != value && !NumericallyEqual(readBack, value))
            throw new ReadbackMismatchException(field, value, readBack);

        LastWrite = (field, beforeWire, value, group, reply.Status ?? -1);
        return after;
    }

    /// <summary>What was written last: field, value before, after, group, status.</summary>
    public (string Field, string Before, string After, int Group, int Status)? LastWrite { get; private set; }

    /// <summary>
    /// Write several fields at once in the right order: BallisticData always
    /// last, because any profile write resets it to 0. Takes a full profile snapshot
    /// first, so the caller can roll back with Diff.
    /// </summary>
    public async Task<BallisticProfile> WriteFieldsAsync(
        IReadOnlyDictionary<string, string> values, int group = 0, CancellationToken ct = default)
    {
        var profile = await ReadProfileAsync(ct).ConfigureAwait(false);
        Snapshot = profile;
        LastSkipped = values.Keys.Where(f => !FieldPolicy.IsWritable(f)).ToList();
        BallisticProfile last = profile;
        foreach (var field in FieldPolicy.WriteOrder(values.Keys))
        {
            if (!values.TryGetValue(field, out var value)) continue;
            if (profile.Raw.TryGetValue(field, out var current) && NumericallyEqual(current, value))
                continue;   // already that value
            last = await WriteFieldAsync(field, value, group, ct).ConfigureAwait(false);
            profile = last;
        }
        return profile;
    }

    /// <summary>Fields the caller asked to write but the policy refused.</summary>
    public IReadOnlyList<string> LastSkipped { get; private set; } = Array.Empty<string>();

    /// <summary>The profile snapshot before the last batch write (for rollback).</summary>
    public BallisticProfile? Snapshot { get; private set; }

    /// <summary>Put the snapshot back. Contested fields are left alone by policy.</summary>
    public async Task<BallisticProfile?> RestoreAsync(CancellationToken ct = default)
    {
        if (Snapshot is null) return null;
        var current = await ReadProfileAsync(ct).ConfigureAwait(false);
        var changes = new Dictionary<string, string>();
        foreach (var f in BallisticProfile.Diff(current, Snapshot))
            if (FieldPolicy.IsWritable(f))
                changes[f] = Snapshot[f];
        if (changes.Count == 0) return current;
        return await WriteFieldsAsync(changes, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The scope answers <c>3.00</c> to a sent <c>3.0</c>, so the comparison is
    /// numeric, not textual. Non-numeric values compare as strings.
    /// </summary>
    static bool NumericallyEqual(string a, string b) =>
        double.TryParse(a, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var x)
        && double.TryParse(b, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var y)
            ? Math.Abs(x - y) < 1e-9
            : string.Equals(a, b, StringComparison.Ordinal);

    public void Dispose() => http.Dispose();
}
