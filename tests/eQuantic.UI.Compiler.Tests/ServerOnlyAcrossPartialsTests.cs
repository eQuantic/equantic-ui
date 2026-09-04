using System.IO;
using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>[ServerOnly]</c> describes a TYPE, so it has to be answered for the type — not for whichever
/// declaration of it the parser happens to be holding.
/// <para>
/// The syntactic form answered per file, and that made a partial class spread across files
/// impossible to silence. C# unifies attributes across partials and rejects a second one
/// (CS0579, "Duplicate 'ServerOnly' attribute"), so an author cannot mark them all; and one
/// declaration silenced only its own, so the others still emitted a module and still warned. A
/// consumer measured four of five EQ1006 warnings surviving, with no way out short of merging six
/// files that were separate on purpose — one per library.
/// </para>
/// </summary>
public class ServerOnlyAcrossPartialsTests
{
    [Fact]
    public void ServerOnlyOnOneDeclaration_SilencesEveryPartOfTheType()
    {
        var dir = Directory.CreateTempSubdirectory("eq-partials");
        try
        {
            // The attribute is legal on exactly ONE of the declarations. This is not a style
            // choice the author could make differently — the compiler refuses the second.
            File.WriteAllText(Path.Combine(dir.FullName, "Seed.Books.cs"), """
                using eQuantic.UI.Core;
                namespace App;
                [ServerOnly]
                public sealed partial class Seed { public string Books() => "books"; }
                """);
            File.WriteAllText(Path.Combine(dir.FullName, "Seed.Films.cs"), """
                namespace App;
                public sealed partial class Seed { public string Films() => "films"; }
                """);
            File.WriteAllText(Path.Combine(dir.FullName, "Seed.Music.cs"), """
                namespace App;
                public sealed partial class Seed { public string Music() => "music"; }
                """);

            // The SDK build hands eqc the project's whole compilation, which is what makes the
            // symbol see every partial. Without it there is one model per file and the symbol knows
            // only the declaration in front of it — the very blindness this test is about.
            var files = Directory.GetFiles(dir.FullName, "*.cs");
            var project = CSharpCompilation.Create(
                "PartialsProbe",
                files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)),
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(project);
            var emitted = compiler.CompileDirectory(dir.FullName).ToList();

            emitted.Should().BeEmpty(
                "the type is server-only, so no declaration of it earns a module — which file "
                + "carries the attribute is not the transpiler's business");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The syntax-only fallback — no model at all — has to recognise the attribute however C# lets
    /// it be spelled. Exact comparison of <c>Name.ToString()</c> missed the qualified form, and an
    /// author writes the namespace out exactly where two namespaces both offer the name.
    /// <para>
    /// This goes through the PARSER directly, with no provider set, because that is the only way to
    /// reach the fallback: <c>ComponentCompiler</c> always installs one, and even without references
    /// the symbol path answers first — an unresolved attribute binds to an error symbol that still
    /// carries the last segment written. Through the compiler, this test passed against the very
    /// bug it exists to catch. Through the bare parser it fails against it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("[ServerOnly]")]
    [InlineData("[ServerOnlyAttribute]")]
    [InlineData("[eQuantic.UI.Primitives.ServerOnly]")]
    [InlineData("[eQuantic.UI.Primitives.ServerOnlyAttribute]")]
    [InlineData("[global::eQuantic.UI.Primitives.ServerOnly]")]
    public void ServerOnly_IsRecognisedInEverySpelling_WithoutAModel(string spelling)
    {
        var parser = new eQuantic.UI.Compiler.Parser.ComponentParser(); // no provider: syntax only
        var definitions = parser.ParseSource($$"""
            namespace App;
            {{spelling}}
            public sealed class Seed { public string Books() => "books"; }
            """, "Seed.cs").ToList();

        definitions.Should().BeEmpty($"{spelling} says the type never crosses, in any spelling");
    }
}
