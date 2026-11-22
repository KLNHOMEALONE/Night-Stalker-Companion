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
