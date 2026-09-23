using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>Broad string/char coverage sweep — drives the .NET coverage program for strings.</summary>
public class StringTotalityConformanceTests
{
    [SkippableTheory]
    // Padding
    [InlineData("\"5\".PadLeft(3, '0')")]              // "005"
    [InlineData("\"5\".PadRight(3, '0')")]             // "500"
    [InlineData("\"ab\".PadLeft(4)")]                  // "  ab"
    // Null/empty/whitespace
    [InlineData("string.IsNullOrEmpty(\"\")")]         // true
    [InlineData("string.IsNullOrEmpty(\"x\")")]        // false
    [InlineData("string.IsNullOrWhiteSpace(\"  \")")]  // true
    [InlineData("string.IsNullOrWhiteSpace(\"x\")")]   // false
    // Trim variants
    [InlineData("\"xxhixx\".TrimStart('x')")]          // "hixx"
    [InlineData("\"xxhixx\".TrimEnd('x')")]            // "xxhi"
    [InlineData("\"--hi--\".Trim('-')")]               // "hi"
    // Replace / case
    [InlineData("\"a-b-c\".Replace('-', '_')")]        // "a_b_c"
    [InlineData("\"Hello\".ToUpperInvariant()")]       // "HELLO"
    [InlineData("\"Hello\".ToLowerInvariant()")]       // "hello"
    // Concat / format / join
    [InlineData("string.Concat(\"a\", \"b\", \"c\")")] // "abc"
    [InlineData("string.Format(\"{0}-{1}\", 1, 2)")]   // "1-2"
    [InlineData("string.Join(\", \", new[]{\"a\",\"b\",\"c\"})")] // "a, b, c"
    // Index / chars
    [InlineData("\"hello\".IndexOf('l')")]             // 2
    [InlineData("\"hello\"[1]")]                        // "e"
    [InlineData("\"abc\".ToCharArray()")]              // ["a","b","c"]
    [InlineData("string.Empty")]                        // ""
    // char methods
    [InlineData("char.IsDigit('5')")]                  // true
    [InlineData("char.IsLetter('a')")]                 // true
    [InlineData("char.ToUpper('a')")]                  // "A"
    [InlineData("char.ToLower('A')")]                  // "a"
    // The (string, index) overloads read the character AT the index, and a surrogate pair there
    // as one code point. They tested the whole string, so every one of these answered false.
    [InlineData("char.IsDigit(\"a1\", 1)")]            // true
    [InlineData("char.IsLetter(\"1a\", 1)")]           // true
    [InlineData("char.IsLetter(\"a1\", 1)")]           // false
    [InlineData("char.IsUpper(\"aB\", 1)")]            // true
    [InlineData("char.IsWhiteSpace(\"a b\", 1)")]      // true
    [InlineData("char.IsLetter(\"x\U0001D400\", 1)")] // true — MATHEMATICAL BOLD CAPITAL A, a pair
    public void Strings_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
