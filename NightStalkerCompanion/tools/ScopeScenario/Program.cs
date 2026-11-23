using NightStalkerCompanion.Services;
using PardApi;

// The scenario the bug report described, run against tools/mock_scope.py:
//
//     python3 tools/mock_scope.py --port 8080
//     dotnet run --project tools/ScopeScenario -- http://127.0.0.1:8080
//
// Reading a group's reticle is reading the ACTIVE group (cmd=6009 answers for one group and
// names it in GroupId), so walking from A to B is a write, and afterwards the screen shows B.
// The question the report asks is whether the app still knows A — the group list used to draw
// its rows from a table that only the explicit A…E read filled, so leaving A blanked its row.

string host = args.Length > 0 ? args[0] : "http://127.0.0.1:8080";
var scope = new PardService(host);
int failed = 0;

void Check(string what, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "  ok   " : "  FAIL ")}{what}{(detail.Length > 0 ? " — " + detail : "")}");
    if (!ok) failed++;
}

// 1. First poll: the scope is on group A and answers for it.
var first = await scope.ReadAsync();
Check("first poll is live", first.IsLive, first.SourceLabel);
Check("group A is active", first.ActiveGroupIndex == 0, $"BallisticData={first.ActiveGroupIndex}");
Check("A's reticle is known", first.GroupReticles?.ContainsKey(0) == true,
    first.GroupReticles is { } t0 ? "rows: " + string.Join(",", t0.Keys.OrderBy(k => k)) : "no table");

// 2. The reported move: select B and read again. B becomes the answer of 6009, and A must not
//    follow it out of the app.
var verdict = await scope.SelectGroupAsync(1);
var second = await scope.ReadAsync();
Check("B became active", second.ActiveGroupIndex == 1,
    $"{verdict.Stage}: {verdict.Detail}");
Check("A is still known after selecting B", second.GroupReticles?.ContainsKey(0) == true,
    second.GroupReticles is { } t1 ? "rows: " + string.Join(",", t1.Keys.OrderBy(k => k)) : "no table");
Check("B is now known too", second.GroupReticles?.ContainsKey(1) == true);

// 3. Rows are filed under the group the reply names. The mock answers with the reticles the
//    real device was measured to hold, so B is style 4 at PosX=-3 PosY=5 and nothing else is.
if (second.GroupReticles is { } rows)
{
    Check("B's row holds B's reticle", rows.TryGetValue(1, out var b) && b.Style == 4 && b.PosX == -3 && b.PosY == 5,
        rows.TryGetValue(1, out var bb) ? bb.ToString() : "no row");
    Check("A's row was not overwritten by B's", rows.TryGetValue(0, out var a) && a.Style == 6 && a.PosX == 0,
        rows.TryGetValue(0, out var aa) ? aa.ToString() : "no row");
}

// 4. And a group that is written keeps its row up to date: the write is followed by read-backs,
//    which are ordinary polls and have to land on the same row.
await scope.WriteReticleAsync(1, 2, null, null, null);
var third = await scope.ReadAsync();
Check("writing a group refreshes its row",
    third.GroupReticles is { } t2 && t2.TryGetValue(1, out var after) && after.Style == 2,
    third.GroupReticles is { } t3 && t3.TryGetValue(1, out var s) ? s.ToString() : "no row");
Check("the other rows survived the write", third.GroupReticles?.ContainsKey(0) == true);

// 5. The rules the screens ask. A reticle belongs to the group its own answer names, and to no
//    other: five groups may hold the same style and the same profile, and 6019 and 6009 are read
//    in parallel while any write moves the active group, so "the active group's reticle" can be
//    two groups' worth of numbers. This is what a tapped style used to trip over — the screen
//    decided it was no longer looking at this group and showed no selection at all.
{
    var torn = new SightingSnapshot
    {
        IsLive = true,
        SourceLabel = "6019 и 6009 разошлись",
        Reticle = Reticle.Create(0, 6, 1, 0, 0),
    };
    Check("a reticle is shown for the group it names", torn.ReticleOf(0)?.Style == 6);
    Check("a reticle is not shown for another group", torn.ReticleOf(1) is null);
    Check("a group nobody read is nobody's reticle", second.ReticleOf(4) is null);
    Check("each group is asked for separately", second.ReticleOf(0)?.GroupId == 0 && second.ReticleOf(1)?.GroupId == 1);
}

Console.WriteLine(failed == 0 ? "\nscenario passed" : $"\n{failed} check(s) failed");
return failed == 0 ? 0 : 1;
