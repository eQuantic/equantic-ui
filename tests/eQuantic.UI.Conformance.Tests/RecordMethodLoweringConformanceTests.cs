using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A record's and a struct's members lower as a class's do (#432). The record and struct emitter
/// lowered a method with a copy of the class emitter's lowering that handled none of an async
/// method, an iterator, an out or ref parameter, or the variable an expression body's pattern binds:
/// the first two wrote a module that does not parse, and the others a method that throws. Each shape
/// is held in both of its forms, an expression body and a block, for a record's own member, a
/// struct's, and a default an interface supplies, because a case written in one form proves one.
/// </summary>
public class RecordMethodLoweringConformanceTests
{
    [SkippableTheory]
    // An async method: `await` outside an async function, and the module failed to parse.
    [InlineData(
        "public record R(int V) { public async Task<int> Own() => await Task.FromResult(V); "
        + "public async Task<int> OwnBlock() { await Task.Yield(); return V * 2; } }",
        "var r = new R(3); return $\"{await r.Own()}|{await r.OwnBlock()}\";")]
    [InlineData(
        "public struct S { public int V; public S(int v) { V = v; } "
        + "public async Task<int> Own() => await Task.FromResult(V); "
        + "public async Task<int> OwnBlock() { await Task.Yield(); return V + 1; } }",
        "var s = new S(4); return $\"{await s.Own()}|{await s.OwnBlock()}\";")]
    [InlineData(
        "public interface IGet { async Task<int> Get() => await Task.FromResult(7); "
        + "async Task<int> GetBlock() { await Task.Yield(); return 8; } } public record RG(int V) : IGet;",
        "IGet g = new RG(1); return $\"{await g.Get()}|{await g.GetBlock()}\";")]
    // An iterator: `yield` outside a generator, the same parse failure.
    [InlineData(
        "public record R(int V) { public IEnumerable<int> Mine() { yield return V; yield return V + 1; } }",
        "return string.Join(\",\", new R(3).Mine());")]
    [InlineData(
        "public struct S { public int V; public S(int v) { V = v; } "
        + "public IEnumerable<int> Mine() { for (var i = 0; i < V; i++) yield return i; } }",
        "return string.Join(\",\", new S(3).Mine());")]
    [InlineData(
        "public interface IItems { IEnumerable<int> Items() { yield return 1; yield return 2; } } "
        + "public record RI(int V) : IItems;",
        "IItems i = new RI(1); return string.Join(\",\", i.Items());")]
    // An out and a ref parameter, which stayed in the signature and never came back.
    [InlineData(
        "public record R(int V) { public bool TryHalf(out int half) { half = V / 2; return V % 2 == 0; } "
        + "public bool TryParse(string t, out int n) => int.TryParse(t, out n); }",
        "var r = new R(6); var a = r.TryHalf(out var h); var b = r.TryParse(\"41\", out var n); return $\"{a}{h}{b}{n}\";")]
    [InlineData(
        "public struct S { public int V; public S(int v) { V = v; } "
        + "public void Bump(ref int x) { x += V; } public void Twice(ref int x) => x *= 2; }",
        "var s = new S(5); var a = 1; s.Bump(ref a); s.Twice(ref a); return a.ToString();")]
    // The variable an expression body's pattern binds, read before anything declared it; the block
    // form declared it, and each row reads both forms or sits beside a row with the other.
    [InlineData(
        "public struct SE { public int V; public SE(int v) { V = v; } "
        + "public override bool Equals(object o) => o is SE m && m.V == V; public override int GetHashCode() => V; }",
        "return new SE(1).Equals(new SE(1)).ToString() + new SE(1).Equals(new SE(2));")]
    [InlineData(
        "public struct SB { public int V; public SB(int v) { V = v; } "
        + "public override bool Equals(object o) { return o is SB m && m.V == V; } public override int GetHashCode() => V; }",
        "return new SB(1).Equals(new SB(1)).ToString() + new SB(1).Equals(new SB(2));")]
    [InlineData(
        "public struct SS { public int V; public SS(int v) { V = v; } public bool Same(object o) => o is SS m && m.V == V; }",
        "return new SS(1).Same(new SS(1)).ToString() + new SS(1).Same(new SS(2));")]
    // Every member kind of a record: a method, a computed property, and an operator.
    [InlineData(
        "public record RS(int V) { public bool Same(object o) => o is RS m && m.V == V; "
        + "public bool Twin => this is { V: var v } && v == V; "
        + "public bool Odd { get { return this is { V: var v } && v % 2 == 1; } } "
        + "public static bool operator <(RS a, object b) => b is RS m && a.V < m.V; "
        + "public static bool operator >(RS a, object b) { return b is RS m && a.V > m.V; } }",
        "var r = new RS(1); return $\"{r.Same(new RS(1))}{r.Twin}{r.Odd}{r < new RS(2)}{r > new RS(2)}\";")]
    // A structural comparison delegates to the twin's own equals, so it threw the same way.
    [InlineData(
        "public struct SE { public int V; public SE(int v) { V = v; } "
        + "public override bool Equals(object o) => o is SE m && m.V == V; public override int GetHashCode() => V; }",
        "var xs = new List<SE> { new SE(1), new SE(2) }; var removed = xs.Remove(new SE(1)); "
        + "return $\"{removed}{xs.Count}{xs.Contains(new SE(2))}\";")]
    // A parameter is declared by its JavaScript name, as every reference names it: a reserved word
    // takes its underscore at the declaration too, where the copy declared `package` itself.
    [InlineData(
        "public record R(int V) { public int Plus(int package) => package + V; public int Times(int @class) { return @class * V; } }",
        "var r = new R(2); return $\"{r.Plus(3)}|{r.Times(4)}\";")]
    // So is a variable a pattern binds, declared by the name its uses have (found in review, #464).
    [InlineData(
        "public record R(int V) { public bool Same(object o) => o is R package && package.V == V; "
        + "public bool Twin(object o) => o is R { V: var @class } && @class == V; }",
        "var r = new R(2); return $\"{r.Same(new R(2))}{r.Twin(new R(3))}\";")]
    // A generic method runs as plain JavaScript, where a type parameter would be a syntax error.
    [InlineData(
        "public record R(int V) { public T Echo<T>(T x) => x; }",
        "return new R(1).Echo(\"hi\");")]
    // Found in review (#432): whether a method is async is asked of its return type's symbol, where
    // the name alone made a method returning a type called TaskItem async. (The harness emits a
    // prelude's records only; the class and component paths are held in the compiler suite.)
    [InlineData(
        "public record TaskItem(int N); public record R(int V) { public TaskItem First() => new TaskItem(V); "
        + "public TaskItem Next(int d) { return new TaskItem(V + d); } }",
        "var r = new R(4); return $\"{r.First().N}|{r.Next(2).N}\";")]
    // A getter that yields fills its buffer, where it wrote `yield` outside a generator.
    [InlineData(
        "public record R(int V) { public IEnumerable<int> Items { get { yield return V; yield return V * 2; } } }",
        "return string.Join(\",\", new R(3).Items);")]
    // A conversion and a setter bind a pattern's variable too, in both forms.
    [InlineData(
        "public record RC(int V) { public static implicit operator int(RC r) => r is { V: var v } ? v * 10 : 0; "
        + "public static explicit operator RC(string s) { return s is { Length: var n } ? new RC(n) : new RC(0); } "
        + "public int Seen { get; private set; } "
        + "public object Last { set => Seen = value is int n ? n : -1; } "
        + "public object First { set { Seen = value is string t ? t.Length : -2; } } }",
        "var r = new RC(2); int i = r; var e = (RC)\"abc\"; r.Last = 7; var a = r.Seen; r.First = \"hello\"; "
        + "return $\"{i}|{e.V}|{a}|{r.Seen}\";")]
    // A parameter the body never mentions takes the underscore, beside one named with it already.
    [InlineData(
        "public record R(int V) { public int Pick(int x, int _x) => V; public int Both(int y, int _y) => _y + V; }",
        "var r = new R(1); return $\"{r.Pick(5, 6)}|{r.Both(2, 3)}\";")]
    public void ARecordsMember_RunsAsAClassMemberDoes(string prelude, string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }
}
