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
    /// The JS literal for an uninitialized ENUM property's C# default — its zero member, lowered exactly
    /// as <c>EnumStrategy</c> lowers a member access ([Flags] numerically, otherwise the camelCase member
    /// name as a string). Returns null for every other type, where C#'s default and JS `undefined` behave
    /// alike. Without this the client leaves the property `undefined`, and a `status === 'none'` test that
    /// is TRUE on the server silently takes the other branch after hydration.
    /// </summary>
    private string? ImplicitEnumDefaultJs(PropertyDeclarationSyntax prop)
    {
        var model = TryGetSemanticModel(prop.SyntaxTree);
        if (model?.GetTypeInfo(prop.Type).Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return null;

        var zero = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(f => f.HasConstantValue
                && Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture) == 0);
        if (zero == null) return null; // no zero member: C# default is an unnamed value — leave it alone

        return enumType.IsFlagsEnum() ? "0" : $"'{zero.Name.ToCamelCase()}'";
    }

    /// <summary>
    /// Parse a .eqx file and extract component definitions
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
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (classDecl.Parent is ClassDeclarationSyntax) continue;
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
            var hasBR = c.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text is "Render" or "Build");
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
                .Any(m => m.Identifier.Text is "Render" or "Build");

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

                var sharedBuild = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "Build");
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
                    .FirstOrDefault(m => m.Identifier.Text == "Build");

                if (buildMethod != null)
                {
                    definition.BuildMethodNode = buildMethod;

                    var returnStatement = buildMethod.DescendantNodes()
                        .OfType<ReturnStatementSyntax>()
                        .FirstOrDefault();

                    if (returnStatement?.Expression != null)
                    {
                        definition.BuildTree = ParseComponentExpression(returnStatement.Expression);
                    }
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
                    .FirstOrDefault(m => m.Identifier.Text == "Build");

                if (buildMethod != null)
                {
                    definition.BuildMethodNode = buildMethod;

                    var returnStatement = buildMethod.DescendantNodes()
                        .OfType<ReturnStatementSyntax>()
                        .FirstOrDefault();

                    if (returnStatement?.Expression != null)
                    {
                        definition.BuildTree = ParseComponentExpression(returnStatement.Expression);
                    }
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
                ImplicitDefaultJs = prop.Initializer == null ? ImplicitEnumDefaultJs(prop) : null,
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

            var methodName = method.Identifier.Text;
            if (methodName == "Render" || methodName == "Build" || methodName == "CreateState")
            {
                if (methodName == "Render" || methodName == "Build")
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
            var primaryDef = new MethodDefinition { Name = classDecl.Identifier.Text, ReturnType = "void" };
            foreach (var param in primary.Parameters)
            {
                primaryDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.ValueText,
                    Type = param.Type?.ToString() ?? "object"
                });
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

        var constructors = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.ParameterList.Parameters.Count > 0); // Only non-default constructors

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
        var model = TryGetSemanticModel(classDecl.SyntaxTree);
        if (model == null) return;
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
            .FirstOrDefault(m => m.Identifier.Text == "Build");
        
        if (buildMethod != null)
        {
            // Capture full method node for robust conversion (Phase 2)
            definition.BuildMethodNode = buildMethod;

            var returnStatement = buildMethod.DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .FirstOrDefault();
            
            if (returnStatement?.Expression != null)
            {
                definition.BuildTree = ParseComponentExpression(returnStatement.Expression);
            }
        }
    }
    
    private ComponentTree? ParseComponentExpression(ExpressionSyntax expression)
    {
        // Handle object initializer: new Container { ... }
        if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            return ParseObjectCreation(objectCreation);
        }
        
        // Handle implicit object creation: new() { ... } - treat as Component
        if (expression is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            return ParseImplicitObjectCreation(implicitCreation);
        }
        
        // Handle constructor with args: new Text("content")
        if (expression is InvocationExpressionSyntax invocation)
        {
            // Could be a factory method
            return new ComponentTree
            {
                ComponentType = invocation.Expression.ToString()
            };
        }
        
        return null;
    }
    
    private ComponentTree ParseObjectCreation(ObjectCreationExpressionSyntax objectCreation)
    {
        var tree = new ComponentTree
        {
            ComponentType = objectCreation.Type.ToString()
        };
        
        // Parse constructor arguments
        if (objectCreation.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = objectCreation.ArgumentList.Arguments[0];
            // For Text("content"), store as Content property
            tree.Properties["Content"] = ParsePropertyValue(firstArg.Expression, "Content");
        }
        
        // Parse initializer properties
        if (objectCreation.Initializer != null)
        {
            ParseInitializer(objectCreation.Initializer, tree);
        }
        
        return tree;
    }
    
    private ComponentTree ParseImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax implicitCreation)
    {
        var tree = new ComponentTree
        {
            ComponentType = "Unknown" // Will be inferred from context
        };
        
        if (implicitCreation.Initializer != null)
        {
            ParseInitializer(implicitCreation.Initializer, tree);
        }
        
        return tree;
    }
    
    private void ParseInitializer(InitializerExpressionSyntax initializer, ComponentTree tree)
    {
        foreach (var expr in initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax assignment)
            {
                var propName = assignment.Left.ToString();
                var propValue = ParsePropertyValue(assignment.Right, propName);
                tree.Properties[propName] = propValue;
                
                // Handle Children specially
                if (propName == "Children" && assignment.Right is InitializerExpressionSyntax childInit)
                {
                    foreach (var childExpr in childInit.Expressions)
                    {
                        var childTree = ParseComponentExpression(childExpr);
                        if (childTree != null)
                        {
                            tree.Children.Add(childTree);
                        }
                    }
                }
            }
        }
    }
    
    private PropertyValue ParsePropertyValue(ExpressionSyntax expression, string? propName = null)
    {
        var value = expression switch
        {
            LiteralExpressionSyntax literal => new PropertyValue
            {
                Type = literal.Kind() switch
                {
                    SyntaxKind.StringLiteralExpression => PropertyValueType.String,
                    SyntaxKind.NumericLiteralExpression => PropertyValueType.Number,
                    SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => PropertyValueType.Boolean,
                    _ => PropertyValueType.String
                },
                StringValue = literal.Token.ValueText
            },
            
            InterpolatedStringExpressionSyntax interpolated => new PropertyValue
            {
                Type = PropertyValueType.Expression,
                Expression = interpolated.ToString(),
                ExpressionNode = interpolated
            },
            
            // Lambda expression: (v) => SetState(() => _message = v)
            ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax => new PropertyValue
            {
                Type = PropertyValueType.EventHandler,
                Expression = expression.ToString(),
                ExpressionNode = expression
            },
            
            // Member access: AppStyles.Button
            MemberAccessExpressionSyntax memberAccess => new PropertyValue
            {
                Type = memberAccess.Name.ToString() switch
                {
                    _ when memberAccess.Expression.ToString().Contains("Styles") => PropertyValueType.StyleClass,
                    _ => PropertyValueType.Expression
                },
                Expression = memberAccess.ToString(),
                ExpressionNode = memberAccess
            },
            
            // Object creation: new Container { ... }
            ObjectCreationExpressionSyntax objCreation => new PropertyValue
            {
                Type = PropertyValueType.Component,
                ComponentValue = ParseObjectCreation(objCreation)
            },
            
            // Initializer expression { ... }
            InitializerExpressionSyntax initExpr => new PropertyValue
            {
                Type = PropertyValueType.ComponentList,
                ListValue = initExpr.Expressions
                    .Select(e => ParseComponentExpression(e))
                    .Where(c => c != null)
                    .Cast<ComponentTree>()
                    .ToList()
            },
            
            // Default: treat as expression
            _ => new PropertyValue
            {
                Type = PropertyValueType.Expression,
                Expression = expression.ToString(),
                ExpressionNode = expression
            }
        };

        // Heuristic: automatically treat "On*" properties as handlers if they ended up as expressions
        if (propName != null && propName.StartsWith("On") && value.Type == PropertyValueType.Expression)
        {
            value.Type = PropertyValueType.EventHandler;
        }

        return value;
    }
}
