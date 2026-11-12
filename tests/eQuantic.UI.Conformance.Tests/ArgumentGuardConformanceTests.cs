using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The argument guards of .NET's argument exceptions throw where .NET throws, the exception it throws,
/// with its <c>ParamName</c>, its <c>ActualValue</c> and its words, and pass where it passes (#1202). Every
/// one was refused by the build (EQ2004).
/// </summary>
public class ArgumentGuardConformanceTests
{
    /// <summary>What a guard did, as the members C# reads: the exception's kind, its parameter's name, its
    /// actual value and its message, a carriage return taken out so a Windows host's newline reads as the
    /// browser's. A local function of every case, since the statement harness emits a prelude's records and
    /// never a class.</summary>
    private const string Run = """
        string Run(Action check)
        {
            try { check(); return "pass"; }
            catch (ArgumentOutOfRangeException e) { return "range|" + e.ParamName + "|" + e.ActualValue + "|" + e.Message.Replace("\r", ""); }
            catch (ArgumentNullException e) { return "null|" + e.ParamName + "|" + e.Message.Replace("\r", ""); }
            catch (ArgumentException e) { return "argument|" + e.ParamName + "|" + e.Message.Replace("\r", ""); }
            catch (NullReferenceException) { return "through null"; }
        }

        """;

    private const string Records = """
        public record P(int X);
        public record Rank(int Value) : IComparable<Rank> { public int CompareTo(Rank other) => other is null ? 1 : Value.CompareTo(other.Value); }
        public record Holder(int X);
        """;

    [SkippableTheory]
    // A sign, its parameter's name the text of the argument it checks.
    [InlineData("int part = -1; return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(part));")]
    [InlineData("var h = new Holder(-3); return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(h.X));")]
    [InlineData("int a = 1, b = 3; return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(a - b));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(0, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfZero(0, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(0, \"v\"));")]
    // A NaN is negative, and a negative zero both negative and zero.
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(double.NaN, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(-0.0, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfZero(-0.0, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(double.NaN, \"v\"));")]
    // Every numeric type the browser holds.
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(-2L, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(-2.5m, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfZero(0.00m, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(-1.25f, \"v\"));")]
    [InlineData("sbyte s = -3; return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(s, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfZero(0u, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfZero('a', \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfNegative('a', \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(double.NegativeInfinity, \"v\"));")]
    // A name left empty or null is written as nothing.
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(-1, \"\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(-1, null));")]
    // Equality, by the type's default equality: NaN equal to NaN, -0.0 to 0.0, a record by value.
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfEqual(3, 3, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfEqual(3, 4, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfEqual(\"a\", \"a\", \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfEqual(double.NaN, double.NaN, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfEqual(-0.0, 0.0, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfEqual(new P(1), new P(1), \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNotEqual(3, 4, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfNotEqual(3, 3, \"v\"));")]
    // A null in an equality guard is written as null, and has no actual value.
    [InlineData("string s = null; return Run(() => ArgumentOutOfRangeException.ThrowIfNotEqual(s, \"a\", \"v\"));")]
    [InlineData("string s = null; return Run(() => ArgumentOutOfRangeException.ThrowIfNotEqual(\"a\", s, \"v\"));")]
    [InlineData("string s = null; return Run(() => ArgumentOutOfRangeException.ThrowIfEqual(s, s, \"v\"));")]
    // Order, by the type's own comparer.
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(5, 4, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(4, 4, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(4, 4, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(3, 4, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfLessThan(3, 4, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfLessThan(4, 4, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(4, 4, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(5, 4, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfLessThan(double.NaN, double.NegativeInfinity, \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(double.NaN, double.NaN, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfLessThan(-0.0, 0.0, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(\"b\", \"a\", \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(\"a\", \"B\", \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan('b', 'a', \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(true, false, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(2.5m, 1m, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(new DateTime(2024, 1, 2), new DateTime(2024, 1, 1), \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(TimeSpan.FromSeconds(1.5), TimeSpan.Zero, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(new Rank(2), new Rank(1), \"v\")) + \"/\" + Run(() => ArgumentOutOfRangeException.ThrowIfLessThan(new Rank(2), new Rank(1), \"v\"));")]
    // CompareTo through a null value throws; a null other is written as nothing.
    [InlineData("string s = null; return Run(() => ArgumentOutOfRangeException.ThrowIfLessThan(s, \"a\", \"v\"));")]
    [InlineData("string s = null; return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(\"a\", s, \"v\"));")]
    // A null, an empty and a white string.
    [InlineData("string s = null; return Run(() => ArgumentNullException.ThrowIfNull(s));")]
    [InlineData("object o = null; return Run(() => ArgumentNullException.ThrowIfNull(o, null));")]
    [InlineData("int? n = null; return Run(() => ArgumentNullException.ThrowIfNull(n, \"n\")) + \"/\" + Run(() => ArgumentNullException.ThrowIfNull(5, \"v\"));")]
    [InlineData("string s = null; return Run(() => ArgumentException.ThrowIfNullOrEmpty(s)) + \"/\" + Run(() => ArgumentException.ThrowIfNullOrEmpty(\"\", \"v\")) + \"/\" + Run(() => ArgumentException.ThrowIfNullOrEmpty(\" \", \"v\"));")]
    [InlineData("return Run(() => ArgumentException.ThrowIfNullOrWhiteSpace(\"  \", \"v\")) + \"/\" + Run(() => ArgumentException.ThrowIfNullOrWhiteSpace(\"\", \"v\"));")]
    [InlineData("return Run(() => ArgumentException.ThrowIfNullOrWhiteSpace(\"\\u0085\", \"v\")) + \"/\" + Run(() => ArgumentException.ThrowIfNullOrWhiteSpace(\"\\u200B\", \"v\")) + \"/\" + Run(() => ArgumentException.ThrowIfNullOrWhiteSpace(\" a \", \"v\"));")]
    [InlineData("string s = null; return Run(() => ArgumentException.ThrowIfNullOrWhiteSpace(s));")]
    [InlineData("return Run(() => ArgumentException.ThrowIfNullOrEmpty(\"\", null));")]
    // Arguments named out of order are evaluated as they are written.
    [InlineData("var log = \"\"; int At(string s, int v) { log += s; return v; } var r = Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(other: At(\"o\", 1), value: At(\"v\", 2), paramName: \"p\")); return log + \"|\" + r;")]
    [InlineData("var log = \"\"; string Name(string s) { log += \"n\"; return s; } int At(int v) { log += \"v\"; return v; } var r = Run(() => ArgumentOutOfRangeException.ThrowIfNegative(paramName: Name(\"p\"), value: At(-1))); return log + \"|\" + r;")]
    public void AGuard_ThrowsAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Run + statements, Records);
    }

    /// <summary>The values in a guard's message are written in the current culture, as <c>string.Format</c>
    /// writes them: pt-BR's decimal comma, its dates and its infinity.</summary>
    [SkippableTheory]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(-1.5, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfEqual(1.5, 1.5, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(2.5m, 1m, \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(new DateTime(2024, 1, 2), new DateTime(2024, 1, 1), \"v\"));")]
    [InlineData("return Run(() => ArgumentOutOfRangeException.ThrowIfNegative(float.NegativeInfinity, \"v\"));")]
    public void AGuardsMessage_IsWrittenInTheCurrentCulture(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Run + statements, Records, culture: "pt-BR");
    }

    /// <summary>
    /// The collections' own refusals of a negative count, which .NET throws through
    /// <c>ArgumentOutOfRangeException.ThrowIfNegative</c>: <c>Queue&lt;T&gt;</c>'s and <c>Stack&lt;T&gt;</c>'s
    /// capacity, <c>LinkedList&lt;T&gt;.CopyTo</c>'s index and <c>HashSet&lt;T&gt;.CopyTo</c>'s. The runtime wrote
    /// their words by hand, so <c>ActualValue</c> reached C# as null while the message stated it (#1273).
    /// </summary>
    [SkippableTheory]
    [InlineData("return Run(() => new Queue<int>(-1));", "en-US")]
    [InlineData("return Run(() => new Stack<int>(-2));", "en-US")]
    [InlineData("var q = new Queue<int>(); return Run(() => q.EnsureCapacity(-1)) + \"/\" + Run(() => q.TrimExcess(-3));", "en-US")]
    [InlineData("var s = new Stack<int>(); return Run(() => s.EnsureCapacity(-1)) + \"/\" + Run(() => s.TrimExcess(-3));", "en-US")]
    [InlineData("var l = new LinkedList<int>(new[] { 1 }); return Run(() => l.CopyTo(new int[3], -1));", "en-US")]
    [InlineData("var h = new HashSet<int> { 1 }; return Run(() => h.CopyTo(new int[3], -1)) + \"/\" + Run(() => h.CopyTo(new int[3], 0, -2));", "en-US")]
    // The value written as the culture writes it, its minus sign included.
    [InlineData("return Run(() => new Queue<int>(-1));", "sv-SE")]
    [InlineData("var h = new HashSet<int> { 1 }; return Run(() => h.CopyTo(new int[3], -1));", "sv-SE")]
    public void ACollectionsNegativeCount_IsRefusedWithItsValue(string statements, string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Run + statements, Records, culture: culture);
    }

    /// <summary>A guard over a type the browser does not hold as a number whose sign it reads, or whose order
    /// it cannot reproduce, is refused by the build, never translated by a guess.</summary>
    [SkippableTheory]
    [InlineData("var g = Guid.NewGuid(); ArgumentOutOfRangeException.ThrowIfGreaterThan(g, g, \"v\"); return \"\";")]
    [InlineData("nint n = 1; ArgumentOutOfRangeException.ThrowIfNegative(n, \"v\"); return \"\";")]
    public void AGuardTheBrowserCannotAnswer_IsRefused(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsRefusedByTheBuild(statements, "EQ2004");
    }
}
