using System.Globalization;
using System.Text.RegularExpressions;

namespace PardApi;

/// <summary>
/// Прицельная марка: один объект, который прицел отдаёт командой
/// <c>GET /?custom=1&amp;cmd=6009</c>:
/// <c>{ "GroupId": 0, "PosX": 0, "PosY": 0, "Style": 3, "Color": 1 }</c>.
/// Пишется другой командой - <c>cmd=6001</c>, и, в отличие от баллистического
/// профиля, несколькими полями в одном запросе (так делает штатное приложение).
/// </summary>
public sealed class Reticle
{
    /// <summary>
    /// Значение <c>Style</c>, при котором прицельной марки нет вообще. Проверено
    /// на железе 22.11 кадром из видеозаписи прицела: при <c>Style=0</c> в центре
    /// кадра ноль ярких пикселей, то есть это не «первая марка», а выключенная.
    /// Верхняя граница <see cref="MaxStyle"/>.
    /// </summary>
    public const int OffStyle = 0;

    /// <summary>
    /// Верхняя граница <c>Style</c>. Марок шесть, они занимают 1..6, ноль - это
    /// <see cref="OffStyle"/>. Подтверждено записью и перечитыванием всех семи
    /// значений и снимками экранов, так что диапазон теперь факт, а не страховка.
    /// При необходимости пишите за ним: <see cref="PardClient.WriteReticleAsync"/>
    /// с <c>validateStyle: false</c>.
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
    /// Марка сейчас включена. Ложь только при <see cref="OffStyle"/>.
    /// Про превью: рисовать картинку марки можно, только если здесь
    /// <see langword="true"/>; иначе интерфейс покажет марку там, где прицел
    /// показывает пустой кадр.
    /// </summary>
    public bool IsEnabled => Style != OffStyle;

    /// <summary>
    /// Стиль, для которого есть картинка-превью, либо <see langword="null"/>,
    /// когда марка выключена. Нужен затем, чтобы интерфейс не рисовал превью
    /// там, где прицел показывает пустой кадр; ассеты лежат по номеру стиля.
    /// </summary>
    public int? PreviewStyle => IsEnabled ? Style : null;

    /// <summary>Копия с заменой стиля. 0 = <see cref="OffStyle"/>, марка гаснет.</summary>
    public Reticle WithStyle(int style) => new(GroupId, style, Color, PosX, PosY, Raw);

    /// <summary>Копия с выключенной маркой - то же самое, что <c>WithStyle(OffStyle)</c>.</summary>
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

    /// <summary>Поля, у которых мы просим другое значение, - для пакетной записи.</summary>
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
