namespace PardApi;

/// <summary>
/// A group of reticles (and, by all accounts, the ballistic profile of the same
/// group). The scope's screen labels the groups <c>A B C D E</c> while the stock
/// app shows those the numbers <c>0..4</c> - which is exactly why the original
/// app makes you remember which reticle you are editing.
/// Here letters are the primary way of talking about groups; numbers survive
/// only inside requests.
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
/// The letters and the <c>par=</c> index in <c>GET /?custom=1&amp;cmd=...&amp;par=N</c>.
/// </summary>
public static class Groups
{
    /// <summary>There are exactly five groups - A..E (the scope owner's data).</summary>
    public const int Count = 5;

    /// <summary>
    /// The <c>par</c> value for a group. The letter-to-number mapping comes from
    /// the stock app, where A=0 ... E=4; what <c>par</c> actually selects on the
    /// device is checked by reading <c>cmd=6009&amp;par=N</c> across all groups.
    /// Until that is confirmed, do not rely on <c>par</c> as a pointer to
    /// <see cref="Reticle.GroupId"/> - they may be different counters.
    /// </summary>
    public static int Par(this Group g) => (int)g;

    /// <summary>The group letter - the way the scope shows it.</summary>
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
    /// Checking an index that came from the device. Silently turning an unknown number into
    /// A is not possible: it would quietly show the wrong reticle.
    /// </summary>
    public static Group FromIndex(int index) =>
        TryFromIndex(index, out var g)
            ? g
            : throw new ArgumentOutOfRangeException(nameof(index),
                $"группа {index} вне диапазона 0..{Count - 1} (A..{Name((Group)(Count - 1))})");

    /// <summary>All groups in order - for the list in the interface.</summary>
    public static IReadOnlyList<Group> All { get; } =
        Enumerable.Range(0, Count).Select(i => (Group)i).ToList();

    public static string Describe(this Group g) => $"{g.Name()} (par={g.Par()})";
}
