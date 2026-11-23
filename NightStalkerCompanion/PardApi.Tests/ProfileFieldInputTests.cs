using PardApi;

namespace PardApi.Tests;

/// <summary>
/// What the value fields on the ballistics screen accept. The steppers were the only way
/// in, and a muzzle velocity is eight hundred taps away from a stepper that moves by 1 —
/// so the fields became editable, and whatever they take is written into the device.
///
/// The text is always in the units on screen. Nothing here converts: this answers which
/// numbers are numbers, and the metric the firmware stores is produced by the screen.
/// </summary>
public class ProfileFieldInputTests
{
    private static readonly ProfileFieldType Whole = ProfileFieldType.Whole;
    private static readonly ProfileFieldType Signed = ProfileFieldType.SignedWhole;
    private static readonly ProfileFieldType Decimal = ProfileFieldType.Decimal;
    private static readonly ProfileFieldType SignedDecimal = new(ProfileFieldForm.Decimal, true);

    // The two fields with a bound. Bounds are stated in the units the field is shown in, so
    // the screen builds them from its own unit system; these are the metric ones.
    private static readonly ProfileFieldType Altitude = ProfileFieldType.Whole.Between(
        ProfileFieldLimits.AltitudeMinimum, ProfileFieldLimits.AltitudeMaximum);

    private static readonly ProfileFieldType Temperature = ProfileFieldType.SignedWhole.Between(
        ProfileFieldLimits.TemperatureMinimum, ProfileFieldLimits.TemperatureMaximum);

    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("285", 285)]
    [InlineData("870", 870)]
    public void A_whole_field_takes_a_whole_number(string text, double expected)
    {
        Assert.True(ProfileFieldInput.TryParse(text, Whole, out double value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("70", 70)]
    [InlineData("70.1", 70.1)]
    [InlineData("0.465", 0.465)]
    [InlineData("3.5", 3.5)]
    public void A_decimal_field_takes_a_fraction_and_a_whole_number_too(string text, double expected)
    {
        // A bullet weighs 1 gram as much as it weighs 1.2, and the device itself sends these
        // fields with varying tail length: «0.465», «70.1», «20».
        Assert.True(ProfileFieldInput.TryParse(text, Decimal, out double value));
        Assert.Equal(expected, value, 10);
    }

    [Theory]
    [InlineData("-15", -15)]
    [InlineData("-0", 0)]
    [InlineData("20", 20)]
    public void ATemperature_takes_a_minus(string text, double expected)
    {
        // The one field of the seven that goes below zero.
        Assert.True(ProfileFieldInput.TryParse(text, Signed, out double value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-300")]
    [InlineData("-1")]
    [InlineData("-")]
    public void A_field_that_cannot_be_negative_refuses_the_minus(string text)
    {
        // The numeric keyboard offers the minus to every field on the screen the same way.
        Assert.False(ProfileFieldInput.TryParse(text, Whole, out _));
        Assert.False(ProfileFieldInput.IsDraft(text, Whole));
        Assert.False(ProfileFieldInput.TryParse(text, Decimal, out _));
        Assert.False(ProfileFieldInput.IsDraft(text, Decimal));
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("1.")]
    [InlineData("1.5")]
    public void A_whole_field_never_sees_a_separator(string text)
    {
        // Velocity, ZeroRange, Altitude and Temperature are stored whole: the sight has no
        // half metre of zero range and the table has no half metre per second in it.
        Assert.False(ProfileFieldInput.IsDraft(text, Whole));
        Assert.False(ProfileFieldInput.TryParse(text, Whole, out _));
    }

    /// Things that can still become a value, so the field keeps them while they are typed.
    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData("1.")]
    [InlineData("0.")]
    [InlineData("0.4")]
    [InlineData("0.465")]
    public void A_half_typed_decimal_stays_in_the_field(string text)
    {
        // «1.» is how a decimal starts being typed, and refusing it would make the field
        // impossible to type in — the separator arrives before the digits after it.
        Assert.True(ProfileFieldInput.IsDraft(text, Decimal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("-7")]
    public void A_half_typed_signed_number_stays_in_the_field(string text)
    {
        // The sign arrives before its digits, the same way.
        Assert.True(ProfileFieldInput.IsDraft(text, Signed));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("-7")]
    [InlineData("-7.")]
    [InlineData("-7.5")]
    public void The_sign_and_the_separator_compose(string text)
    {
        Assert.True(ProfileFieldInput.IsDraft(text, SignedDecimal));
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("-12345678")]
    public void The_longest_whole_number_a_field_holds_is_nine_digits(string text)
    {
        Assert.True(ProfileFieldInput.IsDraft(text, Signed));
    }

    [Theory]
    [InlineData("0.12345678")]
    [InlineData("1234.5678")]
    public void Nine_digits_across_the_separator_still_fit(string text)
    {
        // The cap counts digits, not characters: what stops being a profile number is the
        // size of the number, wherever the separator sits inside it.
        Assert.True(ProfileFieldInput.IsDraft(text, Decimal));
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("-1234567890")]
    [InlineData("0.123456789")]
    public void More_than_nine_digits_never_reaches_the_field(string text)
    {
        Assert.False(ProfileFieldInput.IsDraft(text, Signed));
        Assert.False(ProfileFieldInput.IsDraft(text, Decimal));
        Assert.False(ProfileFieldInput.TryParse(text, Signed, out _));
    }

    [Theory]
    [InlineData("")]            // nothing typed is not a value
    [InlineData(" ")]
    [InlineData("-")]           // mid-typing
    [InlineData("1.")]          // mid-typing
    [InlineData(".5")]          // the device sends «0.5», never «.5»
    [InlineData("+3")]          // only the minus is a sign here
    [InlineData("--3")]
    [InlineData("3-")]
    [InlineData("1..5")]
    [InlineData("1.5.5")]
    [InlineData("1,5")]         // a comma is somebody else's separator
    [InlineData("1 000")]
    [InlineData("12a")]
    [InlineData("1e3")]
    [InlineData("0.465 g")]
    [InlineData("abc")]
    [InlineData("−3")]          // a typographic dash is not a minus
    public void Anything_else_is_refused(string text)
    {
        Assert.False(ProfileFieldInput.TryParse(text, Signed, out _));
        Assert.False(ProfileFieldInput.TryParse(text, Decimal, out _));
    }

    // What goes back to the scope is the number as it was typed, decimals and all; a field that
    // was stepped rather than typed keeps the tail the device sent it with. 7.1 and 7.10 are one
    // number, so this is formatting, not a claim about what the sight stores.
    [Theory]
    [InlineData("870", ProfileFieldForm.Whole, false, 0, "870")]
    [InlineData("870", ProfileFieldForm.Whole, false, 2, "870.00")]   // stepped: the tail decides
    [InlineData("0", ProfileFieldForm.Whole, false, 0, "0")]
    [InlineData("0.465", ProfileFieldForm.Decimal, false, 3, "0.465")]
    [InlineData("0.4658", ProfileFieldForm.Decimal, false, 3, "0.4658")]   // finer than the tail
    [InlineData("0.5", ProfileFieldForm.Decimal, false, 3, "0.5")]         // coarser than it
    [InlineData("0.50", ProfileFieldForm.Decimal, false, 3, "0.50")]
    [InlineData("70", ProfileFieldForm.Decimal, false, 1, "70.0")]
    [InlineData("70.10", ProfileFieldForm.Decimal, false, 1, "70.10")]
    [InlineData("-15", ProfileFieldForm.Whole, true, 0, "-15")]
    [InlineData("-1.5", ProfileFieldForm.Decimal, true, 2, "-1.5")]
    public void The_typed_number_leaves_as_it_was_typed(string text,
        ProfileFieldForm form, bool signed, int storedDecimals, string expected)
    {
        var type = new ProfileFieldType(form, signed);
        Assert.True(ProfileFieldInput.TryParse(text, type, out double value));
        Assert.Equal(expected, ProfileFieldInput.Wire(value, text, storedDecimals));
    }

    [Fact]
    public void A_number_converted_from_the_units_on_screen_keeps_the_typed_precision()
    {
        // 2.8 in is 71.12 mm: what goes out is the conversion at the decimals that were typed.
        Assert.Equal("71.1", ProfileFieldInput.Wire(71.12, "2.8", 1));
        Assert.Equal("71.12", ProfileFieldInput.Wire(71.12, "2.80", 1));
    }

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("", 2, 2)]            // nothing typed after the separator: the tail decides
    [InlineData("70", 1, 1)]          // a stepped field, same answer
    [InlineData("70.1", 3, 1)]        // typed: the typing decides, the tail does not
    [InlineData("70.1", 0, 1)]
    [InlineData("70.1234", 1, 4)]
    public void Typing_decides_the_decimals_and_the_field_tail_fills_the_rest(string text,
        int stored, int expected)
    {
        Assert.Equal(expected, ProfileFieldInput.WireDecimals(text, stored));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_field_that_was_never_read_has_no_tail(int stored)
    {
        // A field the device never sent has no decimals of its own; that is zero, not an error.
        Assert.Equal("7", ProfileFieldInput.Wire(7.4, "", stored));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("500")]
    [InlineData("7999")]
    [InlineData("8000")]
    public void A_height_above_sea_level_goes_up_to_the_highest_place_there_is(string text)
    {
        Assert.True(ProfileFieldInput.TryParse(text, Altitude, out _));
        Assert.True(ProfileFieldInput.IsDraft(text, Altitude));
    }

    [Theory]
    [InlineData("8001")]
    [InlineData("80001")]
    public void A_height_above_the_highest_place_never_reaches_the_field(string text)
    {
        Assert.False(ProfileFieldInput.IsDraft(text, Altitude));
        Assert.False(ProfileFieldInput.TryParse(text, Altitude, out _));
    }

    [Fact]
    public void A_ceiling_is_reached_one_digit_at_a_time()
    {
        // It is the draft rule that does this: every prefix of a legal number is legal, so the
        // keystroke that would pass the mountain is the one refused, and the field never shows
        // an altitude that is not a place.
        Assert.True(ProfileFieldInput.IsDraft("800", Altitude));
        Assert.True(ProfileFieldInput.IsDraft("8000", Altitude));
        Assert.False(ProfileFieldInput.IsDraft("8009", Altitude));
    }

    [Fact]
    public void A_height_below_sea_level_is_refused_like_any_other_negative_magnitude()
    {
        Assert.False(ProfileFieldInput.IsDraft("-1", Altitude));
        Assert.False(ProfileFieldInput.TryParse("-1", Altitude, out _));
    }

    [Theory]
    [InlineData("-272")]
    [InlineData("-15")]
    [InlineData("0")]
    [InlineData("100")]
    public void ATemperature_goes_from_absolute_zero_to_a_hot_day(string text)
    {
        Assert.True(ProfileFieldInput.TryParse(text, Temperature, out _));
    }

    [Theory]
    [InlineData("-273")]
    [InlineData("-1000")]
    [InlineData("101")]
    public void ATemperature_that_is_not_a_temperature_is_refused(string text)
    {
        Assert.False(ProfileFieldInput.IsDraft(text, Temperature));
        Assert.False(ProfileFieldInput.TryParse(text, Temperature, out _));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("-2")]
    [InlineData("-27")]
    public void A_half_typed_number_is_not_judged_by_the_range(string text)
    {
        // «-2» is not a temperature yet, it is the beginning of one, and it is on the way to
        // «-272». Only a complete number has a value to check.
        Assert.True(ProfileFieldInput.IsDraft(text, Temperature));
    }

    [Fact]
    public void A_field_with_no_stated_ceiling_holds_whatever_it_can_type()
    {
        // Velocity, mass, coefficient, height over bore, zero range: the device has never
        // refused a large number, so the app does not invent a limit it has not seen.
        Assert.True(ProfileFieldInput.TryParse("999999999", ProfileFieldType.Whole, out _));
        Assert.True(ProfileFieldInput.TryParse("999999999", ProfileFieldType.Decimal, out _));
    }

    [Fact]
    public void A_bound_is_stated_in_the_units_the_field_is_shown_in()
    {
        // 8 000 m is 26 247 ft: the field in feet bounds its own number, it does not convert.
        var feet = ProfileFieldType.Whole.Between(0, 26247);
        Assert.True(ProfileFieldInput.TryParse("26247", feet, out _));
        Assert.False(ProfileFieldInput.TryParse("26248", feet, out _));
    }

    [Fact]
    public void The_two_bounds_the_profile_has()
    {
        Assert.Equal(0, ProfileFieldLimits.AltitudeMinimum);
        Assert.Equal(8000, ProfileFieldLimits.AltitudeMaximum);
        Assert.Equal(-272, ProfileFieldLimits.TemperatureMinimum);
        Assert.Equal(100, ProfileFieldLimits.TemperatureMaximum);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("70", 0)]
    [InlineData("70.", 0)]
    [InlineData("0.465", 3)]
    [InlineData("-7.5", 1)]
    public void The_separator_counts_the_digits_after_it(string? text, int expected)
    {
        Assert.Equal(expected, ProfileFieldInput.DecimalsOf(text));
    }

    [Fact]
    public void A_field_holds_whatever_its_type_holds()
    {
        // The three types the screen builds its fields from; a fourth would be a decision.
        Assert.Equal(ProfileFieldForm.Whole, Whole.Form);
        Assert.False(Whole.NegativeAllowed);
        Assert.True(Signed.NegativeAllowed);
        Assert.Equal(ProfileFieldForm.Decimal, Decimal.Form);
        Assert.False(Decimal.NegativeAllowed);
        Assert.True(Signed.IsSigned);
    }
}
