using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A framework member reached by its simple name through <c>using static</c> answers what its
/// qualified spelling answers (#485). The mappings matched the QUALIFIED syntax, so a simple name
/// bound to the same member fell to the rule meant for the app's own statics and became a member of a
/// class nothing defines: <c>NaN</c> was <c>Double.naN</c>, <c>PI</c> <c>Math.pI</c>,
/// <c>Empty</c> <c>String.empty</c>, and <c>Join(",", parts)</c> <c>String.join(…)</c>. Each compiled,
/// loaded, and answered undefined or threw at the first call, in the browser alone.
/// </summary>
public class UsingStaticConformanceTests
{
    [SkippableTheory]
    // ---- System.Double ----
    [InlineData("using static System.Double;", "return IsNaN(NaN) ? \"nan\" : \"number\";")]                        // "nan"
    [InlineData("using static System.Double;", "return PositiveInfinity > MaxValue;")]                              // true
    [InlineData("using static System.Double;", "return IsInfinity(NegativeInfinity) && Epsilon > 0;")]              // true
    // ---- System.Math ----
    [InlineData("using static System.Math;", "return Round(PI * 100) / 100;")]                                     // 3.14
    [InlineData("using static System.Math;", "return Max(2, 3) + Abs(-1) + Floor(E);")]                            // 6
    // ---- System.String ----
    [InlineData("using static System.String;", "var parts = new[] { \"a\", \"b\" }; return Join(\",\", parts) + \"|\" + Empty.Length;")] // "a,b|0"
    [InlineData("using static System.String;", "return IsNullOrEmpty(Empty) && Concat(\"a\", \"b\") == \"ab\";")]   // true
    public void AFrameworkMemberThroughUsingStatic_AnswersAsItsQualifiedSpelling(string usingStatic, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, usingStatic);
    }

    /// <summary>An enum's member reached bare is the member, as its qualified spelling is: it read
    /// <c>Level.high</c>, the member of a class nothing defines, where the member is <c>'high'</c>.</summary>
    [SkippableTheory]
    [InlineData("using static Level;\npublic enum Level { Low, High }", "var l = High; return l == Level.High ? l.ToString() : \"other\";")] // "High"
    [InlineData("using static Access;\n[System.Flags] public enum Access { None = 0, Read = 1, Write = 2 }", "return (int)(Read | Write);")] // 3
    [InlineData("using static System.DayOfWeek;", "return Monday == DayOfWeek.Monday && Friday > Monday;")]                // true
    public void AnEnumsMemberThroughUsingStatic_IsTheMember(string prelude, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }
}
