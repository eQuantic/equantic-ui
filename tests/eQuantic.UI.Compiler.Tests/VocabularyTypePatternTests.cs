using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type pattern over the VOCABULARY emits a real <c>instanceof</c>.
/// <para>
/// It used to emit one only for components, and every other vocabulary class fell back to
/// <c>!= null</c> — so <c>leading switch { Icon i => …, Avatar a => … }</c> matched its FIRST arm
/// for any non-null node and left the rest dead. No diagnostic, no failing test: the wrong branch
/// simply ran. It surfaced because the dead arm read a property the base type lacks and tsc
/// complained, which is luck rather than a net. This is the net.
/// </para>
/// </summary>
public class VocabularyTypePatternTests
{
    private readonly CSharpToJsConverter _converter = new();

    /// <summary>The shapes that matter: a vocabulary node, a component (both real classes on the
    /// other side), a value type and an enum (neither is).</summary>
    private const string Vocabulary = """
        namespace eQuantic.UI.Primitives {
            public abstract class VisualNode { }
            public abstract class UiComponent : VisualNode { }
            public sealed class Icon : VisualNode { public float Size { get; set; } }
            public sealed class Avatar : UiComponent { }
            public struct EdgeInsets { public float Start; }
            public enum Variant { Primary, Outline }
        """;

    /// <summary>
    /// The probe lives INSIDE the vocabulary's namespace on purpose. A <c>using</c> after a namespace
    /// block is not legal C#, and the parse error that caused resolved every type to nothing — so the
    /// probe reported the exact fallback it exists to catch. A test that passes for the wrong reason
    /// is the only thing worse than no test.
    /// </summary>
    private string Convert(string bodyCode)
    {
        var code = Vocabulary
            + $"\nclass Wrapper {{ object Method(VisualNode node, object any) {{ {bodyCode} }} }}\n}}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("probe", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        _converter.SetSemanticModel(compilation.GetSemanticModel(tree));

        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return _converter.Convert(method.Body!);
    }

    [Fact]
    public void EveryArmOfAVocabularySwitch_TestsItsOwnType()
    {
        var js = Convert("""
            return node switch { Icon icon => icon.Size, Avatar avatar => 40f, _ => 0f };
            """);

        Assert.Contains("instanceof Icon", js);
        Assert.Contains("instanceof Avatar", js);
        // The bug, spelled out: a null check in an arm makes it match everything below it.
        Assert.DoesNotContain("!= null", js);
    }

    /// <summary>The type has to reach the module's IMPORTS, or the emitted `instanceof Icon`
    /// references a free variable and the module throws on first evaluation.</summary>
    [Fact]
    public void TheTestedTypeIsRecordedAsUsed()
    {
        Convert("return node switch { Icon icon => icon.Size, _ => 0f };");

        Assert.Contains("Icon", _converter.UsedRuntimeTypes);
    }

    [Fact]
    public void AnIsExpression_TestsTheTypeToo()
    {
        var js = Convert("return node is Icon;");

        Assert.Contains("instanceof Icon", js);
    }

    /// <summary>What is NOT a class on the other side keeps the null check, deliberately: a value
    /// type lowers to a plain config object and an enum to a string literal, and `instanceof` on
    /// either is a TypeError rather than a wrong answer.</summary>
    [Fact]
    public void AValueTypeAndAnEnumKeepTheNullCheck()
    {
        var js = Convert("return any switch { EdgeInsets e => 1f, Variant v => 2f, _ => 0f };");

        Assert.DoesNotContain("instanceof EdgeInsets", js);
        Assert.DoesNotContain("instanceof Variant", js);
    }
}
