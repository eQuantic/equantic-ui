using System.Diagnostics;

namespace eQuantic.UI.CLI.Services;

/// <summary>
/// Wrapper for Bun CLI commands.
/// </summary>
public static class BunBundler
{
    /// <summary>
    /// Bundles a TypeScript file using Bun.
    /// </summary>
    public static async Task<bool> BundleAsync(string inputPath, string outputDir)
    {
        var inputFileName = Path.GetFileName(inputPath);
        var inputDir = Path.GetDirectoryName(inputPath) ?? ".";
        
        // Command: bun build ./Input.ts --outdir ./dist --target browser
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "bun",
            Arguments = $"build \"{inputPath}\" --outdir \"{outputDir}\" --target browser --sourcemap=inline",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = inputDir
        };

        try
        {
            using var process = new Process();
            process.StartInfo = processStartInfo;
            
            var output = new List<string>();
            var errors = new List<string>();
            
            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.Add(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) errors.Add(e.Data); };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"   ❌ Bun build failed for {inputFileName}:");
                foreach (var error in errors) Console.WriteLine($"      {error}");
                Console.ResetColor();
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ❌ Failed to invoke Bun: {ex.Message}");
            Console.WriteLine("      Make sure Bun is installed and in your PATH.");
            Console.ResetColor();
            return false;
        }
    }
}
