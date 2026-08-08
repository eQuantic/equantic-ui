using eQuantic.UI.Compiler.Parser;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;

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
    private readonly SourceMapGenerator _sourceMapGenerator;

    /// <summary>
    /// The C#→TS map for whatever the emitter just produced. One seam for every module kind: the
    /// plain-class and static-helper branches used to RETURN before the component path's map
    /// generation ran, so an app's helpers (its ConsoleShell, its data shapers) shipped mapless —
    /// the error overlay could walk a page's frame back to C# and stopped dead on a helper's.
    /// </summary>
    private void AttachSourceMap(CompilationResult result, Models.ComponentDefinition component)
    {
        var mappings = _tsEmitter.GetLastMappings();
        if (mappings.Any() && component.SyntaxTree != null)
        {
            var sourceContent = component.SyntaxTree.GetText().ToString();
            result.SourceMap = _sourceMapGenerator.Generate(
                $"{component.Name}.ts", component.SourcePath, mappings, sourceContent);
        }
    }

    public ComponentCompiler()
    {
        // The provider must exist BEFORE the parser receives it — the old order handed the parser a
        // null provider, silently disabling every semantic-model path in parsing (base-type walks,
        // runtime-provided type discovery) and leaving only the syntactic heuristics.
        _semanticModelProvider = new SemanticModelProvider();
        _parser = new ComponentParser();
        _parser.SetSemanticModelProvider(_semanticModelProvider);
        _tsEmitter = new TypeScriptEmitter();
        _cssEmitter = new CssEmitter();
        _sourceMapGenerator = new SourceMapGenerator();
    }

    /// <summary>
    /// Gets the style provider registry for manual provider registration.
    /// </summary>

    /// <summary>
    /// Sets the full project compilation to enable resolution of external types.
    /// When set, the compiler can resolve types defined in other files in the project.
    /// </summary>
    /// <param name="projectCompilation">The full Roslyn compilation of the project</param>
    public void SetProjectCompilation(Compilation projectCompilation)
    {
        _semanticModelProvider.SetProjectCompilation(projectCompilation);
    }

    /// <summary>
    /// Clears the project compilation, reverting to minimal compilation mode.
    /// </summary>
    public void ClearProjectCompilation()
    {
        _semanticModelProvider.ClearProjectCompilation();
    }

    /// <summary>
    /// Sets the dependency resolver for automatic component dependency detection
    /// </summary>
    public void SetDependencyResolver(ComponentDependencyResolver resolver)
    {
        _tsEmitter.SetDependencyResolver(resolver);
    }

    /// <summary>
    /// Compile a single source file
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
            Namespace = component.Namespace,
            IsPage = component.PageRoutes.Count > 0,
        };
        
        try
        {
            // User value type (record/struct) — emit as a standalone named-class module.
            if (component.IsRecordType && component.ValueTypeSyntax != null)
            {
                var recordConverter = new CSharpToJsConverter();
                if (component.SyntaxTree != null)
                    recordConverter.SetSemanticModel(_semanticModelProvider.GetSemanticModel(component.SyntaxTree));
                result.TypeScript = new RecordTypeEmitter(recordConverter).EmitModule(component.ValueTypeSyntax);
                result.Success = true;
                return result;
            }

            // Plain class — its own module of INSTANCE members.
            if (component.IsPlainClass &&
                component.ValueTypeSyntax is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax plainClass)
            {
                var pm = component.SyntaxTree != null ? _semanticModelProvider.GetSemanticModel(component.SyntaxTree) : null;
                result.TypeScript = _tsEmitter.EmitPlainClassModule(plainClass, pm);
                AttachSourceMap(result, component);
                result.Success = true;
                return result;
            }

            // Static utility class — emit as its own module of static members.
            if (component.IsStaticHelper &&
                component.ValueTypeSyntax is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax staticClass)
            {
                var sm = component.SyntaxTree != null ? _semanticModelProvider.GetSemanticModel(component.SyntaxTree) : null;
                result.TypeScript = _tsEmitter.EmitStaticHelperModule(staticClass, sm);
                AttachSourceMap(result, component);
                result.Success = true;
                return result;
            }

            // Semantic Analysis
            SemanticModel? semanticModel = null;
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
            var tsBuilder = new TypeScriptCodeBuilder();
            // We need a refactor here: TypeScriptEmitter currently creates its own builder.
            // Let's modify Emitter to take a builder or return mapping data.
            // For now, let's assume TypeScriptEmitter is updated to expose the builder or mappings.
            // I'll need to check TypeScriptEmitter.Emit again.
            
            result.TypeScript = _tsEmitter.Emit(component, semanticModel);

            // Collect transpilation diagnostics (unconverted or impossible constructs). Errors fail
            // the build; warnings are surfaced but do not. Replaces silent verbatim fallbacks.
            foreach (var diagnostic in _tsEmitter.GetLastDiagnostics())
            {
                var entry = new CompilationError
                {
                    Message = diagnostic.Message,
                    Code = diagnostic.Code,
                    SourcePath = component.SourcePath,
                    Line = diagnostic.Line,
                    Column = diagnostic.Column,
                };
                if (diagnostic.Severity == ConversionSeverity.Error)
                    result.Errors.Add(entry);
                else
                    result.Warnings.Add(entry);
            }

            if (result.Errors.Count > 0)
            {
                result.Success = false;
                return result;
            }

            // Generate Source Map
            AttachSourceMap(result, component);
            
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
    /// Compile every .cs file in a directory
    /// </summary>
    public IEnumerable<CompilationResult> CompileDirectory(string directoryPath, bool recursive = true)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, "*.cs", searchOption);
        
        foreach (var file in files)
        {
            // Skip obj/bin directories to avoid re-parsing generated code or unrelated files
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            // Try to compile - parser will return empty if not a component
            // No restrictions on naming, inheritance, aliases, or patterns
            foreach (var result in CompileFile(file))
            {
                yield return result;
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
                
                if (!string.IsNullOrEmpty(result.SourceMap))
                {
                    var mapPath = Path.Combine(outputDir, $"{result.ComponentName}.js.map");
                    File.WriteAllText(mapPath, result.SourceMap);
                    
                    // Add sourceMappingURL to the end of the JS file if we were writing JS
                    // But we are currently writing TypeScript/Source as 'JavaScript' property in some places?
                    // result.JavaScript is empty in Compile() though.
                }

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

    /// <summary>
    /// Carries a <c>[Page]</c> route, so the bundler must give it an ENTRY POINT: the server loads a
    /// page's module by name, and a module that was only transpiled is a 404 the build never
    /// mentioned. Where the file sits in the project has nothing to do with it.
    /// </summary>
    public bool IsPage { get; set; }
    public string TypeScript { get; set; } = string.Empty;
    public string JavaScript { get; set; } = string.Empty;
    public string? SourceMap { get; set; }
    public string? Css { get; set; }
    public List<CompilationError> Errors { get; set; } = new();
    public List<CompilationError> Warnings { get; set; } = new();
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

    /// <summary>Stable diagnostic id (e.g. <c>EQ2001</c>), or empty for legacy errors.</summary>
    public string Code { get; set; } = string.Empty;
}
