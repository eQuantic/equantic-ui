using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
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
/// What it reads is the SOURCE of every contract project (<see cref="SurfaceClass"/>): the tools and
/// the native track are outside the promise. An assembly this run references would leave out the
/// server, the integrations and the icon packs, which no test references, and a scan of those was
/// what Copilot found missing twice.
/// </para>
/// </summary>
public class NoOldShapeSurvivesTests
{
    /// <summary>Every source file of every contract project, parsed once, with what the build writes
    /// under <c>obj/</c> and <c>bin/</c> left out.</summary>
    private static readonly Lazy<IReadOnlyList<(string Path, SyntaxNode Root)>> ContractSource = new(() =>
        SurfaceClass.ContractProjects()
            .SelectMany(project => Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (path, CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseDefaults.Options, path).GetRoot()))
            .ToList());

    /// <summary>The scans have to read the contract they clear, the server and an icon pack among it.</summary>
    [Fact]
    public void TheScansReadTheSourceOfEveryContractProject()
    {
        SurfaceClass.ContractProjects().Select(Path.GetFileName).Should().Contain(
            ["eQuantic.UI.Primitives", "eQuantic.UI.Components", "eQuantic.UI.Web", "eQuantic.UI.Server", "eQuantic.UI.Lucide"],
            "the guard reads every contract project, the server and the icon packs among them");
        ContractSource.Value.Count.Should().BeGreaterThan(300, "the scans have to read the contract's source they clear");
    }

    /// <summary>
    /// A positional record SYNTHESISES exactly one <c>Deconstruct</c>, so a second one exists for
    /// one reason only: an older arity somebody wanted to keep reachable. That is the tell, and it
    /// is precise — a legitimate second CONSTRUCTOR is common here (<c>ColorToken(light, dark)</c>
    /// beside <c>ColorToken(both)</c>, <c>CornerRadii</c>'s four corners beside its one), which is
    /// why the constructor count is deliberately NOT what this asks about.
    /// <para>
    /// Read from the source, a type counts the instance <c>Deconstruct</c> methods it declares in
    /// every part of it, and one more when it is a positional record with a parameter, whose
    /// <c>Deconstruct</c> the compiler writes. An extension method of that name declares nothing on
    /// the type it extends.
    /// </para>
    /// </summary>
    [Fact]
    public void NoTypeDeclaresASecondDeconstruct()
    {
        var types = new Dictionary<string, (int Count, string Where)>(StringComparer.Ordinal);
        foreach (var (path, root) in ContractSource.Value)
        {
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var declared = type.Members.OfType<MethodDeclarationSyntax>().Count(method =>
                    method.Identifier.ValueText == "Deconstruct" && !method.Modifiers.Any(SyntaxKind.StaticKeyword));
                var written = type is RecordDeclarationSyntax { ParameterList.Parameters.Count: > 0 } ? 1 : 0;
                if (declared + written == 0) continue;

                var name = FullName(type);
                var where = $"{Path.GetRelativePath(SdkProbe.RepoRoot(), path)}:{type.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                types[name] = types.TryGetValue(name, out var seen) ? (seen.Count + declared + written, seen.Where) : (declared + written, where);
            }
        }

        var doubled = types.Where(type => type.Value.Count > 1)
            .Select(type => $"{type.Key} ({type.Value.Count}, at {type.Value.Where})")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();
        types.Should().NotBeEmpty("the contract has positional records, and a scan that found none has lost its way");
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

        var wrong = new List<string>();
        foreach (var (path, root) in ContractSource.Value)
        {
            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>().Where(IsObsolete))
            {
                var where = $"{Path.GetRelativePath(SdkProbe.RepoRoot(), path)}:{attribute.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                if (Read(attribute) is not { } obsolete)
                {
                    wrong.Add($"{where}: its message, DiagnosticId and UrlFormat are written as literals, so this guard can read them");
                    continue;
                }
                wrong.AddRange(ObsoleteMark.Problems(version, obsolete).Select(problem => $"{where}: {problem}"));
            }
        }

        wrong.Should().BeEmpty(
            $"at {version} an obsolete member of the contract keeps the rule of the version: "
            + string.Join("; ", wrong));
    }

    /// <summary>A type's name with its namespace, the types it is nested in and its arity, so the
    /// parts of a partial type count as one.</summary>
    private static string FullName(TypeDeclarationSyntax type)
    {
        var names = new List<string>();
        for (SyntaxNode? node = type; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case TypeDeclarationSyntax declared:
                    names.Add(declared.Identifier.ValueText + (declared.TypeParameterList is { } generic ? $"`{generic.Parameters.Count}" : string.Empty));
                    break;
                case BaseNamespaceDeclarationSyntax space:
                    names.Add(space.Name.ToString());
                    break;
            }
        }
        names.Reverse();
        return string.Join(".", names);
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

        // The message is the constructor's first argument, written in its place or by its name,
        // `message:`, which may come after `error:`.
        var positional = arguments.Where(argument => argument.NameEquals is null && argument.NameColon is null).ToList();
        var written = arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "message")
            ?? (positional.Count > 0 ? positional[0] : null);
        var message = written is null ? null : Literal(written.Expression);
        if (written is not null && message is null) return null;

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
    [InlineData("1.2.0", "EQO0001", "not a link", "Use Bar instead. Removed in 2.0.", "UrlFormat")]
    [InlineData("1.2.0", "EQO0001", "https://example.org/{1}", "Use Bar instead. Removed in 2.0.", "UrlFormat")]
    [InlineData("1.2.0", "EQO0001", "http://example.org/{0}", "Use Bar instead. Removed in 2.0.", "UrlFormat")]
    [InlineData("1.2.0", "EQO0001", "https://example.org/{0}", "Use Bar instead.", "removed in 2.0")]
    [InlineData("1.2.0", "EQO0001", "https://example.org/{0}", "Use Bar instead. Removed in 1.0.", "removed in 2.0")]
    public void AnObsoleteMemberThatBreaksTheRuleOfItsVersion_IsNamedForWhatItLacks(
        string version, string? id, string? url, string message, string lacks)
    {
        var obsolete = new ObsoleteAttribute(message) { DiagnosticId = id, UrlFormat = url };

        ObsoleteMark.Problems(version, obsolete).Should().ContainSingle().Which.Should().Contain(lacks);
    }

    /// <summary>The reader takes the attribute as C# takes it: the message by position or by name,
    /// in any order with <c>error</c>.</summary>
    [Theory]
    [InlineData("[Obsolete(\"Use Bar. Removed in 2.0.\", DiagnosticId = \"EQO0001\", UrlFormat = \"https://example.org/{0}\")]")]
    [InlineData("[Obsolete(\"Use Bar. Removed in 2.0.\", false, DiagnosticId = \"EQO0001\", UrlFormat = \"https://example.org/{0}\")]")]
    [InlineData("[Obsolete(message: \"Use Bar. Removed in 2.0.\", error: false, DiagnosticId = \"EQO0001\", UrlFormat = \"https://example.org/{0}\")]")]
    [InlineData("[System.Obsolete(error: false, message: \"Use Bar. Removed in 2.0.\", DiagnosticId = \"EQO0001\", UrlFormat = \"https://example.org/{0}\")]")]
    public void AnObsoleteIsReadAsCSharpReadsIt(string attribute)
    {
        var syntax = CSharpSyntaxTree.ParseText($"{attribute} public class Probe {{ }}", ParseDefaults.Options)
            .GetRoot().DescendantNodes().OfType<AttributeSyntax>().Single();

        var read = Read(syntax);

        read.Should().NotBeNull();
        read!.Message.Should().Be("Use Bar. Removed in 2.0.");
        ObsoleteMark.Problems("1.2.0", read).Should().BeEmpty();
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
