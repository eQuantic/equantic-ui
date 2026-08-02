using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Generates TypeScript code from parsed component definitions.
/// Output is designed to be bundled by Bun.
/// </summary>
public class TypeScriptEmitter
{
    private readonly StringBuilder _output = new(); // Legacy, to be removed
    private TypeScriptCodeBuilder _builder = new(); // New builder
    public TypeScriptCodeBuilder.ClassBuilder? ClassBuilder { get; set; }

    // Legacy helper to bridge during refactor
    private void WriteLn(string line = "") => _builder.Line(line);
    private void Indent() => _builder.Indent();
    private void Dedent() => _builder.Dedent();
    private readonly CSharpToJsConverter _converter = new();
    private ComponentDependencyResolver? _dependencyResolver;

    /// <summary>
    /// C# primitive and .NET-compat type names that map to JS primitives or the <c>$eq.*</c> runtime —
    /// never a user module, so they must be excluded from generated <c>import { X } from "./X"</c> lines.
    /// </summary>
    private static readonly HashSet<string> NonImportableTypes = new(StringComparer.Ordinal)
    {
        // C# primitives
        "int", "uint", "long", "ulong", "short", "ushort", "byte", "sbyte",
        "float", "double", "decimal", "bool", "char", "string", "object", "void", "dynamic", "nint", "nuint",
        // BCL / .NET-compat types backed by the runtime or JS built-ins
        "DateTime", "DateTimeOffset", "TimeSpan", "DateOnly", "TimeOnly", "Guid", "Math", "MathF",
        "Convert", "Console", "Enumerable", "Task", "Action", "Func", "Nullable",
        "StringBuilder", "Regex", "Exception", "Type", "Uri",
    };

    /// <summary>
    /// Sets the dependency resolver for automatic dependency detection
    /// </summary>
    public void SetDependencyResolver(ComponentDependencyResolver resolver)
    {
        _dependencyResolver = resolver;
    }

    public List<TypeScriptCodeBuilder.SourceMapping> GetLastMappings() => _builder.GetMappings();

    /// <summary>Transpilation diagnostics raised during the most recent <see cref="Emit"/> call.</summary>
    public IReadOnlyList<ConversionDiagnostic> GetLastDiagnostics() => _converter.Diagnostics;

    /// <summary>
    /// Generate TypeScript code for a component
    /// </summary>
    public string Emit(ComponentDefinition component, SemanticModel? semanticModel = null)
    {
        _builder = new TypeScriptCodeBuilder();
        _converter.SetSemanticModel(semanticModel);
        _converter.ClearDiagnostics();
        _output.Clear();

        // Clear UsedHelpers from previous compilations
        component.UsedHelpers.Clear();
        _converter.UsedAppTypes.Clear();

        // Note: We'll emit imports AFTER generating component code
        // to ensure UsedHelpers is populated

        // Define component class
        var baseClass = component.BaseClassName ?? (component.IsPrimitive ? "HtmlElement" : (component.IsStateful ? "StatefulComponent" : "StatelessComponent"));
        
        // Strip generics from base class name for JS/TS inheritance
        if (baseClass.Contains('<'))
        {
            baseClass = baseClass.Substring(0, baseClass.IndexOf('<'));
        }
        
        _builder.Class(component.Name, baseClass, c =>
            {
                // Component-level fields (static data / consts / instance fields), emitted at the top of
                // the class. Skipped for primitives' INSTANCE fields, whose base ctor sets every prop via
                // Object.assign — an uninitialised instance field would clobber that after super(); a static
                // field is class-level and carries its own initializer, so it is always safe.
                if (component.ComponentFields.Count > 0)
                {
                    _converter.SetCurrentClass(component.Name);
                    foreach (var field in component.ComponentFields)
                    {
                        if (component.IsPrimitive && !field.IsStatic) continue;
                        var tsType = DeclarationType(component, field.Type);
                        var tsDefault = field.DefaultValueNode != null
                            ? _converter.ConvertExpression(field.DefaultValueNode, field.Type)
                            : (field.DefaultValue != null ? ConvertToTsValue(field.DefaultValue, field.Type) : null);
                        // C# value types default without an initializer (`private int _count;` is 0);
                        // an uninitialized TS field is `undefined` and would poison arithmetic (NaN).
                        tsDefault ??= ImplicitValueTypeDefault(field.Type);
                        c.Field(field.Name.ToCamelCase(), tsType, tsDefault, field.DefaultValueNode, field.IsStatic);
                    }
                }

                if (component.IsPrimitive)
                {
                    // Remove field declarations for primitives.
                    // The base HtmlElement constructor calls Object.assign(this, props), 
                    // which sets these properties. If we declare fields here without initializers,
                    // they will be initialized to undefined AFTER super(), overwriting the values.

                    // Emit constructor for primitive
                    // ALWAYS accept props and pass to super, even if C# constructor has no params
                    // This is critical for Object.assign pattern in Component base class
                    
                    var ctor = component.IsPrimitive ? component.Constructors.OrderByDescending(ctr => ctr.Parameters.Count).FirstOrDefault() : null;
                    var hasExplicitParams = ctor?.Parameters.Count > 0;

                    string jsParams;
                    if (hasExplicitParams)
                    {
                        // Constructor has explicit params (e.g., Heading(content, level))
                        var paramList = string.Join(", ", ctor!.Parameters.Select(p => $"{p.Name}: any"));
                        jsParams = paramList;
                    }
                    else
                    {
                        // Constructor has no params - accept generic props for Object.assign
                        jsParams = "props?: any";
                    }

                    c.Constructor(jsParams, () =>
                    {
                        // Pass props to super
                        c.Raw(hasExplicitParams ? "super();" : "super(props);");

                        // Assign explicit parameters as properties
                        if (hasExplicitParams)
                        {
                            foreach (var param in ctor!.Parameters)
                            {
                                c.Raw($"this.{param.Name.ToCamelCase()} = {param.Name};");
                            }
                        }

                        // Apply defaults for properties not provided in props (only if still undefined)
                        foreach (var prop in component.Properties.Where(p => p.IsPublic && p.DefaultValue != null))
                        {
                            var camelName = prop.Name.ToCamelCase();
                            var tsDefault = prop.DefaultValueNode != null 
                                ? _converter.ConvertExpression(prop.DefaultValueNode, prop.Type)
                                : ConvertToTsValue(prop.DefaultValue, prop.Type);
                            
                            c.Raw($"if (this.{camelName} === undefined) this.{camelName} = {tsDefault};");
                        }

                        // Execute C# constructor body (e.g., Direction = FlexDirection.Column)
                        if (ctor?.SyntaxNode?.Body != null)
                        {
                            _converter.SetCurrentClass(component.Name);
                            var jsBody = _converter.Convert(ctor.SyntaxNode.Body);
                            jsBody = jsBody.Trim();
                            if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                            {
                                jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                            }
                            if (!string.IsNullOrWhiteSpace(jsBody))
                            {
                                c.Raw(jsBody, ctor.SyntaxNode.Body);
                            }
                        }
                    });

                    // Emit Render method for primitive - ONLY if defined or it's the base primitive
                    if (component.BuildMethodNode != null && component.BuildMethodNode.Body != null)
                    {
                        c.Method("render", "", false, () => 
                        {
                            // Discover `out var x` variables to hoist. Only single-variable designations:
                            // parenthesised ones (`var (a, b) = …` deconstruction) are emitted as
                            // `let { … } = …` by the assignment strategy, so hoisting them would yield
                            // an invalid `let (a, b);`.
                            var outVars = component.BuildMethodNode.Body.DescendantNodes()
                                .OfType<DeclarationExpressionSyntax>()
                                .Select(d => d.Designation)
                                .OfType<SingleVariableDesignationSyntax>()
                                .Select(s => s.Identifier.Text)
                                .Distinct();

                            foreach (var v in outVars)
                            {
                                c.Raw($"let {v};");
                            }

                            _converter.SetCurrentClass(component.Name);
                            var jsBody = _converter.Convert(component.BuildMethodNode.Body);
                            jsBody = jsBody.Trim();
                            if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                            {
                                jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                            }
                            c.Raw(jsBody, component.BuildMethodNode.Body);
                        });
                    }
                    else if (component.BaseClassName == "HtmlElement" || component.BaseClassName == null)
                    {
                        // Fallback for base primitives that MUST have a render
                        c.Method("render", "", false, () => 
                        {
                            c.Raw("return { tag: 'div', attributes: {}, events: {}, children: [] };");
                        });
                    }

                    // Emit helper methods
                    foreach (var method in component.Methods)
                    {
                        if (method.Name == "Build" || method.Name == "Render") continue;
                        EmitMethod(method, c, component, component.Name);
                    }
                }
                else if (component.IsStateful)
                {
                    c.Method("createState", "", false, () => 
                    {
                        c.Raw($"return new {component.StateClassName}(this)");
                    });
                }
                // A concrete component, OR an abstract base that still defines a concrete Build/members for
                // its subclasses to inherit (a pure-abstract class with no Build emits nothing here).
                else if (!component.IsAbstract || component.BuildMethodNode != null)
                {
                    // Computed/get-set/static properties become real TS members (auto-props flow through
                    // the base Object.assign(props) instead).
                    EmitComponentProperties(component, c);

                    // Constructor: assign positional params, apply auto-property defaults (only when a prop
                    // wasn't supplied — the base ctor's Object.assign runs first), then run the C# ctor body.
                    var ctorDef = component.Constructors.OrderByDescending(ct => ct.Parameters.Count).FirstOrDefault();
                    var ctorParams = ctorDef?.Parameters ?? new System.Collections.Generic.List<ParameterDefinition>();
                    // Auto-properties that must hold a value even when the caller supplies none: an explicit
                    // C# initializer, or (enums) the implicit zero-member default the server-side C# has and
                    // `undefined` does not — see PropertyDefinition.ImplicitDefaultJs.
                    var autoDefaults = component.Properties
                        .Where(p => !p.IsStatic && IsAutoProperty(p)
                                    && (p.DefaultValueNode != null || p.ImplicitDefaultJs != null))
                        .ToList();
                    var hasCtorBody = ctorDef?.BodyNode != null;
                    if (ctorParams.Count > 0 || autoDefaults.Count > 0 || hasCtorBody)
                    {
                        // C# optional parameters keep their defaults as JS default parameters
                        // (`variant: any = 'primary'`) — without them `new Button("x")` would run the
                        // body with `undefined` where C# guarantees `Variant.Primary`.
                        var paramList = string.Join(", ", ctorParams.Select(p => p.DefaultValueNode != null
                            ? $"{p.Name.ToCamelCase()}: any = {_converter.ConvertExpression(p.DefaultValueNode, p.Type)}"
                            : $"{p.Name.ToCamelCase()}?: any"));
                        var signature = paramList.Length > 0 ? $"{paramList}, props?: any" : "props?: any";
                        c.Constructor(signature, () =>
                        {
                            c.Raw("super(props);");
                            foreach (var param in ctorParams)
                            {
                                var camelName = param.Name.ToCamelCase();
                                // PRIMARY-constructor params are implicit fields — always assign. With an
                                // EXPLICIT ctor body, only params that map onto a real auto-property assign
                                // here; one that merely feeds a private/state field (`NestedChild(label)` →
                                // `_label`) has no `this.<name>` to write — the C# ctor body does the wiring.
                                if (hasCtorBody && !component.Properties.Any(pr => !pr.IsStatic && pr.Name.ToCamelCase() == camelName))
                                    continue;
                                c.Raw($"if ({camelName} !== undefined) this.{camelName} = {camelName};");
                            }
                            _converter.SetCurrentClass(component.Name);
                            foreach (var p in autoDefaults)
                            {
                                var cn = p.Name.ToCamelCase();
                                var def = p.DefaultValueNode != null
                                    ? _converter.ConvertExpression(p.DefaultValueNode, p.Type)
                                    : p.ImplicitDefaultJs!;
                                c.Raw($"if (this.{cn} === undefined) this.{cn} = {def};");
                            }
                            if (hasCtorBody)
                            {
                                var body = StripJsBraces(_converter.Convert(ctorDef!.BodyNode!));
                                if (!string.IsNullOrWhiteSpace(body)) c.Raw(body, ctorDef.BodyNode);
                            }
                        });
                    }

                    // Build method — underscore the param when the body never uses it
                    // (noUnusedParameters-clean output; the override contract ignores names).
                    var buildParamName = component.BuildMethodNode?.Body?.ToString().Contains("context") == false
                        ? "_context" : "context";
                    c.Method("build", $"{buildParamName}: BuildContext", false, () =>
                    {
                         if (component.BuildMethodNode != null && component.BuildMethodNode.Body != null)
                         {
                            // Use robust converter for stateless build body
                            _converter.SetCurrentClass(component.Name);
                            var jsBody = _converter.Convert(component.BuildMethodNode.Body);
                            jsBody = jsBody.Trim();
                            if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                            {
                                jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                            }
                            c.Raw(jsBody, component.BuildMethodNode.Body);
                         }
                         else if (component.BuildMethodNode?.ExpressionBody != null)
                         {
                            // Expression-bodied Build: `IComponent Build(ctx) => new Box {…};`. Convert the
                            // whole expression (handles trees, ternaries, switch-expressions, method calls,
                            // interpolation — anything the converter knows) and return it.
                            _converter.SetCurrentClass(component.Name);
                            var expr = _converter.ConvertExpression(component.BuildMethodNode.ExpressionBody.Expression);
                            c.Raw($"return {expr};", component.BuildMethodNode.ExpressionBody.Expression);
                         }
                         else if (component.BuildTree != null)
                         {
                             c.Raw("return (");
                             EmitComponentTree(component.BuildTree);
                             c.Raw(");");
                         }
                         else
                         {
                             // Fallback for components without explicit Build method
                             c.Raw("throw new Error('Build method not implemented');");
                         }
                    });

                    // Emit helper methods
                    foreach (var method in component.Methods)
                    {
                        if (method.Name == "Build" || method.Name == "Render") continue;
                        EmitMethod(method, c, component, component.Name);
                    }
                }
                // Abstract classes: no build method emitted
                
                // Server Actions
                foreach (var action in component.ServerActions)
                {
                    ClassBuilder = c;
                    var paramsList = string.Join(", ", action.Parameters.Select(p => $"{p.Name}: {CSharpTypeToTypeScript(p.Type)}"));
                    var argsList = string.Join(", ", action.Parameters.Select(p => p.Name));
                    var returnType = CSharpTypeToTypeScript(action.ReturnType);

                    c.Method(action.MethodName.ToCamelCase(), paramsList, true, () => 
                    {
                        c.Raw($"return await getServerActionsClient().invoke('{action.ActionId}', [{argsList}])");
                    }, sourceNode: action.SyntaxNode);
                }
            }, component.TypeParameters);

        // State class logic hooks into builder via EmitStateClass (already refactored)
        // We just need to ensure EmitStateClass writes to builder, OR we inline it here if we want full builder control in one pass.
        // Given current structure, we rely on EmitStateClass using _builder.
        if (component.IsStateful)
        {
            EmitStatefulComponent(component); // This method needs update to NOT use WriteLn manually if we want full builder purity, but for now we mix.
        }

        // Transfer UsedHelpers from converter to component after all code generation
        foreach (var helper in _converter.UsedHelpers)
        {
            component.UsedHelpers.Add(helper);
        }

        // Generate component code without imports
        var componentCode = _builder.ToString();

        // NESTED static classes (each section's private `Copy` et al.) embed in THIS module as
        // plain (non-exported) classes above the component — as their own modules, two same-named
        // nested classes would overwrite each other's file, and the C# scoping is lexical anyway.
        var nestedCode = string.Empty;
        if (component.BuildMethodNode?.Parent is ClassDeclarationSyntax ownerClass)
        {
            var nb = new TypeScriptCodeBuilder();
            foreach (var nested in ownerClass.Members.OfType<ClassDeclarationSyntax>()
                         .Where(n => n.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)))
            {
                nb.Class(nested.Identifier.Text, null, c => EmitStaticMembers(nested, c),
                    sourceNode: nested, export: false);
            }
            nestedCode = nb.ToString();
        }

        // Generate imports based on populated UsedHelpers. The emitted body is the authority on what is
        // actually referenced, so it is passed in to drop imports the scan over-collected.
        var importsCode = GenerateImports(component, nestedCode + componentCode);

        // Return imports + nested scope classes + component code
        return importsCode + "\n" + nestedCode + componentCode;
    }
    
    /// <summary>Every identifier the emitted body mentions — the authority on which imports are live.</summary>
    private static HashSet<string> ReferencedIdentifiers(string emittedBody) =>
        System.Text.RegularExpressions.Regex
            .Matches(emittedBody, @"[A-Za-z_$][A-Za-z0-9_$]*")
            .Select(m => m.Value)
            .ToHashSet();

    /// <param name="emittedBody">The already-emitted class body. The type scan is deliberately permissive
    /// (it walks the build tree, member bodies, field initializers and declared property types, plus the
    /// resolver's transitive closure) so nothing needed is ever missed; filtering the result against what
    /// the body actually mentions removes the over-collection instead of narrowing the scan and risking a
    /// missing import — an unused import is only a warning, a missing one is a runtime "X is not defined".</param>
    private string GenerateImports(ComponentDefinition component, string emittedBody)
    {
        var referenced = ReferencedIdentifiers(emittedBody);
        // Core runtime imports
        var coreImports = new HashSet<string> { "Component", "BuildContext", "HtmlElement" };

        if (component.IsStateful)
        {
            coreImports.Add("StatefulComponent");
            coreImports.Add("ComponentState");
        }
        else if (component.IsSharedStateful)
        {
            coreImports.Add("SharedStatefulComponent");
        }
        else if (!component.IsPrimitive)
        {
            coreImports.Add("StatelessComponent");
        }

        if (component.ServerActions.Count > 0)
        {
            coreImports.Add("getServerActionsClient");
        }

        // Component imports based on what's used in the component
        var componentTypes = CollectComponentTypes(component.BuildTree);

        // Also scan procedural code in BuildMethodNode
        if (component.BuildMethodNode != null)
        {
             var localNames = new HashSet<string>(component.Properties.Select(p => p.Name));
             foreach (var m in component.Methods) localNames.Add(m.Name);
             localNames.Add(component.Name);

             var proceduralTypes = CollectComponentTypesFromNode(component.BuildMethodNode, localNames);
             foreach (var t in proceduralTypes) componentTypes.Add(t);
        }

        // Scan component-field initializers too — a type referenced ONLY in a static/instance field
        // initializer (e.g. `static People = new() { new Person(...) }`) still needs its import, or the
        // emitted static initializer throws "Person is not defined" at module load.
        foreach (var field in component.ComponentFields)
        {
            if (field.DefaultValueNode == null) continue;
            foreach (var t in CollectComponentTypesFromNode(field.DefaultValueNode, new HashSet<string> { component.Name }))
            {
                componentTypes.Add(t);
            }
        }

        // Types the CONVERSION introduced into the output (extension calls reduced to
        // `Class.method(...)`) — invisible to every syntax walk above by construction.
        foreach (var t in _converter.UsedAppTypes) componentTypes.Add(t);

        // Scan helper-method and property-accessor BODIES too — a type constructed ONLY inside a helper
        // (e.g. `Money Make() => new Money(..)`) or inside a property body must still be imported, or the
        // emitted body throws "<Type> is not defined". Previously only a property's declared TYPE was added
        // (so a property happening to return its own type imported by luck), never the method/property body.
        var memberLocalNames = new HashSet<string>(component.Properties.Select(p => p.Name)) { component.Name };
        foreach (var m in component.Methods) memberLocalNames.Add(m.Name);
        foreach (var method in component.Methods)
        {
            if (method.SyntaxNode == null) continue;
            foreach (var t in CollectComponentTypesFromNode(method.SyntaxNode, memberLocalNames))
                componentTypes.Add(t);
        }
        foreach (var prop in component.Properties)
        {
            if (prop.Node == null) continue;
            foreach (var t in CollectComponentTypesFromNode(prop.Node, memberLocalNames))
                componentTypes.Add(t);
        }

        // Add property types to imports
        foreach (var prop in component.Properties)
        {
            var type = prop.Type;
            if (type.Contains("<"))
            {
                var startIndex = type.IndexOf('<') + 1;
                var endIndex = type.LastIndexOf('>');
                if (endIndex > startIndex)
                {
                    type = type.Substring(startIndex, endIndex - startIndex);
                }
            }
            if (type.EndsWith("?")) type = type.Substring(0, type.Length - 1);
            
            componentTypes.Add(type);
        }

        // CRITICAL: Add base class to component types (for inheritance like "Column extends Flex")
        if (!string.IsNullOrEmpty(component.BaseClassName))
        {
            var baseClass = component.BaseClassName;
            // Clean generic types
            if (baseClass.Contains('<'))
            {
                baseClass = baseClass.Substring(0, baseClass.IndexOf('<'));
            }
            componentTypes.Add(baseClass);
        }

        // Runtime-provided types are a SOURCE, not just a filter: a vocabulary type referenced only
        // inside a config-object expression (`width: SizeValue.fill`) is invisible to the syntactic
        // collectors above, but the parser's semantic sweep saw it — without the import the emitted
        // module throws "<Type> is not defined" at load.
        foreach (var runtimeType in component.RuntimeProvidedTypes)
        {
            componentTypes.Add(runtimeType);
        }

        // APP-LEVEL types are a SOURCE for the same reason (the site dogfood found the hole): a
        // static helper reached only through a member access (`Brand.Violet`, `Copy.Title`) never
        // appears where the syntactic collectors look, so the module referenced it without importing
        // it and the browser threw "<Type> is not defined" — with no build error. Only names the
        // per-app scan KNOWS became modules are promoted, so this can never emit a dangling import.
        if (_dependencyResolver != null)
        {
            foreach (var appType in component.AppTypes)
            {
                if (appType == component.Name) continue;
                if (_dependencyResolver.GetAllStaticHelpers().Contains(appType)
                    || _dependencyResolver.GetAllRecords().Contains(appType)
                    || _dependencyResolver.GetAllComponents().Contains(appType))
                {
                    componentTypes.Add(appType);
                }
            }
        }

        // AUTOMATIC DEPENDENCY RESOLUTION
        // Use dependency resolver to find transitive dependencies (e.g., Row → Flex). Runtime-provided
        // names must NOT seed it: the resolver is name-keyed over the per-app scan, so a vocabulary
        // "Row" would pull the WEB Row's dependency chain (Flex) into a page that never uses it.
        if (_dependencyResolver != null)
        {
            var perAppSeeds = componentTypes
                .Where(t => !component.RuntimeProvidedTypes.Contains(t.Contains('.') ? t[(t.LastIndexOf('.') + 1)..] : t))
                .ToHashSet();
            var dependencies = _dependencyResolver.ResolveDependencies(perAppSeeds);
            foreach (var dep in dependencies)
            {
                componentTypes.Add(dep);
            }
        }

        var userComponents = new List<string>();

        foreach (var type in componentTypes)
        {
            var cleanType = type.Trim().Replace("?", "");
            if (cleanType.Contains("<")) cleanType = cleanType.Split('<')[0];
            // Array-typed properties reference the ELEMENT type's module (DialogAction[] → DialogAction).
            while (cleanType.EndsWith("[]")) cleanType = cleanType[..^2].TrimEnd();
            // Extract simple name from fully-qualified names (e.g., "eQuantic.UI.Web.Components.Navigation.Breadcrumb" → "Breadcrumb")
            if (cleanType.Contains('.')) cleanType = cleanType.Substring(cleanType.LastIndexOf('.') + 1);

            if (string.IsNullOrEmpty(cleanType) || cleanType == "string" || cleanType == "number" || cleanType == "boolean" || cleanType == "any")
                continue;

            // C# primitives and .NET-compat types map to JS primitives or the `$eq.*` runtime — they are
            // NEVER user modules, so a stray reference (e.g. an `int` property type, a `DateTime`/`Math`
            // usage) must not become `import { int } from "./int"`.
            if (NonImportableTypes.Contains(cleanType))
                continue;

            // Skip HtmlNode - it's a type-only interface, not a runtime class
            if (cleanType == "HtmlNode")
                continue;

            // Skip runtime utilities - they're added from UsedHelpers below
            if (component.UsedHelpers.Contains(cleanType))
                continue;

            // Enum members lower to string literals — the enum type name never appears in emitted code,
            // so importing it would reference a module that doesn't exist.
            if (component.EnumTypes.Contains(cleanType))
                continue;

            // Never import the component's own name (a runtime-provided LIBRARY component referencing
            // itself would otherwise import the very class it declares).
            if (cleanType == component.Name)
                continue;

            // Exception types lower to `new Error(...)` (ObjectCreationStrategy) — the type name never
            // survives into emitted code, so importing it would reference a module that doesn't exist.
            if (cleanType.EndsWith("Exception"))
                continue;

            // Types the runtime provides (the shared vocabulary — discovered semantically by the parser,
            // see ComponentDefinition.RuntimeProvidedTypes) import from @equantic/runtime, never ./<Type>.
            if (component.RuntimeProvidedTypes.Contains(cleanType))
            {
                coreImports.Add(cleanType);
                continue;
            }

            if (IsRuntimeComponent(cleanType))
            {
                coreImports.Add(cleanType);
            }
            else
            {
                userComponents.Add(cleanType);
            }
        }

        // Note: .NET-compat helpers (format, round, dec, long, dateTime, timeSpan, stringBuilder,
        // parseEnum) are emitted as `$eq.*` and provided by the global `$eq` namespace, so they are
        // NOT imported here. Only the remaining runtime utilities (e.g. StyleBuilder/ClassBuilder,
        // tracked in UsedHelpers by RuntimeUtilityStrategy) are imported.
        foreach (var helper in component.UsedHelpers)
        {
            coreImports.Add(helper);
        }

        // Create a temporary builder for imports only
        var importsBuilder = new TypeScriptCodeBuilder();
        importsBuilder.Import(coreImports.Where(referenced.Contains), "@equantic/runtime");

        // Import user types that we actually emit as their own module: UI components AND data records
        // (each gets a generated .ts file). The set is discovered by scanning the project — no fixed
        // skip-list — so any referenced type that we emit is imported, and anything else is left alone.
        var knownComponents = _dependencyResolver?.GetAllComponents().ToHashSet() ?? new HashSet<string>();
        var knownRecords = _dependencyResolver?.GetAllRecords() ?? (IReadOnlySet<string>)new HashSet<string>();
        var knownStaticHelpers = _dependencyResolver?.GetAllStaticHelpers() ?? (IReadOnlySet<string>)new HashSet<string>();
        foreach (var userComp in userComponents.OrderBy(x => x))
        {
            if (userComp == component.Name) continue;
            var isEmittedType = knownComponents.Contains(userComp) || knownRecords.Contains(userComp)
                                || knownStaticHelpers.Contains(userComp);
            // When a resolver is present it is authoritative: import ONLY types we actually emit
            // (records/components it discovered). This drops references that aren't modules — primitives,
            // static-field names read as ClassName.X, helper-class names, etc. — instead of inventing a
            // bogus `./X`. (Without a resolver we keep the old permissive behavior for isolated snippets.)
            if (_dependencyResolver != null && !isEmittedType)
                continue;
            if (!referenced.Contains(userComp))
                continue;
            importsBuilder.Import(new[] { userComp }, $"./{userComp}");
        }

        return importsBuilder.ToString();
    }
    
    /// <summary>The JS literal for C#'s implicit <c>default(T)</c> on a FIELD with no initializer —
    /// numeric and boolean value types only; everything else stays uninitialized (≈ null).</summary>
    private static string? ImplicitValueTypeDefault(string csharpType) => csharpType.TrimEnd('?') switch
    {
        "int" or "Int32" or "short" or "Int16" or "byte" or "sbyte" or "uint" or "UInt32"
            or "ushort" or "UInt16" or "float" or "Single" or "double" or "Double" => "0",
        "bool" or "Boolean" => "false",
        "decimal" or "Decimal" => "$eq.num.dec(0)",
        "long" or "Int64" or "ulong" or "UInt64" => "$eq.num.long(0)",
        _ => null,
    };

    private bool IsRuntimeComponent(string typeName)
    {
        // Only core runtime types are exported from @equantic/runtime
        // UI components (Box, Button, Text, etc.) are generated and imported from local files
        return typeName switch
        {
            "HtmlNode" or "HtmlStyle" or "ServiceKey" or "ServiceProvider" => true,
            "Component" or "BuildContext" or "HtmlElement" => true,
            "StatefulComponent" or "StatelessComponent" or "SharedStatefulComponent" or "ComponentState" => true,
            "getServerActionsClient" or "getRootServiceProvider" => true,
            // StyleBuilder/ClassBuilder are now emitted as `$eq.css.*` (global), not imported.
            _ => false
        };
    }

    private bool UsesFormatting(ComponentDefinition component)
    {
        if (component.SyntaxTree == null) return false;
        
        var root = component.SyntaxTree.GetRoot();
        return root.DescendantNodes()
            .OfType<InterpolatedStringExpressionSyntax>()
            .Any(i => i.Contents.OfType<InterpolationSyntax>()
                .Any(c => c.FormatClause != null || c.AlignmentClause != null));
    }

    private HashSet<string> CollectComponentTypes(ComponentTree? tree)
    {
        var types = new HashSet<string>();
        if (tree == null) return types;

        types.Add(tree.ComponentType);
        foreach (var child in tree.Children)
        {
            foreach (var t in CollectComponentTypes(child))
            {
                types.Add(t);
            }
        }
        return types;
    }

    private HashSet<string> CollectComponentTypesFromNode(SyntaxNode? node, HashSet<string>? localNames = null)
    {
        var types = new HashSet<string>();
        if (node == null) return types;
        
        var creations = node.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in creations)
        {
             var typeName = creation.Type.ToString();
             if (typeName.Contains("<")) typeName = typeName.Split('<')[0];
             // Extract simple name from fully-qualified names (e.g., "System.Collections.Generic.List" → "List")
             if (typeName.Contains('.')) typeName = typeName.Substring(typeName.LastIndexOf('.') + 1);
             types.Add(typeName);
        }

        // EVERY `Upper.member` access roots an import candidate — method calls AND plain static
        // property/field reads (`EquanticBrand.BtnPrimaryFrom`): a static token class referenced
        // only by properties must still import. Downstream filters drop enums, runtime-provided
        // types and anything the resolver doesn't know.
        foreach (var memberAccess in node.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (memberAccess.Expression is IdentifierNameSyntax identifier)
            {
                var name = identifier.Identifier.Text;
                if (!string.IsNullOrEmpty(name) && char.IsUpper(name[0])
                    && (localNames == null || !localNames.Contains(name)))
                {
                    types.Add(name);
                }
            }
        }

        // `x is Foo f` lowers to `x instanceof Foo` for component-model classes (PatternConverter) —
        // the pattern's type must import exactly like a constructed type. Non-class pattern types
        // added here are dropped by the downstream filters (enums, exceptions, resolver-unknown).
        foreach (var pattern in node.DescendantNodes().OfType<DeclarationPatternSyntax>())
        {
            var typeName = pattern.Type.ToString();
            if (typeName.Contains('.')) typeName = typeName[(typeName.LastIndexOf('.') + 1)..];
            types.Add(typeName);
        }

        // DECLARED types carry the only name a TARGET-TYPED `new(...)` ever states
        // (`static readonly MenuPanel Products = new(...)` — the creation itself is nameless), so
        // field/property/local declaration types must import like constructed ones. Tuple and
        // array declarations contribute their ELEMENT type names the same way.
        foreach (var declaration in node.DescendantNodes().OfType<VariableDeclarationSyntax>())
            CollectDeclaredTypeNames(declaration.Type, types);
        foreach (var property in node.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            CollectDeclaredTypeNames(property.Type, types);
        return types;
    }

    /// <summary>Simple type names inside a declared type — through arrays, nullable, generics and
    /// tuples (`(string Role, MenuEntry Entry)[]` contributes <c>MenuEntry</c>). Predefined
    /// keywords (string/int/…) never surface; downstream filters drop anything unknown.</summary>
    private static void CollectDeclaredTypeNames(TypeSyntax type, HashSet<string> types)
    {
        switch (type)
        {
            case ArrayTypeSyntax array: CollectDeclaredTypeNames(array.ElementType, types); break;
            case NullableTypeSyntax nullable: CollectDeclaredTypeNames(nullable.ElementType, types); break;
            case TupleTypeSyntax tuple:
                foreach (var element in tuple.Elements) CollectDeclaredTypeNames(element.Type, types);
                break;
            case GenericNameSyntax generic:
                foreach (var argument in generic.TypeArgumentList.Arguments) CollectDeclaredTypeNames(argument, types);
                break;
            case QualifiedNameSyntax qualified: CollectDeclaredTypeNames(qualified.Right, types); break;
            case IdentifierNameSyntax identifier when char.IsUpper(identifier.Identifier.Text[0]):
                types.Add(identifier.Identifier.Text);
                break;
        }
    }
    
    private void EmitStatefulComponent(ComponentDefinition component)
    {
        // Only emit the State class. The Component class is emitted by the main Emit method.
        
        WriteLn();
        
        _builder.Class(component.StateClassName!, "ComponentState", c =>
        {
            // Private component reference
            c.Field("_component", component.Name);
            c.Field("_needsRender", "boolean", "false");
            
            // Typed fields
            foreach (var field in component.StateFields)
            {
                var tsType = CSharpTypeToTypeScript(field.Type);
                var tsDefault = field.DefaultValueNode != null 
                    ? _converter.ConvertExpression(field.DefaultValueNode, field.Type)
                    : ConvertToTsValue(field.DefaultValue ?? GetDefaultForType(field.Type), field.Type);
                c.Field(field.Name, tsType, tsDefault);
            }

            // Constructor
            c.Constructor($"component: {component.Name}", () =>
            {
                c.Raw("super();");
                c.Raw("this._component = component;");
            });
            
            // SetState
            c.Method("setState", "fn: () => void", false, () => 
            {
                c.Raw("fn();");
                c.Raw("this._needsRender = true;");
                c.Raw("this._component._scheduleRender();");
            });

            // Custom methods (Phase 2: Semantic Body)
            foreach (var method in component.Methods)
            {
                EmitMethod(method, c, component, component.StateClassName);
            }
            
            // Build method
            c.Method("build", "context: BuildContext", false, () =>
            {
                if (component.BuildMethodNode != null && component.BuildMethodNode.Body != null)
                {
                    // Use robust converter to emit full body (supports variables, loops, etc.)
                   _converter.SetCurrentClass(component.StateClassName);
                   var jsBody = _converter.Convert(component.BuildMethodNode.Body);
                   
                   // Remove outer braces since c.Method adds them (via logic or we need to be careful)
                   // Actually c.Method adds braces. Convert(Block) adds braces. 
                   // We should strip the outer braces from jsBody to avoid double indentation/bracing if necessary,
                   // OR just emit the content. 
                   // Let's rely on Convert returning "{ ... }" and we just inject the *content*?
                   // CSharpToJsConverter struct: ConvertBlock returns "{ stmt; stmt; }"
                   // CodeBuilder Method adds "{ ... }". 
                   // So we need to strip first and last char of jsBody.
                   
                   jsBody = jsBody.Trim();
                   if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                   {
                       jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                   }
                   c.Raw(jsBody, component.BuildMethodNode.Body);
                }
                else if (component.BuildMethodNode?.ExpressionBody != null)
                {
                    // Expression-bodied Build (`=> new X(...)`) — before this branch it silently fell
                    // through to the empty-Container fallback, discarding the page's whole tree.
                    _converter.SetCurrentClass(component.StateClassName);
                    var expression = component.BuildMethodNode.ExpressionBody.Expression;
                    c.Raw($"return {_converter.ConvertExpression(expression)};", expression);
                }
                else if (component.BuildTree != null)
                {
                    c.Raw("return (");
                    EmitComponentTree(component.BuildTree);
                    c.Raw(");");
                }
                else
                {
                    c.Raw("return new Container({});");
                }
            });
        });
        WriteLn();
    }
    
    private void EmitStatelessComponent(ComponentDefinition component)
    {
        // No-op: Stateless components are fully handled by Emit()
    }
    
    /// <summary>TS names that always resolve without an import.</summary>
    private static readonly HashSet<string> IntrinsicTsTypes = new()
    {
        "string", "number", "boolean", "any", "unknown", "void", "null", "undefined", "Date",
        "object", "symbol", "bigint", "never",
    };

    /// <summary>
    /// The TS type for a TYPE-ONLY property declaration. A declaration must never introduce a name the
    /// emitted module cannot resolve — that would just trade a TS2339 for a TS2304 — so the type is kept
    /// only when this module is known to import it: intrinsics, runtime-provided vocabulary, and types the
    /// resolver actually emits as their own module (property types feed the import scan, see GetImports).
    /// C# enums lower to string literals and have no TS counterpart, so they degrade to <c>string</c>;
    /// anything else unresolvable degrades to <c>any</c>, which still restores checking on the rest.
    /// </summary>
    private string DeclarationType(ComponentDefinition component, string? csharpType)
    {
        var ts = CSharpTypeToTypeScript(csharpType);

        // Structural forms (`X[]`, `(a: X) => void`, `Record<…>`) are only as resolvable as their parts;
        // keep them only when every bare identifier they mention resolves.
        var suffix = "";
        while (ts.EndsWith("[]"))
        {
            ts = ts[..^2];
            suffix += "[]";
        }
        if (ts.Contains('<') || ts.Contains("=>") || ts.Contains('('))
            return IsResolvableTsName(component, ts) ? ts + suffix : "any" + suffix;

        // Enum members lower to string literals, so `string` describes the runtime value exactly.
        if (component.EnumTypes.Contains(ts)) return "string" + suffix;

        return (IsResolvableTsName(component, ts) ? ts : "any") + suffix;
    }

    /// <summary>Whether <paramref name="ts"/> resolves in the emitted module — see <see cref="DeclarationType"/>.</summary>
    private bool IsResolvableTsName(ComponentDefinition component, string ts)
    {
        if (string.IsNullOrEmpty(ts)) return false;
        if (IntrinsicTsTypes.Contains(ts)) return true;

        // A composite (`(x: Foo) => void`, `Record<string, any>`): every identifier inside must resolve.
        if (ts.Contains('<') || ts.Contains("=>") || ts.Contains('('))
        {
            var names = System.Text.RegularExpressions.Regex
                .Matches(ts, @"[A-Za-z_][A-Za-z0-9_]*")
                .Select(m => m.Value)
                .Where(n => n is not ("void" or "Record"));
            return names.All(n => IsResolvableTsName(component, n));
        }

        // Enum members lower to string literals, so the enum NAME has no TS counterpart to import.
        if (component.EnumTypes.Contains(ts)) return false;

        if (component.RuntimeProvidedTypes.Contains(ts) || IsRuntimeComponent(ts)) return true;

        return (_dependencyResolver?.GetAllComponents().Contains(ts) ?? false)
            || (_dependencyResolver?.GetAllRecords().Contains(ts) ?? false);
    }

    /// <summary>True for a pure auto-property (`{ get; set; }` / `{ get; init; }`) — no expression body and
    /// no accessor with a body. Auto-props flow through the base Object.assign(props); only computed/get-set
    /// properties need an emitted accessor.</summary>
    private static bool IsAutoProperty(PropertyDefinition p)
    {
        var node = p.Node;
        if (node == null) return true;
        if (node.ExpressionBody != null) return false;
        if (node.AccessorList == null) return true;
        return node.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null);
    }

    /// <summary>Unwrap a converted block body (`{ … }`) to its inner statements for inlining into an
    /// accessor / constructor.</summary>
    private static string StripJsBraces(string js)
    {
        js = js.Trim();
        if (js.StartsWith("{") && js.EndsWith("}")) js = js.Substring(1, js.Length - 2).Trim();
        return js;
    }

    /// <summary>
    /// Emit a component's non-auto properties as real TS members: an expression-bodied or block-bodied
    /// get-only property becomes a getter; get/set with bodies become accessors; a static auto-property
    /// becomes a static field. Pure instance auto-properties are intentionally NOT emitted — the base
    /// Object.assign(props) populates them (with the ctor applying any default).
    /// </summary>
    private void EmitComponentProperties(ComponentDefinition component, TypeScriptCodeBuilder.ClassBuilder c)
    {
        foreach (var prop in component.Properties)
        {
            var node = prop.Node;
            if (node == null) continue;
            var name = prop.Name.ToCamelCase();
            var stat = prop.IsStatic ? "static " : "";

            // `int X => expr;`
            if (node.ExpressionBody != null)
            {
                _converter.SetCurrentClass(component.Name);
                var expr = _converter.ConvertExpression(node.ExpressionBody.Expression);
                c.Raw($"{stat}get {name}() {{ return {expr}; }}", node);
                continue;
            }

            if (node.AccessorList != null)
            {
                var accessors = node.AccessorList.Accessors;
                var getter = accessors.FirstOrDefault(a => a.Keyword.Text == "get");
                var setter = accessors.FirstOrDefault(a => a.Keyword.Text is "set" or "init");
                var getterHasBody = getter != null && (getter.Body != null || getter.ExpressionBody != null);
                var setterHasBody = setter != null && (setter.Body != null || setter.ExpressionBody != null);

                if (getterHasBody || setterHasBody)
                {
                    _converter.SetCurrentClass(component.Name);
                    if (getterHasBody)
                    {
                        var body = getter!.ExpressionBody != null
                            ? $"return {_converter.ConvertExpression(getter.ExpressionBody.Expression)};"
                            : StripJsBraces(_converter.Convert(getter.Body!));
                        c.Raw($"{stat}get {name}() {{ {body} }}", getter);
                    }
                    if (setterHasBody)
                    {
                        // C# setters use the implicit `value` parameter, which survives conversion as-is.
                        var body = setter!.ExpressionBody != null
                            ? $"{_converter.ConvertExpression(setter.ExpressionBody.Expression)};"
                            : StripJsBraces(_converter.Convert(setter.Body!));
                        c.Raw($"{stat}set {name}(value) {{ {body} }}", setter);
                    }
                    continue;
                }

                // Pure auto-property. A static one carries its initializer as a real field; an INSTANCE one
                // is populated from outside the class body (base Object.assign(props) / the ctor), so it is
                // emitted TYPE-ONLY — the declaration restores type checking on `this.x` without emitting
                // runtime code that would clobber the assigned value under useDefineForClassFields.
                _converter.SetCurrentClass(component.Name);
                if (prop.IsStatic)
                {
                    var def = prop.DefaultValueNode != null
                        ? _converter.ConvertExpression(prop.DefaultValueNode, prop.Type)
                        : (prop.DefaultValue != null ? ConvertToTsValue(prop.DefaultValue, prop.Type) : null);
                    c.Field(name, DeclarationType(component, prop.Type), def, node, isStatic: true);
                }
                else
                {
                    c.Field(name, DeclarationType(component, prop.Type), null, node, isDeclare: true);
                }
            }
        }
    }

    /// <summary>
    /// Emit a C# <c>static class</c> utility as its own TS module: <c>export class X { static foo() {…}
    /// static get bar() {…} static baz = … }</c>, plus imports for any record/component/helper it uses.
    /// </summary>

    /// <summary>The static-class member emission (fields/getters/methods) — shared by top-level
    /// helper MODULES and by NESTED static classes embedded in their owner's module.</summary>
    private void EmitStaticMembers(ClassDeclarationSyntax cls, TypeScriptCodeBuilder.ClassBuilder c)
    {
            foreach (var f in cls.Members.OfType<FieldDeclarationSyntax>())
            {
                foreach (var v in f.Declaration.Variables)
                {
                    var def = v.Initializer != null
                        ? _converter.ConvertExpression(v.Initializer.Value, f.Declaration.Type.ToString())
                        : null;
                    c.Field(v.Identifier.Text.ToCamelCase(), CSharpTypeToTypeScript(f.Declaration.Type.ToString()), def, v, isStatic: true);
                }
            }
            foreach (var p in cls.Members.OfType<PropertyDeclarationSyntax>())
            {
                var pn = p.Identifier.Text.ToCamelCase();
                if (p.ExpressionBody != null)
                {
                    c.Raw($"static get {pn}() {{ return {_converter.ConvertExpression(p.ExpressionBody.Expression)}; }}", p);
                }
                else if (p.AccessorList != null)
                {
                    var g = p.AccessorList.Accessors.FirstOrDefault(a => a.Keyword.Text == "get");
                    if (g?.ExpressionBody != null)
                        c.Raw($"static get {pn}() {{ return {_converter.ConvertExpression(g.ExpressionBody.Expression)}; }}", g);
                    else if (g?.Body != null)
                        c.Raw($"static get {pn}() {{ {StripJsBraces(_converter.Convert(g.Body))} }}", g);
                    else if (p.Initializer != null)
                        c.Field(pn, CSharpTypeToTypeScript(p.Type.ToString()), _converter.ConvertExpression(p.Initializer.Value, p.Type.ToString()), p, isStatic: true);
                }
            }
            foreach (var m in cls.Members.OfType<MethodDeclarationSyntax>())
            {
                var mn = m.Identifier.Text.ToCamelCase();
                var pars = string.Join(", ", m.ParameterList.Parameters.Select(pp => $"{pp.Identifier.Text.ToJsIdentifier()}: {CSharpTypeToTypeScript(pp.Type?.ToString() ?? "object")}"));
                var isAsync = m.ReturnType.ToString().StartsWith("Task")
                    || m.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
                var isGenerator = m.Body?
                    .DescendantNodes(n => n is not AnonymousFunctionExpressionSyntax
                                          && n is not LocalFunctionStatementSyntax)
                    .OfType<YieldStatementSyntax>().Any() == true;
                // Generic helpers keep their type parameters in the TS signature (`also<T>(node: T)`)
                // — constraints drop (TS needs none of them to bind), names pass through.
                var generics = m.TypeParameterList is { Parameters.Count: > 0 }
                    ? $"<{string.Join(", ", m.TypeParameterList.Parameters.Select(tp => tp.Identifier.Text))}>"
                    : "";
                string mbody;
                if (m.Body != null) mbody = StripJsBraces(_converter.Convert(m.Body));
                else if (m.ExpressionBody != null) mbody = $"return {_converter.ConvertExpression(m.ExpressionBody.Expression)};";
                else continue;
                c.Raw($"static {(isAsync ? "async " : "")}{(isGenerator ? "*" : "")}{mn}{generics}({pars}) {{ {mbody} }}", m);
            }
    }

    public string EmitStaticHelperModule(ClassDeclarationSyntax cls, SemanticModel? semanticModel)
    {
        if (semanticModel != null) _converter.SetSemanticModel(semanticModel);
        _converter.SetCurrentClass(cls.Identifier.Text);
        _converter.UsedHelpers.Clear();
        _converter.UsedAppTypes.Clear();
        var name = cls.Identifier.Text;

        var builder = new TypeScriptCodeBuilder();
        builder.Class(name, null, c => EmitStaticMembers(cls, c));

        // Imports: $eq (if used) + runtime-provided references (the same semantic routing components
        // get — a static helper composing the shared vocabulary/library imports it from the runtime)
        // + any record/component/static-helper this class references as per-app modules.
        var ib = new TypeScriptCodeBuilder();
        var core = new HashSet<string>(_converter.UsedHelpers);
        var runtimeProvided = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        if (semanticModel != null)
            Services.RuntimeProvidedTypeScanner.Collect(cls, semanticModel, runtimeProvided, referencedEnums);
        runtimeProvided.Remove(name);
        core.UnionWith(runtimeProvided);
        if (core.Count > 0) ib.Import(core, "@equantic/runtime");
        var knownComp = _dependencyResolver?.GetAllComponents().ToHashSet() ?? new HashSet<string>();
        var knownRec = _dependencyResolver?.GetAllRecords() ?? (IReadOnlySet<string>)new HashSet<string>();
        var knownHelp = _dependencyResolver?.GetAllStaticHelpers() ?? (IReadOnlySet<string>)new HashSet<string>();
        foreach (var t in CollectComponentTypesFromNode(cls, new HashSet<string> { name })
                     .Concat(_converter.UsedAppTypes) // conversion-introduced names (reduced extension calls)
                     .Distinct().OrderBy(x => x))
        {
            var ct = t.Trim().TrimEnd('?');
            if (ct.Contains('<')) ct = ct.Split('<')[0];
            if (ct.Contains('.')) ct = ct.Substring(ct.LastIndexOf('.') + 1);
            if (string.IsNullOrEmpty(ct) || ct == name || ct == "HtmlNode" || NonImportableTypes.Contains(ct)) continue;
            if (runtimeProvided.Contains(ct) || referencedEnums.Contains(ct)) continue;
            if (knownComp.Contains(ct) || knownRec.Contains(ct) || knownHelp.Contains(ct))
                ib.Import(new[] { ct }, $"./{ct}");
        }
        return ib.ToString() + builder.ToString();
    }

    private void EmitMethod(MethodDefinition method, TypeScriptCodeBuilder.ClassBuilder c, ComponentDefinition component, string? className = null)
    {
        // Abstract methods (no body, no expression body) have nothing to emit — the concrete subclass
        // supplies the implementation, and TS needs no abstract stub on the base.
        if (method.SyntaxNode != null && method.SyntaxNode.Body == null && method.SyntaxNode.ExpressionBody == null)
            return;

        // Same resolvability contract as property declarations: a signature must never introduce a
        // name the module cannot import (enums degrade to string — their runtime representation).
        // Params the body never mentions get the TS underscore convention (noUnusedParameters).
        var bodyText = method.SyntaxNode?.Body?.ToString() ?? method.SyntaxNode?.ExpressionBody?.ToString() ?? "";
        var parameters = string.Join(", ", method.Parameters.Select(p =>
            $"{(bodyText.Contains(p.Name) ? p.Name.ToJsIdentifier() : "_" + p.Name)}: {DeclarationType(component, p.Type)}"));
        var methodName = method.Name.ToCamelCase();
        
        // Lifecycle mapping
        if (method.Name == "OnMount") methodName = "onInit";
        
        var returnType = CSharpTypeToTypeScript(method.ReturnType ?? "void");
        
        // async is a MODIFIER, not a return type: `async void` handlers (the C# event-handler
        // idiom — hover intent timers et al.) must emit `async` too, or their awaits are syntax
        // errors in the bundle. Task-returning methods keep emitting async either way.
        var isAsync = (method.ReturnType != null && method.ReturnType.StartsWith("Task"))
            || method.SyntaxNode?.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword) == true;
        // An iterator method (yield in its OWN body — nested lambdas/local functions don't count)
        // is a JS generator; the YieldStatementStrategy already lowers the statements.
        var isGenerator = method.SyntaxNode?.Body?
            .DescendantNodes(n => n is not Microsoft.CodeAnalysis.CSharp.Syntax.AnonymousFunctionExpressionSyntax
                                  && n is not Microsoft.CodeAnalysis.CSharp.Syntax.LocalFunctionStatementSyntax)
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.YieldStatementSyntax>().Any() == true;
        var asyncPrefix = isAsync ? "async " : "";
        var promiseReturnType = isAsync && returnType == "void" ? "Promise<void>" : 
                                 isAsync ? $"Promise<{returnType}>" : returnType;

        if (method.SyntaxNode != null)
        {
            // Use Robust SyntaxNode Conversion (Phase 2+)
            // Handle body (Block or ExpressionBody)
            string jsBody;
            if (method.SyntaxNode.Body != null)
            {
                _converter.SetCurrentClass(className);
                jsBody = _converter.Convert(method.SyntaxNode.Body);
            }
            else if (method.SyntaxNode.ExpressionBody != null)
            {
                _converter.SetCurrentClass(className);
                var expr = _converter.Convert(method.SyntaxNode.ExpressionBody.Expression);
                jsBody = $"{{ return {expr}; }}";
            }
            else
            {
                jsBody = "{}";
            }
            
            c.Method(methodName, parameters, isAsync, () => {
                var body = jsBody.Trim();
                if (body.StartsWith("{") && body.EndsWith("}"))
                {
                    body = body.Substring(1, body.Length - 2).Trim();
                }
                c.Raw(body);
            }, method.TypeParameters, sourceNode: method.SyntaxNode, isStatic: method.IsStatic, isGenerator: isGenerator);
        }
        else
        {
            // Fallback for legacy parsing (should happen rarely now)
            var body = method.Body.Trim().TrimEnd(';');
            _converter.SetCurrentClass(className);
            var convertedExpr = _converter.Convert(body);
            c.Method(methodName, parameters, isAsync, () => {
                c.Raw($"return {convertedExpr};");
            }, method.TypeParameters);
        }
    }
    
    // Helper to access ClassBuilder from TypeScriptEmitter for EmitMethod
    // Actually, I can just pass the ClassBuilder to EmitMethod or store it.
    // Let's refactor EmitMethod to take ClassBuilder.
    
    private void EmitComponentTree(ComponentTree tree)
    {
        Write($"new {tree.ComponentType}({{");
        
        // Extract Key if present. Keys are special and should be top-level in React-like systems,
        // but here we are passing props object to constructor.
        // We need to ensure that if "Key" is in Properties, it is emitted as "key".
        
        var props = tree.Properties.Where(p => p.Key != "Children").ToList();
        
        if (props.Count > 0 || tree.Children.Count > 0)
        {
            WriteLn();
            Indent();
            
            foreach (var (propName, propValue) in props)
            {
                var tsPropName = propName.ToCamelCase();
                if (propName == "Key") tsPropName = "key"; // Special casing for Key

                var tsValue = EmitPropertyValue(propValue);
                WriteLn($"{tsPropName}: {tsValue},");
            }
            
            if (tree.Children.Count > 0)
            {
                Write("children: [");
                WriteLn();
                Indent();
                foreach (var child in tree.Children)
                {
                    EmitComponentTree(child);
                    WriteLn(",");
                }
                Dedent();
                Write("]");
                WriteLn();
            }
            
            Dedent();
            Write("}");
        }
        else
        {
            Write("}");
        }
        
        Write(")");
    }
    
    private string EmitPropertyValue(PropertyValue value)
    {
        return value.Type switch
        {
            PropertyValueType.String => $"'{EscapeString(value.StringValue ?? "")}'",
            PropertyValueType.Number => value.StringValue ?? "0",
            PropertyValueType.Boolean => value.StringValue?.ToLower() ?? "false",
            PropertyValueType.Expression => value.ExpressionNode != null ? _converter.Convert(value.ExpressionNode) : _converter.Convert(value.Expression ?? ""),
            PropertyValueType.EventHandler => ConvertEventHandler(value),
            PropertyValueType.StyleClass => value.Expression ?? "null",
            PropertyValueType.Component when value.ComponentValue != null => EmitComponentToString(value.ComponentValue),
            _ => "null"
        };
    }

    private string ConvertEventHandler(PropertyValue value)
    {
        var expr = value.ExpressionNode != null ? _converter.Convert(value.ExpressionNode) : _converter.Convert(value.Expression ?? "");
        
        // Automatically bind method groups on 'this' (e.g. this.handleClick -> this.handleClick.bind(this))
        if (expr.StartsWith("this.") && !expr.Contains("(") && !expr.Contains("=>") && !expr.Contains(".bind("))
        {
            return $"{expr}.bind(this)";
        }
        return expr;
    }
    
    private string EmitComponentToString(ComponentTree tree)
    {
        var sb = new StringBuilder();
        sb.Append($"new {tree.ComponentType}({{");
        
        var props = tree.Properties.Where(p => p.Key != "Children").ToList();
        foreach (var (propName, propValue) in props)
        {
            var tsPropName = propName.ToCamelCase();
            if (propName == "Key") tsPropName = "key";
            
            sb.Append($" {tsPropName}: {EmitPropertyValue(propValue)},");
        }
        
        if (tree.Children.Count > 0)
        {
            sb.Append(" children: [");
            sb.Append(string.Join(", ", tree.Children.Select(EmitComponentToString)));
            sb.Append("]");
        }
        
        sb.Append(" })");
        return sb.ToString();
    }
    
    private static string CSharpTypeToTypeScript(string? csharpType)
    {
        if (string.IsNullOrEmpty(csharpType)) return "any";

        // Handle Nullable<T> or T?
        var isNullable = csharpType.EndsWith("?");
        var baseType = isNullable ? csharpType.Substring(0, csharpType.Length - 1) : csharpType;

        // C# tuples ARE arrays at runtime (the deconstruction strategies bank on it), so a tuple
        // TYPE — named or not, arrays included — lowers to a TS tuple type with the names erased:
        // `(string Role, string Href)[]` → `[string, string][]`.
        if (baseType.StartsWith("("))
        {
            var arrayDepth = 0;
            var core = baseType.Trim();
            while (core.EndsWith("[]")) { arrayDepth++; core = core[..^2].Trim(); }
            if (core.StartsWith("(") && core.EndsWith(")"))
            {
                var elements = SplitTopLevel(core[1..^1]).Select(element =>
                {
                    var text = element.Trim();
                    // Drop the element NAME (a trailing identifier after the type).
                    var lastSpace = text.LastIndexOf(' ');
                    if (lastSpace > 0 && text[(lastSpace + 1)..].All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
                        text = text[..lastSpace];
                    return CSharpTypeToTypeScript(text.Trim());
                });
                return $"[{string.Join(", ", elements)}]" + string.Concat(Enumerable.Repeat("[]", arrayDepth));
            }
        }
        
        if (baseType.StartsWith("Nullable<") && baseType.EndsWith(">"))
        {
            baseType = baseType.Substring(9, baseType.Length - 10);
        }

        string tsType = baseType switch
        {
            "string" => "string",
            "int" or "long" or "double" or "float" or "decimal" or "number" => "number",
            "bool" or "boolean" => "boolean",
            "void" => "void",
            "object" => "any",
            "DateTime" => "Date",
            "Guid" => "string",
            "Task" => "void",
            _ => baseType
        };

        // Handle Generics (limited support)
        if (tsType.StartsWith("List<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(5, tsType.Length - 6);
            tsType = $"{CSharpTypeToTypeScript(itemType)}[]";
        }
        else if (tsType.StartsWith("IEnumerable<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(12, tsType.Length - 13);
            tsType = $"{CSharpTypeToTypeScript(itemType)}[]";
        }
        else if (tsType.StartsWith("HashSet<") && tsType.EndsWith(">"))
        {
            // The runtime representation IS a JS Set (HashSetStrategy constructs `new Set()`).
            var itemType = tsType.Substring(8, tsType.Length - 9);
            tsType = $"Set<{CSharpTypeToTypeScript(itemType)}>";
        }
        else if (tsType.StartsWith("Task<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(5, tsType.Length - 6);
            tsType = CSharpTypeToTypeScript(itemType);
        }
        else if (tsType.StartsWith("Action<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(7, tsType.Length - 8);
            tsType = $"({itemType.ToCamelCase()}: {CSharpTypeToTypeScript(itemType)}) => void";
        }
        else if (tsType == "Action")
        {
            tsType = "() => void";
        }
        else if (tsType.StartsWith("Dictionary<") && tsType.EndsWith(">"))
        {
            tsType = "Record<string, any>";
        }

        return tsType;
    }
    
    /// <summary>Splits a type argument list on TOP-LEVEL commas (nested <c>&lt;&gt;</c>/<c>()</c> stay whole).</summary>
    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '<' or '(': depth++; break;
                case '>' or ')': depth--; break;
                case ',' when depth == 0:
                    parts.Add(text[start..i]);
                    start = i + 1;
                    break;
            }
        }
        parts.Add(text[start..]);
        return parts;
    }

    private static string ConvertToTsValue(string value, string type)
    {
        if (value.Contains("new()") || value.Contains("new List"))
        {
            var tsType = CSharpTypeToTypeScript(type);
            if (tsType.EndsWith("[]"))
            {
                return "[]";
            }
        }
        
        return type.ToLowerInvariant() switch
        {
            "string" => $"\"{value.Trim('"')}\"",
            "int" or "double" or "float" => value,
            "bool" or "boolean" => value.ToLower(),
             _ => value
        };
    }
    
    private static string GetDefaultForType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "string" => "\"\"",
            "int" or "double" or "float" => "0",
            "bool" or "boolean" => "false",
             _ => "null"
        };
    }
    
    private static string EscapeString(string s)
    {
        // Backslash MUST be escaped first, otherwise it would double-escape the
        // sequences introduced below. Output is wrapped in single quotes by callers.
        return s
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
    
    #region Output Helpers
    
    private void Write(string text)
    {
        _builder.Line(text); // Basic mapping for Write, though Builder prefers structured calls
    }
    
    #endregion
}
