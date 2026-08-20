using eQuantic.UI.Compiler.Parser;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

    /// <summary>Track L D3: one used resource class, aggregated across every compiled file —
    /// its catalog id, where its Designer lives (the .resx sits beside it), and every key the
    /// reachable tree actually reads (unused keys never ship).</summary>
    public sealed record AggregatedResource(string Id, string DesignerPath)
    {
        public SortedSet<string> Keys { get; } = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<string, AggregatedResource> _resourceUses = new(StringComparer.Ordinal);

    /// <summary>
    /// Transpilation diagnostics → build errors/warnings, for EVERY emit path. The record,
    /// plain-class and static-helper branches each RETURN before the component path's tail, and all
    /// three used to return without draining diagnostics — an EQ error raised while emitting a
    /// record or a static helper never reached <see cref="CompilationResult.Errors"/>, so the build
    /// SUCCEEDED around it. Same disease, third organ: source maps and resource uses had already
    /// been fixed for these branches (see <see cref="CollectResourceUses"/>), and the rule stands —
    /// anything that must happen for every compiled file belongs in a method the branches call.
    /// </summary>
    private static void CollectDiagnostics(CompilationResult result, string sourcePath,
        IEnumerable<ConversionDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var entry = new CompilationError
            {
                Message = diagnostic.Message,
                Code = diagnostic.Code,
                SourcePath = sourcePath,
                Line = diagnostic.Line,
                Column = diagnostic.Column,
            };
            if (diagnostic.Severity == ConversionSeverity.Error)
                result.Errors.Add(entry);
            else
                result.Warnings.Add(entry);
        }

        if (result.Errors.Count > 0)
            result.Success = false;
    }

    /// <summary>
    /// Track L D3: aggregate the rewritten resx reads — catalogs are emitted from exactly this set,
    /// so call sites and catalogs cannot disagree.
    /// <para>
    /// Every emit path drains through HERE, and that is the whole point of the method existing. The
    /// record, plain-class and static-helper branches each RETURN before the component path's tail,
    /// so while this was a loop down there, a string read from a static helper or a data record was
    /// rewritten to a catalog lookup at the call site and then left out of the catalog: the server
    /// rendered it correctly and the browser could not switch it. The identical mistake had already
    /// been made with source maps on the same three branches (see <see cref="AttachSourceMap"/>) —
    /// anything that must happen for EVERY compiled file belongs in a method the branches call, not
    /// in the tail one of them reaches.
    /// </para>
    /// </summary>
    private void CollectResourceUses(IEnumerable<Services.ResourceUse> uses)
    {
        foreach (var use in uses)
        {
            if (!_resourceUses.TryGetValue(use.Id, out var aggregated))
                _resourceUses.Add(use.Id, aggregated = new AggregatedResource(use.Id, use.DesignerPath));
            aggregated.Keys.Add(use.Key);
        }
    }

    /// <summary>Every resource class the compiled tree read, with the keys it read — the CLI
    /// drains this into wwwroot/_equantic/strings/{culture}.json after the last file.</summary>
    public IReadOnlyCollection<AggregatedResource> ResourceUses => _resourceUses.Values;

    /// <summary>Every resource class the parse SAW, used or not (id → Designer path). The CLI
    /// needs both halves: reachable USES decide what an app's own resx contributes (D3 — unused
    /// keys never ship), while a LIBRARY's resource class has no reachable use here at all (its
    /// readers are already transpiled into runtime.js) and contributes whole (D14).</summary>
    public IReadOnlyDictionary<string, string> DiscoveredResources => _parser.DiscoveredResources;

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
    /// Overrides the symbol regime the project compilation implies (see
    /// <see cref="ConversionContext.SymbolsAreAuthoritative"/>). By default a project compilation
    /// means AUTHORITATIVE — unbindable calls are EQ2006 build errors. The one host that must set
    /// this to <c>false</c> is the library-self-transpile pipeline: there the file being compiled
    /// is SOURCE while its dependencies resolve from the library's own dll, so cross-boundary
    /// calls (a source-typed argument into a dll-declared method) can never fully bind — the model
    /// is structurally incomplete, and "unbound" carries no signal.
    /// </summary>
    public bool? SymbolsAreAuthoritative { get; set; }

    /// <summary>
    /// The per-file version of the same demotion, detected rather than declared: an SDK app build
    /// feeds the component LIBRARY's own sources through eqc while the library's dll sits among
    /// the references, so every type in such a file exists TWICE — and a file whose own types are
    /// duplicated can never fully bind (its statics resolve ambiguously; `Spacer.Fixed` inside
    /// ListView.cs stops binding). For that file, "unbound" carries no signal, so it keeps the
    /// heuristics. An APP's files are in no referenced dll, stay un-shadowed, and keep the full
    /// EQ2006 protection.
    /// </summary>
    private static bool FileIsSelfShadowed(SemanticModel? model, SyntaxTree? tree)
    {
        if (model is null || tree is null) return false;

        foreach (var declaration in tree.GetRoot().DescendantNodes()
                     .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax>())
        {
            // Top-level types only: a nested type's metadata name needs its parent chain, and the
            // parent being shadowed already answers the question.
            if (declaration.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax)
                continue;
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol declared)
                continue;

            var ns = declared.ContainingNamespace;
            var fullName = ns is { IsGlobalNamespace: false }
                ? ns.ToDisplayString() + "." + declared.MetadataName
                : declared.MetadataName;
            // Compilation.GetTypeByMetadataName cannot answer this — it PREFERS source, so the
            // duplicate hides behind it. Ask each referenced assembly directly: the file's own
            // type existing in a reference is the duplicate world, whatever name lookup prefers.
            foreach (var reference in model.Compilation.References)
            {
                if (model.Compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly
                    && assembly.GetTypeByMetadataName(fullName) is not null)
                {
                    return true;
                }
            }
        }

        return false;
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
        return CompileAll(components);
    }

    /// <summary>
    /// Every parsed component through <see cref="Compile"/>, with one file-level check first: two
    /// declarations of the SAME name (partial halves, above all — C# 13/14 partial properties,
    /// constructors and events make them likely) each emit a WHOLE module today, and whichever
    /// lands last silently wins. Until eqc merges partials, that is a build error, not a roulette.
    /// Cross-FILE partials still pass through here unseen — each file compiles alone.
    /// </summary>
    private IEnumerable<CompilationResult> CompileAll(IEnumerable<ComponentDefinition> components)
    {
        var list = components.ToList();
        var duplicated = list.GroupBy(c => c.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var component in list)
        {
            var result = Compile(component);
            if (duplicated.Contains(component.Name))
            {
                result.Success = false;
                result.Errors.Add(new CompilationError
                {
                    Code = "EQ2009",
                    SourcePath = component.SourcePath,
                    Message = $"'{component.Name}' is declared more than once in this file (partial "
                        + "declarations?). eqc emits one module per declaration and cannot merge "
                        + "them yet — combine the members into a single declaration.",
                });
            }
            yield return result;
        }
    }
    
    /// <summary>
    /// Compile from source code
    /// </summary>
    /// <summary>Emit TypeScript annotations (default) or plain browser-ready JavaScript.</summary>
    public bool TypeAnnotations
    {
        get => _tsEmitter.TypeAnnotations;
        set => _tsEmitter.TypeAnnotations = value;
    }

    /// <summary>
    /// Emit every node construction wrapped so the built node carries the source span that made it
    /// (<c>VisualNode.Origin</c>) — the identity a visual editor needs to answer "which C# made this
    /// pixel", and which source maps cannot give: the whole Build body is emitted as one span.
    /// <para>
    /// Off, and only a design tool turns it on. The wrapper is real emitted code; an SDK build that
    /// shipped it would be putting a design tool's concern in every user's bundle.
    /// </para>
    /// </summary>
    public bool DesignMode
    {
        get => _tsEmitter.DesignMode;
        set => _tsEmitter.DesignMode = value;
    }

    /// <summary>
    /// The C# errors in this source, as the language itself sees them — CS1002, CS0103, CS0246 —
    /// anchored to their line. Roslyn parses leniently, so eqc will happily transpile a file with
    /// a syntax error and emit a component built from a tree it only half understood; the result
    /// is JavaScript that mounts and throws, which reads as "it just went blank".
    ///
    /// A host that compiles C# on its own (the SDK, where csc runs right after) can ignore this.
    /// A host where eqc IS the whole build — the playground — must ask before it emits.
    /// </summary>
    public IReadOnlyList<CompilationError> GetLanguageErrors(string sourceCode, string sourcePath = "")
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, _semanticModelProvider.JoinOptions, path: sourcePath);
        return _semanticModelProvider.GetLanguageDiagnostics(tree)
            .Select(diagnostic =>
            {
                var position = diagnostic.Location.GetLineSpan().StartLinePosition;
                return new CompilationError
                {
                    Message = diagnostic.GetMessage(),
                    Code = diagnostic.Id,
                    SourcePath = sourcePath,
                    Line = position.Line + 1,
                    Column = position.Character + 1,
                };
            })
            .ToArray();
    }

    public IEnumerable<CompilationResult> CompileSource(string sourceCode, string sourcePath = "")
    {
        var components = _parser.ParseSource(sourceCode, sourcePath);
        return CompileAll(components);
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
            // Regime for every emit below: a host that handed over the project's compilation
            // promised completeness, so unbindable calls are errors; the minimal fallback resolves
            // almost nothing by construction, so heuristics stay legal there. A host may override
            // (see the property) — the library-self-transpile pipeline must — and a file whose own
            // types are duplicated by a reference demotes itself (see FileIsSelfShadowed).
            var authoritative = SymbolsAreAuthoritative ?? _semanticModelProvider.HasProjectCompilation;
            if (authoritative && component.SyntaxTree != null
                && FileIsSelfShadowed(_semanticModelProvider.GetSemanticModel(component.SyntaxTree), component.SyntaxTree))
            {
                authoritative = false;
            }
            _tsEmitter.SymbolsAreAuthoritative = authoritative;

            // C# 15 union — a TS union alias module: implicit case conversions are no-ops in JS,
            // and `pet switch { Dog d => …, Cat c => … }` already tests instanceof per case type.
            if (component.IsUnionType
                && component.ValueTypeSyntax is Microsoft.CodeAnalysis.CSharp.Syntax.UnionDeclarationSyntax union)
            {
                result.TypeScript = EmitUnionAlias(union, TypeAnnotations, out var unionError);
                if (unionError is not null)
                {
                    result.Success = false;
                    result.Errors.Add(new CompilationError
                    {
                        Code = "EQ2008",
                        SourcePath = component.SourcePath,
                        Message = unionError,
                    });
                    return result;
                }
                result.Success = true;
                return result;
            }

            // User value type (record/struct) — emit as a standalone named-class module.
            if (component.IsRecordType && component.ValueTypeSyntax != null)
            {
                var recordConverter = new CSharpToJsConverter
                {
                    SymbolsAreAuthoritative = authoritative,
                };
                if (component.SyntaxTree != null)
                    recordConverter.SetSemanticModel(_semanticModelProvider.GetSemanticModel(component.SyntaxTree));
                // TypeAnnotations flows here too: a record emitted as TypeScript is a parse error
                // for a consumer that runs the module directly, and nothing upstream would notice.
                result.TypeScript = new RecordTypeEmitter(recordConverter)
                    .EmitModule(component.ValueTypeSyntax, TypeAnnotations);
                CollectResourceUses(recordConverter.ResourceUses);
                result.Success = true;
                CollectDiagnostics(result, component.SourcePath, recordConverter.Diagnostics);
                return result;
            }

            // Plain class — its own module of INSTANCE members.
            if (component.IsPlainClass &&
                component.ValueTypeSyntax is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax plainClass)
            {
                var pm = component.SyntaxTree != null ? _semanticModelProvider.GetSemanticModel(component.SyntaxTree) : null;
                result.TypeScript = _tsEmitter.EmitPlainClassModule(plainClass, pm);
                CollectResourceUses(_tsEmitter.GetLastResourceUses());
                AttachSourceMap(result, component);
                result.Success = true;
                CollectDiagnostics(result, component.SourcePath, _tsEmitter.GetLastDiagnostics());
                return result;
            }

            // Static utility class — emit as its own module of static members.
            if (component.IsStaticHelper &&
                component.ValueTypeSyntax is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax staticClass)
            {
                var sm = component.SyntaxTree != null ? _semanticModelProvider.GetSemanticModel(component.SyntaxTree) : null;
                result.TypeScript = _tsEmitter.EmitStaticHelperModule(staticClass, sm);
                CollectResourceUses(_tsEmitter.GetLastResourceUses());
                AttachSourceMap(result, component);
                result.Success = true;
                CollectDiagnostics(result, component.SourcePath, _tsEmitter.GetLastDiagnostics());
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
            CollectDiagnostics(result, component.SourcePath, _tsEmitter.GetLastDiagnostics());

            CollectResourceUses(_tsEmitter.GetLastResourceUses());

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
    /// The module a C# 15 union lowers to. The TYPE half is the TS union of the case types; the
    /// CONST half exists so a value-level import of the name never dangles in plain-JS output
    /// (type-only exports vanish there, and an `import { Pet }` against an empty module dies at
    /// load). Case members beyond the case list (a union BODY) aren't lowered yet.
    /// </summary>
    private static string EmitUnionAlias(
        Microsoft.CodeAnalysis.CSharp.Syntax.UnionDeclarationSyntax union,
        bool typeAnnotations, out string? error)
    {
        error = null;
        var cases = (union.ParameterList?.Parameters ?? default)
            .Select(parameter => parameter.Type?.ToString() ?? "")
            .Where(name => name.Length > 0)
            .ToArray();
        if (cases.Length == 0)
        {
            error = $"union '{union.Identifier.Text}' declares no case types the compiler can lower.";
            return string.Empty;
        }
        if (union.Members.Count > 0)
        {
            error = $"union '{union.Identifier.Text}' declares members — union bodies are not lowered yet.";
            return string.Empty;
        }

        var name = union.Identifier.Text;
        var builder = new System.Text.StringBuilder();
        if (typeAnnotations)
        {
            foreach (var caseType in cases)
                builder.Append($"import {{ {caseType} }} from \"./{caseType}\";\n");
            builder.Append('\n');
            builder.Append($"export type {name} = {string.Join(" | ", cases)};\n");
        }
        builder.Append($"export const {name} = undefined;\n");
        return builder.ToString();
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
