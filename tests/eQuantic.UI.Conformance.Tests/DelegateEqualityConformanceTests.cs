using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Two delegates over the same target and method are equal, however they were made, so <c>==</c>, <c>Equals</c>
/// and <c>-=</c> find a handler made from the group again: each was a function of its own and compared by
/// identity (#959).
/// </summary>
public class DelegateEqualityConformanceTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        public class Box
        {
            public int N;
            public int M() => N;
            public int Twice(int x) => x * 2;
            public static int S() => 1;
            public Func<int> Mine() => M;
            public bool SameAsMine() { Func<int> a = M; Func<int> b = M; return a == b; }
        }
        public class Clicks
        {
            public event Action Clicked;
            public int Count;
            public void OnClick() { Count++; }
            public void Add() { Clicked += OnClick; }
            public void Remove() { Clicked -= OnClick; }
            public void Raise() { Clicked?.Invoke(); }
        }
        public delegate int Same(int x);
        public class Holder { public Action Run; }
        """;

    [SkippableTheory]
    // The rows of #959.
    [InlineData("Func<int, int> f = x => x; var g = new Func<int, int>(f); var h = new Func<int, int>(f); return (g == h) + \"|\" + (g == f);")]
    [InlineData("var b = new Box(); Func<int> m1 = b.M; Func<int> m2 = b.M; return (m1 == m2) + \"|\" + (m1 != m2) + \"|\" + m1.Equals(m2) + \"|\" + object.Equals(m1, m2);")]
    // Another target, another method, a static one, and from inside the class.
    [InlineData("var a = new Box(); var b = new Box(); Func<int> m1 = a.M; Func<int> m2 = b.M; return (m1 == m2).ToString();")]
    [InlineData("var b = new Box(); Func<int> m1 = b.M; Func<int, int> m2 = b.Twice; return m1.Equals(m2).ToString();")]
    [InlineData("Func<int> a = Box.S; Func<int> b = Box.S; return (a == b).ToString();")]
    [InlineData("var b = new Box(); return (b.Mine() == b.Mine()) + \"|\" + b.SameAsMine();")]
    // A delegate of a target still reads the target as it is.
    [InlineData("var b = new Box(); Func<int> m = b.M; b.N = 5; return m().ToString();")]
    // A key of a dictionary, and a member of a list.
    [InlineData("var b = new Box(); Func<int> m1 = b.M; Func<int> m2 = b.M; var l = new List<Func<int>> { m1 }; return l.Contains(m2) + \"|\" + l.Remove(m2) + \"|\" + l.Count;")]
    [InlineData("var b = new Box(); Func<int> m1 = b.M; Func<int> m2 = b.M; var d = new Dictionary<Func<int>, int> { [m1] = 1 }; return d.ContainsKey(m2).ToString();")]
    // A handler made from the group again is the one a removal finds.
    [InlineData("var c = new Clicks(); c.Add(); c.Remove(); c.Raise(); return c.Count.ToString();")]
    [InlineData("var c = new Clicks(); c.Add(); c.Add(); c.Remove(); c.Raise(); return c.Count.ToString();")]
    [InlineData("var c = new Clicks(); Action a = null; a += c.OnClick; a += c.OnClick; a -= c.OnClick; a(); return c.Count.ToString();")]
    [InlineData("var c = new Clicks(); Action a = c.OnClick; a -= c.OnClick; return (a == null).ToString();")]
    // A delegate's own + and -, which JavaScript's wrote as text, on a local and a field through a call.
    [InlineData("var c = new Clicks(); Action a = c.OnClick; Action b = a + c.OnClick; b(); var d = b - a; d(); return c.Count.ToString();")]
    [InlineData("var n = 0; var c = new Holder(); Holder Get() { n++; return c; } Get().Run += () => { }; Get().Run -= () => { }; return n.ToString();")]
    public void TwoDelegates_CompareByTargetAndMethod(string statements) =>
        ModuleGraph.AssertSameAsDotNet(Source, false, ("case", statements));
}
