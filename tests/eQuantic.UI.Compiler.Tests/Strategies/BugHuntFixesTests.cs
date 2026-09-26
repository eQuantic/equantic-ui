using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Regression tests for translation bugs found during the bug hunt:
/// integer division, Math method mapping, ToString format specifiers and
/// switch-expression variable-pattern binding.
/// </summary>
public class BugHuntFixesTests
{
    [Fact]
    public void IntegerDivision_TruncatesTowardZero()
    {
        // C# int / int truncates; JS / is float division.
        var result = TestHelper.ConvertExpression("Id / 2");
        result.Should().Be("Math.trunc(this.id / 2)");
    }

    [Fact]
    public void DecimalDivision_DoesNotTruncate()
    {
        // Total is decimal -> result is fractional, must NOT be wrapped in Math.trunc.
        var result = TestHelper.ConvertExpression("Total / 2");
        result.Should().Be("this.total / 2");
    }

    [Fact]
    public void MathTruncate_MapsToTrunc()
    {
        var result = TestHelper.ConvertExpression("Math.Truncate(Total)");
        result.Should().Be("Math.trunc(this.total)");
    }

    [Fact]
    public void MathCeiling_MapsToCeil()
    {
        var result = TestHelper.ConvertExpression("Math.Ceiling(Total)");
        result.Should().Be("Math.ceil(this.total)");
    }

    [Fact]
    public void MathRound_RoutesThroughBankersRoundingHelper()
    {
        // Math.Round uses banker's rounding + a digit count; both go through the runtime `round` helper.
        TestHelper.ConvertExpression("Math.Round(Total, 2)").Should().Be("$eq.math.round(this.total, 2)");
        TestHelper.ConvertExpression("Math.Round(Total)").Should().Be("$eq.math.round(this.total)");
    }

    [Fact]
    public void MathF_WithoutAModel_StillAnswersInSinglePrecision()
    {
        // No model binds `Total` here, so no table answers — the text fallback does, and MathF's
        // answers are singles: its members return float, except Sign and ILogB.
        TestHelper.ConvertExpression("MathF.Sqrt(Total)").Should().Be("Math.fround(Math.sqrt(Math.fround(this.total)))");
        TestHelper.ConvertExpression("MathF.Round(Total, 2)").Should().Be("$eq.math.roundSingle(Math.fround(this.total), 2)");
        TestHelper.ConvertExpression("MathF.Sign(Total)").Should().Be("Math.sign(Math.fround(this.total))");
    }

    /// <summary>
    /// ...and its ARGUMENTS are singles, which no model converted: MathF's parameters are floats,
    /// and an int past 2^24 is not one until C# rounds it, so <c>MathF.Max(x, 16777217)</c>
    /// compares against 16777216. An int parameter stays an int, and a literal that already is a
    /// single stays as written.
    /// </summary>
    [Theory]
    [InlineData("MathF.Max(Total, 16777217)", "Math.max(Math.fround(this.total), Math.fround(16777217))")]
    [InlineData("MathF.Max(Total, 2.5f)", "Math.max(Math.fround(this.total), 2.5)")]
    [InlineData("MathF.ScaleB(Total, 3)", "Math.fround((Math.fround(this.total) * Math.pow(2, 3)))")]
    [InlineData("MathF.Round(Total, 2, MidpointRounding.AwayFromZero)", "$eq.math.roundSingle(Math.fround(this.total), 2, 'awayFromZero')")]
    [InlineData("Math.Max(Total, 16777217)", "Math.max(this.total, 16777217)")]
    public void WithoutAModel_MathF_TakesSingles(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
    }

    /// <summary>
    /// The fallback singles every MathF argument but ScaleB's exponent and Round's digits and
    /// mode, and that exemption is the BCL's own list, read here: MathF has no other parameter that
    /// is not a float (it has no RootN; float does). A .NET that adds one fails this, naming it,
    /// before the fallback rounds an int it should have left alone.
    /// </summary>
    [Fact]
    public void EveryMathFParameterThatIsNotAFloat_IsOneTheFallbackLeavesAlone()
    {
        var notFloats = typeof(MathF).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.ParameterType != typeof(float))
                .Select(parameter => $"{method.Name}.{parameter.Name}"))
            .Distinct()
            .Order()
            .ToArray();
        notFloats.Should().Equal("Round.digits", "Round.mode", "ScaleB.n");
    }

    /// <summary>
    /// Without a model a Round's overload is read from the call as written: a trailing mode was
    /// dropped (ToEven in its place), and a named mode was taken for the digits.
    /// </summary>
    [Theory]
    [InlineData("Math.Round(Total, 2, MidpointRounding.AwayFromZero)", "$eq.math.round(this.total, 2, 'awayFromZero')")]
    [InlineData("Math.Round(Total, MidpointRounding.AwayFromZero)", "$eq.math.roundWithMode(this.total, 'awayFromZero')")]
    [InlineData("MathF.Round(Total, System.MidpointRounding.ToZero)", "$eq.math.roundSingleWithMode(Math.fround(this.total), 'toZero')")]
    [InlineData("Math.Round(mode: MidpointRounding.ToEven, value: Total)", "$eq.math.roundWithMode(this.total, 'toEven')")]
    [InlineData("Math.Round(Total, digits: 3)", "$eq.math.round(this.total, 3)")]
    [InlineData("Math.Round(Total, 2, Mode)", "$eq.math.round(this.total, 2, this.mode)")]
    [InlineData("MathF.Round(Total, 2, Mode)", "$eq.math.roundSingle(Math.fround(this.total), 2, this.mode)")]
    [InlineData("Math.Round(Total, digits: Digits)", "$eq.math.round(this.total, this.digits)")]
    [InlineData("Math.Round(Total, mode: Mode)", "$eq.math.roundWithMode(this.total, this.mode)")]
    [InlineData("Math.Round(Total, Digits, mode: Mode)", "$eq.math.round(this.total, this.digits, this.mode)")]
    [InlineData("Math.Round(Total, -1)", "$eq.math.round(this.total, -1)")]
    public void WithoutAModel_ARoundKeepsTheOverloadItWasWrittenWith(string call, string expected)
    {
        TestHelper.ConvertExpression(call).Should().Be(expected);
    }

    /// <summary>
    /// ...and ONE argument past the value is a digit count or a mode, which only its type says:
    /// with no model to ask, a variable in that place is a build error. Taken for the digits, a
    /// mode variable chose the digits overload.
    /// </summary>
    [Theory]
    [InlineData("Math.Round(Total, Mode)")]
    [InlineData("Math.Round(Total, Digits)")]
    [InlineData("MathF.Round(Total, Mode)")]
    public void WithoutAModel_ARoundWhoseSecondArgumentCouldBeEither_IsABuildError(string call)
    {
        TestHelper.DiagnosticsFor(call).Should().Contain(d => d.Code == "EQ1004");
    }

    /// <summary>A member JavaScript's Math does not have is a BUILD error, never a guessed name:
    /// `Math.fround(Math.reciprocalEstimate(x))` threw at the call in the browser, and the table
    /// fences the reciprocal estimates by construction.</summary>
    [Fact]
    public void AMathMemberJavaScriptHasNot_IsABuildError()
    {
        TestHelper.ConvertExpression("MathF.ReciprocalEstimate(Total)").Should().NotContain("Math.reciprocalEstimate");
        TestHelper.DiagnosticsFor("MathF.ReciprocalEstimate(Total)").Should().Contain(d => d.Code == "EQ1004");
    }

    /// <summary>A NAMED argument in a call no model bound: nothing says which parameter it names,
    /// and the arguments in written order put <c>newBase</c> where the value goes. A build error,
    /// never a guessed placement; a bound call places it by the method's own parameters.</summary>
    [Fact]
    public void ANamedArgumentNoModelCanPlace_IsABuildError()
    {
        TestHelper.DiagnosticsFor("Math.Log(newBase: 2.0, a: Total)").Should().Contain(d => d.Code == "EQ1004");
        TestHelper.DiagnosticsFor("Math.Log(Total, 2.0)").Should().NotContain(d => d.Code == "EQ1004",
            "a call written in order is placed by position, which is all the table needs");
    }

    /// <summary>
    /// The fallback answers from the SAME table as a bound call, by name, on the home the class
    /// spells. Its own guesses were `Math.copySign`, `Math.bitIncrement` and `Math.iEEERemainder`,
    /// none of which JavaScript has, and a `Math.log(x, b)` that dropped the base.
    /// </summary>
    [Theory]
    [InlineData("MathF.CopySign(Total, 2f)")]
    [InlineData("MathF.BitIncrement(Total)")]
    [InlineData("MathF.IEEERemainder(Total, 3f)")]
    [InlineData("MathF.Log(Total, 2f)")]
    [InlineData("MathF.SinCos(Total)")]
    [InlineData("MathF.ScaleB(Total, 3)")]
    [InlineData("Math.CopySign(Total, 2.0)")]
    [InlineData("Math.BitIncrement(Total)")]
    [InlineData("Math.IEEERemainder(Total, 3.0)")]
    [InlineData("Math.Log(Total, 2.0)")]
    [InlineData("Math.Clamp(Total, 0.0, 1.0)")]
    public void WithoutAModel_TheMathSurface_IsAnsweredByTheTable(string call)
    {
        var emitted = TestHelper.ConvertExpression(call);
        emitted.Should().NotMatchRegex(@"Math\.(copySign|bitIncrement|iEEERemainder|scaleB|sinCos)\(",
            "JavaScript's Math has none of these");
        emitted.Should().NotMatchRegex(@"Math\.log\([^()]*,", "Math.log takes one argument, and the base is .NET's second");
    }

    [Fact]
    public void ToString_WithFormat_UsesFormatHelper()
    {
        var result = TestHelper.ConvertExpression("Total.ToString(\"F2\")");
        result.Should().Be("$eq.text.format(this.total, 'F2')");
    }

    [Fact]
    public void ToString_WithoutArguments_UsesString()
    {
        var result = TestHelper.ConvertExpression("Total.ToString()");
        result.Should().Be("String(this.total)");
    }

    [Fact]
    public void SwitchExpression_VarPattern_BindsCapturedVariable()
    {
        var result = TestHelper.ConvertCodeBlock("var r = x switch { var v => v + 1 };");
        result.Should().Contain("const v = _s");
        result.Should().Contain("return v + 1");
    }

    [Fact]
    public void OrderBy_IsStable_AndCopiesSource()
    {
        var result = TestHelper.ConvertExpression("list.OrderBy(x => x.Id)");
        result.Should().StartWith("[...this.list].sort("); // copies, does not mutate source
        result.Should().Contain("return 0;");               // returns 0 on equal keys (stable)
    }

    [Fact]
    public void Distinct_OnPrimitives_UsesSet()
    {
        // string elements -> Set is correct and matches C# value equality.
        var result = TestHelper.ConvertExpression("items.Distinct()");
        result.Should().Be("[...new Set(this.items)]");
    }

    [Fact]
    public void Distinct_OnPlainClass_UsesSet()
    {
        // Plain reference types use reference equality in C#, which JS Set already matches.
        var result = TestHelper.ConvertExpression("list.Distinct()");
        result.Should().Be("[...new Set(this.list)]");
    }

    [Fact]
    public void Distinct_OnRecord_UsesValueDedup()
    {
        // Records use structural equality; Set (reference) would be wrong.
        var result = TestHelper.ConvertExpression("points.Distinct()");
        result.Should().NotContain("new Set(this.points)");
        result.Should().Contain("JSON.stringify");
        result.Should().Contain("this.points.filter");
    }

    [Fact]
    public void EnumMemberAccess_IsStringNotNumeric()
    {
        // With a semantic model the enum member used to resolve to its numeric value (0);
        // it must now be its member-name string so the representation is consistent.
        var result = TestHelper.ConvertCodeBlock("var b = status == Status.Active;");
        result.Should().Contain("'active'");
        result.Should().NotContain("=== 0");
    }

    [Fact]
    public void ImplicitArrayCreation_BecomesJsArrayLiteral()
    {
        // Found by the conformance harness: array creation used to be emitted verbatim (invalid JS).
        TestHelper.ConvertExpression("new[]{1,2,3}").Should().Be("[1, 2, 3]");
    }

    [Fact]
    public void ExplicitArrayCreation_BecomesJsArrayLiteral()
    {
        TestHelper.ConvertExpression("new int[]{1,2,3}").Should().Be("[1, 2, 3]");
    }

    [Fact]
    public void ArrayContains_MapsToIncludes()
    {
        // Found by the conformance harness: array Contains used to emit `.contains` (not a function).
        TestHelper.ConvertExpression("new[]{1,2,3}.Contains(2)").Should().Be("[1, 2, 3].includes(2)");
    }

    [Fact]
    public void HashSetInitializer_KeepsElements()
    {
        // Found by the conformance harness: HashSet initializer values were dropped (new Set()).
        TestHelper.ConvertExpression("new HashSet<int>{1,2,3}").Should().Be("new Set([1, 2, 3])");
    }

    [Fact]
    public void DictionaryCount_IsTheClasssSize()
    {
        // Found by the conformance harness: Dictionary.Count emitted `.length`, which a dictionary has not.
        TestHelper.ConvertExpression("dict.Count").Should().Be("this.dict.size");
    }
}
