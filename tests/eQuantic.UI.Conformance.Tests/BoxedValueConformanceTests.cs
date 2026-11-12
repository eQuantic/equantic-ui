using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A value boxed into <c>object</c> keeps the .NET type its answers depend on (#552, #742, #768, #954). The
/// browser holds an enum member as its key, a float as a number, a list and an array as one array, and an
/// anonymous object as plain data, so once C# boxed them nothing told a boxed enum from a string of its
/// name, a float from a double, two lists from two tuples, or an anonymous object from a value held as data.
/// An enum and a float are boxed into a small box that carries their type, a list and an array are marked
/// as what they are, and an anonymous object is marked as one; each answers its equality, its hash, its text
/// and its unboxing as .NET's does. An int and a double, a char and a string, a long and a ulong held as
/// object stay one kind each, which is the limit the wiki states.
/// </summary>
public class BoxedValueConformanceTests
{
    /// <summary>The types the cases name, declared on both sides.</summary>
    public const string Prelude = """
        public enum Tone { Warm, Cool }
        public enum Scale { Small, Large }
        public enum Other { Small }
        public enum Status { Pending, Done }
        [Flags] public enum Perm { None = 0, Read = 1, Write = 2 }
        public record Bag(int A, int B) : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() { yield return A; yield return B; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public record Rank(int Level) : IComparable { public int CompareTo(object o) => o is Tone t ? (int)t + 10 : -1; }
        """;

    [SkippableTheory]
    // #552: a boxed enum is not a string of its name.
    [InlineData("""object value = "pending"; object named = "Pending"; return Enum.IsDefined(typeof(Status), value) + "|" + Enum.IsDefined(typeof(Status), named) + "|" + Enum.IsDefined(typeof(Status), (object)Status.Done);""")]
    [InlineData("""try { var s = (Status)(object)"pending"; return s.ToString(); } catch (InvalidCastException) { return "refused"; }""")]
    [InlineData("""object o = Other.Small; try { return ((Scale)o).ToString(); } catch (InvalidCastException) { return "refused"; }""")]
    // #768: a boxed float and a boxed enum print as .NET prints them.
    [InlineData("""object o = 0.1f; return o.ToString() + "|" + $"{o}" + "|" + ("" + o) + "|" + string.Format("{0}", o);""")]
    [InlineData("""object o = Tone.Warm; return o.ToString() + "|" + $"{o}" + "|" + ("" + o) + "|" + string.Format("{0}", o);""")]
    [InlineData("""return new System.Text.StringBuilder().Append((object)0.1f).Append('|').Append((object)Tone.Warm).ToString();""")]
    [InlineData("""var l = new List<object> { Tone.Cool, 1.5f, 0.1f, 2 }; return string.Join(",", l);""")]
    [InlineData("""object o = Perm.Read | Perm.Write; object n = Perm.None; return o.ToString() + "|" + n + "|" + (o is Perm) + "|" + ((Perm)o).HasFlag(Perm.Write);""")]
    // A record of the app's behind IEnumerable<T> compares by value held as object; only a list and an array are by reference.
    [InlineData("""IEnumerable<int> e = new Bag(1, 2); object o = e; var l = new List<object> { new Bag(1, 2) }; return l.Contains(o) + "|" + o.Equals(new Bag(1, 2)) + "|" + (o.GetHashCode() == new Bag(1, 2).GetHashCode());""")]
    // An app's own CompareTo(object) is handed the box, as .NET hands it.
    [InlineData("""return new Rank(0).CompareTo(Tone.Cool) + "|" + new Rank(0).CompareTo("cool");""")]
    // #954: a boxed enum compares and hashes as its type.
    [InlineData("""object other = "small"; Func<object, bool> same = Scale.Small.Equals; return Scale.Small.Equals(other) + "|" + same(other) + "|" + object.Equals(Scale.Small, other);""")]
    [InlineData("""var e = Scale.Small; object o = e; return (o.GetHashCode() == e.GetHashCode()) + "|" + (o.GetHashCode() == 0);""")]
    [InlineData("""object o = Other.Small; return Scale.Small.Equals(o) + "|" + object.Equals(Scale.Small, o) + "|" + o.Equals(Other.Small) + "|" + o.Equals((object)Other.Small);""")]
    [InlineData("""var set = new HashSet<object> { Tone.Warm, "warm", Scale.Small, Other.Small }; return set.Count + "|" + set.Contains(Tone.Warm) + "|" + set.Contains("Warm") + "|" + set.Contains(Other.Small);""")]
    // #954: a boxed list and a boxed array compare and hash by reference.
    [InlineData("""object a = new List<int> { 1 }, b = new List<int> { 1 }; return a.Equals(b) + "|" + object.Equals(a, b) + "|" + new HashSet<object> { a, b }.Count + "|" + a.Equals(a);""")]
    [InlineData("""object a = new[] { 1 }, b = new[] { 1 }; return a.Equals(b) + "|" + object.Equals(a, b) + "|" + new object[] { a, b }.Distinct().Count();""")]
    [InlineData("""var l = new List<int> { 1 }; object o = l; var h = o.GetHashCode(); l.Add(2); return (o.GetHashCode() == h) + "|" + (o.GetHashCode() == l.GetHashCode());""")]
    [InlineData("""object other = new[] { 1, 2 }; return object.Equals((1, 2), other) + "|" + (1, 2).Equals(other);""")]
    // A box unboxes to what it holds and to nothing else.
    [InlineData("""object o = Tone.Cool; var t = (Tone)o; return t + "|" + (o is Tone) + "|" + (o is Tone.Cool) + "|" + (o is Tone.Warm) + "|" + (o is int) + "|" + (o is string) + "|" + (o switch { Tone x => "tone " + x, _ => "other" });""")]
    [InlineData("""object o = 0.1f; var f = (float)o; try { var d = (double)o; return "unboxed " + d; } catch (InvalidCastException) { return f + "|" + (o is float) + "|" + (o is double) + "|" + Convert.ToSingle(o) + "|" + (Convert.ToDouble(o) == (double)0.1f); }""")]
    [InlineData("""object o = Tone.Cool; return (int)o + "|" + Convert.ToInt32(o) + "|" + Convert.ToString(o) + "|" + (o switch { Tone.Warm => "w", Tone.Cool => "c", _ => "?" });""")]
    [InlineData("""var d = new Dictionary<string, object> { ["t"] = Tone.Warm, ["f"] = 0.5f }; return d["t"] + "|" + d["f"] + "|" + ((Tone)d["t"] == Tone.Warm) + "|" + d["t"].Equals(Tone.Warm) + "|" + ((float)d["f"] * 2);""")]
    [InlineData("""Tone? n = Tone.Cool; object o = n; Tone? none = null; object empty = none; return o + "|" + (o is Tone) + "|" + (empty == null) + "|" + ((Tone?)o == Tone.Cool);""")]
    [InlineData("""object a = Tone.Warm, b = Tone.Warm; return (a == b) + "|" + a.Equals(b) + "|" + object.ReferenceEquals(a, a) + "|" + (a.GetHashCode() == b.GetHashCode());""")]
    public void AValueHeldAsObject_KeepsItsType(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // #742: a value the browser holds as data is not an anonymous object of the same members.
    [InlineData("""return Color.White.Equals((object)new { R = (byte)255, G = (byte)255, B = (byte)255, A = (byte)255 }).ToString();""")]
    [InlineData("""object o = new { R = (byte)255, G = (byte)255, B = (byte)255, A = (byte)255 }; object boxed = Color.White; Func<object, bool> same = Color.White.Equals; return Color.White.Equals(o) + "|" + boxed.Equals(o) + "|" + same(o) + "|" + Color.White.Equals((object)Color.White);""")]
    [InlineData("""return Curve.Standard.Equals((object)new { X1 = 0.2f, Y1 = 0f, X2 = 0f, Y2 = 1f }) + "|" + Curve.Standard.Equals((object)Curve.Standard);""")]
    public void AValueHeldAsData_IsNotAnAnonymousObject(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertVocabularyStatementsSameAsDotNet(statements);
    }
}
