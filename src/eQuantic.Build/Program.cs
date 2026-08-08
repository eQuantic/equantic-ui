// CLI entry point for eQuantic.UI Compiler
using System.Diagnostics;
using System.Text;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: eqc <source-dir> <output-dir> [--bun <path>] [--watch]");
    return 1;
}

// TrimEntries: the SDK passes the source dirs as "$(MSBuildProjectDirectory);$(_StandardComponentsDir)",
// and _StandardComponentsDir is defined as multi-line XML, so its value carries leading whitespace/
// newline. Without trimming, Directory.Exists fails on the standard-components path → the built-in
// components (Grid/Box/…) are never transpiled, and pages referencing them break with "X is not defined".
var sourceDirs = args[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var outputDir = args[1].Trim();
var bunPath = args.ToList().Contains("--bun") ? args[args.ToList().IndexOf("--bun") + 1] : null;
var isWatchMode = args.Any(a => a == "--watch");
// --refs <file>: a newline-delimited list of the assemblies MSBuild resolved for this project
// (@(ReferencePathWithRefAssemblies)). Feeding these to the semantic model makes eqc's type resolution
// identical to the real csc build — eQuantic.UI.Core/Components, NuGet and project references all resolve,
// independent of TargetFramework or configuration. Without it, base types like HtmlElement.Children
// (IList<IComponent>) stay unresolved and member calls degrade to naive camel-casing (.Add → .add).
var refsFile = args.ToList().Contains("--refs") ? args[args.ToList().IndexOf("--refs") + 1] : null;
// --ref-sources <file>: newline-delimited DIRECTORIES whose .cs join the compilation as
// SEMANTIC-ONLY reference sources (never transpiled). This is what lets eqc inline external
// constants at the use site — an icon pack's `static readonly IconGlyph` needs its INITIALIZER,
// which metadata (via --refs) does not carry; the pack's tools/source supplies it here.
var refSourcesFile = args.ToList().Contains("--ref-sources") ? args[args.ToList().IndexOf("--ref-sources") + 1] : null;

// Determine intermediate directory based on primary source dir
var primarySourceDir = sourceDirs[0];
var intermediateDir = Path.Combine(primarySourceDir, "obj", "eQuantic", "ts");

var compiler = new ComponentCompiler();

// Explicitly register Tailwind style provider to ensure it's available
// (Assembly discovery may not find it if assembly isn't loaded yet)
compiler.StyleProviders.Register(new eQuantic.UI.Tailwind.Build.TailwindStyleProvider());

// Create full project compilation for better type resolution
// This enables the compiler to resolve types defined in external files
Compilation? projectCompilation = null;
try
{
    // Write to a log file for debugging since MSBuild might not show console output
    var logPath = Path.Combine(intermediateDir, "compilation.log");
    Directory.CreateDirectory(intermediateDir);
    File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Starting compilation setup\n");

    Console.WriteLine("🔍 Attempting to load project compilation...");

    // Collect source files only from the PRIMARY source directory (user's project)
    // Skip standard components directory as those are already compiled
    var allSourceFiles = new List<string>();

    if (Directory.Exists(primarySourceDir))
    {
        Console.WriteLine($"   Scanning project directory: {primarySourceDir}");
        var files = ProjectCompilationHelper.GetProjectSourceFiles(primarySourceDir).ToList();
        Console.WriteLine($"   Found {files.Count} source files in project");
        allSourceFiles.AddRange(files);

        // Feed the SDK-generated global usings (obj/*.GlobalUsings.g.cs) to the compilation so the
        // semantic model resolves BCL types used unqualified — e.g. Dictionary<RecordKey, V> under
        // <ImplicitUsings> — exactly as the real build does, without hardcoding a namespace list.
        var globalUsings = ProjectCompilationHelper.GetGeneratedGlobalUsingsFiles(primarySourceDir).ToList();
        if (globalUsings.Count > 0)
        {
            Console.WriteLine($"   Including {globalUsings.Count} generated global-usings file(s)");
            allSourceFiles.AddRange(globalUsings);
        }
    }

    // Reference-only sources (icon packs etc.): their .cs enter the compilation so the semantic model
    // can reach constant initializers for inlining, but they are NOT in sourceDirs so nothing here is
    // transpiled to a module.
    if (!string.IsNullOrEmpty(refSourcesFile) && File.Exists(refSourcesFile))
    {
        var refSourceCount = 0;
        foreach (var dir in File.ReadAllLines(refSourcesFile).Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in ProjectCompilationHelper.GetProjectSourceFiles(dir))
            {
                allSourceFiles.Add(file);
                refSourceCount++;
            }
        }
        if (refSourceCount > 0)
            Console.WriteLine($"   Including {refSourceCount} reference-source file(s) for constant inlining");
    }

    if (allSourceFiles.Count > 0)
    {
        var assemblyPaths = new List<string>();

        // Preferred path: the exact reference set MSBuild resolved for this project, handed to us via
        // --refs. This is what csc itself compiles against, so the semantic model resolves every type
        // the real build does (eQuantic.UI.*, NuGet, project references) with no TFM/config guessing.
        var usingExplicitRefs = false;
        if (!string.IsNullOrEmpty(refsFile) && File.Exists(refsFile))
        {
            foreach (var line in File.ReadAllLines(refsFile))
            {
                var path = line.Trim();
                if (path.Length > 0 && File.Exists(path))
                    assemblyPaths.Add(path);
            }
            usingExplicitRefs = assemblyPaths.Count > 0;
            Console.WriteLine($"   Using {assemblyPaths.Count} MSBuild-resolved references from {Path.GetFileName(refsFile)}");
        }

        if (!usingExplicitRefs)
        {
            // Fallback for direct CLI use without --refs: the running BCL plus any eQuantic/System/Roslyn
            // assemblies emitted to the project's bin tree. Scan every TFM/config dir (deduping by file
            // name) instead of hardcoding one — the old net8.0 hardcode silently dropped all references.
            assemblyPaths.Add(typeof(object).Assembly.Location);   // System.Private.CoreLib
            assemblyPaths.Add(typeof(Enumerable).Assembly.Location); // System.Linq
            assemblyPaths.Add(typeof(List<>).Assembly.Location);   // System.Collections

            var binRoot = Path.Combine(primarySourceDir, "bin");
            if (Directory.Exists(binRoot))
            {
                var seen = new HashSet<string>(assemblyPaths.Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
                foreach (var dll in Directory.GetFiles(binRoot, "*.dll", SearchOption.AllDirectories))
                {
                    var fileName = Path.GetFileName(dll);
                    var isCandidate =
                        fileName.StartsWith("eQuantic.UI", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase);
                    if (isCandidate && seen.Add(fileName))
                        assemblyPaths.Add(dll);
                }
                Console.WriteLine($"   Found {assemblyPaths.Count - 3} assemblies in bin tree (fallback)");
            }
        }

        Console.WriteLine($"   Creating compilation for {allSourceFiles.Count} files with {assemblyPaths.Count} references...");

        // Get project name from .csproj
        var csprojFiles = Directory.GetFiles(primarySourceDir, "*.csproj", SearchOption.TopDirectoryOnly);
        var assemblyName = csprojFiles.Length > 0 ? Path.GetFileNameWithoutExtension(csprojFiles[0]) : "DynamicAssembly";

        // When we have the complete MSBuild reference set, use it verbatim. Injecting the runtime BCL
        // impl assemblies on top of the targeting-pack ref assemblies would duplicate identities and
        // muddy resolution, so only add the standard fallback set when --refs was not supplied.
        projectCompilation = ProjectCompilationHelper.CreateCompilationFromSources(
            allSourceFiles,
            assemblyPaths,
            assemblyName: assemblyName,
            addStandardReferences: !usingExplicitRefs
        );

        compiler.SetProjectCompilation(projectCompilation);
        Console.WriteLine($"📚 Loaded project compilation: {assemblyName} ({allSourceFiles.Count} files, {assemblyPaths.Count} refs)");

        // Log success
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] SUCCESS: Loaded {assemblyName} with {allSourceFiles.Count} files\n");
    }
    else
    {
        Console.WriteLine("⚠️  No source files found for project compilation");
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] WARNING: No source files found\n");
    }
}
catch (Exception ex)
{
    // If project compilation fails, continue with minimal compilation
    Console.WriteLine($"⚠️  Project compilation failed: {ex.Message}");
    Console.WriteLine($"   Stack: {ex.StackTrace}");
    Console.WriteLine("   Using minimal compilation per file");

    var logPath = Path.Combine(intermediateDir, "compilation.log");
    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n{ex.StackTrace}\n");
}

// Initialize dependency resolver by scanning component directories
var dependencyResolver = new ComponentDependencyResolver();
var componentDirectories = new List<string>(sourceDirs);

// Also scan standard component library locations relative to build tool
var buildDir = AppContext.BaseDirectory;
var standardComponentsPath = Path.GetFullPath(Path.Combine(buildDir, "..", "..", "..", "..", "eQuantic.UI.Web.Components"));
if (Directory.Exists(standardComponentsPath))
{
    componentDirectories.Add(standardComponentsPath);
}

dependencyResolver.ScanSourceDirectories(componentDirectories);
compiler.SetDependencyResolver(dependencyResolver);

var hasBun = !string.IsNullOrEmpty(bunPath) && File.Exists(bunPath);

if (isWatchMode)
{
    Console.WriteLine($"👀 eQuantic.UI: Watching {sourceDirs.Length} directories...");
}
else
{
    Console.WriteLine($"🔨 eQuantic.UI: Compiling components from {sourceDirs.Length} directories");
}

Console.WriteLine($"   Intermediate: {intermediateDir}");
Console.WriteLine($"   Output:       {outputDir}");

// Initial compilation
var initialBuildHadErrors = CompileAndBundle();

if (isWatchMode)
{
    var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100));
    // Watch all source directories
    var watchers = new List<FileSystemWatcher>();
    foreach (var dir in sourceDirs)
    {
        var watcher = new FileSystemWatcher(dir, "*.cs");
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;

        FileSystemEventHandler onChanged = (sender, e) =>
        {
            if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                e.FullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                return;
                
            debouncer.Debounce(() => {
                Console.WriteLine($"🔄 Change detected in {Path.GetFileName(e.FullPath)}. Recompiling...");
                CompileAndBundle();
            });
        };

        watcher.Changed += onChanged;
        watcher.Created += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += (s, e) => onChanged(s, e);
        watcher.EnableRaisingEvents = true;
        watchers.Add(watcher);
    }
    
    await Task.Delay(-1);
}

// Non-zero exit fails the MSBuild `Exec` step when transpilation hit unsupported constructs.
return initialBuildHadErrors ? 1 : 0;

// Format a diagnostic in MSBuild's canonical form so `Exec` recognises it and fails/warns the build:
//   path(line,col): error EQ2001: message
static string FormatDiagnostic(eQuantic.UI.Compiler.CompilationError d, string severity)
{
    var code = string.IsNullOrEmpty(d.Code) ? "EQ0000" : d.Code;
    var path = string.IsNullOrEmpty(d.SourcePath) ? "eQuantic.UI" : d.SourcePath;
    var line = d.Line > 0 ? d.Line : 1;
    var col = d.Column > 0 ? d.Column : 1;
    return $"{path}({line},{col}): {severity} {code}: {d.Message}";
}

// Runs the source-map merge with one JS runtime. Returns true only on a clean exit (0); returns false
// on a launch failure OR a non-zero exit (e.g. the bun JS VM crashing on an AVX-less VM) — never throws,
// so the caller can fall back to another runtime and the build is never broken by map merging.
static bool TryRunMerge(string fileName, string script, string mapFile)
{
    try
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = $"\"{script}\" \"{mapFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        p.Start();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

// Returns true if compilation produced errors (so the process can exit non-zero and fail the build).
bool CompileAndBundle()
{
    try
    {
        var hasErrors = false;
        var entryPoints = new List<string>();
        var safelist = new HashSet<string>();
        
        if (!Directory.Exists(intermediateDir)) Directory.CreateDirectory(intermediateDir);
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        foreach (var dir in sourceDirs)
        {
            if (!Directory.Exists(dir)) continue;
            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;

                // Try to compile - parser will return empty if not a component
                // This removes restrictions on naming, inheritance patterns, aliases, etc.
                var results = compiler.CompileFile(file);
                
                foreach (var result in results)
                {
                    // Warnings surface whether or not compilation succeeded (e.g. an unconverted
                    // construct emitted verbatim). Errors are reported below and fail the build.
                    foreach (var warning in result.Warnings)
                    {
                        Console.Error.WriteLine(FormatDiagnostic(warning, "warning"));
                    }

                    if (result.Success)
                    {
                        var tsPath = Path.Combine(intermediateDir, $"{result.ComponentName}.ts");
                        File.WriteAllText(tsPath, result.TypeScript);
                        
                        if (!string.IsNullOrEmpty(result.SourceMap))
                        {
                            var mapPath = tsPath + ".map";
                            File.WriteAllText(mapPath, result.SourceMap);
                            File.AppendAllText(tsPath, $"\n//# sourceMappingURL={result.ComponentName}.ts.map");
                        }
                        
                        var relativePath = Path.GetRelativePath(dir, file);
                        // A PAGE is always an entry point, wherever its file sits: the server loads
                        // it by name, so a page that is only transpiled is a 404 the build never
                        // mentioned. The positional rule stays as well — a project laid out the
                        // conventional way keeps every module it had before.
                        var isEntryPoint = result.IsPage
                            || (dir == sourceDirs[0]
                                && (relativePath.StartsWith("Pages")
                                    || !relativePath.Contains(Path.DirectorySeparatorChar)));
                        if (isEntryPoint)
                        {
                            entryPoints.Add(tsPath);
                        }

                        // Collect Extracted Styles
                        if (result.ExtractedStyles != null && result.ExtractedStyles.Count > 0)
                        {
                             foreach(var cls in result.ExtractedStyles) {
                                 safelist.Add(cls);
                             }
                        }
                    }
                    else
                    {
                        hasErrors = true;
                        foreach (var error in result.Errors)
                        {
                            Console.Error.WriteLine(FormatDiagnostic(error, "error"));
                        }
                    }
                }
            }
        }
        
        // Write Safelist
        if (safelist.Count > 0)
        {
            var safelistPath = Path.Combine(intermediateDir, "tailwind-safelist.txt");
            File.WriteAllText(safelistPath, string.Join("\n", safelist));
        }
        
        if (hasErrors) return true;

        if (!hasBun && entryPoints.Count > 0)
        {
            // The embedded Bun is a hard requirement — bundling is what produces the page JS. A
            // missing binary must fail the build loudly, never "succeed" without output.
            Console.Error.WriteLine("❌ Embedded Bun not found — the eQuantic.UI build requires the " +
                "bundled Bun (restore the platform Runtime package, e.g. eQuantic.UI.Runtime.Osx64).");
            return true;
        }

        if (hasBun && entryPoints.Count > 0)
        {
            // --root pins the output-path base to the intermediate TS dir (where every entry .ts lives),
            // so bun writes entries FLAT as "<Page>.js" in outDir. Without it bun infers the root from the
            // common ancestor of the absolute entry paths (the repo/cwd) and nests entries under that
            // relative path (e.g. wwwroot/_equantic/samples/.../ts/Dashboard.js), which the boot — loading
            // the flat "/_equantic/<Page>.js" — then 404s on.
            var bunArgs = $"build {string.Join(" ", entryPoints.Select(p => $"\"{p}\""))} --outdir \"{outputDir}\" --root \"{intermediateDir}\" --splitting --sourcemap --minify-syntax --minify-whitespace --target browser --external @equantic/runtime";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bunPath!,
                    Arguments = bunArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("❌ Bun compilation failed:");
                Console.Error.WriteLine(error);
                return true;
            }

            // Post-process source maps to merge C# -> TS and TS -> JS
            var jsMapFiles = Directory.GetFiles(outputDir, "*.js.map", SearchOption.AllDirectories);
            var scriptsDir = Path.Combine(AppContext.BaseDirectory, "Scripts");
            var mergeMapsScript = Path.Combine(scriptsDir, "merge-maps.js");

            if (File.Exists(mergeMapsScript))
            {
                // Ensure dependency is installed
                var nodeModulesDir = Path.Combine(scriptsDir, "node_modules", "@ampproject", "remapping");
                if (!Directory.Exists(nodeModulesDir))
                {
                    Console.WriteLine("📦 Installing remapping dependency in SDK CLI...");
                    var installProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = bunPath!,
                            Arguments = "add @ampproject/remapping",
                            WorkingDirectory = scriptsDir,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    installProcess.Start();
                    installProcess.WaitForExit();
                }
                foreach (var mapFile in jsMapFiles)
                {
                    // Source-map merge is BEST-EFFORT (C#-level debugging only) — it must never fail
                    // the build. Prefer the embedded bun (zero Node by default); if bun can't start OR
                    // exits non-zero (e.g. the bun JS VM crashes on some VMs), fall back to a system
                    // `node`. If neither works, warn and move on.
                    if (!TryRunMerge(bunPath!, mergeMapsScript, mapFile) &&
                        !TryRunMerge("node", mergeMapsScript, mapFile))
                    {
                        Console.Error.WriteLine($"⚠️ Map merging skipped for {Path.GetFileName(mapFile)} (no working JS runtime).");
                    }
                }
            }
        }

        Console.WriteLine($"✅ Built at {DateTime.Now:HH:mm:ss}");
        return false;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"eQuantic.UI(1,1): error EQ0001: Compilation crash: {ex.Message}");
        return true;
    }
}

class Debouncer
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;

    public Debouncer(TimeSpan delay)
    {
        _delay = delay;
    }

    public void Debounce(Action action)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Delay(_delay, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                action();
            }
        });
    }
}
