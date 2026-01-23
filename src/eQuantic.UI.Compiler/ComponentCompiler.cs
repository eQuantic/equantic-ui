using eQuantic.UI.Compiler.Parser;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Models;

using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler;

/// <summary>
/// Main compiler class that orchestrates parsing, analysis, and code generation
/// </summary>
public class ComponentCompiler
{
    private readonly ComponentParser _parser;
    private readonly TypeScriptEmitter _tsEmitter;
    private readonly CssEmitter _cssEmitter;
    private readonly SemanticModelProvider _semanticModelProvider;
    
    public ComponentCompiler()
    {
        _parser = new ComponentParser();
        _tsEmitter = new TypeScriptEmitter();
        _cssEmitter = new CssEmitter();
        _semanticModelProvider = new SemanticModelProvider();
    }
    
    /// <summary>
    /// Compile a single .eqx file
    /// </summary>
    public IEnumerable<CompilationResult> CompileFile(string filePath)
    {
        var components = _parser.Parse(filePath);
        return components.Select(Compile);
    }
    
    /// <summary>
    /// Compile from source code
    /// </summary>
    public IEnumerable<CompilationResult> CompileSource(string sourceCode, string sourcePath = "")
    {
        var components = _parser.ParseSource(sourceCode, sourcePath);
        return components.Select(Compile);
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
            // Semantic Analysis
            Microsoft.CodeAnalysis.SemanticModel? semanticModel = null;
            if (component.SyntaxTree != null)
            {
                semanticModel = _semanticModelProvider.GetSemanticModel(component.SyntaxTree);
                
                // Validate Component Rules
                var validator = new SemanticValidator(semanticModel);
                var semanticErrors = validator.Validate(component);
                
                if (semanticErrors.Count > 0)
                {
                    result.Success = false;
                    result.Errors.AddRange(semanticErrors);
                    return result;
                }
            }

            // Generate TypeScript (preferred for Bun bundling)
            result.TypeScript = _tsEmitter.Emit(component, semanticModel);
            
            // JavaScript generation is now handled by Bun in the build pipeline
            // result.JavaScript is empty here, but will be populated by Bun output later if needed
            
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
    /// Compile all .cs and .eqx files in a directory
    /// </summary>
    public IEnumerable<CompilationResult> CompileDirectory(string directoryPath, bool recursive = true)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        // Search for both .eqx (legacy) and .cs (new)
        var files = Directory.GetFiles(directoryPath, "*.cs", searchOption)
            .Concat(Directory.GetFiles(directoryPath, "*.eqx", searchOption));
        
        foreach (var file in files)
        {
            // Skip obj/bin directories to avoid re-parsing generated code or unrelated files
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || 
                file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            // Only compile if it looks like a component (optimization)
            var content = File.ReadAllText(file);
            if (content.Contains(": StatefulComponent") || content.Contains(": StatelessComponent") || content.Contains(": HtmlElement"))
            {
                foreach (var result in CompileFile(file))
                {
                    yield return result;
                }
            }
        }
    }
    
    /// <summary>
    /// Compile and write output files
    /// </summary>
    public void CompileAndWrite(string inputPath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        
        var results = CompileFile(inputPath);
        
        foreach (var result in results)
        {
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
}

/// <summary>
/// Result of compilation
/// </summary>
public class CompilationResult
{
    public bool Success { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string TypeScript { get; set; } = string.Empty;
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
