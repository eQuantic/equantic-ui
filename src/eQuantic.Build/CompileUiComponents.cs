using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using eQuantic.UI.Compiler;

namespace eQuantic.Build;

public class CompileUiComponents : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] Sources { get; set; } = Array.Empty<ITaskItem>();

    [Required]
    public string OutputPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        try 
        {
            var compiler = new ComponentCompiler();
            Directory.CreateDirectory(OutputPath);
            
            Log.LogMessage(MessageImportance.High, $"🔨 eQuantic.UI SDK: Compiling {Sources.Length} components to {OutputPath}");

            foreach (var source in Sources)
            {
                var filePath = source.ItemSpec;
                Log.LogMessage(MessageImportance.Normal, $"   ⚙️  Compiling {Path.GetFileName(filePath)}...");
                
                var result = compiler.CompileFile(filePath);
                
                if (result.Success)
                {
                    // Write JavaScript
                    var jsPath = Path.Combine(OutputPath, $"{result.ComponentName}.js");
                    File.WriteAllText(jsPath, result.JavaScript);
                    
                    // Write CSS if present
                    if (!string.IsNullOrEmpty(result.Css))
                    {
                        var cssPath = Path.Combine(OutputPath, $"{result.ComponentName}.css");
                        File.WriteAllText(cssPath, result.Css);
                    }
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        Log.LogError(null, null, null, error.SourcePath, error.Line, error.Column, 0, 0, error.Message);
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex);
            return false;
        }
    }
}
