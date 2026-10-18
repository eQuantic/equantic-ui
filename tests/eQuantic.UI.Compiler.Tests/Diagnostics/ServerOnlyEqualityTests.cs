using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// The default comparer's <c>Equals</c>, <c>Nullable.Equals</c> and <c>Equals</c> over a struct reach the value's
/// own equality, and one marked <c>[ServerOnly]</c> is in no twin. The comparison fell back to identity, or to the
/// members a struct's twin compares, and answered what the server's method never would: the call is refused where
/// it is written, with the one EQ2010 a call bound to the member itself meets (Copilot's third review of #977). A
/// comparison that can reach no such member is not refused. Compiled as an SDK build compiles a file
/// (<see cref="ProjectProbe"/>).
/// </summary>
public class ServerOnlyEqualityTests
{
    private const string Types = """
        using System;
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;

        public sealed class Vaulted
        {
            public Vaulted(int n) { N = n; }
            public int N { get; }
            [ServerOnly] public override bool Equals(object? o) => false;
            public override int GetHashCode() => N;
        }

        public class KeptBase
        {
            [ServerOnly] public override bool Equals(object? o) => false;
            public override int GetHashCode() => 0;
        }

        public sealed class KeptDerived : KeptBase { }

        public sealed class TypedKept : IEquatable<TypedKept>
        {
            [ServerOnly] public bool Equals(TypedKept? other) => false;
        }

        public interface IShape { }

        public class Open : IShape { }

        public sealed class Closed : Open
        {
            [ServerOnly] public override bool Equals(object? o) => false;
            public override int GetHashCode() => 0;
        }

        public sealed class Plain { }

        public readonly struct Kept
        {
            public Kept(int n) { N = n; }
            public int N { get; }
            [ServerOnly] public override bool Equals(object? o) => false;
            public override int GetHashCode() => N;
        }

        public readonly record struct KeptRecord(int N)
        {
            [ServerOnly] public bool Equals(KeptRecord other) => false;
            public override int GetHashCode() => N;
        }

        public readonly struct Free
        {
            public Free(int n) { N = n; }
            public int N { get; }
        }
        """;

    /// <summary>The types above, and one method returning <paramref name="call"/>.</summary>
    private static string Source(string signature, string call) =>
        $"{Types}\npublic static class Probe\n{{\n    public static {signature} {{ return {call}; }}\n}}\n";

    /// <summary>The signature, the call, and how its one EQ2010 begins: the member the call reaches, named as a call
    /// bound to it names it, or the contract through which it may reach one.</summary>
    public static TheoryData<string, string, string> Refused => new()
    {
        // The direct call, bound to the member itself: what every comparison below meets.
        { "object Run(Vaulted c)", "c.Equals(c)", "'Vaulted.Equals' is HOST ONLY" },
        { "object Run(Vaulted c)", "EqualityComparer<Vaulted>.Default.Equals(c, c)", "'Vaulted.Equals' is HOST ONLY" },
        { "object Run(KeptDerived d)", "EqualityComparer<KeptDerived>.Default.Equals(d, d)", "'KeptBase.Equals' is HOST ONLY" },
        { "object Run(TypedKept t)", "EqualityComparer<TypedKept>.Default.Equals(t, t)", "'TypedKept.Equals' is HOST ONLY" },
        { "object Run(Open x)", "EqualityComparer<Open>.Default.Equals(x, x)", "'object.Equals' may reach 'Closed.Equals', marked [ServerOnly]" },
        { "object Run(IShape s)", "EqualityComparer<IShape>.Default.Equals(s, s)", "'object.Equals' may reach 'Closed.Equals', marked [ServerOnly]" },
        { "object Run(object o)", "EqualityComparer<object>.Default.Equals(o, o)", "'object.Equals' may reach " },
        { "bool Run<T>(T a, T b)", "EqualityComparer<T>.Default.Equals(a, b)", "'object.Equals' may reach " },
        { "object Run(Kept v)", "v.Equals(v)", "'Kept.Equals' is HOST ONLY" },
        { "object Run(Kept? a, Kept? b)", "a.Equals(b)", "'Kept.Equals' is HOST ONLY" },
        { "object Run(Kept? a, Kept? b)", "Nullable.Equals(a, b)", "'Kept.Equals' is HOST ONLY" },
        { "object Run(Kept? a, Kept? b)", "EqualityComparer<Kept?>.Default.Equals(a, b)", "'Kept.Equals' is HOST ONLY" },
        { "object Run(Kept a, Kept b)", "EqualityComparer<Kept>.Default.Equals(a, b)", "'Kept.Equals' is HOST ONLY" },
        { "object Run(KeptRecord? a, KeptRecord? b)", "a.Equals(b)", "'KeptRecord.Equals' is HOST ONLY" },
        { "object Run(KeptRecord a, KeptRecord b)", "EqualityComparer<KeptRecord>.Default.Equals(a, b)", "'KeptRecord.Equals' is HOST ONLY" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void AComparisonThatReachesAServerOnlyEquals_IsRefusedAtTheCall(string signature, string call, string refusal)
    {
        var source = Source(signature, call);
        var probe = ProjectProbe.Compile(source).Should().ContainSingle(result => result.ComponentName == "Probe").Which;

        var error = probe.Errors.Should().ContainSingle("one call, one refusal").Which;
        error.Code.Should().Be("EQ2010");
        error.Message.Should().StartWith(refusal);
        var lines = source.Split('\n');
        var line = Array.FindIndex(lines, text => text.Contains($"return {call};", StringComparison.Ordinal));
        (error.Line, error.Column).Should().Be((line + 1, lines[line].IndexOf(call, StringComparison.Ordinal) + 1),
            "the refusal is told where the call is written");
    }

    /// <summary>The signature, the call, and what it lowers to: a comparison that can reach no <c>[ServerOnly]</c>
    /// equality, in a build that holds several.</summary>
    public static TheoryData<string, string, string> NotRefused => new()
    {
        { "object Run(Plain p)", "EqualityComparer<Plain>.Default.Equals(p, p)", "$eq.collections.sameItem(p, p)" },
        { "object Run(Free a, Free b)", "EqualityComparer<Free>.Default.Equals(a, b)", "$eq.equals(a, b)" },
        { "object Run(Free? a, Free? b)", "a.Equals(b)", "$eq.equals(a, b)" },
        { "object Run(Free? a, Free? b)", "Nullable.Equals(a, b)", "$eq.equals(a, b)" },
        { "object Run(int a, int b)", "EqualityComparer<int>.Default.Equals(a, b)", "$eq.equals(a, b)" },
    };

    [Theory]
    [MemberData(nameof(NotRefused))]
    public void AComparisonThatReachesNone_IsNotRefused(string signature, string call, string lowered)
    {
        var probe = ProjectProbe.Compile(Source(signature, call)).Should().ContainSingle(result => result.ComponentName == "Probe").Which;

        probe.Errors.Select(error => $"{error.Code} {error.Message}").Should().BeEmpty();
        probe.TypeScript.Should().Contain(lowered);
    }
}
