using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies;

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

    private MethodLowering? _lowering;

    /// <summary>How a method and a member's body are lowered, and every literal signature's
    /// parameter written: one path shared with the record and struct emitter (#432).</summary>
    private MethodLowering Lowering => _lowering ??= new MethodLowering(_converter, () => TypeAnnotations, ModelFor);

    /// <summary>
    /// An OPTIONAL parameter in a hand-written signature — <c>props?: any</c> in TypeScript, and
    /// plain <c>props</c> otherwise, because <c>?</c> in a JavaScript parameter list is a syntax
    /// error that costs the whole module.
    /// <para>
    /// The doc comment describing this had outlived the method itself, so four signatures went back
    /// to spelling <c>"props?: any"</c> by hand — and a component with a parameterless constructor
    /// emitted a module a browser refuses to parse. The claim on <see cref="MethodLowering.Param"/> that every
    /// literal signature goes through it was, for those four, simply false.
    /// </para>
    /// </summary>
    private string OptionalParam(string name, string type) => TypeAnnotations ? $"{name}?: {type}" : name;

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

    /// <summary>
    /// The default of a declared type, asked of the SYMBOL where a model can say, as the record
    /// emitter asks it: the syntax alone cannot see through a name, and answered null for an enum, a
    /// char and every struct, so a property of an enum type read undefined where C# reads its zero
    /// member (#483).
    /// </summary>
    private string DefaultOf(TypeSyntax type) =>
        BindType(type) is { } symbol ? _converter.DefaultOf(symbol) : TypeDeclarationExtensions.DefaultFor(type);

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

    /// <summary>The in-source types this module's hydration specs NAME. They are emitted into the
    /// body (a spec says <c>[Todo]</c>, meaning the class), but they appear in no syntax the type
    /// scan walks — a record reaches a page only as a field's declared type or an action's return
    /// type, neither of which used to produce a runtime reference. Collected here and added to the
    /// import candidates, or the module loads into "Todo is not defined".</summary>
    private readonly HashSet<string> _hydrationReferences = new();
    /// <summary>The vocabulary twins a hydration spec names (<c>of: Rect</c>): imported from the
    /// runtime, never from a sibling module.</summary>
    private readonly HashSet<string> _hydrationRuntimeReferences = new();

    /// <summary>The runtime class a C# KEYWORD annotates as. <c>decimal</c> is the only one: every
    /// other runtime-backed type is spelled the same in both languages, so the type scan sees the
    /// name in the syntax and routes the import itself. This one the TRANSLATION invents, and a
    /// name no walk can see is a name no import covers.</summary>
    internal const string Decimal = "Decimal";

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

    /// <summary>
    /// The class's TYPED BOUNDARY: <c>static get $hydration() { return { total: 'decimal', … }; }</c>,
    /// naming every value the server carries to this component (the hydration manifest) and how it is
    /// coerced: by its wire spec where its JSON form differs from its runtime type (HydrationSpec), and
    /// <c>'declared'</c> otherwise. The runtime adopts exactly the keys this map lists, so a value the
    /// server sends lands even in a member the instance has not assigned yet (a captured
    /// primary-constructor parameter the router did not pass). Nothing is emitted for a component
    /// the manifest does not describe: it never receives state.
    /// </summary>
    private void EmitHydrationMap(TypeScriptCodeBuilder.ClassBuilder c,
        IEnumerable<(string Key, ITypeSymbol? Type, string? Projection)> carried)
    {
        var referenced = _hydrationReferences;
        var entries = carried
            .Select(value => $"{value.Key}: {(value.Projection is { } projection
                ? ProjectionSpec(value.Type, projection)
                : HydrationSpec.Of(value.Type, referenced, _hydrationRuntimeReferences) ?? "'declared'")}")
            .ToList();
        if (entries.Count == 0) return;
        // A GETTER, never a field: the map can name a class (`_geometry: BarChartGeometry`), and a
        // static field initializer runs when THIS class is defined — which, inside the runtime
        // bundle's import cycles, came before the named class was, and the whole bundle failed to
        // load on a TDZ ReferenceError. The runtime reads the map when it hydrates, after every
        // module has loaded.
        c.Member(JsClassMember.Getter("static ", "$hydration", "",
            JsStatement.Raw($"return {{ {string.Join(", ", entries)} }};")));
    }

    /// <summary>
    /// A server value's spec: the projection it crosses as, which is plain data and never the twin of
    /// its class. A twin's getters compute from members, and a projection holds only the members the
    /// browser reads, so rebuilt on the twin it would answer from members that never crossed. Each leaf
    /// the projection reads is coerced by its C# type, the way a field of that type would be.
    /// </summary>
    private string ProjectionSpec(ITypeSymbol? type, string projection)
    {
        var reads = new ProjectionReads();
        foreach (var read in projection.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var presence = read.EndsWith('?');
            var node = reads;
            var current = type;
            foreach (var segment in (presence ? read[..^1] : read).Split('.'))
            {
                current = MemberType(current, segment);
                node = node.Child(segment);
            }
            if (!presence) node.Leaf = current;
        }
        return Members(reads) ?? "{ members: {} }";
    }

    /// <summary>The members of one level that need coercing, or null when every one crosses as it is.</summary>
    private string? Members(ProjectionReads node)
    {
        var entries = new List<string>();
        foreach (var (segment, child) in node.Children)
        {
            var spec = child.Children.Count > 0
                ? Members(child)
                : HydrationSpec.Of(child.Leaf, _hydrationReferences, _hydrationRuntimeReferences);
            if (spec is not null) entries.Add($"{segment.ToCamelCase()}: {spec}");
        }
        return entries.Count == 0 ? null : $"{{ members: {{ {string.Join(", ", entries)} }} }}";
    }

    /// <summary>The C# type of the member a projection reads, as the value's type or one it derives from declares it.</summary>
    private static ITypeSymbol? MemberType(ITypeSymbol? type, string name)
    {
        // A nullable struct's members are the struct's: its `.Value` never reaches a projection's path.
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IPropertySymbol { IsIndexer: false } property) return property.Type;
                if (member is IFieldSymbol field) return field.Type;
            }
        }
        return type?.AllInterfaces
            .SelectMany(face => face.GetMembers(name))
            .OfType<IPropertySymbol>()
            .Select(property => property.Type)
            .FirstOrDefault();
    }

    /// <summary>A Build method's body as IR: its block, its expression as a return, or the
    /// fallback — and the node its first line maps to.</summary>
    private (JsStatement Body, SyntaxNode? Source) BuildBody(MethodDeclarationSyntax? build, JsStatement fallback)
    {
        if (build?.Body != null) return (_converter.ConvertBlockIr(build.Body), build.Body);
        if (build?.ExpressionBody != null)
        {
            var expression = build.ExpressionBody.Expression;
            return (Lowering.ExpressionBody(expression, returns: true), expression);
        }
        return (JsStatement.Block(new[] { fallback }), null);
    }

    /// <summary>An initializer's converted text, in an arrow that declares what it declares — see
    /// <see cref="ExpressionVariableScanner.Scoped"/>. Every initializer the emitter writes goes
    /// through here: a field's, a property's and a prop's default, in a constructor or on the class.</summary>
    private string Initializer(ExpressionSyntax expression, string converted) =>
        ExpressionVariableScanner.Scoped(expression, converted, TypeAnnotations);

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
        // The cancellation trio (utils/cancellation.ts): built through `$eq.cancellation`, named by
        // the parameter a provider takes its token in.
        "CancellationToken", "CancellationTokenSource", "CancellationTokenRegistration",
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
        _converter.SetFallbackTypeReceivers(resolver.GetAllStaticHelpers(), resolver.GetRuntimeProvidedTypes());
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
        _hydrationRuntimeReferences.Clear();
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
                // WHO THIS IS at the seam with the server: the CLR full name, as a string the compiler
                // wrote. The runtime keys the component's hydration state by it, and the server keys by
                // the same name (ComponentIdentity.Of). It was `constructor.name` — the simple name,
                // shared by every `Row` in every namespace, and alive only while no bundler minified
                // identifiers (#278).
                if (component.TypeIdentity.Length > 0)
                    c.Field("$typeId", null, JsStringLiteral.Quote(component.TypeIdentity), null, isStatic: true);

                // Component-level fields (static data / consts / instance fields), emitted at the top of
                // the class. Skipped for primitives' INSTANCE fields, whose base ctor sets every prop via
                // Object.assign — an uninitialised instance field would clobber that after super(); a static
                // field is class-level and carries its own initializer, so it is always safe.
                // A component's statics initialize as any type's do (TypeInitializer, #417): when one
                // of them can observe another, each starts at its zero and they run in declaration
                // order on first use. A primitive keeps its fields, as its statics are its tag's.
                // The statics are collected in ONE pass, in declaration order (TypeInitializer.Stores):
                // every field and then every property ran a property's initializer after a field's
                // written below it.
                var orderedStatics = !component.IsPrimitive && component.ClassSyntax is { } staticsOf
                    && TypeInitializer.Orders(staticsOf, ModelFor);
                _converter.SetCurrentClass(component.Name);
                var initializedStatics = orderedStatics
                    ? TypeInitializer.Collect(component.ClassSyntax!, type => DeclarationType(component, type.ToString()),
                        type => ValueTypeDefault(type.ToString(), type) ?? "null", StaticValue)
                    : [];
                var slots = initializedStatics.Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
                if (component.ComponentFields.Count > 0)
                {
                    _converter.SetCurrentClass(component.Name);
                    foreach (var field in component.ComponentFields)
                    {
                        if (component.IsPrimitive && !field.IsStatic) continue;
                        // A static that initializes in order is its slot's, and holds no field.
                        if (field.IsStatic && slots.Contains(field.Name.ToCamelCase())) continue;
                        var tsType = DeclarationType(component, field.Type);
                        var tsDefault = field.DefaultValueNode is not { Parent: EqualsValueClauseSyntax given } ? null
                            : field.IsStatic && field.TypeNode is { } declared ? StaticValue(given, declared)
                            : Initializer(given.Value, _converter.ConvertExpression(given.Value, field.Type));
                        // C# value types default without an initializer (`private int _count;` is 0);
                        // an uninitialized TS field is `undefined` and would poison arithmetic (NaN).
                        tsDefault ??= ValueTypeDefault(field.Type, field.TypeNode);
                        if (tsDefault is not null && tsDefault.Contains("$eq."))
                            component.UsedHelpers.Add(Eq.Import);
                        c.Field(field.Name.ToCamelCase(), tsType, tsDefault, field.DefaultValueNode, field.IsStatic);
                    }


                }

                // The component's typed boundary: what the server carries to it, read from the
                // hydration manifest the server writes its payload from, so the two halves cannot
                // disagree. Each value is coerced by its C# type: a `long` arriving as the string
                // the wire carries it as becomes the `bigint` the twin declares, where it used to
                // throw "Cannot mix BigInt and other types" on its first arithmetic, in the browser
                // only, after hydration.
                if (!component.IsPrimitive
                    && component.ClassSyntax is { } declaration
                    && ModelFor(declaration) is { } classModel
                    && classModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol componentSymbol)
                {
                    EmitHydrationMap(c, HydrationManifest.Of(componentSymbol, classModel.Compilation));
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
                        var paramList = string.Join(", ", ctor!.Parameters.Select(p => Lowering.Param(p.Name.ToJsIdentifier(), "any")));
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
                            ctorStatements.Add(Assign(JsExpr.ThisMember(param.Name.ToCamelCase()), JsExpr.Identifier(param.Name.ToJsIdentifier())));
                    }

                    // Apply defaults for properties not provided in props (only if still undefined)
                    // A STATIC property's is on the class (StaticInitial): written here, it became
                    // an own property of each instance that nothing reads.
                    foreach (var prop in component.Properties.Where(p => p.IsPublic && !p.IsStatic))
                    {
                        // Read ONCE into a local: it is a mutable property, so nothing says it is
                        // still non-null at a second read a line later.
                        if (prop.DefaultValueNode is not { } declaredDefault) continue;
                        var camelName = prop.Name.ToCamelCase();
                        var tsDefault = Initializer(declaredDefault, _converter.ConvertExpression(declaredDefault, prop.Type));
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
                        // Each statement declares what its expressions declare, with C#'s scope
                        // (ExpressionVariableScanner). This path once kept a copy of its own that
                        // declared every `out var` by its source text (`let @class;`, a SyntaxError)
                        // and no deconstruction element at all.
                        _converter.SetCurrentClass(component.Name);
                        c.Member(JsClassMember.Method("", "render", "", "", "", JsStatement.Block([Contents(component.BuildMethodNode.Body)])),
                            bodySource: component.BuildMethodNode.Body);
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
                // A concrete component, OR an abstract base that still defines a concrete Build or
                // MEMBERS for its subclasses to inherit. Only a pure-abstract class with neither
                // emits nothing here.
                //
                // The member counts are the half the condition was missing while the comment above
                // it already claimed them. An app-owned base is normally written exactly this way —
                // `abstract class CardBase : StatelessComponent` holding what a family of screens
                // shares and no Build — and everything it held was dropped, so a subclass that
                // called one built clean and died at first render on `this.frame is not a
                // function`. That is #268's failure reached one level up: the child keeps its
                // members now, and the base it calls into has to keep its own.
                //
                // ALL THREE COLLECTIONS, each measured on an abstract base carrying only that one
                // kind: a METHOD and a CONSTRUCTOR were dropped whole, and so was a PROPERTY —
                // which costs more than it looks, since an auto-property's default is applied in
                // the constructor, so a child that supplies no value silently got `undefined`
                // instead of the declared default. FIELDS are not in the list because they are not
                // lost: they come from the branch above and survived even before this.
                else if (!component.IsAbstract || component.BuildMethodNode != null
                    || component.Methods.Count > 0 || component.Properties.Count > 0
                    || component.Constructors.Count > 0)
                {
                    // Computed/get-set/static properties become real TS members (auto-props flow through
                    // the base Object.assign(props) instead).
                    EmitComponentProperties(component, c, slots);
                    if (orderedStatics)
                    {
                        component.UsedHelpers.Add(Eq.Import);
                        foreach (var member in TypeInitializer.Members(component.ClassSyntax!, component.Name, initializedStatics,
                                     _converter, Lowering, TypeAnnotations, _converter.Layout))
                            c.Member(member, member.Origin?.Member);
                    }
                    if (component.ClassSyntax is { } indexed)
                    {
                        _converter.SetCurrentClass(component.Name);
                        foreach (var indexer in indexed.Members.OfType<IndexerDeclarationSyntax>())
                            EmitIndexer(indexer, c);
                    }

                    // Constructor: assign positional params, apply auto-property defaults (only when a prop
                    // wasn't supplied — the base ctor's Object.assign runs first), then run the C# ctor body.
                    var ctorDef = component.Constructors.OrderByDescending(ct => ct.Parameters.Count).FirstOrDefault();
                    var ctorParams = ctorDef?.Parameters ?? new System.Collections.Generic.List<ParameterDefinition>();
                    // Auto-properties that must hold a value even when the caller supplies none: an explicit
                    // C# initializer, or the implicit default of a value type, which the server-side C#
                    // has and `undefined` does not — see ImplicitDefault.
                    var autoDefaults = component.Properties
                        .Where(p => !p.IsStatic && IsAutoProperty(p)
                                    && (p.DefaultValueNode != null || ImplicitDefault(p) != null))
                        .ToList();
                    // EITHER form of body counts: a block, or the arrow a one-line constructor is
                    // written with. Only the block was read, so `public Chart(x) => _x = x;` emitted a
                    // constructor that assigned nothing and left the field undefined.
                    var hasCtorBody = ctorDef?.BodyNode != null || ctorDef?.ExpressionBodyNode != null;
                    // A type with a static constructor runs it before its first instance, so the twin has
                    // a constructor to start it in (TypeInitializer.StartedIn) whether or not C# wrote one:
                    // without, `new Dial()` ran the static constructor never.
                    var startsOnConstruction = orderedStatics && TypeInitializer.HasStaticConstructor(component.ClassSyntax!);
                    if (ctorParams.Count > 0 || autoDefaults.Count > 0 || hasCtorBody || startsOnConstruction)
                    {
                        // C# optional parameters keep their defaults as JS default parameters
                        // (`variant: any = 'primary'`) — without them `new Button("x")` would run the
                        // body with `undefined` where C# guarantees `Variant.Primary`.
                        // A DEPENDENCY is not something the caller passes: it comes from the
                        // container, exactly as ActivatorUtilities gives it natively. So it leaves
                        // the signature entirely and is resolved in the body.
                        var services = ctorParams.Where(p => p.IsService).ToList();
                        var passed = ctorParams.Where(p => !p.IsService).ToList();

                        // Each parameter is bound under the name the constructor BODY reads it by, the one
                        // IdentifierStrategy gives a parameter: as written, made a legal JS identifier.
                        // Camel-cased here and read as written there, `string Label` was
                        // `constructor(label…)` beside a body reading `Label` (a ReferenceError at `new`),
                        // and `@default` bound `default`, a module that did not parse.
                        var paramList = string.Join(", ", passed.Select(p => p.DefaultValueNode != null
                            ? $"{p.Name.ToJsIdentifier()}: any = {_converter.ConvertExpression(p.DefaultValueNode, p.Type)}"
                            : $"{p.Name.ToJsIdentifier()}?: any"));
                        // The trailing config object is `props`, unless the C# constructor already binds
                        // that name: then `constructor(props, props)`, or a body's `let props` beside the
                        // parameter, did not parse. A `$` in front is a name no C# binding and no renamed
                        // local function can take.
                        var config = ConfigParameter(ctorParams.Select(p => p.Name.ToJsIdentifier()),
                            (SyntaxNode?)ctorDef?.BodyNode ?? ctorDef?.ExpressionBodyNode);
                        var signature = paramList.Length > 0
                            ? $"{paramList}, {OptionalParam(config, "any")}"
                            : OptionalParam(config, "any");
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
                                    : $"const {service.Name.ToJsIdentifier()}";
                                statements.Add(JsStatement.Raw($"{target} = {Eq.ResolveService}('{service.ServiceKey}');"));

                                // The twin of CapabilityScope.Require: a component that declared it
                                // cannot work without this one says so HERE, where the capability is
                                // missing, rather than letting undefined travel into its own code and
                                // fail at a member access that never mentions capabilities. The two
                                // targets have to agree about this, or the browser is the lenient one
                                // and the bug only exists there.
                                if (service.IsRequiredService)
                                {
                                    var read = ctorDef.IsPrimaryConstructor
                                        ? $"this.{service.Name.ToCamelCase()}"
                                        : service.Name.ToJsIdentifier();
                                    statements.Add(JsStatement.Raw($"if ({read} === undefined || {read} === null) throw new Error("
                                        + $"'{component.Name} needs {service.ServiceKey}, and this target has none. "
                                        + $"Register it with the host, or declare the parameter as {service.ServiceKey}? "
                                        + "if the component can work without it.');"));
                                }
                            }
                            foreach (var param in passed)
                            {
                                var camelName = param.Name.ToCamelCase();
                                var local = param.Name.ToJsIdentifier();
                                var target = component.Properties
                                    .FirstOrDefault(pr => !pr.IsStatic && pr.Name.ToCamelCase() == camelName);
                                // PRIMARY-constructor params are implicit fields — always assign. With an
                                // EXPLICIT ctor body, only params that map onto a real auto-property assign
                                // here; one that merely feeds a private/state field (`NestedChild(label)` →
                                // `_label`) has no `this.<name>` to write — the C# ctor body does the wiring.
                                if (hasCtorBody && target == null) continue;
                                // Nor does one whose twin is a GETTER: `Series => _series;` beside a `series`
                                // parameter emitted `get series()` and `this.series = series` into one class,
                                // which tsc rejects and which throws at `new` in the browser. The value still
                                // arrives, through the constructor body the author wrote — writing the field
                                // themselves is what a read-only property is FOR. Only the explicit-ctor path
                                // skips: a primary constructor has no body to do that wiring, so dropping the
                                // assignment there would lose the value instead of relocating it.
                                if (hasCtorBody && target != null && !IsAssignableSlot(target)) continue;
                                statements.Add(JsStatement.If(
                                    JsExpr.Binary(JsExpr.Identifier(local), "!==", JsExpr.Identifier("undefined")),
                                    Assign(JsExpr.ThisMember(camelName), JsExpr.Identifier(local)), null));
                            }
                            _converter.SetCurrentClass(component.Name);
                            foreach (var p in autoDefaults)
                            {
                                var cn = p.Name.ToCamelCase();
                                var def = p.DefaultValueNode != null
                                    ? Initializer(p.DefaultValueNode, _converter.ConvertExpression(p.DefaultValueNode, p.Type))
                                    : ImplicitDefault(p)!;
                                statements.Add(DefaultIfUndefined(cn, def));
                            }
                            var bodyLine = statements.Count + 1;
                            if (ctorDef?.BodyNode is { } ctorBlock) statements.Add(Contents(ctorBlock));
                            else if (ctorDef?.ExpressionBodyNode is { } ctorExpression)
                                statements.AddRange(Lowering.ExpressionBody(ctorExpression, returns: false).Statements);
                            // …and the initializer last, which is where C# runs it.
                            statements.Add(JsStatement.Raw($"if ({config} && typeof {config} === 'object') Object.assign(this, {config});"));
                            c.Member(JsClassMember.Constructor(signature, JsStatement.Block(statements)),
                                bodySource: (SyntaxNode?)ctorDef?.BodyNode ?? ctorDef?.ExpressionBodyNode, bodyLine: bodyLine);
                        }
                    }

                    // Build method — underscore the param when the body never uses it
                    // (noUnusedParameters-clean output; the override contract ignores names).
                    // An EXPRESSION-bodied Build has no `Body`, so this read `null?.Contains(...)`,
                    // answered `context`, and emitted a parameter the body never uses — which the
                    // emitted module's own type check rejects. Ask whichever half the method has.
                    // The parameter is named as C# named it, the name its body reads it by: `context`
                    // hard-coded left a `Build(ComponentContext ctx)` reading a `ctx` nothing declared.
                    // It takes the underscore only where the body never reads it (ReadsParameter).
                    var buildParameterSyntax = component.BuildMethodNode?.ParameterList.Parameters.FirstOrDefault();
                    var buildParamName = component.BuildMethodNode is not { } buildNode || buildParameterSyntax is null
                        ? "context"
                        : ReadsParameter(buildNode, buildParameterSyntax)
                            ? buildParameterSyntax.Identifier.Text.ToJsIdentifier()
                            : "_" + buildParameterSyntax.Identifier.Text.ToJsIdentifier();
                    // The body converts straight to IR: a block as itself, an expression-bodied Build
                    // (`IComponent Build(ctx) => new Box {…};`) as a return, and nothing as the fallback.
                    _converter.SetCurrentClass(component.Name);
                    // NOTHING AT ALL when the base supplies it. The fallback below is the honest
                    // stub over an abstract framework base, where no build exists to inherit; over
                    // an app-owned base it would OVERRIDE a working one with a throw.
                    if (!component.BuildComesFromTheBase)
                    {
                        var (buildBody, buildSource) = BuildBody(component.BuildMethodNode,
                            JsStatement.Raw("throw new Error('Build method not implemented');"));
                        c.Member(JsClassMember.Method("", "build", "", Lowering.Param(buildParamName, "BuildContext"), "", buildBody),
                            bodySource: buildSource);
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
                // Abstract classes: no build method emitted
                
                // Server Actions
                foreach (var action in component.ServerActions)
                {
                    ClassBuilder = c;
                    // Each parameter under a legal JS name, the same one the invocation passes on:
                    // `Run(int @class)` wrote `run(class)` and `[class]`, a module that did not parse.
                    var paramsList = string.Join(", ", action.Parameters.Select(p => Lowering.Param(p.Name.ToJsIdentifier(), Annotate(p.Type))));
                    var argsList = string.Join(", ", action.Parameters.Select(p => p.Name.ToJsIdentifier()));
                    var returnType = Annotate(action.ReturnType);

                    // The action's RESULT crosses the typed boundary too: a Task<decimal> arrives
                    // as a string, a Task<List<Todo>> as plain objects — hydrated ONCE here, by
                    // the spec of the C# return type, so the caller computes with runtime types.
                    var invoke = $"getServerActionsClient().invoke('{action.ActionId}', [{argsList}])";
                    var resultSpec = HydrationSpec.Of(ActionValueType(action.SyntaxNode), _hydrationReferences, _hydrationRuntimeReferences);
                    if (resultSpec is not null) component.UsedHelpers.Add(Eq.Import);

                    c.Member(JsClassMember.Method("async ", action.MethodName.ToCamelCase(), "", paramsList, "", JsStatement.Block(new[]
                    {
                        JsStatement.Raw(resultSpec is null
                            ? $"return await {invoke}"
                            : $"return {Eq.Hydrate}(await {invoke}, {resultSpec})"),
                    })), action.SyntaxNode);
                }

                _converter.SetCurrentClass(component.Name);
                EmitInheritedDefaults(component.ClassSyntax, c);

                // A static constructor runs before the first instance and the first use of any
                // static member, a method included, and not only before the first read of a static.
                if (orderedStatics && TypeInitializer.HasStaticConstructor(component.ClassSyntax!))
                    c.Rewrite(member => TypeInitializer.StartedIn(member, component.Name, slots));
            }, component.TypeParameters);

        // Generate component code without imports
        var componentCode = _builder.ToString();

        // NESTED static classes (each section's private `Copy` et al.) embed in THIS module as
        // plain (non-exported) classes above the component — as their own modules, two same-named
        // nested classes would overwrite each other's file, and the C# scoping is lexical anyway.
        var nestedCode = string.Empty;
        TypeScriptCodeBuilder? nb = null;
        if (component.BuildMethodNode?.Parent is ClassDeclarationSyntax ownerClass)
        {
            nb = new TypeScriptCodeBuilder { TypeAnnotations = TypeAnnotations, Layout = _converter.Layout };
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

        // Return imports + nested scope classes + component code. The two builders recorded their
        // mappings against their own text, and the module puts the imports above both and the
        // nested classes above the component: every segment moves down by what stands above it,
        // or a frame read through the map lands that many lines too early (#293).
        var module = new JsModule(imports, nestedCode + componentCode);
        var bodyLine = JsModuleWriter.BodyLine(module);
        _builder.ShiftMappings(bodyLine + nestedCode.Count(c => c == '\n'));
        if (nb is not null) _builder.AddMappings(nb.GetMappings(), bodyLine);
        return JsModuleWriter.Write(module);
    }
    
    /// <summary>Every identifier the emitted body mentions — the authority on which imports are live.</summary>
    private static HashSet<string> ReferencedIdentifiers(string emittedBody) =>
        System.Text.RegularExpressions.Regex
            .Matches(emittedBody, @"[A-Za-z_$][A-Za-z0-9_$]*")
            .Select(m => m.Value)
            .ToHashSet();

    /// <param name="component">The component whose module is being written.</param>
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
        // emitted static initializer throws "Person is not defined" at module load. A data class of
        // nested records parses into these fields, and its initializer is exactly where the nested
        // type is the ONLY mention (the catalogue shape every content file takes).
        foreach (var field in component.ComponentFields)
        {
            if (field.DefaultValueNode == null) continue;
            foreach (var t in CollectComponentTypesFromNode(field.DefaultValueNode, new HashSet<string> { component.Name }))
            {
                componentTypes.Add(t);
            }
        }

        // The defaults the class takes from its interfaces (#414) are written into its body, and
        // name types no syntax of the class does.
        foreach (var inherited in InheritedDeclarations(component.ClassSyntax))
        {
            if (ModelFor(inherited) is { } inheritedModel)
                Services.RuntimeProvidedTypeScanner.Collect(inherited, inheritedModel,
                    component.RuntimeProvidedTypes, new HashSet<string>());
            foreach (var t in CollectComponentTypesFromNode(inherited, new HashSet<string> { component.Name }))
                componentTypes.Add(t);
        }

        // Types a HYDRATION SPEC names — see _hydrationReferences: emitted into the body, present
        // in no syntax the walks above cover.
        foreach (var t in _hydrationReferences) componentTypes.Add(t);
        // ...and the vocabulary twins a spec names, which only the runtime exports: classified as
        // runtime-provided, so the router sends them to @equantic/runtime instead of a ./Rect.
        foreach (var t in _hydrationRuntimeReferences)
        {
            componentTypes.Add(t);
            component.RuntimeProvidedTypes.Add(t);
        }
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

        // …and the ones the parser kept OUT of that set. A type POSITION is the seventh way to name
        // a host-only symbol and the only one no expression strategy can reach — the parser's
        // semantic sweep is what sees it, and this is the first place with a diagnostics channel.
        foreach (var (named, at) in component.HostOnlyTypes)
            _converter.Report(at, ConversionSeverity.Error, "EQ2010",
                CodeGen.Extensions.HostOnlySymbolExtensions.Message(named));

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
                if (_dependencyResolver.GetRuntimeProvidedTypes().Contains(appType))
                {
                    component.RuntimeProvidedTypes.Add(appType);
                    componentTypes.Add(appType);
                    continue;
                }
                if (_dependencyResolver.IsModule(appType)) componentTypes.Add(appType);
            }
        }

        // AUTOMATIC DEPENDENCY RESOLUTION
        // Use dependency resolver to find transitive dependencies (e.g., Row → Flex). Runtime-provided
        // names must NOT seed it: the resolver is name-keyed over the per-app scan, so a vocabulary
        // "Row" would pull the WEB Row's dependency chain (Flex) into a page that never uses it.
        if (_dependencyResolver != null)
        {
            var fallbackRuntime = _dependencyResolver.GetRuntimeProvidedTypes();
            var perAppSeeds = componentTypes
                .Where(t =>
                {
                    var simple = t.Contains('.') ? t[(t.LastIndexOf('.') + 1)..] : t;
                    return !component.RuntimeProvidedTypes.Contains(simple) && !fallbackRuntime.Contains(simple);
                })
                .ToHashSet();
            var dependencies = _dependencyResolver.ResolveDependencies(perAppSeeds);
            foreach (var dep in dependencies)
            {
                componentTypes.Add(dep);
            }
        }

        var userComponents = new List<string>();

        // The user universe, discovered by scanning — components, records, helpers, plain classes
        // (IsAppModule). Consulted twice: by the standalone fallback below, and by the authoritative
        // filter at the end. No fixed lists on either path.
        var knownRuntimeProvided = _dependencyResolver?.GetRuntimeProvidedTypes() ?? (IReadOnlySet<string>)new HashSet<string>();

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
            if (component.RuntimeProvidedTypes.Contains(cleanType) || knownRuntimeProvided.Contains(cleanType))
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
                     && !IsAppModule(cleanType))
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
        // `$eq` itself is imported wherever the body names it, whoever wrote the name: a strategy that
        // writes a `$eq.*` call without registering the import (a dictionary's ContainsValue handed a
        // generated tuple equality, a hydration map's `byValue`) is otherwise a module that fails to
        // load on "$eq is not defined", which this emitter has met more than once.
        if (referenced.Contains(Eq.Import)) coreImports.Add(Eq.Import);
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
            var isEmittedType = IsAppModule(userComp);
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
        // A NULLABLE starts null, whatever it wraps. `int?` and `bool?` began 0 and false here, where
        // C# begins them null, so `_count == null` answered the opposite on the two sides; and a
        // reference annotated `?` was left out altogether, `undefined`, which tsc refuses as never
        // assigned (TS2564) and `=== null` reads as a value.
        if (csharpType.EndsWith('?')) return "null";
        if (ImplicitValueTypeDefault(csharpType) is { } byName) return byName;
        if (BindType(typeNode) is not { } symbol) return null;
        var bySymbol = _converter.DefaultOf(symbol);
        return bySymbol == "null" ? null : bySymbol;
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
    /// <para>
    /// The value is answered by the one table (<see cref="Strategies.DefaultValue"/>), and it
    /// has to be: a `long` defaults to 0n and a `decimal` to a Decimal, and answering plain `0` for
    /// them put a NUMBER in a slot the twin declares `bigint`, so the first arithmetic on it threw
    /// "Cannot mix BigInt and other types" — in the browser only, after hydration, on a page whose
    /// server render was perfect.
    /// </para>
    /// <para>It is decided HERE, where the module's imports are, and through the converter: the
    /// parser used to write it as text, so a struct the zero constructs (<c>new Outer(new Inner(),
    /// 0)</c>) was named in a module that never imported it (found in review, #409).</para>
    /// <para>Null for reference types, where C#'s default and `undefined` really do behave alike.</para>
    /// </summary>
    /// <returns>The JS default, or null where there is none to write (a reference type, a nullable
    /// value type, an enum with no zero member — C#'s default there is null or an unnamed value,
    /// and `undefined` is the honest twin).</returns>
    private string? ImplicitDefault(PropertyDefinition property)
    {
        if (property.Node is not { Initializer: null } node || BindType(node.Type) is not { } type) return null;

        // An enum with no zero member: C#'s default is an unnamed value, so leave the slot alone
        // rather than inventing a name for it. DefaultValue answers "0" there, which would be a
        // number in a slot the twin declares as the member-name string.
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            && !enumType.IsFlagsEnum()
            && !enumType.GetMembers().OfType<IFieldSymbol>().Any(field => field.HasConstantValue
                && Convert.ToInt64(field.ConstantValue, System.Globalization.CultureInfo.InvariantCulture) == 0))
        {
            return null;
        }

        // A struct whose twin cannot build its zero answers `undefined`, which IS the slot left
        // alone: writing it would only emit `if (this.width === undefined) this.width = undefined`.
        var value = _converter.DefaultOf(type);
        return value is "null" or "undefined" ? null : value;
    }

    private static string? ImplicitValueTypeDefault(string csharpType) => csharpType switch
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
            "StatefulComponent" or "StatelessComponent" => true,
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

    /// <summary>
    /// Every class or struct declared in SOURCE that a type names, however deep: the type itself, an
    /// array's element, and each argument of a generic (`Func&lt;Widget, int&gt;` names Widget, and a
    /// scan that kept a generic's last argument missed it, found in review, #418). Only such a type
    /// becomes a module; a BCL or a metadata type (`string` is System.String) has none of its own.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> SourceTypesIn(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var inner in SourceTypesIn(array.ElementType)) yield return inner;
                break;
            case INamedTypeSymbol named:
                if (named.TypeKind is TypeKind.Class or TypeKind.Struct && named.Name.Length > 0
                    && named.Locations.Any(location => location.IsInSource))
                    yield return named;
                foreach (var argument in named.TypeArguments)
                    foreach (var inner in SourceTypesIn(argument)) yield return inner;
                break;
        }
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

        // A PARAMETER's type is written into the signature's annotation, and an app type named only
        // there was annotated without being imported: an inherited default's `Draw(Widget w)` was
        // (found in review, #418), and a class's own method's the same.
        // Asked of the model: an interface, an enum or a type parameter is annotated as something
        // else (`any`, the member string) and has no module, so importing it names a file nobody writes.
        foreach (var parameter in node.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (parameter.Type is not { } declared || ModelFor(declared)?.GetTypeInfo(declared).Type is not { } typed)
                continue;
            foreach (var candidate in SourceTypesIn(typed))
                if (localNames == null || !localNames.Contains(candidate.Name))
                    types.Add(candidate.Name);
        }

        // A TARGET-TYPED `new(...)` states NO name — `ObjectCreationStrategy` recovers it from the
        // model and emits `new CatalogueEntry(...)`, so the import must be recovered the same way
        // (a declared type only covers the OUTERMOST creation; nested ones live inside arguments).
        foreach (var implicitCreation in node.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            var created = ModelFor(implicitCreation)?.GetTypeInfo(implicitCreation).Type;
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
        // A parameter's NAME inside a function type is a label and not a type, so a name followed by
        // its colon is left out: `value` in `(value: Point) => string | null` resolved nothing, the
        // whole parameter degraded to `any`, and every lambda passed to it was an implicit any.
        if (ts.Contains('<') || ts.Contains("=>") || ts.Contains('('))
        {
            var names = System.Text.RegularExpressions.Regex
                .Matches(ts, @"\b[A-Za-z_][A-Za-z0-9_]*\b(?!\s*\??:)")
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

    /// <summary>
    /// True when the emitted class has a slot this property can be WRITTEN to, which is what decides
    /// whether the constructor protocol may assign a same-named parameter onto it.
    /// <para>
    /// An auto-property is emitted type-only and filled from outside (the base `Object.assign(props)`
    /// or the constructor), so it takes an assignment. Anything with a real accessor takes one only
    /// when a SETTER is emitted beside the getter — and an expression-bodied property
    /// (`Series => _series;`) emits a getter alone.
    /// </para>
    /// <para>
    /// Getting this wrong is not a cosmetic mismatch. `get series()` and `this.series = series` in
    /// one class is TS2540 to the type checker, and a class body is strict-mode code, so the
    /// assignment THROWS a TypeError at `new` — a component the server rendered perfectly dies at
    /// hydration. Found while writing the first chart, whose shape (private field, read-only
    /// property, constructor parameter of the same name) is ordinary C# any consumer can write.
    /// </para>
    /// </summary>
    private static bool IsAssignableSlot(PropertyDefinition p)
    {
        // No syntax to read (a property the parser knew without a declaration): keep the old answer
        // rather than guess a new one.
        if (IsAutoProperty(p)) return true;
        if (p.Node?.AccessorList is not { } list) return false;   // expression-bodied: a getter alone
        var setter = list.Accessors.FirstOrDefault(a => a.Keyword.Text is "set" or "init");
        return setter is not null && (setter.Body != null || setter.ExpressionBody != null);
    }


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
    private void EmitComponentProperties(ComponentDefinition component, TypeScriptCodeBuilder.ClassBuilder c,
        IReadOnlySet<string> slots)
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
                c.Member(JsClassMember.Getter(stat, name, "", Lowering.ExpressionBody(node.ExpressionBody.Expression, returns: true)), node);
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
                        // A static one's store is the class's, where its accessors' `this` is, and
                        // holds its initializer or its type's default from the start, as a static
                        // auto-property's does: declared on the instance, the slot they wrote did not
                        // exist, and declared alone it read undefined until the first write (#483).
                        // A store that initializes in order is its slot, and the accessors read it.
                        if (prop.IsStatic && slots.Contains(slot)) { }
                        else if (prop.IsStatic && StaticInitial(component, prop) is { } initial)
                            c.Field(slot, DeclarationType(component, prop.Type), initial, node, isStatic: true);
                        else
                            c.Field(slot, DeclarationType(component, prop.Type), null, node, isStatic: prop.IsStatic, isDeclare: true);
                        if (!getterHasBody && getter != null)
                            c.Member(JsClassMember.Getter(stat, name, "", JsStatement.Return(JsExpr.ThisMember(slot))), getter);
                    }

                    if (getterHasBody)
                    {
                        var body = getter!.ExpressionBody != null
                            ? Lowering.ExpressionBody(getter.ExpressionBody.Expression, returns: true)
                            : Lowering.AccessorBody(getter.Body!);
                        c.Member(JsClassMember.Getter(stat, name, "", body), getter);
                    }
                    if (setterHasBody)
                    {
                        // C# setters use the implicit `value` parameter, which survives conversion as-is.
                        var body = setter!.ExpressionBody != null
                            ? Lowering.ExpressionBody(setter.ExpressionBody.Expression, returns: false)
                            : Lowering.AccessorBody(setter.Body!);
                        c.Member(JsClassMember.Setter(stat, name, "value", body), setter);
                    }
                    continue;
                }

                // Pure auto-property. A static one carries its initializer as a real field; an INSTANCE one
                // is populated from outside the class body (base Object.assign(props) / the ctor), so it is
                // emitted TYPE-ONLY — the declaration restores type checking on `this.x` without emitting
                // runtime code that would clobber the assigned value under useDefineForClassFields.
                _converter.SetCurrentClass(component.Name);
                if (prop.IsStatic && slots.Contains(name))
                {
                    // Its slot's, in the type initializer.
                }
                else if (prop.IsStatic)
                {
                    c.Field(name, DeclarationType(component, prop.Type), StaticInitial(component, prop), node, isStatic: true);
                }
                else
                {
                    c.Field(name, DeclarationType(component, prop.Type), null, node, isDeclare: true);
                }
            }
        }
    }

    /// <summary>
    /// What a static property's store holds before anything writes it: its initializer, or its
    /// type's default (a number's 0, an enum's zero member). Null for a reference type with neither,
    /// whose C# default is null. No constructor runs for a static, so the value has to be on the
    /// declaration: <c>static int Count { get; set; }</c> read undefined, where C# reads 0.
    /// </summary>
    private string? StaticInitial(ComponentDefinition component, PropertyDefinition prop)
    {
        var initial = prop.DefaultValueNode is { Parent: EqualsValueClauseSyntax given } && prop.Node is { } declared
            ? StaticValue(given, declared.Type)
            : ValueTypeDefault(prop.Type, prop.Node?.Type);
        if (initial is not null && initial.Contains("$eq.")) component.UsedHelpers.Add(Eq.Import);
        return initial;
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
    /// members, a type initializer's slots — each have to ask it themselves, and for a long time none of them did.
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
        var name = cls.Identifier.Text;
        if (!asStatic) EmitInstanceConstructor(cls, c);

            // A type whose statics can observe one another starts each at its zero and initializes
            // them in declaration order on first use (TypeInitializer, #417). A static that
            // CONSTRUCTS something kept a lazy getter of its own, which survived the library's import
            // cycles (its modules import each other through one barrel, and whichever loads first
            // sees the other's class as undefined) but initialized each static on its own first read,
            // in no order, and again whenever it held null. The type initializer is lazy too, so the
            // cycle is survived as it was. The statics are collected in ONE pass, in declaration order
            // (TypeInitializer.Stores): every field and then every property ran a property's
            // initializer after a field's written below it.
            var ordered = TypeInitializer.Orders(cls, ModelFor);
            _converter.SetCurrentClass(name);
            var initialized = ordered ? TypeInitializer.Collect(cls, DeclaredType, DefaultOf, StaticValue) : [];
            var slots = initialized.Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var f in cls.Members.OfType<FieldDeclarationSyntax>())
            {
                foreach (var v in f.Declaration.Variables)
                {
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
                    // A static that initializes in order is its slot's, and holds no field.
                    if (isStaticMember && slots.Contains(fieldName)) continue;
                    // An instance field is state, declared and started with the class's other state, in
                    // declaration order (InstanceState, #571, #582).
                    if (!isStaticMember) continue;
                    var def = v.Initializer is { } given ? StaticValue(given, f.Declaration.Type) : null;
                    // A static with no initializer holds its type's zero, as C# starts it (#417):
                    // `static int Count;` read undefined, and its first `++` made it NaN.
                    c.Field(fieldName, DeclaredType(f.Declaration.Type), def ?? DefaultOf(f.Declaration.Type), v, isStatic: true);
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
                // The PROPERTY's own `static`, as a field's and an auto-property's already are: the
                // class-level qualifier alone wrote `static int Count => …` on a plain class as an
                // instance getter, and `Probe.count` read undefined.
                var accessorQualifier = asStatic || p.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)
                    ? "static "
                    : "";
                if (p.ExpressionBody != null)
                {
                    EmitGetter(p, c, accessorQualifier);
                }
                else if (p.AccessorList != null)
                {
                    // C# 14 `field`: the property keeps its own store and the accessors guard it.
                    // The slot has to exist before the getter names it — see FieldExpressionStrategy
                    // for why it is called `$name` (a name no C# field can take).
                    var backed = Strategies.Expressions.FieldExpressionStrategy.UsesBackingField(p);
                    var isStaticProperty = accessorQualifier.Length > 0;
                    if (backed && isStaticProperty)
                    {
                        var slot = Strategies.Expressions.FieldExpressionStrategy.BackingSlot(p);
                        // The store starts as the property's initializer, which C# writes into it
                        // directly, or as its type's default. The initializer was dropped: the
                        // accessors are emitted, so nothing below writes it (#483).
                        // On the class for a static property, where its accessors' `this` is the
                        // class: on the instance, a static `field` read undefined (#483).
                        // A store that initializes in order is its slot, and the accessors read it.
                        if (!slots.Contains(slot))
                        {
                            var slotDefault = p.Initializer is { } given ? StaticValue(given, p.Type) : DefaultOf(p.Type);
                            if (slotDefault == "null")
                            {
                                if (CanDeclareTypeOnly)
                                    c.Member(JsClassMember.Field("declare static ", slot, $": {DeclaredType(p.Type)}"), p);
                            }
                            else
                                c.Field(slot, DeclaredType(p.Type), slotDefault, p, isStatic: true);
                        }
                        // An automatic getter reads the store. Without one the class fell to the
                        // auto-property's field below, named like the property, which shadows the
                        // setter: a write skipped it, and `Total = 3` read back 3 where C# reads 6.
                        if (p.AccessorList.Accessors.FirstOrDefault(a => a.Keyword.Text == "get") is { Body: null, ExpressionBody: null })
                            c.Member(JsClassMember.Getter(accessorQualifier, pn, Annotation(DeclaredType(p.Type)),
                                JsStatement.Return(JsExpr.ThisMember(slot))), p);
                    }
                    // An instance property that keeps a store, which the constructor starts with the
                    // class's other state (InstanceState), reads and writes it through accessors
                    // (EmitCompilerAccessors, below).
                    var stored = !isStaticProperty && PropertyStore.KeepsAStore(p);

                    // A property guarding a store has its accessors, and no field of its name.
                    if (EmitGetter(p, c, accessorQualifier) || backed || stored) { }
                    // A static that initializes in order is its slot's.
                    else if (isStaticProperty && slots.Contains(pn)) { }
                    // An instance auto-property is state, declared and started with the class's other
                    // state, in declaration order (InstanceState, #582).
                    else if (!isStaticProperty) { }
                    else if (p.Initializer is { } initial)
                        c.Field(pn, DeclaredType(p.Type), StaticValue(initial, p.Type), p, isStatic: true);
                    // A static AUTO-property — `{ get; set; }`, `{ get; private set; }`, `{ get; }` — is a
                    // field with a name, holding its type's default until something assigns it: `static
                    // bool ReadOnly { get; set; }` IS false, and undefined is not false to `===`.
                    else
                        c.Field(pn, DeclaredType(p.Type), DefaultOf(p.Type), p, isStatic: true);

                    EmitSetter(p, c, accessorQualifier);
                }
                if (accessorQualifier.Length == 0) EmitCompilerAccessors(p, c);
            }
            // `event Action<T>? Changed;` — a member the model raises and a caller subscribes to.
            // Nothing emitted it, so `this.changed?.(edit)` reached a property that did not exist.
            // A static one is a static like any other where the type initializes in order: a
            // subscription is a use of the type, which runs its static constructor first, and the
            // handlers that constructor adds come before the subscriber's, as in .NET. As a plain
            // field, it was subscribed to before the constructor ran.
            foreach (var e in cls.Members.OfType<EventFieldDeclarationSyntax>())
            {
                var isStaticEvent = asStatic || e.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword);
                foreach (var v in e.Declaration.Variables)
                {
                    if (isStaticEvent && slots.Contains(v.Identifier.Text.ToCamelCase())) continue;
                    // An instance one is state, declared and started with the class's other state
                    // (InstanceState).
                    if (isStaticEvent) c.Field(v.Identifier.Text.ToCamelCase(), DeclaredType(e.Declaration.Type), "null", v, isStatic: true);
                }
            }
            if (ordered)
            {
                _converter.UsedHelpers.Add(Eq.Import);
                foreach (var member in TypeInitializer.Members(cls, name, initialized, _converter, Lowering, TypeAnnotations, _converter.Layout))
                    c.Member(member, member.Origin?.Member);
            }
            foreach (var m in cls.Members.OfType<MethodDeclarationSyntax>())
                EmitClassMethod(m, c, asStatic);
            foreach (var indexer in cls.Members.OfType<IndexerDeclarationSyntax>())
                EmitIndexer(indexer, c);
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
                c.Member(JsClassMember.Method("static ", opName, "", opPars, "", opBody), op);
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
                c.Member(JsClassMember.Method("static ", convName, "", convPar, "", convBody), conversion);
            }

            // A twin that keeps a store (PropertyStore, #591) writes it in JSON under its property's
            // name, read through the property, as System.Text.Json writes the property: JSON.stringify
            // writes an object's own properties, so a server action received `$name` and bound nothing.
            // So does one whose field moved a case apart from a member (`value$`, #396), whose own key
            // is storage no server reads. A derived class inherits it.
            if (!asStatic && (cls.Members.OfType<PropertyDeclarationSyntax>().Any(PropertyStore.KeepsAStore)
                    || cls.Members.OfType<FieldDeclarationSyntax>().SelectMany(field => field.Declaration.Variables)
                        .Any(variable => SlotOf(variable, ModelFor(cls)).EndsWith('$'))))
            {
                _converter.UsedHelpers.Add(Eq.Import);
                c.Member(JsClassMember.Method("", "toJSON", "", "", "",
                    JsStatement.Return(JsExpr.Call(JsExpr.Opaque(Eq.Json), JsExpr.This))), cls);
            }

            EmitExtensionBlocks(cls, c);

            // A static constructor runs before the first instance and the first use of any static
            // member, a method included, and not only before the first read of a static.
            if (ordered && TypeInitializer.HasStaticConstructor(cls))
                c.Rewrite(member => TypeInitializer.StartedIn(member, name, slots));
    }

    /// <summary>A static's initializer: its VALUE where C# folds it to a constant
    /// (<see cref="TypeInitializer.Constant"/>), and the expression converted otherwise.</summary>
    private string StaticValue(EqualsValueClauseSyntax initializer, TypeSyntax type) =>
        TypeInitializer.Constant(initializer, ModelFor(initializer), _converter)
        ?? Initializer(initializer.Value, _converter.ConvertExpression(initializer.Value, type.ToString()));

    /// <summary>
    /// A property's getter where it has one with a body: its expression body, or its get
    /// accessor's. The RETURN type is emitted: a computed property is where a model's types cross
    /// from one member to the next, and an unannotated getter makes every read of it `any`, which
    /// then spreads to every lambda over what it returned.
    /// </summary>
    private bool EmitGetter(PropertyDeclarationSyntax p, TypeScriptCodeBuilder.ClassBuilder c, string qualifier)
    {
        var pn = p.Identifier.Text.ToCamelCase();
        var annotation = Annotation(DeclaredType(p.Type));
        if (p.ExpressionBody != null)
        {
            c.Member(JsClassMember.Getter(qualifier, pn, annotation,
                Lowering.ExpressionBody(p.ExpressionBody.Expression, returns: true)), p);
            return true;
        }
        var g = p.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get");
        if (g?.ExpressionBody != null)
            c.Member(JsClassMember.Getter(qualifier, pn, annotation,
                Lowering.ExpressionBody(g.ExpressionBody.Expression, returns: true)), g);
        else if (g?.Body != null)
            c.Member(JsClassMember.Getter(qualifier, pn, annotation, Lowering.AccessorBody(g.Body)), g);
        else
            return false;
        return true;
    }

    /// <summary>
    /// A property's SETTER body — the guarded assignment idiom
    /// (`set { if (value == _x) return; _x = value; Raise(); }`) is where a model keeps its
    /// invariants. Emitting only the getter made every assignment to it a type error, and would
    /// have dropped the invariant if it had compiled. `init` too, and not only `set`: an init
    /// accessor is where a component states what its configuration may be
    /// (`init => field = value.Count > 3 ? throw …`), and looking only for "set" dropped that guard
    /// silently — the invariant simply did not exist in the twin.
    /// </summary>
    private void EmitSetter(PropertyDeclarationSyntax p, TypeScriptCodeBuilder.ClassBuilder c, string qualifier)
    {
        var pn = p.Identifier.Text.ToCamelCase();
        var setter = p.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text is "set" or "init");
        if (setter?.ExpressionBody != null)
            c.Member(JsClassMember.Setter(qualifier, pn, $"value{Annotation(DeclaredType(p.Type))}",
                Lowering.ExpressionBody(setter.ExpressionBody.Expression, returns: false)), setter);
        else if (setter?.Body != null)
            c.Member(JsClassMember.Setter(qualifier, pn, $"value{Annotation(DeclaredType(p.Type))}",
                Lowering.AccessorBody(setter.Body)), setter);
    }

    /// <summary>
    /// The accessors the compiler writes for an instance property (<see cref="PropertyStore.CompilerAccessors"/>,
    /// #591): over the store of one that keeps one, and the half an override inherits, forwarded to
    /// <c>super</c>. A record's twin takes the same ones. The accessors C# writes a body for are lowered by
    /// <see cref="EmitGetter"/> and <see cref="EmitSetter"/>.
    /// </summary>
    private void EmitCompilerAccessors(PropertyDeclarationSyntax p, TypeScriptCodeBuilder.ClassBuilder c)
    {
        var annotation = Annotation(DeclaredType(p.Type));
        foreach (var accessor in PropertyStore.CompilerAccessors(p, ModelFor(p), annotation, $"value{annotation}"))
            c.Member(accessor, p);
    }

    /// <summary>A method of a class module, or of a component's twin when an interface's default
    /// supplies it.</summary>
    private void EmitClassMethod(MethodDeclarationSyntax m, TypeScriptCodeBuilder.ClassBuilder c, bool asStatic)
    {
        if (Lowering.Method(m, asStatic, DeclaredType, returns: TupleReturn) is { } member) c.Member(member, m);
    }

    /// <summary>
    /// An instance indexer as the methods every element access bound to it calls, <c>item</c> and
    /// <c>setItem</c> (#427), in a class, a component, or a default an interface supplies. It was
    /// written into no twin, and `grid[3]` read a property named "3" that nothing had.
    /// </summary>
    private void EmitIndexer(IndexerDeclarationSyntax indexer, TypeScriptCodeBuilder.ClassBuilder c)
    {
        foreach (var member in Lowering.Indexer(indexer, DeclaredType)) c.Member(member, member.Origin?.Member ?? indexer);
    }

    /// <summary>A vocabulary default the class takes from the interface's ASSEMBLY, where eqc has no
    /// body to convert: the twin delegates to the runtime's copy, which the runtime import brings in.</summary>
    private void EmitDelegatedDefault(ISymbol implementation, TypeScriptCodeBuilder.ClassBuilder c)
    {
        var (name, parameters, call) = DefaultInterfaceMembers.Delegation(implementation);
        _converter.UsedRuntimeTypes.Add(implementation.ContainingType.Name);
        var body = JsStatement.Block([JsStatement.Return(JsExpr.Callish(call))]);
        if (implementation is IMethodSymbol)
            c.Member(JsClassMember.Method("", name, "",
                string.Join(", ", parameters.Select(parameter => TypeAnnotations ? $"{parameter}: any" : parameter)), "", body));
        else
            c.Member(JsClassMember.Getter("", name, "", body));
    }

    /// <summary>The declarations of the defaults a class takes (see <see cref="EmitInheritedDefaults"/>),
    /// for the scans that decide the module's imports: a default names types its class never does.</summary>
    private IEnumerable<MemberDeclarationSyntax> InheritedDeclarations(TypeDeclarationSyntax? declaration) =>
        declaration is not null && ModelFor(declaration) is { } model
            && model.GetDeclaredSymbol(declaration) is INamedTypeSymbol self
            ? DefaultInterfaceMembers.Of(self, model.Compilation).Select(member => member.Declaration).OfType<MemberDeclarationSyntax>()
            : [];

    /// <summary>
    /// The DEFAULT INTERFACE MEMBERS the class relies on (#414), written into its twin: JavaScript
    /// has no interface to hold them, so a class that did not declare one had no such member at all,
    /// and <c>PlainTextLanguage</c>'s twin answered <c>rules</c> with undefined. Each body converts
    /// under its interface's file, where its names resolve. A default the transpiler cannot read,
    /// because its interface is compiled into a referenced assembly the runtime does not carry, refuses the class (EQ1008).
    /// </summary>
    private void EmitInheritedDefaults(TypeDeclarationSyntax? declaration, TypeScriptCodeBuilder.ClassBuilder c)
    {
        if (declaration is null || ModelFor(declaration) is not { } model
            || model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol self)
            return;
        foreach (var (implementation, member, _) in DefaultInterfaceMembers.Of(self, model.Compilation))
        {
            if (member is not null && ModelFor(member) is { } memberModel
                && DefaultInterfaceMembers.InterfaceStaticIn(member, memberModel) is { } reached)
            {
                _converter.Report(declaration, ConversionSeverity.Error, "EQ1008",
                    DefaultInterfaceMembers.Homeless(self, implementation, reached));
                continue;
            }
            switch (member)
            {
                case PropertyDeclarationSyntax property when property.ExpressionBody != null
                    || property.AccessorList?.Accessors.Any(a => a.Body != null || a.ExpressionBody != null) == true:
                    _converter.InFileOf(property, () =>
                    {
                        EmitGetter(property, c, "");
                        EmitSetter(property, c, "");
                    });
                    break;
                case MethodDeclarationSyntax method when method.Body != null || method.ExpressionBody != null:
                    _converter.InFileOf(method, () => EmitClassMethod(method, c, asStatic: false));
                    break;
                // A default indexer, as the class's own is written (#427).
                case IndexerDeclarationSyntax indexer:
                    _converter.InFileOf(indexer, () => EmitIndexer(indexer, c));
                    break;
                case null when DefaultInterfaceMembers.RuntimeCarries(implementation.ContainingType):
                    EmitDelegatedDefault(implementation, c);
                    break;
                default:
                    _converter.Report(declaration, ConversionSeverity.Error, "EQ1008",
                        DefaultInterfaceMembers.Unreadable(self, implementation));
                    break;
            }
        }
    }

    /// <summary>
    /// An operator's body in either spelling, or null where it has neither — `extern`, or a
    /// declaration in an interface — and there is nothing to write.
    /// <para>
    /// It is a method's body, built as one: each statement maps to its line (#293) and declares
    /// what its expressions declare. `int.TryParse(s, out var n)` inside an operator emits `n = …`,
    /// and with nothing declaring `n` in a strict ES module the operator threw a ReferenceError the
    /// first time it ran instead of returning a value.
    /// </para>
    /// </summary>
    private JsStatement? OperatorBody(BaseMethodDeclarationSyntax op) =>
        op.Body is null && op.ExpressionBody is null
            ? null
            : Lowering.Body(op.Body, op.ExpressionBody?.Expression, isIterator: false, byReference: []);

    /// <summary>An extension property's or indexer's getter: its expression, or its block lowered as
    /// an accessor's is, an iterator's included (#432).</summary>
    private JsStatement ExtensionGetter(BlockSyntax? block, ExpressionSyntax? expression) =>
        block is not null ? Lowering.AccessorBody(block) : Lowering.Body(null, expression, isIterator: false, []);

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
                : rest.Length == 0 ? Lowering.Param(receiverName, receiverType) : $"{Lowering.Param(receiverName, receiverType)}, {rest}";

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
                        c.Member(JsClassMember.Method("static ", property.Identifier.Text.ToCamelCase(), "", WithReceiver(""),
                            Annotation(DeclaredType(property.Type)), ExtensionGetter(getterBlock, body)), property);
                        ReportExtensionSetter(property.AccessorList?.Accessors, property.Identifier.Text);
                        break;
                    }

                    case IndexerDeclarationSyntax indexer:
                    {
                        var body = indexer.ExpressionBody?.Expression
                            ?? indexer.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get")?.ExpressionBody?.Expression;
                        var getterBlock = indexer.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get")?.Body;
                        var pars = string.Join(", ", indexer.ParameterList.Parameters
                            .Select(pp => Lowering.Param(pp.Identifier.Text.ToJsIdentifier(), DeclaredType(pp.Type))));
                        if (body is null && getterBlock is null)
                        {
                            _converter.Report(indexer, ConversionSeverity.Error, "EQ2008",
                                "extension indexer has no getter body the compiler can lower.");
                            break;
                        }
                        c.Member(JsClassMember.Method("static ", "item", "", WithReceiver(pars),
                            Annotation(DeclaredType(indexer.Type)), ExtensionGetter(getterBlock, body)), indexer);
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
                        // The one method lowering, its receiver in front (#432): it wrote `yield` outside
                        // a generator for an iterator, and asked the return type's name whether it was async.
                        if (Lowering.Method(method, asStatic: true, DeclaredType, WithReceiver, TupleReturn) is { } lowered)
                            c.Member(lowered, method);
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
    /// A plain class's constructor: its C# constructors as the twin's one (<see cref="TwinConstructor"/>,
    /// #583), the builder a record's twin is built with, which starts the class's state
    /// (<see cref="InstanceState"/>) and runs its base's constructor with its own arguments. It kept the
    /// widest constructor, a trailing config object assigned last and a `super()` with no arguments:
    /// `Money() : this(100)` built a Money of no cents, `: base(x * 2)` passed the base nothing, and a
    /// primary constructor was not read at all, its first argument taken for the config. An object
    /// initializer is applied by the construction site once the constructor returns, as C# applies it.
    /// </summary>
    private void EmitInstanceConstructor(ClassDeclarationSyntax cls, TypeScriptCodeBuilder.ClassBuilder c)
    {
        var twin = new TwinConstructor(_converter, Lowering, ModelFor, TypeAnnotations, _converter.Layout, DeclaredType);
        var clause = cls.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault();
        var state = InstanceState(cls);
        c.Member(twin.Build(cls, state.Select(member => twin.StartOf(member.Slot, member.Declaration, member.TsType)).ToList(),
            HasEmittedBase(cls), clause, first: null), cls);
        // The same members, each a class field JavaScript defines on the instance before the constructor
        // writes it, so the write never reaches an accessor of its name (ClassBuilder.State). A store
        // a base's twin already defines is declared for TypeScript only: defined again, it would be
        // undefined once `super()` returned, until the constructor wrote it (TypeScript refuses that,
        // TS2612).
        foreach (var member in state)
        {
            if (member.DefinedByBase) c.Field(member.Slot, member.TsType, null, member.Declaration, isDeclare: true);
            else c.State(member.Slot, member.TsType, member.Declaration);
        }
    }

    /// <summary>One member of a plain class's instance state: the slot it lives in on each instance, what
    /// TypeScript reads it as, the C# that declares it, and whether a base's twin defines the slot, as it
    /// does the store an override shares with the property it overrides.</summary>
    private readonly record struct InstanceMember(string Slot, string TsType, SyntaxNode Declaration, bool DefinedByBase = false);

    /// <summary>
    /// What a plain class's constructor starts, in the order C# starts it (#571, #582): the primary
    /// constructor's parameters a member reads, which C# holds before anything runs, then each instance
    /// field, auto-property, store of a property that keeps one (<see cref="PropertyStore"/>), and
    /// field-like event, in declaration order, its initializer or its type's default. A field's and a
    /// property's alike: the property's was a class field with its initializer, which ran before every
    /// field's initializer the constructor wrote, and in a derived class after its base's constructor.
    /// </summary>
    private IReadOnlyList<InstanceMember> InstanceState(ClassDeclarationSyntax cls)
    {
        var state = new List<InstanceMember>();
        var model = ModelFor(cls);
        foreach (var parameter in cls.ParameterList?.Parameters ?? default)
            if (cls.HoldsParameter(parameter, model))
                state.Add(new(parameter.Identifier.ValueText.ToCamelCase(), DeclaredType(parameter.Type), parameter));
        foreach (var member in cls.Members)
        {
            if (member.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)
                || member.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstKeyword)) continue;
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                        state.Add(new(SlotOf(variable, model), DeclaredType(field.Declaration.Type), variable));
                    break;
                case EventFieldDeclarationSyntax handler:
                    foreach (var variable in handler.Declaration.Variables)
                        state.Add(new(variable.Identifier.Text.ToCamelCase(), DeclaredType(handler.Declaration.Type), variable));
                    break;
                case PropertyDeclarationSyntax property when PropertyStore.SlotOf(property) is { } slot:
                    state.Add(new(slot, DeclaredType(property.Type), property,
                        PropertyStore.KeepsAStore(property) && PropertyStore.BaseKeepsTheStore(property, model)));
                    break;
            }
        }
        return state;
    }

    /// <summary>The slot an instance field's variable lives in (<see cref="Extensions.FieldSlotExtensions"/>):
    /// its twin name, or, a case apart from another member, the name with a <c>$</c> after it.</summary>
    private static string SlotOf(VariableDeclaratorSyntax variable, SemanticModel? model) =>
        model?.GetDeclaredSymbol(variable) is IFieldSymbol field ? field.TwinSlot() : variable.Identifier.Text.ToCamelCase();

    /// <summary>
    /// Whether <paramref name="method"/> reads <paramref name="parameter"/>. Asked of the model where
    /// there is one, since the name alone is a member's too (`this.ctx` beside a parameter `ctx`), and
    /// of the syntax otherwise: a name that is not a member access's, a binding's or an initializer's.
    /// A short name like `c` is a substring of nearly any body, so the text is never asked.
    /// </summary>
    private bool ReadsParameter(SyntaxNode method, ParameterSyntax parameter)
    {
        var name = parameter.Identifier.ValueText;
        var named = method.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText == name);
        if (ModelFor(method) is { } model && model.GetDeclaredSymbol(parameter) is { } symbol)
            return named.Any(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id).Symbol, symbol));
        return named.Any(id => id.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression == id,
            MemberBindingExpressionSyntax or NameColonSyntax or NameEqualsSyntax => false,
            _ => true,
        });
    }

    /// <summary>
    /// The name of the config object a constructor takes last: <c>props</c>, or <c>$props</c> where
    /// the constructor already binds <c>props</c>, which no C# binding and no renamed local function
    /// can be. Its parameters are asked, and every binding of the constructor <paramref name="inside"/>
    /// belongs to: its body shares the parameters' block, so a local `props` beside the parameter was
    /// "Identifier 'props' has already been declared".
    /// </summary>
    private static string ConfigParameter(IEnumerable<string> parameterNames, SyntaxNode? inside) =>
        parameterNames.Contains("props", StringComparer.Ordinal)
            || (inside is not null && LocalFunctionName.MemberDeclares(inside, "props"))
            ? "$props"
            : "props";


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

        // The model of the type's OWN file: an interface's default is written into a class declared
        // in another one (#414), and a model throws for a node outside its tree.
        var model = ModelFor(asked);
        var resolvedRaw = (model?.GetSymbolInfo(asked).Symbol as ITypeSymbol)
            ?? model?.GetTypeInfo(asked).Type;
        var resolved = resolvedRaw is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } lifted
            ? lifted.TypeArguments[0]
            : resolvedRaw;

        // A type parameter is named as its declaration names it: `@class` is declared `class$`, a name
        // TypeScript takes, and a parameter of that type said `class`, which it does not (#467).
        if (resolved is ITypeParameterSymbol typeParameter)
        {
            var named = typeParameter.Name.ToJsIdentifier();
            return nullable ? OrNull(named) : named;
        }

        // A generic's TYPE ARGUMENTS are symbols here even when the string mapper already rewrote
        // the shape around them (`Action<IPainter>` → `(iPainter: IPainter) => void`). An interface
        // among them must answer the same `any` a bare interface parameter does, or the module
        // names something the runtime can export no value for — an interface has none. Asked of
        // the symbol, so there is no state to keep, evict, or share between compilations.
        //
        // AN ENUM among them needs the same repair for the same reason, and it is the same defect
        // one type kind along: the string mapper leaves the C# spelling, and the runtime exports no
        // `NavigableMove` — it mirrors a vocabulary enum as the `<Enum>Value` union, or as `number`
        // when it is [Flags]. `Action<NavigableMove>` emitted `(navigableMove: NavigableMove) =>
        // void`, which named nothing and failed the emitted module's own type check. It surfaced
        // only when a factory first took a delegate over a vocabulary enum (UI.Navigable, #251):
        // every earlier one took a primitive, and a BARE enum parameter had always gone down the
        // `core` switch below, which has answered this correctly all along.
        // NESTED, not just the outer generic's own arguments: `Action<IReadOnlyList<NavigableMove>>`
        // maps to `NavigableMove[]` before this runs, and the enum is one level further in than a
        // single pass over TypeArguments can see. Walking only the top level left that emitting the
        // C# spelling with no import — the exact defect this block exists to end, one nesting down.
        if (!echoed && resolved is INamedTypeSymbol { TypeArguments.Length: > 0 } generic)
        {
            foreach (var argument in generic.TypeArguments.SelectMany(Nested))
            {
                // An exception and a delegate are the same defect two kinds along (TsStandIn):
                // `Action<Exception>` named an `Exception` no module defines.
                if (TsStandIn.Inside(argument, _converter.UsedRuntimeTypes) is not { } crossesAs) continue;
                mapped = System.Text.RegularExpressions.Regex.Replace(
                    mapped, $@"\b{System.Text.RegularExpressions.Regex.Escape(argument.Name)}\b", crossesAs);
            }
        }

        var core = (echoed ? resolved : null) switch
        {
            // An enum crosses as its member STRING (or the number a [Flags] one combines into), an
            // interface as any, an exception as Error and a delegate as its function: TsStandIn.
            { } kind when TsStandIn.For(kind, _converter.UsedRuntimeTypes) is { } standIn => standIn,
            { TypeKind: TypeKind.TypeParameter } => resolved!.Name,
            // A name nothing here can VERIFY is a name the module may not resolve. Annotating with
            // it trades a missing type for a broken one, so it stays open.
            null when echoed && !Resolvable(mapped) => "any",
            _ => mapped,
        };
        if (!nullable || core == "any") return core;
        return OrNull(core);
    }

    /// <summary>
    /// <paramref name="type"/> or null. A function type is PARENTHESISED before the union, or the
    /// `| null` binds to its RETURN: `(e: Edit) => void | null` says the handler may return null, not
    /// that the handler itself may be absent. Every emitter writes a nullable union through here: the
    /// record path wrote its own, and a record's `Func&lt;int, string&gt;? foldLabel` came out a
    /// function that returns null rather than a function that may be missing.
    /// </summary>
    internal static string OrNull(string type) => type.Contains("=>") ? $"({type}) | null" : $"{type} | null";

    /// <summary>
    /// A type and every type argument BELOW it, to any depth — `IReadOnlyList&lt;NavigableMove&gt;`
    /// yields itself and the enum inside it. What the rewrite above needs, because the string
    /// mapper has already flattened the shape (`NavigableMove[]`) and only the NAME inside it is
    /// still the C# one.
    /// </summary>
    private static IEnumerable<ITypeSymbol> Nested(ITypeSymbol type)
    {
        yield return type;
        if (type is not INamedTypeSymbol { TypeArguments.Length: > 0 } generic) yield break;
        foreach (var argument in generic.TypeArguments)
            foreach (var inner in Nested(argument))
                yield return inner;
    }

    /// <summary>
    /// What a non-flags VOCABULARY enum is called on the other side, or null when the enum is an
    /// app's own, the one question every emission path asks (TsStandIn). The runtime mirrors every
    /// vocabulary enum as a string union named <c>&lt;Enum&gt;Value</c>, and declaring that union
    /// instead of a bare <c>string</c> is what lets a component FORWARD its own enum property into a
    /// vocabulary slot: `string` is wider than the slot, so the twin stopped compiling the first time
    /// a component passed one on (a rail's alignment, straight into a Column's `main`). Comparing
    /// against enum MEMBERS never needed it, which is why it took this long to surface.
    /// <para>
    /// An APP's own enum has no union in the runtime — it crosses as its member string, which is
    /// exactly what it is.
    /// </para>
    /// </summary>
    internal static string? VocabularyUnionFor(ITypeSymbol type) =>
        RuntimeProvidedTypeScanner.IsVocabularyNamespace(type.ContainingNamespace?.ToDisplayString() ?? "")
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
            if (VocabularyEnum(compilation, name) is not null)
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
        var symbol = _semanticModel is null ? null : VocabularyEnum(_semanticModel.Compilation, name);
        if (symbol is null) return "string";
        return IsFlags(symbol) ? "number" : Union(name);
    }

    /// <summary>The union's name, registered so it travels with the module — an annotation naming
    /// something the file never imported is a broken type, not a missing one.</summary>
    private string Union(string enumName)
    {
        _converter.UsedRuntimeTypes.Add($"{enumName}Value");
        return $"{enumName}Value";
    }

    /// <summary>The vocabulary enum called <paramref name="name"/>, looked for in every namespace whose
    /// enums the runtime mirrors as string unions — or null when no vocabulary declares one.</summary>
    private static INamedTypeSymbol? VocabularyEnum(Compilation compilation, string name)
    {
        foreach (var ns in RuntimeProvidedTypeScanner.VocabularyNamespaces)
        {
            if (compilation.GetTypeByMetadataName($"{ns}.{name}") is { TypeKind: TypeKind.Enum } symbol)
                return symbol;
        }
        return null;
    }

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
            // Named as its twin: a namespace in the spelling is no name the module has (#479).
            var candidate = entry.Type.TwinTypeName(_semanticModel);
            var resolved = _semanticModel?.GetSymbolInfo(entry.Type).Symbol as INamedTypeSymbol;
            if (resolved is not null ? resolved.TypeKind == TypeKind.Class : Resolvable(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>Whether the class extends one this compilation EMITS — an interface in the base
    /// list is not a base class, and calling super() for one would call Object's.</summary>
    private bool HasEmittedBase(ClassDeclarationSyntax cls) => BaseClassOf(cls) is not null;

    /// <summary>Whether the per-app scan knows this name became one of the app's OWN modules — the
    /// only kind a <c>./Name</c> import may point at.</summary>
    private bool IsAppModule(string name) => _dependencyResolver?.IsModule(name) == true;

    /// <summary>Whether the per-app scan knows this name at all: one of the app's own modules, or a
    /// type the app declares <c>[RuntimeProvided]</c>, which the runtime exports instead.</summary>
    private bool Resolvable(string name) =>
        IsAppModule(name) || _dependencyResolver?.GetRuntimeProvidedTypes().Contains(name) == true;

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
        builder.Class(name, BaseClassOf(cls), c =>
            {
                EmitStaticMembers(cls, c, asStatic);
                if (!asStatic) EmitInheritedDefaults(cls, c);
            },
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
        var hostOnlyInSignatures = new Dictionary<string, SyntaxNode>();
        var inheritedDefaults = InheritedDeclarations(cls).ToList();
        if (semanticModel != null)
        {
            Services.RuntimeProvidedTypeScanner.Collect(cls, semanticModel, runtimeProvided,
                referencedEnums, appTypes: null, hostOnly: hostOnlyInSignatures);
            // The defaults the class takes from its interfaces (#414) name types the class never does.
            foreach (var inherited in inheritedDefaults)
                if (ModelFor(inherited) is { } inheritedModel)
                    Services.RuntimeProvidedTypeScanner.Collect(inherited, inheritedModel, runtimeProvided,
                        referencedEnums, appTypes: null, hostOnly: hostOnlyInSignatures);
        }
        else if (_dependencyResolver != null)
            runtimeProvided.UnionWith(_dependencyResolver.GetRuntimeProvidedTypes());
        // Names the CONVERSION introduced that the runtime provides — a reduced extension call sent
        // home (`VisualNodeExtensions.centered(node)`). The scanner above walks SYNTAX, and the home
        // appears in none: the call is written on the receiver. `UsedAppTypes` is merged below for
        // the app-declared half of exactly this; this is the runtime-provided half, and without it a
        // helper emitted the qualified call with no import and died on "is not defined" at load.
        // Measured on `public static VisualNode Boxed() => new Text("x").Centered();`.
        runtimeProvided.UnionWith(_converter.UsedRuntimeTypes);
        // A TYPE POSITION is the seventh way to name a host-only symbol and the one no expression
        // strategy can reach: `public Matrix2D Placement { get; init; }` on a component compiled,
        // emitted `import { Matrix2D } from "@equantic/runtime"`, and took the page down at
        // hydration. Measured. The scanner keeps the name out of the import list; this is where it
        // gets said, in the same words the other six use.
        foreach (var (named, at) in hostOnlyInSignatures)
            _converter.Report(at, ConversionSeverity.Error, "EQ2010",
                CodeGen.Extensions.HostOnlySymbolExtensions.Message(named));
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
        // The base class is imported whether or not the syntax scanner noticed it: `extends` is the
        // one reference that must resolve before this module's first statement runs.
        if (baseName is not null) imports.Add(new JsImport([baseName], $"./{baseName}"));
        foreach (var t in CollectComponentTypesFromNode(cls, new HashSet<string> { name })
                     .Concat(inheritedDefaults.SelectMany(inherited => CollectComponentTypesFromNode(inherited, new HashSet<string> { name })))
                     .Concat(_converter.UsedAppTypes) // conversion-introduced names (reduced extension calls)
                     .Distinct().OrderBy(x => x))
        {
            var ct = t.Trim().TrimEnd('?');
            if (ct.Contains('<')) ct = ct.Split('<')[0];
            if (ct.Contains('.')) ct = ct.Substring(ct.LastIndexOf('.') + 1);
            if (string.IsNullOrEmpty(ct) || ct == name || ct == baseName
                || ct == "HtmlNode" || NonImportableTypes.Contains(ct)) continue;
            if (runtimeProvided.Contains(ct) || referencedEnums.Contains(ct)) continue;
            if (IsAppModule(ct)) imports.Add(new JsImport([ct], $"./{ct}"));
        }
        var module = new JsModule(imports, builder.ToString());
        // The class's mappings were recorded against its own text; the imports stand above it.
        builder.ShiftMappings(JsModuleWriter.BodyLine(module));
        return JsModuleWriter.Write(module);
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
            var name = bodyText.Contains(p.Name) ? p.Name.ToJsIdentifier() : "_" + p.Name.ToJsIdentifier();
            var defaultValue = syntaxParameters is { } list && index < list.Count
                ? list[index].Default?.Value
                : null;
            var isRest = syntaxParameters is { } paramList && index < paramList.Count
                && paramList[index].Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ParamsKeyword);
            return Lowering.ParamWithDefault(name, DeclarationType(component, p.Type),
                defaultValue is null ? null : _converter.ConvertExpression(defaultValue), isRest);
        }));
        var methodName = method.Name.ToCamelCase();

        // The lifecycle keeps its own name across the crossing. It used to arrive as `onInit`, from
        // the days when the only base was the legacy page state, and that name is the reason
        // OnMount had never once run on the web: `StatefulComponent` — what every write-once
        // component extends — declares `onMount`, calls it from `notifyMounted`, and has no
        // `onInit` at all, so the override landed on nothing. The legacy `onInit` is also skipped
        // for any page that HYDRATED, which is every server-rendered page there is.
        
        var returnType = Annotate(method.ReturnType ?? "void");
        
        // async is a MODIFIER, not a return type: `async void` handlers (the C# event-handler
        // idiom — hover intent timers et al.) must emit `async` too, or their awaits are syntax
        // errors in the bundle. Task-returning methods keep emitting async either way.
        var isAsync = method.SyntaxNode is { } declaration
            ? Lowering.IsAsync(declaration)
            : method.ReturnType != null && method.ReturnType.StartsWith("Task");
        // An iterator method (yield in its OWN body — nested lambdas/local functions don't count)
        // MATERIALISES: it fills an array and returns it. A JS generator would look right and then
        // read as undefined the moment a LINQ operator touched the result, because every sequence
        // in the emitted world is an array. See ConversionContext.IteratorBuffer.
        var isIterator = method.SyntaxNode?.Body.IsIteratorBody() == true;
        if (isIterator) Lowering.ReportIfEndless(method.SyntaxNode!.Body);
        var asyncPrefix = isAsync ? "async " : "";
        var promiseReturnType = isAsync && returnType == "void" ? "Promise<void>" : 
                                 isAsync ? $"Promise<{returnType}>" : returnType;

        if (method.SyntaxNode != null)
        {
            _converter.SetCurrentClass(className);
            var body = Lowering.Body(method.SyntaxNode.Body, method.SyntaxNode.ExpressionBody?.Expression, isIterator, byReference);
            var generics = method.TypeParameters is { } typeParameters && typeParameters.Any()
                ? $"<{string.Join(", ", typeParameters)}>" : "";
            c.Member(JsClassMember.Method((method.IsStatic ? "static " : "") + asyncPrefix, methodName, generics, parameters,
                TupleReturn(method.SyntaxNode.ReturnType), body), method.SyntaxNode);
        }
        else
        {
            // There is no second way to emit a method. This branch re-parsed `method.Body` as a
            // STRING and called the result "legacy parsing (should happen rarely now)" — measured
            // with a throw in its place across the compiler, web and conformance suites (2,769
            // tests): never once. A parser that hands the emitter a method hands it a syntax node.
            throw new InvalidOperationException(
                $"{className}.{methodName} reached the emitter with no syntax node. Every method "
                + "comes from ComponentParser with one; a null marks a CONSTRUCTOR, which is "
                + "emitted elsewhere. Fix whatever produced this definition rather than re-parsing "
                + "its body as text.");
        }
    }
    
    /// <summary>
    /// The return annotation of a method that returns a TUPLE, and nothing for any other: a tuple
    /// crosses as an array literal, which TypeScript reads as an array of the union of its elements,
    /// so a pair of lists of different things destructured into two lists of either and the
    /// runtime's own build refused every use of them. Every other return is left to inference,
    /// which reads the value it returns right. A nullable tuple may be null. Written the way a
    /// declared type is, which asks what each element IS: an enum among them crosses as its member
    /// string, where the name alone wrote its C# spelling, a type TypeScript does not have.
    /// </summary>
    private string TupleReturn(TypeSyntax returnType) =>
        TypeAnnotations && returnType is TupleTypeSyntax or NullableTypeSyntax { ElementType: TupleTypeSyntax }
            && DeclaredType(returnType) is var annotation && annotation != "any"
            ? $": {annotation}"
            : "";

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
                return Nullable($"[{string.Join(", ", elements)}]" + string.Concat(Enumerable.Repeat("[]", arrayDepth)));
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
            return Nullable(ArrayOf(CSharpTypeToTypeScript(element), arrayDepth));
        }

        if (baseType.StartsWith("Nullable<") && baseType.EndsWith(">"))
        {
            baseType = baseType.Substring(9, baseType.Length - 10);
        }

        string tsType = baseType switch
        {
            "string" or "char" => "string",
            // Every integer that is not 64 bits is a JS number, as a float and a double are. A narrow
            // one reached TypeScript verbatim (`static get top(): byte`), naming nothing there.
            "int" or "double" or "float" or "number" or "uint" or "short" or "ushort" or "byte" or "sbyte"
                or "nint" or "nuint" => "number",
            // NOT `number`, either of them. A long is a JS bigint on this side and a decimal is the
            // runtime's Decimal class — `$eq.num.long(0)` and `$eq.num.dec(0)` are what the emitter
            // writes for their literals, and a `number` annotation over either is a lie the rest of
            // the file then typechecks against: `unit.mul(...)` on a "number" is the shape it takes.
            "long" or "ulong" => "bigint",
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
            tsType = ArrayOf(CSharpTypeToTypeScript(sequenceItem), 1);
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
            // The NAME comes from the type only while the type IS a name. `Action<IReadOnlyList<T>>`
            // otherwise produced `iReadOnlyList<T>` in the parameter position — not an identifier at
            // all, so the emitted module did not parse. `Func<…>` below has always answered `value`
            // rather than deriving anything, which is the same answer asked of the same problem.
            //
            // A trailing `?` is not what makes a type composite: `Action<CodeEdit?>` has always
            // named its parameter `codeEdit` and still should, so nullability comes off before the
            // question is asked. Testing the raw string instead cost that name for nothing.
            var named = itemType.TrimEnd('?');
            var parameter = named.Length > 0
                && named.All(character => char.IsLetterOrDigit(character) || character == '_')
                ? named.ToCamelCase()
                : "value";
            tsType = $"({parameter}: {CSharpTypeToTypeScript(itemType)}) => void";
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
        // A dictionary is the runtime's dictionary class, which no `Record` describes, so every
        // name for one degrades to `any` alike: the interfaces and the sorted ones reached
        // TypeScript verbatim, naming types that exist nowhere there.
        else if (IsDictionaryName(tsType))
        {
            tsType = "any";
        }

        return Nullable(tsType);

        // A NULLABLE C# type is nullable in TypeScript too. The flag was computed and then dropped,
        // so `Action?` annotated as `() => void` and passing the null its own signature invites was
        // a type error. A function type needs the parentheses: `() => void | null` parses as a
        // function RETURNING `void | null`. The tuple and array forms answer early, and answer
        // through here too: `(int, int)?` was annotated as a tuple that is never null.
        string Nullable(string mapped) =>
            !isNullable || mapped is "any" or "void" ? mapped : OrNull(mapped);
    }

    /// <summary>
    /// An array of <paramref name="element"/>, <paramref name="depth"/> deep. TypeScript binds `[]`
    /// tighter than `|` and `=>`, so an element that is a union or a function is parenthesized:
    /// `string | null[]` is a string or an array of nulls, and `() => void[]` a function returning an
    /// array. A nullable element is the common case (<c>List&lt;string?&gt;</c>).
    /// </summary>
    private static string ArrayOf(string element, int depth)
    {
        var bound = element.Contains('|') || element.Contains("=>") ? $"({element})" : element;
        return bound + string.Concat(Enumerable.Repeat("[]", depth));
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

    /// <summary>Every C# name for a dictionary: each is a runtime dictionary class on this side.</summary>
    private static bool IsDictionaryName(string tsType) =>
        Unwrap(tsType, ["Dictionary<", "IDictionary<", "IReadOnlyDictionary<", "SortedDictionary<", "SortedList<"]) is not null;

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
}
