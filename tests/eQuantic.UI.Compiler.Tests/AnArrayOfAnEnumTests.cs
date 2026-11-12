using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An array of an enum is typed by what the enum crosses as, as a bare one is (#1064): a vocabulary enum's
/// <c>&lt;Enum&gt;Value</c> union, imported from the runtime, and an app's own enum's member string. A static
/// field typed <c>CodeCommand[]</c> wrote accessors typed by the enum's C# name, which no module exports, and
/// the runtime's <c>tsc</c> refused the library's module (TS2304).
/// </summary>
public sealed class AnArrayOfAnEnumTests
{
    /// <summary>The twin of a class holding <paramref name="members"/>, compiled as a project is: the
    /// vocabulary referenced, and the C# held to compile with no error.</summary>
    private static string Compile(string members)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;
            public enum Shade { Light, Dark }
            public sealed class Palette
            {
                {{members}}
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "Palette.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Palette", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the C# a case writes compiles");

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Palette.cs").Single(result => result.ComponentName == "Palette").TypeScript;
    }

    [Theory]
    [InlineData("private static readonly TypeRole[] Roles = [TypeRole.BodyM];", "static get roles(): TypeRoleValue[]")]
    [InlineData("private static readonly TypeRole[][] Rows = [[TypeRole.BodyM]];", "static get rows(): TypeRoleValue[][]")]
    [InlineData("private readonly TypeRole[] _roles = [TypeRole.BodyM]; public int Count => _roles.Length;", "_roles!: TypeRoleValue[]")]
    [InlineData("private static readonly Shade[] Shades = [Shade.Light];", "static get shades(): string[]")]
    public void AnArrayOfAnEnum_IsTypedAsTheEnumCrosses(string members, string annotation)
    {
        var palette = Compile(members);

        palette.Should().Contain(annotation);
        palette.Should().NotMatchRegex(@":\s*(TypeRole|Shade)(\[\])+", "no module exports the enum's C# name");
    }

    [Fact]
    public void AVocabularyEnumsUnion_IsImportedFromTheRuntime() =>
        Compile("private static readonly TypeRole[] Roles = [TypeRole.BodyM];")
            .Should().MatchRegex(@"import \{[^}]*\bTypeRoleValue\b[^}]*\} from ""@equantic/runtime""");
}
