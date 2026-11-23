using System.Globalization;
using System.Text.RegularExpressions;

namespace PardApi;

/// <summary>
/// The reticle: the single object the scope answers with,
/// <c>GET /?custom=1&amp;cmd=6009</c>:
/// <c>{ "GroupId": 0, "PosX": 0, "PosY": 0, "Style": 3, "Color": 1 }</c>.
/// It is written with a different command - <c>cmd=6001</c> - and, unlike the ballistic
/// profile, with several fields in one request (that is how the stock app does it).
/// </summary>
public sealed class Reticle
{
    /// <summary>
    /// The <c>Style</c> value at which there is no reticle at all. Checked
    /// on hardware 22.11 with a frame from the scope's own video: at <c>Style=0</c> the centre of
    /// the frame has zero bright pixels, so this is not "the first reticle" but a turned-off one.
    /// The upper bound is <see cref="MaxStyle"/>.
    /// </summary>
    public const int OffStyle = 0;

    /// <summary>
    /// The upper bound of <c>Style</c>. There are six reticles, they take 1..6, and zero is
    /// <see cref="OffStyle"/>. Confirmed by writing and reading back all seven
    /// values and by screen captures, so the range is a fact now, not a safety margin.
    /// Write past it if you must: <see cref="PardClient.WriteReticleAsync"/>
    /// with <c>validateStyle: false</c>.
    /// </summary>
    public const int MaxStyle = 6;

    public int GroupId { get; }
    public int Style { get; }
    public int Color { get; }
    public int PosX { get; }
    public int PosY { get; }
    public string Raw { get; }

    Reticle(int g, int style, int color, int x, int y, string raw)
    { GroupId = g; Style = style; Color = color; PosX = x; PosY = y; Raw = raw; }

    public static Reticle Parse(string body)
    {
        var m = Regex.Match(body, "<String>(?<v>.*?)</String>", RegexOptions.Singleline);
        var json = (m.Success ? m.Groups["v"].Value : body).Replace("\n", "").Replace("\r", "");
        int Get(string name, int fallback = 0)
        {
            var f = Regex.Match(json, "\"" + name + "\":\\s*(-?\\d+)");
            return f.Success ? int.Parse(f.Groups[1].Value, CultureInfo.InvariantCulture) : fallback;
        }
        if (!Regex.IsMatch(json, @"""Style"""))
            throw new InvalidOperationException("в ответе нет поля Style: " + Trunc(json));
        return new Reticle(Get("GroupId"), Get("Style"), Get("Color"), Get("PosX"), Get("PosY"), json);
    }

    /// <summary>
    /// Whether the reticle is on. False only for <see cref="OffStyle"/>.
    /// About previews: a reticle picture may be drawn only when this is
    /// <see langword="true"/>; otherwise the interface would show a reticle where the scope
    /// shows an empty frame.
    /// </summary>
    public bool IsEnabled => Style != OffStyle;

    /// <summary>
    /// The style that has a preview picture, or <see langword="null"/>
    /// when the reticle is off. Needed so the interface does not draw a preview
    /// where the scope shows an empty frame; assets are stored by style number.
    /// </summary>
    public int? PreviewStyle => IsEnabled ? Style : null;

    /// <summary>A copy with the style replaced. 0 = <see cref="OffStyle"/>, the reticle goes out.</summary>
    public Reticle WithStyle(int style) => new(GroupId, style, Color, PosX, PosY, Raw);

    /// <summary>A copy with the reticle turned off - the same as <c>WithStyle(OffStyle)</c>.</summary>
    public Reticle WithReticleOff() => new(GroupId, OffStyle, Color, PosX, PosY, Raw);

    /// <summary>A copy with a different colour. The range is not checked here —
    /// <see cref="PardClient.WriteReticleAsync"/> validates it on write, and here we
    /// are only building a target object.</summary>
    public Reticle WithColor(int color) => new(GroupId, Style, color, PosX, PosY, Raw);

    /// <summary>A copy with a different offset. The units are firmware units; the
    /// scale in centimetres per 100 m is unknown to the firmware, so there is no
    /// conversion to offer.</summary>
    public Reticle WithOffsets(int x, int y) => new(GroupId, Style, Color, x, y, Raw);

    /// <summary>
    /// A copy aimed at another group: <c>GroupId</c> in the <c>cmd=6009</c> answer is
    /// the group index, while <c>par=</c> in the URL selects nothing (verified on the
    /// device, 22.11). That is why a write target for a group is built this way.
    /// </summary>
    public Reticle ForGroup(int groupId) => new(groupId, Style, Color, PosX, PosY, Raw);

    /// <summary>
    /// A reticle built from scratch, without reading one first — needed to write a
    /// group we know nothing about yet. To edit a reticle you have read, use
    /// <see cref="WithStyle"/>, <see cref="WithColor"/>, <see cref="WithOffsets"/> and
    /// <see cref="ForGroup"/>: they keep everything you did not ask to change, and
    /// <see cref="ChangesTo"/> will not send anything extra.
    /// </summary>
    public static Reticle Create(int groupId, int style, int color, int posX, int posY) =>
        new(groupId, style, color, posX, posY, "");

    /// <summary>The fields we are asking to change - for a batch write.</summary>
    public IReadOnlyDictionary<string, string> ChangesTo(Reticle other)
    {
        var d = new Dictionary<string, string>();
        if (other.GroupId != GroupId) d["GroupId"] = other.GroupId.ToString(CultureInfo.InvariantCulture);
        if (other.Style != Style) d["Style"] = other.Style.ToString(CultureInfo.InvariantCulture);
        if (other.Color != Color) d["Color"] = other.Color.ToString(CultureInfo.InvariantCulture);
        if (other.PosX != PosX) d["PosX"] = other.PosX.ToString(CultureInfo.InvariantCulture);
        if (other.PosY != PosY) d["PosY"] = other.PosY.ToString(CultureInfo.InvariantCulture);
        return d;
    }

    public override string ToString() =>
        $"GroupId={GroupId}, Style={Style}{(IsEnabled ? "" : " (марка выключена)")}, " +
        $"Color={Color}, PosX={PosX}, PosY={PosY}";

    static string Trunc(string s) => s.Length <= 200 ? s : s[..200] + "...";
}
