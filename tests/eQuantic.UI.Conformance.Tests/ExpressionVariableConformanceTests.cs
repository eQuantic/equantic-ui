using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The variables a C# expression declares — a pattern's bindings, an <c>out var</c>, a
/// deconstruction's elements — are declared by the statement that holds them, with the scope Roslyn
/// gives them (#466). The harness used to put a <c>let</c> for every <c>out var</c> at the top of
/// the block, which is what the emitter did for a method and nothing else did, so these ran here
/// while a getter, a loop and a deconstruction diverged in an app. It declares nothing now.
/// </summary>
public class ExpressionVariableConformanceTests
{
    /// <summary>A record the using cases can dispose: the harness emits records from the prelude.</summary>
    private const string Disposable = "record Res(int V) : IDisposable { public void Dispose() { } }";

    /// <summary>
    /// .NET gives a loop's variables a fresh copy every time round — a foreach body's, and a while's
    /// or a for's condition's — and a local function or a lambda a fresh one on every call. One
    /// <c>let</c> at the top of the method was one slot for all of them: the closures all read the
    /// last value, and a recursive call overwrote its caller's.
    /// </summary>
    [SkippableTheory]
    [InlineData("var reads = new List<Func<int>>(); foreach (var s in new[] { \"1\", \"2\" }) { int.TryParse(s, out var n); reads.Add(() => n); } var a = reads[0]; var b = reads[1]; return a() * 10 + b();")]
    [InlineData("var reads = new List<Func<int>>(); var i = 0; while (int.TryParse(i < 2 ? (i + 1).ToString() : \"x\", out var n)) { reads.Add(() => n); i++; } var a = reads[0]; var b = reads[1]; return a() * 10 + b();")]
    [InlineData("var reads = new List<Func<int>>(); for (var i = 0; i < 2 && int.TryParse((i + 1).ToString(), out var n); i++) reads.Add(() => n); var a = reads[0]; var b = reads[1]; return a() * 10 + b();")]
    [InlineData("var reads = new List<Func<int>>(); foreach (var o in new object[] { 1, 2 }) { if (o is int k) reads.Add(() => k); } var a = reads[0]; var b = reads[1]; return a() * 10 + b();")]
    [InlineData("int Digits(int depth) { int.TryParse(depth.ToString(), out var d); var rest = depth > 0 ? Digits(depth - 1) : 0; return d * 10 + rest; } return Digits(2);")]
    [InlineData("Func<string, int> parse = t => int.TryParse(t, out var n) ? n : -1; return parse(\"4\") + parse(\"x\");")]
    [InlineData("var reads = new List<Func<int>>(); var i = 0; do { i++; } while (int.TryParse(i < 3 ? i.ToString() : \"x\", out var z) && Keep(reads, () => z)); var a = reads[0]; var b = reads[1]; return a() * 10 + b(); static bool Keep(List<Func<int>> list, Func<int> read) { list.Add(read); return true; }")]
    public void OneVariablePerIterationAndPerCall_AsDotNetGivesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>Every statement that can hold one declares it: the ones whose variables live on in
    /// the enclosing block in front of themselves, a loop or a using inside itself, so that two
    /// siblings may repeat a name as C# allows.</summary>
    [SkippableTheory]
    // A deconstruction's elements, mixed, nested and discarded.
    [InlineData("(var c, var d) = (3, 4); return c + d;")]
    [InlineData("int d; (var c, d) = (3, 4); return c * 10 + d;")]
    [InlineData("(var a, (var b, var c)) = (1, (2, 3)); return a * 100 + b * 10 + c;")]
    [InlineData("var (a, (b, c)) = (1, (2, 3)); return a * 100 + b * 10 + c;")]
    [InlineData("var (_, _, z) = (1, 2, 3); return z;")]
    [InlineData("var sum = 0; foreach (var (_, _, z) in new[] { (1, 2, 3), (4, 5, 6) }) sum += z; return sum;")]
    // Loop heads, and two sibling loops binding the same name.
    [InlineData("object[] xs = { 1, 2, \"x\", 4 }; var sum = 0; for (var i = 0; i < xs.Length && xs[i] is int n; i++) sum += n; return sum;")]
    [InlineData("int i; var sum = 0; for (i = 0; i < 3 && int.TryParse((i + 1).ToString(), out var v); i++) sum += v; return sum * 10 + i;")]
    [InlineData("var sum = 0; for (var (i, j) = (0, 3); i < j && int.TryParse(\"1\", out var n); i++) sum += n; return sum;")]
    [InlineData("var i = 0; do { i++; } while (int.TryParse(i < 3 ? \"1\" : \"x\", out var z) && z > 0); return i;")]
    [InlineData("var total = 0; var i = 0; while (int.TryParse(i < 2 ? \"1\" : \"x\", out var n)) { total += n; i++; } i = 0; while (int.TryParse(i < 3 ? \"2\" : \"x\", out var n)) { total += n; i++; } return total;")]
    [InlineData("var sum = 0; foreach (var c in (int.TryParse(\"12\", out var n) ? n : 0).ToString()) sum += c - '0'; foreach (var c in (int.TryParse(\"34\", out var n) ? n : 0).ToString()) sum += c - '0'; return sum;")]
    // A labeled loop keeps its label on the loop, inside the block that declares its head's variables.
    [InlineData("var sum = 0; outer: foreach (var c in (int.TryParse(\"123\", out var n) ? n : 0).ToString()) { if (c == '2') continue outer; sum += c - '0'; } return sum;")]
    [InlineData("var i = 0; var hits = 0; outer: do { i++; if (i == 2) continue outer; hits++; } while (int.TryParse(i < 4 ? \"1\" : \"x\", out var z) && z > 0); return hits * 10 + i;")]
    // A switch's governing expression lives on after it; a guard and an arm are their own scopes.
    [InlineData("switch (int.TryParse(\"7\", out var q) ? q : -1) { case 7: break; default: q = 0; break; } return q;")]
    [InlineData("object o = \"9\"; switch (o) { case string s when int.TryParse(s, out var g): return g; default: return -1; }")]
    // A section's statements declare into the switch block, which a later section assigns and reads.
    [InlineData("var k = 2; switch (k) { case 1: int.TryParse(\"5\", out var a); return a; case 2: a = 7; return a; default: return 0; }")]
    [InlineData("object o = 2; switch (o) { case int x when x == 1: int.TryParse(\"5\", out var a); return a; default: a = 7; return a; }")]
    [InlineData("object o = 2; return o switch { 1 => int.TryParse(\"5\", out var n) ? n : 0, _ => int.TryParse(\"6\", out var n) ? n : 0 };")]
    [InlineData("object o = \"xy\"; return o switch { int => o is int k ? k : 0, _ => o is string k ? k.Length : -1 };")]
    // The other statements an expression can sit in.
    [InlineData("try { throw new InvalidOperationException(int.TryParse(\"12\", out var t) ? \"n\" + t : \"bad\"); } catch (InvalidOperationException e) { return e.Message; }")]
    [InlineData("var gate = \"gate\"; lock (int.TryParse(\"4\", out var held) ? gate : gate) { return held; }")]
    [InlineData("using (var r = new Res(int.TryParse(\"6\", out var u) ? u : 0)) { return r.V + u; }", Disposable)]
    [InlineData("var total = 0; if (total == 0) total += int.TryParse(\"7\", out var v) ? v : 0; if (total == 7) total += int.TryParse(\"7\", out var v) ? v : 0; return total;")]
    [InlineData("if (!int.TryParse(\"7\", out var r)) return -1; return r;")]
    // A query's clauses are arrows of their own, so two queries in one block may bind one name.
    [InlineData("var xs = new[] { \"1\", \"x\" }; var q1 = (from s in xs where int.TryParse(s, out var n) select s).Count(); var q2 = (from s in xs where int.TryParse(s, out var n) && n > 0 select s).Count(); return q1 * 10 + q2;")]
    // A list pattern's own designation is assigned as well as declared.
    [InlineData("int[] a = { 1, 2 }; if (a is [1, _] whole) return whole.Length; return 0;")]
    public void EveryStatementDeclaresWhatItsExpressionsDeclare(string statements, string prelude = "")
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }

    /// <summary>
    /// C#'s verbatim escape makes a keyword a name — <c>@class</c>, <c>@new</c>, <c>@default</c> —
    /// and JavaScript refuses most of them as a binding. The rename (<c>class_</c>) must reach the
    /// declaration and every reference, whichever way the name was bound: a pattern wrote
    /// <c>@class = o</c> beside a declaration of <c>class</c>, and a deconstruction and a for kept
    /// the escape. <c>undefined</c> is no keyword, but the lowerings compare against it.
    /// </summary>
    [SkippableTheory]
    [InlineData("var @class = 5; return @class;")]
    [InlineData("int.TryParse(\"7\", out var @class); return @class;")]
    [InlineData("object o = 3; if (o is int @class) return @class; return 0;")]
    [InlineData("object o = \"ab\"; return o is string { Length: var @new } ? @new : 0;")]
    [InlineData("var (@if, @do) = (1, 2); return @if * 10 + @do;")]
    [InlineData("(var @if, var @do) = (1, 2); return @if * 10 + @do;")]
    [InlineData("var sum = 0; foreach (var @default in new[] { 1, 2 }) sum += @default; return sum;")]
    [InlineData("var sum = 0; foreach (var (@in, @of) in new[] { (1, 2), (3, 4) }) sum += @in * @of; return sum;")]
    [InlineData("var sum = 0; for (var @var = 0; @var < 3; @var++) sum += @var; return sum;")]
    [InlineData("try { throw new Exception(\"boom\"); } catch (Exception @catch) { return @catch.Message; }")]
    [InlineData("Func<int, int> twice = @this => @this * 2; return twice(4);")]
    [InlineData("int @switch(int @case) => @case + 1; return @switch(2);")]
    [InlineData("int Delete(int from) => from - 1; return Delete(3);")]
    [InlineData("Func<int> @default = () => 5; return @default();")]
    [InlineData("return (from @class in new[] { 1, 2, 3 } where @class > 1 select @class * 2).Sum();")]
    [InlineData("int[] a = { 1, 2 }; if (a is [1, _] @while) return @while.Length; return 0;")]
    [InlineData("using (var @using = new Res(4)) { return @using.V; }", Disposable)]
    [InlineData("var undefined = 5; int.TryParse(\"x\", out var n); return n + undefined;")]
    public void ANameJavaScriptReserves_IsOneNameOnEveryPath(string statements, string prelude = "")
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, prelude);
    }
}
