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

var sourceDirs = args[0].Split(';', StringSplitOptions.RemoveEmptyEntries);
var outputDir = args[1];
var bunPath = args.ToList().Contains("--bun") ? args[args.ToList().IndexOf("--bun") + 1] : null;
var isWatchMode = args.Any(a => a == "--watch");

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
    }

    if (allSourceFiles.Count > 0)
    {
        // Find referenced assemblies
        var assemblyPaths = new List<string>();

        // Add standard .NET assemblies
        assemblyPaths.Add(typeof(object).Assembly.Location); // System.Private.CoreLib
        assemblyPaths.Add(typeof(Enumerable).Assembly.Location); // System.Linq
        assemblyPaths.Add(typeof(List<>).Assembly.Location); // System.Collections

        // Try to find ALL assemblies in bin folder (including NuGet packages)
        var binFolder = Path.Combine(primarySourceDir, "bin", "Debug", "net8.0");
        if (Directory.Exists(binFolder))
        {
            // Get all DLLs in bin folder
            var allDlls = Directory.GetFiles(binFolder, "*.dll", SearchOption.TopDirectoryOnly);
            foreach (var dll in allDlls)
            {
                var fileName = Path.GetFileName(dll);
                // Include eQuantic.UI assemblies and common dependencies
                if (fileName.StartsWith("eQuantic.UI", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                {
                    assemblyPaths.Add(dll);
                }
            }
            Console.WriteLine($"   Found {assemblyPaths.Count - 3} assemblies in bin folder");
        }

        Console.WriteLine($"   Creating compilation for {allSourceFiles.Count} files with {assemblyPaths.Count} references...");

        // Get project name from .csproj
        var csprojFiles = Directory.GetFiles(primarySourceDir, "*.csproj", SearchOption.TopDirectoryOnly);
        var assemblyName = csprojFiles.Length > 0 ? Path.GetFileNameWithoutExtension(csprojFiles[0]) : "DynamicAssembly";

        // Create compilation with all sources
        projectCompilation = ProjectCompilationHelper.CreateCompilationFromSources(
            allSourceFiles,
            assemblyPaths,
            assemblyName: assemblyName
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
var standardComponentsPath = Path.GetFullPath(Path.Combine(buildDir, "..", "..", "..", "..", "eQuantic.UI.Components"));
if (Directory.Exists(standardComponentsPath))
{
    componentDirectories.Add(standardComponentsPath);
}

dependencyResolver.ScanSourceDirectories(componentDirectories);
compiler.SetDependencyResolver(dependencyResolver);

var hasBun = !string.IsNullOrEmpty(bunPath) && File.Exists(bunPath);
var mode = hasBun ? "Bun (Bundled)" : "Legacy (1:1)";

if (isWatchMode)
{
    Console.WriteLine($"👀 eQuantic.UI: Watching {sourceDirs.Length} directories... [{mode}]");
}
else
{
    Console.WriteLine($"🔨 eQuantic.UI: Compiling components from {sourceDirs.Length} directories [{mode}]");
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
                        // Entry points are only from the primary source directory (the first one)
                        if (dir == sourceDirs[0] && (relativePath.StartsWith("Pages") || !relativePath.Contains(Path.DirectorySeparatorChar)))
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

        if (hasBun && entryPoints.Count > 0)
        {
            var bunArgs = $"build {string.Join(" ", entryPoints.Select(p => $"\"{p}\""))} --outdir \"{outputDir}\" --splitting --sourcemap --minify-syntax --minify-whitespace --target browser --external @equantic/runtime";
            
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
