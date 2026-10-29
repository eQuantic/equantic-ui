using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.Parser;

/// <summary>
/// Parser for component files using Roslyn
/// </summary>
public class ComponentParser
{
    private SemanticModelProvider? _semanticModelProvider;

    /// <summary>
    /// Every resource class the parse SAW, by catalog id → Designer path (Track L D2/D14). These
    /// are skipped as modules, but the catalog emitter needs them: a LIBRARY's resource class is
    /// read only by components already transpiled into <c>runtime.js</c>, so no reachable use in
    /// this compilation would ever mention it, and the SDK's own strings would silently miss every
    /// app's catalogs.
    /// </summary>
    public Dictionary<string, string> DiscoveredResources { get; } = new(StringComparer.Ordinal);

    /// <summary>The catalogue ids of the build, where it has more than one compilation; null where a
    /// class's own compilation answers (<see cref="Services.ResourceCatalogIds"/>).</summary>
    internal Services.ResourceCatalogIds? ResourceIds { get; set; }

    private string CatalogIdOf(SemanticModel model, INamedTypeSymbol resourceClass) =>
        ResourceIds?.IdFor(resourceClass) ?? Services.ResourceClasses.IdFor(model.Compilation, resourceClass);

    /// <summary>
    /// Supplies a semantic model provider so component detection can walk the base-type chain (via the
    /// project compilation, which resolves library bases) instead of matching base-type name strings.
    /// </summary>
    public void SetSemanticModelProvider(SemanticModelProvider provider) => _semanticModelProvider = provider;

    /// <summary>Semantic model for the tree (via the project compilation), or null to fall back to the
    /// syntactic heuristic — never throws.</summary>
    private SemanticModel? TryGetSemanticModel(SyntaxTree tree)
    {
        if (_semanticModelProvider == null) return null;
        try { return _semanticModelProvider.GetSemanticModel(tree); }
        catch { return null; }
    }

    /// <summary>
    /// Parse a source file and extract component definitions
    /// </summary>
    public IEnumerable<ComponentDefinition> Parse(string filePath)
    {
        var sourceCode = File.ReadAllText(filePath);
        return ParseSource(sourceCode, filePath);
    }
    
    /// <summary>
    /// Parse source code and extract component definitions
    /// </summary>
    public IEnumerable<ComponentDefinition> ParseSource(string sourceCode, string sourcePath = "")
    {
        // The provider knows which language version the surrounding compilation speaks — a tree
        // that will JOIN it must speak the same one (mixed versions are an ArgumentException).
        var options = _semanticModelProvider?.JoinOptions ?? Services.ParseDefaults.Options;
        var tree = CSharpSyntaxTree.ParseText(sourceCode, options, path: sourcePath);
        var root = tree.GetCompilationUnitRoot();
        var results = new List<ComponentDefinition>();
        
        // Extract namespace
        string? ns = null;
        var namespaceDecl = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        
        if (namespaceDecl != null)
        {
            ns = namespaceDecl.Name.ValueText();
        }
        else
        {
            var blockNamespace = root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault();
            if (blockNamespace != null)
            {
                ns = blockNamespace.Name.ValueText();
            }
        }
        
        var model = TryGetSemanticModel(tree);

        // Discover user value types: records (positional or body) and structs are emitted as named JS
        // classes. Reactive — driven by the declarations actually present, not a fixed list. One marked
        // [ServerOnly] has none, whichever of its declarations says so, as the resolver reads it.
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (typeDecl is (RecordDeclarationSyntax or StructDeclarationSyntax)
                && RecordTypeEmitter.CanEmit(typeDecl, ProjectSymbol(typeDecl, model)))
            {
                results.Add(new ComponentDefinition
                {
                    // Its twin's name, its owners' and its own (#584): a nested record's module never
                    // meets a top-level record's of its name.
                    Name = typeDecl.TwinTypeName(),
                    SourcePath = sourcePath,
                    SyntaxTree = tree,
                    Namespace = ns ?? "",
                    IsRecordType = true,
                    ValueTypeSyntax = typeDecl,
                });
            }
        }

        // C# 15 unions: `union Pet(Cat, Dog);` — the case list rides in the declaration's parameter
        // list, and ComponentCompiler emits the alias module (a TS union IS the faithful lowering).
        foreach (var union in root.DescendantNodes().OfType<UnionDeclarationSyntax>())
        {
            results.Add(new ComponentDefinition
            {
                Name = union.Identifier.ValueText,
                SourcePath = sourcePath,
                SyntaxTree = tree,
                Namespace = ns ?? "",
                IsUnionType = true,
                ValueTypeSyntax = union,
            });
        }

        // Discover static utility classes (`static class Format { … }`) used from components — emitted as
        // their own module of static members so `Format.Foo()` resolves at runtime. A NESTED one is a
        // module of its own too, named by its owner (`Section$Copy`, #584), so two sections' `Copy`
        // never write one file, where its owner crosses: one inside a server-only class has none.
        var helperModel = TryGetSemanticModel(tree);
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (!Services.PlainClassModule.OwnersCross(classDecl)
                || (ProjectSymbol(classDecl, model) is { } nestedHelper && Services.PlainClassModule.OwnerKeptOut(nestedHelper)))
                continue;
            // Track L D2: a resx Designer class is NOT a static-helper module — its accessors
            // rewrite to $eq.str at the use site, and a module of ResourceManager.GetString calls
            // cannot run in a browser. Shape-detected, and checked FIRST because the Designer's
            // class itself is not static (only its members are) — but a PublicResXFileCodeGenerator
            // variant marked static must not slip through either.
            if (helperModel?.GetDeclaredSymbol(classDecl) is INamedTypeSymbol declared
                && Services.ResourceClasses.IsResourceClass(declared))
            {
                // Skipped as a module, RECORDED as a resource class. A library's resource class is
                // seen here and used nowhere the app compiles (its components are transpiled into
                // runtime.js at the SDK's own build), so the catalog emitter has no reachable use
                // to learn it from — and D14 promises the SDK's strings ride every app's catalogs.
                DiscoveredResources.TryAdd(
                    CatalogIdOf(helperModel, declared),
                    Services.ResourceClasses.DesignerPathFor(declared));
                continue;
            }
            // [ServerOnly] on the class: it never crosses, so no module — the class-level twin of the
            // method rule below, for the Roslyn service or hosted warm-up that lives in the web project.
            if (IsServerOnly(classDecl)) continue;
            // [RuntimeProvided] static helpers already exist in @equantic/runtime; emitting a per-app
            // module here would create a second implementation of the same exported runtime name.
            if (IsRuntimeProvided(classDecl)) continue;
            if (classDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                results.Add(new ComponentDefinition
                {
                    Name = classDecl.TwinTypeName(),
                    SourcePath = sourcePath,
                    SyntaxTree = tree,
                    Namespace = ns ?? "",
                    IsStaticHelper = true,
                    ValueTypeSyntax = classDecl,
                });
            }
        }

        // Find component classes. Prefer a semantic base-type walk (resolves library bases like Flex /
        // Container and any user-defined intermediate component) over matching base-type name strings;
        // fall back to the syntactic heuristic when no semantic model is available.
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

        // Pre-pass: which classes are components. A class is one if its base resolves to a known component
        // base (semantic walk) / matches a base-name heuristic / it declares Build|Render where nothing can
        // bind its chain — AND, transitively, any class extending another in-file component (so a subclass of a
        // user component is recognized even without its own Build and without relying on the semantic model
        // resolving the full base chain).
        string? BaseName(ClassDeclarationSyntax c) => c.BaseList?.Types.FirstOrDefault()?.Type.TwinTypeName(model);
        bool OwnersAdmit(ClassDeclarationSyntax c) =>
            c.Parent is not TypeDeclarationSyntax
            || PlainClassModule.OwnersCross(c) && !PlainClassModule.IsServerOnlyDeclaration(c)
               && !(model?.GetDeclaredSymbol(c) is INamedTypeSymbol nested && PlainClassModule.OwnerKeptOut(nested));
        var componentNames = new HashSet<string>();
        foreach (var c in classes)
        {
            // A static class is never a component (not instantiable) — even when it declares a Build
            // helper method (e.g. a write-once VIEW helper building an abstract tree). It flows through
            // the static-helper emission path instead.
            if (c.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;

            var bn = BaseName(c);
            var hasBR = c.Members.OfType<MethodDeclarationSyntax>().Any(m => IsBuildEntryPoint(m, "Render", "Build"));
            // With the model, the chain decides wherever it binds: a builder whose last step is `Build` derives
            // from nothing a component does, and was written as one (#1164). The name of a method is the answer
            // only where a base on the chain does not bind, or there is no model at all.
            bool direct = model?.GetDeclaredSymbol(c) is INamedTypeSymbol s
                ? (s.IsUiComponent() || hasBR && !s.BasesBind())
                : (bn is "StatefulComponent" or "StatelessComponent" or "HtmlElement" or "Flex" or "Container" or "Stack" || hasBR);
            // A component is known by its twin's name, a nested one by its owner's too, so a top-level
            // component and a nested plain class of the same simple name are never taken for each
            // other (#584); and a nested one crosses where its owner does, as a nested class does.
            if (direct && OwnersAdmit(c)) componentNames.Add(c.TwinTypeName());
        }
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var c in classes)
            {
                if (componentNames.Contains(c.TwinTypeName())) continue;
                var bn = BaseName(c);
                if (bn != null && componentNames.Contains(bn) && OwnersAdmit(c)) { componentNames.Add(c.TwinTypeName()); changed = true; }
            }
        }

        // WHICH framework base a component reaches, asked ONCE and answered the same way detection
        // answered "is this a component at all". Classification used to compare the WRITTEN base
        // name against three literals and keep a fourth arm for everything else, so a component
        // over an app-owned base was classified by a name the walk had already seen past — #268,
        // #269 and #245 are that one disagreement reached three ways.
        var writtenBaseByName = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var c in classes) writtenBaseByName[c.TwinTypeName()] = BaseName(c);

        ComponentBaseKind ResolveBaseKind(ClassDeclarationSyntax c)
        {
            if (model?.GetDeclaredSymbol(c) is INamedTypeSymbol symbol)
            {
                var resolved = symbol.ResolveComponentBase();
                if (resolved != ComponentBaseKind.None) return resolved;
            }

            // No model, or a model that cannot bind the base — the same condition the detection
            // heuristic above exists for. Walk what the FILE says instead, which reaches an
            // in-file app base exactly as the transitive closure just did.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var name = BaseName(c);
            while (name is not null && seen.Add(name))
            {
                switch (name)
                {
                    case "StatefulComponent": return ComponentBaseKind.StatefulComponent;
                    case "StatelessComponent": return ComponentBaseKind.StatelessComponent;
                    case "HtmlElement": return ComponentBaseKind.HtmlElement;
                    case "ComponentState": return ComponentBaseKind.ComponentState;
                }
                name = writtenBaseByName.TryGetValue(name, out var next) ? next : null;
            }
            return ComponentBaseKind.None;
        }

        var stateNames = new HashSet<string>();

        // The chains of bases a parser with no project compilation reads: the resolver's scan, which
        // reaches every file it read, and this file's own declarations when there is no resolver.
        var chains = _chains ?? PlainClassModule.Scan.Of(root);

        // A PLAIN class — not a component, not static, not a state class — is a model the developer
        // wrote: a bucket, a builder, a small state machine. Nothing emitted one, so `new Bucket()`
        // named something that did not exist. Identity, not value: no structural equals, no `with`.
        foreach (var classDecl in classes)
        {
            if (componentNames.Contains(classDecl.TwinTypeName())) continue; // component path
            if (stateNames.Contains(classDecl.Identifier.ValueText)) continue;     // owned by its page
            // The one rule the resolver reads too (#423): what the class IS decides, never whether it
            // declares a member. A class that declared none was skipped here while the resolver had
            // it imported, so the bundle could not resolve the module every user of it named. A
            // [ServerOnly] class never crosses, whichever of its declarations says so.
            if (!PlainClassModule.Is(classDecl, ProjectSymbol(classDecl, model), chains)) continue;
            // Track L D2: a resx Designer is a plain non-static class by shape, and a module of
            // ResourceManager.GetString calls cannot run in a browser — its accessors rewrite to
            // $eq.str at every use site instead. Recorded on the way past (see the static-helper
            // loop for why the catalog emitter needs to know it exists).
            if (helperModel?.GetDeclaredSymbol(classDecl) is INamedTypeSymbol declaredPlain
                && Services.ResourceClasses.IsResourceClass(declaredPlain))
            {
                DiscoveredResources.TryAdd(
                    CatalogIdOf(helperModel, declaredPlain),
                    Services.ResourceClasses.DesignerPathFor(declaredPlain));
                continue;
            }

            results.Add(new ComponentDefinition
            {
                Name = classDecl.TwinTypeName(),
                SourcePath = sourcePath,
                SyntaxTree = tree,
                Namespace = ns ?? "",
                IsPlainClass = true,
                ValueTypeSyntax = classDecl,
            });
        }

        foreach (var classDecl in classes)
        {
            if (!componentNames.Contains(classDecl.TwinTypeName())) continue;

            // The WRITTEN base, which is what the emitter puts after `extends` and what the relative
            // import beside it names. Never the resolved KIND: a component over `CardBase` must keep
            // saying `extends CardBase`. Named as its twin, so `eQuantic.UI.Web.HtmlElement` is
            // `HtmlElement`, which is what the import beside it brings (#479).
            var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.TwinTypeName(model);
            var resolvedBase = ResolveBaseKind(classDecl);

            var definition = new ComponentDefinition
            {
                Name = classDecl.TwinTypeName(),
                TypeIdentity = ClrIdentity(classDecl),
                SourcePath = sourcePath,
                SyntaxTree = tree,
                ClassSyntax = classDecl,
                Namespace = ns ?? "",
                TypeParameters = classDecl.TypeParameterList?.Parameters.Select(p => p.Identifier.ValueText.ToJsIdentifier()).ToList() ?? new List<string>(),
                IsAbstract = classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword),
                BaseClassName = baseType,
            };

            if (resolvedBase == ComponentBaseKind.HtmlElement)
            {
                // The primitive path, unchanged: an HtmlElement transcribes a DOM tag and has no
                // Build, no page route and no server actions to find.
                definition.IsPrimitive = true;
                definition.IsStateful = false;
                ParsePrimitiveClass(classDecl, definition);
            }
            else
            {
                // THE COMPONENT PATH, AND IT RUNS EVERY MEMBER PARSER. What a component carries into
                // its twin cannot depend on the name of the base it was written over: the three arms
                // this replaced each kept their own list and all three disagreed — the stateless one
                // never looked for server actions (#269), and the one for an app-owned base parsed
                // only Build, dropping every constructor, field, property and method (#268).
                definition.IsStateful = resolvedBase == ComponentBaseKind.StatefulComponent;

                ParsePageAttributes(classDecl, definition);
                ParseServerActions(classDecl, definition);

                // `Members`, not `DescendantNodes`: the two arms disagreed here too, and
                // DescendantNodes reaches INTO a nested class, so a component whose nested type
                // happens to declare a Build would have had that one taken for its own.
                var buildMethod = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => IsBuildEntryPoint(m, "Build"));
                if (buildMethod != null)
                {
                    definition.BuildMethodNode = buildMethod;
                }

                // A component with no Build of its own over an APP-OWNED base inherits one. The
                // written name is the right question here and deliberately so: it is the same name
                // the emitter puts after `extends`, so this asks whether that `extends` target is a
                // module that carries a build — not what the chain ultimately reaches.
                definition.BuildComesFromTheBase = buildMethod is null
                    && BaseName(classDecl) is { } writtenBase
                    && writtenBase is not ("StatefulComponent" or "StatelessComponent"
                        or "HtmlElement" or "ComponentState");

                ParseConstructors(classDecl, definition);
                ParseMethods(classDecl, definition);
                ParseComponentFields(classDecl, definition);
            }

            CollectRuntimeProvidedTypes(classDecl, definition);

            results.Add(definition);
        }

        return results;
    }

    private void ParsePageAttributes(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ValueText();
                if (attrName == "Page" || attrName == "PageAttribute")
                {
                    var routeInfo = new PageRouteInfo();
                    
                    if (attr.ArgumentList?.Arguments.Count > 0)
                    {
                        var routeArg = attr.ArgumentList.Arguments[0];
                        routeInfo.Route = routeArg.Expression.ToString().Trim('"');
                        
                        // Check for named Title argument
                        foreach (var arg in attr.ArgumentList.Arguments.Skip(1))
                        {
                            if (arg.NameEquals?.Name.ValueText() == "Title")
                            {
                                routeInfo.Title = arg.Expression.ToString().Trim('"');
                            }
                        }
                    }
                    
                    definition.PageRoutes.Add(routeInfo);
                }
            }
        }
    }
    
    private void ParseServerActions(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var serverActionAttr = method.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ValueText() == "ServerAction" || a.Name.ValueText() == "ServerActionAttribute");
            
            if (serverActionAttr != null)
            {
                string actionName = method.Identifier.ValueText;
                
                // Check for Name parameter
                if (serverActionAttr.ArgumentList != null)
                {
                    foreach (var arg in serverActionAttr.ArgumentList.Arguments)
                    {
                        if (arg.NameEquals?.Name.ValueText() == "Name")
                        {
                            actionName = arg.Expression.ToString().Trim('"');
                            break;
                        }
                    }
                }

                var actionInfo = new ServerActionInfo
                {
                    MethodName = method.Identifier.ValueText,
                    ActionId = $"{definition.Name}/{actionName}",
                    ReturnType = method.ReturnType.ValueText(),
                    TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.ValueText.ToJsIdentifier()).ToList() ?? new List<string>(),
                    IsAsync = method.Modifiers.Any(m => m.ValueText == "async"),
                    SyntaxNode = method
                };
                
                foreach (var param in method.ParameterList.Parameters)
                {
                    actionInfo.Parameters.Add(new ParameterDefinition
                    {
                        Name = param.Identifier.ValueText,
                        Type = param.Type?.ValueText() ?? "object"
                    });
                }
                
                definition.ServerActions.Add(actionInfo);
            }
        }
    }
    
    /// <summary>
    /// SERVER-ONLY (<c>[ServerOnly]</c>): the method never crosses to the client, so it is not
    /// parsed, not transpiled and not validated against the client boundary — it may use the whole
    /// server surface (an SSR prefetch's HttpClient, EF, the request's services). The counterpart of
    /// <c>[ServerAction]</c>, which keeps the method callable FROM the browser through an RPC stub.
    /// </summary>
    /// <summary>
    /// The class-level form: <c>[ServerOnly]</c> on a static helper says the whole type stays on the
    /// server, and the parser emits no module for it. A plain class is asked the same of its symbol by
    /// <see cref="PlainClassModule"/>, the rule the resolver reads too.
    /// <para>
    /// Asked of the SYMBOL, which unifies partial declarations, and not of the declaration in hand.
    /// The syntactic form answered per file, so a partial class spread across six files was
    /// impossible to silence: C# rejects the attribute on more than one declaration (CS0579, "Duplicate
    /// 'ServerOnly' attribute"), and one declaration silenced only its own — a consumer measured
    /// four of five EQ1006 warnings surviving, with no way at all to silence them short of merging
    /// the files. A type is server-only or it is not; which file says so is not the transpiler's
    /// business.
    /// </para>
    /// <para>
    /// Falls back to the syntax when the model cannot be asked — standalone CompileSource has no
    /// references, so the attribute resolves to an error symbol there and its NAME is all there is.
    /// That is the repo's rule: a name heuristic is legal only where the model has nothing to say.
    /// </para>
    /// </summary>
    private bool IsServerOnly(ClassDeclarationSyntax classDecl)
    {
        // Both spellings: with the reference resolved the symbol is `ServerOnlyAttribute`, and in a
        // compilation without it the attribute binds to an error symbol carrying the name as
        // WRITTEN. The second is the playground's world, and a type that says it stays on the
        // server must be believed there too.
        if (TryGetSemanticModel(classDecl.SyntaxTree)?.GetDeclaredSymbol(classDecl) is { } symbol
            && PlainClassModule.IsServerOnly(symbol))
            return true;

        return classDecl.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.IsNamed("ServerOnly"));
    }

    /// <summary>
    /// The chains of bases the resolver's scan saw across every file it read (#423), which this parser
    /// reads for the plain-class rule when its host has no project compilation to ask: its own file stops
    /// at a base another file declares, and `class Retry : Failure` over another file's
    /// `class Failure : Exception` was written as a module the resolver never imported.
    /// </summary>
    internal void SetChains(PlainClassModule.Scan chains) => _chains = chains;

    private PlainClassModule.Scan? _chains;

    /// <summary>
    /// A type's symbol in the PROJECT's compilation, which the plain-class rule asks for a class's chain
    /// of bases (#423) and the twin rule for a record's or a struct's [ServerOnly], as the resolver asks
    /// the same compilation; null where the host has none. The minimal model of one file is not asked:
    /// it cannot see a base or a partial declaration another file holds, and the resolver, which reads
    /// every file, would answer from names alone.
    /// </summary>
    private INamedTypeSymbol? ProjectSymbol(TypeDeclarationSyntax declaration, SemanticModel? model) =>
        _semanticModelProvider?.HasProjectCompilation == true
            ? model?.GetDeclaredSymbol(declaration) as INamedTypeSymbol
            : null;

    private bool IsRuntimeProvided(ClassDeclarationSyntax classDecl)
    {
        if (TryGetSemanticModel(classDecl.SyntaxTree)?.GetDeclaredSymbol(classDecl) is { } symbol
            && symbol.GetAttributes()
                .Any(a => a.AttributeClass?.Name is "RuntimeProvided" or "RuntimeProvidedAttribute"))
            return true;

        return classDecl.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.IsNamed("RuntimeProvided"));
    }

    /// <summary>
    /// Whether <paramref name="node"/> is code no twin is written from: a method, an operator or a
    /// conversion marked <c>[ServerOnly]</c>, which no twin holds whatever type declares it, and the
    /// body of a component's <c>[ServerAction]</c>, whose twin is a stub written from its SIGNATURE.
    /// <para>
    /// The one rule for what crosses, asked by everything that reads a declaration for the browser:
    /// the parser and the emitters, which leave such code out, and the scans that decide what a module
    /// imports and refuses (<see cref="Services.RuntimeProvidedTypeScanner"/>,
    /// <see cref="Services.NamedSourceTypes"/>). The client boundary is asked where the conversion writes
    /// a symbol, so it never meets this code at all. It was a validator that walked the whole FILE, and
    /// refused a Server Action that read a file as client code (#816); and a class's, a static class's
    /// or a record's <c>[ServerOnly]</c> method was written into its twin, its server code refused there.
    /// </para>
    /// </summary>
    /// <param name="node">A node of the declaration being read.</param>
    /// <param name="inAComponent">Whether that declaration is a component's: a <c>[ServerAction]</c> is
    /// served for a component alone, so on any other type it is a method like the rest.</param>
    /// <param name="model">A model of the compilation that holds <paramref name="node"/>, which says
    /// whether a method is marked (<see cref="IsServerOnlyMethod"/>); null where the caller has none,
    /// and the method is read as written.</param>
    internal static bool StaysOnTheServer(SyntaxNode node, bool inAComponent, SemanticModel? model) => node switch
    {
        MethodDeclarationSyntax or OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax =>
            IsServerOnlyMethod((BaseMethodDeclarationSyntax)node, model),
        BlockSyntax or ArrowExpressionClauseSyntax => inAComponent
            && node.Parent is MethodDeclarationSyntax action && IsServerAction(action),
        _ => false,
    };

    /// <summary>
    /// Whether a method, an operator or a conversion is marked <c>[ServerOnly]</c>, asked of its SYMBOL
    /// wherever a model holds its tree, and of its attribute lists by name only where none does. The
    /// lists alone missed two spellings C# accepts (#1140): an alias
    /// (<c>using Keep = eQuantic.UI.Primitives.ServerOnlyAttribute;</c>), and the defining half of a
    /// partial method, whose attributes C# merges with those of the implementing half, the one with the
    /// body. Each was written into its type's twin, and its server code refused there.
    /// <para>
    /// The minimal model of a standalone compile answers too: with no reference the attribute binds to an
    /// error type, which still carries its name, the alias target's for an alias. A member that writes no
    /// attribute and is no partial method can carry none, so it is answered without binding anything,
    /// which keeps the scans that ask the rule of every node of a declaration as cheap as they were.
    /// </para>
    /// </summary>
    private static bool IsServerOnlyMethod(BaseMethodDeclarationSyntax method, SemanticModel? model)
    {
        if (method.AttributeLists.Count == 0 && !method.Modifiers.Any(SyntaxKind.PartialKeyword)) return false;
        var own = model is null ? null
            : ReferenceEquals(model.SyntaxTree, method.SyntaxTree) ? model
            : model.Compilation.ModelOf(method.SyntaxTree);
        return own?.GetDeclaredSymbol(method) is IMethodSymbol symbol
            ? PlainClassModule.IsServerOnly(symbol)
            : PlainClassModule.IsServerOnlyDeclaration(method);
    }

    /// <summary>A method a component runs on the server and calls from the browser: its twin is a stub
    /// written from its SIGNATURE, and its body never crosses.</summary>
    internal static bool IsServerAction(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.IsNamed("ServerAction"));

    private void ParseMethods(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // Extract properties
        var properties = classDecl.Members
            .OfType<PropertyDeclarationSyntax>();
        
        foreach (var prop in properties)
        {
            var isPublic = prop.Modifiers.Any(SyntaxKind.PublicKeyword);
            
            // An explicit implementation is a property of its own beside the component's of its name, and
            // the emitter leaves out the one that hands off to the other (#1139): taken for a second
            // declaration, whichever came second was dropped, and a getter read itself.
            if (definition.Properties.Any(p => p.Name == prop.Identifier.ValueText
                    && p.Node?.ExplicitInterfaceSpecifier?.ToString() == prop.ExplicitInterfaceSpecifier?.ToString()))
                continue;

            definition.Properties.Add(new PropertyDefinition
            {
                Name = prop.Identifier.ValueText,
                Type = prop.Type.ValueText(),
                DefaultValueNode = prop.Initializer?.Value,
                IsPublic = isPublic,
                IsStatic = prop.Modifiers.Any(SyntaxKind.StaticKeyword),
                Node = prop
            });
        }
        
        // Extract methods (excluding Render/Build which are handled separately)
        var methods = classDecl.Members
            .OfType<MethodDeclarationSyntax>();
        var model = TryGetSemanticModel(classDecl.SyntaxTree);
        
        foreach (var method in methods)
        {
            if (StaysOnTheServer(method, inAComponent: true, model)) continue;
            // A [ServerAction] body runs on the SERVER; the client twin is the RPC stub the
            // emitter writes from definition.ServerActions. Transpiling the body here shipped
            // server code (DbContexts, Stopwatches, the compiler itself) into the browser.
            if (IsServerAction(method)) continue;

            var methodName = method.Identifier.ValueText;
            if (methodName == "CreateState") continue;
            if (IsBuildEntryPoint(method, "Render", "Build"))
            {
                // Build WINS over Render: a write-once component's contract is Build, and letting a
                // later Render overwrite it is how the last method in the file decided the page.
                if (definition.BuildMethodNode is null
                    || definition.BuildMethodNode.Identifier.ValueText != "Build"
                    || methodName == "Build")
                {
                    definition.BuildMethodNode = method;
                }
                continue;
            }

            // An explicit interface implementation is a member of its own beside a method of its name,
            // which holds a name of its own on the twin (#708): taken for a second overload, it was
            // dropped, and the twin called a method nobody wrote (#1139). Overloads are EQ1007's.
            if (definition.Methods.Any(m => m.Name == methodName
                    && m.SyntaxNode?.ExplicitInterfaceSpecifier?.ToString() == method.ExplicitInterfaceSpecifier?.ToString()))
                continue;

            var methodDef = new MethodDefinition
            {
                Name = methodName,
                ReturnType = method.ReturnType.ValueText(),
                TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.ValueText.ToJsIdentifier()).ToList() ?? new List<string>(),
                Body = method.Body?.ToString() ?? method.ExpressionBody?.Expression.ToString() ?? "",
                IsStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword),
                IsOverride = method.Modifiers.Any(SyntaxKind.OverrideKeyword),
                SyntaxNode = method
            };
            
            foreach (var param in method.ParameterList.Parameters)
            {
                methodDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.ValueText,
                    Type = param.Type?.ValueText() ?? "object"
                });
            }
            
            definition.Methods.Add(methodDef);
        }
    }

    /// <summary>
    /// Whether this method IS the component's entry point, rather than merely sharing its name.
    /// <para>
    /// Matching on the name alone made any method called <c>Render</c> the build method — so a page
    /// with a <c>private static VisualNode Render(DocBlock block, IAppTheme theme)</c> helper had its
    /// Build body REPLACED by the helper's, and the helper itself dropped. The emitted
    /// <c>build(_context)</c> then referenced the helper's parameters, which do not exist in that
    /// scope, and the page died at hydration on "block is not defined" — while SSR rendered
    /// correct HTML and answered 200, so a smoke test saw a perfect page.
    /// </para>
    /// <para>
    /// The entry point overrides a public abstract, so it is never static and never private. A
    /// helper is one or the other, which is exactly what tells them apart without a semantic model.
    /// </para>
    /// </summary>
    private static bool IsBuildEntryPoint(MethodDeclarationSyntax method, params string[] names) =>
        names.Contains(method.Identifier.ValueText)
        && !method.Modifiers.Any(SyntaxKind.StaticKeyword)
        && !method.Modifiers.Any(SyntaxKind.PrivateKeyword);

    private void ParsePrimitiveClass(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        ParseMethods(classDecl, definition);

        // Extract constructors, a static one excepted: it is the type initializer's.
        var constructors = InstanceConstructors(classDecl);

        foreach (var ctor in constructors)
        {
            var ctorDef = new MethodDefinition
            {
                Name = ctor.Identifier.ValueText,
                ReturnType = "void",
                Body = ctor.Body?.ToString() ?? ctor.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = null // Marker for constructor helper
            };

            foreach (var param in ctor.ParameterList.Parameters)
            {
                ctorDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.ValueText,
                    Type = param.Type?.ValueText() ?? "object"
                });
            }

            definition.Constructors.Add(ctorDef);
        }
    }

    /// <summary>The constructors that build an instance: every one the class declares but a static
    /// one, which is the type initializer's.</summary>
    private static IEnumerable<ConstructorDeclarationSyntax> InstanceConstructors(ClassDeclarationSyntax classDecl) =>
        classDecl.Members.OfType<ConstructorDeclarationSyntax>()
            .Where(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword));

    private void ParseConstructors(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // C# 12 primary constructor: `class C(int id, string label) : Base`. Its parameters are captured as
        // instance state when referenced in members (Build, helpers), so model them as a constructor whose
        // params the emitter assigns to fields (`this.id = id`); IdentifierStrategy prefixes references with
        // `this.`. (Records route through RecordTypeEmitter, not here.)
        if (classDecl.ParameterList is { Parameters.Count: > 0 } primary)
        {
            var primaryDef = new MethodDefinition
            {
                Name = classDecl.Identifier.ValueText, ReturnType = "void", IsPrimaryConstructor = true,
            };
            foreach (var param in primary.Parameters)
            {
                // Described exactly as an explicit constructor's parameters are. Building the
                // definition by hand here is how a DEPENDENCY stopped being one: a section written
                // `PairLoop(IClock clock)` emitted `if (clock !== undefined) this.clock = clock`,
                // and since nobody composing it in the middle of a tree passes a clock, the field
                // was undefined and the component was inert — in silence, on one target.
                primaryDef.Parameters.Add(Describe(param));
            }
            definition.Constructors.Add(primaryDef);
        }

        // The parameter, and whether it is a dependency. Asked of the MODEL rather than guessed from
        // the name: `IPhotoLibrary` looks like an interface and so does a class somebody called
        // that, and only one of them belongs in the container.
        ParameterDefinition Describe(ParameterSyntax param)
        {
            var described = new ParameterDefinition
            {
                Name = param.Identifier.ValueText,
                Type = param.Type?.ValueText() ?? "object",
                DefaultValueNode = param.Default?.Value,
            };

            if (param.Type is not null
                && TryGetSemanticModel(param.SyntaxTree) is { } model
                && model.GetTypeInfo(param.Type).Type is { } service
                && CapabilityRule.IsDependency(service))
            {
                described.IsService = true;
                described.ServiceKey = service.Name;
                // Whether the component can work without it, which decides what an ABSENT capability
                // does: hand over null as the author allowed, or say which one is missing.
                if (model.GetDeclaredSymbol(param) is { } parameter)
                    described.IsRequiredService = CapabilityRule.IsRequired(parameter);
            }

            return described;
        }

        // A PARAMETERLESS constructor is not an empty one. A stateful component sets its state up
        // there — seeds a controller, subscribes to it, reads a clock — and dropping it because it
        // takes no arguments meant the server ran that setup and the browser did not: SSR rendered
        // a form with three fields, hydration adopted the markup, and the client's own model was
        // empty, so typing changed nothing. Silent, and only on the target the user is on.
        // A STATIC constructor is never an instance one: the type initializer runs it, once
        // (TypeInitializer). Taken for one, it ran on every instance besides, and declared before a
        // parameterless instance constructor it took that one's place, whose body was dropped.
        var constructors = InstanceConstructors(classDecl)
            .Where(c => c.ParameterList.Parameters.Count > 0
                        || c.Body is not null || c.ExpressionBody is not null);

        foreach (var ctor in constructors)
        {
            var ctorDef = new MethodDefinition
            {
                Name = ctor.Identifier.ValueText,
                ReturnType = "void",
                Body = ctor.Body?.ToString() ?? ctor.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = null,
                BodyNode = ctor.Body,
                ExpressionBodyNode = ctor.ExpressionBody?.Expression,
            };

            foreach (var param in ctor.ParameterList.Parameters)
            {
                ctorDef.Parameters.Add(Describe(param));
            }

            definition.Constructors.Add(ctorDef);
        }
    }

    /// <summary>
    /// Collects the simple names of referenced RUNTIME-PROVIDED types (and referenced enums) into the
    /// definition — semantic-model driven via <see cref="RuntimeProvidedTypeScanner"/>, following
    /// whatever the class actually references with no fixed type list.
    /// </summary>
    private void CollectRuntimeProvidedTypes(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // Standalone or not, the emitter needs the source's own universe: every type DECLARED in
        // this tree. Without a semantic model it is the only ground truth left — a referenced name
        // outside this set cannot be user code, so it must be the runtime vocabulary.
        foreach (var declared in classDecl.SyntaxTree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            definition.DeclaredInSource.Add(declared.Identifier.ValueText);

        var model = TryGetSemanticModel(classDecl.SyntaxTree);
        if (model == null) return;
        // A model is only AUTHORITATIVE when the framework's references are actually in the
        // compilation. A partial CompileSource model resolves local symbols and ERRORS on every
        // external one — it classifies nothing, and must not silence the declared-in-source
        // fallback, or the whole vocabulary becomes imports of modules that exist nowhere.
        definition.ResolvedSemantically =
            model.Compilation.GetTypeByMetadataName("eQuantic.UI.Primitives.VisualNode") is not null;
        RuntimeProvidedTypeScanner.Collect(classDecl, model, asComponent: true, definition.RuntimeProvidedTypes,
            definition.EnumTypes, definition.AppTypes, definition.HostOnlyTypes);
    }

    /// <summary>
    /// Captures fields declared directly on a component class (static data, consts, instance fields) into
    /// <see cref="ComponentDefinition.ComponentFields"/>. Uses direct members (not descendants) so fields
    /// of any nested type aren't pulled in. A field is treated as static when declared <c>static</c> or
    /// <c>const</c> — both become a <c>static</c> class member referenced as <c>ClassName.field</c>.
    /// </summary>
    private void ParseComponentFields(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            var isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword)
                           || field.Modifiers.Any(SyntaxKind.ConstKeyword);
            foreach (var variable in field.Declaration.Variables)
            {
                definition.ComponentFields.Add(new StateField
                {
                    Name = variable.Identifier.ValueText,
                    Type = field.Declaration.Type.ValueText(),
                    TypeNode = field.Declaration.Type,
                    DefaultValueNode = variable.Initializer?.Value,
                    IsStatic = isStatic
                });
            }
        }
    }
    

    /// <summary>
    /// The CLR full name of a type DEFINITION, read off its syntax: the namespaces it sits in, its
    /// containing types joined by <c>+</c>, and a generic definition's arity after a backtick —
    /// what <c>Type.FullName</c> says for the same class at runtime, which is the identity the server
    /// keys a component's state by (<c>ComponentIdentity.Of</c>). Syntax rather than the semantic
    /// model because the fallback path has no model, and the answer must not depend on having one.
    /// </summary>
    internal static string ClrIdentity(TypeDeclarationSyntax declaration)
    {
        static string Own(TypeDeclarationSyntax type) =>
            type.TypeParameterList is { Parameters.Count: > 0 } parameters
                ? $"{type.Identifier.ValueText}`{parameters.Parameters.Count}"
                : type.Identifier.ValueText;

        var name = Own(declaration);
        for (var parent = declaration.Parent; parent is TypeDeclarationSyntax outer; parent = parent.Parent)
            name = $"{Own(outer)}+{name}";

        var ns = string.Join(".", declaration.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse()
            .Select(n => n.Name.ValueText()));
        return ns.Length == 0 ? name : $"{ns}.{name}";
    }
}
