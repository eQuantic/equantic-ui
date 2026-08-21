using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// LINQ query syntax is the SAME translation as method syntax, by construction: the query is
/// lowered to the chain the C# compiler would build and handed to the operator strategies. So the
/// assertion that matters is equality of emissions — query form against its method-syntax twin —
/// under the authoritative harness, where every operator must claim symbol-first.
/// </summary>
public class QuerySyntaxTests
{
    // `numbers` is a LOCAL declared by the block itself — ConvertCodeBlock's template only knows
    // `list1` — and declared identically on both sides, so the emissions stay comparable.
    private const string Numbers = "var numbers = new List<int> { 3, 1, 2 }; ";

    [Theory]
    [InlineData(
        Numbers + "var q = from n in numbers where n > 1 select n * 2;",
        Numbers + "var q = numbers.Where(n => n > 1).Select(n => n * 2);")]
    // The degenerate final `select n` after other clauses is elided, as C# elides it.
    [InlineData(
        Numbers + "var q = from n in numbers where n > 1 select n;",
        Numbers + "var q = numbers.Where(n => n > 1);")]
    // …but a query that would otherwise BE its source still projects: C# guarantees a fresh sequence.
    [InlineData(
        Numbers + "var q = from n in numbers select n;",
        Numbers + "var q = numbers.Select(n => n);")]
    // A whole orderby run — each ordering with its own direction — is one OrderBy/ThenBy chain,
    // which the ordering strategy collapses into a single composite stable sort.
    [InlineData(
        "var q = from s in list1 orderby s.Length descending, s select s;",
        "var q = list1.OrderByDescending(s => s.Length).ThenBy(s => s);")]
    [InlineData(
        "var q = from s in list1 where s.Length > 2 orderby s select s.ToUpper();",
        "var q = list1.Where(s => s.Length > 2).OrderBy(s => s).Select(s => s.ToUpper());")]
    [InlineData(
        "var q = from s in list1 group s by s.Length;",
        "var q = list1.GroupBy(s => s.Length);")]
    [InlineData(
        "var q = from s in list1 group s.ToUpper() by s.Length;",
        "var q = list1.GroupBy(s => s.Length, s => s.ToUpper());")]
    // Members of the range variable bind through the mapped copies — `s.Length` is a real property
    // read on a real type (it emits `.length`, the string twin), not a name guess.
    [InlineData(
        "var q = from s in list1 where s.Length > 1 select s.Length;",
        "var q = list1.Where(s => s.Length > 1).Select(s => s.Length);")]
    public void QuerySyntax_EmitsExactlyWhatMethodSyntaxEmits(string query, string methodSyntax)
    {
        var fromQuery = TestHelper.ConvertCodeBlock(query);
        var fromMethods = TestHelper.ConvertCodeBlock(methodSyntax);

        fromQuery.Should().Be(fromMethods);
        fromQuery.Should().NotContainAny(new[] { "Where", "Select", "OrderBy", "GroupBy" },
            because: "the operators must have been claimed by their strategies, not camelCased by the fallback");
    }

    [Fact]
    public void QuerySyntax_NestedInItsOwnSource_LowersBothLevels()
    {
        var js = TestHelper.ConvertCodeBlock(
            "var q = from s in (from t in list1 where t.Length > 0 select t.ToUpper()) where s.Length > 2 select s;");

        js.Should().Contain("this.list1.filter(");
        js.Should().Contain(".map(");
        js.Split(".filter(").Length.Should().Be(3, "both the inner and the outer where lower to filter");
        js.Should().NotContainAny(new[] { "Where", "Select" });
    }

    private const string ProbeTemplate = """
        using System.Collections.Generic;
        using System.Linq;
        using eQuantic.UI.Primitives;

        public sealed class Probe : StatelessComponent
        {
            private readonly List<int> _values = new();

            public override VisualNode Build(ComponentContext context)
            {
                var q = QUERY;
                return new Box();
            }
        }
        """;

    [Theory]
    [InlineData("from a in _values join b in _values on a equals b select a + b", "join")]
    [InlineData("from a in _values let d = a * 2 select d", "let")]
    [InlineData("from a in _values from b in _values select a + b", "second 'from'")]
    [InlineData("from a in _values group a by a % 2 into g select g.Key", "into")]
    [InlineData("from object a in _values select a", "typed range variable")]
    public void QuerySyntax_TransparentIdentifierShapes_AreFencedNotGuessed(string query, string reason)
    {
        var source = ProbeTemplate.Replace("QUERY", query);
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable)));

        var result = compiler.CompileSource(source, "Probe.cs").Single();

        Assert.False(result.Success, $"a query with {reason} must not ship a guessed translation");
        Assert.Contains(result.Errors, e => e.Code == "EQ2008" && e.Message.Contains(reason));
    }
}
