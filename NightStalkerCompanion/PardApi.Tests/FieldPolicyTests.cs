using PardApi;

// Tests of business logic only: parsing the scope answers, the field policy, the group
// mapping. Screens and colours are not tested here - by agreement the UI is not tested.

/// <summary>
/// The order in which fields are written to the scope.
/// </summary>
public class FieldPolicyTests
{
    /// <summary>
    /// BallisticData has to go last among the requested fields: an earlier write reads the
    /// profile back and silently resets it to 0, which zeroes out the ballistic table the
    /// user has just set up.
    /// </summary>
    [Fact]
    public void BallisticData_is_written_last()
    {
        string[] wanted = ["BallisticData", "Velocity", "Sharp", "Color", "Temperature"];

        var order = FieldPolicy.WriteOrder(wanted);

        Assert.Equal("BallisticData", order[^1]);
    }
}
