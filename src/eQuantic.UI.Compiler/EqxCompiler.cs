using eQuantic.UI.Compiler.Parser;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Models;

namespace eQuantic.UI.Compiler;

/// <summary>
/// Main compiler class that orchestrates parsing, analysis, and code generation
/// </summary>
public class EqxCompiler
{
    private readonly EqxParser _parser;
    private readonly JavaScriptEmitter _jsEmitter;
    private readonly CssEmitter _cssEmitter;
    
    public EqxCompiler()
    {
        _parser = new EqxParser();
        _jsEmitter = new JavaScriptEmitter();
        _cssEmitter = new CssEmitter();
    }
    
    /// <summary>
    /// Compile a single .eqx file
    /// </summary>
    public CompilationResult CompileFile(string filePath)
    {
        var component = _parser.Parse(filePath);
        return Compile(component);
    }
    
    /// <summary>
    /// Compile from source code
    /// </summary>
    public CompilationResult CompileSource(string sourceCode, string sourcePath = "")
    {
        var component = _parser.ParseSource(sourceCode, sourcePath);
        return Compile(component);
    }
    
    /// <summary>
    /// Compile a parsed component definition
    /// </summary>
    public CompilationResult Compile(ComponentDefinition component)
    {
        var result = new CompilationResult
        {
            ComponentName = component.Name,
            Namespace = component.Namespace
        };
        
        try
        {
            // Generate JavaScript
            result.JavaScript = _jsEmitter.Emit(component);
            
            // Generate CSS from StyleClass usages
            if (component.StyleUsages.Count > 0)
            {
                result.Css = _cssEmitter.Emit(component.StyleUsages);
            }
            
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new CompilationError
            {
                Message = ex.Message,
                SourcePath = component.SourcePath
            });
        }
        
        return result;
    }
    
    /// <summary>
    /// Compile all .eqx files in a directory
    /// </summary>
    public IEnumerable<CompilationResult> CompileDirectory(string directoryPath, bool recursive = true)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, "*.eqx", searchOption);
        
        foreach (var file in files)
        {
            yield return CompileFile(file);
        }
    }
    
    /// <summary>
    /// Compile and write output files
    /// </summary>
    public void CompileAndWrite(string inputPath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        
        var result = CompileFile(inputPath);
        
        if (result.Success)
        {
            var jsPath = Path.Combine(outputDir, $"{result.ComponentName}.js");
            File.WriteAllText(jsPath, result.JavaScript);
            
            if (!string.IsNullOrEmpty(result.Css))
            {
                var cssPath = Path.Combine(outputDir, $"{result.ComponentName}.css");
                File.WriteAllText(cssPath, result.Css);
            }
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"Error in {error.SourcePath}: {error.Message}");
            }
        }
    }
}

/// <summary>
/// Result of compilation
/// </summary>
public class CompilationResult
{
    public bool Success { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string JavaScript { get; set; } = string.Empty;
    public string? Css { get; set; }
    public List<CompilationError> Errors { get; set; } = new();
}

/// <summary>
/// Compilation error
/// </summary>
public class CompilationError
{
    public string Message { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}
