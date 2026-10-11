using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>Convert.ToBase64String</c>, <c>ToBase64CharArray</c>, <c>ToHexString</c> and
/// <c>ToHexStringLower</c>, every overload, run on both sides (#716). Each was lowered to
/// <c>String()</c> of the array, so <c>new byte[] { 1, 2, 255 }</c> was <c>1,2,255</c> where .NET writes
/// <c>AQL/</c> and <c>0102FF</c>, and an offset, a length and the options went nowhere.
/// </summary>
public class ConvertEncodingConformanceTests
{
    /// <summary>The statements, run in a <c>try</c> whose answer names the refusal: its type, its
    /// parameter where it has one, and its message.</summary>
    private static string Refused(string statements) =>
        "try { " + statements + " } "
        + "catch (ArgumentNullException e) { return \"null:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentOutOfRangeException e) { return \"range:\" + e.ParamName + \":\" + e.Message; } "
        + "catch (ArgumentException e) { return \"argument:\" + e.ParamName + \":\" + e.Message; }";

    /// <summary>A hundred and fourteen bytes counting up from zero: two full lines of base64.</summary>
    private const string Counting = "var b = new byte[114]; for (int i = 0; i < b.Length; i++) b[i] = (byte)(i * 7); ";

    [SkippableTheory]
    [InlineData("return Convert.ToBase64String(new byte[] { 1, 2, 255 });")]                                     // AQL/
    [InlineData("return Convert.ToBase64String(new byte[] { 1 });")]                                             // AQ==
    [InlineData("return Convert.ToBase64String(new byte[] { 1, 2 });")]                                          // AQI=
    [InlineData("return Convert.ToBase64String(new byte[0]);")]                                                  // (empty)
    [InlineData("return Convert.ToBase64String(new byte[] { 251, 255, 191 });")]                                 // +/+/
    [InlineData("return Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }, 1, 3);")]                           // AgME
    [InlineData("return Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }, 5, 0);")]                           // (empty)
    [InlineData("return Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }, 1, 3, Base64FormattingOptions.None);")] // AgME
    [InlineData("return Convert.ToBase64String(Encoding.UTF8.GetBytes(\"h\\u00e9llo\"));")]                     // aMOpbGxv: a Uint8Array
    [InlineData("return Convert.ToBase64String([1, 2, 255]);")]                                                  // AQL/: a span's collection expression
    // Line breaks: every 76 characters, none after the last line, and none for a text of exactly one line.
    [InlineData(Counting + "return Convert.ToBase64String(b, Base64FormattingOptions.InsertLineBreaks).Replace(\"\\r\\n\", \"|\");")]
    [InlineData(Counting + "return Convert.ToBase64String(b, 0, 57, Base64FormattingOptions.InsertLineBreaks).Replace(\"\\r\\n\", \"|\");")]
    [InlineData(Counting + "return Convert.ToBase64String(b, 0, 58, Base64FormattingOptions.InsertLineBreaks).Replace(\"\\r\\n\", \"|\");")]
    [InlineData(Counting + "return Convert.ToBase64String(b, 1, 56, Base64FormattingOptions.InsertLineBreaks).Replace(\"\\r\\n\", \"|\");")]
    [InlineData(Counting + "var o = Base64FormattingOptions.InsertLineBreaks; return Convert.ToBase64String(b, o).Length.ToString();")]
    public void ToBase64String_EncodesAsDotNetEncodes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("return Convert.ToHexString(new byte[] { 1, 2, 255, 16 });")]                 // 0102FF10
    [InlineData("return Convert.ToHexStringLower(new byte[] { 1, 2, 255, 16 });")]            // 0102ff10
    [InlineData("return Convert.ToHexString(new byte[] { 1, 2, 255, 16 }, 1, 2);")]           // 02FF
    [InlineData("return Convert.ToHexStringLower(new byte[] { 1, 2, 255, 16 }, 2, 2);")]      // ff10
    [InlineData("return Convert.ToHexString(new byte[0]);")]                                  // (empty)
    [InlineData("return Convert.ToHexString(Encoding.UTF8.GetBytes(\"\\u00e9\"));")]         // C3A9: a Uint8Array
    [InlineData("return Convert.ToHexStringLower([171, 205]);")]                              // abcd: a span's collection expression
    public void ToHexString_EncodesAsDotNetEncodes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>The characters land in the array handed over, from its offset, and the count of them
    /// is the answer; what lies outside them is left as it was.</summary>
    [SkippableTheory]
    [InlineData("var o = new char[] { '.', '.', '.', '.', '.', '.', '.', '.' }; var n = Convert.ToBase64CharArray(new byte[] { 1, 2, 255 }, 0, 3, o, 2); return n + \":\" + new string(o);")] // 4:..AQL/..
    [InlineData("var o = new char[] { '.', '.', '.', '.' }; var n = Convert.ToBase64CharArray(new byte[] { 9, 1, 2 }, 1, 2, o, 0); return n + \":\" + new string(o);")]                 // 4:AQI=
    [InlineData("var o = new char[] { '.' }; var n = Convert.ToBase64CharArray(new byte[] { 1 }, 1, 0, o, 1); return n + \":\" + new string(o);")]                                     // 0:.
    [InlineData(Counting + "var o = new char[160]; var n = Convert.ToBase64CharArray(b, 0, 114, o, 0, Base64FormattingOptions.InsertLineBreaks); return n + \":\" + new string(o, 0, n).Replace(\"\\r\\n\", \"|\");")]
    public void ToBase64CharArray_WritesAsDotNetWrites(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>What .NET refuses, in the order it checks: the arrays, the length, the offsets, the
    /// options, then the range each offset leaves.</summary>
    [SkippableTheory]
    [InlineData("byte[] b = null; Convert.ToBase64String(b); return \"encoded\";")]
    [InlineData("byte[] b = null; Convert.ToBase64String(b, -1, -1); return \"encoded\";")]
    [InlineData("Convert.ToBase64String(new byte[3], 0, -1); return \"encoded\";")]
    [InlineData("Convert.ToBase64String(new byte[3], -1, -1); return \"encoded\";")]
    [InlineData("Convert.ToBase64String(new byte[3], -1, 2); return \"encoded\";")]
    [InlineData("Convert.ToBase64String(new byte[3], 2, 2); return \"encoded\";")]
    [InlineData("Convert.ToBase64String(new byte[3], (Base64FormattingOptions)2); return \"encoded\";")]
    [InlineData("Convert.ToBase64String(new byte[0], (Base64FormattingOptions)2); return \"encoded\";")]
    [InlineData("Convert.ToBase64String(new byte[3], 4, 0, (Base64FormattingOptions)2); return \"encoded\";")]
    [InlineData("byte[] b = null; Convert.ToHexString(b); return \"encoded\";")]
    [InlineData("byte[] b = null; Convert.ToHexStringLower(b, 0, 0); return \"encoded\";")]
    [InlineData("Convert.ToHexString(new byte[3], 0, -1); return \"encoded\";")]
    [InlineData("Convert.ToHexStringLower(new byte[3], -1, 0); return \"encoded\";")]
    [InlineData("Convert.ToHexString(new byte[3], 1, 3); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(null, 0, 0, null, 0); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], 0, 0, null, 0); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], 0, -1, new char[4], -1); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], -1, 3, new char[4], -1); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], 0, 3, new char[4], -1); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], 4, 0, new char[4], 0, (Base64FormattingOptions)2); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], 2, 2, new char[4], 0); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], 0, 3, new char[4], 1); return \"encoded\";")]
    [InlineData("Convert.ToBase64CharArray(new byte[3], 3, 0, new char[0], 9); return \"encoded\";")]
    public void AnEncoding_RefusesAsDotNetRefuses(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(statements));
    }

    /// <summary>The arguments are values like any other: held in variables, named out of their order,
    /// and each evaluated once, in the order written.</summary>
    [SkippableTheory]
    [InlineData("var log = \"\"; int O() { log += \"o\"; return 1; } int L() { log += \"l\"; return 2; } var r = Convert.ToHexString(length: L(), offset: O(), inArray: new byte[] { 1, 2, 3 }); return log + \":\" + r;")] // lo:0203
    [InlineData("var bytes = new byte[] { 1, 2, 255 }; var o = Base64FormattingOptions.None; return Convert.ToBase64String(bytes, o);")] // AQL/
    [InlineData("var b = new byte[] { 1, 2, 255 }; return Convert.ToBase64String(bytes: b, options: Base64FormattingOptions.None) + Convert.ToHexString(bytes: b);")] // AQL/0102FF: the span's overload, handed an array
    public void AnEncoding_TakesItsArgumentsAsValues(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
