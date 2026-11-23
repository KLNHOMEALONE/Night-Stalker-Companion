namespace PardApi;

/// <summary>
/// Route strings between screens, kept framework-free on purpose.
///
/// Shell is a static and a UI object, so a screen cannot be asked in a test where
/// a button actually takes the user. The app asks this class instead and hands the
/// answer to Shell, which keeps the only navigation promise the user cares about
/// testable: one tap on «home» arrives at the start screen, no matter how many
/// screens were pushed on top of it.
/// </summary>
public static class Routes
{
    /// <summary>Screen names, which are also the routes. The start screen.</summary>
    public const string DevicePage = "DevicePage";

    /// <summary>The media tab.</summary>
    public const string GalleryPage = "GalleryPage";

    /// <summary>
    /// A screen by name, from the root. Three slashes make an absolute Shell route: it gets
    /// there no matter how many screens are stacked on top, so the link does not break when
    /// somebody inserts another push between screens. The reference app Innowise.Music
    /// walks the same way (`GoToAsync($"///{nameof(HomePage)}")`).
    /// </summary>
    public static string Go(string page) => "///" + page;

    /// <summary>
    /// The start screen. Exactly <see cref="Go"/> by page name, not a <see cref="Back"/>
    /// in a loop: a screen does not know how many screens lie on top of it.
    /// </summary>
    public static string Home => Go(DevicePage);

    /// <summary>One screen up, whatever that level turns out to be.</summary>
    public static string Back => "../";

    /// <summary>
    /// Where "home" leads from a stack <paramref name="pushedPages"/> screens deep.
    /// The answer is deliberately the same at any depth: depth is precisely what made
    /// relative navigation unreliable, and the parameter exists so the caller cannot
    /// quietly pass something that changes the result.
    /// </summary>
    public static string HomeFrom(int pushedPages) => Home;

    /// <summary>
    /// Whether to show the "home" button at all. On the start screen it would take the user
    /// to a page they are already on, which reads as a broken button rather than as
    /// an action that does nothing.
    /// </summary>
    public static bool HomeIsAvailable(bool onStartScreen) => !onStartScreen;

    /// <summary>The group's reticle screen. Reading a group's reticle means selecting that group.</summary>
    public static string Reticle(int group) => $"{nameof(Reticle).ToLowerInvariant()}?group={group}";
}
