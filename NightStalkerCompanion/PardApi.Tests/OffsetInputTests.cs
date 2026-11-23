using PardApi;

namespace PardApi.Tests;

/// <summary>
/// What the offset field on the reticle screen accepts. The steppers were the only
/// way in, and aiming a scope by tapping «+» two hundred times is not a way in — but
/// whatever the user types is written straight into the device, so the field cannot
/// guess at a value it was not given.
/// </summary>
public class OffsetInputTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("-7", -7)]
    [InlineData("255", 255)]
    [InlineData("-255", -255)]
    public void Whole_numbers_are_accepted(string text, int expected)
    {
        Assert.True(OffsetInput.TryParse(text, out int value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("+3")]        // only the minus sign is a sign here
    [InlineData("--3")]
    [InlineData("3-")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("1 000")]
    [InlineData("12a")]
    [InlineData("abc")]
    [InlineData("−3")]        // a typographic dash is not a minus
    public void Anything_else_is_refused(string text)
    {
        // Refusing quietly is the point: writing a half-guessed offset to the scope
        // would aim the rifle at nowhere.
        Assert.False(OffsetInput.TryParse(text, out _));
    }

    [Fact]
    public void A_number_too_big_for_the_field_is_refused()
    {
        Assert.False(OffsetInput.TryParse("99999999999999999999", out _));
    }

    /// Things the field may hold while the number is still being typed: an empty field, a
    /// minus that has not caught up with its digits yet, and any whole offset the scope takes.
    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("0")]
    [InlineData("-7")]
    [InlineData("1000")]      // X has no limit the app may invent
    [InlineData("-1000")]
    public void A_half_typed_offset_stays_in_the_field(string text)
    {
        Assert.True(OffsetInput.IsDraft(text, xAxis: true));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("75")]
    [InlineData("-75")]
    public void A_y_the_scope_still_holds_stays_in_the_field(string text)
    {
        // The boundary belongs to the range, so ±75 is as much a draft as ±1 is.
        Assert.True(OffsetInput.IsDraft(text, xAxis: false));
    }

    [Theory]
    [InlineData("76")]
    [InlineData("-76")]
    [InlineData("-751")]
    public void A_y_the_scope_cannot_hold_never_reaches_the_field(string text)
    {
        // Refused while it is being typed, not on Enter: while it sits in the field it looks
        // like an offset, and the scope cannot hold it.
        Assert.False(OffsetInput.IsDraft(text, xAxis: false));
    }

    [Theory]
    [InlineData("76")]
    [InlineData("-76")]
    [InlineData("99999")]
    public void The_same_number_is_fine_on_x(string text)
    {
        // The two axes do not agree, and the field knows which one it is editing.
        Assert.True(OffsetInput.IsDraft(text, xAxis: true));
    }

    /// The characters an integer field must never hold, however they got there: the iOS
    /// keyboard offers none of them, but a paste and a hardware keyboard do.
    [Theory]
    [InlineData(" ")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("1 000")]
    [InlineData("12a")]
    [InlineData("abc")]
    [InlineData("+3")]
    [InlineData("--3")]
    [InlineData("3-")]
    [InlineData("−3")]      // a typographic dash is not a minus
    public void A_field_holding_that_is_not_an_offset_is_refused_while_typing(string text)
    {
        // Both axes, same answer: this is about shape, and shape does not depend on the axis.
        Assert.False(OffsetInput.IsDraft(text, xAxis: true));
        Assert.False(OffsetInput.IsDraft(text, xAxis: false));
    }

    [Fact]
    public void A_number_too_long_to_be_an_offset_is_refused_while_typing()
    {
        // Not even X takes this: the device stores an int, so past that it is not an offset
        // at all, however wide the axis is.
        Assert.False(OffsetInput.IsDraft("99999999999", xAxis: true));
        Assert.False(OffsetInput.IsDraft("99999999999", xAxis: false));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(12, "12")]
    [InlineData(-12, "-12")]
    public void A_stored_value_shows_back_exactly_as_it_was_written(int value, string expected)
    {
        // The field is prefilled with what the device holds; if that prefill came back
        // as "−12" with a dash, the next accepted write would move the shot by accident.
        Assert.Equal(expected, OffsetInput.Format(value));
    }
}
