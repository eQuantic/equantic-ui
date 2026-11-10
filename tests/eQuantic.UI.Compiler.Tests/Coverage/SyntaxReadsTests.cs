using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// Every read of a declaration's syntax in the compiler is noted in the record of the compile that made
/// it (#1281). A served eqc compiles again, after a save that changed bodies alone, only the files whose
/// compile read a changed file, so a read that is not noted is a module left stale: it builds green and
/// is wrong in the browser, and no other test would see it.
/// <para>
/// Two rules, with no baseline to grow: a symbol's syntax and locations are read through
/// <c>SymbolSources</c> (<c>ReadDeclarations</c>, <c>ReadLocations</c>, <c>ReadApplication</c>), never
/// raw, except for the two forms that read nothing a body holds (how many declarations a symbol has,
/// and whether it is declared in source). And a cache keyed by a symbol or a compilation, which would
/// hand an answer read from another file to a compile that did not note the read, is one of the caches
/// below, each with what makes it safe.
/// </para>
/// </summary>
public class SyntaxReadsTests
{
    private static readonly string[] SymbolToSyntax = ["DeclaringSyntaxReferences", "Locations", "ApplicationSyntaxReference"];

    /// <summary>The caches keyed by a symbol or a compilation, by file and field, and why each hands on
    /// no read a compile did not note. A new one is added here by hand, with its reason.</summary>
    private static readonly Dictionary<string, string> Caches = new(StringComparer.Ordinal)
    {
        ["src/eQuantic.UI.Compiler/CodeGen/ExplicitImplementations.cs:Cache"] =
            "keeps the files it read beside each answer, and notes them where the answer is taken",
        ["src/eQuantic.UI.Compiler/CodeGen/Strategies/ValueCopies.cs:Mutating"] =
            "keeps the files it read beside each answer, and notes them where the answer is taken",
        ["src/eQuantic.UI.Compiler/CodeGen/Extensions/ServerOnlyContracts.cs:Read"] =
            "reads declarations alone (attributes and interface maps), which a save that changed bodies alone leaves as they were",
        ["src/eQuantic.UI.Compiler/CodeGen/Extensions/FieldSlotExtensions.cs:Descendants"] =
            "reads declarations alone (each type's members and base), which a save that changed bodies alone leaves as they were",
        ["src/eQuantic.UI.Compiler/Services/ResourceClasses.cs:IdCache"] =
            "reads declarations alone (the names of the resource classes), which a save that changed bodies alone leaves as they were",
    };

    private static readonly HashSet<string> KeyedBy = new(StringComparer.Ordinal)
    {
        "ISymbol", "INamedTypeSymbol", "ITypeSymbol", "IMethodSymbol", "IPropertySymbol", "IFieldSymbol",
        "IEventSymbol", "IAssemblySymbol", "INamespaceSymbol", "IModuleSymbol", "Compilation", "CSharpCompilation",
    };

    [Fact]
    public void EveryReadOfADeclarationsSyntax_IsNoted()
    {
        var offenders = new List<string>();
        foreach (var (file, tree) in CompilerTrees())
        {
            if (Path.GetFileName(file) == "SymbolSources.cs") continue;
            offenders.AddRange(tree.GetCompilationUnitRoot().DescendantNodes().OfType<SimpleNameSyntax>()
                .Where(name => SymbolToSyntax.Contains(name.Identifier.ValueText) && !ReadsNothingABodyHolds(name))
                .Select(name => Where(file, tree, name)));
        }

        Assert.True(offenders.Count == 0,
            "A declaration's syntax is read where no compile notes it, so a served eqc would leave a module stale "
            + "after a save that changed the file it read. Read it through SymbolSources (ReadDeclarations, "
            + "ReadLocations, ReadApplication):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary><c>DeclaringSyntaxReferences.Length</c> (a count of declarations), in an expression or a
    /// pattern, and <c>Locations.Any(x =&gt; x.IsInSource)</c>.</summary>
    private static bool ReadsNothingABodyHolds(SimpleNameSyntax name)
    {
        var accessed = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : (SyntaxNode)name;
        var after = accessed.Parent as MemberAccessExpressionSyntax;
        return name.Identifier.ValueText switch
        {
            "DeclaringSyntaxReferences" => after is { Name.Identifier.ValueText: "Length" } && after.Expression == accessed,
            "Locations" => after is { Name.Identifier.ValueText: "Any" } && after.Expression == accessed
                && after.Parent is InvocationExpressionSyntax { ArgumentList.Arguments: [{ Expression: SimpleLambdaExpressionSyntax lambda }] }
                && lambda.Body is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsInSource", Expression: IdentifierNameSyntax over }
                && over.Identifier.ValueText == lambda.Parameter.Identifier.ValueText,
            _ => false,
        };
    }

    [Fact]
    public void EveryCacheKeyedByASymbolOrACompilation_IsListedWithItsReason()
    {
        var found = new List<string>();
        foreach (var (file, tree) in CompilerTrees())
        {
            foreach (var field in tree.GetCompilationUnitRoot().DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                // Named as it is written, qualified or not: `System.Collections.Generic.Dictionary<…>` is one too.
                var type = field.Declaration.Type switch
                {
                    QualifiedNameSyntax qualified => qualified.Right,
                    AliasQualifiedNameSyntax aliased => aliased.Name,
                    var plain => plain,
                };
                if (type is not GenericNameSyntax { TypeArgumentList.Arguments: [var key, ..] } generic
                    || generic.Identifier.ValueText is not ("ConditionalWeakTable" or "Dictionary" or "ConcurrentDictionary")
                    || !KeyedBy.Contains(LastName(key)))
                    continue;
                foreach (var variable in field.Declaration.Variables)
                    found.Add($"{Relative(file)}:{variable.Identifier.ValueText}");
            }
        }

        var unlisted = found.Where(cache => !Caches.ContainsKey(cache)).ToList();
        var gone = Caches.Keys.Where(cache => !found.Contains(cache)).ToList();
        Assert.True(unlisted.Count == 0,
            "A cache keyed by a symbol or a compilation may hand an answer read from another file to a compile that "
            + "did not note the read. Keep the files read beside the answer and note them where it is taken, then list "
            + "it with its reason:\n  " + string.Join("\n  ", unlisted));
        Assert.True(gone.Count == 0, "A listed cache is gone, so its entry goes too:\n  " + string.Join("\n  ", gone));
    }

    private static string LastName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => type.ToString(),
    };

    private static IEnumerable<(string File, SyntaxTree Tree)> CompilerTrees()
    {
        var root = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Compiler");
        var options = new CSharpParseOptions(LanguageVersion.Preview);
        // …and the files its project links from beside it (`src/Shared`), which compile into it as well.
        var linked = System.Xml.Linq.XDocument.Load(Path.Combine(root, "eQuantic.UI.Compiler.csproj"))
            .Descendants("Compile")
            .Select(item => (string?)item.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(root, include)));
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Concat(linked)
            .Select(file => (file, CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, path: file)));
    }

    private static string Relative(string file) =>
        Path.GetRelativePath(RepoRoot(), file).Replace(Path.DirectorySeparatorChar, '/');

    private static string Where(string file, SyntaxTree tree, SyntaxNode node) =>
        $"{Relative(file)}:{tree.GetLineSpan(node.Span).StartLinePosition.Line + 1}";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
