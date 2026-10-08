using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The formatter knows what it is handed where the compiler does (#455): a double says it is one, so
/// <c>D</c>, <c>X</c> and <c>B</c> refuse it as .NET does whatever it holds, where a whole one printed
/// as the integer it held; a <c>nint</c> and a <c>nuint</c> are integers of the platform's width, which
/// round a half away from zero and write a negative one in sixteen hexadecimal digits on the 64-bit
/// hosts .NET serves from, where they were formatted as doubles; and the per mille sign, the signs of an
/// exponent and the percent sign are the culture's own, which <c>Intl</c> does not have for the first
/// and spells without <c>ar</c>'s direction marks for the others.
/// </summary>
public class NumberKindConformanceTests
{
    [SkippableTheory]
    // A double takes no integer specifier, whole or not, in ToString, a hole and a placeholder.
    [InlineData("try { return (2.0).ToString(\"D\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { double d = 2; return $\"{d:X}\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return string.Format(\"{0:B}\", 2.0); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { double d = -4; return d.ToString(\"X2\"); } catch (Exception e) { return e.Message; }")]
    // An integer's still does, boxed for a placeholder or held as an object.
    [InlineData("object o = 2; int i = -1; return string.Format(\"{0:D3}|{0:X}|{1:X}\", o, i) + \"|\" + 5.ToString(\"D3\");")]
    // A nint and a nuint: integer ties, and the platform's width.
    [InlineData("nint n = 125; nint m = -1; nuint u = 255; return n.ToString(\"E1\") + \"|\" + m.ToString(\"X\") + \"|\" + m.ToString(\"B\") + \"|\" + u.ToString(\"X\") + \"|\" + $\"{m:X}\" + \"|\" + string.Format(\"{0:x}\", m) + \"|\" + n.ToString(\"D5\") + \"|\" + n.ToString(\"N0\") + \"|\" + n.ToString(\"0.0\");")]
    public void ANumbersKind_DecidesWhatItsFormatWrites(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>The culture's own symbols in what the formatter writes by hand: the per mille sign
    /// (<c>ar-SA</c>'s is <c>؉</c>), the exponent's signs and the percent sign of a picture.</summary>
    [SkippableTheory]
    [InlineData(null)]
    [InlineData("pt-BR")]
    [InlineData("sv-SE")]
    [InlineData("ar")]
    [InlineData("ar-SA")]
    public void TheCulturesSymbols_AreItsOwn(string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "return (0.5).ToString(\"0.0‰\") + \"|\" + (-0.25).ToString(\"0‰\") + \"|\" + (0.5).ToString(\"0%\") + \"|\" "
            + "+ (12345.678).ToString(\"E2\") + \"|\" + (0.000123).ToString(\"e3\") + \"|\" + (-0.000012345).ToString(\"G2\") + \"|\" "
            + "+ (1234567.5).ToString(\"#,##0.0\") + \"|\" + (-1234.5).ToString(\"R\");",
            culture: culture);
    }
}
