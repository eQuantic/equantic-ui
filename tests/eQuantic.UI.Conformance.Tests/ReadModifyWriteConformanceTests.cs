using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Every increment and compound assignment the compiler spells out as <c>target = next(target)</c>
/// — a width that wraps, a float that rounds, a char, a Decimal, a user-defined operator, integer
/// division — evaluates its TARGET once, as C# does: the receiver and the index are observed a
/// single time, whatever the text of the lowering names twice. And a postfix step in value position
/// answers the value BEFORE the step, which no inverse recovers once it has wrapped or rounded.
/// Each case puts an effect in the target (<c>i++</c> as the index, a call as the receiver), so a
/// second evaluation is a different answer.
/// </summary>
public class ReadModifyWriteConformanceTests
{
    private const string Money = """
        public readonly struct Money
        {
            public readonly int Cents;
            public Money(int cents) { Cents = cents; }
            public static Money operator +(Money a, Money b) => new Money(a.Cents + b.Cents);
        }
        """;

    [SkippableTheory]
    // ---- float: the compound and both steps round, and the index steps once ----
    [InlineData("var a = new float[3]; int i = 0; a[i++] += 1.5f; return i * 100 + (double)a[0] * 10 + (double)a[1];")]
    [InlineData("var a = new[] { 0.1f, 0.2f }; int i = 0; float old = a[i++]++; return (double)old * 1000 + i * 100 + (double)a[0];")]
    [InlineData("var a = new[] { 0.1f, 0.2f }; int i = 0; ++a[i++]; return i * 100 + (double)a[0];")]
    [InlineData("var arr = new[] { 0.5f }; int calls = 0; float[] Get() { calls++; return arr; } Get()[0] += 0.25f; return calls * 10 + (double)arr[0];")]
    // ---- the narrow widths wrap, and a postfix in value position answers the old value ----
    [InlineData("var b = new byte[] { 250, 0 }; int i = 0; b[i++] += 10; return i * 1000 + b[0];")]
    [InlineData("byte b = 255; var old = b++; return old * 1000 + b;")]
    [InlineData("var b = new byte[] { 255 }; int i = 0; var old = b[i++]++; return old * 1000 + b[0] * 10 + i;")]
    [InlineData("var u = new uint[] { 4294967295u }; int i = 0; u[i++] += 2; return u[0].ToString() + \"|\" + i;")]
    // ---- a char steps its code unit ----
    [InlineData("char c = 'a'; char d = c++; return d.ToString() + c;")]
    [InlineData("var cs = new[] { 'a', 'x' }; int i = 0; cs[i++] += (char)1; return cs[0].ToString() + cs[1] + i;")]
    // ---- a Decimal steps and adds on the type ----
    [InlineData("var m = new[] { 1.5m, 2m }; int i = 0; m[i++] += 1m; return m[0].ToString() + \"|\" + i;")]
    [InlineData("decimal d = 1.5m; var old = d++; return old.ToString() + \"|\" + d;")]
    // ---- integer division stays integral, and back in the target's width ----
    [InlineData("var n = new[] { 7, 9 }; int i = 0; n[i++] /= 2; return n[0] * 10 + i;")]
    [InlineData("sbyte s = -128; s /= -1; return s;")]                       // -128
    [InlineData("short h = -32768; h /= -1; return h;")]                     // -32768
    [InlineData("try { checked { sbyte t = -128; t /= -1; return t; } } catch (OverflowException) { return 1; }")] // 1
    // ---- a right-hand side that changes the target's index: C# fixed the element first ----
    [InlineData("var a = new[] { 0.5f, 0.25f }; int i = 0; a[i] += (i = 1); return (double)a[0] * 100 + (double)a[1] * 10 + i;")]
    [InlineData("var b = new byte[] { 250, 7 }; int i = 0; b[i] += (byte)(i = 1); return b[0] * 100 + b[1] * 10 + i;")]
    [InlineData("var m = new[] { 1m, 2m }; int i = 0; m[i] += (i = 1); return m[0].ToString() + \"|\" + m[1] + \"|\" + i;")]
    [InlineData("var n = new[] { 9, 8 }; int i = 0; n[i] /= (i = 1) + 1; return n[0] * 100 + n[1] * 10 + i;")]
    public void TheTargetIsEvaluatedOnce(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A dictionary ENTRY is read through the guard that throws for a missing key and written
    /// plainly, and the value it takes follows its type's rule like any other target's. A template
    /// of its own had returned ahead of those rules: a float entry added doubles, a decimal's `+=`
    /// glued two texts together, a byte never wrapped and an int's `/=` kept its fraction.
    /// </summary>
    [SkippableTheory]
    [InlineData("var d = new Dictionary<int, float> { [0] = 0.1f }; d[0] += 0.2f; return (double)d[0];")]
    [InlineData("var d = new Dictionary<string, decimal> { [\"a\"] = 1.5m }; d[\"a\"] += 2.25m; return d[\"a\"].ToString();")] // "3.75"
    [InlineData("var d = new Dictionary<int, byte> { [0] = 250 }; d[0] += 10; return d[0].ToString();")]  // "4"
    [InlineData("var d = new Dictionary<int, char> { [0] = 'a' }; d[0] += (char)1; return d[0].ToString();")] // "b"
    [InlineData("var d = new Dictionary<int, int> { [0] = 7 }; d[0] /= 2; return d[0].ToString();")]     // "3"
    [InlineData("var d = new Dictionary<int, float>(); try { d[5] += 1f; return \"no\"; } catch (Exception) { return \"throws\"; }")]
    [InlineData("int n = 0; var d = new Dictionary<int, float> { [0] = 1f }; int K() { n++; return 0; } d[K()] += 0.5f; return n + \"|\" + (double)d[0];")] // "1|1.5"
    [InlineData("var d = new Dictionary<int, int> { [0] = 1 }; var r = (d[0] += 2) * 10; return r.ToString();")] // "30"
    [InlineData("var d = new Dictionary<int, int?> { [0] = null }; d[0] ??= 5; return d[0].ToString();")]  // "5"
    // A STEP reads first too, and .NET throws for a missing key, where JavaScript stepped an
    // undefined into NaN and created the key, and a nullable entry's lift made null of it.
    [InlineData("var d = new Dictionary<string, int>(); try { d[\"gone\"]++; return \"no\"; } catch (KeyNotFoundException) { return \"throws\"; }")]
    [InlineData("var d = new Dictionary<string, int?>(); try { d[\"gone\"]++; return \"no\"; } catch (KeyNotFoundException) { return \"throws\"; }")]
    [InlineData("var d = new Dictionary<string, int?>(); try { --d[\"gone\"]; return \"no\"; } catch (KeyNotFoundException) { return \"throws\"; }")]
    [InlineData("var d = new Dictionary<string, int?>(); try { var v = d[\"gone\"]--; return \"no\"; } catch (KeyNotFoundException) { return \"throws\"; }")]
    [InlineData("var d = new Dictionary<string, int> { [\"k\"] = 1 }; var old = d[\"k\"]++; var pre = ++d[\"k\"]; return old + \"|\" + pre + \"|\" + d[\"k\"];")] // "1|3|3"
    [InlineData("var d = new Dictionary<string, int?> { [\"k\"] = 4 }; var old = d[\"k\"]--; return old + \"|\" + d[\"k\"];")]  // "4|3"
    [InlineData("var d = new Dictionary<string, int?> { [\"k\"] = null }; d[\"k\"]++; return d[\"k\"] == null ? \"null\" : \"v\";")] // "null"
    [InlineData("var d = new Dictionary<string, long> { [\"k\"] = 1 }; d[\"k\"]++; --d[\"k\"]; d[\"k\"]++; return d[\"k\"].ToString();")] // "2"
    [InlineData("var d = new Dictionary<string, double> { [\"k\"] = 0.5 }; d[\"k\"]++; return d[\"k\"].ToString();")]  // "1.5"
    [InlineData("var d = new Dictionary<string, byte> { [\"k\"] = 255 }; d[\"k\"]++; return d[\"k\"].ToString();")]  // "0"
    [InlineData("int n = 0; var d = new Dictionary<int, int> { [0] = 5 }; int K() { n++; return 0; } var old = d[K()]++; return n + \"|\" + old + \"|\" + d[0];")] // "1|5|6"
    public void ADictionaryEntry_TakesItsTypesRule(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A right-hand side that REPLACES the receiver: C# fixed the object before it ran.</summary>
    [SkippableFact]
    public void ARightHandSideThatReplacesTheReceiver_WritesTheObjectTheTargetNamedFirst()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "var h = new Holder { F = 0.5f }; var other = new Holder { F = 2f }; var first = h; h.F += (h = other).F; return (double)first.F * 10 + (double)other.F;",
            // A record, because the harness emits a prelude's records; it is a reference type, so
            // `first` is the same object `h` named before the right-hand side replaced it.
            "public record Holder { public float F; }");
    }

    [SkippableFact]
    public void AUserDefinedOperatorCompound_EvaluatesItsTargetOnce()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "var ms = new[] { new Money(5), new Money(7) }; int i = 0; ms[i++] += new Money(10); return ms[0].Cents * 10 + i;",
            Money);
    }
}
