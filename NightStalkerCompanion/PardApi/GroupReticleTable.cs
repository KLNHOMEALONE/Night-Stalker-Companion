namespace PardApi;

/// <summary>
/// Which group holds which reticle — the table the group list is drawn from.
///
/// Two measured facts make this a rule instead of a dictionary write (notes/APP_FIELDS.md,
/// «ГДЕ ГРУППА»). First, cmd=6009 answers for one group at a time and names it in the reply:
/// <c>GroupId</c> is the group index, the same counter as BallisticData — A..E, verified by
/// writing BallisticData=0..4 and reading 6009 back. Second, the reticle belongs to the group,
/// not to the screen: leaving group A does not make its reticle unknown.
///
/// Before this existed, an ordinary poll read a group's reticle and then dropped the one thing
/// the answer said about it, so the app carried only the active group. The group list drew the
/// rows of the other groups from a table filled solely by the explicit A…E walk, which writes
/// to the device and nobody runs by accident — so selecting B blanked A's row, numbers and all.
///
/// The table is rebuilt per remembered answer rather than mutated: snapshots are immutable and
/// replaced whole, and a dictionary a shown snapshot still points at is not this function's to
/// change.
/// </summary>
public static class GroupReticleTable
{
    /// <summary>
    /// The table with this 6009 answer filed under the group it names.
    /// An answer naming no group the scope has adds no row — an invented row 9 would appear in
    /// the list as a group the device does not have.
    /// </summary>
    public static IReadOnlyDictionary<int, Reticle> Remember(
        IReadOnlyDictionary<int, Reticle>? table, Reticle reticle)
    {
        var next = new Dictionary<int, Reticle>();
        if (table is not null)
            foreach (var row in table)
                next[row.Key] = row.Value;

        // The same group read twice keeps the later answer: a write followed by a read-back has
        // to refresh the row, or the list shows a reticle the scope no longer holds.
        if (Groups.TryFromIndex(reticle.GroupId, out _))
            next[reticle.GroupId] = reticle;

        return next;
    }
}
