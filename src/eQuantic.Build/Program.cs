// CLI entry point for eQuantic.UI Compiler
using eQuantic.UI.Compiler;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: eqc <source-dir> <output-dir> [--bun <path>]");
    Console.Error.WriteLine("\nOptions:");
    Console.Error.WriteLine("  <source-dir>   Directory containing .cs component files");
    Console.Error.WriteLine("  <output-dir>   Directory for compiled output (.ts/.js files)");
    Console.Error.WriteLine("  --bun <path>   Path to Bun executable for TypeScript bundling");
    return 1;
}

var sourceDir = args[0];
var outputDir = args[1];
var bunPath = args.Length > 3 && args[2] == "--bun" ? args[3] : null;

if (!Directory.Exists(sourceDir))
{
    Console.Error.WriteLine($"Error: Source directory not found: {sourceDir}");
    return 1;
}

Directory.CreateDirectory(outputDir);

var compiler = new ComponentCompiler();
var hasBun = !string.IsNullOrEmpty(bunPath) && File.Exists(bunPath);
var mode = hasBun ? "TypeScript" : "JavaScript";

Console.WriteLine($"🔨 eQuantic.UI: Compiling components [{mode}]");

var hasErrors = false;
var files = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);

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

    Console.WriteLine($"   ⚙️  Compiling {Path.GetFileName(file)}...");
    
    var result = compiler.CompileFile(file);
    
    if (result.Success)
    {
        // Write TypeScript if Bun is available, otherwise JavaScript
        if (hasBun && !string.IsNullOrEmpty(result.TypeScript))
        {
            var tsPath = Path.Combine(outputDir, $"{result.ComponentName}.ts");
            File.WriteAllText(tsPath, result.TypeScript);
        }
        else
        {
            var jsPath = Path.Combine(outputDir, $"{result.ComponentName}.js");
            File.WriteAllText(jsPath, result.JavaScript);
        }
        
        // Write CSS if present
        if (!string.IsNullOrEmpty(result.Css))
        {
            var cssPath = Path.Combine(outputDir, $"{result.ComponentName}.css");
            File.WriteAllText(cssPath, result.Css);
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

Console.WriteLine(hasErrors ? "❌ Compilation completed with errors." : "✅ Compilation completed successfully.");
return hasErrors ? 1 : 0;
