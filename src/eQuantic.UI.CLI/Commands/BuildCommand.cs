using eQuantic.UI.Compiler;

namespace eQuantic.UI.CLI.Commands;

public static class BuildCommand
{
    public static void Execute(string input, string output, bool watch)
    {
        Console.WriteLine("🔨 eQuantic.UI Compiler");
        Console.WriteLine($"   Input:  {Path.GetFullPath(input)}");
        Console.WriteLine($"   Output: {Path.GetFullPath(output)}");
        
        if (!Directory.Exists(input) && !File.Exists(input))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Input path not found: {input}");
            Console.ResetColor();
            return;
        }
        
        var compiler = new ComponentCompiler();
        var outputDir = Path.GetFullPath(output);
        Directory.CreateDirectory(outputDir);
        
        // Find all .cs files that look like components
        // In a real CLI this would use a proper project analysis, but for standalone CLI simple grep is fine
        var files = (Directory.Exists(input)
            ? Directory.GetFiles(input, "*.cs", SearchOption.AllDirectories)
            : new[] { input })
            .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/") && 
                       (File.ReadAllText(f).Contains(": StatefulComponent") || 
                        File.ReadAllText(f).Contains(": StatelessComponent") || 
                        File.ReadAllText(f).Contains("[Component]")))
            .ToArray();
        
        if (files.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  No component files found");
            Console.ResetColor();
            return;
        }
        
        Console.WriteLine($"   Found {files.Length} component file(s)");
        Console.WriteLine();
        
        var successCount = 0;
        var errorCount = 0;
        
        foreach (var file in files)
        {
            Console.Write($"   ⚙️  Compiling {Path.GetFileName(file)}...");
            
            try
            {
                var result = compiler.CompileFile(file);
                
                if (result.Success)
                {
                    // Create intermediate directory for TS files
                    var intermediateDir = Path.Combine(outputDir, "obj", "ts");
                    Directory.CreateDirectory(intermediateDir);
                    
                    // Write TypeScript to intermediate folder
                    var tsPath = Path.Combine(intermediateDir, $"{result.ComponentName}.ts");
                    File.WriteAllText(tsPath, result.TypeScript);
                    
                    // Bundle with Bun to final output
                    // e.g. dist/Counter.js
                    var bundled = Services.BunBundler.BundleAsync(tsPath, outputDir).GetAwaiter().GetResult();
                    
                    if (!bundled)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(" ✗ (Bun Error)");
                        Console.ResetColor();
                        errorCount++;
                        continue;
                    }
                    
                    // Write CSS if present
                    if (!string.IsNullOrEmpty(result.Css))
                    {
                        var cssPath = Path.Combine(outputDir, $"{result.ComponentName}.css");
                        File.WriteAllText(cssPath, result.Css);
                    }
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(" ✓");
                    Console.ResetColor();
                    successCount++;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(" ✗");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"      Error: {error.Message}");
                    }
                    Console.ResetColor();
                    errorCount++;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" ✗ {ex.Message}");
                Console.ResetColor();
                errorCount++;
            }
        }
        
        Console.WriteLine();
        Console.WriteLine($"   ✅ {successCount} compiled, ❌ {errorCount} errors");
        
        if (watch)
        {
            Console.WriteLine();
            Console.WriteLine("👀 Watching for changes... (Ctrl+C to stop)");
            WatchForChanges(input, output, compiler);
        }
    }
    
    private static void WatchForChanges(string input, string output, ComponentCompiler compiler)
    {
        var watcher = new FileSystemWatcher
        {
            Path = Directory.Exists(input) ? input : Path.GetDirectoryName(input)!,
            Filter = "*.cs",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        
        watcher.Changed += (_, e) => RecompileFile(e.FullPath, output, compiler);
        watcher.Created += (_, e) => RecompileFile(e.FullPath, output, compiler);
        
        // Keep the app running
        var exitEvent = new ManualResetEvent(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exitEvent.Set();
        };
        exitEvent.WaitOne();
    }
    
    private static void RecompileFile(string filePath, string output, ComponentCompiler compiler)
    {
        // Simple optimization check
        if (!File.ReadAllText(filePath).Contains("Component")) return;

        Console.WriteLine($"   🔄 Recompiling {Path.GetFileName(filePath)}...");
        
        try
        {
            Thread.Sleep(100); // Debounce
            var result = compiler.CompileFile(filePath);
            
            if (result.Success)
            {
                var intermediateDir = Path.Combine(output, "obj", "ts");
                Directory.CreateDirectory(intermediateDir);
                var tsPath = Path.Combine(intermediateDir, $"{result.ComponentName}.ts");
                File.WriteAllText(tsPath, result.TypeScript);
                
                Services.BunBundler.BundleAsync(tsPath, output).GetAwaiter().GetResult();
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   ✓ {Path.GetFileName(filePath)} recompiled");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ✗ {ex.Message}");
            Console.ResetColor();
        }
    }
}
