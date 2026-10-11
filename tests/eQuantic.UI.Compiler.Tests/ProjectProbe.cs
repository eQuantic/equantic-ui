using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// One source compiled as an SDK build compiles it: the compiler is handed the PROJECT's compilation,
/// which the module rule reads each class's chain of bases from. A snippet compiled on its own
/// (<c>CompileSource</c> alone) has none, and the rule there walks by name: an exception class of the
/// app's is a module only where the compilation says what it derives from (#611), so a test of one is
/// a test of a project.
/// </summary>
internal static class ProjectProbe
{
    /// <summary>Every module the source compiles to, with the project's compilation in hand.</summary>
    public static List<CompilationResult> Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Probe.cs").ToList();
    }
}
