using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for <c>Queue&lt;T&gt;</c> (FIFO) and <c>Stack&lt;T&gt;</c> (LIFO) — the compat types.
/// Statement blocks build up a collection and return a value; results are compared against .NET. Note
/// the .NET quirk that <c>Stack.ToArray()</c> is top-first (LIFO) while <c>Queue.ToArray()</c> is
/// front-first.
/// </summary>
public class CollectionsConformanceTests
{
    [SkippableTheory]
    // Queue FIFO
    [InlineData("var q = new Queue<int>(); q.Enqueue(1); q.Enqueue(2); q.Enqueue(3); return q.Dequeue();")]      // 1
    [InlineData("var q = new Queue<int>(); q.Enqueue(1); q.Enqueue(2); return q.Count;")]                       // 2
    [InlineData("var q = new Queue<int>(); q.Enqueue(5); q.Enqueue(6); return q.Peek();")]                      // 5
    [InlineData("var q = new Queue<int>(); q.Enqueue(10); q.Enqueue(20); return string.Join(\",\", q.ToArray());")] // "10,20"
    [InlineData("var q = new Queue<string>(); q.Enqueue(\"a\"); return q.Contains(\"a\");")]                    // true
    // Stack LIFO
    [InlineData("var s = new Stack<int>(); s.Push(1); s.Push(2); s.Push(3); return s.Pop();")]                  // 3
    [InlineData("var s = new Stack<int>(); s.Push(1); s.Push(2); return s.Count;")]                            // 2
    [InlineData("var s = new Stack<int>(); s.Push(7); s.Push(8); return s.Peek();")]                            // 8
    [InlineData("var s = new Stack<int>(); s.Push(10); s.Push(20); s.Push(30); return string.Join(\",\", s.ToArray());")] // "30,20,10" (LIFO)
    [InlineData("var s = new Stack<string>(); s.Push(\"x\"); s.Push(\"y\"); return s.Contains(\"x\");")]        // true
    // Drain pattern (common real usage)
    [InlineData("var q = new Queue<int>(); for (int i = 1; i <= 3; i++) q.Enqueue(i * 10); int sum = 0; while (q.Count > 0) sum += q.Dequeue(); return sum;")] // 60
    public void Collections_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
