using System.Text.RegularExpressions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An interface whose twin can be written gets one, a module holding <c>export interface</c>, and a type parameter
/// constrained to it is declared <c>T extends</c> it, so a member read on a <c>T</c> type-checks (#1878). Every other
/// interface gets none, and its constraint leaves the parameter unconstrained, as every constraint was before #1854.
/// </summary>
public class InterfaceTwinTests
{
    private const string Source = """
        namespace Faced;

        public interface IShaped
        {
            int Area { get; }
            string Label { get; set; }
            string Describe(string unit);
        }

        public sealed class Measured<T> where T : IShaped
        {
            private readonly T _shape;
            public Measured(T shape) { _shape = shape; }
            public int Area() => _shape.Area;
        }

        public interface IListed<TItem> { TItem First { get; } }
        public interface ICounted { static int Count => 0; int Area { get; } }
        public interface IDoubled { int Area { get; } int Twice() => Area * 2; }
        public interface IDerived : IShaped { }

        public sealed class OverListed<T> where T : IListed<int> { public int Count; }
        public sealed class OverCounted<T> where T : ICounted { public int Count; }
        public sealed class OverDoubled<T> where T : IDoubled { public int Count; }
        public sealed class OverDerived<T> where T : IDerived { public int Count; }
        public sealed class OverComparable<T> where T : System.IComparable<T> { public int Count; }
        """;

    private static IReadOnlyList<string> Modules(bool typeAnnotations) =>
        new ComponentCompiler { TypeAnnotations = typeAnnotations }.CompileSource(Source, "Faced.cs")
            .Select(result => result.TypeScript).ToList();

    private static string Module(string marker, bool typeAnnotations) =>
        Modules(typeAnnotations).First(module => module.Contains(marker, StringComparison.Ordinal));

    [Fact]
    public void AnInterface_HasATwin_WithEachMembersTwinNameAndType()
    {
        var face = Module("export interface IShaped", typeAnnotations: true);

        Assert.Contains("readonly area: number;", face, StringComparison.Ordinal);
        Assert.Contains("label: string;", face, StringComparison.Ordinal);
        Assert.DoesNotContain("readonly label", face, StringComparison.Ordinal);
        Assert.Contains("describe(unit: string): string;", face, StringComparison.Ordinal);
        Assert.Contains("export const IShaped = undefined;", face, StringComparison.Ordinal);
    }

    [Fact]
    public void AConstraintToAnInterfaceWithATwin_IsDeclaredAndImported()
    {
        var measured = Module("class Measured", typeAnnotations: true);

        Assert.Contains("class Measured<T extends IShaped>", measured, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"^import \{ IShaped \} from ""\./IShaped"";", RegexOptions.Multiline), measured);
    }

    [Fact]
    public void PlainJavaScript_WritesTheTwinsValueAlone_AndNoConstraint()
    {
        Assert.Equal("export const IShaped = undefined;\n", Module("export const IShaped", typeAnnotations: false));
        Assert.DoesNotContain("IShaped", Module("class Measured", typeAnnotations: false), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OverListed", "IListed")]          // a generic interface
    [InlineData("OverCounted", "ICounted")]        // a static member
    [InlineData("OverDoubled", "IDoubled")]        // a default member
    [InlineData("OverDerived", "IDerived")]        // a base interface
    [InlineData("OverComparable", "IComparable")]  // an interface of the BCL
    public void AnInterfaceWhoseTwinCannotBeWritten_HasNone_AndLeavesTheParameterUnconstrained(string owner, string face)
    {
        var modules = Modules(typeAnnotations: true);

        Assert.DoesNotContain(modules, module => module.Contains($"export interface {face}", StringComparison.Ordinal));
        Assert.Contains($"class {owner}<T>", modules.First(module => module.Contains($"class {owner}", StringComparison.Ordinal)),
            StringComparison.Ordinal);
    }
}
