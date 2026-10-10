using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The runtime's collection classes write the JSON System.Text.Json writes for them: an array, in the
/// order each enumerates (#597). With no <c>toJSON</c>, <c>JSON.stringify</c> wrote a queue, a stack
/// and a sorted set as their fields and a linked list as its node graph, so a Server Action argument
/// reached the server as an object no collection is read from. The harness compares the JSON each side
/// prints, so a returned collection is the proof.
/// </summary>
public class CollectionJsonConformanceTests
{
    [SkippableTheory]
    [InlineData("new Queue<long>(new[] { 1L, 2L })")]                                    // ["1","2"]: a long as a BigInt's digits
    [InlineData("new Queue<int>()")]                                                     // []
    [InlineData("new Stack<int>(new[] { 1, 2, 3 })")]                                    // [3,2,1]: from the top, as it enumerates
    [InlineData("new LinkedList<string>(new[] { \"a\", \"b\" })")]                       // ["a","b"]
    [InlineData("new SortedSet<int> { 3, 1, 2 }")]                                       // [1,2,3]
    [InlineData("new Dictionary<string, Queue<int>> { [\"a\"] = new Queue<int>(new[] { 1 }) }")] // {"a":[1]}: nested
    [InlineData("new List<Stack<int>> { new Stack<int>(new[] { 1, 2 }) }")]              // [[2,1]]
    public void ACollection_IsWrittenAsDotNetWritesIt(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    /// <summary>What a collection holds after it changed, not what it was built with.</summary>
    [SkippableTheory]
    [InlineData("var q = new Queue<int>(); q.Enqueue(1); q.Enqueue(2); q.Dequeue(); q.Enqueue(3); return q;")] // [2,3]
    [InlineData("var s = new Stack<int>(); s.Push(1); s.Push(2); s.Pop(); s.Push(3); return s;")]              // [3,1]
    [InlineData("var l = new LinkedList<int>(); l.AddLast(2); l.AddFirst(1); l.AddLast(3); l.RemoveFirst(); return l;")] // [2,3]
    [InlineData("var s = new SortedSet<string> { \"b\", \"a\" }; s.Remove(\"b\"); s.Add(\"c\"); return s;")]   // ["a","c"]
    public void AChangedCollection_IsWrittenAsDotNetWritesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
