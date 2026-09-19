using System.Reflection;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// NO MEMBER SURVIVES TO KEEP AN OLD SHAPE ALIVE — the rule, as something that fails.
///
/// <para>
/// The SDK is in preview: a signature widens and the old one GOES. What this refuses is the member
/// kept beside the real one so an assembly compiled elsewhere still binds — and the reason it is a
/// test rather than a sentence is that the tree taught the opposite for two rounds.
/// <c>TypeStyle</c> gained a face and grew a seven-parameter constructor and a seven-output
/// <c>Deconstruct</c>, with a test asserting they stayed; <c>SemanticNode</c> then copied that
/// repair for a live-region urgency, at the cost of a CS0121 ambiguity across every six-argument
/// call in the tree; and <c>UI.ProgressBar</c> carried twenty lines explaining why it could NOT do
/// the same. None of the three served a consumer this repository has — nothing here compiles
/// against a released <c>eQuantic.UI.*</c> package.
/// </para>
///
/// <para>
/// A pin that passes by having nothing to say is the shape this repo has been caught by, so each
/// assertion below states what it scanned as well as what it found.
/// </para>
/// </summary>
public class NoOldShapeSurvivesTests
{
    private static readonly Assembly[] Surface =
        [typeof(VisualNode).Assembly, typeof(eQuantic.UI.Components.Button).Assembly];

    /// <summary>
    /// A positional record SYNTHESISES exactly one <c>Deconstruct</c>, so a second one exists for
    /// one reason only: an older arity somebody wanted to keep reachable. That is the tell, and it
    /// is precise — a legitimate second CONSTRUCTOR is common here (<c>ColorToken(light, dark)</c>
    /// beside <c>ColorToken(both)</c>, <c>CornerRadii</c>'s four corners beside its one), which is
    /// why the constructor count is deliberately NOT what this asks about.
    /// </summary>
    [Fact]
    public void NoTypeDeclaresASecondDeconstruct()
    {
        var scanned = 0;
        var doubled = new List<string>();

        foreach (var type in Surface.SelectMany(a => a.GetExportedTypes()))
        {
            scanned++;
            var arities = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == "Deconstruct")
                .Select(method => method.GetParameters().Length)
                .OrderBy(count => count)
                .ToArray();
            if (arities.Length > 1)
                doubled.Add($"{type.Name} ({string.Join(", ", arities)} outputs)");
        }

        scanned.Should().BeGreaterThan(200, "the scan has to see the surface it clears");
        doubled.Should().BeEmpty(
            "a second Deconstruct keeps an older shape readable, which preview does not buy — "
            + "widen the record, fix the call sites, and let the old arity go: "
            + string.Join(", ", doubled));
    }

    /// <summary>
    /// The other survival shape, and the one that is easiest to reach for: a member marked obsolete
    /// and left in place. Deprecation is a promise to remove something later, and "later" is what a
    /// preview does not have — the removal is now.
    /// </summary>
    [Fact]
    public void NothingIsMarkedObsolete()
    {
        var obsolete = Surface
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                    | BindingFlags.DeclaredOnly)
                .Concat([(MemberInfo)type])
                .Where(member => member.GetCustomAttribute<ObsoleteAttribute>() is not null)
                .Select(member => $"{type.Name}.{member.Name}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        obsolete.Should().BeEmpty(
            "an obsolete member is an old shape with a note attached — delete it and its callers "
            + "instead: " + string.Join(", ", obsolete));
    }
}
