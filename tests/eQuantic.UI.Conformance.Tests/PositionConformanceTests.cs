using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A position in a sequence is read once, where C# reads it, and refused where .NET refuses it: an
/// array's, a list's or a string's element counted from the end (#394), and a list's <c>Insert</c>,
/// <c>RemoveAt</c>, <c>RemoveRange</c> and <c>GetRange</c> (#1160).
/// </summary>
public class PositionConformanceTests
{
    /// <summary>
    /// <c>a[^n]</c> over an array, a list or a string evaluates its receiver once, then its offset, then
    /// the length, and its offset is one value whatever it is written as. It was text that named the
    /// receiver twice, so <c>Get()[^1]</c> and every write through it called <c>Get</c> twice, and a
    /// deconstruction's target and a null-conditional write were refused (#394). An offset written as a
    /// sum already answered right, since the parentheses its author wrote survived the text: those cases
    /// are guards.
    /// </summary>
    [SkippableTheory]
    // A read, over an array, a list and a string, each made by a call that counts itself.
    [InlineData("var calls = 0; int[] Get() { calls++; return new[] { 1, 2, 3 }; } var x = Get()[^1]; return x + \"|\" + calls;")]                        // 3|1
    [InlineData("var calls = 0; List<int> Get() { calls++; return new List<int> { 1, 2, 3 }; } return Get()[^2] + \"|\" + calls;")]                     // 2|1
    [InlineData("var calls = 0; string Get() { calls++; return \"abc\"; } return Get()[^1] + \"|\" + calls;")]                                          // c|1
    // An offset written as a sum, a read and a write.
    [InlineData("var a = new[] { 1, 2, 3, 4 }; int i = 1; return a[^(i + 1)].ToString();")]                                                              // 3
    [InlineData("var a = new[] { 1, 2, 3, 4 }; int i = 1; a[^(i + 1)] = 7; return string.Join(\",\", a);")]                                            // 1,2,7,4
    [InlineData("var l = new List<int> { 1, 2, 3, 4 }; int i = 2; l[^(i - 1)] += 10; return string.Join(\",\", l);")]                                   // 1,2,3,14
    // A write, a compound and a step through a receiver made by a call, which runs once each.
    [InlineData("var calls = 0; int[] arr = { 1, 2, 3 }; int[] Get() { calls++; return arr; } Get()[^1] = 9; return arr[2] + \"|\" + calls;")]          // 9|1
    [InlineData("var calls = 0; int[] arr = { 1, 2, 3 }; int[] Get() { calls++; return arr; } Get()[^1] += 5; return arr[2] + \"|\" + calls;")]         // 8|1
    [InlineData("var calls = 0; int[] arr = { 1, 2, 3 }; int[] Get() { calls++; return arr; } var old = Get()[^2]++; return old + \"|\" + arr[1] + \"|\" + calls;")] // 2|3|1
    [InlineData("var calls = 0; var l = new List<int> { 1, 2, 3 }; List<int> Get() { calls++; return l; } Get()[^1] *= 2; return l[2] + \"|\" + calls;")] // 6|1
    // An offset with an effect is evaluated once each time, after the receiver, before the length.
    [InlineData("var a = new[] { 1, 2, 3 }; int n = 0; int Next() => ++n; return a[^Next()] + \"|\" + a[^Next()];")]                                     // 3|2
    [InlineData("var a = new[] { 1, 2, 3 }; var b = new[] { 7, 8 }; int Swap() { a = b; return 1; } return a[^Swap()].ToString();")]                     // 3
    [InlineData("var a = new[] { 1, 2, 3 }; var first = a; var b = new[] { 7, 8 }; int Swap() { a = b; return 1; } a[^Swap()] = 0; return string.Join(\",\", first) + \"|\" + string.Join(\",\", b);")] // 1,2,0|7,8
    // A write answers the value written, a deconstruction swaps two ends, and a width or a precision holds.
    [InlineData("var a = new[] { 1, 2, 3 }; var y = (a[^1] = 6); return y + \"|\" + a[2];")]                                                             // 6|6
    [InlineData("var a = new[] { 1, 2, 3 }; (a[^1], a[0]) = (a[0], a[^1]); return string.Join(\",\", a);")]                                              // 3,2,1
    [InlineData("var a = new long[] { 1, 2 }; a[^1] += 3; return (a[1] * 2).ToString();")]                                                               // 10
    [InlineData("var b = new byte[] { 1, 255 }; b[^1]++; return b[1].ToString();")]                                                                      // 0
    [InlineData("var f = new float[] { 0.1f }; f[^1] += 0.2f; return f[0].ToString();")]                                                                 // 0.3
    // A null-conditional read from the end answers null for a null receiver.
    [InlineData("int[] none = null; int[] some = { 4, 5 }; return (none?[^1] ?? -1) + \"|\" + (some?[^1] ?? -1);")]                                     // -1|5
    // A null-conditional write from the end writes behind the guard, its receiver made once.
    [InlineData("var calls = 0; int[] arr = { 1, 2 }; int[] Get() { calls++; return arr; } int[] none = null; Get()?[^1] = 9; none?[^1] = 7; return string.Join(\",\", arr) + \"|\" + calls;")] // 1,9|1
    // An out argument is held before the call, and written back through the place, not its read.
    [InlineData("var calls = 0; int[] arr = { 1, 2 }; int[] Get() { calls++; return arr; } void Set(out int v) { v = 9; } Set(out Get()[^1]); return string.Join(\",\", arr) + \"|\" + calls;")] // 1,9|1
    [InlineData("var calls = 0; int[] arr = { 1, 2 }; int[] Get() { calls++; return arr; } var ok = int.TryParse(\"7\", out Get()[^1]); return ok + \"|\" + string.Join(\",\", arr) + \"|\" + calls;")] // True|1,7|1
    [InlineData("var calls = 0; int[] arr = { 1, 2 }; int[] Get() { calls++; return arr; } var ok = int.TryParse(\"x\", out Get()[^2]); return ok + \"|\" + string.Join(\",\", arr) + \"|\" + calls;")] // False|0,2|1
    [InlineData("int[] arr = { 1, 2 }; void Twice(ref int v) { v *= 2; } Twice(ref arr[^1]); return string.Join(\",\", arr);")] // 1,4
    // An await stays in the async function around the access, never in an arrow of the writer's.
    [InlineData("int[] arr = { 1, 2, 3 }; int[] Get() => arr; async Task<int> Offset() { await Task.Yield(); return 2; } return Get()[^(await Offset())].ToString();")] // 2
    [InlineData("var calls = 0; int[] arr = { 1, 2 }; int[] Get() { calls++; return arr; } async Task<int> F() { await Task.Yield(); return 9; } Get()[^1] = await F(); return string.Join(\",\", arr) + \"|\" + calls;")] // 1,9|1
    [InlineData("var calls = 0; int[] arr = { 1, 2 }; int[] Get() { calls++; return arr; } async Task<int> F() { await Task.Yield(); return 9; } Get()[^1] += await F(); return string.Join(\",\", arr) + \"|\" + calls;")] // 1,11|1
    // Its value is skipped where nothing is written, and a compound reads its target before the await.
    [InlineData("var asked = 0; int?[] arr = { 1 }; int?[] Get() => arr; async Task<int> F() { asked++; await Task.Yield(); return 9; } Get()[^1] ??= await F(); return arr[0] + \"|\" + asked;")] // 1|0
    [InlineData("var asked = 0; int?[] arr = { null }; int?[] Get() => arr; async Task<int> F() { asked++; await Task.Yield(); return 9; } Get()[^1] ??= await F(); return arr[0] + \"|\" + asked;")] // 9|1
    [InlineData("int[] arr = { 1 }; int[] Get() => arr; async Task<int> F() { arr[0] = 10; await Task.Yield(); return 2; } Get()[^1] += await F(); return arr[0].ToString();")] // 3
    // A null-conditional write that awaits skips its value for a null receiver.
    [InlineData("var asked = 0; int[] arr = { 1, 2 }; int[] Get() => arr; async Task<int> F() { asked++; await Task.Yield(); return 9; } Get()?[^1] = await F(); return string.Join(\",\", arr) + \"|\" + asked;")] // 1,9|1
    [InlineData("var asked = 0; int[] none = null; int[] Get() => none; async Task<int> F() { asked++; await Task.Yield(); return 9; } Get()?[^1] = await F(); return asked.ToString();")] // 0
    public void AnElementCountedFromTheEnd_ReadsItsReceiverOnce(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// An <c>out</c> target counted from the end is taken before the call, which is C#'s place for it only
    /// where nothing ahead of it runs code, so the build refuses the rest: a preceding argument could have
    /// replaced the array, and the element would be written to the old one.
    /// </summary>
    [SkippableTheory]
    [InlineData("int[] a = { 1 }; int[] b = { 2 }; int Swap() { a = b; return 0; } void Put(int x, out int v) { v = 9; } Put(Swap(), out a[^1]); return a[0].ToString();")]
    [InlineData("int[] a = { 1 }; string Say() => \"x\"; void Put(string x, out int v) { v = 9; } Put(Say(), out a[^1]); return a[0].ToString();")]
    public void AnOutTargetFromTheEndAfterCode_IsRefused(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsRefusedByTheBuild(statements, "EQ1004");
    }

    /// <summary>
    /// A list's <c>Insert</c>, <c>RemoveAt</c>, <c>RemoveRange</c> and <c>GetRange</c> take each argument in
    /// its parameter's place, evaluated in the order it is written, and refuse a position .NET refuses in
    /// its words. They were text that placed a named argument by its position in the call, so
    /// <c>Insert(item: 9, index: 0)</c> inserted 0 at 9, and a <c>splice</c> or a <c>slice</c> clamped a
    /// position past the end and counted a negative one from it (#1160).
    /// </summary>
    [SkippableTheory]
    // The rows of #1160: a named argument in its parameter's place.
    [InlineData("var l = new List<int> { 1, 2 }; l.Insert(item: 9, index: 0); return string.Join(\",\", l);")]                                          // 9,1,2
    [InlineData("var l = new List<int> { 1, 2, 3 }; l.RemoveRange(count: 1, index: 0); return string.Join(\",\", l);")]                                // 2,3
    [InlineData("var l = new List<int> { 1, 2, 3 }; return string.Join(\",\", l.GetRange(count: 2, index: 1));")]                                      // 2,3
    // Named and out of order, the arguments are evaluated as they are written.
    [InlineData("var log = \"\"; int At(string s, int v) { log += s; return v; } var l = new List<int> { 1, 2 }; l.Insert(item: At(\"i\", 9), index: At(\"x\", 0)); return log + \"|\" + string.Join(\",\", l);")] // ix|9,1,2
    [InlineData("var log = \"\"; int At(string s, int v) { log += s; return v; } var l = new List<int> { 1, 2, 3 }; var r = l.GetRange(count: At(\"c\", 2), index: At(\"x\", 0)); return log + \"|\" + string.Join(\",\", r);")] // cx|1,2
    // GetRange reads its index once.
    [InlineData("var l = new List<int> { 1, 2, 3 }; int i = 0; var r = l.GetRange(i++, 2); return i + \"|\" + string.Join(\",\", r);")]               // 1|1,2
    // The positions .NET takes: the count itself for Insert, an empty range at the end.
    [InlineData("var l = new List<int> { 1 }; l.Insert(1, 9); return string.Join(\",\", l);")]                                                         // 1,9
    [InlineData("var l = new List<int> { 1, 2 }; l.RemoveRange(2, 0); var r = l.GetRange(2, 0); return string.Join(\",\", l) + \"|\" + r.Count;")]     // 1,2|0
    // The positions it refuses, in its words and with its exception's type.
    [InlineData("var l = new List<int> { 1 }; try { l.Insert(5, 9); return \"inserted \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1 }; try { l.Insert(-1, 9); return \"inserted \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; try { l.RemoveAt(5); return \"removed \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; try { l.RemoveAt(3); return \"removed \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2, 3 }; try { l.RemoveAt(-1); return \"removed \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2 }; try { l.RemoveRange(1, 5); return \"removed \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException) { return \"out of range\"; } catch (ArgumentException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2 }; try { l.RemoveRange(-1, 1); return \"removed \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1, 2 }; try { l.RemoveRange(0, -1); return \"removed \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1 }; try { return string.Join(\",\", l.GetRange(0, 5)); } catch (ArgumentOutOfRangeException) { return \"out of range\"; } catch (ArgumentException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1 }; try { return string.Join(\",\", l.GetRange(-1, 1)); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("var l = new List<int> { 1 }; try { return string.Join(\",\", l.GetRange(0, -1)); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    // Through the list's face, the same checks.
    [InlineData("IList<int> l = new List<int> { 1 }; try { l.Insert(3, 9); return \"inserted\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("IList<int> l = new List<int> { 1, 2 }; l.Insert(index: 1, item: 5); l.RemoveAt(0); try { l.RemoveAt(2); return \"removed\"; } catch (ArgumentOutOfRangeException e) { return string.Join(\",\", l) + \"|\" + e.Message; }")]
    // A null list throws a NullReferenceException before its arguments are checked.
    [InlineData("List<int> l = null; try { l.RemoveRange(-1, 0); return \"removed\"; } catch (NullReferenceException e) { return e.Message; }")]
    public void AListsPositions_AreTakenAndCheckedAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
