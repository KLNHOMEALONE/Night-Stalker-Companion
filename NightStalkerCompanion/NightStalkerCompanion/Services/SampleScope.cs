using PardApi;

namespace NightStalkerCompanion.Services;

/// <summary>
/// A saved profile of the device, used in place of a live poll — for exactly one
/// purpose: reviewing the interface where the scope is not at hand (a plane, the
/// iOS simulator).
///
/// This is not a "pretty numbers" mode. Every value comes from real captures of
/// the firmware (notes/CMD_TABLE.md, notes/APP_FIELDS.md), and every screen must
/// show <see cref="SightingSnapshot.SourceLabel" /> — without it a snapshot is
/// indistinguishable from the truth.
/// </summary>
public static class SampleScope
{
    /// <summary>When these values were captured from the device.</summary>
    public const string CapturedOn = "22.11 19:33";

    /// <summary>Profile exactly as cmd=6019 returned it. No invented fields.</summary>
    private const string ProfileJson =
        """
        { "DistanceSwitch": 1, "DistanceUnit": 0, "BallisticData": 0,
          "Velocity": 285, "BulletWt": 3.00, "BulletBC": 0.465,
          "Altitude": 1220, "Temperature": 20, "ScopeHt": 70.1,
          "ZeroRange": 41, "Sharp": 0, "Color": 0 }
        """;

    /// <summary>
    /// Reticles per group — cmd=6009 after switching through all five groups
    /// (notes/APP_FIELDS.md, "par is not the group"). Group B carries the offset
    /// −3/+5, groups D and E carry colour 0: reticle, colour and offset are
    /// properties of the group.
    /// </summary>
    private static readonly (int Style, int Color, int X, int Y)[] PerGroup =
    {
        (6, 1, 0, 0),
        (4, 1, -3, 5),
        (3, 1, 0, 0),
        (1, 0, 0, 0),
        (1, 0, 0, 0),
    };

    public static SightingSnapshot Snapshot()
    {
        var byGroup = new Dictionary<int, Reticle>();
        for (int g = 0; g < PerGroup.Length; g++)
            byGroup[g] = Reticle.Create(g, PerGroup[g].Style, PerGroup[g].Color,
                PerGroup[g].X, PerGroup[g].Y);

        return new SightingSnapshot
        {
            IsLive = false,
            SourceLabel = $"СНИМОК {CapturedOn} — прибор не подключён",
            // Build name from cmd=3012. The firmware has no separate model number.
            Firmware = "PARD_Night Stalker 4K_EN_20241212",
            Model = "Night Stalker 4K",
            Profile = BallisticProfile.FromResponse(ProfileJson),
            Reticle = byGroup[0],
            GroupReticles = byGroup,
        };
    }

    /// <summary>Verdict for a write attempt while the device is away.</summary>
    public static WriteVerdict NoDevice(string field, string value) => new(
        WriteStage.Failed, field, value,
        Detail: "прибор не подключён — записывать не во что; снимок только для чтения");
}
