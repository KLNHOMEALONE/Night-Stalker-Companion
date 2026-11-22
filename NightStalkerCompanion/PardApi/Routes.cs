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
    /// <summary>Имена экранов — они же маршруты. Стартовый.</summary>
    public const string DevicePage = "DevicePage";

    /// <summary>Вкладка медиа.</summary>
    public const string GalleryPage = "GalleryPage";

    /// <summary>
    /// Экран по имени, от корня. Три слеша — абсолютный маршрут Shell: он ведёт туда
    /// независимо от того, сколько экранов лежит сверху, поэтому ссылка не ломается,
    /// когда между экранами кто-нибудь вставит ещё один push. Так же ходит эталонное
    /// приложение Innowise.Music (`GoToAsync($"///{nameof(HomePage)}")`).
    /// </summary>
    public static string Go(string page) => "///" + page;

    /// <summary>
    /// Стартовый экран. Именно <see cref="Go"/> от имени страницы, а не не
    /// <see cref="Back"/> в цикле: сколько экранов лежит сверху, экран не знает.
    /// </summary>
    public static string Home => Go(DevicePage);

    /// <summary>Один экран вверх, whatever that level turns out to be.</summary>
    public static string Back => "../";

    /// <summary>
    /// Куда ведёт «домой» со стека глубиной в <paramref name="pushedPages"/> экранов.
    /// Ответ намеренно одинаков при любой глубине: глубина — это ровно то, из-за чего
    /// относительная навигация была ненадёжной, а параметр существует, чтобы
    /// вызывающий код не мог тихо передать то, что меняет результат.
    /// </summary>
    public static string HomeFrom(int pushedPages) => Home;

    /// <summary>
    /// Стоит ли вообще показывать кнопку «домой». На стартовом экране она повела бы
    /// пользователя на страницу, где он уже находится, что читается как сломанная
    /// кнопка, а не как пустое действие.
    /// </summary>
    public static bool HomeIsAvailable(bool onStartScreen) => !onStartScreen;

    /// <summary>Экран марки группы. Читать марку группы — значит выбрать эту группу.</summary>
    public static string Reticle(int group) => $"{nameof(Reticle).ToLowerInvariant()}?group={group}";
}
