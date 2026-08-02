using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// JS has no named arguments, so a C# caller who SKIPS an optional by naming a later parameter —
/// <c>new DataColumn("Customer", track, Sortable: true)</c> — only survives the crossing if the
/// emitter puts each value at its parameter's real position and fills the skipped ones from their
/// C# defaults. Emitting written order instead binds the value to the WRONG member, and the failure
/// is silent: that `true` landed in <c>Align</c>, the column stopped being sortable on the client
/// only, SSR rendered a header button the hydrated page did not, and the tag mismatch took the whole
/// page into a full re-render. All three ways of writing the creation go the same way.
/// </summary>
public class NamedArgumentOrderTests
{
    [Fact]
    public void AnExplicitCreation_PutsTheNamedValueAtItsOwnParameter()
    {
        TestHelper.ConvertExpression("new Col(\"A\", 1, Sortable: true)")
            .Should().Be("new Col('A', 1, 'start', true)");
    }

    [Fact]
    public void ATargetTypedCreation_GoesTheSameWay()
    {
        TestHelper.ConvertStatement("Col b = new(\"B\", 2, Sortable: true);")
            .Should().Be("let b = new Col('B', 2, 'start', true);");
    }

    [Fact]
    public void ACreationInsideACollectionExpression_GoesTheSameWay()
    {
        TestHelper.ConvertStatement("Col[] cols = [ new(\"C\", 3, Sortable: true) ];")
            .Should().Be("let cols = [new Col('C', 3, 'start', true)];");
    }

    [Fact]
    public void EveryArgumentNamed_AndOutOfOrder_StillLandsRight()
    {
        TestHelper.ConvertExpression("new Col(Sortable: true, Header: \"D\", Track: 4)")
            .Should().Be("new Col('D', 4, 'start', true)");
    }

    [Fact]
    public void NothingNamed_PassesStraightThrough()
    {
        TestHelper.ConvertExpression("new Col(\"E\", 5)").Should().Be("new Col('E', 5)");
    }
}
