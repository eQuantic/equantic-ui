using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>T[]</c> behind <c>ICollection&lt;T&gt;</c> or <c>IList&lt;T&gt;</c> keeps its size: a list and an array are one
/// JavaScript array here, so a call through the interface grew and shrank it where .NET refuses the call (#711). The
/// conversion to the interface is where the static type still says which it is.
/// </summary>
public class FixedSizeArrayConformanceTests
{
    private const string Catch = "catch (NotSupportedException e) { return e.Message + \"|\" + a.Length; }";

    [SkippableTheory]
    // The rows of #711.
    [InlineData("int[] a = { 1, 2 }; try { ((ICollection<int>)a).Add(3); return \"added\"; } " + Catch)]
    [InlineData("int[] a = { 1, 2 }; try { ((ICollection<int>)a).Clear(); return \"cleared\"; } " + Catch)]
    [InlineData("int[] a = { 1, 2 }; try { ((ICollection<int>)a).Remove(1); return \"removed\"; } " + Catch)]
    [InlineData("int[] a = { 1, 2 }; try { ((IList<int>)a).Insert(0, 9); return \"inserted\"; } " + Catch)]
    [InlineData("int[] a = { 1, 2 }; try { ((IList<int>)a).RemoveAt(0); return \"removed\"; } " + Catch)]
    // An implicit conversion, at an assignment, an argument and through IList's own Add.
    [InlineData("int[] a = { 1, 2 }; IList<int> l = a; try { l.Add(3); return \"added\"; } " + Catch)]
    [InlineData("int[] a = { 1, 2 }; ICollection<int> c = a; try { c.Remove(9); return \"removed\"; } " + Catch)]
    [InlineData("int[] a = { 1, 2 }; void Grow(IList<int> l) => l.Add(3); try { Grow(a); return \"added\"; } " + Catch)]
    // Writing an element is allowed on both sides, and the array is still the array.
    [InlineData("int[] a = { 1, 2 }; IList<int> l = a; l[0] = 9; return a[0] + \"|\" + l.Count + \"|\" + l.Contains(2) + \"|\" + l.IndexOf(2);")]
    // A list behind the same faces keeps growing.
    [InlineData("var a = new List<int> { 1, 2 }; ICollection<int> c = a; c.Add(3); c.Remove(1); IList<int> l = a; l.Insert(0, 9); l.RemoveAt(1); return string.Join(\",\", a);")]
    [InlineData("var a = new List<int> { 1, 2 }; ICollection<int> c = a; c.Clear(); return a.Count.ToString();")]
    // A list made from an array is a list.
    [InlineData("int[] a = { 1 }; IList<int> f = a; var l = new List<int>(f); l.Add(2); var m = a.ToList(); m.Add(3); return l.Count + \"|\" + m.Count;")]
    // A null array passes through.
    [InlineData("int[] a = null; IList<int> l = a; return (l == null).ToString();")]
    [InlineData("int[] a = { 1 }; ICollection<int> c = a; var l = new List<int>(); ICollection<int> d = l; return c.IsReadOnly + \"|\" + d.IsReadOnly;")]
    public void AnArrayBehindAList_KeepsItsSize(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
