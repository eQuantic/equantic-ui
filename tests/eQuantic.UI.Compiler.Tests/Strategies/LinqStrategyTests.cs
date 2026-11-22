using FluentAssertions;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class LinqStrategyTests
{
    /// <summary>A projection is the runtime's lazy query, read one element at a time when walked (#685).</summary>
    [Fact]
    public void Select_IsALazyQuery()
    {
        var result = TestHelper.ConvertExpression("list.Select(x => x.Id)");
        result.Should().Be("$eq.query.select(this.list, (x) => x.id)");
    }

    /// <summary>A filter is the runtime's lazy query, read one element at a time when walked (#685).</summary>
    [Fact]
    public void Where_IsALazyQuery()
    {
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active)");
        result.Should().Be("$eq.query.where(this.list, (x) => x.active)");
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

    /// <summary>Any with no predicate is the runtime's walk, which stops at the first element (#685).</summary>
    [Fact]
    public void Any_NoPredicate_StopsAtTheFirstElement()
    {
        var result = TestHelper.ConvertExpression("list.Any()");
        result.Should().Be("$eq.query.any(this.list)");
    }

    /// <summary>Any with a predicate is the runtime's walk, which stops at the first match (#685).</summary>
    [Fact]
    public void Any_WithPredicate_StopsAtTheFirstMatch()
    {
        var result = TestHelper.ConvertExpression("list.Any(x => x.Active)");
        result.Should().Be("$eq.query.any(this.list, (x) => x.active)");
    }

    /// <summary>All is the runtime's walk, which stops at the first element that fails (#685).</summary>
    [Fact]
    public void All_StopsAtTheFirstFailure()
    {
        var result = TestHelper.ConvertExpression("list.All(x => x.Active)");
        result.Should().Be("$eq.query.all(this.list, (x) => x.active)");
    }

    /// <summary>OrderBy sorts a COPY of its source (it is non-mutating in C#), when its query is walked (#685).</summary>
    [Fact]
    public void OrderBy_SortsACopyWhenWalked()
    {
        var result = TestHelper.ConvertExpression("list.OrderBy(x => x.Id)");
        result.Should().StartWith("$eq.query.defer(($src) => [...$src].sort(");
        result.Should().Contain("const $k = ((x) => x.id);");
        result.Should().EndWith(", this.list)");
    }
    [Fact]
    public void Chained_Calls_Respect_Order()
    {
        // list.Where(x => x.Active).OrderBy(x => x.Name).Select(x => x.Id)
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active).OrderBy(x => x.Name).Select(x => x.Id)");

        // Check structural correctness of the chain: each operator wraps the one before it, and
        // OrderBy sorts a copy of the filtered query when it is walked (#685).
        result.Should().StartWith("$eq.query.select($eq.query.defer(($src) => [...$src].sort(");
        result.Should().Contain("const $k = ((x) => x.name);");
        result.Should().EndWith("}), $eq.query.where(this.list, (x) => x.active)), (x) => x.id)");
    }

    [Fact]
    public void Nested_Lambdas_Recurse_Correctly()
    {
        // list.Select(u => u.Orders.Where(o => o.Total > 100))
        var result = TestHelper.ConvertExpression("list.Select(u => u.Orders.Where(o => o.Total > 100))");
        
        // This validates that the inner .Where() is correctly converted inside the .Select() callback.
        // o.Total is a decimal — a real Decimal in the typed world — so the > comparison routes
        // through compareTo, and the int literal converts at the bound tree's seam (ValueFlow).
        result.Should().Be("$eq.query.select(this.list, (u) => $eq.query.where(u.orders, (o) => (o.total.compareTo($eq.num.dec(100)) > 0)))");
    }

    [Fact]
    public void Complex_Predicate_With_Nested_Scope()
    {
        // list.Where(x => x.Active && otherList.Any(y => y.Id == x.Id))
        var result = TestHelper.ConvertExpression("list.Where(x => x.Active && otherList.Any(y => y.Id == x.Id))");

        // This validates that variable 'x' from outer scope is accessible in inner 'Any'
        result.Should().Be("$eq.query.where(this.list, (x) => x.active && $eq.query.any(this.otherList, (y) => y.id === x.id))");
    }

    /// <summary>Skip is the runtime's lazy query, which reads past the skipped elements when walked (#685).</summary>
    [Fact]
    public void Skip_WalksLazily()
    {
        var result = TestHelper.ConvertExpression("list.Skip(5)");
        result.Should().Be("$eq.query.skip(this.list, 5)");
    }

    /// <summary>Take is the runtime's lazy query, whose walk stops once it has the count (#685).</summary>
    [Fact]
    public void Take_WalksLazily()
    {
        var result = TestHelper.ConvertExpression("list.Take(10)");
        result.Should().Be("$eq.query.take(this.list, 10)");
    }

    [Fact]
    public void Skip_Take_Chain_ForPagination()
    {
        var result = TestHelper.ConvertExpression("list.Skip(20).Take(10)");
        result.Should().Be("$eq.query.take($eq.query.skip(this.list, 20), 10)");
    }

    /// <summary>Distinct spreads the runtime's set of the source when its query is walked (#685).</summary>
    [Fact]
    public void Distinct_OfAClass_DefersTheRuntimesSetSpread()
    {
        var result = TestHelper.ConvertExpression("list.Distinct()");
        result.Should().Be("$eq.query.defer(($src) => [...$eq.collections.hashSet('item', $src)], this.list)");
    }

    [Fact]
    public void Contains_OfAnIdentityElement_MapsTo_Includes()
    {
        var result = TestHelper.ConvertExpression("items.Contains(str)");
        result.Should().Be("this.items.includes(this.str)");
    }

    [Fact]
    public void Contains_WithLiteral_MapsCorrectly()
    {
        var result = TestHelper.ConvertExpression("numbers.Contains(5)");
        result.Should().Be("this.numbers.includes(5)");
    }

    [Fact]
    public void Contains_OfAClass_AsksTheElementsOwnEquality()
    {
        // A class may override Equals, as its subclasses may: the element's own equality is asked,
        // as EqualityComparer<T>.Default asks it (#425).
        var result = TestHelper.ConvertExpression("list.Contains(item)");
        result.Should().Be("$eq.collections.contains(this.list, this.item, 'item')");
    }

    [Fact]
    public void Last_NoPredicate_MapsToLastElement()
    {
        // The source read ONCE, as C# reads it: a member is bound, where it was written twice.
        var result = TestHelper.ConvertExpression("list.Last()");
        result.Should().Be("(($0) => ($0[$0.length - 1]))(this.list)");
    }

    [Fact]
    public void LastOrDefault_NoPredicate_MapsWithNullCoalescing()
    {
        var result = TestHelper.ConvertExpression("list.LastOrDefault()");
        result.Should().Be("(($0) => ($0[$0.length - 1] ?? null))(this.list)");
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

    /// <summary>SelectMany is the runtime's lazy query, flattening one element at a time when walked (#685).</summary>
    [Fact]
    public void SelectMany_IsALazyQuery()
    {
        var result = TestHelper.ConvertExpression("list.SelectMany(x => x.Items)");
        result.Should().Be("$eq.query.selectMany(this.list, (x) => x.items)");
    }

    [Fact]
    public void Sum_NoPredicate_MapsToReduce()
    {
        var result = TestHelper.ConvertExpression("numbers.Sum()");
        result.Should().Be("this.numbers.reduce(($a, $b) => $a + $b, 0)");
    }

    [Fact]
    public void Sum_WithSelector_MapsToReduceWithSelector()
    {
        var result = TestHelper.ConvertExpression("list.Sum(x => x.Amount)");
        result.Should().Be("this.list.reduce(($sum, x) => $sum + x.amount, 0)");
    }

    /// <summary>A selector with a block has no expression body to write into the callback: it was read as
    /// null and the compile crashed. It is called as the lambda it is.</summary>
    [Fact]
    public void Sum_AndAverage_WithABlockSelector_CallTheLambda()
    {
        TestHelper.ConvertExpression("numbers.Sum(x => { return x * 2; })")
            .Should().Contain("$sum + ((x) => {").And.Contain("return Math.imul(x, 2);");
        TestHelper.ConvertExpression("numbers.Average(x => { return x * 2; })")
            .Should().Contain("$eq.linq.average(this.numbers, (x) => {").And.Contain("return Math.imul(x, 2);");
    }

    /// <summary>The mean is the runtime's, told how the values add and whether they are nullable,
    /// which only the bound overload says (#719).</summary>
    [Fact]
    public void Average_NoPredicate_IsTheRuntimesMean()
    {
        var result = TestHelper.ConvertExpression("numbers.Average()");
        result.Should().Be("$eq.linq.average(this.numbers, undefined, 'int', false)");
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

    /// <summary>
    /// With no model to say what the call answers, <c>Max</c> and <c>Min</c> keep the numeric answer of
    /// <c>Math.max</c> and <c>Math.min</c>, folded over the values: spread into the call, the values were
    /// its arguments, past the cap an engine sets on them (#1060). The values are the runtime's sequence of
    /// the source, which no model says is an array: a set's spread worked, and a set has no <c>reduce</c>
    /// (found by Copilot's review of #1161).
    /// </summary>
    [Theory]
    [InlineData("values.Max()", "$eq.linq.seq(values).reduce(($m, $v) => Math.max($m, $v), -Infinity)")]
    [InlineData("values.Min(x => x.Size)", "$eq.linq.seq(values).map((x) => x.size).reduce(($m, $v) => Math.min($m, $v), Infinity)")]
    public void MaxMin_WithNoModel_FoldTheValues(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    [Fact]
    public void MaxMin_OverAnEnum_OrdersByItsMembersValues()
    {
        // An enum's values cross as member NAMES, which order alphabetically where .NET orders by
        // value, so the call carries its members' values (it was refused until it did).
        TestHelper.ConvertExpression("new[] { Size.Small, Size.Large }.Max()")
            .Should().Be("$eq.linq.max(['small', 'large'], undefined, { 'small': 0, 'medium': 1, 'large': 2 }, false)");
    }

    [Fact]
    public void MaxMin_WithNoFaithfulOrder_AreRefused()
    {
        // A comparer has no form to call: it is refused where it was ordered wrongly.
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
    public void AnOperatorInItsStaticForm_IsRefused_ButMaxAndMin()
    {
        // The strategies read the source from the left of the member access, which in the static form
        // is the TYPE: `Enumerable.Count(xs)` came out as `Enumerable.filter(xs).length` and
        // `Enumerable.ToDictionary(xs, f)` keyed its dictionary by the source, with no diagnostic.
        foreach (var (call, name) in new[]
        {
            ("Enumerable.ToDictionary(numbers, x => x)", "ToDictionary"),
            ("Enumerable.ToDictionary(keySelector: x => x, source: numbers)", "ToDictionary"),
            ("Enumerable.Count(numbers)", "Count"),
            ("Enumerable.Select(numbers, x => x * 2)", "Select"),
            ("Enumerable.Aggregate(numbers, (a, b) => a + b)", "Aggregate"),
            ("Enumerable.OrderBy(numbers, x => x)", "OrderBy"),
            ("Enumerable.GroupBy(numbers, x => x % 2)", "GroupBy"),
            ("Enumerable.ToList(numbers)", "ToList"),
            ("Enumerable.FirstOrDefault(numbers)", "FirstOrDefault"),
            ("Enumerable.Sum(numbers)", "Sum"),
            ("Enumerable.Distinct(numbers)", "Distinct"),
        })
        {
            TestHelper.DiagnosticsFor($"var r = {call}")
                .Should().Contain(d => d.Code == "EQ1004"
                    && d.Message.Contains($"LINQ {name} in its static form (write source.{name}("), call);
        }
        // Max and Min bind their arguments by parameter, and the reduced form is the one that translates.
        foreach (var call in new[]
        {
            "Enumerable.Max(numbers)", "Enumerable.Min(numbers, x => -x)", "numbers.Count()",
            "numbers.ToDictionary(x => x)", "Enumerable.Range(0, 3).ToList()",
        })
        {
            TestHelper.DiagnosticsFor($"var r = {call}")
                .Should().NotContain(d => d.Code == "EQ1004", $"`{call}` translates");
        }
    }

    [Fact]
    public void ToDictionary_WithAComparerThatAsksForTheDefault_IsTheShapeWithoutOne()
    {
        // The comparer was once called as if it were the element selector it shares an argument count
        // with (#390), and then every comparer was refused, the default's included. One the collection
        // fence passes is dropped now, so the call is the one written without it (#578), and one that
        // changes equality is the fence's to refuse (CollectionComparerFenceTests).
        foreach (var (withComparer, without) in new[]
        {
            ("items.ToDictionary(x => x, (IEqualityComparer<string>)null)", "items.ToDictionary(x => x)"),
            ("items.ToDictionary(x => x, x => x.Length, (IEqualityComparer<string>)null)", "items.ToDictionary(x => x, x => x.Length)"),
        })
        {
            TestHelper.DiagnosticsFor($"var r = {withComparer}")
                .Should().NotContain(d => d.Severity == ConversionSeverity.Error, $"`{withComparer}` builds");
            TestHelper.ConvertExpression(withComparer).Should().Be(TestHelper.ConvertExpression(without),
                "a comparer that asks for the default is dropped, never called as a selector");
        }
        TestHelper.DiagnosticsFor("var r = items.ToDictionary(x => x, x => x.Length)")
            .Should().NotContain(d => d.Code == "EQ1004", "an element selector is not a comparer");
    }

    [Fact]
    public void ToDictionary_BuildsTheDictionaryClass_AndAcceptsAnEnumWithAliases()
    {
        // A date and a class were refused while the result was a plain object, which held a key by
        // its text; the runtime's dictionary finds a date by value and a class instance by reference.
        foreach (var key in new[] { "o => new DateTime(2026, 1, o.Id)", "o => o", "o => o.Id",
                     "o => new DateOnly(2026, 1, o.Id)", "o => Size.Small", "o => o.Id.ToString()",
                     "o => Environment.SpecialFolder.Personal" })
        {
            TestHelper.DiagnosticsFor($"var r = Orders.ToDictionary({key})")
                .Should().NotContain(d => d.Code == "EQ1004", $"the dictionary class holds the key of `{key}`");
        }
        TestHelper.ConvertStatement("var r = Orders.ToDictionary(o => new DateTime(2026, 1, o.Id));")
            .Should().Contain("$eq.linq.toDictionary(").And.Contain(", null, true)");
        TestHelper.ConvertStatement("var r = Orders.ToDictionary(o => o.Id, o => o);")
            .Should().Contain("$eq.linq.toDictionary(").And.NotContain("true)");
    }

    /// <summary>A list's own Reverse reverses it in place, and answers nothing (#1076): it was LINQ's reversed
    /// copy, thrown away, so the list stayed as it was.</summary>
    [Fact]
    public void AListsReverse_ReversesItInPlace()
    {
        TestHelper.ConvertExpression("list.Reverse()").Should().Be("$eq.collections.listReverse(this.list)");
        TestHelper.ConvertExpression("list.Reverse(1, 2)").Should().Be("$eq.collections.listReverse(this.list, 1, 2)");
    }
}
