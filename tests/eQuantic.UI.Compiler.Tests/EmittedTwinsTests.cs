using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A twin's file is named for its TYPE and nothing else, so two types of one name are one file and
/// the second replaces the first. C# is happy — the namespaces separate them — and the failure
/// arrives in the browser as a component dying on a field its class does not have. Reported by the
/// site, which met it with two `record BenchSeat` in different namespaces and spent the debugging
/// on a runtime error whose cause was a build that had quietly dropped a file.
/// </summary>
public class EmittedTwinsTests
{
    private static string Here(string path) => path;

    [Fact]
    public void TwoTypesOfOneName_InDifferentFiles_AreRefused()
    {
        var twins = new EmittedTwins();
        twins.Claim("BenchSeat", "Sections/Bench.cs", "class BenchSeat { a }", Here).Should().BeNull();

        var collision = twins.Claim("BenchSeat", "Pages/Bench.cs", "class BenchSeat { b }", Here);

        collision.Should().NotBeNull();
        collision.Should().Contain("BenchSeat").And.Contain("Sections/Bench.cs");
        collision.Should().Contain("named for its TYPE", "the message has to say WHY namespaces do not help");
    }

    [Fact]
    public void TwoTypesOfOneName_InTheSameFile_AreRefusedToo()
    {
        var twins = new EmittedTwins();
        twins.Claim("Seat", "Bench.cs", "class Seat { a }", Here).Should().BeNull();

        twins.Claim("Seat", "Bench.cs", "class Seat { b }", Here)
            .Should().Contain("another type of the same name in this file");
    }

    [Fact]
    public void TheSameTwinEmittedTwice_IsNotACollision()
    {
        // Compared by CONTENT: a type that reaches the writer twice writes the same bytes, and
        // refusing that would fail builds that were never broken.
        var twins = new EmittedTwins();
        var module = "class Seat { a }";
        twins.Claim("Seat", "Bench.cs", module, Here).Should().BeNull();
        twins.Claim("Seat", "Bench.cs", module, Here).Should().BeNull();
        twins.Claim("Seat", "Other.cs", module, Here).Should().BeNull();
    }

    /// <summary>`Seat` and `seat` are two types to C# and ONE file to Windows and to macOS as it
    /// ships. Keying by ordinal would let the second overwrite the first on the machines most
    /// people build on and pass on Linux, which is worse than either — a source tree has to build
    /// the same everywhere.</summary>
    [Fact]
    public void NamesDifferingOnlyInCase_AreRefused_BecauseAFilenameDoesNotCare()
    {
        var twins = new EmittedTwins();
        twins.Claim("Seat", "a.cs", "class Seat {}", Here).Should().BeNull();

        var collision = twins.Claim("seat", "b.cs", "class seat {}", Here);

        collision.Should().NotBeNull();
        collision.Should().Contain("differ only in case",
            "the message must say WHY two different names collide, or it reads as a compiler bug");
        collision.Should().Contain("'seat'").And.Contain("'Seat'");
    }

    [Fact]
    public void DifferentNames_NeverCollide()
    {
        var twins = new EmittedTwins();
        twins.Claim("Seat", "a.cs", "class Seat {}", Here).Should().BeNull();
        twins.Claim("Bench", "b.cs", "class Bench {}", Here).Should().BeNull();
    }

    /// <summary>The shape the site actually hit: two records of one name, different members. The
    /// compiler emits both — it is the FILE they land in that is shared.</summary>
    [Fact]
    public void TheCompilerEmitsBothTypes_WhichIsWhyTheWriterHasToRefuse()
    {
        const string source = """
            namespace App.Sections { public sealed record BenchSeat(string Label, int Row, string Tone); }
            namespace App.Pages { public sealed record BenchSeat(string Name, bool Taken); }
            """;
        var results = new ComponentCompiler().CompileSource(source, "Bench.cs").ToList();

        results.Should().HaveCount(2);
        results.Select(r => r.ComponentName).Should().AllBe("BenchSeat");
        results[0].TypeScript.Should().NotBe(results[1].TypeScript,
            "they are different types — which is exactly what makes one file wrong");
    }
}
