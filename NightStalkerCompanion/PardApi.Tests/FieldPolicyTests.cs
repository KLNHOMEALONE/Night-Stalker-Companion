using PardApi;

// Тесты только бизнес-логики: разбор ответов прицела, политика полей, перевод
// групп. Экраны и цвета здесь не проверяются — по договорённости UI не тестируется.

/// <summary>
/// Порядок записи полей на прицел.
/// </summary>
public class FieldPolicyTests
{
    /// <summary>
    /// BallisticData обязан уходить последним из запрошенных: более ранняя запись
    /// перечитывает профиль и молча сбрасывает его в 0, из-за чего обнуляется
    /// баллистическая таблица, которую пользователь только что выставил.
    /// </summary>
    [Fact]
    public void BallisticData_записывается_последним()
    {
        string[] wanted = ["BallisticData", "Velocity", "Sharp", "Color", "Temperature"];

        var order = FieldPolicy.WriteOrder(wanted);

        Assert.Equal("BallisticData", order[^1]);
    }
}
