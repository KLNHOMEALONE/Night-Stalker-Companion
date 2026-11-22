namespace PardApi;

/// <summary>
/// Группа прицельных марок (и, судя по всему, баллистический профиль той же
/// группы). На экране прицела группы подписаны буквами <c>A B C D E</c>, штатное
/// приложение показывает им соответствующие номера <c>0..4</c> - именно из-за этого
/// расхождения в оригинальном приложении приходится держать в голове,
/// какую марку ты сейчас правишь. Здесь буквы и есть основной способ говорить о
/// группах; числа остаются только в запросах.
/// </summary>
public enum Group
{
    A = 0,
    B = 1,
    C = 2,
    D = 3,
    E = 4
}

/// <summary>
/// Соответствие букв и индекса <c>par=</c> в <c>GET /?custom=1&amp;cmd=...&amp;par=N</c>.
/// </summary>
public static class Groups
{
    /// <summary>Групп ровно пять - A..E (данные владельца прицела).</summary>
    public const int Count = 5;

    /// <summary>
    /// Значение <c>par</c> для группы. Соответствие «буква ↔ номер» взято из
    /// штатного приложения, где A=0 ... E=4; что именно выбирает <c>par</c> на
    /// приборе проверяется чтением <c>cmd=6009&amp;par=N</c> по всем группам.
    /// Пока это не подтверждено, не полагайтесь на <c>par</c> как на указатель на
    /// <see cref="Reticle.GroupId"/> - это могут быть разные счётчики.
    /// </summary>
    public static int Par(this Group g) => (int)g;

    /// <summary>Буква группы - так её показывает прицел.</summary>
    public static string Name(this Group g) => ((char)('A' + (int)g)).ToString();

    public static bool TryParse(string? s, out Group group)
    {
        group = Group.A;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().ToUpperInvariant();
        if (s.Length == 1 && s[0] >= 'A' && s[0] < 'A' + Count)
        {
            group = (Group)(s[0] - 'A');
            return true;
        }
        if (int.TryParse(s, out var n)) return TryFromIndex(n, out group);
        return false;
    }

    public static bool TryFromIndex(int index, out Group group)
    {
        group = Group.A;
        if (index < 0 || index >= Count) return false;
        group = (Group)index;
        return true;
    }

    /// <summary>
    /// Проверка индекса из ответа прибора. Молча превращать незнакомое число в
    /// A нельзя: молча покажется не та марка.
    /// </summary>
    public static Group FromIndex(int index) =>
        TryFromIndex(index, out var g)
            ? g
            : throw new ArgumentOutOfRangeException(nameof(index),
                $"группа {index} вне диапазона 0..{Count - 1} (A..{Name((Group)(Count - 1))})");

    /// <summary>Все группы по порядку - для списка в интерфейсе.</summary>
    public static IReadOnlyList<Group> All { get; } =
        Enumerable.Range(0, Count).Select(i => (Group)i).ToList();

    public static string Describe(this Group g) => $"{g.Name()} (par={g.Par()})";
}
