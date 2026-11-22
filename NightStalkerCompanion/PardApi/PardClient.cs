using System.Net.Http;
using System.Text.RegularExpressions;

namespace PardApi;

/// <summary>Что прицел ответил на запрос: код, теле, разбор конверта.</summary>
public sealed record PardReply(int HttpStatus, string Body, int? Cmd, int? Status)
{
    public bool HasEnvelope => Cmd.HasValue && Status.HasValue;
    /// <summary>
    /// Ловушка, на которой сломаны все слепые переборы: прицел отвечает конвертом
    /// <c>&lt;Cmd&gt;3027&lt;/Cmd>&lt;Status&gt;0&lt;/Status&gt;</c> на ЛЮБОЕ тело
    /// POST, включая выдуманное имя команды. Поэтому <c>Status == 0</c> не значит
    /// «команда понята», а значит лишь «модуль hfs жив». Единственный настоящий
    /// признак применённой записи - перечитывание.
    /// </summary>
    public bool IsGenericPostAnswer => Cmd == 3027 && Status == 0;
}

/// <summary>
/// Транспорт и разбор команд прицела PARD Night Stalker 4K.
///
/// Всё, что умеет прибор по Wi-Fi, это GET на <c>http://192.168.1.254</c>:
///   <c>/?custom=1&amp;cmd=N</c>            чтение (тело есть только у чтения);
///   <c>/?custom=1&amp;cmd=N&amp;par=P&amp;Поле=значение</c>   запись;
///   <c>/?custom=1&amp;cmd=N&amp;str=текст</c>                 строковые аргументы.
/// POST прицел не разбирает, а на команды без тела отвечает пустым
/// <c>&lt;Status&gt;0&lt;/Status&gt;</c> и что-то делает, поэтому клиент ходит сюда
/// только теми командами, у которых уже наблюдалось тело, либо записью со
/// обязательным перечитыванием.
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
            // Прицел держит соединение и не любит переиспользование между
            // быстрыми запросами; keep-alive здесь только мешает.
        };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PardApi/1.0");
        http.DefaultRequestHeaders.ConnectionClose = false;
    }

    static readonly Regex Envelope = new(
        @"<Cmd>(?<cmd>-?\d+)</Cmd>\s*<Status>(?<st>-?\d+)</Status>", RegexOptions.Singleline);

    /// <summary>Один GET и разбор конверта. Бросает исключение на сетевом отказе.</summary>
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

    /// <summary>Номер прошивки. После неудачного обновления прибор отдаёт 0.</summary>
    public async Task<int> FirmwareAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=1003", ct).ConfigureAwait(false);
        var m = Regex.Match(r.Body, "<Value>(?<v>-?\\d+)</Value>");
        return m.Success ? int.Parse(m.Groups["v"].Value) : -1;
    }

    /// <summary>Имя сборки, например <c>PARD_Night Stalker 4K_EN_20241212</c>.</summary>
    public async Task<string> ModelAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=3012", ct).ConfigureAwait(false);
        var m = Regex.Match(r.Body, "<String>(?<v>.*?)</String>", RegexOptions.Singleline);
        return m.Success ? m.Groups["v"].Value : "";
    }

    /// <summary>Баллистический профиль целиком.</summary>
    public async Task<BallisticProfile> ReadProfileAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=6019", ct).ConfigureAwait(false);
        return BallisticProfile.FromResponse(r.Body);
    }

    /// <summary>
    /// Пачка состояния соседних команд одной командой <c>3014</c>: прицел сам
    /// возвращает список пар <c>&lt;Cmd&gt;/&lt;Status&gt;</c>, где Status - это
    /// текущее значение. Дешевле десятка отдельных запросов и, главное, безопасно:
    /// это чтение.
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

    /// <summary>Текущая прицельная марка (cmd=6009).</summary>
    public async Task<Reticle> ReadReticleAsync(CancellationToken ct = default)
    {
        var r = await GetAsync("custom=1&cmd=6009", ct).ConfigureAwait(false);
        return Reticle.Parse(r.Body);
    }

    /// <summary>
    /// Сменить марку. Пишем одной командой сразу несколько полей, как это делает
    /// штатное приложение: <c>cmd=6001&amp;par=&amp;Style=4&amp;Color=1</c>.
    /// Проверка перечитыванием обязательна: <c>&lt;Status&gt;0&lt;/Status&gt;</c>
    /// на этом приборе не значит ничего.
    /// </summary>
    /// <param name="validateStyle">
    /// false, если нужно записать стиль вне подтверждённого диапазона 0..6
    /// (0 при этом не ошибка: это <see cref="Reticle.OffStyle"/> - марка выключена).
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

    /// <summary>Быстро поставить только стиль, остальное не трогаем.</summary>
    public Task<Reticle> WriteStyleAsync(int style, bool validateStyle = true, CancellationToken ct = default)
        => ReadReticleAsync(ct).ContinueWith(
               r => WriteReticleAsync(r.Result.WithStyle(style), validateStyle, ct), ct).Unwrap();

    /// <summary>
    /// Записать одно поле профиля. Всегда с перечитыванием: ответ
    /// <c>&lt;Status&gt;0&lt;/Status&gt;</c> ничего не гарантирует.
    /// </summary>
    /// <param name="group">
    /// Значение <c>par</c>. В живых записях штатное приложение шлёт <c>par=0</c>;
    /// есть подозрение, что это индекс группы пристрелки, и для части полей он
    /// значим (см. notes/CMD_TABLE.md, раздел «ОТЛОЖЕНО: DistanceSwitch»).
    /// </param>
    /// <exception cref="InvalidOperationException">поле запрещено к записи политикой.</exception>
    /// <exception cref="ReadbackMismatchException">прицел вернул другое значение.</exception>
    public async Task<BallisticProfile> WriteFieldAsync(
        string field, string value, int group = 0, CancellationToken ct = default)
    {
        if (!FieldPolicy.IsWritable(field))
            throw new InvalidOperationException(
                $"поле '{field}' намеренно не пишется этим клиентом: {FieldPolicy.Reason(field)}");

        var before = await ReadProfileAsync(ct).ConfigureAwait(false);
        if (Snapshot is null) Snapshot = before;   // чтобы RestoreAsync работал и после одиночной записи
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

    /// <summary>Что было записано последним: поле, значение до, после, группа, статус.</summary>
    public (string Field, string Before, string After, int Group, int Status)? LastWrite { get; private set; }

    /// <summary>
    /// Записать сразу несколько полей в правильном порядке: BallisticData всегда
    /// последним, потому что любая запись профиля сбрасывает его в 0. Перед началом
    /// снимает профиль целиком, чтобы вызывающий мог откатиться по Diff.
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
                continue;                                   // и так такое значение
            last = await WriteFieldAsync(field, value, group, ct).ConfigureAwait(false);
            profile = last;
        }
        return profile;
    }

    /// <summary>Поля, которые вызывающий просил записать, но политика не позволила.</summary>
    public IReadOnlyList<string> LastSkipped { get; private set; } = Array.Empty<string>();

    /// <summary>Снимок профиля перед последней пакетной записью (для отката).</summary>
    public BallisticProfile? Snapshot { get; private set; }

    /// <summary>Вернуть снимок обратно. Спорные поля не трогаются политикой.</summary>
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
    /// Прицел отдаёт <c>3.00</c> на присланное <c>3.0</c>, поэтому сравнение
    /// числовое, а не строковое. Нечисловые значения сравниваются как строки.
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
