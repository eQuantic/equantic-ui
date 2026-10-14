using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
/// <para>
/// AT 1.0 THE RULE INVERTS, and this follows the version rather than a date someone has to remember
/// (#323). While the version carries a prerelease label nothing is obsolete, since the preview
/// removes. Once it does not, the compatibility promise holds: a member of the contract leaves only
/// in a major, after an <c>[Obsolete]</c> that says so (<see cref="ObsoleteMark"/>), and the build
/// itself refuses a removal (<c>EqStableVersionRemovesNoContractMember</c> in
/// <c>Directory.Build.targets</c>). A second <c>Deconstruct</c> is refused at every version.
/// </para>
/// <para>
/// What it scans is every CONTRACT assembly this run carries, read from what ships
/// (<see cref="SurfaceClass"/>): the tools and the native track are outside the promise.
/// </para>
/// </summary>
public class NoOldShapeSurvivesTests
{
    private static readonly Assembly[] Surface = SurfaceClass.ShippingAssemblies()
        .Where(assembly => SurfaceClass.Of(assembly) == SurfaceClass.Contract)
        .ToArray();

    /// <summary>The scan has to hold the two assemblies it was written for, or it lost its way.</summary>
    [Fact]
    public void TheScanReadsTheContractAssembliesThisRunCarries()
    {
        Surface.Select(assembly => assembly.GetName().Name).Should().Contain(
            ["eQuantic.UI.Primitives", "eQuantic.UI.Components", "eQuantic.UI.Web"],
            "the guard reads every contract assembly this run carries, the vocabulary and the components among them");
    }

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
    /// preview does not have — the removal is now. At a stable version it is the only way a member
    /// leaves, and it has to say when.
    /// <para>
    /// Read from the SOURCE of every contract project, not from the assemblies this run happens to
    /// reference: the server, the integrations and the icon packs are the contract too, and a test
    /// that reflected over its own references never saw them. The source also holds only what a
    /// person wrote, where an assembly holds the <c>[Obsolete]</c> C# writes on the constructors of a
    /// type with a <c>required</c> member, for a compiler too old to set it.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryObsoleteMemberKeepsTheRuleOfItsVersion()
    {
        var version = ReleaseVersion.Of(typeof(VisualNode).Assembly);
        version.Should().NotBeEmpty("the vocabulary carries the version Directory.Build.props gave the build");

        var projects = SurfaceClass.ContractProjects();
        projects.Select(Path.GetFileName).Should().Contain(
            ["eQuantic.UI.Primitives", "eQuantic.UI.Server", "eQuantic.UI.Lucide"],
            "the scan reads every contract project, the server and the icon packs among them");

        var files = 0;
        var wrong = new List<string>();
        foreach (var project in projects)
        {
            foreach (var file in Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                             && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            {
                files++;
                var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseDefaults.Options, file).GetRoot();
                foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>().Where(IsObsolete))
                {
                    var where = $"{Path.GetRelativePath(SdkProbe.RepoRoot(), file)}:{attribute.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                    if (Read(attribute) is not { } obsolete)
                    {
                        wrong.Add($"{where}: its message, DiagnosticId and UrlFormat are written as literals, so this guard can read them");
                        continue;
                    }
                    wrong.AddRange(ObsoleteMark.Problems(version, obsolete).Select(problem => $"{where}: {problem}"));
                }
            }
        }

        files.Should().BeGreaterThan(300, "the scan has to read the contract's source it clears");
        wrong.Should().BeEmpty(
            $"at {version} an obsolete member of the contract keeps the rule of the version: "
            + string.Join("; ", wrong));
    }

    private static bool IsObsolete(AttributeSyntax attribute) =>
        attribute.Name.ToString() is "Obsolete" or "ObsoleteAttribute" or "System.Obsolete" or "System.ObsoleteAttribute"
            or "global::System.Obsolete" or "global::System.ObsoleteAttribute";

    /// <summary>The attribute as written, or nothing when one of the three is not a literal.</summary>
    private static ObsoleteAttribute? Read(AttributeSyntax attribute)
    {
        var arguments = attribute.ArgumentList?.Arguments.ToList() ?? [];
        static string? Literal(ExpressionSyntax expression) =>
            expression is LiteralExpressionSyntax { Token.Value: string text } ? text : null;

        var positional = arguments.Where(argument => argument.NameEquals is null && argument.NameColon is null).ToList();
        var message = positional.Count > 0 ? Literal(positional[0].Expression) : null;
        if (positional.Count > 0 && message is null) return null;

        var obsolete = new ObsoleteAttribute(message);
        foreach (var named in arguments.Where(argument => argument.NameEquals is not null))
        {
            var value = Literal(named.Expression);
            if (value is null) return null;
            switch (named.NameEquals!.Name.Identifier.ValueText)
            {
                case nameof(ObsoleteAttribute.DiagnosticId): obsolete.DiagnosticId = value; break;
                case nameof(ObsoleteAttribute.UrlFormat): obsolete.UrlFormat = value; break;
            }
        }
        return obsolete;
    }

    /// <summary>The rule at both versions, on attributes made for the case: the scan above reads one
    /// version, the one this tree is at, and the other one has to be proved here.</summary>
    [Theory]
    [InlineData("0.2.0-preview.63", "EQO0001", "https://example.org/{0}", "Use Bar instead. Removed in 2.0.", "the preview removes")]
    [InlineData("1.2.0", null, "https://example.org/{0}", "Use Bar instead. Removed in 2.0.", "DiagnosticId")]
    [InlineData("1.2.0", "CS0618", "https://example.org/{0}", "Use Bar instead. Removed in 2.0.", "DiagnosticId")]
    [InlineData("1.2.0", "EQO0001", null, "Use Bar instead. Removed in 2.0.", "UrlFormat")]
    [InlineData("1.2.0", "EQO0001", "https://example.org/{0}", "Use Bar instead.", "removed in 2.0")]
    [InlineData("1.2.0", "EQO0001", "https://example.org/{0}", "Use Bar instead. Removed in 1.0.", "removed in 2.0")]
    public void AnObsoleteMemberThatBreaksTheRuleOfItsVersion_IsNamedForWhatItLacks(
        string version, string? id, string? url, string message, string lacks)
    {
        var obsolete = new ObsoleteAttribute(message) { DiagnosticId = id, UrlFormat = url };

        ObsoleteMark.Problems(version, obsolete).Should().ContainSingle().Which.Should().Contain(lacks);
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.0.0")]
    [InlineData("1.9.3+5a1b2c3")]
    public void AnObsoleteMemberThatSaysWhenItGoes_IsRightAtAStableVersion(string version)
    {
        var obsolete = new ObsoleteAttribute("Use Bar instead. Removed in 2.0.")
        {
            DiagnosticId = "EQO0001",
            UrlFormat = "https://github.com/eQuantic/equantic-ui/wiki/Upgrading#{0}",
        };

        ObsoleteMark.Problems(version, obsolete).Should().BeEmpty();
    }
}
