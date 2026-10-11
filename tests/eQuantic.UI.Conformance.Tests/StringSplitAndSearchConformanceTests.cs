using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>string.Split</c> in every overload, and the string searches that take a start and a count, run on
/// both sides (#753, #724). Split was JavaScript's <c>split</c> of its first argument whatever the
/// overload: an array of separators was read as one text joined by commas, a null one as the text
/// "null" where .NET splits on white space, the count was dropped, and an option held in a variable was
/// ignored. <c>LastIndexOf(value, startIndex)</c> took the start as the index a match may begin at,
/// where .NET's is the index it must end by, and neither search stopped at its count.
/// <para>
/// Each answer is printed with its count of parts and every part, so an empty part and a missing one
/// read differently.
/// </para>
/// </summary>
public class StringSplitAndSearchConformanceTests
{
    /// <summary>The statements, run in a <c>try</c> whose answer names the refusal: its type, its
    /// parameter where it has one, and its message.</summary>
    private static string Refused(string statements) =>
        "try { " + statements + " } "
        + "catch (ArgumentNullException e) { return \"null:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentOutOfRangeException e) { return \"range:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentException e) { return \"argument:\" + e.ParamName + \":\" + e.Message; }";

    private static string Parts(string split) =>
        $"var r = {split}; return r.Length + \":\" + string.Join(\"|\", r);";

    [SkippableTheory]
    // A char separator, several, an array of them, and none: white space, a null and an empty array included.
    [InlineData("\" abcd ef \".Split(new[] { 'a', 'a' })")]                                  // 2: |bcd ef
    [InlineData("\" abcd ef \".Split(new[] { 'a', 'd' })")]                                  // 3: |bc| ef
    [InlineData("\"xaybz\".Split('a', 'b')")]                                                // 3:x|y|z
    [InlineData("\" abcd ef \".Split((char[])null)")]                                        // 4:|abcd|ef|
    [InlineData("\" abcd ef \".Split(new char[0])")]                                         // 4:|abcd|ef|
    [InlineData("\" abcd ef \".Split()")]                                                    // 4:|abcd|ef|
    [InlineData("\"a\\u0085b\\u00A0c\".Split((char[])null)")]                                // 3:a|b|c: .NET's white space
    [InlineData("\"a,b;c\".Split(',', ';', ':', '.', '!', '?', '-', '_', '+', '=', '/')")]    // 3:a|b|c: eleven written one by one
    [InlineData("\"a,b;c\".Split([',', ';'])")]                                           // 3:a|b|c: a span's collection expression
    // A count: at most that many parts, the last one the rest.
    [InlineData("\" abcd ef \".Split('d', 2)")]                                              // 2: abc| ef
    [InlineData("\" abcd ef \".Split(new[] { 'a', 'd' }, 2)")]                               // 2: |bcd ef
    [InlineData("\" abcd ef \".Split(new[] { 'a', 'd' }, 1)")]                               // 1: abcd ef
    [InlineData("\" abcd ef \".Split(new[] { 'a', 'd' }, 0)")]                               // 0:
    [InlineData("\" abcd ef \".Split((char[])null, 2)")]                                     // 2:|abcd ef
    // The options: empty parts removed, parts trimmed, both, and the count with them.
    [InlineData("\" abcd ef \".Split(' ', StringSplitOptions.RemoveEmptyEntries)")]          // 2:abcd|ef
    [InlineData("\" abcd ef \".Split('d', StringSplitOptions.TrimEntries)")]                 // 2:abc|ef
    [InlineData("\" abcd ef \".Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)")] // 2:abcd|ef
    [InlineData("\" abcd ef \".Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)")]       // 2:abcd|ef
    [InlineData("\"  a  b  c  \".Split(' ', 3, StringSplitOptions.RemoveEmptyEntries)")]     // 3:a|b|c
    [InlineData("\" a , b , c \".Split(',', 2, StringSplitOptions.TrimEntries)")]            // 2:a|b , c
    [InlineData("\" , ,a,b\".Split(',', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)")] // 2:a|b
    [InlineData("\" abcd ef \".Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries)")] // 2:abcd|ef
    [InlineData("\" x \".Split(',', 1, StringSplitOptions.TrimEntries)")]                    // 1:x
    [InlineData("\" a,b \".Split(',', 1, StringSplitOptions.TrimEntries)")]                  // 1:a,b
    [InlineData("\"\".Split(',')")]                                                          // 1:
    [InlineData("\"\".Split(',', StringSplitOptions.RemoveEmptyEntries)")]                   // 0:
    [InlineData("\"a\".Split(',', 0)")]                                                      // 0:
    // A string separator, several, and none: a null or an empty one is no separator, a null array is white space.
    [InlineData("\" abcd ef \".Split(\"cd\")")]                                              // 2: ab| ef
    [InlineData("\"acdbcdc\".Split(\"cd\", 2, StringSplitOptions.None)")]                    // 2:a|bcdc
    [InlineData("\"xabyabz\".Split(\"ab\", 1, StringSplitOptions.None)")]                    // 1:xabyabz
    [InlineData("\" abcd ef \".Split((string)null)")]                                        // 1: abcd ef
    [InlineData("\" abcd ef \".Split(\"\")")]                                                // 1: abcd ef
    [InlineData("\"\".Split(\"\", StringSplitOptions.RemoveEmptyEntries)")]                  // 0:
    [InlineData("\" a \".Split(\"\", StringSplitOptions.TrimEntries)")]                      // 1:a
    [InlineData("\" abcd ef \".Split(new[] { \"cd\", \"b\" }, StringSplitOptions.None)")]    // 3: a|| ef
    [InlineData("\"abcd\".Split(new[] { \"b\", \"bc\" }, StringSplitOptions.None)")]         // 2:a|cd: the first that matches
    [InlineData("\"abcd\".Split(new[] { \"bc\", \"b\" }, StringSplitOptions.None)")]         // 2:a|d
    [InlineData("\" abcd ef \".Split(new string[] { null, \"\" }, StringSplitOptions.None)")] // 1: abcd ef
    [InlineData("\" abcd ef \".Split(new string[] { null, \"c\" }, StringSplitOptions.None)")] // 2: ab|d ef
    [InlineData("\" abcd ef \".Split((string[])null, StringSplitOptions.None)")]             // 4:|abcd|ef|
    [InlineData("\" abcd ef \".Split(new string[0], StringSplitOptions.None)")]              // 4:|abcd|ef|
    [InlineData("\" abcd ef \".Split((string[])null, 2, StringSplitOptions.None)")]          // 2:|abcd ef
    public void Split_SplitsAsDotNetSplits(string split)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Parts(split));
    }

    /// <summary>The separators, the count and the options are values like any other: held in a
    /// variable, named out of their order, and each evaluated once, in the order written.</summary>
    [SkippableTheory]
    [InlineData("var o = StringSplitOptions.RemoveEmptyEntries; var r = \"a,,b\".Split(',', o); return r.Length + \":\" + string.Join(\"|\", r);")] // 2:a|b
    [InlineData("var seps = new[] { ',', ';' }; var r = \"a,b;c\".Split(seps); return r.Length + \":\" + string.Join(\"|\", r);")]                    // 3:a|b|c
    [InlineData("var r = \"a,,b,c\".Split(options: StringSplitOptions.RemoveEmptyEntries, separator: ','); return r.Length + \":\" + string.Join(\"|\", r);")] // 3:a|b|c
    [InlineData("var log = \"\"; int N() { log += \"n\"; return 2; } char S() { log += \"s\"; return ','; } var r = \"a,b,c\".Split(count: N(), separator: S()); return log + \":\" + string.Join(\"|\", r);")] // ns:a|b,c
    [InlineData("string text = null; try { text.Split(','); return \"split\"; } catch (NullReferenceException) { return \"null\"; }")]               // null
    public void Split_TakesItsArgumentsAsValues(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("\" abc \".Split(' ', (StringSplitOptions)4); return \"split\";")]           // argument:options:Value of flags is invalid.
    [InlineData("\" abc \".Split(new[] { \"b\" }, (StringSplitOptions)8); return \"split\";")]
    public void Split_RefusesAsDotNetRefuses(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(statements));
    }

    /// <summary>A negative count, refused before the options, on two lines.</summary>
    [SkippableTheory]
    [InlineData("\" abc \".Split(new[] { 'a' }, -1); return \"split\";")]
    [InlineData("\" abc \".Split(' ', -1, (StringSplitOptions)4); return \"split\";")]
    [InlineData("\" abc \".Split(\"b\", -1, StringSplitOptions.None); return \"split\";")]
    public void Split_RefusesANegativeCountAsDotNetRefusesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNetExceptTheHostsNewline(Refused(statements),
            ".NET ends the message's first line with Environment.NewLine");
    }

    [SkippableTheory]
    // LastIndexOf: the start is the last char a match may end on, and the search runs back its count.
    [InlineData("return \"abcabc\".LastIndexOf(\"bc\", 4);")]                                // 1
    [InlineData("return \"abcabc\".LastIndexOf(\"c\", 5, 2);")]                              // 5
    [InlineData("return \"abcabc\".LastIndexOf(\"a\", 5, 2);")]                              // -1
    [InlineData("return \"abcabc\".LastIndexOf(\"a\", 6);")]                                 // 3: one past the end steps back
    [InlineData("return \"abcabc\".LastIndexOf(\"\", 2);")]                                  // 3
    [InlineData("return \"\".LastIndexOf(\"a\", 0);")]                                       // -1
    [InlineData("return \"\".LastIndexOf(\"a\", -1);")]                                      // -1
    [InlineData("return \"\".LastIndexOf(\"\", 0);")]                                        // 0
    [InlineData("return \"abcabc\".LastIndexOf(count: 2, value: \"c\", startIndex: 5);")]    // 5
    // IndexOf: the search stops at its count.
    [InlineData("return \"abcabc\".IndexOf(\"c\", 0, 2);")]                                  // -1
    [InlineData("return \"abcabc\".IndexOf(\"c\", 1, 2);")]                                  // 2
    [InlineData("return \"abcabc\".IndexOf(\"a\", 6);")]                                     // -1
    [InlineData("return \"abcabc\".IndexOf(\"\", 3);")]                                      // 3
    [InlineData("return \"abcabc\".IndexOf(\"\", 3, 0);")]                                   // 3
    public void ASearchWithAStartOrACount_FindsWhatDotNetFinds(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("return \"abcabc\".LastIndexOf(\"a\", 7);")]                                 // range:startIndex
    [InlineData("return \"abcabc\".LastIndexOf(\"a\", -1);")]                                // range:startIndex
    [InlineData("return \"abcabc\".LastIndexOf(\"a\", 2, 4);")]                              // range:count
    [InlineData("return \"abcabc\".LastIndexOf((string)null, 2);")]                          // null:value
    [InlineData("return \"abcabc\".IndexOf(\"a\", 7);")]                                     // range:startIndex
    [InlineData("return \"abcabc\".IndexOf(\"a\", 2, 5);")]                                  // range:count
    [InlineData("return \"abcabc\".IndexOf((string)null, 1);")]                              // null:value
    public void ASearchWithAStartOrACount_RefusesAsDotNetRefuses(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(statements));
    }
}
