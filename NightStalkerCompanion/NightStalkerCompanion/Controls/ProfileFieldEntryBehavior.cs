using NightStalkerCompanion.ViewModels;
using PardApi;

namespace NightStalkerCompanion.Controls;

/// <summary>
/// Keeps a profile field to a number of its own type — while the number is being typed, not
/// when Enter is pressed.
///
/// The rule lives in <see cref="ProfileFieldInput.IsDraft"/>; this only applies it, for the
/// reason the offset field has: what is in the field is not harmless. A velocity showing
/// «28.5» is a velocity the scope cannot store, a ballistic coefficient showing «0.4» is a
/// different bullet, and a temperature field holding «1 000» is a temperature nobody typed
/// on purpose. Checking at Enter leaves all three on screen until then, and a keyboard is
/// not a guarantee — the numeric keypad has no separator and no minus, but a paste and a
/// hardware keyboard both do.
///
/// A refused keystroke puts back the text the field held a moment ago rather than a
/// cleaned-up version of the new one. Cleaning up means guessing, and a guessed profile
/// value goes to the scope.
///
/// Which type this field edits comes from the row the Entry is bound to, not from a property
/// set from outside: the fields are built from a table, there are seven of them, and the two
/// that take a minus are one lookup away from the five that do not. A rule wired by hand
/// could be wired to the wrong field; the row a field is bound to cannot be another row.
/// </summary>
public sealed class ProfileFieldEntryBehavior : Behavior<Entry>
{
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
        if (ProfileFieldInput.IsDraft(text, TypeOf(field)))
        {
            accepted = text;
            return;
        }

        reverting = true;
        field.Text = accepted;
        reverting = false;
    }

    /// <summary>
    /// The type of the field this Entry edits. A row that is not an editable profile field
    /// has no type to contradict, and is left to hold whatever it is given.
    /// </summary>
    private static ProfileFieldType TypeOf(Entry field)
        => (field.BindingContext as FieldRow)?.Type ?? ProfileFieldType.Whole;
}
