using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A nullable struct answers as .NET answers it, executed on both sides.
/// <list type="bullet">
/// <item>A member a struct declares, read through <c>?.</c> or a property pattern, is the struct's own,
/// whatever its name (#966): <c>Value</c>, <c>HasValue</c> and <c>GetValueOrDefault</c> were taken for
/// <c>Nullable&lt;T&gt;</c>'s, so <c>a?.Value</c> answered the struct itself, and <c>Count</c> for a
/// collection's, so <c>c?.Count</c> read a <c>length</c> no struct has.</item>
/// <item>An empty nullable unwrapped to its value throws <c>InvalidOperationException</c>, as
/// <c>Nullable&lt;T&gt;.Value</c> does (#967): by a cast to the value's type or another, by a conversion
/// through a struct's own operator, which C# applies to the value, by a <c>foreach</c> over nullable
/// elements, and by <c>Value</c> itself. The browser carried the null on, and a struct's operator built a
/// value holding it.</item>
/// <item>Equals over a nullable struct, <c>Nullable.Equals</c> and <c>EqualityComparer&lt;T&gt;.Default.Equals</c>
/// answer as the lifted <c>==</c> does (#968): two absent values equal, an absent one unequal to any value,
/// and two values by the struct's own equality, which is its <c>Equals</c>, never its <c>==</c>. The build
/// refused all three. A struct whose own <c>Equals</c> is the app's code is asked it every time, a value compared
/// with itself included, as .NET's <c>Nullable&lt;T&gt;.Equals</c> and default comparers ask it with no identity
/// check; the browser answered such a value at its identity check, and the override never ran.</item>
/// <item>The <c>ToString</c> method group of a nullable value writes what its call writes, of the value as it is
/// when the delegate is made, and an empty one is refused there, as .NET refuses a delegate over the null it
/// boxes to (#955): the browser wrote JavaScript's text, <c>small</c>, <c>true</c> and <c>1,2</c>.</item>
/// </list>
/// </summary>
public class NullableStructConformanceTests
{
    private const string Values = """
        public readonly record struct Spot(int Row, int Col);

        public readonly struct Probe
        {
            public Probe(int value) { Value = value; }
            // Named as Nullable<T>'s members are: a read through a nullable Probe is Probe's own.
            public int Value { get; }
            public bool HasValue => Value > 0;
            public int GetValueOrDefault(int fallback) => Value > 0 ? Value : fallback;
        }

        public readonly record struct Cents(int Count)
        {
            public static explicit operator Cents(int value) => new(value);
        }

        public readonly struct Reach
        {
            public Reach(int start, int length) { Start = start; Length = length; }
            public int Start { get; }
            public int Length { get; }
            // Equal by the START alone: Equals is .NET's, member by member, and never this operator.
            public static bool operator ==(Reach a, Reach b) => a.Start == b.Start;
            public static bool operator !=(Reach a, Reach b) => a.Start != b.Start;
        }

        public readonly struct Loose
        {
            public Loose(int n) { N = n; }
            public int N { get; }
            // Its own Equals(object): another Loose by the last digit, and an int by its value.
            public override bool Equals(object o) => o is Loose l ? l.N % 10 == N % 10 : o is int i && i == N;
            public override int GetHashCode() => N % 10;
        }

        public enum Level { Low, High }

        public readonly struct Counted
        {
            public static int Calls;
            public Counted(int n) { N = n; }
            public int N { get; }
            // Counts each time it is asked: .NET asks it every time, a value compared with itself included.
            public override bool Equals(object o) { Calls++; return o is Counted c && c.N == N; }
            public override int GetHashCode() => N;
        }

        public readonly struct Typed : IEquatable<Typed>
        {
            public static int Calls;
            public static int TypedCalls;
            public Typed(int n) { N = n; }
            public int N { get; }
            public bool Equals(Typed other) { TypedCalls++; return other.N == N; }
            public override bool Equals(object o) { Calls++; return o is Typed t && Equals(t); }
            public override int GetHashCode() => N;
        }
        """;

    /// <summary>A static property of the app's own named like the comparer's, whose getter throws.</summary>
    private const string Comparers = """
        public static class Custom
        {
            public static EqualityComparer<int> Default => throw new InvalidOperationException("no comparer");
        }
        """;

    [SkippableTheory]
    // ---- a struct's own member named as Nullable<T>'s, through ?. ----
    [InlineData("Probe? a = new Probe(3); return a?.Value;")]                                  // 3
    [InlineData("Probe? a = null; return a?.Value;")]                                          // null
    [InlineData("Probe? a = new Probe(3); return a?.Value + 1;")]                              // 4
    [InlineData("Probe? a = new Probe(0); return a?.HasValue;")]                               // false: Probe's, not the Nullable's
    [InlineData("Probe? a = new Probe(4); return a?.HasValue;")]                               // true
    [InlineData("Probe? a = new Probe(0); return a?.GetValueOrDefault(9);")]                   // 9
    [InlineData("Func<Probe?> make = () => new Probe(5); return make()?.Value;")]              // 5: a receiver held in a temporary
    [InlineData("var xs = new List<Probe?> { new Probe(1), null, new Probe(2) }; return xs.Select(x => x?.Value ?? -1).Sum();")] // 2
    [InlineData("KeyValuePair<int, string>? p = new KeyValuePair<int, string>(1, \"one\"); return p?.Value;")] // "one": a pair's own
    // ---- Nullable<T>'s own members keep their meaning ----
    [InlineData("Probe? a = new Probe(3); return a.HasValue;")]                                 // true
    [InlineData("Probe? a = null; return a.HasValue;")]                                         // false
    [InlineData("Probe? a = new Probe(3); return a.Value.Value;")]                              // 3
    [InlineData("Probe? a = new Probe(0); return a.GetValueOrDefault(new Probe(7)).Value;")]    // 0
    [InlineData("Probe? a = null; return a.GetValueOrDefault(new Probe(7)).Value;")]            // 7
    // ---- a struct's own member named as a collection's Count, through ?. and a property pattern ----
    [InlineData("Cents? c = new Cents(4); return c?.Count;")]                                   // 4
    [InlineData("Cents? c = null; return c?.Count;")]                                           // null
    [InlineData("Cents? c = new Cents(2); return c?.Count > 1;")]                               // true
    [InlineData("Func<Cents?> make = () => new Cents(6); return make()?.Count;")]               // 6
    [InlineData("Cents? c = new Cents(4); return c is { Count: 4 };")]                         // true
    [InlineData("Cents? c = null; return c is { Count: 4 };")]                                 // false
    [InlineData("Probe? a = new Probe(7); return a is { Value: 7, HasValue: true };")]         // true
    public void AStructsOwnMember_IsReadThroughANullable(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    [SkippableTheory]
    // ---- a vocabulary value whose own member is called Value ----
    [InlineData("SizeValue? s = SizeValue.Fixed(10); return s?.Value;")]                        // 10
    [InlineData("SizeValue? s = null; return s?.Value;")]                                       // null
    [InlineData("GridTrack? t = GridTrack.Flex(2); return t?.Value;")]                          // 2
    public void AVocabularyValuesOwnMember_IsReadThroughANullable(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // ---- a struct's own operator, which C# applies to the value ----
    [InlineData("int? n = null; try { Cents c = (Cents)n; return \"built \" + c.Count; } catch (InvalidOperationException e) { return e.Message; }")] // "Nullable object must have a value."
    [InlineData("int? n = null; try { Cents c = (Cents)n; return \"built\"; } catch (SystemException) { return \"refused\"; }")] // "refused"
    [InlineData("var log = \"\"; int? Read() { log += \"r\"; return null; } try { Cents c = (Cents)Read(); return \"built\"; } catch (InvalidOperationException) { return log; }")] // "r": read once
    [InlineData("int? n = 5; Cents c = (Cents)n; return c.Count;")]                             // 5
    // ---- a cast to the value's type, or to another ----
    [InlineData("int? n = null; try { int v = (int)n; return \"read \" + v; } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("int? n = null; try { long v = (long)n; return \"read \" + v; } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("double? d = null; try { var i = (int)d; return \"read \" + i; } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("decimal? m = null; try { var v = (double)m; return \"read \" + v; } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("char? c = null; try { var v = (int)c; return \"read \" + v; } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("Spot? s = null; try { var v = (Spot)s; return \"built\"; } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("Level? e = null; try { var v = (Level)e; return \"cast \" + v; } catch (InvalidOperationException ex) { return ex.Message; }")]
    [InlineData("Level? e = null; try { var v = (int)e; return \"cast \" + v; } catch (InvalidOperationException ex) { return ex.Message; }")]
    [InlineData("int? n = null; try { var v = (Level)n; return \"cast \" + v; } catch (InvalidOperationException ex) { return ex.Message; }")]
    // ---- a foreach over nullable elements, and Value itself ----
    [InlineData("var xs = new List<int?> { 1, null }; var total = 0; try { foreach (int x in xs) total += x; return \"summed \" + total; } catch (InvalidOperationException e) { return total + \" \" + e.Message; }")] // "1 Nullable object must have a value."
    [InlineData("int? n = null; try { var v = n.Value; return \"read \" + v; } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("Spot? s = null; try { return s.Value.Row; } catch (InvalidOperationException e) { return e.Message; }")]
    // ---- a present value converts as it did, and a lifted conversion keeps its null ----
    [InlineData("int? n = 5; return (long)n * 2L;")]                                            // "10"
    [InlineData("double? d = 2.7; return (int)d;")]                                             // 2
    [InlineData("Spot? s = new Spot(1, 2); return ((Spot)s).Row;")]                             // 1
    [InlineData("Level? e = Level.High; return (int)e;")]                                       // 1
    [InlineData("int? n = 1; return (Level)n;")]                                                // "High"
    [InlineData("var xs = new List<int?> { 1, 2 }; var total = 0; foreach (int x in xs) total += x; return total;")] // 3
    [InlineData("int? n = 4; return n.Value * 2;")]                                             // 8
    [InlineData("int? n = null; Cents? c = (Cents?)n; return c is null;")]                      // true
    [InlineData("int? n = null; long? l = n; return l is null;")]                               // true
    [InlineData("Level? e = null; int? n = (int?)e; return n is null;")]                        // true
    public void AnEmptyNullable_UnwrappedToItsValue_Throws(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    [SkippableTheory]
    // ---- Equals over two nullable record structs ----
    [InlineData("Spot? a = null, b = null; return a.Equals(b);")]                               // true
    [InlineData("Spot? a = new Spot(1, 2), b = null; return a.Equals(b);")]                     // false
    [InlineData("Spot? a = null, b = new Spot(1, 2); return a.Equals(b);")]                     // false
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(1, 2); return a.Equals(b);")]           // true
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(2, 1); return a.Equals(b);")]           // false
    [InlineData("Spot? a = new Spot(1, 2); Spot b = new Spot(1, 2); return a.Equals(b);")]      // true
    [InlineData("Spot? a = null; return a.Equals(null);")]                                      // true
    [InlineData("Spot? a = new Spot(1, 2); return a.Equals((object)5);")]                      // false: another type
    [InlineData("Spot? a = new Spot(1, 2); return a.Equals((object)new Spot(1, 2));")]         // true
    // ---- Nullable.Equals ----
    [InlineData("Spot? a = null, b = null; return Nullable.Equals(a, b);")]                     // true
    [InlineData("Spot? a = new Spot(1, 2), b = null; return Nullable.Equals(a, b);")]           // false
    [InlineData("Spot? a = null, b = new Spot(1, 2); return Nullable.Equals(a, b);")]           // false
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(1, 2); return Nullable.Equals(a, b);")] // true
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(2, 1); return Nullable.Equals(a, b);")] // false
    [InlineData("Spot? a = new Spot(1, 2); return Nullable.Equals(a, new Spot(1, 2));")]        // true
    [InlineData("int? a = 5, b = null; return Nullable.Equals(a, b) + \"|\" + Nullable.Equals(a, 5);")] // "False|True"
    [InlineData("Level? a = Level.High, b = Level.High; return Nullable.Equals(a, b);")]        // true
    // ---- EqualityComparer<T>.Default ----
    [InlineData("Spot? a = null, b = null; return EqualityComparer<Spot?>.Default.Equals(a, b);")]                       // true
    [InlineData("Spot? a = null, b = new Spot(1, 2); return EqualityComparer<Spot?>.Default.Equals(a, b);")]             // false
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(1, 2); return EqualityComparer<Spot?>.Default.Equals(a, b);")]   // true
    [InlineData("Spot? a = new Spot(1, 2), b = new Spot(2, 1); return EqualityComparer<Spot?>.Default.Equals(a, b);")]   // false
    [InlineData("Spot a = new Spot(1, 2), b = new Spot(1, 2); return EqualityComparer<Spot>.Default.Equals(a, b);")]     // true
    [InlineData("int? a = null, b = null; return EqualityComparer<int?>.Default.Equals(a, b);")]                         // true
    [InlineData("return EqualityComparer<int>.Default.Equals(1, 1) + \"|\" + EqualityComparer<string>.Default.Equals(\"a\", \"b\");")] // "True|False"
    [InlineData("return EqualityComparer<double>.Default.Equals(double.NaN, double.NaN);")]                              // true
    // ---- a struct's Equals is .NET's, member by member, never its == ----
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(1, 9); return a.Equals(b);")]        // false, where a == b is true
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(1, 5); return a.Equals(b);")]        // true
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(1, 9); return Nullable.Equals(a, b);")] // false
    [InlineData("Reach? a = new Reach(1, 5), b = new Reach(1, 9); return EqualityComparer<Reach?>.Default.Equals(a, b);")] // false
    // ---- a struct's own Equals(object) is asked ----
    [InlineData("Loose? a = new Loose(3), b = new Loose(13); return a.Equals(b);")]             // true
    [InlineData("Loose? a = new Loose(3); return a.Equals((object)3);")]                        // true
    [InlineData("Loose? a = null; return a.Equals((object)3);")]                                // false
    [InlineData("Loose? a = new Loose(3), b = null; return a.Equals(b);")]                      // false
    [InlineData("Loose? a = new Loose(3), b = new Loose(13); return Nullable.Equals(a, b);")]   // true
    [InlineData("Loose? a = new Loose(3), b = new Loose(13); return EqualityComparer<Loose?>.Default.Equals(a, b);")] // true
    // ---- each operand once, in the order written ----
    [InlineData("var log = \"\"; Spot? At(int i) { log += i; return new Spot(i, 0); } var same = At(1).Equals(At(2)); return log + same;")] // "12False"
    [InlineData("var log = \"\"; Spot? At(int i) { log += i; return null; } var same = Nullable.Equals(At(1), At(2)); return log + same;")] // "12True"
    [InlineData("var log = \"\"; Spot? At(int i) { log += i; return new Spot(1, 0); } var same = Nullable.Equals(n2: At(2), n1: At(1)); return log + same;")] // "21True"
    [InlineData("var log = \"\"; Spot? At(int i) { log += i; return null; } var same = EqualityComparer<Spot?>.Default.Equals(At(1), At(2)); return log + same;")] // "12True"
    // ---- inside a lambda ----
    [InlineData("var xs = new List<Spot?> { new Spot(1, 2), null, new Spot(1, 2) }; Spot? target = new Spot(1, 2); return xs.Count(x => x.Equals(target));")] // 2
    [InlineData("var xs = new List<Spot?> { new Spot(1, 2), null, new Spot(3, 4) }; return xs.Count(x => Nullable.Equals(x, null));")] // 1
    public void ANullableStructsEquals_AnswersAsTheLiftedEqualityDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    [SkippableTheory]
    // ---- the ToString group of a nullable value writes what its call writes (#955) ----
    [InlineData("Level? s = Level.High; Func<string> f = s.ToString; s = Level.Low; return f();")] // "High": the value made over
    [InlineData("bool? b = true; Func<string> g = b.ToString; return g();")]                         // "True"
    [InlineData("(int, int)? t = (1, 2); Func<string> h = t.ToString; return h();")]                 // "(1, 2)"
    [InlineData("double? d = 1.5; Func<string> f = d.ToString; return f();")]                         // "1.5"
    [InlineData("Spot? p = new Spot(1, 2); Func<string> f = p.ToString; return f();")]               // "Spot { Row = 1, Col = 2 }"
    // ---- an empty one is refused where the delegate is made ----
    [InlineData("Level? s = null; try { Func<string> f = s.ToString; return \"made \" + f(); } catch (NullReferenceException) { return \"nre\"; } catch (ArgumentException) { return \"arg\"; }")] // "nre"
    public void ANullablesToStringGroup_WritesWhatItsCallWrites(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    [SkippableTheory]
    // ---- a nullable struct's Equals asks its own Equals(object) every time, itself included, and never a null ----
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1); var r = a.Equals(a); return r + \"|\" + Counted.Calls;")]                     // "True|1"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1), b = new Counted(1); var r = a.Equals(b); return r + \"|\" + Counted.Calls;")]  // "True|1"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1), b = new Counted(2); var r = a.Equals(b); return r + \"|\" + Counted.Calls;")]  // "False|1"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1), b = null; var r = a.Equals(b); return r + \"|\" + Counted.Calls;")]           // "False|0"
    [InlineData("Counted.Calls = 0; Counted? a = null, b = new Counted(1); var r = a.Equals(b); return r + \"|\" + Counted.Calls;")]           // "False|0"
    [InlineData("Counted.Calls = 0; Counted? a = null, b = null; var r = a.Equals(b); return r + \"|\" + Counted.Calls;")]                     // "True|0"
    // ---- Nullable.Equals and EqualityComparer<T?>.Default ----
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1); var r = Nullable.Equals(a, a); return r + \"|\" + Counted.Calls;")]            // "True|1"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1), b = new Counted(2); var r = Nullable.Equals(a, b); return r + \"|\" + Counted.Calls;")] // "False|1"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1), b = null; var r = Nullable.Equals(a, b) || Nullable.Equals(b, a); return r + \"|\" + Counted.Calls;")] // "False|0"
    [InlineData("Counted.Calls = 0; Counted? a = null, b = null; var r = Nullable.Equals(a, b); return r + \"|\" + Counted.Calls;")]             // "True|0"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1); var r = EqualityComparer<Counted?>.Default.Equals(a, a); return r + \"|\" + Counted.Calls;")] // "True|1"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1), b = new Counted(1); var r = EqualityComparer<Counted?>.Default.Equals(a, b); return r + \"|\" + Counted.Calls;")] // "True|1"
    [InlineData("Counted.Calls = 0; Counted? a = new Counted(1), b = null; var r = EqualityComparer<Counted?>.Default.Equals(a, b) || EqualityComparer<Counted?>.Default.Equals(b, a); return r + \"|\" + Counted.Calls;")] // "False|0"
    // ---- a value that is not nullable, whose own Equals takes a null too ----
    [InlineData("Counted.Calls = 0; var v = new Counted(1); var r = v.Equals(v); return r + \"|\" + Counted.Calls;")]                          // "True|1"
    [InlineData("Counted.Calls = 0; var v = new Counted(1); var r = v.Equals(null); return r + \"|\" + Counted.Calls;")]                       // "False|1"
    [InlineData("Counted.Calls = 0; var v = new Counted(1); var r = EqualityComparer<Counted>.Default.Equals(v, v); return r + \"|\" + Counted.Calls;")] // "True|1"
    // ---- IEquatable<T> beside Equals(object): the comparers ask the typed one, Nullable<T>.Equals the override ----
    [InlineData("Typed.Calls = 0; Typed.TypedCalls = 0; Typed? a = new Typed(1); var r = a.Equals(a); return r + \"|\" + Typed.Calls + \"|\" + Typed.TypedCalls;")] // "True|1|1"
    [InlineData("Typed.Calls = 0; Typed.TypedCalls = 0; Typed? a = new Typed(1); var r = Nullable.Equals(a, a); return r + \"|\" + Typed.Calls + \"|\" + Typed.TypedCalls;")] // "True|0|1"
    [InlineData("Typed.Calls = 0; Typed.TypedCalls = 0; Typed? a = new Typed(1); var r = EqualityComparer<Typed?>.Default.Equals(a, a); return r + \"|\" + Typed.Calls + \"|\" + Typed.TypedCalls;")] // "True|0|1"
    [InlineData("Typed.Calls = 0; Typed.TypedCalls = 0; var v = new Typed(1); var r = v.Equals(v); return r + \"|\" + Typed.Calls + \"|\" + Typed.TypedCalls;")] // "True|0|1"
    [InlineData("Typed.Calls = 0; Typed.TypedCalls = 0; var v = new Typed(1); var r = v.Equals((object)v); return r + \"|\" + Typed.Calls + \"|\" + Typed.TypedCalls;")] // "True|1|1"
    [InlineData("Typed.Calls = 0; Typed.TypedCalls = 0; var v = new Typed(1); var r = EqualityComparer<Typed>.Default.Equals(v, v); return r + \"|\" + Typed.Calls + \"|\" + Typed.TypedCalls;")] // "True|0|1"
    // ---- a struct with no Equals of its own answers as before ----
    [InlineData("Reach? a = new Reach(1, 5); return a.Equals(a) + \"|\" + Nullable.Equals(a, a) + \"|\" + EqualityComparer<Reach?>.Default.Equals(a, a);")] // "True|True|True"
    public void AStructsOwnEquals_IsAskedEveryTime(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Values);
    }

    /// <summary>
    /// Only <c>EqualityComparer&lt;T&gt;</c>'s own <c>Default</c> is the default comparer: a static property of the
    /// app's own with the same name is a value its getter answers, a throw here, and a call made on it is no
    /// lowering's, so the build still refuses it, as it did before #968, where the property was dropped unread.
    /// </summary>
    [Fact]
    public void AnAppsOwnDefaultProperty_IsNotTheDefaultComparer() =>
        ConformanceRunner.AssertStatementsRefusedByTheBuild(
            "try { return Custom.Default.Equals(1, 1); } catch (InvalidOperationException e) { return e.Message; }",
            "EQ2004", Comparers);

    [SkippableTheory]
    // ---- a vocabulary record struct the browser holds as data ----
    [InlineData("Color? a = Color.FromRgb(1, 2, 3), b = Color.FromRgb(1, 2, 3); return a.Equals(b);")]  // true
    [InlineData("Color? a = Color.FromRgb(1, 2, 3), b = null; return a.Equals(b);")]                    // false
    [InlineData("Color? a = null; return Nullable.Equals(a, Color.White);")]                            // false
    [InlineData("Color? a = Color.White; return EqualityComparer<Color?>.Default.Equals(a, Color.FromRgb(255, 255, 255));")] // true
    public void ANullableVocabularyValuesEquals_AnswersAsTheLiftedEqualityDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }

    /// <summary>The spreadsheet's own values and an app's own structs, built as an app's build builds them:
    /// one module per type, each importing what it names.</summary>
    private const string App = """
        using System;
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;
        using eQuantic.UI.Sheet;

        public readonly record struct Spot(int Row, int Col);

        public readonly struct Probe
        {
            public Probe(int value) { Value = value; }
            public int Value { get; }
            public bool HasValue => Value > 0;
        }

        public readonly record struct Cents(int Count)
        {
            public static explicit operator Cents(int value) => new(value);
        }

        public sealed class Ledger
        {
            public SheetCellSnapshot? Last { get; private set; }
            public Probe? Reading { get; set; }
            public Cents? Total { get; set; }
            public void Record(int row, string value) => Last = new SheetCellSnapshot(new CellRef(row, 0), value);
            public string? LastValue() => Last?.Value;
            public int? ReadingValue() => Reading?.Value;
            public bool? ReadingPresent() => Reading?.HasValue;
            public int? TotalCount() => Total?.Count;
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a cell snapshot's own Value", "var l = new Ledger(); l.Record(1, \"x\"); return l.LastValue();"),
        ("no cell snapshot", "var l = new Ledger(); return l.LastValue();"),
        ("an app struct's own Value", "var l = new Ledger { Reading = new Probe(4) }; return l.ReadingValue();"),
        ("an app struct's own HasValue", "var l = new Ledger { Reading = new Probe(0) }; return l.ReadingPresent();"),
        ("an app record struct's own Count", "var l = new Ledger { Total = new Cents(3) }; return l.TotalCount();"),
        ("a vocabulary value's own Value", "SizeValue? s = SizeValue.Fixed(12); return s?.Value;"),
        ("an empty int converted by a struct's operator", "int? n = null; try { var c = (Cents)n; return \"built\"; } catch (InvalidOperationException e) { return e.Message; }"),
        ("an empty range's Value", "SheetRange? r = null; try { return r.Value.RowCount; } catch (InvalidOperationException e) { return e.Message; }"),
        ("an empty cell snapshot cast to its value", "SheetCellSnapshot? s = null; try { return ((SheetCellSnapshot)s).Value; } catch (InvalidOperationException e) { return e.Message; }"),
        ("two empty ranges are Equals", "SheetRange? a = null, b = null; return a.Equals(b);"),
        ("a range Equals the same range built apart", "SheetRange? a = new SheetRange(new CellRef(1, 1)); SheetRange? b = new SheetRange(new CellRef(1, 1)); return a.Equals(b);"),
        ("Nullable.Equals over a range and none", "SheetRange? a = new SheetRange(new CellRef(1, 1)), b = null; return Nullable.Equals(a, b);"),
        ("the default comparer of a nullable range", "SheetRange? a = new SheetRange(new CellRef(2, 2)), b = new SheetRange(new CellRef(2, 2)); return EqualityComparer<SheetRange?>.Default.Equals(a, b);"),
        ("an app record struct's Equals", "Spot? a = new Spot(1, 2), b = new Spot(1, 2); return a.Equals(b);"),
        ("Nullable.Equals over a vocabulary value", "Color? a = Color.FromRgb(1, 2, 3); return Nullable.Equals(a, Color.FromRgb(1, 2, 3));"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullableStruct_AnswersAsDotNetInAnAppsBuild(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(App, typeAnnotations, Cases);
}
