using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A static generic class keeps one set of statics per closed type, as C# does (#1855): <c>Cache&lt;int&gt;</c> and
/// <c>Cache&lt;string&gt;</c> hold their own fields and each runs its static constructor on its own first use. The twin
/// is one class for every type argument, so every closed type shared one set: the second closure read the first's
/// state, and its static constructor never ran.
/// </summary>
public class StaticGenericClassConformanceTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;

        public static class Log { public static string Text = ""; }

        public static class Cache<T>
        {
            public static T? Last;
            public static int Hits { get; private set; }
            public static readonly List<T> All = new();
            static Cache() { Log.Text += "init "; }
            public static T Keep(T value) { Last = value; All.Add(value); Hits++; return value; }
            public static Func<T, T> Keeper => Keep;
            public static int Count => All.Count;
            public static T Twice(T value) { Func<T, T> again = v => Keep(v); Keep(value); return again(value); }
        }

        public static class Plain<T>
        {
            public static int N;
            public static int Count { get; private set; }
            public static int Bump() { Count += 2; return ++N; }
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("two closed types hold their own statics",
            "Cache<int>.Keep(1); Cache<int>.Keep(2); Cache<string>.Keep(\"a\"); return Cache<int>.Hits + \"|\" + Cache<string>.Hits + \"|\" + Cache<int>.Last + \"|\" + Cache<string>.Last;"), // "2|1|2|a"
        ("each closed type runs its static constructor once, on its own first use",
            "Log.Text = \"\"; Cache<int>.Keep(1); Cache<string>.Keep(\"a\"); Cache<int>.Keep(3); return Log.Text + Cache<int>.Count;"), // "init init 2"
        ("a closed type never used is at its zeros",
            "Cache<int>.Keep(7); return Cache<long>.Hits + \"|\" + Cache<long>.Count + \"|\" + (Cache<string>.Last == null);"), // "0|0|True"
        ("a static method group is its closed type's",
            "var keep = Cache<int>.Keeper; keep(5); keep(6); return Cache<int>.Count + \"|\" + Cache<double>.Count;"), // "2|0"
        ("a static method calls its siblings, a lambda included, on its own closed type",
            "Cache<string>.Twice(\"x\"); return Cache<string>.Hits + \"|\" + Cache<int>.Hits;"), // "2|0"
        ("a static with no initializer is per closed type too",
            "Plain<int>.Bump(); Plain<int>.Bump(); Plain<string>.Bump(); return Plain<int>.N + \"|\" + Plain<string>.N + \"|\" + Plain<bool>.N;"), // "2|1|0"
        ("an auto-property with no initializer is per closed type too",
            "Plain<int>.Bump(); Plain<string>.Bump(); Plain<string>.Bump(); return Plain<int>.Count + \"|\" + Plain<string>.Count + \"|\" + Plain<bool>.Count;"), // "2|4|0"
        ("a closed type over a closed generic is its own",
            "Cache<List<int>>.Keep(new List<int> { 1 }); return Cache<List<int>>.Count + \"|\" + Cache<List<long>>.Count;"), // "1|0"
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AStaticGenericClass_KeepsItsStaticsPerClosedType(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
