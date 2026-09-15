using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// WHERE the native SDK is allowed to ask which device it is building for.
///
/// <para>
/// <c>Sdk.props</c> is imported ABOVE the project body, so <c>TargetFramework</c> is not set yet:
/// it is a global property that early only for <c>-f</c> and for the inner build of a multi-target
/// project. For the ordinary shape of a mobile app — <c>&lt;TargetFramework&gt;net10.0-ios&lt;/…&gt;</c>
/// in the body — the platform reads as EMPTY up there, and every default conditioned on it silently
/// does not happen.
/// </para>
/// <para>
/// ITEMS in that file are safe, and that is not an inconsistency: MSBuild evaluates every property
/// before it evaluates any item, so an ItemGroup condition reads the platform that Sdk.targets
/// writes. A PROPERTY only ever sees what was assigned above its own line, so a PropertyGroup there
/// reads the empty one. That single difference is why this guard names PropertyGroup and nothing
/// else.
/// </para>
/// <para>
/// Measured on a single-target <c>net10.0-ios</c> app while both of these were in Sdk.props: the
/// platform minimum fell back to the iOS SDK's own (26.5 rather than 15.0, an app that refuses to
/// install on anything older than the SDK it was built with) and the Xcode version check stayed on,
/// which is the defect that sent a developer looking. Neither could be seen from this repository,
/// whose only iOS consumer is multi-target and whose native template is desktop-only.
/// </para>
/// </summary>
public class NativeSdkPlatformQuestionTests
{
    private static string SdkDir([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..",
            "src", "eQuantic.UI.Sdk.Native", "Sdk"));

    private const string Platform = "_EqPlatform";

    [Fact]
    public void SdkProps_AsksNothingOfThePlatformItCannotKnowThere()
    {
        var props = Path.Combine(SdkDir(), "Sdk.props");
        File.Exists(props).Should().BeTrue($"{props} is what this guard reads");

        // The GROUP's condition and each property's own: MSBuild treats them the same way, and a
        // default written as `<PropertyGroup><Foo Condition="…$(_EqPlatform)…">` is evaluated just
        // as early as one written on the group. Reading only the group would leave the rule true in
        // letter and bypassable in one line.
        var offenders = XDocument.Load(props)
            .Descendants()
            .Where(element => element.Name.LocalName == "PropertyGroup")
            .SelectMany(group => group.Elements().Prepend(group))
            .Select(element => (string?)element.Attribute("Condition"))
            .Where(condition => condition?.Contains(Platform, StringComparison.Ordinal) == true)
            .ToList();

        offenders.Should().BeEmpty(
            $"a PropertyGroup in Sdk.props reads {Platform} before the project body has set " +
            $"TargetFramework, so it is empty for a single-target app and the default never " +
            $"happens. Sdk.targets owns every property that depends on the platform — move it " +
            $"there, below the block that asks the question. (Conditions found: " +
            $"{string.Join(" | ", offenders)})");
    }

    /// <summary>
    /// And the other half, without which the guard above passes over an SDK that asks the question
    /// nowhere at all: Sdk.targets must still answer it for the shape Sdk.props could not.
    /// </summary>
    [Fact]
    public void SdkTargets_AsksThePlatformQuestionForTheShapeSdkPropsCannot()
    {
        var targets = Path.Combine(SdkDir(), "Sdk.targets");
        File.Exists(targets).Should().BeTrue($"{targets} is what this guard reads");

        var answered = XDocument.Load(targets)
            .Descendants()
            .Where(element => element.Name.LocalName == "PropertyGroup")
            .Any(element =>
                ((string?)element.Attribute("Condition"))?.Contains($"'$({Platform})' == ''",
                    StringComparison.Ordinal) == true
                && element.Elements().Any(child => child.Name.LocalName == Platform));

        answered.Should().BeTrue(
            $"Sdk.targets must fill {Platform} in when Sdk.props could not — that is the whole " +
            $"reason the properties conditioned on it live there. Without it a single-target " +
            $"mobile app is built as a desktop head: no platform shell, no app manifest, and the " +
            $"Xcode version check back on.");
    }
}
