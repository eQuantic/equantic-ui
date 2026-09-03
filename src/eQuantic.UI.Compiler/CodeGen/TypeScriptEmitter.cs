using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Generates TypeScript code from parsed component definitions.
/// Output is designed to be bundled by Bun.
/// </summary>
public class TypeScriptEmitter
{
    /// <summary>Emit TypeScript type annotations (the default). False emits plain JavaScript —
    /// what a browser can run directly off an import map, no bundler in the path (the playground's
    /// mode). The OUTPUT language is the only thing that changes; every strategy stays the same.</summary>
    public bool TypeAnnotations { get; set; } = true;

    /// <summary>See <see cref="ConversionContext.DesignMode"/>. Off, and only a design tool turns
    /// it on — an SDK build must never emit the wrapper into a user's bundle.</summary>
    public bool DesignMode { get; set; }

    private TypeScriptCodeBuilder _builder = new();

    /// <summary>One parameter in a hand-written signature: annotated in TypeScript mode, bare in
    /// plain-JavaScript mode. Every literal signature the emitter writes goes through here.</summary>
    private string Param(string name, string type) => TypeAnnotations ? $"{name}: {type}" : name;

    /// <summary>
    /// An OPTIONAL parameter in a hand-written signature — <c>props?: any</c> in TypeScript, and
    /// plain <c>props</c> otherwise, because <c>?</c> in a JavaScript parameter list is a syntax
    /// error that costs the whole module.
    /// <para>
    /// The doc comment describing this had outlived the method itself, so four signatures went back
    /// to spelling <c>"props?: any"</c> by hand — and a component with a parameterless constructor
    /// emitted a module a browser refuses to parse. The claim on <see cref="Param"/> that every
    /// literal signature goes through it was, for those four, simply false.
    /// </para>
    /// </summary>
    private string OptionalParam(string name, string type) => TypeAnnotations ? $"{name}?: {type}" : name;

    /// <summary>
    /// A concise body converted to <c>return …;</c>, with the <c>let</c> for any pattern variable it
    /// binds already hoisted in front.
    /// <para>
    /// `x is { } y` converts to an assignment of `y` inside the converted condition, and an
    /// expression body reaches none of the statement strategies that hoist it — so the name was
    /// assigned and never declared, which in a module (strict mode) throws the first time that code
    /// runs. It shipped in FOUR emission paths before this existed, one per place someone wrote the
    /// same two lines by hand.
    /// </para>
    /// </summary>
    /// <summary><c>target = value;</c> as IR.</summary>
    private static JsStatement Assign(JsExpr target, JsExpr value) =>
        JsStatement.Expression(JsExpr.Binary(target, "=", value));

    /// <summary><c>if (this.name === undefined) this.name = value;</c> — a default applied only
    /// where props left the slot empty.</summary>
    private static JsStatement DefaultIfUndefined(string name, string value) =>
        JsStatement.If(
            JsExpr.Binary(JsExpr.ThisMember(name), "===", JsExpr.Identifier("undefined")),
            Assign(JsExpr.ThisMember(name), JsExpr.Opaque(value)), null);

    /// <summary>A C# block's statements, to be placed in a member body the emitter is assembling
    /// — no braces of their own.</summary>
    private JsStatement Contents(BlockSyntax block) =>
        JsStatement.Sequence(((JsBlock)_converter.ConvertBlockIr(block)).Statements);

    /// <summary>The model that can answer for THIS node's tree — a component and the types it
    /// references may live in different files of one compilation, and Roslyn throws for a node
    /// from another tree.</summary>
    private SemanticModel? ModelFor(SyntaxNode node)
    {
        if (_converter.Model is not { } model) return null;
        if (ReferenceEquals(node.SyntaxTree, model.SyntaxTree)) return model;
        return model.Compilation.ContainsSyntaxTree(node.SyntaxTree)
            ? model.Compilation.GetSemanticModel(node.SyntaxTree)
            : null;
    }

    /// <summary>The symbol of a declared type — what the hydration spec is computed from — or
    /// null where no model can say (a rewritten node, an isolated snippet).</summary>
    private ITypeSymbol? BindType(TypeSyntax? type) =>
        type is null ? null : ModelFor(type)?.GetTypeInfo(type).Type;

    /// <summary>The VALUE a [ServerAction] resolves to on the client: its return type with the
    /// task unwrapped — <c>Task&lt;List&lt;Todo&gt;&gt;</c> is <c>List&lt;Todo&gt;</c>; void and a
    /// bare Task carry nothing.</summary>
    private ITypeSymbol? ActionValueType(MethodDeclarationSyntax? method)
    {
        if (method is null || ModelFor(method)?.GetDeclaredSymbol(method) is not IMethodSymbol symbol)
            return null;
        return symbol.ReturnType switch
        {
            INamedTypeSymbol { Arity: 1 } task when task.OriginalDefinition.ToDisplayString()
                is "System.Threading.Tasks.Task<TResult>" or "System.Threading.Tasks.ValueTask<TResult>"
                => task.TypeArguments[0],
            { SpecialType: SpecialType.System_Void } => null,
            INamedTypeSymbol bare when bare.ToDisplayString()
                is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" => null,
            var other => other,
        };
    }

    /// <summary>
    /// The class's TYPED BOUNDARY: <c>static $hydration = { total: 'decimal', … }</c>, naming every
    /// field whose wire form differs from its runtime type (HydrationSpec). The runtime hydrates
    /// SSR state and prefetch payloads by this map — coerced once at the boundary, so use sites
    /// need no defensive coercions. Nothing is emitted when every field is identity.
    /// </summary>
    /// <summary>The in-source types this module's hydration specs NAME. They are emitted into the
    /// body (a spec says <c>[Todo]</c>, meaning the class), but they appear in no syntax the type
    /// scan walks — a record reaches a page only as a field's declared type or an action's return
    /// type, neither of which used to produce a runtime reference. Collected here and added to the
    /// import candidates, or the module loads into "Todo is not defined".</summary>
    private readonly HashSet<string> _hydrationReferences = new();

    /// <summary>The runtime class a C# KEYWORD annotates as. <c>decimal</c> is the only one: every
    /// other runtime-backed type is spelled the same in both languages, so the type scan sees the
    /// name in the syntax and routes the import itself. This one the TRANSLATION invents, and a
    /// name no walk can see is a name no import covers.</summary>
    private const string Decimal = "Decimal";

    /// <summary>Runtime names an ANNOTATION introduced — same contract as
    /// <see cref="_hydrationReferences"/>, and merged into the import candidates beside it.</summary>
    private readonly HashSet<string> _annotationReferences = new();

    /// <summary>The TS annotation for a C# type, registering any runtime class the mapping names.
    /// Every emitting call site goes through here rather than the static mapper, so a translated
    /// name cannot reach the output without its import.</summary>
    private string Annotate(string? csharpType)
    {
        var ts = CSharpTypeToTypeScript(csharpType);
        // The name can arrive wrapped — `Decimal[]`, `Decimal | null`, `Map<string, Decimal>` — so
        // the test is on the IDENTIFIERS the annotation is made of, not on the whole string.
        foreach (var name in System.Text.RegularExpressions.Regex.Matches(ts, "[A-Za-z_][A-Za-z0-9_]*"))
        {
            if (name.ToString() == Decimal) _annotationReferences.Add(Decimal);
        }
        return ts;
    }

    private void EmitHydrationMap(TypeScriptCodeBuilder.ClassBuilder c,
        IEnumerable<(string Key, TypeSyntax? Type)> fields)
    {
        var referenced = _hydrationReferences;
        var entries = fields
            .Select(field => (field.Key, Spec: HydrationSpec.Of(BindType(field.Type), referenced)))
            .Where(field => field.Spec is not null)
            .Select(field => $"{field.Key}: {field.Spec}")
            .ToList();
        if (entries.Count == 0) return;
        c.Field("$hydration", null, $"{{ {string.Join(", ", entries)} }}", null, isStatic: true);
    }

    /// <summary>A Build method's body as IR: its block, its expression as a return, or the
    /// fallback — and the node its first line maps to.</summary>
    private (JsStatement Body, SyntaxNode? Source) BuildBody(MethodDeclarationSyntax? build, JsStatement fallback)
    {
        if (build?.Body != null) return (_converter.ConvertBlockIr(build.Body), build.Body);
        if (build?.ExpressionBody != null)
        {
            var expression = build.ExpressionBody.Expression;
            return (JsStatement.Block(new[] { JsStatement.Raw(ExpressionBodyReturn(expression)) }), expression);
        }
        return (JsStatement.Block(new[] { fallback }), null);
    }

    private string ExpressionBodyReturn(ExpressionSyntax expression) =>
        $"{PatternVariableScanner.Declarations(expression, TypeAnnotations)}return {_converter.ConvertExpression(expression)};";

    /// <summary>The same, in STATEMENT position (a setter) — no return to give it.</summary>
    private string ExpressionBodyStatement(ExpressionSyntax expression) =>
        $"{PatternVariableScanner.Declarations(expression, TypeAnnotations)}{_converter.ConvertExpression(expression)};";

    private string ParamWithDefault(string name, string type, string? convertedDefault,
        bool rest = false) =>
        // `params xs` is a REST parameter. Emitted as a plain one it bound only the FIRST argument
        // (`Count("a", "b")` answered 1) and was undefined when none were passed, which threw on
        // the first read of `.length`.
        rest ? $"...{Param(name, type)}"
        : convertedDefault is null ? Param(name, type)
        : convertedDefault == "undefined" && TypeAnnotations ? $"{name}?: {type}"
        : $"{Param(name, type)} = {convertedDefault}";
    public TypeScriptCodeBuilder.ClassBuilder? ClassBuilder { get; set; }

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
    /// The .NET-compat VALUE TYPES the runtime exports. Their values are built through
    /// <c>$eq.time.*</c> / <c>$eq.num.*</c>, so they look like helpers rather than types — but an
    /// annotation can NAME one (<c>selected: DateOnly | null</c>, from a <c>DateOnly?</c>
    /// parameter), and a named type that nothing imports is a module that does not compile. That
    /// reaches a user as a blank page, never as an error, which is why both import paths consult
    /// this and not just the one that happened to hit it first.
    /// </summary>
    private static readonly HashSet<string> RuntimeValueTypes = new(StringComparer.Ordinal)
    {
        "DateTime", "DateOnly", "TimeOnly", "TimeSpan", "DateTimeOffset", "Decimal",
    };

    /// <summary>
    /// Sets the dependency resolver for automatic dependency detection
    /// </summary>
    /// <summary>Forwarded to the converter — see <see cref="CSharpToJsConverter.SymbolsAreAuthoritative"/>.</summary>
    public bool SymbolsAreAuthoritative
    {
        get => _converter.SymbolsAreAuthoritative;
        set => _converter.SymbolsAreAuthoritative = value;
    }

    public void SetDependencyResolver(ComponentDependencyResolver resolver)
    {
        _dependencyResolver = resolver;
    }

    /// <summary>The model backing the CURRENT emission — the import collector asks it for the type
    /// of a target-typed <c>new(...)</c>, whose syntax states no name at all.</summary>
    private SemanticModel? _semanticModel;

    public List<TypeScriptCodeBuilder.SourceMapping> GetLastMappings() => _builder.GetMappings();

    /// <summary>Transpilation diagnostics raised during the most recent <see cref="Emit"/> call.</summary>
    public IReadOnlyList<ConversionDiagnostic> GetLastDiagnostics() => _converter.Diagnostics;

    /// <summary>Track L D3: the resx reads the last emit rewrote — the compiler aggregates them
    /// into the per-culture catalog set.</summary>
    public IReadOnlyList<Services.ResourceUse> GetLastResourceUses() => _converter.ResourceUses;

    /// <summary>
    /// Generate TypeScript code for a component
    /// </summary>
    public string Emit(ComponentDefinition component, SemanticModel? semanticModel = null)
    {
        _converter.EmitTypeAnnotations(TypeAnnotations);
        _converter.EmitDesignOrigins(DesignMode);
        _builder = new TypeScriptCodeBuilder { TypeAnnotations = TypeAnnotations, Layout = _converter.Layout };
        _semanticModel = semanticModel;
        _converter.SetSemanticModel(semanticModel);
        // Everything the PREVIOUS component left behind goes here — the node cache above all, which
        // pins a syntax tree per entry and used to survive this point (see ConversionContext.Reset).
        _converter.Reset();
        _hydrationReferences.Clear();
        _annotationReferences.Clear();
        component.UsedHelpers.Clear();

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
                        tsDefault ??= ValueTypeDefault(field.Type, field.TypeNode);
                        if (tsDefault is not null && tsDefault.Contains("$eq."))
                            component.UsedHelpers.Add(Eq.Import);
                        c.Field(field.Name.ToCamelCase(), tsType, tsDefault, field.DefaultValueNode, field.IsStatic);
                    }


                }

                // The component's typed boundary — what hydration coerces an incoming payload
                // by. Fields AND public auto-properties: a property is a slot the payload
                // fills exactly as a field is, and leaving them out meant a `long` prop
                // arrived as the JSON number it was sent as, into a slot the twin declares
                // `bigint`. The first arithmetic on it threw "Cannot mix BigInt and other
                // types" — in the browser only, after hydration, on a page the server had
                // rendered perfectly.
                if (!component.IsPrimitive)
                {
                    var slots = component.ComponentFields
                        .Where(field => !field.IsStatic)
                        .Select(field => (Key: field.Name.ToCamelCase(), Type: field.TypeNode))
                        .Concat(component.Properties
                            .Where(prop => prop.IsPublic && !prop.IsStatic && IsAutoProperty(prop))
                            .Select(prop => (Key: prop.Name.ToCamelCase(), Type: prop.Node?.Type)))
                        .GroupBy(slot => slot.Key, StringComparer.Ordinal)
                        .Select(group => group.First());
                    EmitHydrationMap(c, slots);
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
                        var paramList = string.Join(", ", ctor!.Parameters.Select(p => Param(p.Name, "any")));
                        jsParams = paramList;
                    }
                    else
                    {
                        // Constructor has no params - accept generic props for Object.assign
                        jsParams = OptionalParam("props", "any");
                    }

                    // Pass props to super
                    var ctorStatements = new List<JsStatement>
                    {
                        JsStatement.Expression(JsExpr.Call(JsExpr.Identifier("super"),
                            hasExplicitParams ? Array.Empty<JsExpr>() : new[] { JsExpr.Identifier("props") })),
                    };

                    // Assign explicit parameters as properties
                    if (hasExplicitParams)
                    {
                        foreach (var param in ctor!.Parameters)
                            ctorStatements.Add(Assign(JsExpr.ThisMember(param.Name.ToCamelCase()), JsExpr.Identifier(param.Name)));
                    }

                    // Apply defaults for properties not provided in props (only if still undefined)
                    foreach (var prop in component.Properties.Where(p => p.IsPublic && p.DefaultValue != null))
                    {
                        var camelName = prop.Name.ToCamelCase();
                        var tsDefault = prop.DefaultValueNode != null 
                            ? _converter.ConvertExpression(prop.DefaultValueNode, prop.Type)
                            : ConvertToTsValue(prop.DefaultValue, prop.Type);
                        // The default rides into the CONSTRUCTOR as text, past the converter's
                        // helper tracking — `$eq.num.long(0)` in a module that never imported $eq
                        // was "ReferenceError: $eq is not defined" at `new`, containing the
                        // component on a page the server had rendered perfectly.
                        if (tsDefault.Contains("$eq.")) component.UsedHelpers.Add(Eq.Import);
                        ctorStatements.Add(DefaultIfUndefined(camelName, tsDefault));
                    }

                    // Execute C# constructor body (e.g., Direction = FlexDirection.Column)
                    var ctorBodyLine = ctorStatements.Count + 1;
                    if (ctor?.SyntaxNode?.Body != null)
                    {
                        _converter.SetCurrentClass(component.Name);
                        ctorStatements.Add(Contents(ctor.SyntaxNode.Body));
                    }
                    c.Member(JsClassMember.Constructor(jsParams, JsStatement.Block(ctorStatements)),
                        bodySource: ctor?.SyntaxNode?.Body, bodyLine: ctorBodyLine);

                    // Emit Render method for primitive - ONLY if defined or it's the base primitive
                    if (component.BuildMethodNode != null && component.BuildMethodNode.Body != null)
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
                            .Distinct()
                            .ToList();
                        var renderStatements = outVars.Select(v => JsStatement.Raw($"let {v};")).ToList();

                        _converter.SetCurrentClass(component.Name);
                        renderStatements.Add(Contents(component.BuildMethodNode.Body));
                        c.Member(JsClassMember.Method("", "render", "", "", "", JsStatement.Block(renderStatements)),
                            bodySource: component.BuildMethodNode.Body, bodyLine: outVars.Count + 1);
                    }
                    else if (component.BaseClassName == "HtmlElement" || component.BaseClassName == null)
                    {
                        // Fallback for base primitives that MUST have a render
                        c.Member(JsClassMember.Method("", "render", "", "", "", JsStatement.Block(new[]
                        {
                            JsStatement.Raw("return { tag: 'div', attributes: {}, events: {}, children: [] };"),
                        })));
                    }

                    // Emit helper methods
                    foreach (var method in component.Methods)
                    {
                        // By IDENTITY, not by name: a private static helper called Render is a
                        // helper, and skipping it by name dropped it from the output entirely.
                        if (method.SyntaxNode is not null
                            && ReferenceEquals(method.SyntaxNode, component.BuildMethodNode)) continue;
                        EmitMethod(method, c, component, component.Name);
                    }
                }
                else if (component.IsStateful)
                {
                    c.Member(JsClassMember.Method("", "createState", "", "", "", JsStatement.Block(new[]
                    {
                        JsStatement.Raw($"return new {component.StateClassName}(this)"),
                    })));
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
                        // A DEPENDENCY is not something the caller passes: it comes from the
                        // container, exactly as ActivatorUtilities gives it natively. So it leaves
                        // the signature entirely and is resolved in the body.
                        var services = ctorParams.Where(p => p.IsService).ToList();
                        var passed = ctorParams.Where(p => !p.IsService).ToList();

                        var paramList = string.Join(", ", passed.Select(p => p.DefaultValueNode != null
                            ? $"{p.Name.ToCamelCase()}: any = {_converter.ConvertExpression(p.DefaultValueNode, p.Type)}"
                            : $"{p.Name.ToCamelCase()}?: any"));
                        var signature = paramList.Length > 0
                            ? $"{paramList}, {OptionalParam("props", "any")}"
                            : OptionalParam("props", "any");
                        var statements = new List<JsStatement> { JsStatement.Expression(JsExpr.Call(JsExpr.Identifier("super"))) };
                        {
                            // The config object carries what a C# OBJECT INITIALIZER assigned, and in C#
                            // that runs AFTER the constructor. Handing it to super() first put it there
                            // first, so every positional parameter's own default overwrote it —
                            // `new Button(label, ...) { OnPressed = f }` emitted `onPressed = null`
                            // over the handler and the button did nothing at all.

                            // FIRST, so the C# constructor body below can use them — which is the
                            // whole point of taking a dependency through a constructor.
                            foreach (var service in services)
                            {
                                // Emitting any `$eq.*` REQUIRES the module to import `$eq` — the
                                // strategies signal that through UsedHelpers, and this one did not:
                                // the resolve line shipped, the import did not, and the page died on
                                // "$eq is not defined" the moment it constructed.
                                component.UsedHelpers.Add(Eq.Import);
                                // WHERE it lands is the constructor's form. An explicit ctor's body
                                // does its own wiring (`_clock = clock`), so a local is what it
                                // reads. A PRIMARY constructor's parameter is an implicit field and
                                // members reference it as `this.clock`, so a local would leave that
                                // field undefined and the dependency unreachable from Build.
                                var target = ctorDef!.IsPrimaryConstructor
                                    ? $"this.{service.Name.ToCamelCase()}"
                                    : $"const {service.Name.ToCamelCase()}";
                                statements.Add(JsStatement.Raw($"{target} = {Eq.ResolveService}('{service.ServiceKey}');"));

                                // The twin of CapabilityScope.Require: a component that declared it
                                // cannot work without this one says so HERE, where the capability is
                                // missing, rather than letting undefined travel into its own code and
                                // fail at a member access that never mentions capabilities. The two
                                // targets have to agree about this, or the browser is the lenient one
                                // and the bug only exists there.
                                if (service.IsRequiredService)
                                {
                                    var name = service.Name.ToCamelCase();
                                    var read = ctorDef.IsPrimaryConstructor ? $"this.{name}" : name;
                                    statements.Add(JsStatement.Raw($"if ({read} === undefined || {read} === null) throw new Error("
                                        + $"'{component.Name} needs {service.ServiceKey}, and this target has none. "
                                        + $"Register it with the host, or declare the parameter as {service.ServiceKey}? "
                                        + "if the component can work without it.');"));
                                }
                            }
                            foreach (var param in passed)
                            {
                                var camelName = param.Name.ToCamelCase();
                                // PRIMARY-constructor params are implicit fields — always assign. With an
                                // EXPLICIT ctor body, only params that map onto a real auto-property assign
                                // here; one that merely feeds a private/state field (`NestedChild(label)` →
                                // `_label`) has no `this.<name>` to write — the C# ctor body does the wiring.
                                if (hasCtorBody && !component.Properties.Any(pr => !pr.IsStatic && pr.Name.ToCamelCase() == camelName))
                                    continue;
                                statements.Add(JsStatement.If(
                                    JsExpr.Binary(JsExpr.Identifier(camelName), "!==", JsExpr.Identifier("undefined")),
                                    Assign(JsExpr.ThisMember(camelName), JsExpr.Identifier(camelName)), null));
                            }
                            _converter.SetCurrentClass(component.Name);
                            foreach (var p in autoDefaults)
                            {
                                var cn = p.Name.ToCamelCase();
                                var def = p.DefaultValueNode != null
                                    ? _converter.ConvertExpression(p.DefaultValueNode, p.Type)
                                    : p.ImplicitDefaultJs!;
                                // Same seam as above: ImplicitDefaultJs is parser-made text the
                                // converter never saw, so the $eq it may carry is marked here.
                                if (def.Contains("$eq.")) component.UsedHelpers.Add(Eq.Import);
                                statements.Add(DefaultIfUndefined(cn, def));
                            }
                            var bodyLine = statements.Count + 1;
                            if (hasCtorBody) statements.Add(Contents(ctorDef!.BodyNode!));
                            // …and the initializer last, which is where C# runs it.
                            statements.Add(JsStatement.Raw("if (props && typeof props === 'object') Object.assign(this, props);"));
                            c.Member(JsClassMember.Constructor(signature, JsStatement.Block(statements)),
                                bodySource: ctorDef?.BodyNode, bodyLine: bodyLine);
                        }
                    }

                    // Build method — underscore the param when the body never uses it
                    // (noUnusedParameters-clean output; the override contract ignores names).
                    // An EXPRESSION-bodied Build has no `Body`, so this read `null?.Contains(...)`,
                    // answered `context`, and emitted a parameter the body never uses — which the
                    // emitted module's own type check rejects. Ask whichever half the method has.
                    var buildBodyText = component.BuildMethodNode?.Body?.ToString()
                        ?? component.BuildMethodNode?.ExpressionBody?.ToString();
                    var buildParamName = buildBodyText?.Contains("context") == false ? "_context" : "context";
                    // The body converts straight to IR: a block as itself, an expression-bodied Build
                    // (`IComponent Build(ctx) => new Box {…};`) as a return, and nothing as the fallback.
                    _converter.SetCurrentClass(component.Name);
                    var (buildBody, buildSource) = BuildBody(component.BuildMethodNode,
                        JsStatement.Raw("throw new Error('Build method not implemented');"));
                    c.Member(JsClassMember.Method("", "build", "", Param(buildParamName, "BuildContext"), "", buildBody),
                        bodySource: buildSource);

                    // Emit helper methods
                    foreach (var method in component.Methods)
                    {
                        // By IDENTITY, not by name: a private static helper called Render is a
                        // helper, and skipping it by name dropped it from the output entirely.
                        if (method.SyntaxNode is not null
                            && ReferenceEquals(method.SyntaxNode, component.BuildMethodNode)) continue;
                        EmitMethod(method, c, component, component.Name);
                    }
                }
                // Abstract classes: no build method emitted
                
                // Server Actions
                foreach (var action in component.ServerActions)
                {
                    ClassBuilder = c;
                    var paramsList = string.Join(", ", action.Parameters.Select(p => Param(p.Name, Annotate(p.Type))));
                    var argsList = string.Join(", ", action.Parameters.Select(p => p.Name));
                    var returnType = Annotate(action.ReturnType);

                    // The action's RESULT crosses the typed boundary too: a Task<decimal> arrives
                    // as a string, a Task<List<Todo>> as plain objects — hydrated ONCE here, by
                    // the spec of the C# return type, so the caller computes with runtime types.
                    var invoke = $"getServerActionsClient().invoke('{action.ActionId}', [{argsList}])";
                    var resultSpec = HydrationSpec.Of(ActionValueType(action.SyntaxNode), _hydrationReferences);
                    if (resultSpec is not null) component.UsedHelpers.Add(Eq.Import);

                    c.Member(JsClassMember.Method("async ", action.MethodName.ToCamelCase(), "", paramsList, "", JsStatement.Block(new[]
                    {
                        JsStatement.Raw(resultSpec is null
                            ? $"return await {invoke}"
                            : $"return {Eq.Hydrate}(await {invoke}, {resultSpec})"),
                    })), action.SyntaxNode);
                }
            }, component.TypeParameters);

        // State class logic hooks into builder via EmitStateClass (already refactored)
        // We just need to ensure EmitStateClass writes to builder, OR we inline it here if we want full builder control in one pass.
        // Given current structure, we rely on EmitStateClass using _builder.
        if (component.IsStateful)
        {
            EmitStatefulComponent(component); // This method needs update to NOT use WriteLn manually if we want full builder purity, but for now we mix.
        }

        // Generate component code without imports
        var componentCode = _builder.ToString();

        // NESTED static classes (each section's private `Copy` et al.) embed in THIS module as
        // plain (non-exported) classes above the component — as their own modules, two same-named
        // nested classes would overwrite each other's file, and the C# scoping is lexical anyway.
        var nestedCode = string.Empty;
        if (component.BuildMethodNode?.Parent is ClassDeclarationSyntax ownerClass)
        {
            var nb = new TypeScriptCodeBuilder { TypeAnnotations = TypeAnnotations, Layout = _converter.Layout };
            foreach (var nested in ownerClass.Members.OfType<ClassDeclarationSyntax>()
                         .Where(n => n.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)))
            {
                nb.Class(nested.Identifier.Text, null, c => EmitStaticMembers(nested, c),
                    sourceNode: nested, export: false);
            }
            nestedCode = nb.ToString();
        }

        // The helpers the converter collected, transferred once ALL code is generated — the nested
        // classes above included. Transferred before them, a `Copy.About` that reads a resx emitted
        // `$eq.str(…)` into a module whose import line had already been decided without it: the
        // browser answered "$eq is not defined" and the whole module failed to load, taking the page
        // with it. The nested body is code like any other and registers what it needs.
        foreach (var helper in _converter.UsedHelpers)
        {
            component.UsedHelpers.Add(helper);
        }

        // Generate imports based on populated UsedHelpers. The emitted body is the authority on what is
        // actually referenced, so it is passed in to drop imports the scan over-collected.
        var imports = Imports(component, nestedCode + componentCode);

        // Return imports + nested scope classes + component code
        return JsModuleWriter.Write(new JsModule(imports, nestedCode + componentCode));
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
    /// <summary>What the module imports, as records — the runtime's names first, then one sibling
    /// module per user type the body references. A decision that comes out as data, not as lines.</summary>
    private IReadOnlyList<JsImport> Imports(ComponentDefinition component, string emittedBody)
    {
        var referenced = ReferencedIdentifiers(emittedBody);
        // A type this module DECLARES is not a type this module imports. The nested `Copy` classes
        // are emitted inline above the component, and the type scan cannot tell that from a type
        // living in its own module, so it asked for `import { Copy } from "./Copy"` — a module
        // nobody writes. The bundler inlines the local class and hides it; anything that does not
        // bundle (tsc, a plain browser module) fails to resolve and takes the page with it.
        var declaredHere = System.Text.RegularExpressions.Regex
            .Matches(emittedBody, @"(?:^|\n)\s*(?:export\s+)?(?:abstract\s+)?class\s+([A-Za-z_$][A-Za-z0-9_$]*)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet();
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

        // Component imports: scan the Build method's syntax for the types it uses
        var componentTypes = new HashSet<string>();

        if (component.BuildMethodNode != null)
        {
             var localNames = new HashSet<string>(component.Properties.Select(p => p.Name));
             foreach (var m in component.Methods) localNames.Add(m.Name);
             localNames.Add(component.Name);

             var proceduralTypes = CollectComponentTypesFromNode(component.BuildMethodNode, localNames);
             foreach (var t in proceduralTypes) componentTypes.Add(t);
        }

        // Scan field initializers too — a type referenced ONLY in a static/instance field
        // initializer (e.g. `static People = new() { new Person(...) }`) still needs its import, or the
        // emitted static initializer throws "Person is not defined" at module load. BOTH field
        // collections: a data class of nested records parses into StateFields, and its initializer
        // is exactly where the nested type is the ONLY mention (the catalogue shape every content
        // file takes).
        foreach (var field in component.ComponentFields.Concat(component.StateFields))
        {
            if (field.DefaultValueNode == null) continue;
            foreach (var t in CollectComponentTypesFromNode(field.DefaultValueNode, new HashSet<string> { component.Name }))
            {
                componentTypes.Add(t);
            }
        }

        // Types a HYDRATION SPEC names — see _hydrationReferences: emitted into the body, present
        // in no syntax the walks above cover.
        foreach (var t in _hydrationReferences) componentTypes.Add(t);
        foreach (var t in _annotationReferences) componentTypes.Add(t);

        // Types the CONVERSION introduced into the output (extension calls reduced to
        // `Class.method(...)`) — invisible to every syntax walk above by construction.
        foreach (var t in _converter.UsedAppTypes) componentTypes.Add(t);
        // Names the conversion introduced that the RUNTIME provides (the factory surface): they
        // join the referenced set AND the runtime-provided classification, so the import router
        // sends them to @equantic/runtime instead of inventing a ./UI module.
        foreach (var t in _converter.UsedRuntimeTypes)
        {
            componentTypes.Add(t);
            component.RuntimeProvidedTypes.Add(t);
        }

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
                    || _dependencyResolver.GetAllPlainClasses().Contains(appType)
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

        // The user universe, discovered by scanning — components, records, helpers, plain classes.
        // Consulted twice: by the standalone fallback below, and by the authoritative filter at the
        // end. No fixed lists on either path.
        var knownComponents = _dependencyResolver?.GetAllComponents().ToHashSet() ?? new HashSet<string>();
        var knownRecords = _dependencyResolver?.GetAllRecords() ?? (IReadOnlySet<string>)new HashSet<string>();
        var knownStaticHelpers = _dependencyResolver?.GetAllStaticHelpers() ?? (IReadOnlySet<string>)new HashSet<string>();
        var knownPlain = _dependencyResolver?.GetAllPlainClasses() ?? (IReadOnlySet<string>)new HashSet<string>();
        bool KnownUserType(string name) => knownComponents.Contains(name) || knownRecords.Contains(name)
                                           || knownStaticHelpers.Contains(name) || knownPlain.Contains(name);

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
            {
                // …unless the RUNTIME provides it. The list exists to stop `import { X } from
                // "./X"` for a type no module declares; a compat value type is a different case —
                // no local module, but a real export from @equantic/runtime, and an annotation
                // that names it (`selected: DateOnly | null`) needs the name to resolve.
                if (IsRuntimeComponent(cleanType)) coreImports.Add(cleanType);
                continue;
            }

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
            // No AUTHORITATIVE semantic model ran (standalone CompileSource without the framework
            // references — the playground's mode): the user universe is what the source declares
            // plus what the scan discovered, so a referenced type outside BOTH can only be the
            // runtime vocabulary. Without this, Button/Text/Column become imports of ./Button —
            // modules that exist nowhere.
            else if (!component.ResolvedSemantically
                     && !component.DeclaredInSource.Contains(cleanType)
                     && !KnownUserType(cleanType))
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
        var imports = new List<JsImport>();
        imports.Add(new JsImport(coreImports.Where(referenced.Contains).ToList(), "@equantic/runtime"));

        // Import user types that we actually emit as their own module: UI components AND data records
        // (each gets a generated .ts file). The set is discovered by scanning the project — no fixed
        // skip-list — so any referenced type that we emit is imported, and anything else is left alone.
        foreach (var userComp in userComponents.OrderBy(x => x))
        {
            if (userComp == component.Name) continue;
            var isEmittedType = KnownUserType(userComp);
            // When a resolver is present it is authoritative: import ONLY types we actually emit
            // (records/components it discovered). This drops references that aren't modules — primitives,
            // static-field names read as ClassName.X, helper-class names, etc. — instead of inventing a
            // bogus `./X`. (Without a resolver we keep the old permissive behavior for isolated snippets.)
            if (_dependencyResolver != null && !isEmittedType)
                continue;
            if (!referenced.Contains(userComp))
                continue;
            // …and never a type this module DECLARES: the nested `Copy` classes are emitted inline
            // above the component, so `from "./Copy"` names a module nobody writes.
            if (declaredHere.Contains(userComp))
                continue;
            imports.Add(new JsImport([userComp], $"./{userComp}"));
        }

        return imports;
    }
    
    /// <summary>The JS literal for C#'s implicit <c>default(T)</c> on a FIELD with no initializer —
    /// numeric and boolean value types only; everything else stays uninitialized (≈ null).</summary>
    /// <summary>
    /// The default of a field declared without an initializer. The spelled name answers the common
    /// primitives; where it cannot — an ENUM (whose default is its zero member, a name string on
    /// this side), a <c>char</c>, or a type reached through an alias (<c>using Amount =
    /// decimal;</c>) — the SYMBOL answers, through the same table the OrDefault family reads. A
    /// field left with no default is <c>undefined</c>, so an enum field rendered as nothing where
    /// .NET renders its zero member.
    /// </summary>
    private string? ValueTypeDefault(string csharpType, TypeSyntax? typeNode)
    {
        if (ImplicitValueTypeDefault(csharpType) is { } byName) return byName;
        if (csharpType.EndsWith('?') || BindType(typeNode) is not { } symbol) return null;
        var bySymbol = Strategies.DefaultValue.Of(symbol);
        return bySymbol == "null" ? null : bySymbol;
    }

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
            // The .NET-compat VALUE TYPES — see RuntimeValueTypes.
            _ when RuntimeValueTypes.Contains(typeName) => true,
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

        // A TARGET-TYPED `new(...)` states NO name — `ObjectCreationStrategy` recovers it from the
        // model and emits `new CatalogueEntry(...)`, so the import must be recovered the same way
        // (a declared type only covers the OUTERMOST creation; nested ones live inside arguments).
        foreach (var implicitCreation in node.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            var created = _semanticModel?.GetTypeInfo(implicitCreation).Type;
            if (created is { Name.Length: > 0 }) types.Add(created.Name);
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
                var tsType = Annotate(field.Type);
                // A compat-typed field must DEFAULT to its runtime type (0n, dec(0)) — the default
                // is also the witness legacy hydration reads a field's type from.
                var tsDefault = field.DefaultValueNode != null
                    ? _converter.ConvertExpression(field.DefaultValueNode, field.Type)
                    : (field.Type.EndsWith('?') ? null : ValueTypeDefault(field.Type, field.TypeNode))
                      ?? ConvertToTsValue(field.DefaultValue ?? GetDefaultForType(field.Type), field.Type);
                if (tsDefault.Contains("$eq.")) component.UsedHelpers.Add(Eq.Import);
                c.Field(field.Name, tsType, tsDefault);
            }

            // The state's typed boundary — what SSR hydration coerces each field by (see
            // component.ts: `static $hydration` wins; fields without a spec keep the witness).
            EmitHydrationMap(c, component.StateFields.Select(field => (field.Name, field.TypeNode)));

            // Constructor
            c.Member(JsClassMember.Constructor($"component: {component.Name}", JsStatement.Block(new[]
            {
                JsStatement.Expression(JsExpr.Call(JsExpr.Identifier("super"))),
                Assign(JsExpr.ThisMember("_component"), JsExpr.Identifier("component")),
            })));
            
            // SetState
            c.Member(JsClassMember.Method("", "setState", "", Param("fn", "() => void"), "", JsStatement.Block(new[]
            {
                JsStatement.Expression(JsExpr.Call(JsExpr.Identifier("fn"))),
                Assign(JsExpr.ThisMember("_needsRender"), JsExpr.Literal("true")),
                JsStatement.Expression(JsExpr.Call(JsExpr.Member(JsExpr.ThisMember("_component"), "_scheduleRender"))),
            })));

            // Custom methods (Phase 2: Semantic Body)
            foreach (var method in component.Methods)
            {
                EmitMethod(method, c, component, component.StateClassName);
            }
            
            // Build method
            // The state's Build: its block, its expression as a return (before that branch existed an
            // expression-bodied Build fell through to the empty Container and lost the page's tree),
            // or the empty Container.
            _converter.SetCurrentClass(component.StateClassName);
            var (stateBody, stateSource) = BuildBody(component.BuildMethodNode, JsStatement.Raw("return new Container({});"));
            c.Member(JsClassMember.Method("", "build", "", Param("context", "BuildContext"), "", stateBody),
                bodySource: stateSource);
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
        var ts = Annotate(csharpType);

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

        // Enum members lower to string literals — and when the enum belongs to the VOCABULARY, the
        // runtime NAMES that set of literals, which is narrower than `string` and therefore usable
        // where a vocabulary slot expects it. See EnumUnion for why the width matters.
        if (component.EnumTypes.Contains(ts)) return VocabularyEnumUnion(ts) + suffix;

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
    /// <summary>
    /// A block's CONTENTS, for a member whose braces the emitter writes itself. The converter lays
    /// the block out with its statements one level in; here that level comes off again (the first
    /// line is trimmed, every later line loses one indentation unit), so the contents start at
    /// column zero and the builder's own indentation puts them where the member is.
    /// </summary>
    private static string StripJsBraces(string js)
    {
        js = js.Trim();
        if (js.StartsWith("{") && js.EndsWith("}")) js = js.Substring(1, js.Length - 2).Trim();
        var lines = js.Split('\n');
        for (var i = 1; i < lines.Length; i++)
            if (lines[i].StartsWith("    ", StringComparison.Ordinal)) lines[i] = lines[i][4..];
        return string.Join("\n", lines);
    }

    private static string Braced(string body) => JsMemberWriter.Braced(body);

    /// <summary>A member body as IR: the block itself when there is one and nothing reshapes it;
    /// otherwise the text the emitter still assembles, as a raw statement the member writer places
    /// one level in.</summary>
    private JsStatement BodyOf(BlockSyntax? block, string? text) =>
        block is not null && text is null ? _converter.ConvertBlockIr(block) : JsStatement.Raw(text ?? "");

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
                c.Member(JsClassMember.Getter(stat, name, "", JsStatement.Raw(ExpressionBodyReturn(node.ExpressionBody.Expression))), node);
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

                    // C# 14 `field`: the property guards its own store, so the twin needs the store
                    // (type-only — the setter is what creates it, and a real field would be defined
                    // as undefined after super() under useDefineForClassFields) and, when the getter
                    // is the compiler's, a getter that reads it. Without that getter the property is
                    // WRITE-ONLY in JavaScript: every read of it answers undefined.
                    if (Strategies.Expressions.FieldExpressionStrategy.UsesBackingField(node))
                    {
                        var slot = Strategies.Expressions.FieldExpressionStrategy.BackingSlot(node);
                        c.Field(slot, DeclarationType(component, prop.Type), null, node, isDeclare: true);
                        if (!getterHasBody && getter != null)
                            c.Member(JsClassMember.Getter(stat, name, "", JsStatement.Return(JsExpr.ThisMember(slot))), getter);
                    }

                    if (getterHasBody)
                    {
                        var body = getter!.ExpressionBody != null
                            ? JsStatement.Raw(ExpressionBodyReturn(getter.ExpressionBody.Expression))
                            : _converter.ConvertBlockIr(getter.Body!);
                        c.Member(JsClassMember.Getter(stat, name, "", body), getter);
                    }
                    if (setterHasBody)
                    {
                        // C# setters use the implicit `value` parameter, which survives conversion as-is.
                        var body = setter!.ExpressionBody != null
                            ? JsStatement.Raw(ExpressionBodyStatement(setter.ExpressionBody.Expression))
                            : _converter.ConvertBlockIr(setter.Body!);
                        c.Member(JsClassMember.Setter(stat, name, "value", body), setter);
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

    /// <summary>The class member emission (fields/getters/methods) — shared by top-level helper
    /// MODULES, by NESTED static classes embedded in their owner's module, and by a PLAIN class the
    /// developer wrote. The only difference is whether the members are static, so that is the
    /// parameter: a plain class is the same shapes without the keyword, plus its constructor.</summary>
    /// <summary>
    /// A type annotation, or nothing at all when the target is plain JavaScript.
    /// <para>
    /// <see cref="TypeScriptCodeBuilder.ClassBuilder.Field"/> asks this question for the members it
    /// writes, but the members written through <c>Raw</c> — getters, setters, abstract and declare
    /// members, lazy statics — each have to ask it themselves, and for a long time none of them did.
    /// A leaked <c>: T</c> is not a cosmetic problem in that mode: the browser rejects the module at
    /// parse time, so nothing in the file runs and the only symptom is an empty frame.
    /// </para>
    /// </summary>
    private string Annotation(string type) => TypeAnnotations ? $": {type}" : "";

    /// <summary>Whether a TYPE-ONLY member (<c>abstract</c>, <c>declare</c>) can be written at all.
    /// Neither keyword exists in JavaScript, and neither carries runtime behaviour to preserve.</summary>
    private bool CanDeclareTypeOnly => TypeAnnotations;

    private void EmitStaticMembers(ClassDeclarationSyntax cls, TypeScriptCodeBuilder.ClassBuilder c,
        bool asStatic = true)
    {
        var qualifier = asStatic ? "static " : "";
        var name = cls.Identifier.Text;
        if (!asStatic) EmitInstanceConstructor(cls, c);

            foreach (var f in cls.Members.OfType<FieldDeclarationSyntax>())
            {
                foreach (var v in f.Declaration.Variables)
                {
                    var def = v.Initializer != null
                        ? _converter.ConvertExpression(v.Initializer.Value, f.Declaration.Type.ToString())
                        : null;
                    // The TYPE is emitted either way. Without it every field of a plain class is
                    // implicitly `any`, and the first thing that goes is the checking the whole
                    // two-layer design exists for.
                    // A member's OWN `static` wins over the class-level default: a plain class
                    // with `public const int StateBlockComment = 1;` needs it on the CLASS, with
                    // its value — dropping either left `CurlyBraceLanguage.stateBlockComment`
                    // undefined and every comparison against it false.
                    var isStaticMember = asStatic
                        || f.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)
                        || f.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstKeyword);
                    var fieldName = v.Identifier.Text.ToCamelCase();
                    // A static field that CONSTRUCTS something runs at module-evaluation time, and
                    // the library's modules import each other through one barrel: whichever loads
                    // first sees the other's class as undefined. C# initialises a type's statics on
                    // FIRST USE, so a lazy getter is both the faithful translation and the only one
                    // that survives the cycle. (`static x = 0` stays a field: nothing to break.)
                    if (isStaticMember && def is not null && NeedsLazyInit(def))
                    {
                        var slot = $"_{fieldName}";
                        // Raw output, so the annotation gate Field() applies has to be applied by
                        // hand. In plain-JavaScript mode this text is run by a browser, where
                        // `static _x: T | undefined;` is a syntax error that takes the WHOLE module
                        // with it — one static collection in a helper class blanked the preview and
                        // reported only "Unexpected strict mode reserved word".
                        c.Member(JsClassMember.Field("static ", slot, Annotation($"{DeclaredType(f.Declaration.Type)} | undefined")), v);
                        c.Member(JsClassMember.Getter("static ", fieldName, Annotation(DeclaredType(f.Declaration.Type)),
                            JsStatement.Raw($"return {name}.{slot} ??= {def};")), v);
                    }
                    else
                    {
                        c.Field(fieldName, DeclaredType(f.Declaration.Type),
                            isStaticMember ? def : null, v, isStatic: isStaticMember);
                    }
                }
            }
            foreach (var p in cls.Members.OfType<PropertyDeclarationSyntax>())
            {
                // An ABSTRACT property is DECLARED, never emitted. The derived class supplies the
                // getter, and a field here would become an OWN property on the instance — which
                // shadows the prototype's getter, so the base would answer for every subclass.
                // `declare` is type-only: it says what the base's own methods may read, and emits
                // nothing to shadow with.
                if (p.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword))
                {
                    if (CanDeclareTypeOnly)
                        c.Member(JsClassMember.Field("abstract ", p.Identifier.Text.ToCamelCase(), $": {DeclaredType(p.Type)}"), p);
                    continue;
                }
                var pn = p.Identifier.Text.ToCamelCase();
                // The RETURN type is emitted: a computed property is where a model's types cross
                // from one member to the next, and an unannotated getter makes every read of it
                // `any` — which then spreads to every lambda over what it returned.
                var propertyType = DeclaredType(p.Type);
                if (p.ExpressionBody != null)
                {
                    c.Member(JsClassMember.Getter(qualifier, pn, Annotation(propertyType),
                        JsStatement.Raw(ExpressionBodyReturn(p.ExpressionBody.Expression))), p);
                }
                else if (p.AccessorList != null)
                {
                    // C# 14 `field`: the property keeps its own store and the accessors guard it.
                    // The slot has to exist before the getter names it — see FieldExpressionStrategy
                    // for why it is called `$name` (a name no C# field can take).
                    if (Strategies.Expressions.FieldExpressionStrategy.UsesBackingField(p))
                    {
                        var slot = Strategies.Expressions.FieldExpressionStrategy.BackingSlot(p);
                        var slotDefault = TypeDeclarationExtensions.DefaultFor(p.Type);
                        if (slotDefault == "null")
                        {
                            if (CanDeclareTypeOnly) c.Member(JsClassMember.Field("declare ", slot, $": {DeclaredType(p.Type)}"), p);
                        }
                        else
                            c.Field(slot, DeclaredType(p.Type), slotDefault, p);
                    }

                    var g = p.AccessorList.Accessors.FirstOrDefault(a => a.Keyword.Text == "get");
                    if (g?.ExpressionBody != null)
                        c.Member(JsClassMember.Getter(qualifier, pn, Annotation(propertyType),
                            JsStatement.Raw(ExpressionBodyReturn(g.ExpressionBody.Expression))), g);
                    else if (g?.Body != null)
                        c.Member(JsClassMember.Getter(qualifier, pn, Annotation(propertyType), _converter.ConvertBlockIr(g.Body)), g);
                    else if (p.Initializer != null)
                        c.Field(pn, DeclaredType(p.Type),
                            _converter.ConvertExpression(p.Initializer.Value, p.Type.ToString()), p,
                            isStatic: asStatic || p.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword));
                    // An AUTO-property — `{ get; set; }`, `{ get; private set; }`, `{ get; }` — is a
                    // field with a name. Emitting nothing for it left the class without the member
                    // its own constructor assigns: `Property 'readOnly' does not exist`.
                    else
                    {
                        // A VALUE type carries its C# default — `public bool ReadOnly { get; set; }`
                        // IS false before anyone assigns it, and leaving it undefined is not false
                        // to `===`. A reference type is DECLARED only: its C# default is null, but
                        // the declared type is non-nullable and the constructor is what assigns.
                        var defaulted = TypeDeclarationExtensions.DefaultFor(p.Type);
                        var isStaticProperty = asStatic
                            || p.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword);
                        if (defaulted == "null" && !isStaticProperty)
                        {
                            if (CanDeclareTypeOnly) c.Member(JsClassMember.Field("declare ", pn, $": {DeclaredType(p.Type)}"), p);
                        }
                        else
                            c.Field(pn, DeclaredType(p.Type), defaulted, p, isStatic: isStaticProperty);
                    }

                    // A property with a SETTER body — the guarded assignment idiom
                    // (`set { if (value == _x) return; _x = value; Raise(); }`) — is where a model
                    // keeps its invariants. Emitting only the getter made every assignment to it a
                    // type error, and would have dropped the invariant if it had compiled.
                    // `init` too, and not only `set`: an init accessor is where a component states
                    // what its configuration may be (`init => field = value.Count > 3 ? throw …`),
                    // and looking only for "set" dropped that guard silently — the invariant simply
                    // did not exist in the twin.
                    var setter = p.AccessorList.Accessors
                        .FirstOrDefault(a => a.Keyword.Text is "set" or "init");
                    if (setter?.ExpressionBody != null)
                        c.Member(JsClassMember.Setter(qualifier, pn, $"value{Annotation(DeclaredType(p.Type))}",
                            JsStatement.Raw(ExpressionBodyStatement(setter.ExpressionBody.Expression))), setter);
                    else if (setter?.Body != null)
                        c.Member(JsClassMember.Setter(qualifier, pn, $"value{Annotation(DeclaredType(p.Type))}",
                            _converter.ConvertBlockIr(setter.Body)), setter);
                }
            }
            // `event Action<T>? Changed;` — a member the model raises and a caller subscribes to.
            // Nothing emitted it, so `this.changed?.(edit)` reached a property that did not exist.
            foreach (var e in cls.Members.OfType<EventFieldDeclarationSyntax>())
            {
                foreach (var v in e.Declaration.Variables)
                {
                    c.Field(v.Identifier.Text.ToCamelCase(), DeclaredType(e.Declaration.Type), "null", v,
                        isStatic: asStatic || e.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword));
                }
            }
            foreach (var m in cls.Members.OfType<MethodDeclarationSyntax>())
            {
                // Same for an abstract METHOD: there is nothing to emit, and TypeScript needs no
                // stub on the base.
                if (m.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword)) continue;
                var mn = m.Identifier.Text.ToCamelCase();
                // Optional parameters keep their default here too — a STATIC helper is exactly what
                // other modules call with the trailing arguments omitted.
                // `out` leaves the signature — it is not passed IN. What it carries comes back in
                // the returned object; see OutParameters.
                var byReference = OutParameters.Of(m.ParameterList);
                var pars = string.Join(", ", m.ParameterList.Parameters
                    .Where(pp => !OutParameters.IsOut(pp))
                    .Select(pp =>
                {
                    // A parameter the body never mentions takes the underscore convention — the
                    // interface a tokenizer implements hands over state that a simple language
                    // never reads, and the runtime's own build rejects an unused name.
                    var body = m.Body?.ToString() ?? m.ExpressionBody?.ToString() ?? "";
                    var parameterName = body.Contains(pp.Identifier.Text)
                        ? pp.Identifier.Text.ToJsIdentifier()
                        : "_" + pp.Identifier.Text.ToJsIdentifier();
                    return ParamWithDefault(parameterName, DeclaredType(pp.Type),
                        pp.Default is null ? null : _converter.ConvertExpression(pp.Default.Value),
                        pp.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ParamsKeyword));
                }));
                var isAsync = m.ReturnType.ToString().StartsWith("Task")
                    || m.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
                var isIterator = m.Body.IsIteratorBody();
                if (isIterator) ReportIfEndless(m.Body);
                // Generic helpers keep their type parameters in the TS signature (`also<T>(node: T)`)
                // — constraints drop (TS needs none of them to bind), names pass through.
                var generics = m.TypeParameterList is { Parameters.Count: > 0 }
                    ? $"<{string.Join(", ", m.TypeParameterList.Parameters.Select(tp => tp.Identifier.Text))}>"
                    : "";
                string mbody;
                if (m.Body != null)
                {
                    _converter.SetIteratorBuffer(isIterator ? IteratorBufferName : null);
                    mbody = StripJsBraces(_converter.Convert(m.Body));
                    _converter.SetIteratorBuffer(null);
                    if (isIterator) mbody = WrapIterator(mbody);
                }
                else if (m.ExpressionBody != null) mbody = ExpressionBodyReturn(m.ExpressionBody.Expression);
                else continue;
                // `out var x` at a CALL SITE inside this body needs `x` to exist before the call.
                mbody = OutParameters.HoistedLocals(m.Body ?? (SyntaxNode?)m.ExpressionBody) + mbody;
                if (byReference.Count > 0) mbody = OutParameters.WrapBody(mbody, byReference, isAsync);
                var modifiers = (m.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword) || asStatic ? "static " : "")
                    + (isAsync ? "async " : "");
                c.Member(JsClassMember.Method(modifiers, mn, generics, pars, "", JsStatement.Raw(mbody)), m);
            }
            // USER-DEFINED OPERATORS — the same family a record's twin already carries, and for the
            // same reason: JavaScript cannot overload an operator, so the call site lowers `a + b`
            // on two in-source objects to `T.opAdd(a, b)` whatever kind of type T is. It did that
            // for a plain class too while nothing here wrote the method, so the page compiled, the
            // server rendered it, and the browser said "T.opAdd is not a function".
            //
            // The NAMES come from RecordTypeEmitter, which is also where the call-site lowering gets
            // them. Three places, one spelling — computing it here instead is how the two sides of a
            // conversion came to disagree once already.
            foreach (var op in cls.Members.OfType<OperatorDeclarationSyntax>())
            {
                var opName = op.ParameterList.Parameters.Count == 1
                    ? RecordTypeEmitter.UnaryOperatorMethodName(op.OperatorToken.Text)
                    : RecordTypeEmitter.OperatorMethodName(op.OperatorToken.Text);
                // No name means the call site cannot lower it either, so writing a method nobody
                // calls would only add a second way to be wrong.
                if (opName is null) continue;
                if (OperatorBody(op) is not { } opBody) continue;
                var opPars = string.Join(", ", op.ParameterList.Parameters
                    .Select(pp => pp.Identifier.Text.ToJsIdentifier()));
                c.Member(JsClassMember.Method("static ", opName, "", opPars, "", JsStatement.Raw(opBody)), op);
            }

            foreach (var conversion in cls.Members.OfType<ConversionOperatorDeclarationSyntax>())
            {
                if (OperatorBody(conversion) is not { } convBody) continue;
                var declared = _semanticModel?.GetDeclaredSymbol(conversion) as IMethodSymbol;
                var convName = declared is not null
                    ? RecordTypeEmitter.ConversionNameFor(declared)
                    : RecordTypeEmitter.ConversionMethodName(
                        conversion.Type.ToString() == cls.Identifier.Text
                            ? conversion.ParameterList.Parameters[0].Type!.ToString()
                            : conversion.Type.ToString(),
                        from: conversion.Type.ToString() == cls.Identifier.Text);
                var convPar = conversion.ParameterList.Parameters[0].Identifier.Text.ToJsIdentifier();
                c.Member(JsClassMember.Method("static ", convName, "", convPar, "", JsStatement.Raw(convBody)), conversion);
            }

            EmitExtensionBlocks(cls, c);
    }

    /// <summary>
    /// An operator's body in either spelling, or null where it has neither — `extern`, or a
    /// declaration in an interface — and there is nothing to write.
    /// <para>
    /// The hoisted locals come first, as they do for an ordinary method: `int.TryParse(s, out var n)`
    /// inside an operator emits `n = …` with nothing declaring `n`, and an ES module is strict, so
    /// the operator threw a ReferenceError the first time it ran instead of returning a value.
    /// </para>
    /// </summary>
    private string? OperatorBody(BaseMethodDeclarationSyntax op)
    {
        var body = op.ExpressionBody is { } expression
            ? ExpressionBodyReturn(expression.Expression)
            : op.Body is { } block ? StripJsBraces(_converter.Convert(block)) : null;
        return body is null
            ? null
            : OutParameters.HoistedLocals(op.Body ?? (SyntaxNode?)op.ExpressionBody) + body;
    }

    /// <summary>
    /// C# 14 extension blocks (<c>extension(T receiver) { … }</c>): every member lowers to a
    /// STATIC on the declaring class with the receiver as the first parameter — the same lowering
    /// classic extensions always had, now covering properties (a static call:
    /// <c>SeqExtensions.isEmpty(sequence)</c>) and C# 15 extension indexers (<c>item(receiver, i)</c>).
    /// A receiver-TYPE-only block declares static extension members; those take no receiver.
    /// The call-site strategies route here by the member symbol's IsExtension containing type.
    /// Before this, an extension block emitted NOTHING — the class shipped empty and every use
    /// site died in the browser.
    /// </summary>
    private void EmitExtensionBlocks(ClassDeclarationSyntax cls, TypeScriptCodeBuilder.ClassBuilder c)
    {
        foreach (var block in cls.Members.OfType<ExtensionBlockDeclarationSyntax>())
        {
            var receiverParameter = block.ParameterList?.Parameters.FirstOrDefault();
            var receiverName = receiverParameter?.Identifier.Text is { Length: > 0 } named
                ? named.ToJsIdentifier()
                : null;
            var receiverType = receiverParameter?.Type is { } rt ? DeclaredType(rt) : "any";
            string WithReceiver(string rest) => receiverName is null
                ? rest
                : rest.Length == 0 ? Param(receiverName, receiverType) : $"{Param(receiverName, receiverType)}, {rest}";

            foreach (var member in block.Members)
            {
                switch (member)
                {
                    case PropertyDeclarationSyntax property:
                    {
                        var body = property.ExpressionBody?.Expression
                            ?? property.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get")?.ExpressionBody?.Expression;
                        var getterBlock = property.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get")?.Body;
                        if (body is null && getterBlock is null)
                        {
                            _converter.Report(property, ConversionSeverity.Error, "EQ2008",
                                $"extension property '{property.Identifier.Text}' has no getter body the compiler can lower — auto-accessors have no store on a receiver.");
                            break;
                        }
                        var text = body is not null ? ExpressionBodyReturn(body) : StripJsBraces(_converter.Convert(getterBlock!));
                        c.Member(JsClassMember.Method("static ", property.Identifier.Text.ToCamelCase(), "", WithReceiver(""),
                            Annotation(DeclaredType(property.Type)), JsStatement.Raw(text)), property);
                        ReportExtensionSetter(property.AccessorList?.Accessors, property.Identifier.Text);
                        break;
                    }

                    case IndexerDeclarationSyntax indexer:
                    {
                        var body = indexer.ExpressionBody?.Expression
                            ?? indexer.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get")?.ExpressionBody?.Expression;
                        var getterBlock = indexer.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get")?.Body;
                        var pars = string.Join(", ", indexer.ParameterList.Parameters
                            .Select(pp => Param(pp.Identifier.Text.ToJsIdentifier(), DeclaredType(pp.Type))));
                        if (body is null && getterBlock is null)
                        {
                            _converter.Report(indexer, ConversionSeverity.Error, "EQ2008",
                                "extension indexer has no getter body the compiler can lower.");
                            break;
                        }
                        var text = body is not null ? ExpressionBodyReturn(body) : StripJsBraces(_converter.Convert(getterBlock!));
                        c.Member(JsClassMember.Method("static ", "item", "", WithReceiver(pars),
                            Annotation(DeclaredType(indexer.Type)), JsStatement.Raw(text)), indexer);
                        ReportExtensionSetter(indexer.AccessorList?.Accessors, "this[]");
                        break;
                    }

                    case MethodDeclarationSyntax method:
                    {
                        if (OutParameters.Of(method.ParameterList).Count > 0)
                        {
                            _converter.Report(method, ConversionSeverity.Error, "EQ2008",
                                $"extension method '{method.Identifier.Text}' with out/ref parameters is not lowered yet.");
                            break;
                        }
                        var pars = string.Join(", ", method.ParameterList.Parameters.Select(pp =>
                            ParamWithDefault(pp.Identifier.Text.ToJsIdentifier(), DeclaredType(pp.Type),
                                pp.Default is null ? null : _converter.ConvertExpression(pp.Default.Value),
                                pp.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ParamsKeyword))));
                        var isAsync = method.ReturnType.ToString().StartsWith("Task")
                            || method.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
                        string body;
                        if (method.Body != null) body = StripJsBraces(_converter.Convert(method.Body));
                        else if (method.ExpressionBody != null) body = ExpressionBodyReturn(method.ExpressionBody.Expression);
                        else break;
                        body = OutParameters.HoistedLocals(method.Body ?? (SyntaxNode?)method.ExpressionBody) + body;
                        c.Member(JsClassMember.Method("static " + (isAsync ? "async " : ""), method.Identifier.Text.ToCamelCase(), "",
                            WithReceiver(pars), "", JsStatement.Raw(body)), method);
                        break;
                    }

                    default:
                        _converter.Report(member, ConversionSeverity.Error, "EQ2008",
                            $"extension member '{member.Kind()}' has no JavaScript lowering yet (operators and events pend).");
                        break;
                }
            }
        }
    }

    private void ReportExtensionSetter(IEnumerable<AccessorDeclarationSyntax>? accessors, string name)
    {
        if (accessors?.Any(a => a.Keyword.Text is "set" or "init") == true)
        {
            _converter.Report(accessors!.First(a => a.Keyword.Text is "set" or "init"),
                ConversionSeverity.Error, "EQ2008",
                $"extension setter on '{name}' is not lowered yet — assignment through an extension member has no call-site translation.");
        }
    }

    /// <summary>
    /// The user-declared constructors of a plain class, plus the field initialisers that C# runs
    /// before them. JS has ONE constructor, so overloads collapse to the widest; a class that
    /// declares none still needs one, or its initialised fields would never be assigned.
    /// </summary>
    private void EmitInstanceConstructor(ClassDeclarationSyntax cls, TypeScriptCodeBuilder.ClassBuilder c)
    {
        var initialisers = new StringBuilder();
        foreach (var field in cls.Members.OfType<FieldDeclarationSyntax>())
        {
            // `const` is static in C#. Assigning one per instance shadowed the class member the
            // subclasses read, so `StateNormal` was undefined on the class and 0 on the instance.
            if (field.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)
                || field.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstKeyword)) continue;
            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer is not { } init) continue;
                var value = _converter.ConvertExpression(init.Value, field.Declaration.Type.ToString());
                // The SAME casing the field declaration uses, or the constructor writes a second,
                // differently-spelled member beside the one every read goes through.
                initialisers.Append($"this.{variable.Identifier.Text.ToCamelCase()} = {value}; ");
            }
        }

        var ctor = cls.Members.OfType<ConstructorDeclarationSyntax>()
            .Where(x => !x.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword))
            .OrderByDescending(x => x.ParameterList.Parameters.Count)
            .FirstOrDefault();

        var parameters = ctor is null
            ? ""
            : string.Join(", ", ctor.ParameterList.Parameters.Select(p =>
                ParamWithDefault(p.Identifier.Text.ToJsIdentifier(), DeclaredType(p.Type),
                    p.Default is null ? null : _converter.ConvertExpression(p.Default.Value),
                    p.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ParamsKeyword))));

        var body = ctor?.Body is { } block ? StripJsBraces(_converter.Convert(block))
            : ctor?.ExpressionBody is { } expression ? $"{_converter.ConvertExpression(expression.Expression)};"
            : "";

        // `new Editor(text) { ReadOnly = true }` — an object initialiser is an ordinary way to
        // construct one of these, and it arrives as a trailing config object exactly as it does for
        // a component. A constructor that did not take one made the emitted call arity-wrong.
        var config = parameters.Length == 0 ? OptionalParam("props", "any") : $", {OptionalParam("props", "any")}";
        var assign = " if (props && typeof props === 'object') Object.assign(this, props);";
        // A derived class must call super() before it touches `this`.
        var superCall = HasEmittedBase(cls) ? "super(); " : "";
        c.Member(JsClassMember.Constructor($"{parameters}{config}", JsStatement.Raw(superCall + initialisers + body + assign)),
            ctor ?? (SyntaxNode)cls);
    }

    /// <summary>
    /// A PLAIN class the developer wrote — not a record, not static, not a component: a bucket, a
    /// builder, a small state machine. Nothing emitted one, so `new Bucket()` named something that
    /// did not exist. Identity, not value: no structural equals and no `with`.
    /// </summary>
    /// <summary>The array an iterator method fills — one name, so every emitter agrees.</summary>
    private const string IteratorBufferName = "_seq";

    /// <summary>
    /// An iterator that never ends is ordinary C# — the caller stops taking. Materialised into an
    /// array it is a hang, and a hung tab says nothing about why. Named here instead.
    /// </summary>
    private void ReportIfEndless(BlockSyntax? body)
    {
        if (body.FindEndlessYieldLoop() is not { } loop) return;
        _converter.Report(loop, ConversionSeverity.Error, "EQ2005",
            "This iterator never finishes, and iterators are MATERIALISED into an array — the loop "
            + "would run forever instead of yielding lazily. Give the loop an end (a bound, a "
            + "`yield break`), or take what you need inside it and return a finished sequence.");
    }

    /// <summary>
    /// Wraps a converted iterator body so it declares the buffer and returns it. The yields inside
    /// were already lowered to <c>_seq.push(…)</c>.
    /// </summary>
    /// <summary>An iterator's body fills a buffer and returns it — contents in, contents out.</summary>
    private static string WrapIterator(string contents) =>
        $"const {IteratorBufferName} = [];\n{contents}\nreturn {IteratorBufferName};";

    /// <summary>
    /// The TS annotation for a declared type, asking the semantic model what KIND of thing it is
    /// before falling back to the name. The name alone is not enough for the two shapes that have
    /// no TypeScript twin to name: an enum (its runtime form is the member string) and an interface
    /// (the compiler emits no module for one, so importing the name asks for a file that does not
    /// exist — `Cannot find name 'ICodeLanguage'`).
    /// </summary>
    private string DeclaredType(TypeSyntax? type)
    {
        if (type is null) return "any";
        // Ask about the type ITSELF. `IThing?` is a NullableTypeSyntax WRAPPER: Roslyn answers
        // nothing about the wrapper, and the mapper answers "IThing | null" for it — which is not
        // the name, so nothing downstream could tell an echoed name from a translated one.
        var nullable = type is NullableTypeSyntax;
        var asked = nullable ? ((NullableTypeSyntax)type).ElementType : type;
        // Compare against the NORMALISED spelling: a generated signature writes
        // `global::Ns.Thing`, and comparing the mapper's output to the raw text would call every
        // qualified name "translated" and skip the enum/interface handling below.
        var askedName = NormalizeQualification(asked.ToString());
        var mapped = Annotate(askedName);
        var echoed = mapped == askedName;

        var resolvedRaw = (_semanticModel?.GetSymbolInfo(asked).Symbol as ITypeSymbol)
            ?? _semanticModel?.GetTypeInfo(asked).Type;
        var resolved = resolvedRaw is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } lifted
            ? lifted.TypeArguments[0]
            : resolvedRaw;

        // A generic's TYPE ARGUMENTS are symbols here even when the string mapper already rewrote
        // the shape around them (`Action<IPainter>` → `(iPainter: IPainter) => void`). An interface
        // among them must answer the same `any` a bare interface parameter does, or the module
        // names something the runtime can export no value for — an interface has none. Asked of
        // the symbol, so there is no state to keep, evict, or share between compilations.
        if (!echoed && resolved is INamedTypeSymbol { TypeArguments.Length: > 0 } generic)
        {
            foreach (var argument in generic.TypeArguments)
            {
                if (argument.TypeKind != TypeKind.Interface) continue;
                mapped = System.Text.RegularExpressions.Regex.Replace(
                    mapped, $@"\b{System.Text.RegularExpressions.Regex.Escape(argument.Name)}\b", "any");
            }
        }

        var core = (echoed ? resolved : null) switch
        {
            // An enum crosses as its member STRING — unless it is [Flags], whose members COMBINE
            // and therefore cross as the number the bitwise operators need.
            { TypeKind: TypeKind.Enum } => IsFlags(resolved!) ? "number" : EnumUnion(resolved!),
            { TypeKind: TypeKind.Interface } => "any",
            { TypeKind: TypeKind.TypeParameter } => resolved!.Name,
            // A name nothing here can VERIFY is a name the module may not resolve. Annotating with
            // it trades a missing type for a broken one, so it stays open.
            null when echoed && !Resolvable(mapped) => "any",
            _ => mapped,
        };
        // A function type has to be PARENTHESISED before a union, or the `| null` binds to its
        // RETURN: `(e: Edit) => void | null` says the handler may return null, not that the
        // handler itself may be absent.
        if (!nullable || core == "any") return core;
        return core.Contains("=>") ? $"({core}) | null" : $"{core} | null";
    }

    /// <summary>
    /// What a non-flags enum is called on the other side. The runtime mirrors every VOCABULARY enum
    /// as a string union named <c>&lt;Enum&gt;Value</c>, and declaring that union instead of a bare
    /// <c>string</c> is what lets a component FORWARD its own enum property into a vocabulary slot:
    /// `string` is wider than the slot, so the twin stopped compiling the first time a component
    /// passed one on (a rail's alignment, straight into a Column's `main`). Comparing against enum
    /// MEMBERS never needed it, which is why it took this long to surface.
    /// <para>
    /// An APP's own enum has no union in the runtime — it still crosses as its member string, which
    /// is exactly what it is.
    /// </para>
    /// </summary>
    private string EnumUnion(ITypeSymbol type) =>
        VocabularyUnionFor(type) is { } union ? Union(type.Name) : "string";

    /// <summary>The union name for a VOCABULARY enum, or null when the enum is an app's own — the
    /// one question three emission paths ask (components, plain classes, records).</summary>
    internal static string? VocabularyUnionFor(ITypeSymbol type) =>
        type.ContainingNamespace?.ToDisplayString().StartsWith(VocabularyNamespace) == true
            ? $"{type.Name}Value"
            : null;

    /// <summary>
    /// The union names the emitted TEXT mentions, verified against the vocabulary. They exist only
    /// in the TypeScript — no scan of the C# syntax could have found them — and an annotation
    /// naming something the module never imported is a broken type, not a missing one.
    /// </summary>
    internal static void SeedEnumUnions(string emitted, Compilation? compilation, HashSet<string> runtimeProvided)
    {
        if (compilation is null) return;
        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(emitted, @"(?<![\w$])([A-Z][A-Za-z0-9]*)Value(?![\w$])"))
        {
            var name = match.Groups[1].Value;
            if (compilation.GetTypeByMetadataName($"{VocabularyNamespace}.{name}") is { TypeKind: TypeKind.Enum })
                runtimeProvided.Add($"{name}Value");
        }
    }

    /// <summary>
    /// The same rule asked by NAME: a component knows its enums as names rather than symbols, so
    /// the question goes to the compilation — does the vocabulary declare an enum called this? A
    /// [Flags] enum answers <c>number</c> here as it does on the symbol path: its runtime value IS
    /// a number, and calling it a string was a latent lie this pass found.
    /// </summary>
    private string VocabularyEnumUnion(string name)
    {
        var symbol = _semanticModel?.Compilation.GetTypeByMetadataName($"{VocabularyNamespace}.{name}");
        if (symbol is not { TypeKind: TypeKind.Enum }) return "string";
        return IsFlags(symbol) ? "number" : Union(name);
    }

    /// <summary>The union's name, registered so it travels with the module — an annotation naming
    /// something the file never imported is a broken type, not a missing one.</summary>
    private string Union(string enumName)
    {
        _converter.UsedRuntimeTypes.Add($"{enumName}Value");
        return $"{enumName}Value";
    }

    /// <summary>The namespace whose enums the runtime mirrors as string unions.</summary>
    private const string VocabularyNamespace = "eQuantic.UI.Primitives";

    /// <summary>A [Flags] enum is a SET of members, and a set of members is a number.</summary>
    private static bool IsFlags(ITypeSymbol type) =>
        type.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute");

    /// <summary>The base CLASS of a declaration, or null. An interface in the base list is not one,
    /// and a generic base loses its arguments — TypeScript needs none of them to extend.</summary>
    private string? BaseClassOf(ClassDeclarationSyntax cls)
    {
        if (cls.BaseList is null) return null;
        foreach (var entry in cls.BaseList.Types)
        {
            var candidate = entry.Type.ToString();
            if (candidate.Contains('<')) candidate = candidate[..candidate.IndexOf('<')];
            var resolved = _semanticModel?.GetSymbolInfo(entry.Type).Symbol as INamedTypeSymbol;
            if (resolved is not null ? resolved.TypeKind == TypeKind.Class : Resolvable(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>Whether the class extends one this compilation EMITS — an interface in the base
    /// list is not a base class, and calling super() for one would call Object's.</summary>
    private bool HasEmittedBase(ClassDeclarationSyntax cls) => BaseClassOf(cls) is not null;

    /// <summary>Whether a bare NAME is something the emitted module can actually name: a type this
    /// compilation emits, or one the runtime provides.</summary>
    /// <summary>
    /// Whether an initialiser has to wait for first USE. Anything that NAMES another module — by
    /// constructing it, calling it, or just reading one of its members — is unsafe at
    /// module-evaluation time, because the library's modules import each other through one barrel
    /// and whichever loads first sees the other as undefined. Only a self-contained literal is
    /// safe where it stands, so that is what the test asks for.
    /// </summary>
    private static bool NeedsLazyInit(string initialiser) =>
        !System.Text.RegularExpressions.Regex.IsMatch(initialiser.Trim(),
            @"^(-?\d+(\.\d+)?|'[^']*'|""[^""]*""|`[^`]*`|true|false|null|undefined|\[\]|\{\})$");

    private bool Resolvable(string name) =>
        _dependencyResolver?.GetAllComponents().Contains(name) == true
        || _dependencyResolver?.GetAllRecords().Contains(name) == true
        || _dependencyResolver?.GetAllStaticHelpers().Contains(name) == true
        || _dependencyResolver?.GetAllPlainClasses().Contains(name) == true;

    public string EmitPlainClassModule(ClassDeclarationSyntax cls, SemanticModel? semanticModel) =>
        EmitClassModule(cls, semanticModel, asStatic: false);

    public string EmitStaticHelperModule(ClassDeclarationSyntax cls, SemanticModel? semanticModel) =>
        EmitClassModule(cls, semanticModel, asStatic: true);

    private string EmitClassModule(ClassDeclarationSyntax cls, SemanticModel? semanticModel, bool asStatic)
    {
        if (semanticModel != null) { _semanticModel = semanticModel; _converter.SetSemanticModel(semanticModel); }
        _converter.EmitTypeAnnotations(TypeAnnotations);
        _converter.EmitDesignOrigins(DesignMode);
        _converter.SetCurrentClass(cls.Identifier.Text);
        _converter.UsedHelpers.Clear();
        _converter.UsedAppTypes.Clear();
        _converter.UsedRuntimeTypes.Clear();
        // This module's diagnostics start at zero — without this, GetLastDiagnostics() after a
        // plain-class/static-helper emit still carried the PREVIOUS component's entries, and now
        // that ComponentCompiler drains every branch, a leak here would fail the wrong file.
        _converter.ClearDiagnostics();
        var name = cls.Identifier.Text;

        // The BASE class travels. Dropping it is how `CSharpLanguage : CurlyBraceLanguage` came out
        // as an empty class that answered "tokenize is not a function" — from very far away from the
        // declaration that lost it.
        var builder = new TypeScriptCodeBuilder { TypeAnnotations = TypeAnnotations, Layout = _converter.Layout };
        builder.Class(name, BaseClassOf(cls), c => EmitStaticMembers(cls, c, asStatic),
            isAbstract: cls.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword));
        var emitted = builder.ToString();
        // This module used a LOCAL builder, so the mappings it recorded never reached
        // GetLastMappings — which is why a static helper (the app's ConsoleShell, say) shipped
        // with no source map while every component beside it had one: the error overlay could
        // walk a page's frame back to C# and had to stop dead on a helper's.
        _builder = builder;

        // Imports: $eq (if used) + runtime-provided references (the same semantic routing components
        // get — a static helper composing the shared vocabulary/library imports it from the runtime)
        // + any record/component/static-helper this class references as per-app modules.
        var imports = new List<JsImport>();
        var core = new HashSet<string>(_converter.UsedHelpers);
        var runtimeProvided = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        if (semanticModel != null)
            Services.RuntimeProvidedTypeScanner.Collect(cls, semanticModel, runtimeProvided, referencedEnums);
        runtimeProvided.Remove(name);
        // The BASE class never comes through the aggregator. `extends` dereferences while the module
        // is EVALUATING, and the library's modules import each other through one barrel — so the
        // aggregator's binding is still undefined and the class fails to define at all. A method
        // body is fine through it (it dereferences when called); a base class is not.
        var baseName = BaseClassOf(cls);
        if (baseName is not null) runtimeProvided.Remove(baseName);
        // Only what the emitted text NAMES. A type the C# mentions and the emission erases (an
        // interface, an enum) would otherwise import a name nothing uses — which the runtime's own
        // build rejects. Lookarounds rather than `\b`: `$eq` starts with a non-word character.
        SeedEnumUnions(emitted, semanticModel?.Compilation, runtimeProvided);
        // A compat value type is not in an eQuantic namespace, so the scanner above never buckets
        // it — but a factory's `DateOnly? selected` annotates with the name all the same.
        foreach (var compat in RuntimeValueTypes) runtimeProvided.Add(compat);
        runtimeProvided.RemoveWhere(referenced => !System.Text.RegularExpressions.Regex.IsMatch(
            emitted, $@"(?<![\w$]){System.Text.RegularExpressions.Regex.Escape(referenced)}(?![\w$])"));
        core.UnionWith(runtimeProvided);
        if (core.Count > 0) imports.Add(new JsImport(core.ToList(), "@equantic/runtime"));
        var knownComp = _dependencyResolver?.GetAllComponents().ToHashSet() ?? new HashSet<string>();
        var knownRec = _dependencyResolver?.GetAllRecords() ?? (IReadOnlySet<string>)new HashSet<string>();
        var knownHelp = _dependencyResolver?.GetAllStaticHelpers() ?? (IReadOnlySet<string>)new HashSet<string>();
        var knownPlainClasses = _dependencyResolver?.GetAllPlainClasses() ?? (IReadOnlySet<string>)new HashSet<string>();
        // The base class is imported whether or not the syntax scanner noticed it: `extends` is the
        // one reference that must resolve before this module's first statement runs.
        if (baseName is not null) imports.Add(new JsImport([baseName], $"./{baseName}"));
        foreach (var t in CollectComponentTypesFromNode(cls, new HashSet<string> { name })
                     .Concat(_converter.UsedAppTypes) // conversion-introduced names (reduced extension calls)
                     .Distinct().OrderBy(x => x))
        {
            var ct = t.Trim().TrimEnd('?');
            if (ct.Contains('<')) ct = ct.Split('<')[0];
            if (ct.Contains('.')) ct = ct.Substring(ct.LastIndexOf('.') + 1);
            if (string.IsNullOrEmpty(ct) || ct == name || ct == baseName
                || ct == "HtmlNode" || NonImportableTypes.Contains(ct)) continue;
            if (runtimeProvided.Contains(ct) || referencedEnums.Contains(ct)) continue;
            if (knownComp.Contains(ct) || knownRec.Contains(ct) || knownHelp.Contains(ct)
                || knownPlainClasses.Contains(ct))
                imports.Add(new JsImport([ct], $"./{ct}"));
        }
        return JsModuleWriter.Write(new JsModule(imports, builder.ToString()));
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
        // OPTIONAL parameters keep their default in the signature — C# lets a caller omit them, and
        // a call site the compiler cannot see (another module, a callback) would otherwise pass
        // `undefined` straight into the body. The syntax carries the default; the converter turns
        // it into the same literal the rest of the emit uses (enums → their member string, consts
        // inlined).
        var syntaxParameters = method.SyntaxNode?.ParameterList.Parameters;
        // `out` parameters leave the signature and come back in the returned object — OutParameters.
        var byReference = OutParameters.Of(method.SyntaxNode?.ParameterList);
        var outNames = byReference.Where(OutParameters.IsOut)
            .Select(p => p.Identifier.Text).ToHashSet(StringComparer.Ordinal);
        var parameters = string.Join(", ", method.Parameters
            .Select((p, index) => (Parameter: p, Index: index))
            .Where(entry => !outNames.Contains(entry.Parameter.Name))
            .Select(entry =>
        {
            var (p, index) = entry;
            var name = bodyText.Contains(p.Name) ? p.Name.ToJsIdentifier() : "_" + p.Name;
            var defaultValue = syntaxParameters is { } list && index < list.Count
                ? list[index].Default?.Value
                : null;
            var isRest = syntaxParameters is { } paramList && index < paramList.Count
                && paramList[index].Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ParamsKeyword);
            return ParamWithDefault(name, DeclarationType(component, p.Type),
                defaultValue is null ? null : _converter.ConvertExpression(defaultValue), isRest);
        }));
        var methodName = method.Name.ToCamelCase();

        // The lifecycle keeps its own name across the crossing. It used to arrive as `onInit`, from
        // the days when the only base was the legacy page state, and that name is the reason
        // OnMount had never once run on the web: `SharedStatefulComponent` — what every write-once
        // component extends — declares `onMount`, calls it from `notifyMounted`, and has no
        // `onInit` at all, so the override landed on nothing. The legacy `onInit` is also skipped
        // for any page that HYDRATED, which is every server-rendered page there is.
        
        var returnType = Annotate(method.ReturnType ?? "void");
        
        // async is a MODIFIER, not a return type: `async void` handlers (the C# event-handler
        // idiom — hover intent timers et al.) must emit `async` too, or their awaits are syntax
        // errors in the bundle. Task-returning methods keep emitting async either way.
        var isAsync = (method.ReturnType != null && method.ReturnType.StartsWith("Task"))
            || method.SyntaxNode?.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword) == true;
        // An iterator method (yield in its OWN body — nested lambdas/local functions don't count)
        // MATERIALISES: it fills an array and returns it. A JS generator would look right and then
        // read as undefined the moment a LINQ operator touched the result, because every sequence
        // in the emitted world is an array. See ConversionContext.IteratorBuffer.
        var isIterator = method.SyntaxNode?.Body.IsIteratorBody() == true;
        if (isIterator) ReportIfEndless(method.SyntaxNode!.Body);
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
                _converter.SetIteratorBuffer(isIterator ? IteratorBufferName : null);
                jsBody = _converter.Convert(method.SyntaxNode.Body);
                _converter.SetIteratorBuffer(null);
                if (isIterator) jsBody = Braced(WrapIterator(StripJsBraces(jsBody)));
            }
            else if (method.SyntaxNode.ExpressionBody != null)
            {
                _converter.SetCurrentClass(className);
                // An expression body never reaches ReturnStatementStrategy, so nothing hoisted the
                // `let` for a pattern variable bound in it — the converted condition assigned an
                // undeclared name, which in a module (strict mode) throws ReferenceError.
                jsBody = $"{Braced(ExpressionBodyReturn(method.SyntaxNode.ExpressionBody.Expression))}";
            }
            else
            {
                jsBody = "{}";
            }
            
            var body = StripJsBraces(jsBody);
            body = OutParameters.HoistedLocals(method.SyntaxNode.Body
                ?? (SyntaxNode?)method.SyntaxNode.ExpressionBody) + body;
            if (byReference.Count > 0) body = OutParameters.WrapBody(body, byReference, isAsync);
            var generics = method.TypeParameters is { } typeParameters && typeParameters.Any()
                ? $"<{string.Join(", ", typeParameters)}>" : "";
            c.Member(JsClassMember.Method((method.IsStatic ? "static " : "") + asyncPrefix, methodName, generics, parameters, "",
                JsStatement.Raw(body)), method.SyntaxNode);
        }
        else
        {
            // Fallback for legacy parsing (should happen rarely now)
            var body = method.Body.Trim().TrimEnd(';');
            _converter.SetCurrentClass(className);
            var convertedExpr = _converter.Convert(body);
            var generics = method.TypeParameters is { } typeParameters && typeParameters.Any()
                ? $"<{string.Join(", ", typeParameters)}>" : "";
            c.Member(JsClassMember.Method(asyncPrefix, methodName, generics, parameters, "",
                JsStatement.Raw($"return {convertedExpr};")));
        }
    }
    
    /// <summary>
    /// Drops NAMESPACE qualification from a type name, keeping generics and arrays intact:
    /// <c>global::eQuantic.UI.Primitives.VisualNode</c> → <c>VisualNode</c>,
    /// <c>System.Collections.Generic.List&lt;A.B.Foo&gt;</c> → <c>List&lt;Foo&gt;</c>.
    /// <para>
    /// A TypeScript module binds SIMPLE names — the import is `import { VisualNode }`, and there is
    /// no namespace object to reach through — so a qualified name echoed into an annotation is not
    /// merely ugly, it does not parse: `global::` is a syntax error the bundler dies on, and it is
    /// exactly how a SOURCE GENERATOR writes types, since qualifying is how generated code avoids
    /// ambiguity. The name-based special cases below (List, Dictionary, Action…) also only ever
    /// matched unqualified spellings, so this is what makes `System.Action&lt;T&gt;` map at all.
    /// </para>
    /// </summary>
    internal static string NormalizeQualification(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName ?? "";
        if (!typeName!.Contains('.') && !typeName.Contains("::")) return typeName;

        // Each run of `Namespace.` (optionally behind `global::`) that PRECEDES an identifier is
        // qualification; the identifier that survives is the type's own name. Applied everywhere in
        // the string, so generic arguments normalise with the same pass.
        return System.Text.RegularExpressions.Regex.Replace(
            typeName, @"(?:global::)?(?:[A-Za-z_][A-Za-z0-9_]*\.)+", "");
    }

    internal static string CSharpTypeToTypeScript(string? csharpType)
    {
        if (string.IsNullOrEmpty(csharpType)) return "any";
        csharpType = NormalizeQualification(csharpType);

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
        
        // A plain ARRAY maps by its element at any depth — `int[]` is `number[]`, `int[][]` is
        // `number[][]`. Without this the scalar switch below saw `int[]` whole, matched nothing,
        // and the C# spelling reached TypeScript verbatim (a parameter typed `int[]` broke the
        // runtime's own build the first time a shared method took one).
        if (baseType.EndsWith("[]"))
        {
            var arrayDepth = 0;
            var element = baseType;
            while (element.EndsWith("[]"))
            {
                arrayDepth++;
                element = element[..^2].Trim();
            }
            return CSharpTypeToTypeScript(element) + string.Concat(Enumerable.Repeat("[]", arrayDepth));
        }

        if (baseType.StartsWith("Nullable<") && baseType.EndsWith(">"))
        {
            baseType = baseType.Substring(9, baseType.Length - 10);
        }

        string tsType = baseType switch
        {
            "string" or "char" => "string",
            "int" or "double" or "float" or "number" => "number",
            // NOT `number`, either of them. A long is a JS bigint on this side and a decimal is the
            // runtime's Decimal class — `$eq.num.long(0)` and `$eq.num.dec(0)` are what the emitter
            // writes for their literals, and a `number` annotation over either is a lie the rest of
            // the file then typechecks against: `unit.mul(...)` on a "number" is the shape it takes.
            "long" => "bigint",
            "decimal" => Decimal,
            "bool" or "boolean" => "boolean",
            "void" => "void",
            "object" => "any",
            // NOT the JS `Date`. The runtime carries its own DateTime — ticks, .NET's epoch, and
            // the kind — exported beside DateOnly and TimeOnly, which reach here correctly only
            // because nothing rewrote them. Emitting `Date` typed the generated factory surface
            // for a class the framework never passes, and told any consumer reading it to hand
            // over the wrong one.
            "DateTime" => "DateTime",
            "Guid" => "string",
            "Task" => "void",
            // C# names the build argument `ComponentContext`; the runtime declares one interface
            // for it, under the name the DOM side has always used. Emitting the C# name asked for
            // a second, incompatible type with the same meaning.
            "ComponentContext" or "BuildContext" => "RenderContext",
            _ => baseType
        };

        // Handle Generics (limited support)
        // EVERY sequence is a JS array at runtime, so every name for one annotates as `T[]`. The
        // read-only interfaces were the hole: `IReadOnlyList<int>` reached TypeScript verbatim, and
        // a `foreach` over it typed its variable `unknown` — a type error in the runtime's own build
        // and, worse, a lie in an app's editor.
        if (SequenceOf(tsType) is { } sequenceItem)
        {
            tsType = $"{CSharpTypeToTypeScript(sequenceItem)}[]";
        }
        else if (SetOf(tsType) is { } setItem)
        {
            // The runtime representation IS a JS Set (HashSetStrategy constructs `new Set()`).
            tsType = $"Set<{CSharpTypeToTypeScript(setItem)}>";
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
        // `Func<…>` is the same shape with an answer: the LAST type argument is the return, the
        // rest are parameters. Missing here, the C# spelling reached TypeScript verbatim — a
        // shared method taking a `Func<string, bool>` emitted `holds: Func<string, bool>`, which
        // names nothing in TS and fails the emitted module's own type check. (`Action<T>` was
        // taught this the same way, by the first shared method that took one.)
        else if (tsType.StartsWith("Func<") && tsType.EndsWith(">"))
        {
            var arguments = SplitTopLevel(tsType[5..^1]).Select(argument => argument.Trim()).ToList();
            var result = CSharpTypeToTypeScript(arguments[^1]);
            var parameters = arguments[..^1].Select((argument, index) =>
                $"{(arguments.Count == 2 ? "value" : "arg" + (index + 1))}: {CSharpTypeToTypeScript(argument)}");
            tsType = $"({string.Join(", ", parameters)}) => {result}";
        }
        else if (tsType.StartsWith("Dictionary<") && tsType.EndsWith(">"))
        {
            tsType = "Record<string, any>";
        }

        // A NULLABLE C# type is nullable in TypeScript too. The flag was computed and then dropped,
        // so `Action?` annotated as `() => void` and passing the null its own signature invites was
        // a type error. A function type needs the parentheses: `() => void | null` parses as a
        // function RETURNING `void | null`.
        if (isNullable && tsType is not ("any" or "void"))
            tsType = tsType.Contains("=>") ? $"({tsType}) | null" : $"{tsType} | null";

        return tsType;
    }

    /// <summary>Every C# name for an ordered sequence — all of them are a JS array.</summary>
    private static string? SequenceOf(string tsType)
    {
        string[] names = ["List<", "IList<", "ICollection<", "IEnumerable<", "IReadOnlyList<",
            "IReadOnlyCollection<"];
        return Unwrap(tsType, names);
    }

    /// <summary>The set family — a JS Set.</summary>
    private static string? SetOf(string tsType) => Unwrap(tsType, ["HashSet<", "ISet<", "IReadOnlySet<"]);

    private static string? Unwrap(string tsType, string[] prefixes)
    {
        if (!tsType.EndsWith(">")) return null;
        foreach (var prefix in prefixes)
        {
            if (tsType.StartsWith(prefix))
                return tsType.Substring(prefix.Length, tsType.Length - prefix.Length - 1);
        }

        return null;
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
    
}
