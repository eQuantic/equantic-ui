using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// `new List&lt;T&gt;(x)` means two OPPOSITE things depending on what `x` is, and the difference is
/// invisible in the syntax. Sizing a list ahead — the most ordinary optimisation there is — used to
/// emit the capacity AS the list: `let lines = 12;` and then `lines.push(...)`, which throws in the
/// browser and says nothing at build time. Found in a document model that rebuilt its lines.
/// <para>
/// Written against a real compilation rather than a bare expression: which meaning applies is a
/// question for the resolved constructor, and a harness with no symbols cannot ask it.
/// </para>
/// </summary>
public class CollectionConstructionTests
{
    // TypeScript: the element type an empty collection is annotated with is for the type checker,
    // and plain JavaScript carries no annotation at all.
    private static string Convert(string body) => TestHelper.ConvertCodeBlock(body, typeAnnotations: true);

    [Fact]
    public void ACapacityMakesAnEMPTYCollection()
    {
        var js = Convert("""
            var source = new List<string>();
            var sized = new List<string>(source.Count + 1);
            var fixedSize = new List<string>(16);
            """);

        // A capacity is a hint, not the contents, but C# evaluates it and the constructor refuses a negative
        // one, so one that is not a constant the list takes is handed to the runtime first.
        js.Should().Contain("let sized: string[] = ($eq.collections.listCapacity(source.length + 1), []);",
            "a capacity is a hint, not the contents, and is evaluated and checked as the constructor does");
        js.Should().Contain("let fixedSize: string[] = [];", "a constant the list takes has nothing to evaluate or refuse");
    }

    [Fact]
    public void ASourceCollectionIsCOPIED_NotAliased()
    {
        var js = Convert("""
            var source = new List<string>();
            var copy = new List<string>(source);
            """);

        js.Should().Contain("let copy: string[] = [...source];",
            "C# copies the source — handing back the same array would make every later Add "
            + "mutate what it was built from");
    }

    [Fact]
    public void AndTheOrdinaryShapesAreUnchanged()
    {
        Convert("var empty = new List<string>();").Should().Contain("let empty: string[] = [];",
            "an empty array infers `any[]`, so the element type is emitted with it");
        Convert("""var seeded = new List<string> { "a", "b" };""")
            .Should().Contain("let seeded = ['a', 'b'];",
                "a literal with elements infers its own type");
    }
}
