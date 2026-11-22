using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PardApi;

/// <summary>
/// Профиль баллистического калькулятора, который прицел отдаёт командой
/// <c>GET /?custom=1&amp;cmd=6019</c> в теле <c>&lt;String&gt;{...}&lt;/String&gt;</c>.
/// Полей ровно двенадцать, имена берём у самого прибора и не выдумываем своих.
/// </summary>
public sealed class BallisticProfile
{
    /// <summary>Порядок полей тот, что отдаёт прицел. Порядок важен при выводе.</summary>
    public static readonly string[] FieldOrder =
    {
        "DistanceSwitch", "DistanceUnit", "BallisticData", "Velocity", "BulletWt",
        "BulletBC", "Altitude", "Temperature", "ScopeHt", "ZeroRange", "Sharp", "Color"
    };

    /// <summary>Сырые строковые значения, как их прислал прицел, без интерпретаций.</summary>
    public IReadOnlyDictionary<string, string> Raw { get; }

    private BallisticProfile(IReadOnlyDictionary<string, string> raw) => Raw = raw;

    public string this[string field] =>
        Raw.TryGetValue(field, out var v) ? v
        : throw new InvalidOperationException($"поле '{field}' не приходило от прибора");

    public Num Number(string field) =>
        Num.Parse(Raw.TryGetValue(field, out var v) ? v : null);

    public string Xml { get; private init; } = "";

    /// <summary>Разбирает ответ <c>cmd=6019</c>. Бросает исключение, если профиля там нет.</summary>
    public static BallisticProfile FromResponse(string xml)
    {
        // Живой прибор заворачивает профиль в <String>. Голый JSON тоже принимаем:
        // так клиент не падает на прокси и на прошивках, которые отдают JSON напрямую.
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
    /// Поля, у которых изменилось ТЕКСТОВОЕ представление. Именно так и надо
    /// сравнивать: прицел отдаёт <c>3.00</c> там, где мы послали <c>3.0</c>, и
    /// числовое сравние молчало бы там, где стоит шуметь.
    /// </summary>
    public static IReadOnlyList<string> Diff(BallisticProfile a, BallisticProfile b) =>
        FieldOrder.Where(f => Get(a, f) != Get(b, f)).ToList();

    static string Get(BallisticProfile p, string f) => p.Raw.TryGetValue(f, out var v) ? v : "<нет>";

    static string Trunc(string s) => s.Length <= 200 ? s : s[..200] + "...";

    public override string ToString() =>
        string.Join(", ", FieldOrder.Where(Raw.ContainsKey).Select(f => $"{f}={Raw[f]}"));
}

/// <summary>
/// Число в том виде, в каком его отдаёт прицел: значение плюс исходная строка.
/// Дробные поля (<c>BulletWt</c>, <c>BulletBC</c>, <c>ScopeHt</c>) прицел пишет с
/// разной длиной хвоста, поэтому обратно отправлять надо исходную строку, а не
/// пересчитанный double.
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

    /// <summary>Строка для отправки обратно: с тем же количеством знаков, что прислал прибор.</summary>
    public string ToWire() => IsEmpty ? "" : Source;

    /// <summary>Новое значение в том же инвариантном формате, что понимает прицел.</summary>
    public Num With(double value) =>
        new Num(value.ToString("0.####", CultureInfo.InvariantCulture), value);

    public override string ToString() => string.IsNullOrEmpty(Source) ? "-" : Source;
}

/// <summary>
/// Правило «это поле наш код не трогает». Проверено на прицеле 22.11:
/// <list type="bullet">
/// <item><description><c>DistanceSwitch</c> командой <c>6018</c> принимает только
/// значение 1; записи 0 и 2 прибор отклоняет (перечитывание с повторами оставляет
/// прежнее), а покойное значение 2 этой командой не выставляется вовсе. Значит
/// штатное приложение меняет его другим каналом - <c>POST /</c> со <c>status=</c>,
/// форма <c>value</c> которого ещё неизвестна. Пока пишем запрет: тихо оставить
/// пользователя с другим режимом дистанции - хуже, чем не поменять вовсе.</description></item>
/// <item><description><c>Sharp</c> - это резкость изображения, а не баллистика.
/// Записывается она <c>6018</c> исправно (0 -&gt; 1 применилось с первого
/// перечитывания), но в баллистическом профиле её менять не за чем, поэтому она
/// в списке только показанных.</description></item>
/// <item><description><c>Color</c> штатное приложение пишет через <c>cmd=6001</c>
/// вместе с маркой, а не через профиль.</description></item>
/// </list>
/// <see cref="DistanceSwitch"/> показывается пользователю как есть.
/// </summary>
public static class FieldPolicy
{
    public static readonly IReadOnlySet<string> ReadOnlyFields =
        new HashSet<string>(StringComparer.Ordinal) { "DistanceSwitch", "Sharp", "Color" };

    /// <summary>Пишется последним: любая запись профиля сбрасывает его в 0.</summary>
    public const string BallisticData = "BallisticData";

    public static bool IsWritable(string field) => !ReadOnlyFields.Contains(field);

    /// <summary>
    /// Порядок отправки: сначала всё обычное, BallisticData в конце. Спорные поля
    /// (Sharp, Color, DistanceSwitch) из порядка выбрасываются совсем: иначе
    /// батч падал бы исключением на середине, уже записав половину.
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

/// <summary>Прицел ответил вежливо, но перечитывание показало другое.</summary>
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
