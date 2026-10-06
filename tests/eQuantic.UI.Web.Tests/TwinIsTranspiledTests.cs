using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <c>[TwinIsTranspiled]</c> marks exactly the vocabulary's types whose twins the runtime transpiles
/// from their C# (#592): every class, record and struct of the folders <see cref="SharedComponentTranspilationTests"/>
/// transpiles out of Primitives, and no other type of the assembly. The compiler reads the mark where
/// an app reaches them as metadata, so a type missing it is built through a config object its twin
/// does not take, and a hand-written twin carrying it is built through a constructor it does not have.
/// </summary>
public class TwinIsTranspiledTests
{
    /// <summary>The Primitives folders the runtime transpiles, as SharedComponentTranspilationTests reads them.</summary>
    private static readonly string[] TranspiledFolders = ["Sheet", "Forms"];

    [Fact]
    public void EveryTypeTheRuntimeTranspilesOutOfPrimitives_IsMarked_AndNoOther()
    {
        var declared = TranspiledFolders
            .SelectMany(folder => Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Primitives", folder), "*.cs"))
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot()
                .DescendantNodes().OfType<TypeDeclarationSyntax>())
            .Where(type => type is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax)
            .Select(type => type.Identifier.ValueText)
            .ToHashSet();
        var marked = typeof(VisualNode).Assembly.GetTypes()
            .Where(type => type.IsDefined(typeof(TwinIsTranspiledAttribute), inherit: false))
            .Select(type => type.Name)
            .ToHashSet();

        declared.Should().NotBeEmpty("the folders the runtime transpiles were found and read");
        marked.Should().BeEquivalentTo(declared,
            "a transpiled twin takes the C# constructor and a hand-written one a config object, and the mark is how an app's build tells them apart");
    }

    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        return here!.FullName;
    }
}
