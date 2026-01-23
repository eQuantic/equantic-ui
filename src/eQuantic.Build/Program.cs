// CLI entry point for eQuantic.UI Compiler
using System.Diagnostics;
using eQuantic.UI.Compiler;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: eqc <source-dir> <output-dir> [--bun <path>] [--watch]");
    return 1;
}

var sourceDir = args[0];
var outputDir = args[1];
var bunPath = args.ToList().Contains("--bun") ? args[args.ToList().IndexOf("--bun") + 1] : null;
var isWatchMode = args.Any(a => a == "--watch");

// Determine intermediate directory for TS files
var intermediateDir = Path.Combine(sourceDir, "obj", "eQuantic", "ts");
if (!Directory.Exists(intermediateDir)) Directory.CreateDirectory(intermediateDir);
if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

var compiler = new ComponentCompiler();
var hasBun = !string.IsNullOrEmpty(bunPath) && File.Exists(bunPath);
var mode = hasBun ? "Bun (Bundled)" : "Legacy (1:1)";

if (isWatchMode)
{
    Console.WriteLine($"👀 eQuantic.UI: Watching for changes... [{mode}]");
}
else
{
    Console.WriteLine($"🔨 eQuantic.UI: Compiling components [{mode}]");
}

Console.WriteLine($"   Intermediate: {intermediateDir}");
Console.WriteLine($"   Output:       {outputDir}");

// Initial compilation
CompileAndBundle();

if (isWatchMode)
{
    var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100));
    using var watcher = new FileSystemWatcher(sourceDir, "*.cs");
    
    watcher.IncludeSubdirectories = true;
    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;

    FileSystemEventHandler onChanged = (sender, e) =>
    {
        // Filter out extraneous files
        if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
            e.FullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            return;
            
        debouncer.Debounce(() => {
            Console.Clear();
            Console.WriteLine($"🔄 Change detected in {Path.GetFileName(e.FullPath)}. Recompiling...");
            CompileAndBundle();
            Console.WriteLine("👀 Watching...");
        });
    };

    watcher.Changed += onChanged;
    watcher.Created += onChanged;
    watcher.Deleted += onChanged;
    watcher.Renamed += (s, e) => onChanged(s, e);
    
    watcher.EnableRaisingEvents = true;
    
    // Prevent exit
    await Task.Delay(-1); 
}

return 0;

void CompileAndBundle()
{
    try
    {
        var hasErrors = false;
        var generatedFiles = new List<string>();
        var entryPoints = new List<string>();
        
        var files = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);
        
        // Step 1: Generate TypeScript
        foreach (var file in files)
        {
            // Skip obj/bin directories
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
        
            // Only compile components
            var content = File.ReadAllText(file);
            if (!content.Contains(": StatefulComponent") && !content.Contains(": StatelessComponent"))
                continue;
        
            // Console.WriteLine($"   ⚙️  Generating {Path.GetFileName(file)}...");
            
            var result = compiler.CompileFile(file);
            
            if (result.Success)
            {
                var tsPath = Path.Combine(intermediateDir, $"{result.ComponentName}.ts");
                File.WriteAllText(tsPath, result.TypeScript);
                generatedFiles.Add(tsPath);
                
                var relativePath = Path.GetRelativePath(sourceDir, file);
                // Simple heuristic: loose files in root or pages/components dirs are entry points
                if (relativePath.StartsWith("Pages") || !relativePath.Contains(Path.DirectorySeparatorChar))
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
        
        if (hasErrors) return;
        
        // Step 2: Bundle with Bun
        if (hasBun && entryPoints.Count > 0)
        {
            // Console.WriteLine($"   📦 Bundling {entryPoints.Count} entry points...");
            
            // Generate source maps and minify
            var bunArgs = $"build {string.Join(" ", entryPoints.Select(p => $"\"{p}\""))} --outdir \"{outputDir}\" --splitting --sourcemap --minify --target browser --external @equantic/runtime";
            
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
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("❌ Bun compilation failed:");
                Console.Error.WriteLine(error);
                return;
            }
            
            // Console.WriteLine(output);
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
