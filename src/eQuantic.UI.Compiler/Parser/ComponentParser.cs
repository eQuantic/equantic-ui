using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;
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
    /// The JS literal for an uninitialized VALUE-TYPE property's C# default. A field of a value type
    /// is zero in C# whether or not anyone wrote <c>= 0</c>; on the client it is <c>undefined</c>
    /// unless someone writes it, and the two are not the same value.
    /// <para>
    /// This started at enums, where the divergence is loud: an unset enum is its zero member, lowered
    /// as a string, so a <c>status === 'none'</c> test that is TRUE on the server takes the other
    /// branch after hydration. Numbers were left out because <c>undefined</c> is falsy and reads like
    /// <c>x > 0</c> behave the same — which is true right up to the first ARITHMETIC:
    /// <c>Math.max(w, undefined)</c> is NaN, and a NaN width reaches the stylesheet as
    /// <c>width:NaNpx</c>, a rule the CSS parser drops whole. It showed up on a code block, on a
    /// client-rendered page only, because SSR computes the same property in C# where it is 0.
    /// </para>
    /// <para>Null for reference types, where C#'s default and `undefined` really do behave alike.</para>
    /// </summary>
    private string? ImplicitValueDefaultJs(PropertyDeclarationSyntax prop)
    {
        var model = TryGetSemanticModel(prop.SyntaxTree);
        var type = model?.GetTypeInfo(prop.Type).Type;

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            var zero = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue
                    && Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture) == 0);
            if (zero == null) return null; // no zero member: C# default is an unnamed value — leave it alone

            return enumType.IsFlagsEnum() ? "0" : $"'{zero.Name.ToCamelCase()}'";
        }

        // A NULLABLE value type really is null when unset — `undefined` is the honest twin there.
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
            return null;

        return type?.SpecialType switch
        {
            SpecialType.System_Boolean => "false",
            SpecialType.System_SByte or SpecialType.System_Byte
                or SpecialType.System_Int16 or SpecialType.System_UInt16
                or SpecialType.System_Int32 or SpecialType.System_UInt32
                or SpecialType.System_Int64 or SpecialType.System_UInt64
                or SpecialType.System_Single or SpecialType.System_Double
                or SpecialType.System_Decimal => "0",
            _ => null,
        };
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
        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: sourcePath);
        var root = tree.GetCompilationUnitRoot();
        var results = new List<ComponentDefinition>();
        
        // Extract namespace
        string? ns = null;
        var namespaceDecl = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        
        if (namespaceDecl != null)
        {
            ns = namespaceDecl.Name.ToString();
        }
        else
        {
            var blockNamespace = root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault();
            if (blockNamespace != null)
            {
                ns = blockNamespace.Name.ToString();
            }
        }
        
        // Discover user value types: records (positional or body) and structs are emitted as named JS
        // classes. Reactive — driven by the declarations actually present, not a fixed list.
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (RecordTypeEmitter.CanEmit(typeDecl))
            {
                results.Add(new ComponentDefinition
                {
                    Name = typeDecl.Identifier.Text,
                    SourcePath = sourcePath,
                    SyntaxTree = tree,
                    Namespace = ns ?? "",
                    IsRecordType = true,
                    ValueTypeSyntax = typeDecl,
                });
            }
        }

        // Discover static utility classes (`static class Format { … }`) used from components — emitted as
        // their own module of static members so `Format.Foo()` resolves at runtime.
        // NESTED static classes are excluded: they are their owner's private scope (every section
        // having its own `Copy` is the pattern), so they embed INSIDE the owner's module — as their
        // own module, two same-named nested classes would overwrite each other's file.
        var helperModel = TryGetSemanticModel(tree);
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (classDecl.Parent is ClassDeclarationSyntax) continue;
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
                    Services.ResourceClasses.IdFor(helperModel.Compilation, declared),
                    Services.ResourceClasses.DesignerPathFor(declared));
                continue;
            }
            if (classDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                results.Add(new ComponentDefinition
                {
                    Name = classDecl.Identifier.Text,
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
        var model = TryGetSemanticModel(tree);

        // Pre-pass: which classes are components. A class is one if its base resolves to a known component
        // base (semantic walk) / matches a base-name heuristic / it declares Build|Render — AND, transitively,
        // any class extending another in-file component (so a subclass of a user component is recognized even
        // without its own Build and without relying on the semantic model resolving the full base chain).
        string? BaseName(ClassDeclarationSyntax c)
        {
            var b = c.BaseList?.Types.FirstOrDefault()?.Type.ToString();
            if (b == null) return null;
            if (b.Contains('<')) b = b.Substring(0, b.IndexOf('<'));
            return b.Contains('.') ? b.Substring(b.LastIndexOf('.') + 1) : b;
        }
        var componentNames = new HashSet<string>();
        foreach (var c in classes)
        {
            // A static class is never a component (not instantiable) — even when it declares a Build
            // helper method (e.g. a write-once VIEW helper building an abstract tree). It flows through
            // the static-helper emission path instead.
            if (c.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;

            var bn = BaseName(c);
            var hasBR = c.Members.OfType<MethodDeclarationSyntax>().Any(m => IsBuildEntryPoint(m, "Render", "Build"));
            bool direct = model?.GetDeclaredSymbol(c) is INamedTypeSymbol s
                ? (s.IsUiComponent() || hasBR)
                : (bn is "StatefulComponent" or "StatelessComponent" or "HtmlElement" or "Flex" or "Container" or "Stack" || hasBR);
            if (direct) componentNames.Add(c.Identifier.Text);
        }
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var c in classes)
            {
                if (componentNames.Contains(c.Identifier.Text)) continue;
                var bn = BaseName(c);
                if (bn != null && componentNames.Contains(bn)) { componentNames.Add(c.Identifier.Text); changed = true; }
            }
        }

        // A ComponentState<T> subclass is owned by its page: the page's module emits it COMPLETE (state
        // fields + setState + handlers + ctor + build, via ParseStateClass) and `createState()` news it up
        // from that same module. Emitting it ALSO as a standalone component module produced a broken,
        // duplicated `<State>.ts/.js` carrying only `build()` — referencing state fields/handlers that
        // module never declares. It is detected as a component here only because it has a `Build` method
        // (and ComponentState is itself a component base), so drop state classes from the standalone set.
        var stateNames = new HashSet<string>();
        foreach (var c in classes)
        {
            bool isState = model?.GetDeclaredSymbol(c) is INamedTypeSymbol s
                ? s.IsComponentState()
                : BaseName(c) == "ComponentState";
            if (isState)
            {
                componentNames.Remove(c.Identifier.Text);
                stateNames.Add(c.Identifier.Text);
            }
        }

        // A PLAIN class — not a component, not static, not a state class — is a model the developer
        // wrote: a bucket, a builder, a small state machine. Nothing emitted one, so `new Bucket()`
        // named something that did not exist. Identity, not value: no structural equals, no `with`.
        foreach (var classDecl in classes)
        {
            if (classDecl.Parent is TypeDeclarationSyntax) continue;          // nested: its owner's scope
            if (classDecl.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;  // static-helper path
            if (componentNames.Contains(classDecl.Identifier.Text)) continue; // component path
            if (stateNames.Contains(classDecl.Identifier.Text)) continue;     // owned by its page
            if (classDecl.Members.Count == 0) continue;
            // Track L D2: a resx Designer is a plain non-static class by shape, and a module of
            // ResourceManager.GetString calls cannot run in a browser — its accessors rewrite to
            // $eq.str at every use site instead. Recorded on the way past (see the static-helper
            // loop for why the catalog emitter needs to know it exists).
            if (helperModel?.GetDeclaredSymbol(classDecl) is INamedTypeSymbol declaredPlain
                && Services.ResourceClasses.IsResourceClass(declaredPlain))
            {
                DiscoveredResources.TryAdd(
                    Services.ResourceClasses.IdFor(helperModel.Compilation, declaredPlain),
                    Services.ResourceClasses.DesignerPathFor(declaredPlain));
                continue;
            }

            results.Add(new ComponentDefinition
            {
                Name = classDecl.Identifier.Text,
                SourcePath = sourcePath,
                SyntaxTree = tree,
                Namespace = ns ?? "",
                IsPlainClass = true,
                ValueTypeSyntax = classDecl,
            });
        }

        foreach (var classDecl in classes)
        {
            var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString();
            var hasBuildOrRender = classDecl.Members.OfType<MethodDeclarationSyntax>()
                .Any(m => IsBuildEntryPoint(m, "Render", "Build"));

            if (!componentNames.Contains(classDecl.Identifier.Text)) continue;

            var definition = new ComponentDefinition
            {
                Name = classDecl.Identifier.Text,
                SourcePath = sourcePath,
                SyntaxTree = tree,
                Namespace = ns ?? "",
                TypeParameters = classDecl.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                IsAbstract = classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword)
            };

            if (baseType == "StatefulComponent" && BaseResolvesToPrimitives(classDecl))
            {
                // The SHARED stateful shape (eQuantic.UI.Primitives.StatefulComponent): state lives on
                // the component itself and SetState rebuilds directly — no CreateState/ComponentState
                // split. Structurally it parses like a stateless component (Build + ctors + methods +
                // fields on the class); the emitter swaps the base for the runtime's
                // SharedStatefulComponent.
                definition.IsStateful = false;
                definition.IsSharedStateful = true;
                definition.BaseClassName = "SharedStatefulComponent";
                ParsePageAttributes(classDecl, definition);
                ParseServerActions(classDecl, definition);

                var sharedBuild = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => IsBuildEntryPoint(m, "Build"));
                if (sharedBuild != null)
                {
                    definition.BuildMethodNode = sharedBuild;
                }

                ParseConstructors(classDecl, definition);
                ParseMethods(classDecl, definition);
                ParseComponentFields(classDecl, definition);
            }
            else if (baseType == "StatefulComponent")
            {
                definition.IsStateful = true;

                // Parse Page attributes and ServerActions
                ParsePageAttributes(classDecl, definition);
                ParseServerActions(classDecl, definition);
                
                // Find state class name from CreateState method
                var createStateMethod = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "CreateState");
                
                if (createStateMethod != null)
                {
                    var newExpr = createStateMethod.DescendantNodes()
                        .OfType<ObjectCreationExpressionSyntax>()
                        .FirstOrDefault();
                    
                    if (newExpr != null)
                    {
                        definition.StateClassName = newExpr.Type.ToString();
                    }
                }

                definition.BaseClassName = baseType;
                
                // If we found a state class name, find it in the same file
                if (!string.IsNullOrEmpty(definition.BaseClassName) && !string.IsNullOrEmpty(definition.StateClassName))
                {
                    var stateClass = classes.FirstOrDefault(c => c.Identifier.Text == definition.StateClassName);
                    if (stateClass != null)
                    {
                        ParseStateClass(stateClass, definition);
                    }
                }

                // Static/instance data fields declared on the stateful component class itself.
                ParseComponentFields(classDecl, definition);
            }
            else if (baseType == "StatelessComponent")
            {
                definition.IsStateful = false;
                definition.BaseClassName = baseType;
                ParsePageAttributes(classDecl, definition);

                // Parse Build method for stateless component
                var buildMethod = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => IsBuildEntryPoint(m, "Build"));

                if (buildMethod != null)
                {
                    definition.BuildMethodNode = buildMethod;
                }

                // Parse constructors (for components with positional args like Text, Heading)
                ParseConstructors(classDecl, definition);

                // Parse other helper methods
                ParseMethods(classDecl, definition);

                // Capture static/instance data fields declared on the component
                ParseComponentFields(classDecl, definition);
            }
            else if (baseType == "HtmlElement")
            {
                definition.IsPrimitive = true;
                definition.IsStateful = false;
                definition.BaseClassName = baseType;
                ParsePrimitiveClass(classDecl, definition);
            }
            else
            {
                // Component that extends another component (not directly StatelessComponent/HtmlElement)
                // Check if it has its own Build method
                definition.IsStateful = false;
                definition.BaseClassName = baseType;

                var buildMethod = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => IsBuildEntryPoint(m, "Build"));

                if (buildMethod != null)
                {
                    definition.BuildMethodNode = buildMethod;
                }
                else
                {
                    // No Build method, treat as primitive (extends another component without overriding)
                    definition.IsPrimitive = true;
                    ParseMethods(classDecl, definition);
                }
            }

            CollectRuntimeProvidedTypes(classDecl, definition);
            // A stateful component's Build lives in its STATE class — runtime-provided references
            // there (e.g. the VisualNodeComponent bridge) must route the same way.
            if (!string.IsNullOrEmpty(definition.StateClassName))
            {
                var stateDecl = classes.FirstOrDefault(c => c.Identifier.Text == definition.StateClassName);
                if (stateDecl != null) CollectRuntimeProvidedTypes(stateDecl, definition);
            }

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
                var attrName = attr.Name.ToString();
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
                            if (arg.NameEquals?.Name.ToString() == "Title")
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
                .FirstOrDefault(a => a.Name.ToString() == "ServerAction" || a.Name.ToString() == "ServerActionAttribute");
            
            if (serverActionAttr != null)
            {
                string actionName = method.Identifier.Text;
                
                // Check for Name parameter
                if (serverActionAttr.ArgumentList != null)
                {
                    foreach (var arg in serverActionAttr.ArgumentList.Arguments)
                    {
                        if (arg.NameEquals?.Name.ToString() == "Name")
                        {
                            actionName = arg.Expression.ToString().Trim('"');
                            break;
                        }
                    }
                }

                var actionInfo = new ServerActionInfo
                {
                    MethodName = method.Identifier.Text,
                    ActionId = $"{definition.Name}/{actionName}",
                    ReturnType = method.ReturnType.ToString(),
                    TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                    IsAsync = method.Modifiers.Any(m => m.ValueText == "async"),
                    SyntaxNode = method
                };
                
                foreach (var param in method.ParameterList.Parameters)
                {
                    actionInfo.Parameters.Add(new ParameterDefinition
                    {
                        Name = param.Identifier.ValueText,
                        Type = param.Type?.ToString() ?? "object"
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
    private static bool IsServerOnly(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name.ToString() is "ServerOnly" or "ServerOnlyAttribute");

    private void ParseMethods(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // Extract properties
        var properties = classDecl.Members
            .OfType<PropertyDeclarationSyntax>();
        
        foreach (var prop in properties)
        {
            var isPublic = prop.Modifiers.Any(SyntaxKind.PublicKeyword);
            
            if (definition.Properties.Any(p => p.Name == prop.Identifier.Text)) continue;

            definition.Properties.Add(new PropertyDefinition
            {
                Name = prop.Identifier.Text,
                Type = prop.Type.ToString(),
                DefaultValue = prop.Initializer?.Value.ToString(),
                DefaultValueNode = prop.Initializer?.Value,
                ImplicitDefaultJs = prop.Initializer == null ? ImplicitValueDefaultJs(prop) : null,
                IsPublic = isPublic,
                IsStatic = prop.Modifiers.Any(SyntaxKind.StaticKeyword),
                Node = prop
            });
        }
        
        // Extract methods (excluding Render/Build which are handled separately)
        var methods = classDecl.Members
            .OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            if (IsServerOnly(method)) continue;
            // A [ServerAction] body runs on the SERVER; the client twin is the RPC stub the
            // emitter writes from definition.ServerActions. Transpiling the body here shipped
            // server code (DbContexts, Stopwatches, the compiler itself) into the browser.
            if (method.AttributeLists.SelectMany(a => a.Attributes)
                .Any(a => a.Name.ToString() is "ServerAction" or "ServerActionAttribute")) continue;

            var methodName = method.Identifier.Text;
            if (methodName == "CreateState") continue;
            if (IsBuildEntryPoint(method, "Render", "Build"))
            {
                // Build WINS over Render: a write-once component's contract is Build, and letting a
                // later Render overwrite it is how the last method in the file decided the page.
                if (definition.BuildMethodNode is null
                    || definition.BuildMethodNode.Identifier.Text != "Build"
                    || methodName == "Build")
                {
                    definition.BuildMethodNode = method;
                }
                continue;
            }

            if (definition.Methods.Any(m => m.Name == methodName)) continue;

            var methodDef = new MethodDefinition
            {
                Name = methodName,
                ReturnType = method.ReturnType.ToString(),
                TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                Body = method.Body?.ToString() ?? method.ExpressionBody?.Expression.ToString() ?? "",
                IsStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword),
                SyntaxNode = method
            };
            
            foreach (var param in method.ParameterList.Parameters)
            {
                methodDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.ValueText,
                    Type = param.Type?.ToString() ?? "object"
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
        names.Contains(method.Identifier.Text)
        && !method.Modifiers.Any(SyntaxKind.StaticKeyword)
        && !method.Modifiers.Any(SyntaxKind.PrivateKeyword);

    private void ParsePrimitiveClass(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        ParseMethods(classDecl, definition);

        // Extract constructors
        var constructors = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>();
        
        foreach (var ctor in constructors)
        {
            var ctorDef = new MethodDefinition
            {
                Name = ctor.Identifier.Text,
                ReturnType = "void",
                Body = ctor.Body?.ToString() ?? ctor.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = null // Marker for constructor helper
            };

            foreach (var param in ctor.ParameterList.Parameters)
            {
                ctorDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.ValueText,
                    Type = param.Type?.ToString() ?? "object"
                });
            }

            definition.Constructors.Add(ctorDef);
        }
    }

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
                Name = classDecl.Identifier.Text, ReturnType = "void", IsPrimaryConstructor = true,
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

        /// <summary>
        /// Whether an interface parameter is a DEPENDENCY rather than a shape data arrives in.
        /// <para>
        /// The runtime's own interfaces are not dependencies: `IReadOnlyList&lt;AccordionItem&gt;` is
        /// how a component receives its items, and an Accordion resolving its rows from a container
        /// is nonsense — which is exactly what the first version of this rule did, and what the
        /// committed transpilation caught within the hour.
        /// </para>
        /// </summary>
        static bool IsDependency(ITypeSymbol service) =>
            service.ContainingNamespace?.ToDisplayString() is { } space
            && !space.StartsWith("System", StringComparison.Ordinal);

        // The parameter, and whether it is a dependency. Asked of the MODEL rather than guessed from
        // the name: `IPhotoLibrary` looks like an interface and so does a class somebody called
        // that, and only one of them belongs in the container.
        ParameterDefinition Describe(ParameterSyntax param)
        {
            var described = new ParameterDefinition
            {
                Name = param.Identifier.ValueText,
                Type = param.Type?.ToString() ?? "object",
                DefaultValueNode = param.Default?.Value,
            };

            if (param.Type is not null
                && TryGetSemanticModel(param.SyntaxTree) is { } model
                && model.GetTypeInfo(param.Type).Type is { TypeKind: TypeKind.Interface } service
                && IsDependency(service))
            {
                described.IsService = true;
                described.ServiceKey = service.Name;
            }

            return described;
        }

        // A PARAMETERLESS constructor is not an empty one. A stateful component sets its state up
        // there — seeds a controller, subscribes to it, reads a clock — and dropping it because it
        // takes no arguments meant the server ran that setup and the browser did not: SSR rendered
        // a form with three fields, hydration adopted the markup, and the client's own model was
        // empty, so typing changed nothing. Silent, and only on the target the user is on.
        var constructors = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.ParameterList.Parameters.Count > 0
                        || c.Body is not null || c.ExpressionBody is not null);

        foreach (var ctor in constructors)
        {
            var ctorDef = new MethodDefinition
            {
                Name = ctor.Identifier.Text,
                ReturnType = "void",
                Body = ctor.Body?.ToString() ?? ctor.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = null,
                BodyNode = ctor.Body
            };

            foreach (var param in ctor.ParameterList.Parameters)
            {
                ctorDef.Parameters.Add(Describe(param));
            }

            definition.Constructors.Add(ctorDef);
        }
    }

    /// <summary>True when the class's direct base type resolves (semantically) into the shared
    /// <c>eQuantic.UI.Primitives</c> namespace — the write-once component model, whose stateful shape
    /// (direct <c>SetState</c>) differs from the Core <c>CreateState</c> split.</summary>
    private bool BaseResolvesToPrimitives(ClassDeclarationSyntax classDecl)
    {
        // The SHAPE decides first — syntactic, cheap, and unambiguous when it matches: only the
        // write-once page declares `VisualNode Build(ComponentContext …)` on the class itself
        // (the Core split declares CreateState() and builds on a ComponentState). This must not
        // depend on a semantic model: in-memory CompileSource without references (the
        // playground's mode) resolves the base to an ERROR symbol, and defaulting to the Core
        // shape miscompiled the page SILENTLY — a nameless state class, the Build body dropped
        // for an empty Container.
        var buildsVisualNode = classDecl.Members.OfType<MethodDeclarationSyntax>().Any(m =>
            m.Identifier.Text == "Build"
            && m.ReturnType.ToString().EndsWith("VisualNode")
            && m.ParameterList.Parameters.Count == 1
            && m.ParameterList.Parameters[0].Type?.ToString().EndsWith("ComponentContext") == true);
        if (buildsVisualNode) return true;

        // The semantic model settles everything the shape cannot (aliases, indirect bases).
        var baseSyntax = classDecl.BaseList?.Types.FirstOrDefault()?.Type;
        if (baseSyntax == null) return false;
        var model = TryGetSemanticModel(classDecl.SyntaxTree);
        if (model == null) return false;

        ISymbol? symbol;
        try { symbol = model.GetSymbolInfo(baseSyntax).Symbol; }
        catch { return false; }

        var ns = (symbol as INamedTypeSymbol)?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns == "eQuantic.UI.Primitives" || ns.StartsWith("eQuantic.UI.Primitives.");
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
            definition.DeclaredInSource.Add(declared.Identifier.Text);

        var model = TryGetSemanticModel(classDecl.SyntaxTree);
        if (model == null) return;
        // A model is only AUTHORITATIVE when the framework's references are actually in the
        // compilation. A partial CompileSource model resolves local symbols and ERRORS on every
        // external one — it classifies nothing, and must not silence the declared-in-source
        // fallback, or the whole vocabulary becomes imports of modules that exist nowhere.
        definition.ResolvedSemantically =
            model.Compilation.GetTypeByMetadataName("eQuantic.UI.Primitives.VisualNode") is not null;
        RuntimeProvidedTypeScanner.Collect(classDecl, model, definition.RuntimeProvidedTypes,
            definition.EnumTypes, definition.AppTypes);
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
                    Name = variable.Identifier.Text,
                    Type = field.Declaration.Type.ToString(),
                    DefaultValue = variable.Initializer?.Value.ToString(),
                    DefaultValueNode = variable.Initializer?.Value,
                    IsStatic = isStatic
                });
            }
        }
    }

    private void ParseStateClass(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // Extract fields
        var fields = classDecl.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(SyntaxKind.PrivateKeyword));
        
        foreach (var field in fields)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                definition.StateFields.Add(new StateField
                {
                    Name = variable.Identifier.Text,
                    Type = field.Declaration.Type.ToString(),
                    DefaultValue = variable.Initializer?.Value.ToString(),
                    DefaultValueNode = variable.Initializer?.Value
                });
            }
        }
        
        // Extract methods (excluding Build)
        var methods = classDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text != "Build");
        
        foreach (var method in methods)
        {
            if (IsServerOnly(method)) continue;

            var methodDef = new MethodDefinition
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
                TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                Body = method.Body?.ToString() ?? method.ExpressionBody?.Expression.ToString() ?? "",
                IsStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword),
                SyntaxNode = method
            };
            
            foreach (var param in method.ParameterList.Parameters)
            {
                methodDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.ValueText,
                    Type = param.Type?.ToString() ?? "object"
                });
            }
            
            definition.Methods.Add(methodDef);
        }
        
        // Parse Build method for component tree
        var buildMethod = classDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => IsBuildEntryPoint(m, "Build"));
        
        if (buildMethod != null)
        {
            definition.BuildMethodNode = buildMethod;
        }
    }
    
}
