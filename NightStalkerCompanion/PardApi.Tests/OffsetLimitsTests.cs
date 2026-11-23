using PardApi;

namespace PardApi.Tests;

/// <summary>
/// What the scope lets each offset be. The two axes behave differently, and the point of
/// testing them together is that they must never be collapsed into one rule again: X has
/// never been seen to be refused, Y stops at 75.
/// </summary>
public class OffsetLimitsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(74)]
    [InlineData(-74)]
    [InlineData(75)]      // the boundary itself is a value the scope takes
    [InlineData(-75)]
    public void Y_takes_everything_up_to_its_limit(int value)
    {
        Assert.True(OffsetLimits.WithinY(value));
    }

    [Theory]
    [InlineData(76)]      // one past the limit
    [InlineData(-76)]
    [InlineData(300)]
    [InlineData(-1000)]
    public void Y_refuses_past_its_limit(int value)
    {
        // Refusing here is the whole use of this type: what the screen sends has to be
        // something the scope can hold, and a field that offers 76 offers a number the
        // device was never seen to accept.
        Assert.False(OffsetLimits.WithinY(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(75)]
    [InlineData(307)]     // as far as X has been aimed
    [InlineData(-307)]
    [InlineData(308)]     // and one past that is still taken
    [InlineData(100000)]
    public void X_has_no_limit_the_scope_enforces(int value)
    {
        // This asserts that nothing is enforced. The scope has taken every X it was
        // given, so a wall here would be the app's invention — and it would refuse a
        // value the sight accepts, which is the one thing this screen is not allowed
        // to do.
        Assert.True(OffsetLimits.WithinX(value));
    }

    [Fact]
    public void The_two_axes_are_not_the_same_rule()
    {
        // 300 is ordinary on X and impossible on Y. Any refactor that reads like
        // "the offset limit" and forgets which axis it is holding breaks here.
        Assert.True(OffsetLimits.WithinX(300));
        Assert.False(OffsetLimits.WithinY(300));
    }

    [Fact]
    public void Y_is_as_strict_below_zero_as_above()
    {
        // A limit typed in as a pair of constants drifts: one gets edited, the other
        // does not. Both sides are asked about the same number.
        Assert.Equal(OffsetLimits.WithinY(OffsetLimits.YLimit), OffsetLimits.WithinY(-OffsetLimits.YLimit));
        Assert.False(OffsetLimits.WithinY(OffsetLimits.YLimit + 1));
        Assert.False(OffsetLimits.WithinY(-OffsetLimits.YLimit - 1));
    }

    [Theory]
    [InlineData(true, 300, true)]     // X: taken
    [InlineData(false, 300, false)]   // Y: refused
    [InlineData(true, 76, true)]
    [InlineData(false, 76, false)]
    [InlineData(false, -75, true)]
    public void One_question_asked_of_either_axis(bool xAxis, int value, bool expected)
    {
        Assert.Equal(expected, OffsetLimits.Within(xAxis, value));
    }
}
