using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A record or a struct marked <c>[ServerOnly]</c> never crosses, as a class marked so does not: no
/// twin, no module, no diagnostic, and no import of one, whichever of its declarations carries the
/// attribute. Every record and struct got a twin whatever it was marked, so a server-only hasher over
/// <c>HMACSHA256</c> failed the build with EQ2004, whose own message says to mark the type
/// <c>[ServerOnly]</c>. Compiled as an app's build compiles it: one compilation behind the model, handed
/// to the resolver too, and the resolver scanning the directory.
/// </summary>
public class ServerOnlyValueTypeTests
{
    /// <summary>A value type that hashes with the server's cryptography, which has no JavaScript translation.</summary>
    private static string Hasher(string declaration, string name) => $$"""
        {{declaration}} {{name}}
        {
            private readonly byte[] _key;
            public {{name}}(byte[] key) { _key = key; }
            public string Hash(string s) => System.Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(_key, System.Text.Encoding.UTF8.GetBytes(s)));
        }
        """;

    [Fact]
    public void AServerOnlyStructOrRecord_HasNoTwinAndNoDiagnostic()
    {
        var (results, resolver) = Compile(new Dictionary<string, string>
        {
            ["TokenHasher.cs"] = Hasher("[eQuantic.UI.Primitives.ServerOnly] public struct", "TokenHasher"),
            ["TokenSigner.cs"] = Hasher("[eQuantic.UI.Primitives.ServerOnly] public record", "TokenSigner"),
            // The attribute on one declaration of a partial type, the members on another, in another file:
            // the type is server-only, which only its symbol says of the second.
            ["Ledger.cs"] = "[eQuantic.UI.Primitives.ServerOnly] public partial struct Ledger { }",
            ["LedgerBody.cs"] = Hasher("public partial struct", "Ledger"),
        });

        results.Where(result => result.TypeScript.Length > 0).Select(result => result.ComponentName)
            .Should().BeEmpty("a server-only type has no twin");
        results.SelectMany(result => result.Errors.Concat(result.Warnings)).Select(error => $"{error.Code} {error.Message}")
            .Should().BeEmpty("nothing of a server-only type is transpiled, so nothing in it is refused");
        new[] { "TokenHasher", "TokenSigner", "Ledger" }.Where(resolver.IsModule)
            .Should().BeEmpty("no module imports a twin nothing writes");
    }

    [Fact]
    public void AStructThatCrosses_StillRefusesWhatItCannotTranslate()
    {
        var (results, resolver) = Compile(new Dictionary<string, string>
        {
            ["TokenHasher.cs"] = Hasher("public struct", "TokenHasher"),
        });

        results.Should().ContainSingle(result => result.ComponentName == "TokenHasher")
            .Which.Errors.Select(error => error.Code).Should().Contain("EQ2004");
        resolver.IsModule("TokenHasher").Should().BeTrue();
    }

    private static (List<CompilationResult> Results, ComponentDependencyResolver Resolver) Compile(
        IReadOnlyDictionary<string, string> files)
    {
        var dir = Directory.CreateTempSubdirectory("eq-server-only-value-").FullName;
        try
        {
            var trees = files.Select(file =>
            {
                var path = Path.Combine(dir, file.Key);
                File.WriteAllText(path, file.Value);
                return CSharpSyntaxTree.ParseText(file.Value, ParseDefaults.Options, path: path);
            }).ToList();
            var compilation = CSharpCompilation.Create("App", trees,
                ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                    .Split(Path.PathSeparator)
                    .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Select(p => TestReferences.Of(p))
                    .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

            var resolver = new ComponentDependencyResolver(compilation);
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var results = files.Keys.SelectMany(file => compiler.CompileFile(Path.Combine(dir, file))).ToList();
            return (results, resolver);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
