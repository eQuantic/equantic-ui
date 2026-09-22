using System.Runtime.CompilerServices;
using eQuantic.UI.Native.Hosting;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The same ABI pin as the vocabulary's, for the enums a Photon app declares with.
///
/// <para>
/// C# writes an enum constant into the CONSUMING assembly's IL rather than a reference to it, so a
/// member inserted in the middle renumbers everything after it and an app compiled against the
/// previous package goes on emitting the old number. `EnumValueAbiTests` in
/// <c>eQuantic.UI.Web.Tests</c> says this for the vocabulary, and it can only see
/// <c>eQuantic.UI.Primitives</c>.
/// </para>
///
/// <para>
/// <c>AppCategory</c> and <c>PhotonBundleValueKind</c> used to be in that assembly and were covered
/// by it. They are native-only declarations and moved to <c>eQuantic.UI.Native.Hosting</c> — and a
/// move like that is exactly how a pin is lost without anything going red: regenerating the other
/// baseline simply drops the rows, because the pin only refuses a row it can no longer FIND. So the
/// rows came here BY HAND, carried across rather than re-derived, which is what makes them evidence
/// that the numbers did not change in the move.
/// </para>
///
/// <para>
/// Append-only, like its sibling: a new member at the END takes the next free number and breaks
/// nobody. Regenerate after an intentional append with
/// <c>EQ_UPDATE_HOSTING_ENUM_VALUES=1 dotnet test tests/eQuantic.UI.Native.Engine.Tests</c>. The
/// baseline may grow; a line that changes is the bug.
/// </para>
/// </summary>
public class HostingEnumValueAbiTests
{
    private static string BaselinePath([CallerFilePath] string sourcePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourcePath)!, "Coverage", "enum-values.baseline.txt");

    /// <summary>Every public enum of the hosting assembly, as <c>Type.Member = value</c>.</summary>
    private static string[] Current() =>
        typeof(PhotonEntitlements).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.IsPublic)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .SelectMany(type => Enum.GetNames(type)
                .Select(name => (Name: name,
                    Value: Convert.ToInt64(Enum.Parse(type, name), System.Globalization.CultureInfo.InvariantCulture)))
                .OrderBy(member => member.Value)
                .ThenBy(member => member.Name, StringComparer.Ordinal)
                .Select(member => $"{type.Name}.{member.Name} = {member.Value}"))
            .ToArray();

    [Fact]
    public void NoHostingEnumMemberEverChangesItsNumber()
    {
        var current = Current();
        var path = BaselinePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_HOSTING_ENUM_VALUES") == "1")
        {
            File.WriteAllLines(path, current);
            return;
        }

        File.Exists(path).Should().BeTrue(
            $"the ABI baseline lives at {path} — run once with EQ_UPDATE_HOSTING_ENUM_VALUES=1");

        var live = current.ToHashSet(StringComparer.Ordinal);
        var broken = File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !live.Contains(line))
            .ToArray();

        string.Join("\n", broken).Should().BeEmpty(
            "every one of these members had this number in a shipped package, and a Photon app "
            + "compiled against it embedded the number rather than a reference. A member that MOVED "
            + "renumbers what a released app already emits; one that VANISHED leaves it emitting a "
            + "number nothing answers. Append at the END instead, then refresh with "
            + "EQ_UPDATE_HOSTING_ENUM_VALUES=1");
    }

    /// <summary>
    /// The pin is only worth its file if it FAILS, and the move it was written for is the thing to
    /// state: these two shipped from <c>eQuantic.UI.Primitives</c> with these numbers, and carrying
    /// the type to another assembly is not a licence to renumber it.
    /// </summary>
    [Fact]
    public void TheMovedEnumsKeptTheNumbersTheyShippedWith()
    {
        ((int)AppCategory.None).Should().Be(0);
        ((int)AppCategory.Utilities).Should().Be(1, "Utilities shipped as 1 while the type was in Primitives");
        ((int)PhotonBundleValueKind.Text).Should().Be(0);
        ((int)PhotonBundleValueKind.UrlScheme).Should().Be(2,
            "UrlScheme shipped as 2 — a kind inserted above it would change what a released app's "
            + "manifest declaration means");
    }
}
