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
        twins.Claim("BenchSeat", "App.Sections.BenchSeat", "Sections/Bench.cs", "class BenchSeat { a }", Here, out _).Should().Be(TwinClaim.Fresh);

        twins.Claim("BenchSeat", "App.Pages.BenchSeat", "Pages/Bench.cs", "class BenchSeat { b }", Here, out var collision).Should().Be(TwinClaim.Collision);

        collision.Should().NotBeNull();
        collision.Should().Contain("BenchSeat").And.Contain("Sections/Bench.cs");
        collision.Should().Contain("named for its TYPE", "the message has to say WHY namespaces do not help");
    }

    [Fact]
    public void TwoTypesOfOneName_InTheSameFile_AreRefusedToo()
    {
        var twins = new EmittedTwins();
        twins.Claim("Seat", "App.Sections.Seat", "Bench.cs", "class Seat { a }", Here, out _).Should().Be(TwinClaim.Fresh);

        twins.Claim("Seat", "App.Pages.Seat", "Bench.cs", "class Seat { b }", Here, out var same)
            .Should().Be(TwinClaim.Collision);
        same.Should().Contain("another type of the same name in this file");
    }

    [Fact]
    public void TheSameTwinEmittedTwice_IsNotACollision()
    {
        // Compared by CONTENT: a type that reaches the writer twice writes the same bytes, and
        // refusing that would fail builds that were never broken.
        var twins = new EmittedTwins();
        var module = "class Seat { a }";
        twins.Claim("Seat", "App.Seat", "Bench.cs", module, Here, out _).Should().Be(TwinClaim.Fresh);
        // REPEAT, not Fresh: the writer must skip it. The module is identical but its source map
        // is not — that embeds the C# path, so rewriting it would map the module to another file.
        twins.Claim("Seat", "App.Seat", "Bench.cs", module, Here, out _).Should().Be(TwinClaim.Repeat);
        twins.Claim("Seat", "App.Seat", "Other.cs", module, Here, out _).Should().Be(TwinClaim.Repeat);
    }

    /// <summary>`Seat` and `seat` are two types to C# and ONE file to Windows and to macOS as it
    /// ships. Keying by ordinal would let the second overwrite the first on the machines most
    /// people build on and pass on Linux, which is worse than either — a source tree has to build
    /// the same everywhere.</summary>
    [Fact]
    public void NamesDifferingOnlyInCase_AreRefused_BecauseAFilenameDoesNotCare()
    {
        var twins = new EmittedTwins();
        twins.Claim("Seat", "App.Seat", "a.cs", "class Seat {}", Here, out _).Should().Be(TwinClaim.Fresh);

        twins.Claim("seat", "App.seat", "b.cs", "class seat {}", Here, out var collision).Should().Be(TwinClaim.Collision);

        collision.Should().NotBeNull();
        collision.Should().Contain("differ only in case",
            "the message must say WHY two different names collide, or it reads as a compiler bug");
        collision.Should().Contain("'seat'").And.Contain("'Seat'");
    }

    [Fact]
    public void DifferentNames_NeverCollide()
    {
        var twins = new EmittedTwins();
        twins.Claim("Seat", "App.Seat", "a.cs", "class Seat {}", Here, out _).Should().Be(TwinClaim.Fresh);
        twins.Claim("Bench", "App.Bench", "b.cs", "class Bench {}", Here, out _).Should().Be(TwinClaim.Fresh);
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

    /// <summary>
    /// Reported by the site on preview.41, and the reason this class had to learn the difference
    /// between a file and a type. `LibrarySeedSource` is ONE `sealed partial class` across six
    /// files; the ledger counted DECLARATIONS and answered five collisions, each able to stop the
    /// build on its own. Nothing was colliding: one type cannot silently replace itself.
    /// </summary>
    [Fact]
    public void OneTypeSplitAcrossFiles_IsNotACollision()
    {
        var twins = new EmittedTwins();
        twins.Claim("LibrarySeedSource", "App.Data.LibrarySeedSource", "Seed/Books.cs",
            "class LibrarySeedSource { books }", Here, out _).Should().Be(TwinClaim.Fresh);

        var claim = twins.Claim("LibrarySeedSource", "App.Data.LibrarySeedSource", "Seed/Films.cs",
            "class LibrarySeedSource { films }", Here, out var message);

        claim.Should().Be(TwinClaim.Divided, "a partial type is one type, and one type is one twin");
        message.Should().NotBeNull();
        message.Should().Contain("Seed/Books.cs",
            "the warning has to name the declaration that DID reach the twin");
        message.Should().Contain("not in it",
            "and say plainly that these members were left out, since that only shows in the browser");
    }

    /// <summary>
    /// The other half of the same report: `obj/Debug/…/AppUI.g.cs` and `obj/Release/…/AppUI.g.cs`
    /// collected together, which is one generated type written twice by two configurations. This
    /// one hits CI every time, because CI builds Release over a tree that has a Debug obj.
    /// </summary>
    [Fact]
    public void OneGeneratedTypeFromTwoConfigurations_IsNotACollision()
    {
        var twins = new EmittedTwins();
        const string module = "class AppUI { factories }";
        twins.Claim("AppUI", "App.AppUI", "obj/Debug/net10.0/generated/AppUI.g.cs", module, Here, out _)
            .Should().Be(TwinClaim.Fresh);

        twins.Claim("AppUI", "App.AppUI", "obj/Release/net10.0/generated/AppUI.g.cs", module, Here, out var message)
            .Should().Be(TwinClaim.Repeat);
        message.Should().BeNull("identical bytes from one type are not worth a word to anybody");
    }

    /// <summary>
    /// The guard on the guard: making partials pass must not make the ORIGINAL bug pass. What
    /// separates them is not the namespace — grouping the FILE by namespace would let two
    /// `BenchSeat` through, which is the bug this class exists for — it is whether the two claims
    /// are the same TYPE. Same name, different identity: still refused.
    /// </summary>
    [Fact]
    public void TwoTypesOfOneName_AreStillRefused_NowThatOneTypeMayRepeat()
    {
        var twins = new EmittedTwins();
        twins.Claim("BenchSeat", "App.Sections.BenchSeat", "a.cs", "class BenchSeat { a }", Here, out _)
            .Should().Be(TwinClaim.Fresh);

        twins.Claim("BenchSeat", "App.Pages.BenchSeat", "b.cs", "class BenchSeat { b }", Here, out var error)
            .Should().Be(TwinClaim.Collision);
        error.Should().Contain("named for its TYPE");
    }
}
