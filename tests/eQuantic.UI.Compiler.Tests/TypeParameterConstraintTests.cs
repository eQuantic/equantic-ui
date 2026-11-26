using System.Text.RegularExpressions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type parameter's constraint and a cast to a type parameter, as TypeScript can say them and plain JavaScript never
/// does (#1854, #1860).
/// <para>
/// A constraint to a class with a twin is written <c>T extends</c> that class and imported, in TypeScript only: plain
/// JavaScript writes no type parameter, so it imports no class only a constraint names. A nullable class constraint is
/// named by its class, since <c>ShapeBase | null</c> is no name to import. A cast to a type parameter is asserted where
/// TypeScript has it in scope, a class's in its own instance members, and never in a type nested in it, which is a
/// module of its own declaring none of its owner's parameters.
/// </para>
/// </summary>
public class TypeParameterConstraintTests
{
    private const string Source = """
        namespace Constrained;

        public abstract class ShapeBase { public abstract int Area { get; } }

        public sealed class Holder<T> where T : ShapeBase
        {
            public int Count;
        }

        public sealed class Maybe<T> where T : ShapeBase?
        {
            public int Count;
        }

        public sealed class Owner<T> where T : ShapeBase
        {
            public ShapeBase Keep(object value) => (T)value;

            public sealed class Held
            {
                public ShapeBase Unwrap(object value) => (T)value;
            }
        }
        """;

    private static string Module(string marker, bool typeAnnotations) =>
        new ComponentCompiler { TypeAnnotations = typeAnnotations }.CompileSource(Source, "Constrained.cs")
            .First(result => result.TypeScript.Contains(marker, StringComparison.Ordinal)).TypeScript;

    private static bool Imports(string module, string name) =>
        Regex.IsMatch(module, $@"^import[^\n]*\b{name}\b", RegexOptions.Multiline);

    [Fact]
    public void AClassConstraint_IsDeclaredAndImported_InTypeScript()
    {
        var holder = Module("class Holder", typeAnnotations: true);

        Assert.Contains("class Holder<T extends ShapeBase>", holder, StringComparison.Ordinal);
        Assert.True(Imports(holder, "ShapeBase"), holder);
    }

    [Fact]
    public void PlainJavaScript_ImportsNoClassOnlyAConstraintNames()
    {
        var holder = Module("class Holder", typeAnnotations: false);

        Assert.DoesNotContain("ShapeBase", holder, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullableClassConstraint_IsNamedByItsClass()
    {
        var maybe = Module("class Maybe", typeAnnotations: true);

        Assert.Contains("class Maybe<T extends ShapeBase>", maybe, StringComparison.Ordinal);
        Assert.True(Imports(maybe, "ShapeBase"), maybe);
        Assert.DoesNotMatch(new Regex(@"^import[^\n]*\|", RegexOptions.Multiline), maybe);
    }

    [Fact]
    public void ACastToTheClasssParameter_IsAssertedInItsOwnInstanceMember_AndNotInANestedType()
    {
        Assert.Contains("(value as T)", Module("keep(", typeAnnotations: true), StringComparison.Ordinal);
        Assert.DoesNotContain("as T)", Module("unwrap(", typeAnnotations: true), StringComparison.Ordinal);
    }
}
