using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A value a case returns DIRECTLY compares as the runtime holds it (#596). The .NET side wrote it as
/// System.Text.Json writes it, and the JS side as <c>JSON.stringify</c> writes the runtime's value, so
/// every case below failed although its translation is right: a long that crossed as a BigInt printed
/// <c>"5"</c> against <c>5</c>, and <c>return 5L;</c>, which imports nothing from the runtime, threw
/// "JSON.stringify cannot serialize BigInt". The cases returned <c>.ToString()</c> instead, which hid
/// the trap: a long that became a JS number would have passed. <see cref="RuntimeJson"/> says what each
/// side writes.
/// </summary>
public class ReturnedValueConformanceTests
{
    private const string Prelude = """
        public record Item(long Id, string Name);
        public enum Day { Monday, Friday }
        [System.Flags] public enum Mode { None = 0, A = 1, B = 2 }
        public enum Big : long { A = 1 }
        """;

    [SkippableTheory]
    // A long and a ulong are a BigInt, wherever they sit.
    [InlineData("return 5L;")]
    [InlineData("return TimeSpan.FromSeconds(1).Ticks;")]
    [InlineData("return long.MaxValue;")]
    [InlineData("return ulong.MaxValue;")]
    [InlineData("long? n = 5; return n;")]
    [InlineData("long? n = null; return n;")]
    [InlineData("return (object)5L;")]
    [InlineData("return new long[] { 1, 2 };")]
    [InlineData("return new List<long> { 1, 2 };")]
    [InlineData("return new Item(5, \"x\");")]
    [InlineData("return new { A = 5L, B = 1 };")]
    [InlineData("return new Dictionary<string, long> { [\"a\"] = 1 };")]
    [InlineData("return new Dictionary<long, int> { [5] = 1 };")]
    // A decimal is the runtime's Decimal, its scale kept.
    [InlineData("return 1.5m;")]
    [InlineData("return new[] { 1.50m, 2m };")]
    [InlineData("return decimal.MaxValue;")]
    [InlineData("return new Dictionary<decimal, int> { [1.50m] = 1 };")]
    // An enum by its member's name, a flags enum and a value no member names by its number.
    [InlineData("return Day.Friday;")]
    [InlineData("return new[] { Day.Monday };")]
    [InlineData("return (Day)7;")]
    [InlineData("return Mode.A | Mode.B;")]
    [InlineData("return new Dictionary<Day, int> { [Day.Friday] = 1 };")]
    [InlineData("return new Dictionary<Mode, int> { [Mode.A | Mode.B] = 1 };")]
    [InlineData("return new Dictionary<Day, int> { [(Day)7] = 1 };")]
    // An enum over a long: its member by name, and a value no member names as the runtime holds it.
    [InlineData("return Big.A;")]
    [InlineData("return (Big)5;")]
    // A double as JavaScript writes it, where .NET's own text switches to E notation sooner.
    [InlineData("return 1e17;")]
    [InlineData("return 123456789012345678.0;")]
    [InlineData("return 0.00001;")]
    [InlineData("return 1e21;")]
    [InlineData("return double.NaN;")]
    [InlineData("return double.NegativeInfinity;")]
    [InlineData("return -0.0;")]
    [InlineData("return new Dictionary<double, int> { [1e21] = 1, [0.5] = 2 };")]
    // A float as the double the browser holds it in.
    [InlineData("return 0.1f;")]
    [InlineData("return 1e20f;")]
    // A value tuple and a pair are arrays.
    [InlineData("return (5L, \"x\");")]
    [InlineData("return (1, (2L, 3));")]
    [InlineData("return new KeyValuePair<string, long>(\"a\", 1);")]
    [InlineData("return new Dictionary<string, int> { [\"a\"] = 1 }.First();")]
    public void AValueReturnedDirectly_ComparesAsTheRuntimeHoldsIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    /// <summary>
    /// A case whose translation names nothing from the runtime imports nothing, so no
    /// <c>BigInt.prototype.toJSON</c> is installed: the harness writes the BigInt as the runtime would.
    /// The theory above imports the runtime through its prelude's record, and does not reach this.
    /// </summary>
    [SkippableTheory]
    [InlineData("return 5L;")]
    [InlineData("return new long[] { 1, 2 };")]
    [InlineData("return (1, 2L);")]
    public void ABigIntInACaseThatImportsNothing_PrintsAsTheRuntimeWouldWriteIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "");
    }
}
