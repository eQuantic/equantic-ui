using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A <c>long</c> divided by a literal, handed to a node of the vocabulary, reaches it as a number (#1776).
/// An app listed "a long divided by an integer literal becomes a BigInt" among what breaks only in the
/// browser. Measured on the build an app runs, with the vocabulary referenced: the quotient is a BigInt,
/// as a long is, and C#'s implicit conversion into the parameter's float is written where it stands, so
/// no node receives one. This holds that, since the claim went into the issue without it.
/// </summary>
public class ALongHandedToTheVocabularyTests
{
    [Fact]
    public void AQuotientOfALong_ConvertsIntoTheVocabularysFloat()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            public sealed class Board : StatelessComponent
            {
                public long Total { get; set; } = 7;

                public override VisualNode Build(ComponentContext context) =>
                    new Column(gap: Total / 2, runGap: Total) { Width = SizeValue.Fill };
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: "Board.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => (MetadataReference)TestReferences.Of(file))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(CSharpCompilation.Create("Board", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

        var ts = compiler.CompileSource(source, "Board.cs").Single().TypeScript;

        ts.Should().Contain("new Column($eq.num.singleFromLong(this.total / 2n), 'start', 'stretch', false, $eq.num.singleFromLong(this.total)",
            "the BigInt quotient converts into the float the parameter takes, as C# converts it");
    }
}
