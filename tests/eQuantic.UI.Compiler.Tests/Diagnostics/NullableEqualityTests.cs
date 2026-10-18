using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// Equals over a nullable struct, <c>Nullable.Equals</c> and <c>EqualityComparer&lt;T&gt;.Default.Equals</c>
/// build, where each was EQ2004 (#968), and lower to the comparison the default comparer of the value's
/// type is, which finds two empty values equal and an empty one unequal to any value. What the browser
/// answers with it is the conformance suite's (<c>NullableStructConformanceTests</c>). The comparer's
/// <c>Default</c> read on its own is still refused: no value the browser holds carries it, so only the call
/// made on it lowers. Compiled as an SDK build compiles a file (<see cref="ProjectProbe"/>).
/// </summary>
public class NullableEqualityTests
{
    private static string Source(string body) => $$"""
        using System;
        using System.Collections.Generic;

        public readonly record struct Spot(int Row, int Col);

        public static class Probe
        {
            public static object Same(Spot? a, Spot? b)
            {
                {{body}}
            }
        }
        """;

    [Theory]
    [InlineData("return a.Equals(b);")]
    [InlineData("return Nullable.Equals(a, b);")]
    [InlineData("return EqualityComparer<Spot?>.Default.Equals(a, b);")]
    public void ANullableStructsEquality_BuildsAsItsDefaultComparer(string body)
    {
        var probe = ProjectProbe.Compile(Source(body)).Should().ContainSingle(result => result.ComponentName == "Probe").Which;

        probe.Errors.Concat(probe.Warnings).Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}")
            .Should().BeEmpty("the call lowers to the comparison its value type's default comparer is");
        probe.TypeScript.Should().Contain("$eq.equals(a, b)");
    }

    [Fact]
    public void TheDefaultComparerReadAlone_IsStillRefused()
    {
        var probe = ProjectProbe.Compile(Source("var comparer = EqualityComparer<Spot?>.Default; return comparer != null;"))
            .Should().ContainSingle(result => result.ComponentName == "Probe").Which;

        probe.Errors.Should().ContainSingle().Which.Message
            .Should().StartWith("'System.Collections.Generic.EqualityComparer<Spot?>.Default' has no JavaScript translation");
    }
}
