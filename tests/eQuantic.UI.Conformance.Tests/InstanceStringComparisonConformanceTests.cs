using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A string's own methods that take a <c>StringComparison</c>, and <c>CompareTo</c>, on both sides
/// (#528). They read the comparison from its spelling and lower-cased both sides, so the Kelvin sign
/// matched a k, a comparison held in a variable was dropped, <c>Replace</c> dropped its own, a range
/// past the end clamped instead of throwing, and <c>CompareTo</c> ordered by code unit, every capital
/// before every small letter. A SEARCH by a culture comparison has no case here: the browser refuses
/// it, at build time when it is a constant, which the compiler's tests pin.
/// </summary>
public class InstanceStringComparisonConformanceTests
{
    [SkippableTheory]
    [InlineData("return (\"x\" + \"\\u212A\").Contains(\"k\", StringComparison.OrdinalIgnoreCase);")] // false: the Kelvin sign is not a k
    [InlineData("return (\"\\u212A\" + \"x\").StartsWith(\"k\", StringComparison.OrdinalIgnoreCase);")] // false
    [InlineData("return \"Kx\".StartsWith(\"k\", StringComparison.OrdinalIgnoreCase);")] // true
    [InlineData("return (\"a\" + \"\\u212A\" + \"k\").IndexOf(\"K\", StringComparison.OrdinalIgnoreCase);")] // 2
    [InlineData("return (\"x\" + \"\\u017F\").EndsWith(\"S\", StringComparison.OrdinalIgnoreCase);")] // false: nor the long s an s
    [InlineData("return \"\\u0131\".Equals(\"I\", StringComparison.OrdinalIgnoreCase);")] // false: nor the dotless i an I
    [InlineData("return \"\\u0131x\".StartsWith(\"i\", StringComparison.OrdinalIgnoreCase);")] // false
    [InlineData("return \"\\u1F80x\".StartsWith(\"\\u1F88\", StringComparison.OrdinalIgnoreCase);")] // true: an iota subscript to its title case
    [InlineData("return \"x\\uD801\\uDC28y\".IndexOf(\"\\uD801\\uDC00\", StringComparison.OrdinalIgnoreCase);")] // 1: a pair by its code point
    [InlineData("return \"x\\uD801\\uDC28\".IndexOf('\\uDC28', StringComparison.OrdinalIgnoreCase);")] // 2: a lone half inside a pair
    [InlineData("return \"\\uD801\\uDC28\\uD801\\uDC28\".LastIndexOf(((char)0xD801).ToString(), StringComparison.OrdinalIgnoreCase);")] // 2
    [InlineData("return \"\\uD801\\uDC28x\".StartsWith(\"\\uD801\\uDC00X\", StringComparison.OrdinalIgnoreCase);")] // true
    public void AnOrdinalSearchIgnoringCase_KeepsDotNetsCasing(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("var c = StringComparison.OrdinalIgnoreCase; return \"aB\".StartsWith(\"ab\", c);")] // true
    [InlineData("var c = StringComparison.OrdinalIgnoreCase; return \"aB\".Equals(\"ab\", c);")] // true
    [InlineData("var c = StringComparison.Ordinal; return \"aB\".Contains(\"b\", c);")] // false
    [InlineData("StringComparison c = StringComparison.OrdinalIgnoreCase; return \"aBcB\".LastIndexOf(\"b\", 2, c);")] // 1
    [InlineData("var c = StringComparison.OrdinalIgnoreCase; return \"aBc\".Replace(\"b\", \"x\", c);")] // "axc"
    [InlineData("var c = (StringComparison)9; try { return \"abc\".StartsWith(\"a\", c).ToString(); } catch (ArgumentException e) { return e.Message; }")] // not supported
    [InlineData("var active = true; return \"aB\".StartsWith(\"ab\", active ? StringComparison.OrdinalIgnoreCase : StringComparison.CurrentCulture);")] // true: no constant, so the runtime decides
    [InlineData("return \"abcb\".IndexOf(value: \"b\", comparisonType: StringComparison.Ordinal, startIndex: 2);")] // 3: the comparison found by its parameter
    public void AComparisonHeldInAVariable_IsTheOneItHolds(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("return \"abcb\".IndexOf(\"B\", 2, StringComparison.OrdinalIgnoreCase);")] // 3
    [InlineData("return \"abcb\".IndexOf(\"B\", 2, 1, StringComparison.OrdinalIgnoreCase);")] // -1: the count ends the search
    [InlineData("return \"abcb\".IndexOf(\"b\", 1, 1, StringComparison.Ordinal);")] // 1
    [InlineData("try { return \"abc\".IndexOf(\"b\", 4, StringComparison.Ordinal).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // startIndex
    [InlineData("try { return \"abcb\".IndexOf(\"B\", 2, 5, StringComparison.OrdinalIgnoreCase).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // count
    [InlineData("return \"abc\".LastIndexOf(\"b\", 3, StringComparison.OrdinalIgnoreCase);")] // 1: one past the end steps back
    [InlineData("return \"abcb\".LastIndexOf(\"B\", 3, 2, StringComparison.OrdinalIgnoreCase);")] // 3
    [InlineData("return \"abcb\".LastIndexOf(\"b\", 2, 2, StringComparison.Ordinal);")] // 1: the count ends the search back
    [InlineData("try { return \"abc\".LastIndexOf(\"b\", 2, 4, StringComparison.Ordinal).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // count
    [InlineData("try { return \"abc\".LastIndexOf(\"b\", 4, StringComparison.Ordinal).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // startIndex
    [InlineData("return \"abc\".LastIndexOf(\"\", 1, StringComparison.OrdinalIgnoreCase);")] // 2
    [InlineData("return \"\".LastIndexOf(\"\", StringComparison.Ordinal);")] // 0
    [InlineData("return \"abc\".LastIndexOf(\"\", StringComparison.Ordinal);")] // 3
    [InlineData("try { return \"abc\".IndexOf(null, StringComparison.Ordinal).ToString(); } catch (ArgumentNullException e) { return e.Message; }")] // value
    public void TheStartAndTheCount_AreCheckedAsDotNetChecksThem(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("return \"aBc\".Replace(\"b\", \"x\", StringComparison.OrdinalIgnoreCase);")] // "axc"
    [InlineData("return \"aAaA\".Replace(\"AA\", \"x\", StringComparison.OrdinalIgnoreCase);")] // "xx"
    [InlineData("return (\"x\" + \"\\u212A\").Replace(\"k\", \"y\", StringComparison.OrdinalIgnoreCase) == \"x\" + \"\\u212A\";")] // true: nothing replaced
    [InlineData("return \"abc\".Replace(\"b\", \"$&$&\");")] // "a$&$&c": the replacement is text
    [InlineData("return \"abc\".Replace(\"b\", null);")] // "ac"
    [InlineData("try { return \"abc\".Replace(\"\", \"x\"); } catch (ArgumentException e) { return e.Message; }")] // empty
    [InlineData("try { return \"abc\".Replace(null, \"x\", StringComparison.OrdinalIgnoreCase); } catch (ArgumentNullException e) { return e.Message; }")] // oldValue
    public void Replace_KeepsItsComparisonAndWritesText(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("return \"a\".CompareTo(\"B\");")] // -1: the current culture, not the code units
    [InlineData("return \"B\".CompareTo(\"a\");")] // 1
    [InlineData("string none = null; return \"a\".CompareTo(none);")] // 1
    [InlineData("var names = new List<string> { \"b\", \"B\", \"a\", \"A\" }; names.Sort((x, y) => x.CompareTo(y)); return string.Join(\",\", names);")] // "a,A,b,B"
    [InlineData("return \"\\u00ADab\".Equals(\"ab\", StringComparison.InvariantCulture);")] // true: a whole-string culture comparison
    [InlineData("return \"a\".Equals(\"A\", StringComparison.CurrentCultureIgnoreCase);")] // true
    [InlineData("return \"\\u212A\".Equals(\"k\", StringComparison.OrdinalIgnoreCase);")] // false
    public void AWholeStringComparison_FollowsTheStatics(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
