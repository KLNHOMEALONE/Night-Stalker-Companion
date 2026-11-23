using PardApi;

namespace NightStalkerCompanion.Controls;

/// <summary>
/// Keeps an offset field to whole numbers, and Y to what the scope holds — while the number is
/// being typed, not when Enter is pressed.
///
/// The rule itself lives in <see cref="OffsetInput.IsDraft"/>; this only applies it, because
/// what is in the field is not harmless. A field showing «1.5» is a field the user reads as an
/// offset, and a field showing Y = 200 is a field the scope will never take. Checking at Enter
/// leaves both on screen until then, and a keyboard is not a guarantee: the numeric keypad has
/// no separator and no minus, but a paste and a hardware keyboard both do.
///
/// A refused keystroke puts back the text the field held a moment ago rather than a cleaned-up
/// version of the new one. Cleaning up means guessing, and a guessed offset goes to the device.
/// </summary>
public sealed class OffsetEntryBehavior : Behavior<Entry>
{
    /// <summary>Which offset this field edits: «Y», or anything else for X.</summary>
    public string Axis { get; set; } = "X";

    /// <summary>The last text the field was allowed to hold — what a refused key puts back.</summary>
    private string accepted = "";

    private bool reverting;

    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        accepted = bindable.Text ?? "";
        bindable.TextChanged += OnTextChanged;
    }

    protected override void OnDetachingFrom(Entry bindable)
    {
        bindable.TextChanged -= OnTextChanged;
        base.OnDetachingFrom(bindable);
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        // The guard is against the line below: writing Text fires TextChanged again, and that
        // one is the field obeying the rule, not a keystroke testing it.
        if (reverting || sender is not Entry field)
            return;

        string text = e.NewTextValue ?? "";
        if (OffsetInput.IsDraft(text, Axis != "Y"))
        {
            accepted = text;
            return;
        }

        reverting = true;
        field.Text = accepted;
        reverting = false;
    }
}
