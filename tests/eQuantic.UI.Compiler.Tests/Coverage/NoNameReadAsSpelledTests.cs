using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// A name eqc reads from syntax is the name C# reads, never its spelling (#704). <c>@Shelf</c> and
/// <c>Shelf</c> are one type, and <c>Identifier.Text</c> is the one that keeps the <c>@</c>: a class
/// declared <c>@Shelf</c> went out as <c>export class @Shelf</c> in a module named <c>@Shelf</c>, a
/// record printed <c>@Point { X = 1 }</c>, and a constructed <c>new Store.@Failure()</c> imported
/// nothing, because the scan registered <c>@Failure</c> where every reference wrote <c>Failure</c>.
/// <para>
/// Two spellings are refused, with no baseline: <c>Identifier.Text</c> anywhere in the SDK's source,
/// where <c>Identifier.ValueText</c> is the name; and in the compiler, <c>ToString()</c> on a
/// <c>TypeSyntax</c>, where <c>ValueText()</c> is. The second needs the model, since a
/// <c>.Type.ToString()</c> is as often a symbol's display string, so the compiler's own source is
/// compiled here and each receiver's type asked of it.
/// </para>
/// </summary>
public class NoNameReadAsSpelledTests
{
    [Fact]
    public void NoIdentifierIsReadByItsSpelling()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles(Path.Combine(RepoRoot(), "src")))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview));
            offenders.AddRange(tree.GetCompilationUnitRoot().DescendantNodes().Where(ReadsTheSpelling)
                .Select(node => Where(file, tree, node)));
        }

        Assert.True(offenders.Count == 0,
            "A name is read by its spelling, so `@Shelf` is not `Shelf`. Read `Identifier.ValueText`:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// An identifier's <c>Text</c> read in any of the shapes C# writes it: <c>x.Identifier.Text</c>,
    /// <c>Identifier.Text</c>, <c>x?.Identifier.Text</c>, and the patterns that read the same,
    /// <c>{ Identifier.Text: … }</c> (a member access too) and <c>{ Identifier: { Text: … } }</c> (found in
    /// Copilot's second review of #931).
    /// </summary>
    private static bool ReadsTheSpelling(SyntaxNode node) => node switch
    {
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Text" } access => NamesIdentifier(access.Expression),
        SubpatternSyntax { ExpressionColon.Expression: IdentifierNameSyntax { Identifier.ValueText: "Text" } } inner =>
            inner.Parent?.Parent?.Parent is SubpatternSyntax { ExpressionColon.Expression: var outer } && NamesIdentifier(outer),
        _ => false,
    };

    private static bool NamesIdentifier(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax { Identifier.ValueText: "Identifier" }
            or MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Identifier" }
            or MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Identifier" };

    [Fact]
    public void NoTypeSyntaxBecomesANameByItsSpelling()
    {
        var root = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Compiler");
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        // The implicit usings are the build's, written into obj by the build this test runs after.
        var globalUsings = Directory.EnumerateFiles(Path.Combine(root, "obj"), "*.GlobalUsings.g.cs", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
        // …and the files it links from beside it, which its project names (`src/Shared`).
        var linked = System.Xml.Linq.XDocument.Load(Path.Combine(root, "eQuantic.UI.Compiler.csproj"))
            .Descendants("Compile")
            .Select(item => (string?)item.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(root, include)));
        var trees = SourceFiles(root).Concat(linked)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, path: file))
            .ToList();
        var analysed = trees.ToList();
        trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(globalUsings), options, path: globalUsings));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            // The source IS the compiler: its built assembly beside it would make every call ambiguous.
            .Where(path => Path.GetFileName(path) != "eQuantic.UI.Compiler.dll")
            .Select(path => (MetadataReference)TestReferences.Of(path));
        var compilation = CSharpCompilation.Create("eQuantic.UI.Compiler", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                // C# 15's syntax nodes, which the compiler reads under the same suppression.
                specificDiagnosticOptions: [new("RSEXPERIMENTAL006", ReportDiagnostic.Suppress)]));
        // A receiver the model cannot bind has no type to ask, so a missing reference would pass every
        // site in silence: the compiler's source compiles here, or the guard says why it cannot read it.
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(10).ToList();
        Assert.True(errors.Count == 0, "The compiler's source does not compile here:\n  " + string.Join("\n  ", errors));
        var typeSyntax = compilation.GetTypeByMetadataName(typeof(TypeSyntax).FullName!);
        Assert.NotNull(typeSyntax);

        var offenders = new List<string>();
        foreach (var tree in analysed)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var call in tree.GetCompilationUnitRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.ArgumentList.Arguments.Count != 0) continue;
                var receiver = call.Expression switch
                {
                    MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" } access => access.Expression,
                    MemberBindingExpressionSyntax { Name.Identifier.ValueText: "ToString" } binding
                        => (binding.Parent?.Parent as ConditionalAccessExpressionSyntax)?.Expression
                           ?? binding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()?.Expression,
                    _ => null,
                };
                if (receiver is null || model.GetTypeInfo(receiver).Type is not { } type) continue;
                for (var walked = type; walked is not null; walked = walked.BaseType)
                {
                    if (!SymbolEqualityComparer.Default.Equals(walked, typeSyntax)) continue;
                    offenders.Add(Where(tree.FilePath, tree, call));
                    break;
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "A type's name is taken from its spelling, so `Store.@Box` is not `Store.Box`. Read `ValueText()`:\n  "
            + string.Join("\n  ", offenders));
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Where(string file, SyntaxTree tree, SyntaxNode node) =>
        $"{Path.GetRelativePath(RepoRoot(), file).Replace(Path.DirectorySeparatorChar, '/')}:"
        + $"{tree.GetLineSpan(node.Span).StartLinePosition.Line + 1}";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
