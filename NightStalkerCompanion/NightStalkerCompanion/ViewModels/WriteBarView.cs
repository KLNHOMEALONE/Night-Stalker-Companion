using Microsoft.Maui.Graphics;
using NightStalkerCompanion.Services;

namespace NightStalkerCompanion.ViewModels;

/// <summary>
/// The write status line as the control needs it: two strings and one dot colour.
///
/// The colour is picked from the design tokens rather than hard-coded, and the
/// stage→colour mapping lives here once, so no screen can colour "mismatch" green.
/// </summary>
public sealed record WriteBarView(string Headline, string Explanation, Color Dot)
{
    public static WriteBarView Idle { get; } = Of(WriteVerdict.Idle);

    public static WriteBarView Of(WriteVerdict verdict)
        => new(verdict.Headline, verdict.Explanation, DotOf(verdict.Stage));

    private static Color DotOf(WriteStage stage) => stage switch
    {
        WriteStage.Confirmed => AppColor.Get("Ok"),
        WriteStage.Writing => AppColor.Get("Warn"),
        WriteStage.NotApplied or WriteStage.Mismatch or WriteStage.Failed => AppColor.Get("Accent"),
        // No write yet: the mock-up keeps the dot neutral-grey rather than green,
        // because nothing has been confirmed.
        _ => AppColor.Get("Ink3"),
    };
}
