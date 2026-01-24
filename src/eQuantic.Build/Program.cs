// CLI entry point for eQuantic.UI Compiler
using System.Diagnostics;
using eQuantic.UI.Compiler;

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

// Initialize dependency resolver by scanning component directories
var dependencyResolver = new eQuantic.UI.Compiler.Services.ComponentDependencyResolver();
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
