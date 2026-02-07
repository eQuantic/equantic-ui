// CLI entry point for eQuantic.UI Compiler
using System.Diagnostics;
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
CompileAndBundle();

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

return 0;

void CompileAndBundle()
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
                    if (result.Success)
                    {
                        var tsPath = Path.Combine(intermediateDir, $"{result.ComponentName}.ts");
                        File.WriteAllText(tsPath, result.TypeScript);
                        
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
                            Console.Error.WriteLine($"Error [{error.SourcePath}:{error.Line}]: {error.Message}");
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
        
        if (hasErrors) return;
        
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
                return;
            }
        }

        Console.WriteLine($"✅ Built at {DateTime.Now:HH:mm:ss}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Compilation crash: {ex.Message}");
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
