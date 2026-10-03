using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>GetHashCode</c> by .NET's contract: values <c>Equals</c> finds equal hash equal, and a type that
/// overrides it answers its own. Every call threw in the browser, <c>getHashCode is not a function</c>,
/// for a string, a number and a record alike (#519). .NET's own numbers are not stable across
/// processes, so a case compares two hashes, never a hash with a number, unless the app wrote it.
/// </summary>
public class HashConformanceTests
{
    private const string Prelude = """
        public record P(int X, int Y);
        public record Weighted(int X)
        {
            public override int GetHashCode() => X * 7;
        }
        public record Keyed(int A, string B)
        {
            public override int GetHashCode() => HashCode.Combine(A, B);
        }
        public struct Pt { public int X; public int Y; }
        public struct Tilted
        {
            public int X;
            public override int GetHashCode() => base.GetHashCode() ^ 1;
        }
        """;

    [SkippableTheory]
    [InlineData("return \"abc\".GetHashCode() == ('a' + \"bc\").GetHashCode();")]                                    // true
    [InlineData("int n = 42; return n.GetHashCode() == 42.GetHashCode();")]                                         // true
    [InlineData("return new P(1, 2).GetHashCode() == new P(1, 2).GetHashCode();")]                                  // true
    [InlineData("object o = new P(4, 5); return o.GetHashCode() == new P(4, 5).GetHashCode();")]                    // true
    [InlineData("return 1.0m.GetHashCode() == 1.00m.GetHashCode();")]                                               // true
    [InlineData("var nan = double.NaN; return nan.GetHashCode() == (0.0 / 0.0).GetHashCode();")]                    // true
    [InlineData("var zero = 0.0; return zero.GetHashCode() == (-0.0).GetHashCode();")]                              // true
    [InlineData("return new DateTime(2020, 1, 1).GetHashCode() == new DateTime(2020, 1, 1).GetHashCode();")]        // true
    [InlineData("return (1, \"a\").GetHashCode() == (1, \"a\").GetHashCode();")]                                    // true
    [InlineData("return new Weighted(3).GetHashCode();")]                                                            // 21
    [InlineData("return HashCode.Combine(1, \"a\") == HashCode.Combine(1, \"a\");")]                                // true
    [InlineData("return new Keyed(1, \"x\").GetHashCode() == new Keyed(1, \"x\").GetHashCode();")]                    // true: Combine inside an override
    [InlineData("var a = new Pt { X = 1, Y = 2 }; var b = new Pt { X = 1, Y = 2 }; return a.GetHashCode() == b.GetHashCode();")] // true: a struct
    [InlineData("var a = new Tilted { X = 3 }; var b = new Tilted { X = 3 }; return a.GetHashCode() == b.GetHashCode();")]         // true: base in a struct
    [InlineData("var xs = new int[200000]; return (xs, 1).GetHashCode() == (xs, 1).GetHashCode();")]                   // true: a large array
    public void AHash_AgreesWithEquals(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>
    /// A Guid is its canonical text, the lowercase <c>D</c> format, whatever spelling it was read from,
    /// so <c>==</c>, a dictionary's key, a set and its hash find one Guid where two spellings were two
    /// values, and the string constructor reads it too (#459).
    /// </summary>
    [SkippableTheory]
    [InlineData("return Guid.Parse(\"0f8fad5b-d9cb-469f-a165-70867728950A\") == Guid.Parse(\"0f8fad5b-d9cb-469f-a165-70867728950a\");")] // true
    [InlineData("return Guid.Parse(\" 0F8FAD5BD9CB469FA16570867728950A \").ToString();")]                                                   // "0f8fad5b-d9cb-469f-a165-70867728950a"
    [InlineData("var d = new Dictionary<Guid, int> { [Guid.Parse(\"0F8FAD5B-D9CB-469F-A165-70867728950A\")] = 1 }; return d.ContainsKey(new Guid(\"{0f8fad5b-d9cb-469f-a165-70867728950a}\"));")] // true
    [InlineData("var s = new HashSet<Guid> { Guid.Parse(\"A0000000-0000-0000-0000-000000000000\"), Guid.Parse(\"(a0000000-0000-0000-0000-000000000000)\") }; return s.Count;")] // 1
    [InlineData("var ok = Guid.TryParse(\"nope\", out var g); return ok + \":\" + g;")]                                                     // "False:00000000-0000-0000-0000-000000000000"
    [InlineData("try { Guid.Parse(\"nope\"); return \"parsed\"; } catch { return \"refused\"; }")]                                          // "refused"
    [InlineData("return Guid.Parse(\"A0000000-0000-0000-0000-000000000000\").GetHashCode() == Guid.Parse(\"a0000000-0000-0000-0000-000000000000\").GetHashCode();")] // true
    [InlineData("return new Guid() == Guid.Empty;")]                                                                            // true
    [InlineData("return string.Join(\",\", new[] { \"A0000000-0000-0000-0000-000000000000\" }.Select(Guid.Parse));")]          // "a0000000-0000-0000-0000-000000000000"
    public void AGuid_IsItsCanonicalText(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
