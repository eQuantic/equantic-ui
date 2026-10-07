using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// Every name the emitted code DECLARES, where C# declared nothing, starts with a `$`, which no C#
/// identifier holds. The lowerings named their own bindings like a C# local would (<c>_sum</c>,
/// <c>key</c>, the comparator's <c>a</c> and <c>b</c>, the filter's <c>x</c>), and a C# local of that
/// name, read inside the code the lowering wraps, read the lowering's binding instead: a captured
/// <c>_sum</c> read the running total, a captured <c>key</c> threw before it was set, and a sequence
/// named <c>x</c> was the element it was compared with (#397). The compiler's own source is read for
/// every binding a string it writes declares, so the next lowering cannot copy the old spelling.
/// </summary>
public class EmittedBindingNamesTests
{
    /// <summary>The bindings the emitter declares that are C#'s own names: a setter's parameter is
    /// <c>value</c> because the setter's body, which the developer wrote, reads it by that name.</summary>
    private static readonly HashSet<string> CSharpsOwn = new(StringComparer.Ordinal) { "value" };

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..", ".."));

    /// <summary>
    /// Every binding a string of the compiler's code generator declares, with where: an arrow's or a
    /// function's parameters, a <c>const</c>, <c>let</c> or <c>var</c>, a <c>catch</c>'s, and the
    /// parameters a string hands an IR arrow. Read as C#, by the parser eqc reads with, since what
    /// matters is the text a string holds.
    /// </summary>
    private static IReadOnlyList<(string Name, string Where)> DeclaredBindings()
    {
        var found = new List<(string, string)>();
        var codeGen = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Compiler", "CodeGen");
        foreach (var file in Directory.EnumerateFiles(codeGen, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseDefaults.Options).GetRoot();
            var relative = Path.GetRelativePath(codeGen, file);
            foreach (var node in root.DescendantNodes())
            {
                var text = node switch
                {
                    LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
                    InterpolatedStringExpressionSyntax interpolated => string.Concat(interpolated.Contents.Select(content =>
                        content is InterpolatedStringTextSyntax piece ? piece.TextToken.ValueText : "⟪hole⟫")),
                    _ => null,
                };
                if (text is null) continue;
                var where = $"{relative}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                foreach (var name in Declared(text)) found.Add((name, where));
            }
        }
        return found;
    }

    private static IEnumerable<string> Declared(string text)
    {
        foreach (Match arrow in Regex.Matches(text, @"\(([^()]*)\)\s*=>"))
            foreach (var parameter in Parameters(arrow.Groups[1].Value)) yield return parameter;
        foreach (Match arrow in Regex.Matches(text, @"(?<![\w$.⟫)])([A-Za-z_$][\w$]*)\s*=>"))
            if (arrow.Groups[1].Value != "async") yield return arrow.Groups[1].Value;
        foreach (Match declaration in Regex.Matches(text, @"\b(?:const|let|var)\s+([A-Za-z_$][\w$]*)"))
            yield return declaration.Groups[1].Value;
        foreach (Match function in Regex.Matches(text, @"\bfunction\s*\*?\s*[A-Za-z_$]*\s*\(([^()]*)\)"))
            foreach (var parameter in Parameters(function.Groups[1].Value)) yield return parameter;
        foreach (Match handler in Regex.Matches(text, @"\bcatch\s*\(\s*([A-Za-z_$][\w$]*)"))
            yield return handler.Groups[1].Value;
    }

    /// <summary>The names of a parameter list, without their types, defaults or spreads; a part a hole
    /// fills is the C#'s own and is skipped.</summary>
    private static IEnumerable<string> Parameters(string list)
    {
        foreach (var raw in list.Split(','))
        {
            var parameter = raw.Trim();
            if (parameter.Length == 0 || parameter.Contains('⟪')) continue;
            parameter = parameter.TrimStart('.');
            var colon = parameter.IndexOf(':');
            if (colon >= 0) parameter = parameter[..colon];
            var equals = parameter.IndexOf('=');
            if (equals >= 0) parameter = parameter[..equals];
            parameter = parameter.Trim();
            if (Regex.IsMatch(parameter, @"^[A-Za-z_$][\w$]*$")) yield return parameter;
        }
    }

    [Fact]
    public void TheScan_FindsTheBindingsItIsKnownToDeclare()
    {
        // The instrument first: a scan that read nothing would pass the test below with nothing in it.
        DeclaredBindings().Select(binding => binding.Name).Should().Contain(["$sum", "$groups", "$resolve", "$seen"]);
    }

    [Fact]
    public void EveryBindingTheEmittedCodeDeclares_StartsWithADollar()
    {
        var offenders = DeclaredBindings()
            .Where(binding => !binding.Name.StartsWith('$') && !CSharpsOwn.Contains(binding.Name))
            .Select(binding => $"{binding.Name} at {binding.Where}")
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "a binding the emitted code declares is named with a `$`, which no C# name holds, or a C# local "
            + "of that name read inside the code around it reads the binding instead (#397)");
    }
}
