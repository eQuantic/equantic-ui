using eQuantic.UI.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Build;

/// <summary>
/// The families an app declares from Google Fonts, <c>[assembly: GoogleFont("Inter", FontWeight.Bold)]</c>,
/// read from the syntax of its source files BEFORE the app compiles.
/// <para>
/// A face has to be embedded in the same compile that names it through the font catalog, so the build
/// reads the declaration before the compiler runs, and before any constant elsewhere in the app exists.
/// The family is therefore a string literal, each weight a member of <see cref="FontWeight"/> and
/// <c>Italic</c> <c>true</c> or <c>false</c>, written out where the attribute stands: anything else is
/// EQ4007 at its place, as EQ3003 is for a capability's reason built at run time. The compiler binds the
/// attribute again when the app compiles, so a misspelt member fails there too.
/// </para>
/// </summary>
public static class GoogleFontDeclarations
{
    /// <summary>The code a declaration the build cannot read fails with.</summary>
    public const string UnreadableCode = "EQ4007";

    /// <summary>The attribute's name as an app may write it, with or without its suffix.</summary>
    private static readonly HashSet<string> Names = ["GoogleFont", "GoogleFontAttribute"];

    /// <summary>One family the app declares: the cuts it ships, and where it is first declared.</summary>
    /// <param name="Family">The family's name at Google Fonts.</param>
    /// <param name="Cuts">Its cuts, each once, upright before italic and the lightest first.</param>
    /// <param name="Place">Where it is declared, in MSBuild's form: <c>path(line,column)</c>.</param>
    public sealed record Declaration(string Family, IReadOnlyList<FontCut> Cuts, string Place);

    /// <summary>
    /// Every family the files at <paramref name="paths"/> declare, the cuts of a family declared twice
    /// together, or the errors that stop the build, each one line in MSBuild's own form.
    /// </summary>
    public static (IReadOnlyList<Declaration> Declarations, IReadOnlyList<string> Errors) Read(IEnumerable<string> paths)
    {
        var errors = new List<string>();
        var families = new List<(string Family, SortedSet<FontCut> Cuts, string Place)>();
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        foreach (var path in paths)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path).GetRoot();
            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                if (attribute.Parent is not AttributeListSyntax { Target.Identifier.RawKind: (int)SyntaxKind.AssemblyKeyword }
                    || !Names.Contains(RightmostName(attribute.Name)))
                    continue;
                var place = Place(attribute);
                if (Read(attribute, place, errors) is not { } declared) continue;
                var family = families.FindIndex(entry => entry.Family == declared.Family);
                if (family < 0)
                    families.Add((declared.Family, [.. declared.Cuts], place));
                else
                    families[family].Cuts.UnionWith(declared.Cuts);
            }
        }
        return (families.Select(entry => new Declaration(entry.Family, [.. entry.Cuts], entry.Place)).ToList(), errors);
    }

    /// <summary>One attribute's family and cuts, or null once its errors are added to <paramref name="errors"/>.</summary>
    private static Declaration? Read(AttributeSyntax attribute, string place, List<string> errors)
    {
        var count = errors.Count;
        string? family = null;
        var weights = new List<int>();
        var italic = false;
        foreach (var argument in attribute.ArgumentList?.Arguments ?? [])
        {
            if (argument.NameEquals is { } property)
            {
                if (property.Name.Identifier.ValueText != nameof(GoogleFontAttribute.Italic))
                    errors.Add(Error(place, $"GoogleFont has no {property.Name.Identifier.ValueText} the build reads"));
                else if (argument.Expression.Kind() is SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression)
                    italic = argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression);
                else
                    errors.Add(Error(place, $"GoogleFont's Italic is read before the app compiles, so it is written out as true or false, not as '{argument.Expression}'"));
            }
            else if (family is null && argument.NameColon is null or { Name.Identifier.ValueText: "family" })
            {
                if (argument.Expression is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression))
                    errors.Add(Error(place, $"GoogleFont's family is read before the app compiles, so it is written out as a string literal, \"Inter\", not as '{argument.Expression}'"));
                else if (literal.Token.ValueText.Trim() is { Length: > 0 } name)
                    family = name;
                else
                    errors.Add(Error(place, "GoogleFont names no family"));
                family ??= "";
            }
            else if (argument.NameColon is null && Weight(argument.Expression) is { } weight)
            {
                weights.Add(weight);
            }
            else
            {
                errors.Add(Error(place, $"GoogleFont's weights are read before the app compiles, so each is written out as a member of FontWeight, FontWeight.Bold, not as '{argument.Expression}'"));
            }
        }
        if (family is null) errors.Add(Error(place, "GoogleFont names no family"));
        if (errors.Count > count) return null;

        if (weights.Count == 0) weights.Add((int)FontWeight.Regular);
        var cuts = new SortedSet<FontCut>();
        foreach (var weight in weights)
        {
            cuts.Add(new FontCut(weight, false));
            if (italic) cuts.Add(new FontCut(weight, true));
        }
        return new Declaration(family!, [.. cuts], place);
    }

    /// <summary>The weight <c>FontWeight.Bold</c> names, however its type is qualified, or null.</summary>
    private static int? Weight(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax { Name: IdentifierNameSyntax member } access
        && RightmostName(access.Expression) == nameof(FontWeight)
        && Enum.TryParse<FontWeight>(member.Identifier.ValueText, ignoreCase: false, out var weight)
        && Enum.IsDefined(weight)
            ? (int)weight
            : null;

    private static string RightmostName(ExpressionSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => "",
    };

    private static string Place(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1})";
    }

    private static string Error(string place, string message) => $"{place}: error {UnreadableCode}: {message}.";
}
