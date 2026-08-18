using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A local function inside <c>Build</c>, which is how a screen names the row it repeats.
///
/// <para>
/// C# HOISTS a local function: it can be called above its own declaration, and the CLR neither
/// knows nor cares that it is not a member. JavaScript hoists a `function` declaration and does not
/// hoist a `const` arrow, so the two languages disagree about a shape that costs nothing to write.
/// The disagreement is invisible on the server, which runs the C#, and fatal in the browser, which
/// runs the emission of it — the worst place for a difference to live.
/// </para>
/// </summary>
public class LocalFunctionCrossingTests
{
    private static string Transpile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "HelperPage.cs");
        var usings = CSharpSyntaxTree.ParseText(
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;",
            path: "GlobalUsings.g.cs");

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create("LocalFunctions", [tree, usings], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "HelperPage.cs").Single().TypeScript;
    }

    private const string HelperSource = """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace eQuantic.UI.Web.Tests.Fixtures;

        public sealed class RowsPage : StatelessComponent
        {
            private static readonly string[] Items = ["one", "two"];

            public override VisualNode Build(ComponentContext context)
            {
                var labels = Items.Select(Row).ToArray();
                return new Text(string.Join(", ", labels), TypeRole.BodyM);

                string Row(string item) => item.ToUpperInvariant();
            }
        }
        """;

    /// <summary>
    /// A local function is NOT a member, so a reference to it is not a member access. Emitting
    /// <c>this.row.bind(this)</c> reads the name off an object that never had it: `undefined.bind`
    /// throws where the C# ran fine, and the page dies at the first row it tries to build.
    /// </summary>
    [Fact]
    public void ALocalFunction_PassedAsADelegate_IsNotAMemberOfThis()
    {
        var page = Transpile(HelperSource);

        page.Should().NotContain("this.row", "a local function is a function in scope, not a member");
        page.Should().Contain(".map(row)", "the method group IS the local, passed by name");
    }

    /// <summary>
    /// C# lets the declaration come after the use. A `const` arrow emitted in that order leaves the
    /// name in its temporal dead zone at the moment the code reads it, which is a ReferenceError on
    /// the one target that runs the emission — so the declarations lead the block, in source order.
    /// </summary>
    [Fact]
    public void ALocalFunction_DeclaredAfterItsUse_IsHoistedLikeCSharpHoistsIt()
    {
        var page = Transpile(HelperSource);

        var declaration = page.IndexOf("const row = ", StringComparison.Ordinal);
        var use = page.IndexOf(".map(row)", StringComparison.Ordinal);

        declaration.Should().BeGreaterThan(-1, "the local function has to be emitted at all");
        declaration.Should().BeLessThan(use,
            "C# hoists a local function, so the emission has to declare it before the use");
    }
}
