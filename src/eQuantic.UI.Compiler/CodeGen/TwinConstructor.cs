using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// A type's C# constructors as the one constructor its twin has, built as IR for every emitter that
/// writes a twin eqc builds: a record's and a struct's (<see cref="RecordTypeEmitter"/>, #413) and a
/// plain class's (<see cref="TypeScriptEmitter"/>, #583). JavaScript has ONE constructor, so the twin's
/// is a branch per C# constructor on how many arguments arrived: each one that does its own work (a
/// <see cref="Root"/>) binds its parameters, starts the type's state, calls its base's constructor with
/// its own arguments and runs its body, and each one that chains with `: this(…)` (an
/// <see cref="Alternate"/>) evaluates the chain's arguments into its root's parameters and runs its own
/// body after the root's. A constructor the branch cannot tell apart from another, because it takes a
/// count of arguments the other takes too, or one that chains to a constructor that chains in turn, is
/// refused (EQ1009).
/// <para>
/// It was the record emitter's, as text, while a plain class kept its widest constructor, a trailing
/// config object and a `super()` with no arguments: `Money() : this(100)` built a Money of no cents,
/// `: base(x * 2)` passed the base nothing, and a class's primary constructor was not read at all.
/// </para>
/// </summary>
internal sealed class TwinConstructor
{
    private readonly CSharpToJsConverter _converter;
    private readonly MethodLowering _lowering;
    private readonly Func<SyntaxNode, SemanticModel?> _modelFor;
    private readonly bool _annotations;
    private readonly JsLayout _layout;
    private readonly Func<TypeSyntax?, string>? _parameterType;

    /// <param name="converter">The converter the C# goes through.</param>
    /// <param name="lowering">The method lowering of the emitter, which writes a parameter as its target does.</param>
    /// <param name="modelFor">The model that can answer about a node, or null where none can.</param>
    /// <param name="annotations">Whether the twin is TypeScript.</param>
    /// <param name="layout">The layout the emitter writes the constructor in, which a function
    /// nested in it (a root's own body, run in a function of its own) is laid out in too.</param>
    /// <param name="parameterType">The TypeScript a parameter's declared type is, where the twin's
    /// constructor names its parameters' types; null types every one of them <c>any</c>, as a record's
    /// twin does.</param>
    public TwinConstructor(CSharpToJsConverter converter, MethodLowering lowering, Func<SyntaxNode, SemanticModel?> modelFor,
        bool annotations, JsLayout layout, Func<TypeSyntax?, string>? parameterType = null)
    {
        _converter = converter;
        _lowering = lowering;
        _modelFor = modelFor;
        _annotations = annotations;
        _layout = layout;
        _parameterType = parameterType;
    }

    /// <summary>One member the constructor starts: the slot it is held in on the instance, what it starts
    /// as, whether evaluating that can do anything but read a value, which is what a derived type
    /// evaluates before its base's constructor runs, and the C# that declares it, which the statements
    /// that start it map to, so a frame thrown in an initializer leads to its own line (#293).</summary>
    public readonly record struct Start(string Slot, JsExpr Value, bool RunsCode, SyntaxNode? Origin = null);

    /// <summary>
    /// What a member starts as, as C# starts it: a primary constructor's parameter the parameter
    /// itself, a property's or a field's initializer converted like any expression where the
    /// constructor's parameters are in scope, and its type's default where there is none.
    /// <paramref name="tsType"/> is what TypeScript reads the member as: a reference type that starts
    /// as C#'s null where TypeScript reads it as never null (`declare c: string`) is said to be one.
    /// <para>
    /// Every initializer runs on every construction (#413). It was each member's PARAMETER default,
    /// which JavaScript evaluates only for an argument that is missing, so an object initializer setting
    /// a member skipped its initializer, and `new Counted { B = 10 }` ran one `++N` where C# runs two.
    /// </para>
    /// </summary>
    public Start StartOf(string slot, SyntaxNode declaration, string tsType)
    {
        var (value, runsCode) = declaration switch
        {
            ParameterSyntax parameter => (ParameterName(parameter, primary: true), false),
            PropertyDeclarationSyntax { Initializer: { } initializer } property => (Initialized(initializer.Value, property.Type), true),
            PropertyDeclarationSyntax property => (DefaultOf(property.Type), false),
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declared } variable => variable.Initializer is { } given
                ? (Initialized(given.Value, declared.Type), true)
                : (DefaultOf(declared.Type), false),
            _ => ("null", false),
        };
        if (value == "null" && _annotations && !Nullable(tsType)) value = "null!";
        return new Start(slot, JsExpr.Opaque(value), runsCode, declaration);
    }

    /// <summary>
    /// The twin's constructor. It takes the C# constructor's parameters, never a member's value, starts
    /// every member of <paramref name="state"/>, in its order, and runs the constructor's body after
    /// them; an object initializer is applied by the construction site once it returns
    /// (<see cref="Strategies.Expressions.ObjectInitializer"/>). Over a base, C# runs the derived type's
    /// initializers BEFORE the base constructor, and JavaScript cannot touch `this` before `super()`, so
    /// each one that can do anything is evaluated into a local first and assigned after.
    /// <para>
    /// One root keeps its parameters as the twin's own. Several, which only a type with no primary
    /// constructor can have, each take a branch on how many arguments arrived: its parameters are
    /// declared once, the union of every root's, and bound in the branch that takes the call, where its
    /// base's constructor runs with its own arguments and its body after the members.
    /// <paramref name="first"/> runs before anything else: a type with a static constructor starts its
    /// initialization there, as C# runs it before the first instance.
    /// </para>
    /// </summary>
    /// <param name="type">The declaration whose constructors the twin reaches.</param>
    /// <param name="state">What the constructor starts, in the order C# starts it (<see cref="StartOf"/>).</param>
    /// <param name="hasBase">Whether the twin extends another, whose constructor it has to call.</param>
    /// <param name="clause">The base clause of a primary constructor (<c>: Shape(Kind)</c>), whose
    /// arguments the base's constructor takes.</param>
    /// <param name="first">What runs before anything else, or null.</param>
    public JsClassMember Build(TypeDeclarationSyntax type, IReadOnlyList<Start> state, bool hasBase,
        PrimaryConstructorBaseTypeSyntax? clause, JsStatement? first)
    {
        var constructors = ConstructorsOf(type);
        _converter.SetCurrentClass(type.Identifier.Text);
        var single = constructors.Roots.Count == 1 ? constructors.Roots[0] : null;
        var arrived = single is null ? "$a" : "arguments";

        var statements = new List<JsStatement>();
        if (first is not null) statements.Add(first);
        string parameters;
        if (single is not null)
        {
            // A count an alternate takes below what the root requires reaches the twin with fewer
            // arguments than its parameters: each is then optional to TypeScript, as JavaScript reads it.
            var optional = constructors.Alternates.Any(alternate => alternate.Arity.Low < single.Arity.Low);
            parameters = string.Join(", ", single.Parameters.Select(parameter =>
            {
                var defaulted = parameter.Default is { } given
                    ? _converter.ConvertExpression(given.Value, parameter.Type?.ToString())
                    : single.Primary && parameter.Type is { } typed ? DefaultOf(typed)
                    : optional ? "undefined" : null;
                // Its declared type where the emitter names one: one that may arrive missing is optional
                // to TypeScript (`x?: number`).
                var type = _parameterType?.Invoke(parameter.Type) ?? "any";
                return _lowering.ParamWithDefault(ParameterName(parameter, single.Primary), type, defaulted,
                    parameter.Modifiers.Any(SyntaxKind.ParamsKeyword));
            }));
            foreach (var alternate in constructors.Alternates)
                if (Mapped(alternate, arrived, selected: null) is { } mapped)
                    statements.Add(JsStatement.If(mapped.Test, JsStatement.Block(mapped.Body), null));
        }
        else
        {
            parameters = $"...{arrived}{(_annotations ? ": any[]" : "")}";
            var union = constructors.Roots.SelectMany(root => root.Parameters.Select(parameter => ParameterName(parameter, primary: false)))
                .Distinct().ToList();
            if (union.Count > 0) statements.Add(JsStatement.Raw($"let {string.Join(", ", union.Select(parameter => parameter + Annotation))};"));
            statements.Add(JsStatement.Let("$k", Annotation, JsExpr.Literal("-1")));
            var branches = constructors.Alternates
                .Select(alternate => Mapped(alternate, arrived, Assign("$k", JsExpr.Literal(Index(constructors, alternate.Target).ToString())))!.Value)
                .Concat(constructors.Roots.Select((root, i) => (Test: root.Arity.Test(arrived),
                    Body: Bindings(root.Parameters, arrived).Select(binding => Assign(binding.Name, binding.Value))
                        .Append(Assign("$k", JsExpr.Literal(i.ToString()))).ToList())));
            statements.Add(Chain(branches.Select(branch => ((JsExpr?)branch.Test, (IReadOnlyList<JsStatement>)branch.Body)).ToList()));
        }

        if (hasBase)
        {
            // Each in a local under a name no member and no name of the constructor's own can take: a
            // C# name holds no `$`, and the constructor's are `$` and a letter (`$a`, `$k`, `$c0`). A
            // member `A` or `K` was `const $a` beside the rest parameter `$a`, and the module did not load.
            foreach (var start in state)
                if (start.RunsCode) statements.Add(JsStatement.Const(Evaluated(start.Slot), start.Value) with { Origin = start.Origin });
            var implicitException = ImplicitExceptionBase(type);
            if (single is not null)
                statements.AddRange(SuperCall(single, clause, implicitException));
            else
                // Every branch calls it, the last one whatever arrived, as JavaScript requires of a
                // derived class's constructor.
                statements.Add(Chain(constructors.Roots.Select((root, i) => (
                    i == constructors.Roots.Count - 1 ? null : (JsExpr?)JsExpr.Binary(JsExpr.Identifier("$k"), "===", JsExpr.Literal(i.ToString())),
                    SuperCall(root, clause, implicitException))).ToList()));
            foreach (var start in state)
                statements.Add(Assign(JsExpr.ThisMember(start.Slot), start.RunsCode ? JsExpr.Identifier(Evaluated(start.Slot)) : start.Value)
                    with { Origin = start.Origin });
        }
        else
        {
            foreach (var start in state)
                statements.Add(Assign(JsExpr.ThisMember(start.Slot), start.Value) with { Origin = start.Origin });
        }

        if (single is not null)
            statements.AddRange(RootBody(single, constructors.Alternates));
        else
            foreach (var (root, i) in constructors.Roots.Select((root, i) => (root, i)))
                if (RootBody(root, constructors.Alternates) is { Count: > 0 } body)
                    statements.Add(JsStatement.If(JsExpr.Binary(JsExpr.Identifier("$k"), "===", JsExpr.Literal(i.ToString())),
                        JsStatement.Block(body), null));
        statements.AddRange(AlternateBodies(constructors.Alternates, arrived));
        return JsClassMember.Constructor(parameters, JsStatement.Block(statements));
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
    /// The constructors the twin reaches, and a refusal (EQ1009) for each one it cannot reach: every
    /// explicit constructor of a record or a struct was dropped before, in silence, and every one but
    /// the widest of a class.
    /// </summary>
    private Constructors ConstructorsOf(TypeDeclarationSyntax type)
    {
        // A record's copy constructor (its one parameter the record's own type) is what `with` copies
        // through in C#, and no `new` reaches it: taken for a branch, it met any other constructor of
        // one argument and refused the type (EQ1009), which compiled before. Nor does a `new` reach
        // the constructor only .NET's serialization calls (IsSerializationConstructor).
        var declared = type.Members.OfType<ConstructorDeclarationSyntax>()
            .Where(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword) && !IsCopyConstructor(type, constructor)
                && !IsSerializationConstructor(constructor))
            .ToList();

        var roots = new List<Root>();
        if (type.ParameterList is { } primary)
            roots.Add(new Root(primary.Parameters.ToList(), true, null, Arity.Of(primary.Parameters.ToList())));
        else
        {
            foreach (var constructor in declared.Where(constructor => !Chains(constructor)))
            {
                var arity = Arity.Of(constructor.ParameterList.Parameters.ToList());
                if (Clash(roots.Select(root => (Signature(type, root), root.Arity)), arity) is { } clash)
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
            if (Clash(taken, arity) is { } clash)
            {
                Refuse(type, constructor, arity, clash);
                continue;
            }
            alternates.Add(new Alternate(constructor, arity, target));
        }
        return new Constructors(roots, alternates);
    }

    /// <summary>
    /// Whether a constructor is the one only .NET's serialization calls: the <c>ISerializable</c>
    /// pattern's <c>(SerializationInfo info, StreamingContext context)</c>, which Visual Studio's
    /// exception template writes beside <c>()</c>, <c>(string)</c> and <c>(string, Exception)</c>. No
    /// <c>new</c> in the browser reaches it, since the browser has no <c>SerializationInfo</c>, so it is no
    /// branch of the twin, as a record's copy constructor is not. Taken for one, it met
    /// <c>(string, Exception)</c>, which takes as many arguments, and refused the class (EQ1009), so the
    /// template had to be edited before the class could build. Only the model can say the two types are
    /// .NET's: with none to ask, a constructor whose own types the app happened to name so is one C# can
    /// call, and it stays a branch, where matching the names erased it (Copilot's review of #708).
    /// </summary>
    private bool IsSerializationConstructor(ConstructorDeclarationSyntax constructor) =>
        constructor.ParameterList.Parameters.Count == 2
        && _modelFor(constructor)?.GetDeclaredSymbol(constructor) is IMethodSymbol { Parameters: [var info, var context] }
        && IsSerialization(info.Type, "SerializationInfo") && IsSerialization(context.Type, "StreamingContext");

    /// <summary>Whether <paramref name="type"/> is the type of <c>System.Runtime.Serialization</c> named
    /// <paramref name="name"/>.</summary>
    private static bool IsSerialization(ITypeSymbol type, string name) =>
        type is INamedTypeSymbol { Name: var declared, ContainingNamespace: var space, ContainingType: null }
        && declared == name && space.ToDisplayString() == "System.Runtime.Serialization";

    /// <summary>Whether a record's constructor is its copy constructor: one parameter, of the record's own
    /// type, asked of the model and, without one, of the type's name.</summary>
    private bool IsCopyConstructor(TypeDeclarationSyntax type, ConstructorDeclarationSyntax constructor)
    {
        if (type is not RecordDeclarationSyntax || constructor.ParameterList.Parameters is not [{ Type: { } parameter }]) return false;
        if (_modelFor(constructor) is { } model && model.GetDeclaredSymbol(type) is { } self)
            return SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(parameter).Type, self);
        return parameter.ToString() == type.Identifier.Text;
    }

    /// <summary>The first constructor already taken whose counts of arguments meet <paramref name="arity"/>.</summary>
    private static string? Clash(IEnumerable<(string Signature, Arity Arity)> taken, Arity arity) =>
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
        if (_modelFor(chain)?.GetSymbolInfo(chain).Symbol is not IMethodSymbol target)
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
        public JsExpr Test(string arrived)
        {
            var length = JsExpr.Member(JsExpr.Identifier(arrived), "length");
            if (High == int.MaxValue) return JsExpr.Binary(length, ">=", JsExpr.Literal(Low.ToString()));
            if (Low == High) return JsExpr.Binary(length, "===", JsExpr.Literal(Low.ToString()));
            return JsExpr.Binary(JsExpr.Binary(length, ">=", JsExpr.Literal(Low.ToString())), "&&",
                JsExpr.Binary(JsExpr.Member(JsExpr.Identifier(arrived), "length"), "<=", JsExpr.Literal(High.ToString())));
        }

        public override string ToString() => High == int.MaxValue ? $"{Low} or more" : Low == High ? $"{Low}" : $"{Low} to {High}";
    }

    /// <summary>
    /// An alternate's branch, which evaluates the `: this(…)` arguments where the alternate's own
    /// parameters are bound to what arrived, in the order C# evaluates them (as they are written, a
    /// named one where it is written), each into a temporary, and lands them in its root's parameters
    /// once they are all evaluated: an argument that reads a parameter of the root's name reads the
    /// alternate's own. The alternate's parameters live in a block of their own, which shadows the
    /// root's, and so do the variables the arguments declare (`out var n`), which were assigned with no
    /// declaration at all; the values cross the block in temporaries, so no function is needed to hold
    /// the C#. Null where the branch has nothing to do.
    /// </summary>
    private (JsExpr Test, List<JsStatement> Body)? Mapped(Alternate alternate, string arrived, JsStatement? selected)
    {
        var chain = alternate.Constructor.Initializer!;
        var (evaluated, landed) = Landed(chain, alternate.Target);
        if (landed.Count == 0 && selected is null) return null;
        var body = new List<JsStatement>();
        if (evaluated.Count > 0)
        {
            body.Add(JsStatement.Raw($"let {string.Join(", ", evaluated.Select((_, i) => $"$c{i}{Annotation}"))};"));
            var scoped = new List<JsStatement>(Bound(alternate.Constructor, arrived));
            foreach (var argument in chain.ArgumentList.Arguments)
                if (Declarations(argument.Expression) is { } declarations) scoped.Add(declarations);
            for (var i = 0; i < evaluated.Count; i++) scoped.Add(Assign($"$c{i}", evaluated[i]));
            body.Add(JsStatement.Block(scoped));
        }
        foreach (var (parameter, value) in landed) body.Add(Assign(parameter, value));
        if (selected is not null) body.Add(selected);
        return (alternate.Arity.Test(arrived), body);
    }

    /// <summary>
    /// What a `: this(…)` chain evaluates, in the order C# evaluates it, each into the temporary
    /// <c>$c</c> and its index, and what each of its root's parameters takes: the temporary of the
    /// argument that reaches it, its default where the chain leaves it out, which would otherwise hold
    /// whatever argument arrived in its place, and for a <c>params</c> one the array C# passes, its
    /// elements gathered when the chain lists them. Read from the bound tree (<see cref="BoundArguments"/>),
    /// and from the syntax in its order where there is no model to ask.
    /// </summary>
    private (IReadOnlyList<JsExpr> Evaluated, IReadOnlyList<(string Parameter, JsExpr Value)> Landed) Landed(
        ConstructorInitializerSyntax chain, Root root)
    {
        var parameters = root.Parameters;
        JsExpr Default(int i) => JsExpr.Opaque(parameters[i].Default is { } given
            ? _converter.ConvertExpression(given.Value, parameters[i].Type?.ToString())
            : DefaultOf(parameters[i].Type!));
        string Name(int i) => ParameterName(parameters[i], root.Primary);
        bool IsRest(int i) => parameters[i].Modifiers.Any(SyntaxKind.ParamsKeyword);
        JsExpr Temporary(int index) => JsExpr.Identifier($"$c{index}");

        if (BoundArguments.Of(_modelFor(chain)?.GetOperation(chain), argument => JsExpr.Opaque(_converter.ConvertExpression(argument)))
                is { } bound && bound.ByParameter.Count == parameters.Count)
        {
            var landed = parameters.Select((_, i) => (Parameter: Name(i), Value: bound.ByParameter[i] switch
            {
                null => IsRest(i) ? JsExpr.Array([]) : Default(i),
                [{ Spread: true } whole] => Temporary(whole.Written),
                var slots when IsRest(i) => JsExpr.Array(slots.Select(slot => Temporary(slot.Written)).ToList()),
                [var one, ..] => Temporary(one.Written),
                _ => Default(i),
            })).ToList();
            return (bound.Written, landed);
        }

        // No model: the arguments in the order they are written, each in the parameter it names or the
        // one in its position.
        var values = new JsExpr?[parameters.Count];
        var rest = parameters.Count > 0 && IsRest(parameters.Count - 1) ? parameters.Count - 1 : -1;
        var arguments = chain.ArgumentList.Arguments;
        var gathered = new List<JsExpr>();
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var value = JsExpr.Opaque(_converter.ConvertExpression(argument.Expression));
            var ordinal = argument.NameColon is { } named
                ? parameters.ToList().FindIndex(parameter => parameter.Identifier.ValueText == named.Name.Identifier.ValueText)
                : i;
            if (rest >= 0 && ordinal >= rest && argument.NameColon is null) gathered.Add(value);
            else if (ordinal >= 0 && ordinal < values.Length) values[ordinal] = value;
        }
        if (rest >= 0) values[rest] ??= JsExpr.Array(gathered);
        var evaluated = new List<JsExpr>();
        var bySyntax = new List<(string Parameter, JsExpr Value)>();
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is { } value)
            {
                evaluated.Add(value);
                bySyntax.Add((Name(i), Temporary(evaluated.Count - 1)));
            }
            else if (parameters[i].Default is not null)
                bySyntax.Add((Name(i), Default(i)));
        }
        return (evaluated, bySyntax);
    }

    /// <summary>
    /// A constructor's own parameters, declared with <c>let</c> in the block that reads them, each bound to
    /// its place among what arrived (<see cref="Bindings"/>). A <c>const</c> refused a body that assigns its
    /// own parameter (`raw = raw.Trim();` threw), which C# allows.
    /// </summary>
    private IEnumerable<JsStatement> Bound(ConstructorDeclarationSyntax constructor, string arrived)
    {
        var bindings = Bindings(constructor.ParameterList.Parameters.ToList(), arrived);
        if (bindings.Count == 0) yield break;
        // One declaration for all of them, as the twin wrote it before it was IR: the IR's `let`
        // declares one name.
        yield return JsStatement.Raw("let " + string.Join(", ", bindings.Select(binding =>
            $"{binding.Name}{Annotation} = {JsExprWriter.WriteIn(binding.Value, JsPrecedence.Assignment)}")) + ";");
    }

    /// <summary>
    /// Each parameter by the name every reference to it is converted to, and what it takes from what
    /// arrived: the argument in its place, its default where nothing arrived there, and for a
    /// <c>params</c> one every argument from its place on. By index, never by destructuring, which goes
    /// through the iterator protocol: measured in bun, `new CodeRange(caret)` took 12 ns destructuring
    /// <c>arguments</c> and 1.6 reading it by index.
    /// </summary>
    private IReadOnlyList<(string Name, JsExpr Value)> Bindings(IReadOnlyList<ParameterSyntax> parameters, string arrived) =>
        parameters.Select((parameter, i) =>
        {
            var name = ParameterName(parameter, primary: false);
            var argument = JsExpr.Index(JsExpr.Identifier(arrived), JsExpr.Literal(i.ToString()));
            if (parameter.Modifiers.Any(SyntaxKind.ParamsKeyword))
                return (name, JsExpr.Call(JsExpr.Member(JsExpr.Member(JsExpr.Member(JsExpr.Identifier("Array"), "prototype"), "slice"), "call"),
                    JsExpr.Identifier(arrived), JsExpr.Literal(i.ToString())));
            return parameter.Default is { } given
                ? (name, JsExpr.Conditional(JsExpr.Binary(argument, "===", JsExpr.Identifier("undefined")),
                    JsExpr.Opaque(_converter.ConvertExpression(given.Value, parameter.Type?.ToString())), argument))
                : (name, argument);
        }).ToList();

    /// <summary>The body each alternate runs after its root's, as C# runs a constructor that chains with
    /// `: this(…)`, with its own parameters bound to what arrived. It is the constructor's last
    /// statement, so its `return` ends it as it ends that constructor in C#.</summary>
    private IEnumerable<JsStatement> AlternateBodies(IReadOnlyList<Alternate> alternates, string arrived)
    {
        foreach (var alternate in alternates)
        {
            if (Body(alternate.Constructor) is not { Count: > 0 } body) continue;
            yield return JsStatement.If(alternate.Arity.Test(arrived),
                JsStatement.Block([.. Bound(alternate.Constructor, arrived), .. body]), null);
        }
    }

    /// <summary>
    /// A root's own body. A `return` in it ends that constructor in C# and nothing after it, where the
    /// body of an alternate chaining to it still runs, so a body that returns early, under an alternate
    /// with a body of its own, runs in a function of its own whose `return` ends it alone. C# allows no
    /// <c>await</c> and no <c>yield</c> in a constructor, so nothing in it changes meaning there.
    /// </summary>
    private IReadOnlyList<JsStatement> RootBody(Root root, IReadOnlyList<Alternate> alternates)
    {
        if (root.Explicit is not { } own || Body(own) is not { Count: > 0 } body) return [];
        var returns = own.Body?.DescendantNodes(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>().Any() == true;
        var followed = alternates.Any(alternate => alternate.Target == root && Body(alternate.Constructor).Count > 0);
        return returns && followed
            ? [JsStatement.Expression(JsExpr.Call(JsExpr.ArrowBlock("", JsStatement.Block(body), _layout, _converter.Depth + 1)))]
            : body;
    }

    /// <summary>A constructor's own statements, or none for an empty body.</summary>
    private IReadOnlyList<JsStatement> Body(ConstructorDeclarationSyntax constructor)
    {
        if (constructor.Body is { Statements.Count: > 0 } block)
            return _converter.ConvertBlockIr(block) switch
            {
                JsBlock converted => converted.Statements,
                var other => [other],
            };
        if (constructor.ExpressionBody is { } arrow)
            return _lowering.ExpressionBody(arrow.Expression, returns: false).Statements;
        return [];
    }

    /// <summary>
    /// The call a root makes to its base's constructor, as statements: with its own `: base(…)`
    /// arguments, or the base clause's (`record Circle(double Radius) : Shape(DefaultKind)`, evaluated
    /// where the primary constructor's parameters are in scope), as the bound tree binds them
    /// (<see cref="BoundArguments"/>). Each lands in its parameter's place and is evaluated in the order it
    /// is written, a named one out of the signature's order into a temporary first; the variables they
    /// declare (`out var n`) are declared before them. `: base("square", Color: "red")` was
    /// `super('square', 'red')`, handing Sides the color, and a base clause took every bare name for a
    /// forwarded parameter, so a constant was a variable nothing declared.
    /// </summary>
    private IReadOnlyList<JsStatement> SuperCall(Root root, PrimaryConstructorBaseTypeSyntax? clause, IMethodSymbol? implicitException)
    {
        if (root.Explicit?.Initializer is { } chain && chain.ThisOrBaseKeyword.IsKind(SyntaxKind.BaseKeyword))
        {
            var called = _modelFor(chain)?.GetOperation(chain);
            return Super(chain.ArgumentList, BoundArguments.Of(called, argument => JsExpr.Opaque(_converter.ConvertExpression(argument))),
                argument => _converter.ConvertExpression(argument), ExceptionBase(called));
        }
        if (clause?.ArgumentList is { } list)
        {
            var called = _modelFor(clause)?.GetOperation(clause);
            return Super(list, BoundArguments.Of(called, argument => JsExpr.Opaque(InPrimaryScope(argument))),
                InPrimaryScope, ExceptionBase(called));
        }
        // The implicit call of the base's parameterless constructor, which over an exception of .NET's
        // writes that constructor's own text (#611).
        return implicitException is not null ? ExceptionSuper(implicitException, bound: null) : [CallSuper([])];
    }

    /// <summary>
    /// The constructor of an exception of .NET's that a base call reaches, which the runtime's exception
    /// base stands for in the twin of an exception class of the app's (#611); null for any other.
    /// </summary>
    private static IMethodSymbol? ExceptionBase(IOperation? called) =>
        called is IInvocationOperation { TargetMethod: { MethodKind: MethodKind.Constructor } constructor }
        && ExceptionTypes.Is(constructor.ContainingType) && !ExceptionTypes.HasTwin(constructor.ContainingType)
            ? constructor
            : null;

    /// <summary>
    /// The constructor an IMPLICIT base call reaches, the base's parameterless one, where
    /// <paramref name="type"/> is an exception class of the app's directly over an exception no twin
    /// stands for, one of .NET's (#611): a constructor with no `: base(…)` and the implicit one call it.
    /// Null for any other type, and where no model can be asked.
    /// </summary>
    private IMethodSymbol? ImplicitExceptionBase(TypeDeclarationSyntax type) =>
        _modelFor(type)?.GetDeclaredSymbol(type) is INamedTypeSymbol { BaseType: { } baseType }
        && ExceptionTypes.Is(baseType) && !ExceptionTypes.HasTwin(baseType)
            ? baseType.InstanceConstructors.FirstOrDefault(constructor => constructor.Parameters.IsEmpty)
            : null;

    /// <summary>An argument of the base clause, which runs before `super()`, where the parameters are
    /// the constructor's own and `this` cannot be read: `: Base(X + 1)` wrote `super(this.x + 1)`.</summary>
    private string InPrimaryScope(ExpressionSyntax argument) =>
        _converter.WithConstructorParametersInScope(() => _converter.ConvertExpression(argument));

    private IReadOnlyList<JsStatement> Super(ArgumentListSyntax list, BoundArguments? bound, Func<ExpressionSyntax, string> convert,
        IMethodSymbol? exceptionBase = null)
    {
        var statements = list.Arguments.Select(argument => Declarations(argument.Expression)).OfType<JsStatement>().ToList();
        // No model: the arguments in the order they are written.
        if (bound is null)
            statements.Add(CallSuper(list.Arguments.Select(argument => JsExpr.Opaque(convert(argument.Expression))).ToList()));
        else if (exceptionBase is not null)
            statements.AddRange(ExceptionSuper(exceptionBase, bound));
        else if (bound.InWrittenOrder)
            statements.Add(CallSuper(bound.InParameterOrder()));
        else
        {
            statements.AddRange(bound.Written.Select((value, i) => JsStatement.Const($"$s{i}", value)));
            statements.Add(CallSuper(bound.InParameterOrder(i => JsExpr.Identifier($"$s{i}"))));
        }
        return statements;
    }

    /// <summary>
    /// The call of the runtime's exception base (<see cref="Eq.ExceptionBase"/>, #611), whose constructor
    /// takes what a <c>new</c> of the exception of .NET's the base call binds hands the runtime (#558):
    /// the argument bound to its <c>message</c>, with the text that constructor writes where none is
    /// given or the one given may be null (<see cref="ExceptionTypes.FrameworkText"/>), and what it takes
    /// besides, each by its parameter (<see cref="ExceptionTypes.Parts"/>), whatever their places in its
    /// signature (<c>ArgumentOutOfRangeException(paramName, message)</c>). So `: base("bad", name)` over
    /// <c>ArgumentException</c> reads "bad (Parameter 'x')" and its <c>ParamName</c> "x", and a class over
    /// <c>InvalidOperationException</c> that calls its base implicitly reads that type's own text, where
    /// both read only the message, or .NET's default for the class. Every argument is evaluated in the
    /// order it is written, each into a temporary of its own unless the call reads them in that order;
    /// one that is none of these runs and is carried nowhere. <paramref name="bound"/> is null for the
    /// implicit call, which passes nothing.
    /// </summary>
    private IReadOnlyList<JsStatement> ExceptionSuper(IMethodSymbol constructor, BoundArguments? bound)
    {
        var written = bound?.Written ?? [];
        IReadOnlyList<BoundArguments.Slot>? SlotsOf(string parameter) =>
            bound is not null && constructor.Parameters.FirstOrDefault(candidate => candidate.Name == parameter) is { } found
                ? bound.ByParameter[found.Ordinal]
                : null;
        var message = SlotsOf("message") is [{ Written: >= 0 } slot, ..] ? slot.Written : -1;
        // A framework constructor says what it takes by its parameters' names, as a `new` of it reads them;
        // an exception of a library's, which no twin stands for either, hands its inner exception alone.
        var framework = ExceptionTypes.IsFramework(constructor.ContainingType);
        var parts = (framework ? ExceptionTypes.Parts : [("innerException", "innerException")])
            .Select(part => (part.Member, Slots: SlotsOf(part.Parameter),
                IsParams: constructor.Parameters.Any(candidate => candidate.Name == part.Parameter && candidate.IsParams)))
            .Where(part => part.Slots is not null)
            .ToList();
        var reads = (message >= 0 ? [message] : Enumerable.Empty<int>())
            .Concat(parts.SelectMany(part => part.Slots!.Select(slot => slot.Written)));
        var direct = reads.SequenceEqual(Enumerable.Range(0, written.Count));
        JsExpr Read(int index) => direct ? written[index] : JsExpr.Identifier($"$s{index}");

        var text = framework && ExceptionTypes.FrameworkText(constructor) is { } fallback
            ? JsExpr.Literal(JsStringLiteral.Quote(fallback))
            : null;
        var given = message < 0 ? null : _modelFor(bound!.Sources[message])?.GetOperation(bound.Sources[message]);
        var messageValue = message < 0 ? text
            : text is null || given is not null && ExceptionTypes.NeverNull(given) ? Read(message)
            : given is { ConstantValue: { HasValue: true, Value: null } } ? text
            : JsExpr.Binary(Read(message), "??", text);

        var arguments = new List<JsExpr>();
        if (parts.Count > 0)
        {
            arguments.Add(messageValue ?? JsExpr.Identifier("undefined"));
            arguments.Add(JsExpr.Object(parts.Select(part => new JsProperty(part.Member, part.Slots switch
            {
                // An array passed whole to a params parameter is the array; elements C# packs into one are
                // gathered (`new AggregateException(a, b)`).
                [{ Spread: true } whole] => Read(whole.Written),
                var slots when part.IsParams => JsExpr.Array(slots!.Select(slot => Read(slot.Written)).ToList()),
                [var one, ..] => Read(one.Written),
                _ => JsExpr.Identifier("undefined"),
            })).ToList()));
        }
        else if (messageValue is not null)
            arguments.Add(messageValue);
        var statements = direct ? [] : written.Select((value, i) => JsStatement.Const($"$s{i}", value)).ToList<JsStatement>();
        statements.Add(CallSuper(arguments));
        return statements;
    }

    private static JsStatement CallSuper(IReadOnlyList<JsExpr> arguments) =>
        JsStatement.Expression(JsExpr.Call(JsExpr.Identifier("super"), arguments));

    /// <summary>The declarations of the variables <paramref name="expression"/> declares (`out var n`),
    /// or null for none.</summary>
    private JsStatement? Declarations(ExpressionSyntax expression) =>
        ExpressionVariableScanner.Declarations(expression, _annotations).Trim() is { Length: > 0 } declared
            ? JsStatement.Raw(declared)
            : null;

    /// <summary>
    /// The branches as one chain, each tested in turn and the first that holds taken: a branch with no
    /// test is the last one, taken whatever arrived.
    /// </summary>
    private static JsStatement Chain(IReadOnlyList<(JsExpr? Test, IReadOnlyList<JsStatement> Body)> branches)
    {
        JsStatement? rest = null;
        for (var i = branches.Count - 1; i >= 0; i--)
        {
            var (test, body) = branches[i];
            rest = test is null ? JsStatement.Block(body) : JsStatement.If(test, JsStatement.Block(body), rest);
        }
        return rest ?? JsStatement.Block([]);
    }

    /// <summary>The local a member's initializer is evaluated into before its base's constructor runs.</summary>
    private static string Evaluated(string slot) => "$$" + slot;

    /// <summary>Where a root stands among the roots.</summary>
    private static int Index(Constructors constructors, Root root) =>
        constructors.Roots.Select((candidate, i) => (candidate, i)).First(pair => pair.candidate == root).i;

    /// <summary>Whether TypeScript reads a member's type as one that may hold null.</summary>
    private static bool Nullable(string tsType) =>
        tsType == "any" || tsType.Split('|').Any(part => part.Trim() is "null" or "undefined");

    private string Annotation => _annotations ? ": any" : "";

    private static JsStatement Assign(string name, JsExpr value) => Assign(JsExpr.Identifier(name), value);

    private static JsStatement Assign(JsExpr target, JsExpr value) =>
        JsStatement.Expression(JsExpr.Binary(target, "=", value));

    /// <summary>
    /// The name a constructor parameter is bound under, the one every reference to it is converted to:
    /// a primary constructor's camelCased, as a member it makes, and an explicit constructor's as
    /// written, each made a legal JavaScript name (`class` is `class$`).
    /// </summary>
    internal static string ParameterName(ParameterSyntax parameter, bool primary) =>
        (primary ? parameter.Identifier.ValueText.ToCamelCase() : parameter.Identifier.ValueText).ToJsIdentifier();

    /// <summary>An initializer, converted in the constructor's parameter scope: a positional parameter
    /// it reads (`Tag = "#" + Id`) is the parameter itself, since no member is set yet.</summary>
    private string Initialized(ExpressionSyntax initializer, TypeSyntax type) =>
        ExpressionVariableScanner.Scoped(initializer, _converter.WithConstructorParametersInScope(
            () => _converter.ConvertExpression(initializer, type.ToString())), _annotations);

    /// <summary>
    /// The default of a declared type, asked of the SYMBOL where one is available. The syntax-only
    /// fallback cannot see through a name: it answers <c>null</c> for <c>char</c> and for every enum,
    /// where C# gives <c>'\0'</c> and the zero-valued member. It goes through the converter, which
    /// registers every struct the zero constructs for this module's imports.
    /// </summary>
    private string DefaultOf(TypeSyntax type) =>
        _modelFor(type)?.GetTypeInfo(type).Type is { } symbol
            ? _converter.DefaultOf(symbol)
            : TypeDeclarationExtensions.DefaultFor(type);
}
