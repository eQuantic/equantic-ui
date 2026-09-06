using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The D14 guard, guarded the way the factory surface is: a component must never HARDCODE a
/// user-facing string. Every built-in label and announcement reads <c>SdkStrings</c>, so the
/// fourteen audited literals cannot grow back one careless component at a time — which is how a
/// 100% Portuguese app ends up announcing "Checked" to VoiceOver.
/// <para>
/// The walk is syntactic and narrow ON PURPOSE: only the positions that reach a human — a
/// <c>Label</c>/<c>Placeholder</c>/<c>Description</c> property assignment or the same-named
/// argument — are policed. A literal anywhere else in a component (a CSS keyword, a glyph name, a
/// data-attribute) is machine text, and a guard that flagged those would be turned off within a
/// week.
/// </para>
/// </summary>
public class SdkStringLiteralGuardTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static readonly string[] HumanFacingNames = ["Label", "Placeholder", "Description"];

    [Fact]
    public void NoComponent_HardcodesAUserFacingString()
    {
        var componentsDir = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Components");
        var files = Directory.GetFiles(componentsDir, "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Charts"), "*.cs"))
            .Where(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .ToList();
        files.Should().HaveCountGreaterThan(30, "the shared component library");

        var offenders = new List<string>();
        foreach (var file in files)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var name = Path.GetFileName(file);

            // `Label = "Dismiss"` — an initializer or a plain assignment.
            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Right is not LiteralExpressionSyntax literal) continue;
                if (literal.Token.Kind() != SyntaxKind.StringLiteralToken) continue;
                var target = assignment.Left switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.Text,
                    MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                    _ => "",
                };
                if (HumanFacingNames.Contains(target) && literal.Token.ValueText.Length > 0)
                    offenders.Add($"{name}: {target} = \"{literal.Token.ValueText}\"");
            }

            // `new IconButton(new Icon(Icons.Close), "Dismiss")` reached by NAME — `label: "…"`.
            foreach (var argument in root.DescendantNodes().OfType<ArgumentSyntax>())
            {
                if (argument.NameColon is null) continue;
                if (argument.Expression is not LiteralExpressionSyntax literal) continue;
                if (literal.Token.Kind() != SyntaxKind.StringLiteralToken) continue;
                var parameter = argument.NameColon.Name.Identifier.Text;
                if (HumanFacingNames.Any(n => string.Equals(n, parameter, StringComparison.OrdinalIgnoreCase))
                    && literal.Token.ValueText.Length > 0)
                    offenders.Add($"{name}: {parameter}: \"{literal.Token.ValueText}\"");
            }
        }

        offenders.Should().BeEmpty(
            "a component's user-facing text belongs in SdkStrings (resx-backed, D14) — otherwise a "
            + "localized app still announces English. Offenders: " + string.Join("; ", offenders));
    }

    [Fact]
    public void EverySdkString_IsReadByAtLeastOneComponent()
    {
        // The other direction: a property nobody reads is a translation cost with no user. It also
        // catches the rename that leaves the old key behind.
        var componentsDir = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Components");
        var sources = string.Join("\n", Directory.GetFiles(componentsDir, "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Charts"), "*.cs"))
            .Where(f => !f.EndsWith("SdkStrings.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText));

        // A string can also reach a human through ANOTHER string: the date hint is built from the
        // culture's pattern and the three letters this language stands the parts on, and only the
        // hint is what a field shows. So a property one of SdkStrings' own members reads counts as
        // read — the member doing the reading is itself policed by the rule above.
        var own = CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(componentsDir, "SdkStrings.cs"))).GetRoot();
        // OUTSIDE its own declaration is the whole of it. Every property here is written
        // `X => SdkResources.X`, so a name always occurs inside its own body — counting that
        // would mark all sixteen as read and leave a guard that can never fail.
        var readByAnotherString = own.DescendantNodes().OfType<IdentifierNameSyntax>()
            // A `<see cref="…"/>` is a mention, not a read — the other way this could go soft.
            .Where(identifier => !identifier.Ancestors()
                .OfType<DocumentationCommentTriviaSyntax>().Any())
            .Where(identifier => identifier.FirstAncestorOrSelf<PropertyDeclarationSyntax>()
                ?.Identifier.Text != identifier.Identifier.Text)
            .Select(identifier => identifier.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);

        var unread = typeof(Components.SdkStrings).GetProperties()
            .Where(p => !sources.Contains($"SdkStrings.{p.Name}", StringComparison.Ordinal))
            .Where(p => !readByAnotherString.Contains(p.Name))
            .Select(p => p.Name)
            .ToList();

        unread.Should().BeEmpty("every SDK string must have a component that says it");
    }
}
