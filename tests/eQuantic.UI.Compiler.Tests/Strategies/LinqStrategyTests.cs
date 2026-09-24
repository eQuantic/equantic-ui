using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class LinqStrategyTests
{
    [Fact]
    public void Select_MapsTo_Map()
    {
        var result = TestHelper.ConvertExpression("list.Select(x => x.Id)");
        result.Should().Be("this.list.map((x) => x.id)");
    }

    [Fact]
    public void Where_MapsTo_Filter()
    {
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active)");
        result.Should().Be("this.list.filter((x) => x.active)");
    }

    [Fact]
    public void First_NoPredicate_MapsTo_Index0()
    {
        var result = TestHelper.ConvertExpression("list.First()");
        result.Should().Be("this.list[0]");
    }

    [Fact]
    public void First_WithPredicate_MapsTo_Find()
    {
        var result = TestHelper.ConvertExpression("list.First(x => x.Id == 1)");
        result.Should().Be("this.list.find((x) => x.id === 1)");
    }

    [Fact]
    public void Any_NoPredicate_MapsTo_LengthCheck()
    {
        var result = TestHelper.ConvertExpression("list.Any()");
        result.Should().Be("(this.list.length > 0)");
    }

    [Fact]
    public void Any_WithPredicate_MapsTo_Some()
    {
        var result = TestHelper.ConvertExpression("list.Any(x => x.Active)");
        result.Should().Be("this.list.some((x) => x.active)");
    }
    
    [Fact]
    public void All_MapsTo_Every()
    {
        var result = TestHelper.ConvertExpression("list.All(x => x.Active)");
        result.Should().Be("this.list.every((x) => x.active)");
    }

    [Fact]
    public void OrderBy_MapsTo_Sort()
    {
        // Simple case: OrderBy generic
        // We expect .sort((a, b) => ...) transformation
        var result = TestHelper.ConvertExpression("list.OrderBy(x => x.Id)");
        // The implementation should produce a sort function over a COPY of the source
        // (OrderBy is non-mutating in C#).
        result.Should().Contain(".sort(");
        result.Should().StartWith("[...this.list]");
    }
    [Fact]
    public void Chained_Calls_Respect_Order()
    {
        // list.Where(x => x.Active).OrderBy(x => x.Name).Select(x => x.Id)
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active).OrderBy(x => x.Name).Select(x => x.Id)");
        
        // Check structural correctness of the chain (OrderBy copies the filtered source).
        result.Should().StartWith("[...this.list.filter((x) => x.active)]");
        result.Should().Contain(".sort(");
        result.Should().EndWith(".map((x) => x.id)");
    }

    [Fact]
    public void Nested_Lambdas_Recurse_Correctly()
    {
        // list.Select(u => u.Orders.Where(o => o.Total > 100))
        var result = TestHelper.ConvertExpression("list.Select(u => u.Orders.Where(o => o.Total > 100))");
        
        // This validates that the inner .Where() is correctly converted inside the .Select() callback.
        // o.Total is a decimal — a real Decimal in the typed world — so the > comparison routes
        // through compareTo, and the int literal converts at the bound tree's seam (ValueFlow).
        result.Should().Be("this.list.map((u) => u.orders.filter((o) => (o.total.compareTo($eq.num.dec(100)) > 0)))");
    }

    [Fact]
    public void Complex_Predicate_With_Nested_Scope()
    {
        // list.Where(x => x.Active && otherList.Any(y => y.Id == x.Id))
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active && otherList.Any(y => y.Id == x.Id))");

        // This validates that variable 'x' from outer scope is accessible in inner 'Any'
        result.Should().Be("this.list.filter((x) => x.active && this.otherList.some((y) => y.id === x.id))");
    }

    [Fact]
    public void Skip_MapsTo_Slice()
    {
        var result = TestHelper.ConvertExpression("list.Skip(5)");
        result.Should().Be("this.list.slice(5)");
    }

    [Fact]
    public void Take_MapsTo_Slice()
    {
        var result = TestHelper.ConvertExpression("list.Take(10)");
        result.Should().Be("this.list.slice(0, 10)");
    }

    [Fact]
    public void Skip_Take_Chain_ForPagination()
    {
        var result = TestHelper.ConvertExpression("list.Skip(20).Take(10)");
        result.Should().Be("this.list.slice(20).slice(0, 10)");
    }

    [Fact]
    public void Distinct_MapsTo_SetSpread()
    {
        var result = TestHelper.ConvertExpression("list.Distinct()");
        result.Should().Be("[...new Set(this.list)]");
    }

    [Fact]
    public void Contains_MapsTo_Includes()
    {
        var result = TestHelper.ConvertExpression("list.Contains(item)");
        result.Should().Be("this.list.includes(this.item)");
    }

    [Fact]
    public void Contains_WithLiteral_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("list.Contains(5)");
        result.Should().Be("this.list.includes(5)");
    }

    [Fact]
    public void Last_NoPredicate_MapsToLastElement()
    {
        var result = TestHelper.ConvertExpression("list.Last()");
        result.Should().Be("(this.list[this.list.length - 1])");
    }

    [Fact]
    public void LastOrDefault_NoPredicate_MapsWithNullCoalescing()
    {
        var result = TestHelper.ConvertExpression("list.LastOrDefault()");
        result.Should().Be("(this.list[this.list.length - 1] ?? null)");
    }

    [Fact]
    public void Last_WithPredicate_MapsToFilterPop()
    {
        var result = TestHelper.ConvertExpression("list.Last(x => x.Active)");
        result.Should().Be("(this.list.filter((x) => x.active).pop())");
    }

    [Fact]
    public void Single_NoPredicate_MapsToFirstElement()
    {
        var result = TestHelper.ConvertExpression("list.Single()");
        result.Should().Be("(this.list[0])");
    }

    [Fact]
    public void Single_WithPredicate_MapsToFind()
    {
        var result = TestHelper.ConvertExpression("list.Single(x => x.Id == 1)");
        result.Should().Be("(this.list.find((x) => x.id === 1))");
    }

    [Fact]
    public void SelectMany_MapsToFlatMap()
    {
        var result = TestHelper.ConvertExpression("list.SelectMany(x => x.Items)");
        result.Should().Be("this.list.flatMap((x) => x.items)");
    }

    [Fact]
    public void Sum_NoPredicate_MapsToReduce()
    {
        var result = TestHelper.ConvertExpression("numbers.Sum()");
        result.Should().Be("this.numbers.reduce((_a, _b) => _a + _b, 0)");
    }

    [Fact]
    public void Sum_WithSelector_MapsToReduceWithSelector()
    {
        var result = TestHelper.ConvertExpression("list.Sum(x => x.Amount)");
        result.Should().Be("this.list.reduce((_sum, x) => _sum + x.amount, 0)");
    }

    [Fact]
    public void Average_NoPredicate_MapsToReduceDivide()
    {
        var result = TestHelper.ConvertExpression("numbers.Average()");
        result.Should().Be("(this.numbers.reduce((_a, _b) => _a + _b, 0) / this.numbers.length)");
    }

    [Fact]
    public void Min_NoSelector_OrdersByTheTypeItAnswers()
    {
        // An int: ordered by `<`, and an empty list throws, where Math.min() answered Infinity.
        var result = TestHelper.ConvertExpression("numbers.Min()");
        result.Should().Be("$eq.linq.min(this.numbers, undefined, 'value', false)");
    }

    [Fact]
    public void Max_NoSelector_OrdersByTheTypeItAnswers()
    {
        // A comparable type of the app's own: ordered by the compareTo its twin carries, a null passed
        // over, an empty list answering null. (A class that is not comparable is refused, below.)
        var result = TestHelper.ConvertExpression("new List<Grade>().Max()");
        result.Should().Be("$eq.linq.max([], undefined, 'comparable', true)");
    }

    [Fact]
    public void Min_WithSelector_OrdersByTheTypeItSelects()
    {
        var result = TestHelper.ConvertExpression("list.Min(x => x.Value)");
        result.Should().Be("$eq.linq.min(this.list, (x) => x.value, 'value', false)");
    }

    [Fact]
    public void MaxMin_WithNoFaithfulOrder_AreRefused()
    {
        // An enum's values cross as member NAMES, which order alphabetically where .NET orders by
        // value, and a comparer has no form to call: each is refused where it was ordered wrongly.
        TestHelper.DiagnosticsFor("var r = new[] { Size.Small, Size.Large }.Max()")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("LINQ Max/Min over Size"));
        TestHelper.DiagnosticsFor("var r = numbers.Min(Comparer<int>.Default)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("LINQ Max/Min with a comparer"));
        TestHelper.DiagnosticsFor("var r = numbers.Max()")
            .Should().NotContain(d => d.Code == "EQ1004", "an int has a faithful order");

        // Any other value orders by a compareTo it carries, and only the decimal, the dates and the
        // app's own comparable types carry one. .NET's default comparer throws for a type that is not
        // comparable, where calling one here was a TypeError, and a type parameter may be a number.
        foreach (var (call, type) in new[]
        {
            ("var r = Orders.Max()", "Order"),
            ("var r = new object[] { 1, 2 }.Min()", "object"),
            ("var r = new[] { (1, 2) }.Max()", "(int, int)"),
            ("var r = new[] { new Rank() }.Max()", "Rank"),
            // The first statement is the one converted, so the generic function comes alone.
            ("static T Top<T>(IEnumerable<T> xs) where T : IComparable<T> => xs.Max()", "T"),
        })
        {
            TestHelper.DiagnosticsFor(call)
                .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains($"LINQ Max/Min over {type}"), call);
        }
        foreach (var call in new[]
        {
            "var r = new[] { new Grade(2), new Grade(1) }.Max()",
            "var r = new[] { 1.5m, 2.5m }.Min()",
            "var r = new[] { DateTime.Now }.Max()",
            "var r = new DateTimeOffset?[] { null }.Min()",
            "var r = new[] { TimeSpan.Zero }.Max()",
            "var r = new[] { DateOnly.MinValue }.Min()",
            "var r = new[] { TimeOnly.MinValue }.Max()",
        })
        {
            TestHelper.DiagnosticsFor(call)
                .Should().NotContain(d => d.Code == "EQ1004", $"`{call}` orders by a compareTo it carries");
        }
    }

    [Fact]
    public void ToDictionary_WithAComparer_IsRefused()
    {
        // The comparer was called as if it were the element selector.
        TestHelper.DiagnosticsFor("var r = items.ToDictionary(x => x, (IEqualityComparer<string>)null)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("ToDictionary with a comparer"));
        TestHelper.DiagnosticsFor("var r = items.ToDictionary(x => x, x => x.Length, (IEqualityComparer<string>)null)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("ToDictionary with a comparer"),
                "the comparer beside an element selector is refused in the same words");
        TestHelper.DiagnosticsFor("var r = items.ToDictionary(x => x, x => x.Length)")
            .Should().NotContain(d => d.Code == "EQ1004", "an element selector is not a comparer");
    }

    [Fact]
    public void ToDictionary_KeyedByWhatAPlainObjectCannotHold_IsRefused()
    {
        // A DateTime's text drops its ticks, a class instance is "[object Object]", and an enum with
        // aliases has two names for one key: none keeps .NET's equality as a plain object's text.
        TestHelper.DiagnosticsFor("var r = Orders.ToDictionary(o => new DateTime(2026, 1, o.Id))")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("ToDictionary keyed by System.DateTime"));
        TestHelper.DiagnosticsFor("var r = Orders.ToDictionary(o => o)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("ToDictionary keyed by Order"));
        TestHelper.DiagnosticsFor("var r = Orders.ToDictionary(o => Environment.SpecialFolder.Personal)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("ToDictionary keyed by System.Environment.SpecialFolder"));

        foreach (var held in new[] { "o => o.Id", "o => new DateOnly(2026, 1, o.Id)", "o => Size.Small", "o => o.Id.ToString()" })
        {
            TestHelper.DiagnosticsFor($"var r = Orders.ToDictionary({held})")
                .Should().NotContain(d => d.Code == "EQ1004", $"a plain object holds the key of `{held}` by its text");
        }
    }

    [Fact]
    public void Reverse_MapsToSpreadReverse()
    {
        var result = TestHelper.ConvertExpression("list.Reverse()");
        result.Should().Be("[...this.list].reverse()");
    }
}
