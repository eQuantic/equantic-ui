using eQuantic.UI.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A project compilation as an app's build hands it to eqc: the C# compiler runs the source generators
/// first, and eqc reads what they wrote beside the app's own sources.
/// <para>
/// A twin's hydration map lists what the server carries to the component, and eqc takes that from the
/// hydration manifest alone, the entries the server writes its payload from. A component compiled
/// without the generator has no manifest, so it carries nothing and its twin lists nothing: a test of
/// the map compiles through here.
/// </para>
/// </summary>
internal static class GeneratedProject
{
    /// <summary>The compilation of one source file with the vocabulary referenced, generated.</summary>
    public static Compilation Of(string source, string path)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        return Generated(CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(path),
            [CSharpSyntaxTree.ParseText(source, eQuantic.UI.Compiler.Services.ParseDefaults.Options, path)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable)));
    }

    /// <summary><paramref name="compilation"/> with the hydration manifest the generator writes for it.</summary>
    public static Compilation Generated(Compilation compilation)
    {
        // Generated with the sources' own parse options, as the C# compiler generates: a generated tree
        // in another language version is a compilation Roslyn refuses to build.
        CSharpGeneratorDriver.Create(
                [new HydrationManifestGenerator().AsSourceGenerator()],
                parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _);
        return generated;
    }
}
