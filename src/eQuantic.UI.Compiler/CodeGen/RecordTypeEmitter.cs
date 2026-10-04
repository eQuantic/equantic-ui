using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Emits a user value type — a record (positional or with a body) or a struct — as a named JS class
/// with full value semantics: a constructor over the type's value members (with per-member defaults),
/// a structural <c>equals</c> (which <c>$eq.equals</c> delegates to automatically), a prototype-
/// preserving <c>with</c>, a .NET-style <c>toString</c>, and the type's user-declared instance methods
/// (the thing a plain-object representation can't carry). Member names are camelCased.
/// </summary>
public class RecordTypeEmitter
{
    private readonly CSharpToJsConverter _converter;
    private readonly Services.ComponentDependencyResolver? _modules;
    private readonly MethodLowering _lowering;

    /// <summary>Whether this emission writes TypeScript: set by <see cref="Emit"/>, read wherever the
    /// variables an expression declares are declared, since <c>let n: any;</c> does not parse as
    /// JavaScript.</summary>
    private bool _annotations;

    /// <param name="converter">The converter the bodies go through.</param>
    /// <param name="modules">The per-app scan, which knows which of the app's own types became
    /// modules. Without one, no app type is imported — the rule the component and class paths hold,
    /// so an import never names a module nobody wrote.</param>
    public RecordTypeEmitter(CSharpToJsConverter converter, Services.ComponentDependencyResolver? modules = null)
    {
        _converter = converter;
        _modules = modules;
        _lowering = new MethodLowering(converter, () => _annotations, ModelFor);
    }

    /// <summary>
    /// True for the value types this emitter handles: every record and every struct, whatever it
    /// declares (#428). What a type declares has nothing to do with whether the browser can name it: a
    /// record that declares only methods, only computed properties, only an indexer, or nothing at all
    /// is still constructed, compared and extended, and the rule that asked for a value member, a static
    /// surface or a base list left `new Animal()` naming a class nothing wrote, and every record over it
    /// unextended. A PARTIAL declaration that declares nothing is the one exception, as it is for a
    /// class (PlainClassModule): another declaration carries the type's members, and the empty one
    /// written first would leave the twin without them.
    /// </summary>
    public static bool CanEmit(TypeDeclarationSyntax type) =>
        type is RecordDeclarationSyntax or StructDeclarationSyntax
        && !(type.Modifiers.Any(SyntaxKind.PartialKeyword) && type.Members.Count == 0 && type.ParameterList is null);

    /// <summary>
    /// Whether this emitter writes a twin for <paramref name="type"/>: declared in source, by a
    /// declaration <see cref="CanEmit"/> accepts. The rule every path that NAMES the twin asks — a
    /// type test (<c>instanceof</c>) and a default (<c>new T()</c>) may only name a class that exists,
    /// and a type declared only by an empty partial declaration has none.
    /// </summary>
    public static bool EmitsTwin(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration && CanEmit(declaration));

    /// <summary>A model that can answer about THIS declaration. Roslyn throws for a node from
    /// another tree, so the COMPILATION is asked for that tree's own model; when even it does not
    /// know the tree, the literal rules still apply.</summary>
    private SemanticModel? ModelFor(SyntaxNode node)
    {
        if (_converter.Model is not { } model) return null;
        if (ReferenceEquals(node.SyntaxTree, model.SyntaxTree)) return model;
        return model.Compilation.ContainsSyntaxTree(node.SyntaxTree)
            ? model.Compilation.GetSemanticModel(node.SyntaxTree)
            : null;
    }

    /// <summary>
    /// The default of a declared type, asked of the SYMBOL where one is available. The syntax-only
    /// fallback cannot see through a name: it answers <c>null</c> for <c>char</c> and for every
    /// enum, where C# gives <c>'\0'</c> and the zero-valued member. A static of either type would
    /// then hold a different value in the twin than on the server, silently. It goes through the
    /// converter, which registers every struct the zero constructs for this module's imports.
    /// </summary>
    private string DefaultOf(TypeSyntax type) =>
        ModelFor(type)?.GetTypeInfo(type).Type is { } symbol
            ? _converter.DefaultOf(symbol)
            : TypeDeclarationExtensions.DefaultFor(type);

    /// <summary>
    /// What the constructor sets a member to, as C# sets it: a positional property the parameter it is
    /// made from, a property's or a field's initializer converted like any expression, and the type's
    /// default where there is none. <paramref name="runsCode"/> says whether evaluating it can do
    /// anything but read a value, which is what has to run before a base's constructor.
    /// <para>
    /// Every initializer runs on every construction (#413). It was each member's PARAMETER default,
    /// which JavaScript evaluates only for an argument that is missing, so an object initializer
    /// setting a member skipped its initializer, and `new Counted { B = 10 }` ran one `++N` where C#
    /// runs two. The constructor takes the C# constructor's parameters and nothing else.
    /// </para>
    /// </summary>
    private string ValueOf(ValueMember member, out bool runsCode)
    {
        runsCode = false;
        switch (member.Declaration)
        {
            case ParameterSyntax parameter:
                return ParameterName(parameter, primary: true);
            case PropertyDeclarationSyntax { Initializer: { } initializer } property:
                runsCode = true;
                return Initialized(initializer.Value, property.Type);
            case PropertyDeclarationSyntax property:
                return DefaultOf(property.Type);
            case VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } variable:
                runsCode = variable.Initializer is not null;
                return variable.Initializer is { } value ? Initialized(value.Value, declaration.Type) : DefaultOf(declaration.Type);
            default:
                return "null";
        }
    }

    /// <summary>An initializer, converted in the constructor's parameter scope: a positional parameter
    /// it reads (`Tag = "#" + Id`) is the parameter itself, since no member is set yet.</summary>
    private string Initialized(ExpressionSyntax initializer, TypeSyntax type) =>
        ExpressionVariableScanner.Scoped(initializer, _converter.WithConstructorParametersInScope(
            () => _converter.ConvertExpression(initializer, type.ToString())), _annotations);

    /// <summary>
    /// The name a constructor parameter is bound under, the one every reference to it is converted to:
    /// a primary constructor's camelCased, as a member it makes, and an explicit constructor's as
    /// written, each made a legal JavaScript name (`class` is `class$`).
    /// </summary>
    private static string ParameterName(ParameterSyntax parameter, bool primary) =>
        (primary ? parameter.Identifier.ValueText.ToCamelCase() : parameter.Identifier.ValueText).ToJsIdentifier();

    /// <summary>Every accessor bodyless and no expression body — an auto-property and nothing else.</summary>
    private static bool IsPureAuto(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is null
        && property.AccessorList is { } list
        && list.Accessors.All(a => a.Body is null && a.ExpressionBody is null);

    /// <summary>
    /// Emits the type as a standalone TypeScript module — the structural <c>equals</c>/<c>with</c> use
    /// <c>$eq</c>, imported from the runtime, and the class is exported so components can import it.
    /// </summary>
    /// <param name="type">The record (or struct) to emit a twin module for.</param>
    /// <param name="tsTypeDeclarations">TypeScript output. FALSE is plain JavaScript, and it has to
    /// reach here: a record emitted with `declare x: string` and `constructor(x: any = null)` is a
    /// TypeScript file, so a consumer that runs the module directly — the playground, which asks
    /// the compiler for plain JS — gets "Unexpected identifier" at import time. That failure is
    /// invisible from the outside: compilation reports success, and the page renders a blank
    /// frame.</param>
    public string EmitModule(TypeDeclarationSyntax type, bool tsTypeDeclarations = true)
    {
        // What the BODIES reference, not just what the members declare: a computed property
        // (`InsertedRange => new CodeRange(new CodePosition(…))`) names types the positional
        // members never mention, and an unimported name is `Cannot find name 'CodePosition'`.
        var runtimeProvided = new HashSet<string> { "$eq" };
        var appTypes = new HashSet<string>();
        if (ModelFor(type) is { } model)
        {
            Services.RuntimeProvidedTypeScanner.Collect(type, model, runtimeProvided, new HashSet<string>(), appTypes);
            // The defaults the type takes from its interfaces (#414) name types the type never does.
            if (model.GetDeclaredSymbol(type) is INamedTypeSymbol self)
                foreach (var inherited in DefaultInterfaceMembers.Of(self, model.Compilation))
                    if (inherited.Declaration is { } declaration && ModelFor(declaration) is { } inheritedModel)
                        Services.RuntimeProvidedTypeScanner.Collect(declaration, inheritedModel, runtimeProvided,
                            new HashSet<string>(), appTypes);
        }

        // What the hydration map names, split by where it comes from: this compilation's own twins
        // are sibling modules, and the vocabulary's (`of: Rect`) join the runtime import.
        var specReferences = new HashSet<string>();
        var specRuntime = new HashSet<string>();
        var declared = ModelFor(type)?.GetDeclaredSymbol(type) as INamedTypeSymbol;
        if (declared is not null) HydrationSpec.Members(declared, specReferences, specRuntime);
        runtimeProvided.UnionWith(specRuntime);

        var body = Emit(type, tsTypeDeclarations);
        // Names the CONVERSION introduced, which is why this reads AFTER `Emit`: a reduced extension
        // call sent home (`VisualNodeExtensions.centered(node)`) is written on the RECEIVER, so the
        // home's name appears in no syntax the scanner above walks. The component and static-helper
        // paths merge the same two sets for the same reason; this one did not, and measured on
        // `public VisualNode Boxed() => new Text(Title).Centered();` it emitted
        // `import { $eq, Text }` beside `VisualNodeExtensions.centered(...)` — a qualified call to a
        // name the module never imports, which fails at LOAD rather than at the call.
        runtimeProvided.UnionWith(_converter.UsedRuntimeTypes);
        // The one runtime name the TRANSLATION invents (`decimal` is `Decimal` on the other side), so
        // no scan of the C# can see it: a member or a tuple return typed decimal named a class the
        // module never imported, and the runtime's own build refused it. It is a candidate like the
        // rest, kept only where the emitted text names it (TypeScriptEmitter.Annotate is the class
        // path's twin).
        runtimeProvided.Add(TypeScriptEmitter.Decimal);
        // A vocabulary enum member is annotated with its UNION, a name that exists only in the
        // emitted TypeScript — the scanner above walks C# syntax and could never have seen it.
        TypeScriptEmitter.SeedEnumUnions(body, ModelFor(type)?.Compilation, runtimeProvided);
        // A module declares its own name and never imports it, whoever added it: struck here, after
        // every set above has been merged in. It was struck right after the scan, and the names the
        // conversion introduced put it back: a runtime-provided record calling its own static helper
        // (`CodeDiffLayout.addGaps(…)`) imported itself, which TypeScript refuses as a conflict.
        runtimeProvided.Remove(type.Identifier.Text);
        // Only what the emitted text actually NAMES: a type mentioned in the C# and erased on the
        // way out (an interface, an enum) would otherwise import a name nothing uses, which the
        // runtime's own build rejects.
        // Lookarounds, not `\b`: `$eq` starts with a non-word character, so a word boundary never
        // matches it and the one import every module needs would be the first to go.
        bool Names(string name) => System.Text.RegularExpressions.Regex.IsMatch(body,
            $@"(?<![\w$]){System.Text.RegularExpressions.Regex.Escape(name)}(?![\w$])");
        var used = runtimeProvided
            .Where(Names)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var imports = new StringBuilder(
            $"import {{ {string.Join(", ", used)} }} from \"@equantic/runtime\";\n");
        // A base record is emitted as its own module — import it so `extends` resolves.
        var (baseName, _) = BaseInfo(type);
        if (baseName != null) imports.Append($"import {{ {baseName} }} from \"./{baseName}\";\n");
        // Records the hydration map references by NAME (`price: Money`) are their own modules too;
        // the map is the only place the emitted JS names them (types erase), so import them here.
        if (declared is not null)
        {
            // The APP-declared half of the same thing: an extension home the app itself owns is its
            // own module, and the call names it without ever mentioning it in the C#.
            foreach (var introduced in _converter.UsedAppTypes)
                if (Names(introduced))
                    specReferences.Add(introduced);
            // And the app's own types the BODY names: a sibling record a method constructs, and a
            // struct member's zero (`span: any = new Span2()`), which the constructor writes for a
            // member that has no default of its own. This path imported only what the hydration map
            // named, so both were a name the module never imported — `new Holder()` on the web threw
            // where C# built a zeroed Span2.
            foreach (var appType in appTypes)
                if (_modules?.IsModule(appType) == true && Names(appType))
                    specReferences.Add(appType);
            specReferences.Remove(type.Identifier.Text);
            if (baseName != null) specReferences.Remove(baseName);
            // A reference the RUNTIME provides is imported from there already, and a second import
            // of the same name from a sibling module is a duplicate identifier. Latent until a
            // runtime-provided record first needed a hydration spec: `BarRect`'s floats hydrate as
            // singles, so `BarChartGeometry`'s map names it — and the runtime ships both.
            specReferences.ExceptWith(used);
            foreach (var reference in specReferences.OrderBy(n => n, StringComparer.Ordinal))
                imports.Append($"import {{ {reference} }} from \"./{reference}\";\n");
        }
        return imports.Append("\nexport ").Append(body).Append('\n').ToString();
    }

    /// <summary>
    /// The C# constructor the twin's own one IS (#413): a record's primary constructor, or the one
    /// explicit constructor that runs a body of its own, or the implicit parameterless one. The others
    /// chain to it with `: this(…)`, and reach it through <see cref="ChainedOverloads"/>.
    /// </summary>
    private readonly record struct MainConstructor(IReadOnlyList<ParameterSyntax> Parameters, bool Primary,
        ConstructorDeclarationSyntax? Explicit, IReadOnlyList<ConstructorDeclarationSyntax> Alternates);

    /// <summary>
    /// The constructor <see cref="MainConstructor"/> describes. JavaScript has ONE constructor, so a
    /// type whose constructors each run a body of their own has no twin that can be all of them: the
    /// widest is the twin's, and every other is refused (EQ1009) rather than dropped, which is what
    /// happened to every explicit constructor of a record or a struct before.
    /// </summary>
    private MainConstructor MainOf(TypeDeclarationSyntax type)
    {
        var declared = type.Members.OfType<ConstructorDeclarationSyntax>()
            .Where(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();
        if (type.ParameterList is { } primary)
            return new MainConstructor(primary.Parameters.ToList(), true, null, declared);

        var bodies = declared.Where(constructor => !Chains(constructor)).ToList();
        if (bodies.Count == 0) return new MainConstructor([], false, null, declared);
        var main = bodies.OrderByDescending(constructor => constructor.ParameterList.Parameters.Count).First();
        foreach (var other in bodies.Where(other => other != main))
            _converter.Report(other, ConversionSeverity.Error, "EQ1009",
                $"'{type.Identifier.Text}{other.ParameterList}' runs a body of its own, and so does "
                + $"'{type.Identifier.Text}{main.ParameterList}'. A JavaScript class has one constructor, so the "
                + "twin's is the widest, and reaches another only when it chains to it with `: this(…)`. "
                + "Chain this one to it, or keep one constructor.");
        return new MainConstructor(main.ParameterList.Parameters.ToList(), false, main,
            declared.Where(constructor => constructor != main && Chains(constructor)).ToList());
    }

    /// <summary>Whether a constructor hands its work to another with `: this(…)`.</summary>
    private static bool Chains(ConstructorDeclarationSyntax constructor) =>
        constructor.Initializer?.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) == true;

    /// <summary>
    /// The OTHER constructors — `CodeRange(CodePosition caret) : this(caret, caret)`. JavaScript has
    /// one constructor, so each alternate becomes a branch on how many arguments actually arrived:
    /// bind its own parameters from the arguments, evaluate the `: this(…)` arguments, and land those
    /// in the main constructor's. Its own body runs after the main one's, as C# runs it
    /// (<see cref="AlternateBodies"/>).
    /// <para>
    /// Dropping them is what used to happen, and it was silent: `new CodeRange(caret)` left the
    /// second member NULL, so every read of it threw somewhere far away from the constructor. An
    /// alternate the branch cannot tell from the main one, because the main one takes that many
    /// arguments too, or one that chains to another alternate, is refused (EQ1009).
    /// </para>
    /// </summary>
    private string ChainedOverloads(TypeDeclarationSyntax type, MainConstructor main)
    {
        var slots = main.Parameters.Select(parameter => ParameterName(parameter, main.Primary)).ToList();
        var required = main.Parameters.Count(parameter => parameter.Default is null
            && !parameter.Modifiers.Any(SyntaxKind.ParamsKeyword));
        var sb = new StringBuilder();
        foreach (var ctor in main.Alternates)
        {
            if (ctor.Initializer is not { } chain || !chain.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword)) continue;
            var arity = ctor.ParameterList.Parameters.Count;
            if (arity >= required && arity <= slots.Count
                || ModelFor(chain)?.GetSymbolInfo(chain).Symbol is IMethodSymbol target
                    && target.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is ConstructorDeclarationSyntax other
                    && other != main.Explicit)
            {
                _converter.Report(ctor, ConversionSeverity.Error, "EQ1009",
                    $"'{type.Identifier.Text}{ctor.ParameterList}' chains to another constructor, and the twin's one "
                    + "constructor reaches it by how many arguments arrive: it must chain to the constructor the twin "
                    + $"takes, with fewer or more arguments than that one accepts ({required} to {slots.Count}).");
                continue;
            }

            _converter.SetCurrentClass(type.Identifier.Text);
            // The `: this(…)` arguments, computed in a scope of the alternate's own, where its
            // parameters are the arguments that arrived, and landed in the main constructor's slots
            // all at once: an argument that reads a slot it also writes sees the value that arrived.
            // A block of its own would have bound `const a = a`, a parameter named like a slot.
            var landed = new List<(string Slot, string Value)>();
            for (var i = 0; i < chain.ArgumentList.Arguments.Count; i++)
            {
                var argument = chain.ArgumentList.Arguments[i];
                var ordinal = argument.NameColon is { } named
                    ? main.Parameters.ToList().FindIndex(parameter => parameter.Identifier.ValueText == named.Name.Identifier.ValueText)
                    : i;
                if (ordinal >= 0 && ordinal < slots.Count)
                    landed.Add((slots[ordinal], _converter.ConvertExpression(argument.Expression)));
            }
            sb.Append($"if (arguments.length === {arity}) {{ [{string.Join(", ", landed.Select(l => l.Slot))}] = "
                + $"(({Own(ctor)}) => [{string.Join(", ", landed.Select(l => l.Value))}])({Arrived(ctor)}); }} ");
        }
        return sb.ToString();
    }

    /// <summary>An alternate's own parameters, as an arrow binds them.</summary>
    private string Own(ConstructorDeclarationSyntax alternate) =>
        string.Join(", ", alternate.ParameterList.Parameters.Select(parameter =>
            _lowering.Param(ParameterName(parameter, primary: false), "any")));

    /// <summary>The arguments that arrived, in an alternate's parameters' places.</summary>
    private static string Arrived(ConstructorDeclarationSyntax alternate) =>
        string.Join(", ", alternate.ParameterList.Parameters.Select((_, i) => $"arguments[{i}]"));

    /// <summary>The body each alternate runs after the main constructor's, as C# runs a constructor
    /// that chains with `: this(…)`, with its own parameters bound to the arguments that arrived.</summary>
    private string AlternateBodies(TypeDeclarationSyntax type, MainConstructor main)
    {
        var sb = new StringBuilder();
        foreach (var ctor in main.Alternates)
        {
            if (Body(ctor) is not { Length: > 0 } body) continue;
            // An arrow of its own: its parameters are the arguments that arrived, its `this` the
            // constructor's, and a `return` in it ends it alone, as it ends that constructor in C#.
            sb.Append($"if (arguments.length === {ctor.ParameterList.Parameters.Count}) {{ "
                + $"(({Own(ctor)}) => {{ {body}}})({Arrived(ctor)}); }} ");
        }
        return sb.ToString();
    }

    /// <summary>The statements of the type's static constructor, which run after its static
    /// initializers (<see cref="TypeInitializer"/>); none when it declares none.</summary>
    private IReadOnlyList<JsStatement> StaticConstructorBody(TypeDeclarationSyntax type)
    {
        if (type.Members.OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword)) is not { } cctor)
            return [];
        if (cctor.Body is { } block && _converter.ConvertBlockIr(block) is JsBlock converted) return converted.Statements;
        return cctor.ExpressionBody is { } arrow ? [_lowering.ExpressionBody(arrow.Expression, returns: false)] : [];
    }

    /// <summary>A constructor's own statements in the one-line layout, or nothing for an empty body.</summary>
    private string Body(ConstructorDeclarationSyntax constructor)
    {
        if (constructor.Body is { Statements.Count: > 0 } block
            && _converter.ConvertBlockIr(block) is JsBlock converted)
            return string.Concat(converted.Statements.Select(statement => JsStatementWriter.Write(statement, JsLayout.Compact) + " "));
        if (constructor.ExpressionBody is { } arrow)
            return JsStatementWriter.Write(_lowering.ExpressionBody(arrow.Expression, returns: false), JsLayout.Compact) + " ";
        return "";
    }

    /// <summary>
    /// The twin's constructor (#413). It takes the C# constructor's parameters, never a member's
    /// value, sets every member as C# does, in declaration order, and runs the constructor's body
    /// after them; an object initializer is applied by the construction site once it returns
    /// (<see cref="Strategies.Expressions.ObjectInitializer"/>). Over a base, C# runs the derived
    /// type's initializers BEFORE the base constructor, and JavaScript cannot touch `this` before
    /// `super()`, so each initializer that can do anything is evaluated into a local first and
    /// assigned after.
    /// </summary>
    private string Constructor(TypeDeclarationSyntax type, IReadOnlyList<ValueMember> members, string? baseName, string superArgs)
    {
        var main = MainOf(type);
        _converter.SetCurrentClass(type.Identifier.Text);
        var parameters = main.Parameters.Select(parameter => _lowering.ParamWithDefault(
            ParameterName(parameter, main.Primary), "any",
            parameter.Default is { } given
                ? _converter.ConvertExpression(given.Value, parameter.Type?.ToString())
                : main.Primary && parameter.Type is { } typed ? DefaultOf(typed) : null,
            parameter.Modifiers.Any(SyntaxKind.ParamsKeyword)));

        var sb = new StringBuilder($"constructor({string.Join(", ", parameters)}) {{ ");
        sb.Append(ChainedOverloads(type, main));
        var values = members.Select(member => (member, Value: ValueOf(member, out var runsCode), runsCode)).ToList();
        if (baseName is not null)
        {
            foreach (var (member, value, runsCode) in values)
                if (runsCode) sb.Append($"const ${member.Js} = {value}; ");
            var arguments = main.Explicit?.Initializer is { } chain && chain.ThisOrBaseKeyword.IsKind(SyntaxKind.BaseKeyword)
                ? string.Join(", ", chain.ArgumentList.Arguments.Select(argument => _converter.ConvertExpression(argument.Expression)))
                : superArgs;
            sb.Append($"super({arguments}); ");
            foreach (var (member, value, runsCode) in values)
                sb.Append($"this.{member.Js} = {(runsCode ? "$" + member.Js : value)}; ");
        }
        else
        {
            foreach (var (member, value, _) in values)
                sb.Append($"this.{member.Js} = {value}; ");
        }
        if (main.Explicit is { } own) sb.Append(Body(own));
        sb.Append(AlternateBodies(type, main));
        return sb.Append("} ").ToString();
    }

    /// <summary>The TS annotation for a declared type, resolved the way the class emitter does it:
    /// an enum is its member string and an interface has no emitted twin to name.</summary>
    private string TsTypeOf(TypeSyntax? type)
    {
        if (type is null) return "any";
        var resolved = ModelFor(type)?.GetTypeInfo(type).Type;
        return resolved switch
        {
            { TypeKind: TypeKind.Enum } => "string",
            { TypeKind: TypeKind.Interface } => "any",
            _ => TypeDeclarationExtensions.TsTypeFor(type, ModelFor(type)),
        };
    }

    /// <param name="type">The record/struct declaration to emit.</param>
    /// <param name="tsTypeDeclarations">Emit the TYPE-ONLY <c>declare</c> member declarations. They are
    /// TypeScript syntax (no runtime code — they restore checking on <c>record.x</c> for the .ts module
    /// path) and MUST stay off for plain-JS consumers: the conformance harness executes the emitted
    /// class as <c>.mjs</c>, where <c>declare</c> is a parse error.</param>
    public string Emit(TypeDeclarationSyntax type, bool tsTypeDeclarations = false)
    {
        _converter.EmitTypeAnnotations(tsTypeDeclarations);
        _annotations = tsTypeDeclarations;
        var name = type.Identifier.Text;
        var members = type.ValueMembers(ModelFor(type));
        var (baseName, superArgs) = BaseInfo(type);

        var sb = new StringBuilder();
        sb.Append($"class {name}{(baseName != null ? $" extends {baseName}" : "")} {{ ");

        // TYPE-ONLY member declarations: they restore checking on `record.x` without emitting any runtime
        // code (the constructor below does the assigning). Only OWN members are declared — a positional
        // parameter its base already has a property for is the base's, declared by the base module.
        if (tsTypeDeclarations)
            foreach (var m in members)
                sb.Append($"declare {m.Js}: {m.TsType}; ");

        sb.Append(Constructor(type, members, baseName, superArgs));

        // VALUE semantics belong to records and structs. A plain class is IDENTITY: giving it a
        // structural `equals` would make two different buckets compare equal, and a `with` would
        // hand back a copy where the caller expects the same object.
        if (type is RecordDeclarationSyntax or StructDeclarationSyntax)
        {
            // Structural equality — $eq.equals(a, b) delegates here when `a` is an instance. A record
            // compares as C# compares it: the same runtime type (its EqualityContract), what its base
            // compares, then its own members. `o instanceof Animal` alone made a Dog equal to an Animal
            // with the same members, once a record could extend one that declares no value (#428).
            // Param annotations are TS-only (the plain-JS path must stay parseable as .mjs).
            sb.Append(tsTypeDeclarations ? $"equals(o: unknown) {{ return o instanceof {name}"
                : $"equals(o) {{ return o instanceof {name}");
            if (baseName != null) sb.Append(" && super.equals(o)");
            else if (type is RecordDeclarationSyntax) sb.Append(" && o.constructor === this.constructor");
            foreach (var m in members) sb.Append($" && $eq.equals(this.{m.Js}, o.{m.Js})");
            sb.Append("; } ");

            // with(patch): a COPY, onto the prototype (a spread would drop the methods), then the
            // members the patch names. C# copies the fields and runs no initializer, and building it
            // through the constructor ran every one of them again (#413).
            sb.Append(tsTypeDeclarations ? $"with(patch: any): {name} {{ return {Eq.With}(this, patch); }} "
                : $"with(patch) {{ return {Eq.With}(this, patch); }} ");

            // The zero C# gives a struct whose constructor does more than zero it: `default(S)`, an
            // array's slot, an OrDefault. Built without the constructor, which would run it.
            if (IsStruct(type) && ZeroRunsCode(type))
            {
                var zeros = string.Join(", ", members.Select(m => $"{m.Js}: {ZeroOf(m)}"));
                sb.Append(tsTypeDeclarations
                    ? $"static $zero(): {name} {{ return Object.assign(Object.create({name}.prototype), {{ {zeros} }}); }} "
                    : $"static $zero() {{ return Object.assign(Object.create({name}.prototype), {{ {zeros} }}); }} ");
            }

            // The twin's own TYPED BOUNDARY: which members hydrate off the wire, and as what —
            // `$eq.hydrate` rebuilds a payload object on this prototype and coerces by this map.
            if (ModelFor(type)?.GetDeclaredSymbol(type) is INamedTypeSymbol symbol
                && HydrationSpec.Members(symbol, new HashSet<string>(), new HashSet<string>()) is { } hydration)
                // A getter, for the reason TypeScriptEmitter's map is one: a static initializer
                // naming another class runs before an import cycle has defined it.
                sb.Append($"static get $hydration() {{ return {hydration}; }} ");
        }

        // User-declared methods — a STATIC one keeps its modifier: a record's factory
        // (`LegalBlock.P(…)`, `Money.Zero()`) is called on the CLASS, and emitting it as an
        // instance method turns every call site into "X.p is not a function" at hydration.
        var userToString = false;
        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.Text == "ToString") userToString = true;
            sb.Append(EmitMethod(method, name));
        }

        // An INSTANCE INDEXER, as the methods every element access bound to it calls (#427). It was
        // written into no twin, and `new Grid()[3]` read a property named "3" that nothing had.
        foreach (var indexer in type.Members.OfType<IndexerDeclarationSyntax>())
        {
            _converter.SetCurrentClass(name);
            foreach (var member in _lowering.Indexer(indexer, TsTypeOf)) sb.Append(Written(member));
        }

        // OPERATOR overloads. JavaScript cannot overload `+`, so the operator becomes a static
        // method and the call site is rewritten to call it — dropping it silently made `a + b` on
        // two objects concatenate their toString()s, which is wrong output with nothing to see.
        foreach (var op in type.Members.OfType<OperatorDeclarationSyntax>())
        {
            var opName = op.ParameterList.Parameters.Count == 1
                ? UnaryOperatorMethodName(op.OperatorToken.Text)
                : OperatorMethodName(op.OperatorToken.Text);
            if (opName is null) continue;
            var pars = string.Join(", ", op.ParameterList.Parameters
                .Select(p => _lowering.Param(p.Identifier.Text.ToJsIdentifier(), TsTypeOf(p.Type))));
            // The body is lowered as a method's (#432): an `out var` inside it is declared in front,
            // and so is a variable an expression body's pattern binds, where an ES module is strict.
            sb.Append(StaticMember(opName, pars, op.Body, op.ExpressionBody));
        }

        // CONVERSION operators — `implicit operator Money(int v)`, `explicit operator int(Money m)`.
        // A static method named for the direction: `Money.fromInt(5)`, `Money.toInt(m)`. The call
        // sites reach it through the bound tree (ValueFlow for an implicit one, the cast strategy
        // for an explicit one); without it the value crossed RAW, an int where a Money was expected.
        foreach (var conversion in type.Members.OfType<ConversionOperatorDeclarationSyntax>())
        {
            var parameter = conversion.ParameterList.Parameters[0];
            // The NAME comes from the symbol wherever there is one, through the same function the
            // call site uses. The two used to compute it apart — this side compared type SYNTAX
            // (`conversion.Type.ToString() == name`) and the call site compared SYMBOLS — so a
            // conversion written with a qualified name emitted `toMoney` and was called as
            // `fromInt`. Undefined at runtime, with a green build.
            var opName = ModelFor(conversion)?.GetDeclaredSymbol(conversion) is { } symbol
                ? ConversionNameFor(symbol)
                : ConversionMethodName(
                    conversion.Type.ToString() == name ? parameter.Type!.ToString() : conversion.Type.ToString(),
                    from: conversion.Type.ToString() == name);
            var par = _lowering.Param(parameter.Identifier.Text.ToJsIdentifier(), TsTypeOf(parameter.Type));
            sb.Append(StaticMember(opName, par, conversion.Body, conversion.ExpressionBody));
        }

        // Static FIELDS — `public static readonly CodePosition Start = new(0, 0);`. The other half of
        // the "well-known value" idiom, and nothing emitted them: `CodePosition.start` was
        // undefined, so every comparison against the origin silently failed.
        // ONE pass, in SOURCE ORDER, over both spellings of a static value: the field
        // (`static readonly T F = …`) and the auto-property (`static T P { get; } = …`). Two passes
        // would initialise every field before every property whatever the source said, and C# runs
        // static initialisers in declaration order — `static A = B;` written above `static B = 1;`
        // reads B's DEFAULT in .NET, and would have read 1 here.
        //
        // Absent an initialiser the member takes its TYPE's default, not `undefined`: C# gives
        // `static int Count { get; set; }` a 0, and a twin answering undefined disagrees with the
        // server about a number.
        //
        // And when one of them can observe another (an initializer that is not a constant, or a static
        // constructor), every static starts at its zero and the initializers run in declaration
        // order, on first use (TypeInitializer, #417): written in place, `static first = new Early()`
        // ran Early's constructor before `static seed = 3` was defined, and read NaN.
        var ordered = TypeInitializer.Orders(type, ModelFor);
        var initialized = new List<TypeInitializer.Ordered>();
        foreach (var member in type.Members)
        {
            _converter.SetCurrentClass(name);
            switch (member)
            {
                case FieldDeclarationSyntax field when field.Modifiers.Any(m =>
                        m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)):
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var fieldValue = variable.Initializer is { } init
                            ? ExpressionVariableScanner.Scoped(init.Value,
                                _converter.ConvertExpression(init.Value, field.Declaration.Type.ToString()), _annotations)
                            : DefaultOf(field.Declaration.Type);
                        if (ordered && !field.Modifiers.Any(SyntaxKind.ConstKeyword))
                            initialized.Add(new(variable.Identifier.Text.ToCamelCase(), TsTypeOf(field.Declaration.Type),
                                DefaultOf(field.Declaration.Type), variable.Initializer is null ? null : fieldValue, variable));
                        else
                            sb.Append($"static {variable.Identifier.Text.ToCamelCase()} = {fieldValue}; ");
                    }
                    break;

                // A PURE auto-property only — every accessor bodyless, no expression body. One with a
                // custom setter is behaviour, and emitting it as a plain field would silently throw
                // that behaviour away; it stays unemitted, as it was before, rather than emitted wrong.
                case PropertyDeclarationSyntax prop
                    when prop.Modifiers.Any(SyntaxKind.StaticKeyword) && IsPureAuto(prop):
                    var propValue = prop.Initializer is { } propInit
                        ? ExpressionVariableScanner.Scoped(propInit.Value,
                            _converter.ConvertExpression(propInit.Value, prop.Type.ToString()), _annotations)
                        : DefaultOf(prop.Type);
                    if (ordered)
                        initialized.Add(new(prop.Identifier.Text.ToCamelCase(), TsTypeOf(prop.Type), DefaultOf(prop.Type),
                            prop.Initializer is null ? null : propValue, prop));
                    else
                        sb.Append($"static {prop.Identifier.Text.ToCamelCase()} = {propValue}; ");
                    break;

                // A static property that guards its own store with `field`: the store, named as the
                // accessors name it, in declaration order with the other statics. Its accessors come
                // with the properties below. It was neither, so the type had no such property (#483).
                case PropertyDeclarationSyntax backed
                    when backed.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && Strategies.Expressions.FieldExpressionStrategy.UsesBackingField(backed):
                    var slotValue = backed.Initializer is { } slotInit
                        ? ExpressionVariableScanner.Scoped(slotInit.Value,
                            _converter.ConvertExpression(slotInit.Value, backed.Type.ToString()), _annotations)
                        : DefaultOf(backed.Type);
                    if (ordered)
                        initialized.Add(new(Strategies.Expressions.FieldExpressionStrategy.BackingSlot(backed), TsTypeOf(backed.Type),
                            DefaultOf(backed.Type), backed.Initializer is null ? null : slotValue, backed));
                    else
                        sb.Append($"static {Strategies.Expressions.FieldExpressionStrategy.BackingSlot(backed)} = {slotValue}; ");
                    break;
            }
        }
        if (ordered)
        {
            _converter.SetCurrentClass(name);
            foreach (var member in TypeInitializer.Members(name, initialized, StaticConstructorBody(type), _annotations))
                sb.Append(Written(member));
        }

        // PROPERTIES with a body — computed, on the instance (`Start => Anchor <= Focus ? … : …`)
        // or static (`static Foo Empty => …`, the factory idiom). A record is a value with
        // BEHAVIOUR; emitting only its positional members threw the behaviour away.
        foreach (var property in type.Members.OfType<PropertyDeclarationSyntax>())
            sb.Append(ComputedProperty(property, name));

        // The DEFAULT INTERFACE MEMBERS the type relies on (#414): JavaScript has no interface to
        // hold them, so a record or a struct that did not declare one had no such member at all.
        // Each body converts under its interface's file, where its names resolve.
        if (ModelFor(type) is { } typeModel && typeModel.GetDeclaredSymbol(type) is INamedTypeSymbol self)
        {
            foreach (var (implementation, member, _) in DefaultInterfaceMembers.Of(self, typeModel.Compilation))
            {
                if (member is not null && ModelFor(member) is { } memberModel
                    && DefaultInterfaceMembers.InterfaceStaticIn(member, memberModel) is { } reached)
                {
                    _converter.Report(type, ConversionSeverity.Error, "EQ1008",
                        DefaultInterfaceMembers.Homeless(self, implementation, reached));
                    continue;
                }
                switch (member)
                {
                    case MethodDeclarationSyntax method when method.Body != null || method.ExpressionBody != null:
                        _converter.InFileOf(method, () => sb.Append(EmitMethod(method, name)));
                        break;
                    case PropertyDeclarationSyntax property when ComputedGetter(property) is not null || IsSetterOnly(property):
                        _converter.InFileOf(property, () => sb.Append(ComputedProperty(property, name)));
                        break;
                    // A default indexer, as the type's own is written (#427).
                    case IndexerDeclarationSyntax indexer:
                        _converter.InFileOf(indexer, () =>
                        {
                            _converter.SetCurrentClass(name);
                            foreach (var lowered in _lowering.Indexer(indexer, TsTypeOf)) sb.Append(Written(lowered));
                        });
                        break;
                    // A vocabulary default from the interface's assembly, with no body to convert:
                    // the twin delegates to the runtime's copy.
                    case null when DefaultInterfaceMembers.RuntimeCarries(implementation.ContainingType):
                        var (delegatedName, parameters, call) = DefaultInterfaceMembers.Delegation(implementation);
                        _converter.UsedRuntimeTypes.Add(implementation.ContainingType.Name);
                        sb.Append(implementation is IMethodSymbol
                            ? $"{delegatedName}({string.Join(", ", parameters.Select(p => tsTypeDeclarations ? $"{p}: any" : p))}) {{ return {call}; }} "
                            : $"get {delegatedName}() {{ return {call}; }} ");
                        break;
                    default:
                        _converter.Report(type, ConversionSeverity.Error, "EQ1008",
                            DefaultInterfaceMembers.Unreadable(self, implementation));
                        break;
                }
            }
        }

        // .NET record ToString ("Name { X = …, Y = … }") unless the user overrode it: the members
        // PrintMembers writes, a base's first, each once (#546), and `Name { }` for none, as .NET
        // writes it, where the twin wrote two spaces.
        if (!userToString)
        {
            var printed = Printed(type).ToList();
            var inner = printed.Count == 0
                ? ""
                : string.Join(", ", printed.Select(m => $"{m.Display} = ${{this.{m.Js}}}")) + " ";
            sb.Append($"toString() {{ return `{name} {{ {inner}}}`; }} ");
        }

        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// The members a record's text prints, as PrintMembers writes them: its base's first, then its
    /// own, the positional properties it makes in its parameters' order and then the public instance
    /// fields and readable properties of its body in declaration order, a computed one included.
    /// A struct prints its own the same way.
    /// </summary>
    private IEnumerable<(string Display, string Js)> Printed(TypeDeclarationSyntax type)
    {
        if (type is RecordDeclarationSyntax
            && ModelFor(type)?.GetDeclaredSymbol(type) is INamedTypeSymbol { BaseType: { } parent }
            && parent.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
                .OfType<TypeDeclarationSyntax>().FirstOrDefault(CanEmit) is { } baseDeclaration)
        {
            foreach (var inherited in Printed(baseDeclaration)) yield return inherited;
        }
        foreach (var member in type.ValueMembers(ModelFor(type)))
            if (member is { Printed: true, Declaration: ParameterSyntax }) yield return (member.Display, member.Js);
        foreach (var member in type.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field when field.Modifiers.Any(SyntaxKind.PublicKeyword)
                    && !field.Modifiers.Any(SyntaxKind.StaticKeyword) && !field.Modifiers.Any(SyntaxKind.ConstKeyword):
                    foreach (var variable in field.Declaration.Variables)
                        yield return (variable.Identifier.ValueText, variable.Identifier.ValueText.ToCamelCase());
                    break;
                case PropertyDeclarationSyntax property when property.Modifiers.Any(SyntaxKind.PublicKeyword)
                    && !property.Modifiers.Any(SyntaxKind.StaticKeyword) && PubliclyReadable(property):
                    yield return (property.Identifier.ValueText, property.Identifier.ValueText.ToCamelCase());
                    break;
            }
        }
    }

    /// <summary>Whether a public property can be read from outside: an expression body, or a getter
    /// with no accessibility of its own.</summary>
    private static bool PubliclyReadable(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is not null
        || property.AccessorList?.Accessors.Any(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
            && accessor.Modifiers.Count == 0) == true;

    /// <summary>A struct, or a record struct, whose declaration is a record's.</summary>
    internal static bool IsStruct(TypeDeclarationSyntax type) =>
        type is StructDeclarationSyntax
        || type is RecordDeclarationSyntax record && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword);

    /// <summary>A member's zero, the value <c>default</c> gives it.</summary>
    private string ZeroOf(ValueMember member) => member.Declaration switch
    {
        ParameterSyntax { Type: { } type } => DefaultOf(type),
        PropertyDeclarationSyntax property => DefaultOf(property.Type),
        VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } => DefaultOf(declaration.Type),
        _ => "null",
    };

    /// <summary>
    /// Whether the twin's constructor, called with no argument, does more than zero a struct: a
    /// positional parameter's default, an instance initializer, or a constructor body it runs (an
    /// explicit parameterless constructor, or the one explicit constructor of a struct that has no
    /// primary one). C# runs none of it for <c>default(S)</c>, an array's slot or an OrDefault, and
    /// for <c>new S()</c> where S declares no parameterless constructor, so such a struct's zero is
    /// built without the constructor (<c>S.$zero()</c>), and every other's is <c>new S()</c>. One rule,
    /// read by the emitter that writes <c>$zero</c> and by <see cref="Strategies.DefaultValue"/>, which
    /// names it.
    /// </summary>
    internal static bool ZeroRunsCode(TypeDeclarationSyntax declaration) =>
        declaration.ParameterList?.Parameters.Any(parameter => parameter.Default is not null) == true
        || declaration.Members.Any(member => member switch
        {
            FieldDeclarationSyntax field => !field.Modifiers.Any(SyntaxKind.StaticKeyword)
                && !field.Modifiers.Any(SyntaxKind.ConstKeyword)
                && field.Declaration.Variables.Any(variable => variable.Initializer is not null),
            PropertyDeclarationSyntax property => !property.Modifiers.Any(SyntaxKind.StaticKeyword)
                && property.Initializer is not null,
            ConstructorDeclarationSyntax constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
                && (constructor.ParameterList.Parameters.Count == 0 || declaration.ParameterList is null),
            _ => false,
        });

    /// <summary>
    /// A property with a setter body and no getter at all (<c>int Twice { set => Stored = value * 2; }</c>),
    /// which a twin writes as a setter alone. It was dropped with every property whose getter had no
    /// body, a default interface member included (found in review, #418). A property with an
    /// automatic getter and a setter body is not one: its getter reads a backing field the value
    /// members hold.
    /// </summary>
    private static bool IsSetterOnly(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is null
        && property.AccessorList?.Accessors is { } accessors
        && accessors.All(a => a.Keyword.Text is "set" or "init")
        && accessors.Any(a => a.Body is not null || a.ExpressionBody is not null);

    /// <summary>A property's getter body, an expression or a block, when it has one.</summary>
    private static SyntaxNode? ComputedGetter(PropertyDeclarationSyntax property) =>
        (SyntaxNode?)property.ExpressionBody?.Expression
        ?? property.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text == "get") switch
        {
            { ExpressionBody: { } arrow } => arrow.Expression,
            { Body: { } block } => block,
            _ => null,
        };

    /// <summary>A property with a body, as its getter, and its setter where it has one with a body
    /// (an interface's default property writes through its other members, found in review, #418);
    /// nothing for a property with no getter body.</summary>
    private string ComputedProperty(PropertyDeclarationSyntax property, string className)
    {
        var getter = ComputedGetter(property);
        // A static `field` store's automatic getter reads the store (#483). An instance one is part
        // of the value, which the value members hold.
        var readsItsStore = getter is null
            && property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
            && Strategies.Expressions.FieldExpressionStrategy.UsesBackingField(property);
        if (getter is null && !IsSetterOnly(property) && !readsItsStore) return "";
        _converter.SetCurrentClass(className);
        var prefix = property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ? "static " : "";
        var propertyName = property.Identifier.Text.ToCamelCase();
        // Both accessors are lowered as a method's body is (#432), so a variable an expression body's
        // pattern binds is declared in front of its use, as it is in a class.
        var text = getter switch
        {
            null when readsItsStore => Written(JsClassMember.Getter(prefix, propertyName, "",
                JsStatement.Return(JsExpr.ThisMember(Strategies.Expressions.FieldExpressionStrategy.BackingSlot(property))))),
            null => "",
            BlockSyntax block => Written(JsClassMember.Getter(prefix, propertyName, "", _lowering.AccessorBody(block))),
            _ => Written(JsClassMember.Getter(prefix, propertyName, "",
                _lowering.Body(null, (ExpressionSyntax)getter, isIterator: false, [], isAsync: false))),
        };
        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text is "set" or "init");
        var setterBody = setter?.ExpressionBody is { } arrow
            ? _lowering.ExpressionBody(arrow.Expression, returns: false)
            : setter?.Body is { } setterBlock
                ? _lowering.AccessorBody(setterBlock)
                : null;
        if (setterBody is not null)
            text += Written(JsClassMember.Setter(prefix, propertyName, _lowering.Param("value", TsTypeOf(property.Type)), setterBody));
        return text;
    }

    /// <summary>An operator or a conversion as the static method its call sites reach, its body
    /// lowered as a method's (#432).</summary>
    private string StaticMember(string name, string parameters, BlockSyntax? block, ArrowExpressionClauseSyntax? arrow) =>
        Written(JsClassMember.Method("static ", name, "", parameters, "",
            _lowering.Body(block, arrow?.Expression, isIterator: false, [], isAsync: false)));

    /// <summary>A member in the one-line layout this emitter writes a class in, and the space after it.</summary>
    private static string Written(JsClassMember member) => JsMemberWriter.Write(member, JsLayout.Compact) + " ";

    /// <summary>
    /// The base record (if any) from a primary-constructor base clause (<c>record Dog(…) : Animal(Name)</c>):
    /// its name (generics erased) and the JS <c>super(...)</c> arguments. A base record named without
    /// arguments (<c>record Dog : Animal;</c>) is extended with a bare <c>super()</c>, since it has a
    /// constructor that takes none: it was dropped, and the derived twin had none of its base's
    /// members, the defaults it takes included (found in review, #418). Only a base with a twin is
    /// extended, or <c>extends</c> would name a module nothing writes (#428). Interfaces yield none.
    /// </summary>
    private (string? BaseName, string SuperArgs) BaseInfo(TypeDeclarationSyntax type)
    {
        var primary = type.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault();
        if (primary == null)
        {
            if (type.BaseList?.Types.FirstOrDefault() is SimpleBaseTypeSyntax simple
                && ModelFor(simple)?.GetSymbolInfo(simple.Type).Symbol is INamedTypeSymbol { TypeKind: TypeKind.Class } baseType
                && EmitsTwin(baseType))
            {
                return (simple.Type.TwinTypeName(ModelFor(simple)), "");
            }
            return (null, "");
        }

        var baseName = primary.Type.TwinTypeName(ModelFor(primary));

        var superArgs = new List<string>();
        if (primary.ArgumentList != null)
        {
            foreach (var arg in primary.ArgumentList.Arguments)
            {
                if (arg.Expression is IdentifierNameSyntax id)
                {
                    // A member forwarded to the base: the derived constructor's own parameter for it.
                    superArgs.Add(id.Identifier.ValueText.ToCamelCase().ToJsIdentifier());
                }
                else
                {
                    // It runs before `super()`, where the parameters are the constructor's own and
                    // `this` cannot be read: `: Base(X + 1)` wrote `super(this.x + 1)`, which threw.
                    superArgs.Add(_converter.WithConstructorParametersInScope(
                        () => _converter.ConvertExpression(arg.Expression)));
                }
            }
        }
        return (baseName, string.Join(", ", superArgs));
    }

    /// <summary>
    /// The static-method name an operator becomes. Named for the operation, not for the CLR's
    /// <c>op_Addition</c> mangling: the emitted class is read by people too.
    /// </summary>
    /// <summary>
    /// What a conversion is CALLED, from the symbol — the one place that decides it, so the emitted
    /// method and every call site cannot drift apart. Direction is symbol equality against the
    /// declaring type, and the other side's name uses the minimally-qualified display the call site
    /// has always used.
    /// </summary>
    internal static string ConversionNameFor(IMethodSymbol conversion)
    {
        var toSelf = SymbolEqualityComparer.Default.Equals(
            conversion.ReturnType, conversion.ContainingType);
        var other = toSelf ? conversion.Parameters[0].Type : conversion.ReturnType;
        return ConversionMethodName(
            other.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), from: toSelf);
    }

    internal static string? OperatorMethodName(string token) => token switch
    {
        "+" => "opAdd",
        "-" => "opSubtract",
        "*" => "opMultiply",
        "/" => "opDivide",
        "%" => "opModulo",
        "==" => "opEquals",
        "!=" => "opNotEquals",
        "<" => "opLessThan",
        ">" => "opGreaterThan",
        "<=" => "opLessOrEqual",
        ">=" => "opGreaterOrEqual",
        _ => null,
    };

    /// <summary>A UNARY operator's method, named by what it does so it cannot collide with the
    /// binary operator spelled with the same token (`-m` is opNegate, `a - b` is opSubtract).</summary>
    internal static string? UnaryOperatorMethodName(string token) => token switch
    {
        "-" => "opNegate",
        "+" => "opPlus",
        "!" => "opNot",
        "~" => "opComplement",
        _ => null,
    };

    /// <summary>
    /// A CONVERSION operator's method: <c>from</c> the source type when it produces the declaring
    /// type, <c>to</c> the target type when it consumes it — named by the C# keyword or type name
    /// the author wrote (`int` → Int, `Money` → Money), the same text a symbol displays, so the
    /// emitter (syntax) and the call site (bound tree) agree without a table.
    /// </summary>
    internal static string ConversionMethodName(string typeText, bool from)
    {
        var bare = typeText.Trim().TrimEnd('?');
        var dot = bare.LastIndexOf('.');
        if (dot >= 0) bare = bare[(dot + 1)..];

        // A CONSTRUCTED type is not an identifier. `List<int>` reached here verbatim and produced
        // `fromList<int>` — emitted as a method name and used as one by the call site, which no
        // JavaScript parser accepts. Every segment of the type becomes part of the name instead, so
        // `List<int>` is `fromListInt` and `Dictionary<string, int>` is `fromDictionaryStringInt`:
        // still readable, still deterministic, and different constructed types stay different names.
        var parts = bare.Split(new[] { '<', '>', ',', ' ', '[', ']', '.', '?' },
            System.StringSplitOptions.RemoveEmptyEntries);
        var identifier = string.Concat(parts.Select(part =>
            part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));

        return (from ? "from" : "to") + identifier;
    }

    /// <summary>
    /// A record's or a struct's method, its own or a default an interface supplies, lowered as a
    /// class's is (<see cref="MethodLowering"/>, #432): an async method, an iterator, an out or ref
    /// parameter and an expression body's pattern variable went through a copy here that handled
    /// none of them. Typed only in the .ts emission, where a record's methods are its behaviour and an
    /// untyped parameter ends the checking on the way in; the conformance harness runs the same class
    /// as plain `.mjs`, where an annotation is a parse error rather than a type.
    /// </summary>
    private string EmitMethod(MethodDeclarationSyntax method, string className)
    {
        _converter.SetCurrentClass(className);
        return _lowering.Method(method, asStatic: false, TsTypeOf,
            returns: type => _annotations ? TupleReturn(type) : "") is { } member ? Written(member) : "";
    }

    /// <summary>
    /// The return annotation of a method that returns a TUPLE, and nothing for any other: a tuple
    /// crosses as an array literal, which TypeScript reads as an array of the union of its elements,
    /// so its type is said (TypeScriptEmitter.TupleReturn has the class path's twin). Each element
    /// through the rule a member's type takes, so an enum among them is the member string it crosses
    /// as, where the whole tuple's name wrote the enum's C# spelling, a type TypeScript does not have.
    /// </summary>
    private string TupleReturn(TypeSyntax returnType)
    {
        var tuple = returnType as TupleTypeSyntax ?? (returnType as NullableTypeSyntax)?.ElementType as TupleTypeSyntax;
        if (tuple is null) return "";
        var elements = "[" + string.Join(", ", tuple.Elements.Select(element =>
            TypeDeclarationExtensions.TsTypeFor(element.Type, ModelFor(element.Type)))) + "]";
        return ": " + (returnType is NullableTypeSyntax ? TypeScriptEmitter.OrNull(elements) : elements);
    }
}
