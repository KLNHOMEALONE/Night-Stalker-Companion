using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PardApi;

/// <summary>
/// The ballistic calculator profile the scope answers with
/// <c>GET /?custom=1&amp;cmd=6019</c> in a <c>&lt;String&gt;{...}&lt;/String&gt;</c> body.
/// There are exactly twelve fields, named as the device names them; we invent none.
/// </summary>
public sealed class BallisticProfile
{
    /// <summary>Field order is the one the scope answers in. The order matters when shown.</summary>
    public static readonly string[] FieldOrder =
    {
        "DistanceSwitch", "DistanceUnit", "BallisticData", "Velocity", "BulletWt",
        "BulletBC", "Altitude", "Temperature", "ScopeHt", "ZeroRange", "Sharp", "Color"
    };

    /// <summary>Raw string values, exactly as the scope sent them, uninterpreted.</summary>
    public IReadOnlyDictionary<string, string> Raw { get; }

    private BallisticProfile(IReadOnlyDictionary<string, string> raw) => Raw = raw;

    public string this[string field] =>
        Raw.TryGetValue(field, out var v) ? v
        : throw new InvalidOperationException($"поле '{field}' не приходило от прибора");

    public Num Number(string field) =>
        Num.Parse(Raw.TryGetValue(field, out var v) ? v : null);

    public string Xml { get; private init; } = "";

    /// <summary>Parses a <c>cmd=6019</c> answer. Throws when there is no profile in it.</summary>
    public static BallisticProfile FromResponse(string xml)
    {
        // A live device wraps the profile in <String>. Bare JSON is accepted too:
        // so the client survives proxies and firmware that answers JSON directly.
        var m = Regex.Match(xml, "<String>(?<v>.*?)</String>", RegexOptions.Singleline);
        var body = (m.Success ? m.Groups["v"].Value : xml).Replace("\n", "").Replace("\r", "").Trim();
        if (!body.StartsWith("{", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "в ответе нет ни <String> с JSON, ни голого JSON: " + Trunc(xml));
        using var doc = JsonDocument.Parse(body);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.EnumerateObject())
            dict[p.Name] = p.Value.ValueKind == JsonValueKind.String
                ? p.Value.GetString()!
                : p.Value.ToString();
        return new BallisticProfile(dict) { Xml = xml };
    }

    /// <summary>
    /// Fields whose TEXTUAL representation changed. That is the right thing to
    /// compare: the scope answers <c>3.00</c> where we sent <c>3.0</c>, and a
    /// numeric comparison would stay silent exactly where it should make noise.
    /// </summary>
    public static IReadOnlyList<string> Diff(BallisticProfile a, BallisticProfile b) =>
        FieldOrder.Where(f => Get(a, f) != Get(b, f)).ToList();

    static string Get(BallisticProfile p, string f) => p.Raw.TryGetValue(f, out var v) ? v : "<нет>";

    static string Trunc(string s) => s.Length <= 200 ? s : s[..200] + "...";

    public override string ToString() =>
        string.Join(", ", FieldOrder.Where(Raw.ContainsKey).Select(f => $"{f}={Raw[f]}"));
}

/// <summary>
/// A number in the shape the scope sends it: a value plus the original string.
/// The device writes fractional fields (<c>BulletWt</c>, <c>BulletBC</c>, <c>ScopeHt</c>) with
/// varying tail length, so what goes back is the original string, not a
/// recomputed double.
/// </summary>
public readonly struct Num
{
    public string Source { get; }
    public double Value { get; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Source);

    Num(string src, double v) { Source = src; Value = v; }

    public static Num Parse(string? source)
    {
        source ??= "";
        var t = source.Trim();
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return new Num(t, v);
        return new Num(t, double.NaN);
    }

    /// <summary>The string to send back: as many digits as the device sent.</summary>
    public string ToWire() => IsEmpty ? "" : Source;

    /// <summary>A new value in the same invariant format the scope understands.</summary>
    public Num With(double value) =>
        new Num(value.ToString("0.####", CultureInfo.InvariantCulture), value);

    public override string ToString() => string.IsNullOrEmpty(Source) ? "-" : Source;
}

/// <summary>
/// The rule "our code does not touch this field". Checked on the device 22.11:
/// <list type="bullet">
/// <item><description><c>DistanceSwitch</c> accepts only
/// the value 1 through <c>6018</c>; the device rejects writes of 0 and 2 (repeated
/// reads keep returning the old value), and its resting value of 2 cannot be set with
/// this command at all. So the stock app changes it another way - <c>POST /</c> with <c>status=</c>,
/// whose <c>value</c> shape is still unknown. For now the answer is no: quietly leaving
/// the user on a different distance mode is worse than not changing it.</description></item>
/// <item><description><c>Sharp</c> is image sharpness, not ballistics.
/// It writes through <c>6018</c> just fine (0 -&gt; 1 applied on the first
/// read-back), but there is no reason to change it in a ballistic profile, so it
/// stays in the show-only list.</description></item>
/// <item><description>The stock app writes <c>Color</c> through <c>cmd=6001</c>
/// together with the reticle, not through the profile.</description></item>
/// </list>
/// <see cref="DistanceSwitch"/> is shown to the user as it is.
/// </summary>
public static class FieldPolicy
{
    public static readonly IReadOnlySet<string> ReadOnlyFields =
        new HashSet<string>(StringComparer.Ordinal) { "DistanceSwitch", "Sharp", "Color" };

    /// <summary>Written last: any profile write resets it to 0.</summary>
    public const string BallisticData = "BallisticData";

    public static bool IsWritable(string field) => !ReadOnlyFields.Contains(field);

    /// <summary>
    /// Send order: everything ordinary first, BallisticData at the end. Contested fields
    /// (Sharp, Color, DistanceSwitch) are dropped from the order entirely: otherwise
    /// the batch would throw halfway through, having already written half of it.
    /// </summary>
    public static IReadOnlyList<string> WriteOrder(IEnumerable<string> fields)
    {
        var writable = fields.Where(IsWritable).Distinct(StringComparer.Ordinal).ToList();
        return writable.Contains(BallisticData, StringComparer.Ordinal)
            ? writable.Where(f => f != BallisticData).Append(BallisticData).ToList()
            : writable;
    }

    public static string Reason(string field) => field switch
    {
        "DistanceSwitch" => "значение не возвращается назад командой 6018, назначение не подтверждено",
        "Sharp" => "пишется штатным приложением через cmd=6001",
        "Color" => "пишется штатным приложением через cmd=6001",
        _ => ""
    };
}

/// <summary>The scope answered politely, but the read-back showed something else.</summary>
public sealed class ReadbackMismatchException : Exception
{
    public string Field { get; }
    public string Sent { get; }
    public string ReadBack { get; }

    public ReadbackMismatchException(string field, string sent, string readBack)
        : base($"поле '{field}': отправили '{sent}', прицел вернул '{readBack}'. " +
               "Не считайте поле записанным и не продолжайте молча.")
    { Field = field; Sent = sent; ReadBack = readBack; }
}
