// Authoring-coverage probe: transpiles one .cs file (arbitrary component classes) through the REAL
// eQuantic compiler pipeline and prints the emitted TypeScript per component, so authoring-pattern gaps
// can be triaged. References Core + Components so the semantic model resolves eQuantic types cleanly
// (Box/Text/StatelessComponent/RenderContext) — no `.Add → .add` noise from missing references.
//
// Usage:  dotnet run --project tools/AuthoringProbe -- <file.cs>
//   or (faster, prebuilt):  dotnet tools/AuthoringProbe/bin/Debug/net10.0/AuthoringProbe.dll <file.cs>

using System.Reflection;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: AuthoringProbe <file.cs>");
    return 1;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"file not found: {path}");
    return 1;
}

var source = File.ReadAllText(path);

// Build a compilation that references the real eQuantic assemblies so the semantic model resolves the
// component base types and standard components exactly like the SDK build does.
var coreDll = typeof(eQuantic.UI.Core.IComponent).Assembly.Location;
var componentsDll = typeof(eQuantic.UI.Components.Box).Assembly.Location;
var refs = new List<string>
{
    typeof(object).Assembly.Location,
    typeof(Enumerable).Assembly.Location,
    typeof(List<>).Assembly.Location,
    Assembly.Load("System.Runtime").Location,
    Assembly.Load("System.Collections").Location,
    coreDll,
    componentsDll,
};

var compiler = new ComponentCompiler();

// Mirror the real SDK build: a dependency resolver that has scanned the project, so the import collector's
// "only import types we actually emit (records/components)" gate is active (otherwise primitives and
// static-field names leak as bogus `import { X } from "./X"`).
try
{
    // Scan an isolated copy of just this file (not its directory, which may hold many unrelated probe
    // files) so the resolver's known-types set reflects exactly this snippet.
    var probeDir = Path.Combine(Path.GetTempPath(), "authoring-probe-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(probeDir);
    File.Copy(path, Path.Combine(probeDir, Path.GetFileName(path)), overwrite: true);
    // Scan the isolated snippet copy AND the standard component sources, exactly like the SDK build, so
    // built-ins (Box/Text/…) and the snippet's own records/components are all known to the import gate.
    var scanDirs = new List<string> { probeDir };
    var stdComponents = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "eQuantic.UI.Components");
    if (Directory.Exists(stdComponents)) scanDirs.Add(Path.GetFullPath(stdComponents));
    var resolver = new ComponentDependencyResolver();
    resolver.ScanSourceDirectories(scanDirs.ToArray());
    compiler.SetDependencyResolver(resolver);
}
catch (Exception ex)
{
    Console.Error.WriteLine("probe resolver warning: " + ex.Message);
}

try
{
    var tree = CSharpSyntaxTree.ParseText(source, path: path);
    var compilation = CSharpCompilation.Create(
        "AuthoringProbe",
        new[] { tree },
        refs.Where(File.Exists).Select(r => MetadataReference.CreateFromFile(r)),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    compiler.SetProjectCompilation(compilation);
}
catch (Exception ex)
{
    Console.Error.WriteLine("probe setup warning: " + ex.Message);
}

var results = compiler.CompileSource(source, path).ToList();
if (results.Count == 0)
{
    Console.WriteLine("<no components emitted>");
    return 0;
}

foreach (var r in results)
{
    Console.WriteLine($"=== {r.ComponentName} (success={r.Success}) ===");
    if (!r.Success)
    {
        foreach (var e in r.Errors) Console.WriteLine("ERR: " + e.Message);
        continue;
    }
    Console.WriteLine(r.TypeScript.TrimEnd());
    Console.WriteLine();
}
return 0;
