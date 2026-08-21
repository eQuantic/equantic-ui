using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Query syntax in the HEURISTIC world (no semantic model): no clause binds, so the lowering
/// derives every operator from the clause SHAPE — and the operator strategies then claim by name,
/// as the two-regime rule allows them to here. The authoritative twin of these lives in
/// <c>CodeGen.QuerySyntaxTests</c>.
/// </summary>
public class QueryExpressionStrategyTests : StrategyTestBase
{
    [Fact]
    public void WhereSelect_LowersByShape()
    {
        var js = Convert("from n in numbers where n > 1 select n * 2");

        js.Should().Contain(".filter(");
        js.Should().Contain(".map(");
        js.Should().NotContainAny(new[] { "Where", "Select" });
    }

    [Fact]
    public void IdentitySelect_AfterAClause_IsElided()
    {
        var js = Convert("from n in numbers where n > 1 select n");

        js.Should().Contain(".filter(");
        js.Should().NotContain(".map(");
    }

    [Fact]
    public void DegenerateQuery_StillProjectsAFreshSequence()
    {
        Convert("from n in numbers select n").Should().Contain(".map(");
    }

    [Fact]
    public void OrderBy_Directions_ComeFromTheKeywords()
    {
        var js = Convert("from s in words orderby s.Length descending, s select s");

        js.Should().Contain("].sort(");
        js.Split("].sort(").Length.Should().Be(2, "one composite sort for the whole orderby run");
        js.Should().NotContainAny(new[] { "OrderBy", "ThenBy" });
    }
}
