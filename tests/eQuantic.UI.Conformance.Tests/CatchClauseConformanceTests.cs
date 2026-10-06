using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A catch clause tests its type and its filter, in the order C# tries the clauses, and an exception
/// none takes goes on (#474). Each clause was a JavaScript catch of its own: two of them were a
/// SyntaxError that cost the module, one took everything, its type never tested and its filter
/// dropped, and a <c>throw;</c> was a SyntaxError too.
/// <para>
/// Every typed clause here is the only way out of its case: a clause that took the wrong exception, or
/// let the right one through, answers differently or escapes, and the two sides disagree. A catch-all
/// is used only where the case is ABOUT one.
/// </para>
/// </summary>
public class CatchClauseConformanceTests
{
    private const string Prelude = """
        public class GateClosedException : InvalidOperationException
        {
            public GateClosedException(string message) : base(message) { }
        }

        public class Failure : Exception
        {
            public Failure(string message) : base(message) { }
        }
        """;

    [SkippableTheory]
    // The issue's four: two clauses, a type that does not match, a filter that refuses, and a filter
    // that declares a variable.
    [InlineData("try { throw new ArgumentException(\"x\"); } catch (InvalidOperationException) { return 1; } catch (ArgumentException) { return 2; }")] // 2
    [InlineData("try { try { throw new ArgumentException(\"x\"); } catch (InvalidOperationException) { return 1; } } catch (Exception) { return 2; }")] // 2
    [InlineData("try { try { throw new Exception(\"a\"); } catch (Exception e) when (e.Message == \"b\") { return 1; } } catch (Exception) { return 2; }")] // 2
    [InlineData("try { throw new FormatException(\"12\"); } catch (Exception e) when (int.TryParse(e.Message, out var n)) { return n; }")] // 12
    // A clause takes the types derived from its own, and the first clause that takes it wins.
    [InlineData("try { throw new ArgumentNullException(\"p\"); } catch (InvalidOperationException) { return \"wrong\"; } catch (ArgumentException) { return \"argument\"; }")]
    [InlineData("try { throw new ArgumentNullException(\"p\"); } catch (ArgumentException) { return 1; } catch (Exception) { return 2; }")] // 1
    [InlineData("try { throw new InvalidOperationException(\"x\"); } catch (ArgumentException) { return 1; } catch { return 2; }")] // 2
    // A filter that accepts, one that refuses ahead of a clause that takes it, and one that reads the
    // variable a later clause names again.
    [InlineData("try { throw new Exception(\"b\"); } catch (Exception e) when (e.Message == \"b\") { return 1; }")] // 1
    [InlineData("try { throw new InvalidOperationException(\"x\"); } catch (InvalidOperationException e) when (e.Message == \"y\") { return 1; } catch (InvalidOperationException e) { return e.Message.Length + 10; }")] // 11
    // A filter that THROWS has answered false, and the exception it was asked about goes on.
    [InlineData("Exception x = null; try { try { throw new InvalidOperationException(\"a\"); } catch (Exception e) when (x.Message == \"z\") { return \"first\"; } } catch (InvalidOperationException e) { return \"outer:\" + e.Message; }")]
    // A filter's pattern variable, and a variable two clauses' filters both declare.
    [InlineData("try { throw new ArgumentException(\"m\"); } catch (Exception e) when (e is ArgumentException { Message: var m }) { return m; }")] // "m"
    [InlineData("try { throw new FormatException(\"7\"); } catch (ArgumentException e) when (int.TryParse(e.Message, out var n)) { return n; } catch (FormatException e) when (int.TryParse(e.Message, out var n)) { return n * 2; }")] // 14
    // `throw;` rethrows the exception caught, even after the clause's variable is reassigned.
    [InlineData("try { try { throw new Exception(\"a\"); } catch (Exception) { throw; } } catch (Exception e) { return e.Message; }")] // "a"
    [InlineData("try { try { throw new Exception(\"a\"); } catch (Exception e) { e = new Exception(\"b\"); throw; } } catch (Exception e) { return e.Message; }")] // "a"
    [InlineData("try { try { throw new ArgumentException(\"a\"); } catch (ArgumentException) when (true) { throw; } } catch (ArgumentException e) { return e.Message; }")] // "a"
    // An exception thrown from an awaited call is caught by its type.
    [InlineData("async Task F() { await Task.Yield(); throw new InvalidOperationException(\"late\"); } try { await F(); return \"none\"; } catch (ArgumentException) { return \"argument\"; } catch (InvalidOperationException e) { return e.Message; }")]
    // `new(…)` targeting an exception type builds the exception.
    [InlineData("Exception e = new(\"boom\"); try { throw e; } catch (Exception caught) { return caught.Message; }")]
    public void ACatchClause_TestsItsTypeAndItsFilter(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// An exception the RUNTIME throws on .NET's behalf is of the type .NET throws there, so a clause
    /// of that type or of a base takes it and a clause of another type lets it through. A JavaScript
    /// TypeError, a member read through null, is the NullReferenceException .NET throws there.
    /// </summary>
    [SkippableTheory]
    [InlineData("int m = int.MaxValue; try { return checked(m + 1); } catch (DivideByZeroException) { return 1; } catch (ArithmeticException) { return 2; }")] // 2
    [InlineData("int zero = 0; try { return 1 / zero; } catch (OverflowException) { return 1; } catch (DivideByZeroException) { return 2; }")] // 2
    [InlineData("var d = new Dictionary<string, int>(); try { return d[\"k\"]; } catch (ArgumentException) { return 1; } catch (KeyNotFoundException) { return 2; }")] // 2
    [InlineData("var d = new Dictionary<string, int>(); try { return d[null!]; } catch (KeyNotFoundException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2
    [InlineData("try { return int.Parse(\"x\"); } catch (OverflowException) { return 1; } catch (FormatException) { return 2; }")] // 2
    [InlineData("try { return int.Parse(\"99999999999\"); } catch (FormatException) { return 1; } catch (OverflowException) { return 2; }")] // 2
    [InlineData("object gate = null; try { lock (gate) { return 0; } } catch (InvalidOperationException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2: a lock refuses a null gate
    [InlineData("try { return \"abc\".Substring(5); } catch (ArgumentNullException) { return 1; } catch (ArgumentException) { return 2; }")] // 2
    [InlineData("try { return new List<int>().Max(); } catch (ArgumentException) { return 1; } catch (InvalidOperationException) { return 2; }")] // 2
    [InlineData("try { return Convert.ToBoolean('a') ? 1 : 0; } catch (FormatException) { return 1; } catch (InvalidCastException) { return 2; }")] // 2
    [InlineData("string s = null; try { return s.Length; } catch (ArgumentException) { return 1; } catch (NullReferenceException) { return 2; }")] // 2
    [InlineData("try { return char.Parse(null!) == 'a' ? 0 : 1; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2: char.Parse refuses null by name
    [InlineData("try { Exception x = null; throw x!; } catch (InvalidOperationException) { return 1; } catch (NullReferenceException) { return 2; }")] // 2: throw null is a NullReferenceException
    [InlineData("try { return DateTime.Parse(null!).Year; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2: a null argument is refused by name
    [InlineData("try { return TimeSpan.Parse(null!).Hours; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2
    [InlineData("try { return DateOnly.Parse(null!).Day; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException e) { return e.Message.Length; }")] // the message names `s`
    [InlineData("try { return TimeOnly.Parse(null!).Hour; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2
    [InlineData("try { return DateTimeOffset.Parse(null!).Year; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException e) { return e.Message.Length; }")] // the message names `input`
    [InlineData("string format = null; try { return string.Format(format!, 1).Length; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2
    [InlineData("try { return new string((char[])null!, 0, 0).Length; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2
    [InlineData("return new string((char[])null!).Length;")] // 0: the one-array constructor takes null as no chars
    [InlineData("string s = null; try { return s.ToCharArray(0, 1).Length; } catch (ArgumentNullException) { return 1; } catch (NullReferenceException) { return 2; }")] // 2: a null receiver
    [InlineData("string s = null; try { return s ?? throw (Exception)null!; } catch (NullReferenceException) { return \"nre\"; }")] // "nre": and so is a throw expression's null
    [InlineData("var log = \"\"; Func<string> m = () => { log += \"m\"; return \"msg\"; }; Func<string> p = () => { log += \"p\"; return \"x\"; }; try { throw new ArgumentException(m(), p()); } catch (ArgumentException) { } try { throw new ArgumentOutOfRangeException(p(), m()); } catch (ArgumentException) { } return log;")] // "mppm": every argument runs, as written
    // The runtime's set, a list's searches, sorts and copies, and string.Join (#488, #438, #429).
    [InlineData("try { new HashSet<int>().UnionWith(null!); return 0; } catch (ArgumentOutOfRangeException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2
    [InlineData("try { return new HashSet<int>(-1).Count; } catch (ArgumentNullException) { return 1; } catch (ArgumentOutOfRangeException) { return 2; }")] // 2
    [InlineData("try { new HashSet<int> { 1, 2 }.CopyTo(new int[1]); return 0; } catch (ArgumentOutOfRangeException) { return 1; } catch (ArgumentException) { return 2; }")] // 2
    [InlineData("var s = new HashSet<int> { 1, 2 }; try { s.TrimExcess(1); return 0; } catch (ArgumentNullException) { return 1; } catch (ArgumentOutOfRangeException) { return 2; }")] // 2
    [InlineData("var s = new HashSet<int> { 1 }; try { foreach (var x in s) s.Add(x + 1); return 0; } catch (ArgumentException) { return 1; } catch (InvalidOperationException) { return 2; }")] // 2
    [InlineData("try { return new List<int> { 5, 6, 5 }.IndexOf(5, 1, 3); } catch (ArgumentNullException) { return -1; } catch (ArgumentOutOfRangeException) { return -2; }")] // -2
    [InlineData("try { new List<int> { 1, 2 }.CopyTo(new int[1]); return 0; } catch (ArgumentOutOfRangeException) { return 1; } catch (ArgumentException) { return 2; }")] // 2
    [InlineData("try { new List<int> { 1, 2 }.CopyTo(-1, new int[3], 0, 1); return 0; } catch (ArgumentNullException) { return 1; } catch (ArgumentOutOfRangeException) { return 2; }")] // 2
    [InlineData("try { return new List<int> { 1 }.RemoveAll(null!); } catch (ArgumentOutOfRangeException) { return -1; } catch (ArgumentNullException) { return -2; }")] // -2
    [InlineData("try { return Array.IndexOf((int[])null!, 1); } catch (NullReferenceException) { return -1; } catch (ArgumentNullException) { return -2; }")] // -2
    [InlineData("try { new List<int> { 2, 1 }.Sort((a, b) => throw new FormatException()); return 0; } catch (FormatException) { return 1; } catch (InvalidOperationException) { return 2; }")] // 2: what the comparison threw is the inner one
    [InlineData("var l = Enumerable.Range(0, 20).ToList(); try { l.Sort((a, b) => -1); return 0; } catch (InvalidOperationException) { return 1; } catch (ArgumentException) { return 2; }")] // 2: an inconsistent comparison
    [InlineData("try { Array.Sort(new[] { 5, 4, 3 }, 2, 2); return 0; } catch (ArgumentOutOfRangeException) { return 1; } catch (ArgumentException) { return 2; }")] // 2
    [InlineData("try { return new List<int> { 1, 3 }.BinarySearch(5, Comparer<int>.Create((a, b) => throw new FormatException())); } catch (FormatException) { return -1; } catch (InvalidOperationException) { return -2; }")] // -2
    [InlineData("try { return string.Join(\",\", (IEnumerable<int>)null!).Length; } catch (NullReferenceException) { return 1; } catch (ArgumentNullException) { return 2; }")] // 2
    public void AnExceptionTheRuntimeThrows_IsOfTheTypeDotNetThrows(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>An exception class of the app's own is one by its symbol, whatever its name, and a clause
    /// of its base takes it.</summary>
    [SkippableTheory]
    [InlineData("try { throw new GateClosedException(\"closed\"); } catch (ArgumentException) { return \"argument\"; } catch (GateClosedException e) { return e.Message; }")]
    [InlineData("try { throw new GateClosedException(\"closed\"); } catch (InvalidOperationException e) { return \"base:\" + e.Message; }")]
    [InlineData("try { throw new Failure(\"f\"); } catch (InvalidOperationException) { return \"wrong\"; } catch (Failure e) { return e.Message; }")]
    [InlineData("try { try { throw new InvalidOperationException(\"plain\"); } catch (GateClosedException) { return \"wrong\"; } } catch (InvalidOperationException e) { return e.Message; }")]
    public void AnExceptionOfTheAppsOwn_IsCaughtByItsType(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>The same test in a type pattern, a switch over exceptions and an <c>as</c>, which were
    /// a null check: every exception was every exception type.</summary>
    [SkippableTheory]
    [InlineData("Exception e = new ArgumentNullException(\"p\"); return $\"{e is ArgumentException}{e is InvalidOperationException}{e is Exception}\";")] // TrueFalseTrue
    [InlineData("object o = new InvalidOperationException(\"x\"); return o switch { ArgumentException => 1, InvalidOperationException => 2, _ => 3 };")] // 2
    [InlineData("Exception e = new FormatException(\"x\"); return (e as ArgumentException) == null;")] // true
    [InlineData("Exception e = new GateClosedException(\"x\"); return $\"{e is InvalidOperationException}{e is GateClosedException}{e is Failure}\";")] // TrueTrueFalse
    public void ATypePatternOverAnException_TestsItsType(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
