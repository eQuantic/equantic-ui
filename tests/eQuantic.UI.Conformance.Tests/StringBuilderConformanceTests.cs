using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for <c>System.Text.StringBuilder</c> — the compat type. Fluent chains transpile to the
/// runtime <c>stringBuilder</c> and must produce the same final string as .NET, including the quirks:
/// <c>Append(bool)</c> -> "True"/"False" and <c>AppendLine</c> -> "\n" (Unix Environment.NewLine, which
/// is what the .NET evaluator uses on this platform).
/// </summary>
public class StringBuilderConformanceTests
{
    [SkippableTheory]
    [InlineData("new StringBuilder().Append(\"Hello\").Append(\" \").Append(\"World\").ToString()")] // "Hello World"
    [InlineData("new StringBuilder(\"a\").Append(\"b\").Append(\"c\").ToString()")]                  // "abc"
    [InlineData("new StringBuilder().Append(1).Append(2).Append(3).ToString()")]                      // "123"
    [InlineData("new StringBuilder().Append(true).Append(false).ToString()")]                         // "TrueFalse"
    [InlineData("new StringBuilder(\"hello\").Insert(0, \">>\").ToString()")]                         // ">>hello"
    [InlineData("new StringBuilder(\"a-b-c\").Replace(\"-\", \"+\").ToString()")]                     // "a+b+c"
    [InlineData("new StringBuilder(\"hello\").Remove(0, 2).ToString()")]                              // "llo"
    [InlineData("new StringBuilder(\"hello\").Clear().Append(\"x\").ToString()")]                     // "x"
    [InlineData("new StringBuilder(\"hello\").Length")]                                               // 5
    [InlineData("new StringBuilder().Append(\"ab\").Append(\"cd\").Length")]                          // 4
    public void StringBuilder_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    /// <summary>
    /// The overloads that count or take a range (#650): each appended or inserted the whole value
    /// once, a <c>char[]</c> wrote JavaScript's text of an array, <c>a,b</c>, and <c>Replace</c> and
    /// <c>ToString</c> ignored their range. A null appends and inserts nothing, in every overload.
    /// </summary>
    [SkippableTheory]
    [InlineData("return new StringBuilder(\"12\").Append('x', 3).ToString();")]                                     // "12xxx"
    [InlineData("return new StringBuilder(\"12\").Append('x', 0).ToString();")]                                     // "12"
    [InlineData("return new StringBuilder(\"12\").Append(\"hello\", 1, 3).ToString();")]                            // "12ell"
    [InlineData("return new StringBuilder(\"12\").Append(\"abc\", 10, 0).ToString();")]                             // "12": an empty range anywhere
    [InlineData("return new StringBuilder(\"12\").Append(new StringBuilder(\"hello\"), 1, 3).ToString();")]         // "12ell"
    [InlineData("return new StringBuilder(\"12\").Append(new[] { 'a', 'b' }).ToString();")]                         // "12ab"
    [InlineData("return new StringBuilder(\"12\").Append(new[] { 'a', 'b', 'c' }, 1, 2).ToString();")]              // "12bc"
    [InlineData("return new StringBuilder(\"12\").Append((string)null).Append((object)null).Append((char[])null).ToString();")] // "12"
    [InlineData("return new StringBuilder(\"12\").Append((string)null, 0, 0).Append((char[])null, 0, 0).ToString();")] // "12"
    [InlineData("return new StringBuilder(\"12\").Insert(1, \"ab\", 2).ToString();")]                               // "1abab2"
    [InlineData("return new StringBuilder(\"12\").Insert(1, (string)null, 3).ToString();")]                         // "12"
    [InlineData("return new StringBuilder(\"12\").Insert(1, new[] { 'x', 'y' }).ToString();")]                      // "1xy2"
    [InlineData("return new StringBuilder(\"12\").Insert(1, new[] { 'x', 'y', 'z' }, 1, 2).ToString();")]           // "1yz2"
    [InlineData("return new StringBuilder(\"12\").Insert(2, \"x\").Insert(1, (string)null).ToString();")]            // "12x"
    [InlineData("return new StringBuilder(\"aaaa\").Replace(\"a\", \"b\", 0, 2).ToString();")]                       // "bbaa"
    [InlineData("return new StringBuilder(\"aaaa\").Replace('a', 'b', 1, 2).ToString();")]                          // "abba"
    [InlineData("return new StringBuilder(\"aaaa\").Replace(\"aa\", \"b\", 1, 3).ToString();")]                      // "aba": a match must lie inside
    [InlineData("return new StringBuilder(\"aba\").Replace(\"a\", null).ToString();")]                               // "b"
    [InlineData("return new StringBuilder(\"12\").Remove(2, 0).ToString();")]                                       // "12"
    [InlineData("return new StringBuilder(\"12\").ToString(1, 1);")]                                                // "2"
    [InlineData("return new StringBuilder(\"12\").ToString(2, 0);")]                                                // ""
    public void ACountedOrRangedOverload_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    private static string Refused(string call) =>
        $"try {{ {call}; return \"no\"; }} "
        + "catch (ArgumentNullException e) { return \"null: \" + e.Message; } "
        + "catch (ArgumentOutOfRangeException e) { return \"range: \" + e.ParamName + \": \" + e.Message; } "
        + "catch (ArgumentException e) { return \"argument: \" + e.Message; }";

    /// <summary>Each refusal is .NET's, its checks in .NET's order: a null array before its range
    /// in <c>Insert</c>, the index before the value, an old value before the range in
    /// <c>Replace</c>.</summary>
    [SkippableTheory]
    [InlineData("new StringBuilder(\"12\").Append(\"abc\", 2, 5)")]                     // range: startIndex: Index was out of range. …
    [InlineData("new StringBuilder(\"12\").Append(new StringBuilder(\"abc\"), 2, 5)")]  // range: startIndex
    [InlineData("new StringBuilder(\"12\").Append(new[] { 'a' }, 5, 0)")]               // range: charCount: an empty range past the end of an array
    [InlineData("new StringBuilder(\"12\").Append((string)null, 0, 1)")]                // null: Value cannot be null. (Parameter 'value')
    [InlineData("new StringBuilder(\"12\").Append((char[])null, 0, 1)")]                // null
    [InlineData("new StringBuilder(\"12\").Insert(5, \"x\")")]                          // range: index
    [InlineData("new StringBuilder(\"12\").Insert(-1, (string)null)")]                  // range: index, before the value
    [InlineData("new StringBuilder(\"12\").Insert(5, \"ab\", 1)")]                      // range: index
    [InlineData("new StringBuilder(\"12\").Insert(1, new[] { 'x' }, 0, 2)")]            // range: startIndex
    [InlineData("new StringBuilder(\"12\").Insert(1, (char[])null, -1, 0)")]            // null: before the negative start
    [InlineData("new StringBuilder(\"aaaa\").Replace(\"\", \"x\")")]                    // argument: The value cannot be an empty string. (Parameter 'oldValue')
    [InlineData("new StringBuilder(\"aaaa\").Replace((string)null, \"x\", 5, 0)")]      // null: oldValue, before the range
    [InlineData("new StringBuilder(\"aaaa\").Replace(\"a\", \"b\", 2, 5)")]             // range: count
    [InlineData("new StringBuilder(\"aaaa\").Replace('a', 'b', -1, 1)")]                // range: startIndex
    [InlineData("new StringBuilder(\"12\").Remove(1, 5)")]                              // range: length
    [InlineData("new StringBuilder(\"12\").ToString(1, 5)")]                            // range: length: Index and length must refer to a location within the string.
    [InlineData("new StringBuilder(\"12\").ToString(3, 0)")]                            // range: startIndex: startIndex cannot be larger than length of string.
    public void ARefusal_IsDotNets(string call)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(call));
    }

    /// <summary>A negative count or start writes its actual value on a line of its own, which .NET
    /// ends with the host's newline. A null array's negative count names <c>charCount</c>, where a
    /// null string's names <c>count</c>: the reason the <c>char[]</c> overloads are their own
    /// methods.</summary>
    [SkippableTheory]
    [InlineData("new StringBuilder(\"12\").Append('x', -1)")]                           // repeatCount ('-1') must be a non-negative value. …
    [InlineData("new StringBuilder(\"12\").Append(\"abc\", -1, 1)")]                    // startIndex
    [InlineData("new StringBuilder(\"12\").Append((string)null, 0, -1)")]               // count
    [InlineData("new StringBuilder(\"12\").Append((char[])null, 0, -1)")]               // charCount
    [InlineData("new StringBuilder(\"12\").Insert(5, \"ab\", -1)")]                     // count, before the index
    [InlineData("new StringBuilder(\"12\").Insert(1, new[] { 'x' }, 0, -1)")]           // charCount
    [InlineData("new StringBuilder(\"12\").Remove(-1, -1)")]                            // length, before the start
    [InlineData("new StringBuilder(\"12\").ToString(-1, 1)")]                           // startIndex
    public void ARefusalOnTwoLines_IsDotNets(string call)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNetExceptTheHostsNewline(Refused(call),
            ".NET ends the message's first line with Environment.NewLine");
    }

    /// <summary>
    /// The expressions whose .NET answer is the HOST's newline.
    ///
    /// <para>
    /// `Environment.NewLine` is `\r\n` on Windows and `\n` elsewhere, and a browser has no
    /// environment — so the SDK decided its newline is `\n`, and said so in both places that
    /// implement it. The decision was written down twice and held until the suite ran on a Windows
    /// runner for the first time.
    /// </para>
    ///
    /// <para>
    /// Folded on both sides, so a translation defect still fails. What this does NOT do is make the
    /// divergence go away. WHERE it shows was measured, not assumed — see
    /// <see cref="ConformanceRunner.AssertSameAsDotNetExceptTheHostsNewline"/>: a browser folds CR LF in
    /// markup before the DOM exists, so a Windows-hosted server's SSR hydrates clean, and what differs
    /// is DATA carried to the client in a payload. The product answer under discussion is the SDK
    /// normalising its own strings to `\n`; it is open.
    /// </para>
    /// </summary>
    [SkippableTheory]
    [InlineData("new StringBuilder().AppendLine(\"line1\").Append(\"line2\").ToString()",
        "AppendLine appends Environment.NewLine")]
    public void StringBuilder_WhereDotNetAnswersTheHostsNewline(string expression, string why)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNetExceptTheHostsNewline(expression, why);
    }
}
