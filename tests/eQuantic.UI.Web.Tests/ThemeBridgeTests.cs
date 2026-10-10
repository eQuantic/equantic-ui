using System.Runtime.CompilerServices;
using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The SSR→client THEME BRIDGE (C# half). <see cref="ThemeBridge.SerializeJson"/> is the wire form the
/// server injects into <c>window.__EQ_THEME__</c>; the runtime rehydrates it (theme-bridge.spec.ts
/// round-trips it back to the generated <c>photonTheme</c>). The Photon serialization is pinned to a
/// shared fixture the vitest suite also reads, so a drift on either side fails a test. Refresh with
/// <c>EQ_UPDATE_THEME_JSON=1</c>.
/// </summary>
public class ThemeBridgeTests
{
    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "theme-bridge.photon.json");
    }

    /// <summary>
    /// A role whose size follows the window crosses with its clamp (#652): without it the browser's
    /// role is the ceiling while the server's is fluid, and a component that sets the role as its
    /// override hydrates to another size. The family slot is held open with a null.
    /// </summary>
    [Fact]
    public void AFluidRole_CarriesItsClamp_AfterAnOpenFamilySlot()
    {
        var theme = new NamedFaceTests.RoleTheme(PhotonTheme.Instance, TypeRole.Display,
            PhotonTheme.Instance.Type(TypeRole.Display).WithFluidSize(34, 4.2f, 54));

        ThemeBridge.SerializeJson(theme).Should().Contain(",false,false,null,[34,4.2,54]]");
    }

    /// <summary>
    /// A fluid role's size, line box and tracking cross whole, so the browser divides the same
    /// floats the server did: four decimals of 60.49904 move the line height's last digit.
    /// </summary>
    [Fact]
    public void AFluidRole_CrossesItsSizeLineBoxAndTrackingWhole()
    {
        var style = new TypeStyle(79.49431f, 60.49904f, FontWeight.Bold, -1.234567f, 1.3f).WithFluidSize(10, 4.2f, 17.662743f);
        var theme = new NamedFaceTests.RoleTheme(PhotonTheme.Instance, TypeRole.Display, style);

        var json = ThemeBridge.SerializeJson(theme);

        json.Should().Contain($"\"display\":[{style.Size.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},{style.LineHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},");
    }

    [Fact]
    public void PhotonSerialization_MatchesTheSharedFixture()
    {
        var json = ThemeBridge.SerializeJson(PhotonTheme.Instance);
        var path = FixturePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_THEME_JSON") == "1")
        {
            File.WriteAllText(path, json);
            return;
        }

        File.Exists(path).Should().BeTrue(
            $"the runtime ships the theme-bridge fixture at {path} — run once with EQ_UPDATE_THEME_JSON=1");
        File.ReadAllText(path).Should().Be(json,
            "theme-bridge.photon.json must be regenerated (EQ_UPDATE_THEME_JSON=1) whenever the Photon tokens change");
    }

    [Fact]
    public void Serialization_CarriesTheThemeIdentity_SoTheClientRendersTheSelectedTheme()
    {
        var photon = ThemeBridge.SerializeJson(PhotonTheme.Instance);
        var material = ThemeBridge.SerializeJson(MaterialTheme.Instance);

        // Photon primary base = #0050A0 (0,80,160); Material's M3 baseline primary = #6750A4 (103,80,164).
        photon.Should().Contain("\"primary\":[[[0,80,160,255]");
        material.Should().Contain("\"primary\":[[[103,80,164,255]");

        // A distinct theme yields a distinct blob — the bridge is what makes the client match the SSR.
        material.Should().NotBe(photon);

        // The wire shape is complete: surfaces, per-variant groups, the type scale, elevations, shape.
        foreach (var key in new[] { "surfaces", "disabledOpacity", "variants", "type", "elevations", "shape" })
            material.Should().Contain($"\"{key}\":");
        material.Should().Contain("\"tertiary\":", "M3 adds the tertiary role");
    }

    [Fact]
    public void SeededMaterial_SerializesTheDerivedColors()
    {
        // A brand seed flows all the way to the wire: eQuantic blue #0050A0 → a blue-dominant primary.
        var blue = ThemeBridge.SerializeJson(MaterialTheme.FromSeed(Color.FromRgb(0x00, 0x50, 0xA0)));
        blue.Should().NotBe(ThemeBridge.SerializeJson(MaterialTheme.Instance),
            "a seeded theme differs from the fixed baseline on the wire");
    }
}
