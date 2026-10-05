using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
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
    /// unextended. A PARTIAL declaration that declares nothing is one exception, as it is for a
    /// class (PlainClassModule): another declaration carries the type's members, and the empty one
    /// written first would leave the twin without them.
    /// <para>
    /// A type marked <c>[ServerOnly]</c> is the other, as a class is: it never crosses, so it has no
    /// twin, and the code in it may use the whole server surface. Every record and struct got a twin
    /// whatever it was marked, so `[ServerOnly] struct TokenHasher` over HMACSHA256 failed the build
    /// with EQ2004, whose own message says to mark the type [ServerOnly]. Asked of the declaration,
    /// and of <paramref name="symbol"/> for the partial declaration another of whose declarations
    /// carries the attribute (C# allows it on one of them only); null where the host has no
    /// compilation, which then reads the declaration alone.
    /// </para>
    /// </summary>
    public static bool CanEmit(TypeDeclarationSyntax type, INamedTypeSymbol? symbol) =>
        type is RecordDeclarationSyntax or StructDeclarationSyntax
        && !(type.Modifiers.Any(SyntaxKind.PartialKeyword) && type.Members.Count == 0 && type.ParameterList is null)
        && !type.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.IsNamed("ServerOnly"))
        && !(symbol?.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is "ServerOnly" or "ServerOnlyAttribute") ?? false);

    /// <summary>
    /// Whether this emitter writes a twin for <paramref name="type"/>: declared in source, by a
    /// declaration <see cref="CanEmit"/> accepts. The rule every path that NAMES the twin asks — a
    /// type test (<c>instanceof</c>) and a default (<c>new T()</c>) may only name a class that exists,
    /// and a type declared only by an empty partial declaration has none, nor one marked
    /// <c>[ServerOnly]</c>.
    /// </summary>
    public static bool EmitsTwin(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration && CanEmit(declaration, type));

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
    /// A C# constructor that does its own work, which the twin runs (#413): the primary constructor, an
    /// explicit one that does not chain with `: this(…)`, or the implicit parameterless one. Its
    /// parameters, whether they are the primary ones, its declaration (none for the primary or the
    /// implicit one), and the counts of arguments it takes.
    /// </summary>
    private sealed record Root(IReadOnlyList<ParameterSyntax> Parameters, bool Primary,
        ConstructorDeclarationSyntax? Explicit, Arity Arity);

    /// <summary>A constructor that chains with `: this(…)` to a <see cref="Root"/>, and the counts of
    /// arguments it takes.</summary>
    private sealed record Alternate(ConstructorDeclarationSyntax Constructor, Arity Arity, Root Target);

    /// <summary>Every constructor of a type the twin reaches, the roots and the ones chaining to them.</summary>
    private sealed record Constructors(IReadOnlyList<Root> Roots, IReadOnlyList<Alternate> Alternates);

    /// <summary>
    /// The constructors the twin reaches. JavaScript has ONE constructor, so the twin's is a branch per
    /// C# constructor on how many arguments arrived: each one that does its own work (a
    /// <see cref="Root"/>) binds its parameters, sets the members and runs its base's constructor and
    /// its body, and each one that chains with `: this(…)` evaluates the chain's arguments into its
    /// root's parameters and runs its own body after the root's. A constructor the branch cannot tell
    /// apart from another, because it takes a count of arguments the other takes too, or one that
    /// chains to a constructor that chains in turn, is refused (EQ1009): every explicit constructor of a
    /// record or a struct was dropped before, in silence.
    /// </summary>
    private Constructors ConstructorsOf(TypeDeclarationSyntax type)
    {
        var declared = type.Members.OfType<ConstructorDeclarationSyntax>()
            .Where(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();

        var roots = new List<Root>();
        if (type.ParameterList is { } primary)
            roots.Add(new Root(primary.Parameters.ToList(), true, null, Arity.Of(primary.Parameters.ToList())));
        else
        {
            foreach (var constructor in declared.Where(constructor => !Chains(constructor)))
            {
                var arity = Arity.Of(constructor.ParameterList.Parameters.ToList());
                if (Clash(type, roots.Select(root => (Signature(type, root), root.Arity)), arity) is { } clash)
                {
                    Refuse(type, constructor, arity, clash);
                    continue;
                }
                roots.Add(new Root(constructor.ParameterList.Parameters.ToList(), false, constructor, arity));
            }
            if (roots.Count == 0) roots.Add(new Root([], false, null, new Arity(0, 0)));
        }

        var alternates = new List<Alternate>();
        foreach (var constructor in declared.Where(Chains))
        {
            var signature = $"'{type.Identifier.Text}{constructor.ParameterList}'";
            if (TargetOf(constructor.Initializer!, roots) is not { } target)
            {
                _converter.Report(constructor, ConversionSeverity.Error, "EQ1009",
                    $"{signature} chains to a constructor that does not do its own work: one that chains in turn, or a "
                    + "struct's implicit one beside constructors of its own. The twin has one constructor, which reaches the "
                    + "others by how many arguments arrive, so each must chain to one that does its own work.");
                continue;
            }
            var arity = Arity.Of(constructor.ParameterList.Parameters.ToList());
            var taken = roots.Select(root => (Signature(type, root), root.Arity))
                .Concat(alternates.Select(alternate => ($"'{type.Identifier.Text}{alternate.Constructor.ParameterList}'", alternate.Arity)));
            if (Clash(type, taken, arity) is { } clash)
            {
                Refuse(type, constructor, arity, clash);
                continue;
            }
            alternates.Add(new Alternate(constructor, arity, target));
        }
        return new Constructors(roots, alternates);
    }

    /// <summary>The first constructor already taken whose counts of arguments meet <paramref name="arity"/>.</summary>
    private static string? Clash(TypeDeclarationSyntax type, IEnumerable<(string Signature, Arity Arity)> taken, Arity arity) =>
        taken.Where(other => other.Arity.Overlaps(arity)).Select(other => other.Signature).FirstOrDefault();

    private void Refuse(TypeDeclarationSyntax type, ConstructorDeclarationSyntax constructor, Arity arity, string clash) =>
        _converter.Report(constructor, ConversionSeverity.Error, "EQ1009",
            $"'{type.Identifier.Text}{constructor.ParameterList}' takes {arity} argument(s), and {clash} takes that many too. "
            + "The twin has one constructor, which tells the others apart by how many arguments arrive, so no two may "
            + "take the same count: give this one a count of its own, or make it a static factory.");

    /// <summary>How a refusal names a root.</summary>
    private static string Signature(TypeDeclarationSyntax type, Root root) =>
        $"'{type.Identifier.Text}({string.Join(", ", root.Parameters)})'";

    /// <summary>Whether a constructor hands its work to another with `: this(…)`.</summary>
    private static bool Chains(ConstructorDeclarationSyntax constructor) =>
        constructor.Initializer?.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) == true;

    /// <summary>
    /// The root a `: this(…)` chain reaches: an explicit one by its declaration, the primary one
    /// (declared by the type's own declaration), or a struct's implicit parameterless one. Null for a
    /// chain to a constructor that chains in turn. Without a model to ask, the root that takes as many
    /// arguments as the chain passes.
    /// </summary>
    private Root? TargetOf(ConstructorInitializerSyntax chain, IReadOnlyList<Root> roots)
    {
        if (ModelFor(chain)?.GetSymbolInfo(chain).Symbol is not IMethodSymbol target)
        {
            var count = chain.ArgumentList.Arguments.Count;
            return roots.FirstOrDefault(root => root.Arity.Low <= count && count <= root.Arity.High) ?? roots[0];
        }
        var declared = target.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        return roots.FirstOrDefault(root => root.Explicit is { } own ? declared == own
            : root.Primary ? declared is TypeDeclarationSyntax
            : target.IsImplicitlyDeclared);
    }

    /// <summary>The argument counts a constructor accepts: from its required parameters to all of
    /// them, with no upper end for a <c>params</c> one.</summary>
    private readonly record struct Arity(int Low, int High)
    {
        public static Arity Of(IReadOnlyList<ParameterSyntax> parameters) => new(
            parameters.Count(parameter => parameter.Default is null && !parameter.Modifiers.Any(SyntaxKind.ParamsKeyword)),
            parameters.Any(parameter => parameter.Modifiers.Any(SyntaxKind.ParamsKeyword)) ? int.MaxValue : parameters.Count);

        public bool Overlaps(Arity other) => Low <= other.High && other.Low <= High;

        /// <summary>The test a branch of the twin's constructor makes of the count of what arrived.</summary>
        public string Test(string arrived) =>
            High == int.MaxValue ? $"{arrived}.length >= {Low}"
            : Low == High ? $"{arrived}.length === {Low}"
            : $"{arrived}.length >= {Low} && {arrived}.length <= {High}";

        public override string ToString() => High == int.MaxValue ? $"{Low} or more" : Low == High ? $"{Low}" : $"{Low} to {High}";
    }

    /// <summary>
    /// An alternate's branch, which evaluates the `: this(…)` arguments where the alternate's own
    /// parameters are bound to what arrived, and lands them in its root's parameters once they are all
    /// evaluated: an argument that reads a parameter of the root's name reads the alternate's own. The
    /// alternate's parameters live in a block of their own, which shadows the root's, and the values
    /// cross it in temporaries, so no function is needed to hold the C#.
    /// </summary>
    private string Mapped(Alternate alternate, string arrived, string? selected)
    {
        var landed = Landed(alternate.Constructor.Initializer!, alternate.Target);
        if (landed.Count == 0 && selected is null) return "";
        var annotation = _annotations ? ": any" : "";
        var temporaries = landed.Select((_, i) => $"$c{i}").ToList();
        var sb = new StringBuilder($"if ({alternate.Arity.Test(arrived)}) {{ ");
        if (landed.Count > 0)
        {
            sb.Append($"let {string.Join(", ", temporaries.Select(temporary => temporary + annotation))}; ");
            sb.Append($"{{ {Bound(alternate.Constructor, arrived)}");
            for (var i = 0; i < landed.Count; i++) sb.Append($"{temporaries[i]} = {landed[i].Value}; ");
            sb.Append("} ");
            for (var i = 0; i < landed.Count; i++) sb.Append($"{landed[i].Parameter} = {temporaries[i]}; ");
        }
        if (selected is not null) sb.Append(selected);
        return sb.Append("} ").ToString();
    }

    /// <summary>
    /// What each of a root's parameters takes from a `: this(…)` chain: the argument that names or
    /// reaches it, its default where the chain leaves it out, which would otherwise hold whatever
    /// argument arrived in its place, and for a <c>params</c> one the array C# passes, its elements
    /// gathered when the chain lists them.
    /// </summary>
    private IReadOnlyList<(string Parameter, string Value)> Landed(ConstructorInitializerSyntax chain, Root root)
    {
        var parameters = root.Parameters;
        var values = new string?[parameters.Count];
        var rest = parameters.Count > 0 && parameters[^1].Modifiers.Any(SyntaxKind.ParamsKeyword) ? parameters.Count - 1 : -1;
        var arguments = chain.ArgumentList.Arguments;
        // The array passed whole, which C# also allows, rather than its elements.
        var whole = rest >= 0 && arguments.Count == parameters.Count && arguments[^1].NameColon is null
            && ModelFor(chain)?.GetTypeInfo(arguments[^1].Expression).Type is IArrayTypeSymbol;
        var gathered = new List<string>();
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var value = _converter.ConvertExpression(argument.Expression);
            var ordinal = argument.NameColon is { } named
                ? parameters.ToList().FindIndex(parameter => parameter.Identifier.ValueText == named.Name.Identifier.ValueText)
                : i;
            if (rest >= 0 && ordinal >= rest && argument.NameColon is null && !whole) gathered.Add(value);
            else if (ordinal >= 0 && ordinal < values.Length) values[ordinal] = value;
        }
        if (rest >= 0) values[rest] ??= $"[{string.Join(", ", gathered)}]";
        for (var i = 0; i < values.Length; i++)
            values[i] ??= parameters[i].Default is { } given
                ? _converter.ConvertExpression(given.Value, parameters[i].Type?.ToString())
                : null;
        return parameters.Select((parameter, i) => (Parameter: ParameterName(parameter, root.Primary), Value: values[i]))
            .Where(landed => landed.Value is not null)
            .Select(landed => (landed.Parameter, landed.Value!))
            .ToList();
    }

    /// <summary>A constructor's own parameters, declared in the block that reads them and bound to what
    /// arrived, each with its default and a <c>params</c> one with every argument from its place on.</summary>
    private string Bound(ConstructorDeclarationSyntax constructor, string arrived) =>
        constructor.ParameterList.Parameters.Count == 0 ? "" : $"const [{Pattern(constructor.ParameterList.Parameters.ToList(), primary: false)}] = {arrived}; ";

    /// <summary>Parameters as a destructuring pattern binds them: each by the name every reference to it
    /// is converted to, with its default, and a <c>params</c> one as the rest.</summary>
    private string Pattern(IReadOnlyList<ParameterSyntax> parameters, bool primary) =>
        string.Join(", ", parameters.Select(parameter =>
        {
            var name = ParameterName(parameter, primary);
            if (parameter.Modifiers.Any(SyntaxKind.ParamsKeyword)) return "..." + name;
            var fallback = parameter.Default is { } given
                ? _converter.ConvertExpression(given.Value, parameter.Type?.ToString())
                : primary && parameter.Type is { } typed ? DefaultOf(typed) : null;
            return fallback is null ? name : $"{name} = {fallback}";
        }));

    /// <summary>The body each alternate runs after its root's, as C# runs a constructor that chains with
    /// `: this(…)`, with its own parameters bound to what arrived. It is the constructor's last
    /// statement, so its `return` ends it as it ends that constructor in C#.</summary>
    private string AlternateBodies(IReadOnlyList<Alternate> alternates, string arrived)
    {
        var sb = new StringBuilder();
        foreach (var alternate in alternates)
        {
            if (Body(alternate.Constructor) is not { Length: > 0 } body) continue;
            sb.Append($"if ({alternate.Arity.Test(arrived)}) {{ {Bound(alternate.Constructor, arrived)}{body}}} ");
        }
        return sb.ToString();
    }

    /// <summary>
    /// A root's own body. A `return` in it ends that constructor in C# and nothing after it, where the
    /// body of an alternate chaining to it still runs, so a body that returns early, under an alternate
    /// with a body of its own, runs in a function of its own whose `return` ends it alone. C# allows no
    /// <c>await</c> and no <c>yield</c> in a constructor, so nothing in it changes meaning there.
    /// </summary>
    private string RootBody(Root root, IReadOnlyList<Alternate> alternates)
    {
        if (root.Explicit is not { } own || Body(own) is not { Length: > 0 } body) return "";
        var returns = own.Body?.DescendantNodes(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>().Any() == true;
        var followed = alternates.Any(alternate => alternate.Target == root && Body(alternate.Constructor).Length > 0);
        return returns && followed ? $"(() => {{ {body}}})(); " : body;
    }

    /// <summary>A static's initializer: its VALUE where C# folds it to a constant
    /// (<see cref="TypeInitializer.Constant"/>), and the expression converted otherwise.</summary>
    private string StaticValue(EqualsValueClauseSyntax initializer, TypeSyntax type) =>
        TypeInitializer.Constant(initializer, ModelFor(initializer), _converter)
        ?? ExpressionVariableScanner.Scoped(initializer.Value, _converter.ConvertExpression(initializer.Value, type.ToString()), _annotations);

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
    /// <para>
    /// One root keeps its parameters as the twin's own. Several, which only a type with no primary
    /// constructor can have, each take a branch on how many arguments arrived (#413): its parameters are
    /// declared once, the union of every root's, and bound in the branch that takes the call, where its
    /// base's constructor runs with its own arguments and its body after the members. A type with a
    /// static constructor runs it before anything else, as C# runs it before the first instance.
    /// </para>
    /// </summary>
    private string Constructor(TypeDeclarationSyntax type, IReadOnlyList<ValueMember> members, string? baseName, string superArgs)
    {
        var constructors = ConstructorsOf(type);
        var name = type.Identifier.Text;
        _converter.SetCurrentClass(name);
        var annotation = _annotations ? ": any" : "";
        var single = constructors.Roots.Count == 1 ? constructors.Roots[0] : null;
        var arrived = single is null ? "$a" : "arguments";

        var sb = new StringBuilder();
        if (single is not null)
        {
            // A count an alternate takes below what the root requires reaches the twin with fewer
            // arguments than its parameters: each is then optional to TypeScript, as JavaScript reads it.
            var optional = constructors.Alternates.Any(alternate => alternate.Arity.Low < single.Arity.Low);
            var parameters = single.Parameters.Select(parameter => _lowering.ParamWithDefault(
                ParameterName(parameter, single.Primary), "any",
                parameter.Default is { } given
                    ? _converter.ConvertExpression(given.Value, parameter.Type?.ToString())
                    : single.Primary && parameter.Type is { } typed ? DefaultOf(typed)
                    : optional ? "undefined" : null,
                parameter.Modifiers.Any(SyntaxKind.ParamsKeyword)));
            sb.Append($"constructor({string.Join(", ", parameters)}) {{ ");
            if (TypeInitializer.HasStaticConstructor(type)) sb.Append(Started(name));
            foreach (var alternate in constructors.Alternates) sb.Append(Mapped(alternate, arrived, selected: null));
        }
        else
        {
            sb.Append($"constructor(...{arrived}{(_annotations ? ": any[]" : "")}) {{ ");
            if (TypeInitializer.HasStaticConstructor(type)) sb.Append(Started(name));
            var union = constructors.Roots.SelectMany(root => root.Parameters.Select(parameter => ParameterName(parameter, primary: false)))
                .Distinct().ToList();
            if (union.Count > 0) sb.Append($"let {string.Join(", ", union.Select(parameter => parameter + annotation))}; ");
            sb.Append($"let $k{annotation} = -1; ");
            var branches = constructors.Alternates.Select(alternate =>
                    Mapped(alternate, arrived, $"$k = {Index(constructors, alternate.Target)}; "))
                .Concat(constructors.Roots.Select((root, i) =>
                    $"if ({root.Arity.Test(arrived)}) {{ {(root.Parameters.Count > 0 ? $"[{Pattern(root.Parameters, primary: false)}] = {arrived}; " : "")}$k = {i}; }} "));
            sb.Append(string.Join("else ", branches));
        }

        // A member of a reference type with no initializer starts as C#'s null, which strict TypeScript
        // refuses for a type it reads as never null (`declare c: string`): the value is said to be one.
        var values = members.Select(member => (member, Value: ValueOf(member, out var runsCode), runsCode))
            .Select(entry => entry.Value == "null" && _annotations && !Nullable(entry.member.TsType)
                ? entry with { Value = "null!" }
                : entry)
            .ToList();
        if (baseName is not null)
        {
            foreach (var (member, value, runsCode) in values)
                if (runsCode) sb.Append($"const ${member.Js} = {value}; ");
            if (single is not null)
                sb.Append($"super({SuperArguments(single, superArgs)}); ");
            else
                // Every branch calls it, the last one whatever arrived, as JavaScript requires of a
                // derived class's constructor.
                sb.Append(string.Join("else ", constructors.Roots.Select((root, i) => i == constructors.Roots.Count - 1
                    ? $"{{ super({SuperArguments(root, superArgs)}); }} "
                    : $"if ($k === {i}) {{ super({SuperArguments(root, superArgs)}); }} ")));
            foreach (var (member, value, runsCode) in values)
                sb.Append($"this.{member.Js} = {(runsCode ? "$" + member.Js : value)}; ");
        }
        else
        {
            foreach (var (member, value, _) in values)
                sb.Append($"this.{member.Js} = {value}; ");
        }

        if (single is not null)
            sb.Append(RootBody(single, constructors.Alternates));
        else
            foreach (var (root, i) in constructors.Roots.Select((root, i) => (root, i)))
                if (RootBody(root, constructors.Alternates) is { Length: > 0 } body)
                    sb.Append($"if ($k === {i}) {{ {body}}} ");
        sb.Append(AlternateBodies(constructors.Alternates, arrived));
        return sb.Append("} ").ToString();
    }

    /// <summary>Whether TypeScript reads a member's type as one that may hold null.</summary>
    private static bool Nullable(string tsType) =>
        tsType == "any" || tsType.Split('|').Any(part => part.Trim() is "null" or "undefined");

    /// <summary>Where a root stands among the roots.</summary>
    private static int Index(Constructors constructors, Root root) =>
        constructors.Roots.Select((candidate, i) => (candidate, i)).First(pair => pair.candidate == root).i;

    /// <summary>The arguments a root hands its base's constructor: its own `: base(…)` ones, or the base
    /// clause's.</summary>
    private string SuperArguments(Root root, string superArgs) =>
        root.Explicit?.Initializer is { } chain && chain.ThisOrBaseKeyword.IsKind(SyntaxKind.BaseKeyword)
            ? string.Join(", ", chain.ArgumentList.Arguments.Select(argument => _converter.ConvertExpression(argument.Expression)))
            : superArgs;

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
        _startsInitialization = TypeInitializer.HasStaticConstructor(type) ? name : null;
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

            // getHashCode: the members `equals` reads, combined, as the record's synthesized GetHashCode
            // and a struct's ValueType.GetHashCode hash them, so two values `equals` finds equal hash
            // alike by construction (#519). One the app wrote OVERRIDING it is its own, emitted with
            // its methods: a static one, or one that hides it, leaves the synthesized one in place, as
            // .NET does. A record derived from a record of this compilation combines its base's hash
            // first, which is the base's own override where it wrote one, as .NET's synthesized hash
            // calls it.
            if (!type.Members.OfType<MethodDeclarationSyntax>().Any(method =>
                    method is { Identifier.Text: "GetHashCode", ParameterList.Parameters.Count: 0 }
                    && method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.OverrideKeyword))))
            {
                var hashed = members.Select(m => $"this.{m.Js}");
                if (ModelFor(type)?.GetDeclaredSymbol(type) is INamedTypeSymbol { BaseType: { IsRecord: true } baseRecord }
                    && baseRecord.Locations.Any(location => location.IsInSource))
                    hashed = hashed.Prepend("super.getHashCode()");
                sb.Append(tsTypeDeclarations ? "getHashCode(): number { return $eq.hash.combine(" : "getHashCode() { return $eq.hash.combine(")
                    .Append(string.Join(", ", hashed))
                    .Append("); } ");
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

        // Static STORES — `public static readonly CodePosition Start = new(0, 0);`, the other half of
        // the "well-known value" idiom, which nothing emitted: `CodePosition.start` was undefined, so
        // every comparison against the origin silently failed. A field, an auto-property (one with a
        // custom setter is behaviour, which a plain field would throw away, so it keeps its own
        // accessors below), the store a property guards with `field` (#483), and a field-like event,
        // in SOURCE ORDER (TypeInitializer.Stores), which is the order C# initializes them in.
        //
        // Absent an initialiser the member takes its TYPE's default, not `undefined`: C# gives
        // `static int Count { get; set; }` a 0, and a twin answering undefined disagrees with the
        // server about a number.
        //
        // And when one of them can observe another (an initializer that is not a constant, a zero that
        // constructs, or a static constructor), every static starts at its zero and the initializers
        // run in declaration order, on first use (TypeInitializer, #417): written in place,
        // `static first = new Early()` ran Early's constructor before `static seed = 3` was defined, and
        // read NaN. A constant is its value wherever it stands, and never initializes.
        var ordered = TypeInitializer.Orders(type, ModelFor);
        _converter.SetCurrentClass(name);
        foreach (var member in type.Members)
        {
            if (member is FieldDeclarationSyntax constant && constant.Modifiers.Any(SyntaxKind.ConstKeyword))
                foreach (var variable in constant.Declaration.Variables)
                    sb.Append($"static {variable.Identifier.Text.ToCamelCase()} = "
                        + $"{(variable.Initializer is { } init ? StaticValue(init, constant.Declaration.Type) : DefaultOf(constant.Declaration.Type))}; ");
            else if (!ordered)
                foreach (var store in TypeInitializer.StoresOf(member))
                    sb.Append($"static {store.Name} = {(store.Initializer is { } init ? StaticValue(init, store.Type) : DefaultOf(store.Type))}; ");
        }
        if (ordered)
            foreach (var member in TypeInitializer.Members(type, name, TypeInitializer.Collect(type, TsTypeOf, DefaultOf, StaticValue),
                         _converter, _lowering, _annotations, JsLayout.Compact))
                sb.Append(Written(member));

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
                .OfType<TypeDeclarationSyntax>().FirstOrDefault(declaration => CanEmit(declaration, parent)) is { } baseDeclaration)
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
            null when readsItsStore => Own(JsClassMember.Getter(prefix, propertyName, "",
                JsStatement.Return(JsExpr.ThisMember(Strategies.Expressions.FieldExpressionStrategy.BackingSlot(property))))),
            null => "",
            BlockSyntax block => Own(JsClassMember.Getter(prefix, propertyName, "", _lowering.AccessorBody(block))),
            _ => Own(JsClassMember.Getter(prefix, propertyName, "",
                _lowering.Body(null, (ExpressionSyntax)getter, isIterator: false, []))),
        };
        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.Keyword.Text is "set" or "init");
        var setterBody = setter?.ExpressionBody is { } arrow
            ? _lowering.ExpressionBody(arrow.Expression, returns: false)
            : setter?.Body is { } setterBlock
                ? _lowering.AccessorBody(setterBlock)
                : null;
        if (setterBody is not null)
            text += Own(JsClassMember.Setter(prefix, propertyName, _lowering.Param("value", TsTypeOf(property.Type)), setterBody));
        return text;
    }

    /// <summary>An operator or a conversion as the static method its call sites reach, its body
    /// lowered as a method's (#432).</summary>
    private string StaticMember(string name, string parameters, BlockSyntax? block, ArrowExpressionClauseSyntax? arrow) =>
        Own(JsClassMember.Method("static ", name, "", parameters, "",
            _lowering.Body(block, arrow?.Expression, isIterator: false, [])));

    /// <summary>A member in the one-line layout this emitter writes a class in, and the space after it.</summary>
    private static string Written(JsClassMember member) => JsMemberWriter.Write(member, JsLayout.Compact) + " ";

    /// <summary>The start of the type's initialization (<see cref="TypeInitializer.Start"/>), the first
    /// statement of the constructor of a type with a static constructor, in this emitter's layout.</summary>
    private static string Started(string className) =>
        JsStatementWriter.Write(TypeInitializer.Start(className), JsLayout.Compact) + " ";

    /// <summary>The name of the type being written when it declares a static constructor, which its static
    /// members start before anything else (<see cref="TypeInitializer.StartedIn"/>); null otherwise.</summary>
    private string? _startsInitialization;

    /// <summary>A member of the type being written, starting its initialization first where the type
    /// declares a static constructor.</summary>
    private string Own(JsClassMember member) => Written(_startsInitialization is { } type
        ? TypeInitializer.StartedIn(member, type, new HashSet<string>())
        : member);

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
    /// binary operator spelled with the same token (`-m` is opNegate, `a - b` is opSubtract). A step's
    /// (`++`, `--`) is a unary operator too, which every step of its type calls, a local's and an
    /// indexer's alike: it was written into no twin, and `c++` stepped an object into NaN.</summary>
    internal static string? UnaryOperatorMethodName(string token) => token switch
    {
        "-" => "opNegate",
        "+" => "opPlus",
        "!" => "opNot",
        "~" => "opComplement",
        "++" => "opIncrement",
        "--" => "opDecrement",
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
            returns: type => _annotations ? TupleReturn(type) : "") is { } member ? Own(member) : "";
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
