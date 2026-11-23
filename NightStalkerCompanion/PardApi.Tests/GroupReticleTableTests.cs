using PardApi;

namespace PardApi.Tests;

/// <summary>
/// The table of «which group holds which reticle», which is what the group list on the
/// dashboard shows. Two facts make this a rule rather than a dictionary write, and both come
/// from measuring the device (notes/APP_FIELDS.md, «ГДЕ ГРУППА»):
///
///   · cmd=6009 answers for one group at a time, and says which in <c>GroupId</c>;
///   · <c>GroupId</c> is the group index A..E, the same counter as BallisticData.
///
/// So every ordinary poll learns a row, and forgetting that is the bug this table exists to
/// prevent: the dashboard used to know only the active group, and selecting B erased A from the
/// list even though the app had read A a minute ago.
/// </summary>
public class GroupReticleTableTests
{
    private static Reticle Of(int group, int style = 1) => Reticle.Create(group, style, 1, 0, 0);

    [Fact]
    public void An_answer_for_one_group_becomes_that_groups_row()
    {
        var table = GroupReticleTable.Remember(null, Of(1));

        Assert.Equal(1, table.Count);
        Assert.Equal(1, table[1].Style);
    }

    [Fact]
    public void Remembering_one_group_does_not_erase_another()
    {
        // The reported bug, in one line: walk A..E was never run, so the app held A's reticle
        // only as long as A was active, and the list went blank one row at a time.
        var before = GroupReticleTable.Remember(null, Of(0, style: 6));

        var after = GroupReticleTable.Remember(before, Of(1, style: 4));

        Assert.Equal(2, after.Count);
        Assert.Equal(6, after[0].Style);
        Assert.Equal(4, after[1].Style);
    }

    [Fact]
    public void A_group_remembered_twice_keeps_the_later_answer()
    {
        // Writing to a group and reading it back has to refresh its row, or the list keeps
        // showing a reticle the scope no longer holds.
        var before = GroupReticleTable.Remember(null, Of(2, style: 3));

        var after = GroupReticleTable.Remember(before, Of(2, style: 5));

        Assert.Equal(5, after[2].Style);
        Assert.Equal(1, after.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(9)]
    public void An_answer_about_a_group_that_does_not_exist_adds_no_row(int groupId)
    {
        // 6009 with a GroupId outside A..E has never been seen. Inventing row 9 would put a
        // group on the list that the scope does not have.
        var before = GroupReticleTable.Remember(null, Of(0));

        var after = GroupReticleTable.Remember(before, Of(groupId));

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(1, after.Count);
    }

    [Fact]
    public void Remembering_leaves_the_table_it_was_given_alone()
    {
        // Snapshots are immutable and replaced whole. Mutating the dictionary a shown snapshot
        // still points at would let one poll redraw another screen's numbers.
        var before = GroupReticleTable.Remember(null, Of(0));

        GroupReticleTable.Remember(before, Of(3));

        Assert.Single(before);
    }
}
