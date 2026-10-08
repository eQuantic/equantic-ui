using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for object creation (new T() or new()).
/// Handles:
/// - <c>List&lt;T&gt;</c> -> []
/// - HtmlNode -> {} (UI config)
/// - UI Components -> new Component(config) or just config
/// <para>
/// Built as IR, its arguments and its initializer's values each their own node, so a lambda a
/// construction holds (a node's handler, a rule's test) reaches the writer as an arrow whose block
/// maps line by line (#492). The arguments were spliced as text, and every line of such a lambda's
/// block went with them.
/// </para>
/// </summary>
public class ObjectCreationStrategy : IExpressionIrStrategy
{
    private static readonly JsExpr Undefined = JsExpr.Identifier("undefined");

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ObjectCreationExpressionSyntax || node is ImplicitObjectCreationExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        // C# 13's `System.Threading.Lock` — single-threaded JS drops the lock STATEMENT's
        // semantics already (the body just runs); the gate object itself is inert, and emitting
        // `new Lock()` named a class no browser has.
        if (context.SemanticHelper.GetType(node)?.ToDisplayString() == "System.Threading.Lock")
            return JsExpr.Object([]);

        // `new object()` — a value with nothing but its identity, the gate a `lock` takes or a
        // sentinel: the runtime's, since a plain `{}` is an anonymous type here, compared by its
        // members, so two of them were Equal and one dictionary key. `new object()` named a class
        // JavaScript does not have, and threw where it ran (#478). An empty initializer, the only one
        // an object can take, changes nothing: `new object() { }` and `new() { }` are the same value.
        if (context.SemanticHelper.GetType(node) is { SpecialType: SpecialType.System_Object }
            && node is BaseObjectCreationExpressionSyntax { Initializer: null or { Expressions.Count: 0 } })
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.NewObject));
        }

        // `new string(chars)` and `new string(c, n)`: the text they build, where `new string(…)` named a
        // class JavaScript does not have (#524).
        if (context.SemanticHelper.GetType(node) is { SpecialType: SpecialType.System_String }
            && context.SemanticHelper.GetOperation(node) is Microsoft.CodeAnalysis.Operations.IObjectCreationOperation creation
            && NewString(creation, context) is { } built)
            return built;

        if (node is ObjectCreationExpressionSyntax objCreation)
        {
            return ConvertExplicit(objCreation, context);
        }
        else if (node is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            return ConvertImplicit(implicitCreation, context);
        }
        throw new InvalidOperationException("Invalid node type");
    }

    /// <summary>
    /// The text a string constructor builds: its chars joined (none for a null array, as .NET's
    /// <c>new string((char[])null)</c> is empty), a char repeated, or a range of chars joined. Null
    /// for an overload with no such reading here (a span, a pointer). Each argument is placed by the
    /// parameter it binds to, so a named argument written out of order fills its own, and the parts
    /// keep their written order, the order C# evaluates them in, which the template writer preserves
    /// where the holes follow another.
    /// </summary>
    private static JsExpr? NewString(Microsoft.CodeAnalysis.Operations.IObjectCreationOperation creation,
        ConversionContext context)
    {
        if (creation.Constructor is not { } constructor) return null;
        var template = constructor.Parameters.Select(parameter => parameter.Type).ToArray() switch
        {
            [IArrayTypeSymbol] => "({0} ?? []).join('')",
            [{ SpecialType: SpecialType.System_Char }, { SpecialType: SpecialType.System_Int32 }] => "{0}.repeat({1})",
            // The range refused where it leaves the array, as .NET refuses it: `slice` clamped it. A null
            // array is refused by its parameter's name, `value`.
            [IArrayTypeSymbol, { SpecialType: SpecialType.System_Int32 }, { SpecialType: SpecialType.System_Int32 }]
                => $"{Eq.TextChars}({{0}}, {{1}}, {{2}}, 'value').join('')",
            _ => null,
        };
        if (template is null || creation.Arguments.Length != constructor.Parameters.Length) return null;
        if (template.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
        var parts = new List<JsExpr>();
        var slots = new int[constructor.Parameters.Length];
        foreach (var argument in creation.Arguments)
        {
            if (argument.Parameter is not { } parameter || argument.Value.Syntax is not ExpressionSyntax value) return null;
            slots[parameter.Ordinal] = parts.Count;
            parts.Add(context.Converter.ConvertIr(value));
        }
        var placed = System.Text.RegularExpressions.Regex.Replace(template, @"\{(\d)\}",
            hole => $"{{{slots[hole.Groups[1].Value[0] - '0']}}}");
        return JsExpr.Template(placed, parts, context.TypeAnnotations);
    }

    private JsExpr ConvertExplicit(ObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var typeName = creation.Type.ToString();
        // TYPE ARGUMENTS are erased at runtime, and `new Bucket<string>()` is not even valid JS —
        // `<` parses as a comparison. The generic COLLECTIONS below still match on their full text,
        // so the erasure happens after they have had their say.
        var genericTypeName = creation.Type is GenericNameSyntax generic
            ? generic.Identifier.Text
            : null;
        // A qualified creation (`new eQuantic.UI.Primitives.Image(...)` — the disambiguation idiom
        // when a section class shares a name) must construct the IMPORTED simple name: module
        // bindings exist in JS, namespace chains do not. The List/Dictionary specials below keep
        // matching (they see the simple generic name).
        if (creation.Type is QualifiedNameSyntax qualifiedName)
            typeName = qualifiedName.Right.ToString();
        else if (creation.Type is AliasQualifiedNameSyntax aliasQualified)
            typeName = aliasQualified.Name.ToString();
        var createdType = context.SemanticHelper.GetType(creation);

        // A HOST-ONLY type constructed from client code. `new Matrix2D(...)` compiled, emitted an
        // import of a name the runtime deliberately ships no export for, and took the page down at
        // hydration — while the static-member read beside it (`Matrix2D.Identity`) was already
        // fenced, through MemberAccessStrategy. The fence's doc counts the branches that owe it
        // this call; construction was one it did not name.
        if (createdType.ReportIfHostOnlyType(creation, context)) return Undefined;

        // An in-tree creation whose TYPE an authoritative model cannot bind is the same story as
        // an unbound call (EQ2006): missing references or code that doesn't compile — emitting
        // `new Whatever()` and inventing an import for it is how a typo shipped as a browser
        // ReferenceError. Guessing stays legal where it is honest.
        if (createdType is null or IErrorTypeSymbol && !context.CanGuess(creation.Type))
        {
            context.Report(creation, ConversionSeverity.Error, "EQ2006",
                $"'{typeName}' does not bind in the compiler's semantic model, so `new {typeName}(…)` "
                + "would be a guess. Either this code does not compile, or the compiler is missing "
                + "references/generated sources — the SDK passes them via --refs/--generated; a "
                + "custom host must do the same.");
        }

        // `new T()` where T is a generic type parameter cannot be transpiled: JS erases generic type
        // arguments, so the concrete constructor is unknown at runtime (emitting `new T()` would throw
        // "T is not defined"). Fail the build with guidance instead of shipping broken code.
        if (createdType is ITypeParameterSymbol)
        {
            context.Report(creation, ConversionSeverity.Error, "EQ2003",
                $"Cannot instantiate type parameter '{typeName}' with `new {typeName}()` — generic type " +
                "arguments are erased at runtime in JavaScript, so the concrete type is unknown. Pass a " +
                "factory (e.g. Func<T>) or the constructed value as a parameter instead.");
            return Undefined;
        }

        // A value the browser holds as DATA (`[TwinIsData]`, `Color`) is built as its data:
        // `new Color(1, 2, 3, 4)` is `{ r: 1, g: 2, b: 3, a: 4 }`. Asked of the SYMBOL, so an app's
        // own type named `Color` is built as the app's (it was matched by name, #494).
        if (createdType is INamedTypeSymbol data && data.TwinIsData())
            return DataConstruction(creation, data, context);

        // Records and user structs are emitted as named JS classes (they carry instance methods) —
        // construct via `new` with the arguments the constructor binds, then apply any object
        // initializer to what it built (#413).
        if (createdType is { IsRecord: true }
            || (createdType is { TypeKind: TypeKind.Struct } && createdType.IsStructuralValueType()))
        {
            return BuildValueTypeConstruction(creation, createdType, context);
        }

        // A plain class eqc writes is built as a record is (#582, #583): the constructor the call binds,
        // with its arguments as the bound tree binds them, then its initializer applied to what it built.
        // It took an object initializer as a trailing config object, an argument of its constructor, so
        // the values were evaluated before the constructor ran and its initializers moved them.
        if (createdType is INamedTypeSymbol plain && IsBuiltAsCSharp(plain))
            return BuildClassConstruction(creation, plain, context);

        var arguments = new List<JsExpr>();
        var emittedSlots = 0;

        if (creation.ArgumentList != null && creation.ArgumentList.Arguments.Count > 0)
        {
            var ordered = OrderedArguments(creation, context);
            emittedSlots = ordered.Count;
            arguments.AddRange(ordered);
        }

        // A COLLECTION initializer (`new Column(gap) { a, b }`) is Add-per-element in C# — it must
        // go through the twin's add(), never ride the trailing CONFIG slot: an array Object.assigns
        // its INDICES onto the node, `children` stays empty, and nothing says so at build time.
        // Collections themselves (List/Dictionary/…) keep their literal lowering below.
        if (creation.Initializer?.Kind() == SyntaxKind.CollectionInitializerExpression
            && !IsCollectionLikeTypeName(typeName))
        {
            return AddPerElementConstruction(
                creation.Initializer, JsExpr.New(JsExpr.Identifier(genericTypeName ?? typeName), arguments), context);
        }

        // A list built with an element an EXTENSION adds (`new List<string> { 1 }` over
        // `Add(this List<string>, int)`) is applied as C# applies it, each element through its own
        // `Add` (ObjectInitializer), over the list the constructor built: as the list's literal, every
        // element was the list's own, 1 where .NET holds "#1".
        if (ListAddedByAnExtension(creation.Initializer, createdType, context) is { } listed)
            return ObjectInitializer.Apply(
                arguments.Count == 0 || IsCapacityArgument(creation, context) ? JsExpr.Array([]) : JsExpr.Array([JsExpr.Spread(arguments[0])]),
                listed, context);

        // An object initializer that ADDS to what a member holds (`Items = { 1, 2 }`) or writes an
        // entry (`[k] = v`) is applied once the object exists (#462): a config object can only
        // replace a member, and the list the member was initialized with was gone. Only for a class
        // whose twin eqc writes; the vocabulary's hand-written twins take their config as they always did.
        if (creation.Initializer is { } applied && TwinIsWritten(createdType)
            && applied.IsKind(SyntaxKind.ObjectInitializerExpression) && !ObjectInitializer.OnlyAssigns(applied))
        {
            return ObjectInitializer.Apply(JsExpr.New(JsExpr.Identifier(genericTypeName ?? typeName), arguments), applied, context);
        }

        JsExpr? initializer = null;
        var assignInitializerAfterConstruction = false;
        if (creation.Initializer != null)
        {
            initializer = context.Converter.ConvertIr(creation.Initializer);
            // The config object lands in the constructor's TRAILING config slot — when the call
            // site supplied fewer positional arguments than the resolved constructor's arity, the
            // skipped parameters fill from their C# defaults first (`new Stack { Width = … }` must
            // emit `new Stack('topStart', {…})`, never the config in the align slot).
            // Fill from the slots actually EMITTED, not the syntactic argument count: named
            // arguments that skip earlier parameters (`new Positioned(x, bottom: 0, start: 0)`)
            // already emitted the skipped defaults, and counting the call site again would append
            // them twice — pushing the config past the constructor's arity, where it is silently
            // dropped.
            if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol ctor)
            {
                if (emittedSlots < ctor.Parameters.Length)
                    arguments.AddRange(ctor.Parameters.Skip(emittedSlots).Select(parameter => DefaultOf(parameter, context)));
                arguments.Add(initializer);
            }
            else if (emittedSlots > 0)
            {
                // No resolvable constructor (standalone CompileSource, no references — the
                // playground's mode): the arity is unknowable, so the trailing slot is a trap —
                // `new Text(content, role) { Tabular = true }` landed the config in the COLOR
                // parameter. Object.assign after construction is the C# initializer's exact
                // semantics and needs no arity. (Constructor-side normalization of config values
                // does not run for these — acceptable next to a guaranteed mis-slot.)
                assignInitializerAfterConstruction = true;
            }
            else
            {
                // No positional arguments: the config as the sole argument is the dominant
                // constructor contract (`Component(props)`), and correct regardless of arity.
                arguments.Add(initializer);
            }
        }

        // Special handling for Collections (handle both short and fully-qualified names).
        //
        // The single argument means one of TWO opposite things, and only the semantic model can say
        // which: `new List<T>(capacity)` is an empty list sized ahead, `new List<T>(source)` is a
        // copy. Passing it straight through emitted the capacity AS the list — `var lines = 7;`
        // followed by `lines.push(...)`, which throws — and nothing said so at build time.
        if (typeName.StartsWith("List<") || typeName.Contains(".List<")
            || typeName.StartsWith("IEnumerable<") || typeName.Contains(".IEnumerable<"))
        {
            if (arguments.Count == 0 || arguments is [JsObject { Properties.Count: 0 }]) return JsExpr.Array([]);
            if (IsCapacityArgument(creation, context)) return JsExpr.Array([]);
            // A copy of the source, not an alias of it; a dictionary spreads into its pairs.
            if (creation.Initializer == null && creation.ArgumentList?.Arguments.Count == 1)
                return JsExpr.Array([JsExpr.Spread(arguments[0])]);
            return Spliced(arguments);
        }
        
        // `new string(c, count)` — the padding idiom (`new string(' ', indentWidth)`). There is no
        // String constructor in JS that means this; `repeat` is what it means.
        if (typeName == "string" && creation.ArgumentList?.Arguments.Count == 2)
        {
            var parts = OrderedArguments(creation, context);
            return JsExpr.Call(JsExpr.Member(parts[0], "repeat"), parts[1]);
        }

        // HtmlNode -> Plain Object
        if (typeName == "HtmlNode")
        {
            return arguments.Count == 0 ? JsExpr.Object([]) : Spliced(arguments);
        }
        
        // RenderContext -> Mock or Plain Object (since it's a TS interface)
        if (typeName == "RenderContext")
        {
            return JsExpr.Opaque("{ getService: () => null }");
        }

        // An exception the model cannot see: ExceptionCreationStrategy builds every one it can, from
        // its symbol, so here the name is all there is to go on, and it is rooted at System.Exception.
        if (createdType is null or IErrorTypeSymbol && typeName.EndsWith("Exception"))
        {
            IReadOnlyList<string> chain = typeName == "Exception" ? ["System.Exception"] : [typeName, "System.Exception"];
            return ExceptionTypes.Construction(chain, creation, context);
        }

        if (assignInitializerAfterConstruction)
            return JsExpr.Call(JsExpr.Identifier("Object.assign"),
                JsExpr.New(JsExpr.Identifier(genericTypeName ?? typeName), arguments), initializer!);
        return JsExpr.New(JsExpr.Identifier(genericTypeName ?? typeName), arguments);
    }

    /// <summary>
    /// A list's collection initializer one of whose elements the bound tree adds through an EXTENSION
    /// method or a C# 14 extension block's member, or null: such a list is applied element by element,
    /// where its literal could only hold every element as the list's own <c>Add</c> holds it.
    /// </summary>
    private static InitializerExpressionSyntax? ListAddedByAnExtension(InitializerExpressionSyntax? initializer, ITypeSymbol? type,
        ConversionContext context) =>
        initializer is { RawKind: (int)SyntaxKind.CollectionInitializerExpression }
        && type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"
        && initializer.Expressions.Any(element => context.SemanticHelper.CollectionInitializerMethod(element)
            is { IsExtensionMethod: true } or { ContainingType.IsExtension: true })
            ? initializer
            : null;

    /// <summary>
    /// Values that stand where ONE expression is read, as they always stood: the only one, or, where
    /// a creation's arguments and its initializer meet in a collection's literal, all of them
    /// spliced in as text. That second shape is a comma expression and was never right: kept as it
    /// was, for a change that is about where lines map rather than what a collection holds.
    /// </summary>
    private static JsExpr Spliced(IReadOnlyList<JsExpr> values) =>
        values.Count == 1 ? values[0] : JsExpr.Opaque(string.Join(", ", values.Select(JsExprWriter.Write)));

    /// <summary>A parameter's C# default as the literal a skipped argument is filled with
    /// (<see cref="DefaultLiteralFor"/>).</summary>
    private static JsExpr DefaultOf(IParameterSymbol parameter, ConversionContext context) =>
        JsExpr.Literal(DefaultLiteralFor(parameter, context));

    /// <summary>Type names whose creations lower to JS literals (an array or an object) — a collection
    /// initializer on THESE is the literal itself, never Add-per-element on a constructed node. A set is
    /// HashSetStrategy's.</summary>
    private static bool IsCollectionLikeTypeName(string typeName) =>
        typeName.StartsWith("List<") || typeName.Contains(".List<")
        || typeName.StartsWith("IEnumerable<") || typeName.Contains(".IEnumerable<")
        || typeName.Contains("Collection<");

    /// <summary>
    /// C# collection-initializer semantics, exactly: construct, then one <c>add(…)</c> per element —
    /// <c>(($n) =&gt; { $n.add(a); $n.add(b); return $n; })(new Column(12))</c>. A two-expression
    /// element (<c>{ key, value }</c>) is the two-argument Add overload. Works for every class with
    /// an Add: the vocabulary twins ship <c>add()</c>, and a user class transpiles its own.
    /// <para>
    /// The arrow is a block of statements, each <c>add</c> carrying the element it adds, so a
    /// breakpoint on a child's line binds and a lambda among the elements keeps its lines (#492).
    /// The elements convert one level in, where the block lays them out.
    /// </para>
    /// </summary>
    internal static JsExpr AddPerElementConstruction(
        InitializerExpressionSyntax initializer, JsExpr construction, ConversionContext context)
    {
        var node = JsExpr.Identifier("$n");
        var adds = context.Converter.InBlock(() => initializer.Expressions
            .Select(element => JsStatement.Expression(JsExpr.Call(JsExpr.Member(node, "add"),
                element is InitializerExpressionSyntax pair
                    ? pair.Expressions.Select(e => context.Converter.ConvertIr(e)).ToList()
                    : [context.Converter.ConvertIr(element)])) with { Origin = element })
            .ToList());
        return JsExpr.Call(JsExpr.ArrowBlock("$n", JsStatement.Block([.. adds, JsStatement.Return(node)]),
            context.Layout, context.Depth), construction);
    }

    /// <summary>
    /// Converts a creation's arguments in the CONSTRUCTOR's parameter order. JS has no named arguments,
    /// so `new Button("x", onPressed: f)` must emit `f` at the parameter's real position with the
    /// skipped parameters filled from their C# defaults (`'primary'`, `'medium'`) — emitting call-site
    /// order would silently bind values to the wrong parameters. Without a resolvable constructor
    /// symbol or named arguments, syntactic order passes through untouched.
    /// </summary>
    /// <summary>
    /// Is the single argument a CAPACITY (an int) rather than a source collection or a comparer?
    /// Answered from the resolved constructor, not from the way the expression looks: `new
    /// List&lt;string&gt;(other.Count)` and `new List&lt;string&gt;(other)` are one character apart.
    /// </summary>
    private static bool IsCapacityArgument(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        if (creation.ArgumentList?.Arguments.Count != 1) return false;
        if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol { Parameters.Length: 1 } ctor)
            return ctor.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
        // No symbol (a partial semantic model): an integer literal is still unambiguous.
        return context.SemanticHelper.GetType(creation.ArgumentList.Arguments[0].Expression)
            is { SpecialType: SpecialType.System_Int32 };
    }

    /// <summary>
    /// A construction of a value the browser holds as data: each argument fills the member its
    /// parameter is named for (a positional record's parameter IS its member), the initializer the
    /// members it assigns after them, and every other member starts as its type's default. A parameter
    /// with no member of its name has nowhere to go, and dropping its value would build a wrong colour
    /// in silence, so it stops the build.
    /// <para>
    /// C# evaluates the arguments in the order they are written and the initializer after them, and a
    /// literal evaluates its members in ITS order. Where those differ and a value may have an effect (a
    /// named argument out of place, an argument the initializer then overwrites), every value is bound
    /// first, in the written order, and the literal reads the bindings.
    /// </para>
    /// </summary>
    private static JsExpr DataConstruction(BaseObjectCreationExpressionSyntax creation, INamedTypeSymbol data, ConversionContext context) =>
        // Plain data holds numbers and strings, never a lambda, so its values stay text for now.
        JsExpr.Opaque(DataLiteral(creation, data, context));

    private static string DataLiteral(BaseObjectCreationExpressionSyntax creation, INamedTypeSymbol data, ConversionContext context)
    {
        var members = data.DataMembers().Select(member => member.Name).ToList();
        var written = new List<(string Member, string Js, ExpressionSyntax Source)>();
        if (creation.ArgumentList is { } list && context.SemanticHelper.GetSymbol(creation) is IMethodSymbol ctor)
        {
            for (var i = 0; i < list.Arguments.Count; i++)
            {
                var argument = list.Arguments[i];
                // A positional argument's slot is its list position: C# allows one after a named
                // argument only when that one sits in its own position.
                var parameter = argument.NameColon is { } named
                    ? ctor.Parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.Text)
                    : i < ctor.Parameters.Length ? ctor.Parameters[i] : null;
                if (parameter is null) continue;
                if (!members.Contains(parameter.Name))
                {
                    context.Report(argument, ConversionSeverity.Error, "EQ1004",
                        $"'{data.Name}' is plain data in the browser, built from its members by name, and its "
                        + $"constructor's parameter '{parameter.Name}' names none of them.");
                    return "undefined";
                }
                written.Add((parameter.Name, context.Converter.ConvertExpression(argument.Expression), argument.Expression));
            }
        }
        if (creation.Initializer is { } initializer)
            foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                if (context.SemanticHelper.GetSymbol(assignment.Left) is { } member && members.Contains(member.Name))
                    written.Add((member.Name, context.Converter.ConvertExpression(assignment.Right), assignment.Right));

        var order = written.Select(value => members.IndexOf(value.Member)).ToList();
        var inOrder = order.Distinct().Count() == order.Count && order.SequenceEqual(order.OrderBy(index => index));
        if (inOrder || written.All(value => HasNoEffect(value.Source, context)))
        {
            var last = written.GroupBy(value => value.Member).ToDictionary(group => group.Key, group => group.Last().Js);
            return TwinData.Literal(data,
                member => last.TryGetValue(member.Name, out var value) ? value : null,
                type => DefaultValue.Of(type, context));
        }

        // `$0`, `$1`…: no C# name can take one, so nothing a value names is shadowed.
        var bound = written.Select((value, index) => (value.Member, Name: $"${index}")).ToList();
        var lastBound = bound.GroupBy(value => value.Member).ToDictionary(group => group.Key, group => group.Last().Name);
        var literal = TwinData.Literal(data,
            member => lastBound.TryGetValue(member.Name, out var name) ? name : null,
            type => DefaultValue.Of(type, context));
        return $"(({string.Join(", ", bound.Select(value => value.Name))}) => ({literal}))({string.Join(", ", written.Select(value => value.Js))})";
    }

    /// <summary>Whether evaluating the expression can do nothing but produce its value: a constant, or
    /// a name bound to a local, a parameter or a field.</summary>
    private static bool HasNoEffect(ExpressionSyntax expression, ConversionContext context) =>
        context.SemanticHelper.TryGetConstantValue(expression, out _)
        || expression is IdentifierNameSyntax && context.SemanticHelper.GetSymbol(expression) is ILocalSymbol or IParameterSymbol or IFieldSymbol;

    private static IReadOnlyList<JsExpr> OrderedArguments(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var args = creation.ArgumentList!.Arguments;
        var converted = args.Select(a => context.Converter.ConvertIr(a.Expression)).ToList();
        var symbol = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;

        // A COMPONENT's constructor loses its dependencies on the way out — the emitted one resolves
        // them from the container instead — so every argument standing in a dependency's place has
        // to go with them, or the ones after it slide into the wrong parameters. That was
        // `new Quark(clock, mood, size)` arriving as `(mood = clock, size = 'happy')`, which is not
        // a type error in either language and shows up as `dp.toFixed is not a function`.
        //
        // It runs through the SLOT path rather than beside it, so the drop is by PARAMETER and the
        // form a developer chose — positional, named, mixed, or omitted — cannot change the answer.
        var dropping = symbol is not null && DeclaresDependency(symbol);
        if (!dropping && !args.Any(a => a.NameColon != null)) return converted;

        if (symbol is not { } ctor) return converted;

        var slots = new JsExpr?[ctor.Parameters.Length];
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i].NameColon?.Name.Identifier.Text;
            // A positional argument's slot is its LIST position — C# only allows positionals after
            // a named argument when the named one sits IN its own position, so the list index IS
            // the parameter index (an independent counter clobbered slot 0 in that mixed form).
            var ordinal = name == null
                ? i
                : ctor.Parameters.FirstOrDefault(p => p.Name == name)?.Ordinal ?? -1;
            if (ordinal >= 0 && ordinal < slots.Length) slots[ordinal] = converted[i];
        }

        // A dependency slot is not filled with a default — it is REMOVED, because the parameter
        // it stood in front of is gone from the emitted signature too.
        var keep = Enumerable.Range(0, slots.Length)
            .Where(i => !(dropping && CapabilityRule.IsDependency(ctor.Parameters[i].Type)))
            .ToList();

        var lastSet = keep.FindLastIndex(i => slots[i] != null);
        var ordered = new List<JsExpr>();
        for (var k = 0; k <= lastSet; k++)
            ordered.Add(slots[keep[k]] ?? DefaultOf(ctor.Parameters[keep[k]], context));
        return ordered;
    }

    /// <summary>
    /// Whether this constructor belongs to a COMPONENT and declares a dependency — the only case
    /// where the emitted signature differs from the one the call site was written against.
    /// </summary>
    private static bool DeclaresDependency(IMethodSymbol ctor) =>
        IsComponent(ctor.ContainingType) && ctor.Parameters.Any(p => CapabilityRule.IsDependency(p.Type));

    private static bool IsComponent(INamedTypeSymbol? symbol)
    {
        for (var b = symbol; b is not null; b = b.BaseType)
            if (b.Name is "StatelessComponent" or "StatefulComponent" or "UiComponent") return true;
        return false;
    }

    /// <summary>The TS literal for a parameter's C# default value: the constant it is in the parameter's
    /// type (<see cref="ConstantLiteral"/>), an enum's in the enum's representation. Shared with
    /// InvocationStrategy (named INVOCATION arguments reorder the same way creations do).</summary>
    internal static string DefaultLiteralFor(IParameterSymbol parameter, ConversionContext context)
    {
        // A non-nullable STRUCT parameter defaulted with `= default` (BoxStyle, EdgeInsets…) must
        // fill as `undefined`, never `null`: the hand-written twin declares its own default
        // (`style: BoxStyle = new BoxStyle()`), which `undefined` triggers and an explicit `null`
        // silently bypasses — the null then walks into the lowering as a style.
        if (parameter.HasExplicitDefaultValue && parameter.ExplicitDefaultValue is null
            && parameter.Type is { IsValueType: true }
            && parameter.Type.OriginalDefinition?.SpecialType != SpecialType.System_Nullable_T)
            return "undefined";
        if (!parameter.HasExplicitDefaultValue || parameter.ExplicitDefaultValue is null) return "null";

        // The constant in the parameter's type: a decimal default was written as a number, a long as a
        // number, a float as its own shortest text, a char with no quotes (a bare identifier), a string
        // with a quote in it as a broken literal, and a [Flags] member as a name its enum never holds.
        return ConstantLiteral.Write(parameter.ExplicitDefaultValue, parameter.Type, context) ?? "null";
    }

    /// <summary>
    /// Whether eqc writes the twin of <paramref name="type"/>: one the source declares, one from a
    /// namespace it transpiles whole into the runtime, or one the vocabulary marks
    /// <c>[TwinIsTranspiled]</c> (#592). Its constructor is the C# constructor and its methods are the
    /// type's; a vocabulary twin is hand-written and takes a trailing config object.
    /// </summary>
    internal static bool TwinIsWritten(ITypeSymbol? type) =>
        type is not null
        && (type.Locations.Any(location => location.IsInSource)
            || type.TwinIsTranspiled()
            || Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(type.ContainingNamespace?.ToDisplayString() ?? string.Empty));

    /// <summary>
    /// A record or a struct built as C# builds it (#413): the constructor the call binds, with its
    /// arguments in its parameters' order, then the object initializer applied to what it built
    /// (<see cref="ObjectInitializer"/>). The twin's constructor is the C# constructor, so it runs
    /// every initializer itself; a member's value was a constructor argument once, which made an
    /// initializer the default of a parameter, run only when the initializer did not set the member.
    /// <para>
    /// A struct built by its implicit parameterless constructor is its zero, as C# builds it: none of
    /// its initializers runs, and none of its constructors, so it is the default the type has.
    /// </para>
    /// </summary>
    private static JsExpr BuildValueTypeConstruction(BaseObjectCreationExpressionSyntax creation, ITypeSymbol type, ConversionContext context)
    {
        var ctor = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;

        var constructed = JsExpr.Identifier(type.Name);
        // The abstract VOCABULARY (BoxStyle, EdgeInsets, …) is runtime-provided by a HAND-WRITTEN TS
        // twin that takes a trailing CONFIG OBJECT, the shape UI-component classes accept
        // (`new Row(gap, { height: … })`). Positional args pass through.
        if (!TwinIsWritten(type)
            && Services.RuntimeProvidedTypeScanner.IsVocabularyNamespace(type.ContainingNamespace?.ToDisplayString() ?? string.Empty))
        {
            var parts = new List<JsExpr>();
            if (creation.ArgumentList != null)
                parts.AddRange(OrderedArguments(creation, context));
            var specs = HydrationSpecs(creation, type, context);
            if (creation.Initializer != null)
            {
                // The config object rides the TRAILING slot: a call that supplied fewer positional
                // arguments than the constructor's arity must first fill the skipped parameters
                // from their C# defaults — otherwise `new TransitionSpec(channels, 300) { Easing = … }`
                // emits the config in the `delayMs` slot and the easing silently reverts to default.
                if (ctor is not null)
                {
                    var supplied = creation.ArgumentList?.Arguments.Count ?? 0;
                    for (var i = supplied; i < ctor.Parameters.Length; i++)
                        parts.Add(DefaultOf(ctor.Parameters[i], context));
                    parts.AddRange(specs);
                    parts.Add(context.Converter.ConvertIr(creation.Initializer));
                    return JsExpr.New(constructed, parts);
                }

                // No resolvable constructor (standalone CompileSource, no references — the
                // playground's mode): the arity is unknowable, so the trailing slot is a trap —
                // `new Text(content, role) { Tabular = true }` landed the config in the COLOR
                // parameter. Object.assign after construction is the C# initializer's exact
                // semantics and needs no arity at all.
                var config = context.Converter.ConvertIr(creation.Initializer);
                return JsExpr.Call(JsExpr.Identifier("Object.assign"), JsExpr.New(constructed, parts), config);
            }
            if (specs.Count > 0)
            {
                // The specs follow EVERY constructor parameter, so a skipped one is filled first.
                if (ctor is not null)
                    for (var i = parts.Count; i < ctor.Parameters.Length; i++)
                        parts.Add(DefaultOf(ctor.Parameters[i], context));
                parts.AddRange(specs);
            }
            return JsExpr.New(constructed, parts);
        }

        var construction = Construction(creation, type, ctor, context);
        return creation.Initializer is { } initializer
            ? ObjectInitializer.Apply(construction, initializer, context)
            : construction;
    }

    /// <summary>
    /// The hydration specs a vocabulary twin takes after its constructor's parameters, one for each
    /// type argument whose parameter carries <c>[HydratesTypeArgument]</c> (#291): the C# type is
    /// erased in JavaScript, and the twin revives what it receives of that type with the spec a Server
    /// Action's result is revived with. <c>null</c> where the argument needs no revival.
    /// <para>
    /// An argument that names a type parameter is refused (EQ2013): where the twin is built, the type
    /// is erased, so its spec would be null and every value it receives would pass through unrevived.
    /// A helper as natural as <c>static ServerTopic&lt;T&gt; Topic&lt;T&gt;(string name) =&gt; new(name)</c>
    /// built every topic that way, and the build was green.
    /// </para>
    /// </summary>
    private static IReadOnlyList<JsExpr> HydrationSpecs(BaseObjectCreationExpressionSyntax creation, ITypeSymbol type,
        ConversionContext context)
    {
        var specs = new List<JsExpr>();
        foreach (var argument in type.HydratedTypeArguments())
        {
            if (argument.MentionsTypeParameter())
                context.Report(creation, ConversionSeverity.Error, "EQ2013",
                    $"'{type.ToDisplayString()}' is built where its type argument '{argument.ToDisplayString()}' is a type " +
                    "parameter, which JavaScript erases, so what it receives could not be revived as that type. Build it " +
                    "where the type argument is a concrete type, as in a static member of the type that declares the topic.");
            specs.Add(JsExpr.Opaque(HydrationSpec.Of(argument, context.UsedAppTypes, context.UsedRuntimeTypes) ?? "null"));
        }
        return specs;
    }

    /// <summary>
    /// The call of a twin eqc writes with its C# constructors (<see cref="TwinConstructor"/>): the
    /// constructor the call binds, its arguments as the bound tree binds them (BoundArguments), and the
    /// syntax's own order where there is no model to ask. A struct built by its implicit parameterless
    /// constructor is its zero.
    /// </summary>
    private static JsExpr Construction(BaseObjectCreationExpressionSyntax creation, ITypeSymbol type, IMethodSymbol? ctor,
        ConversionContext context, JsExpr? types = null) =>
        type.IsValueType && ctor is { IsImplicitlyDeclared: true, Parameters.Length: 0 } && TwinIsWritten(type)
            ? JsExpr.Opaque(DefaultValue.Of(type, context))
            : BoundArguments.Of(context.SemanticHelper.GetOperation(creation), argument => context.Converter.ConvertIr(argument)) is { } bound
                ? bound.New(type.Name, context.TypeAnnotations, types)
                : types is null
                    ? JsExpr.New(JsExpr.Identifier(type.Name), ConstructorArguments(creation, ctor, context))
                    : JsExpr.Call(JsExpr.Identifier(Eq.ExceptionConstruct),
                        [JsExpr.Identifier(type.Name), types, .. ConstructorArguments(creation, ctor, context)]);

    /// <summary>
    /// Whether <paramref name="type"/> is a class whose twin eqc writes with its C# constructors
    /// (<see cref="TwinConstructor"/>, #583), which is built as a record is
    /// (<see cref="BuildClassConstruction"/>), an exception class of the app's among them (#611). A node
    /// of the tree, a component and a component's state take their initializer as the props the
    /// runtime's classes take, and the vocabulary's hand-written twins take a config object of their own.
    /// </summary>
    internal static bool IsBuiltAsCSharp(INamedTypeSymbol type) =>
        type is { TypeKind: TypeKind.Class, IsRecord: false, IsStatic: false }
        && TwinIsWritten(type)
        && !type.IsVisualNode() && !type.IsComponentState();

    /// <summary>
    /// A plain class built as C# builds it (#582): the constructor the call binds, then the object
    /// initializer applied to what it built (<see cref="ObjectInitializer"/>), or the elements of a
    /// collection initializer added to it, each through its <c>Add</c>.
    /// </summary>
    private static JsExpr BuildClassConstruction(BaseObjectCreationExpressionSyntax creation, INamedTypeSymbol type,
        ConversionContext context)
    {
        // A construction of a generic exception class hands its base the types it is, before the
        // constructor's body runs (#611, #708).
        var construction = Construction(creation, type, context.SemanticHelper.GetSymbol(creation) as IMethodSymbol, context,
            ExceptionTypes.HasTwin(type) ? ExceptionTypes.ConstructedTypes(type, context) : null);
        return creation.Initializer switch
        {
            null => construction,
            { RawKind: (int)SyntaxKind.CollectionInitializerExpression } collection => AddPerElementConstruction(collection, construction, context),
            var initializer => ObjectInitializer.Apply(construction, initializer, context),
        };
    }

    /// <summary>
    /// A twin constructor's arguments, in its parameters' order: a NAMED argument fills the parameter
    /// it names, the unnamed ones fill by position, a parameter the call skips is passed
    /// <c>undefined</c>, which lets the twin's own default run in its module (#385), and the ones
    /// after the last argument are left out. Filling by WRITTEN order put `Sortable: true` in the
    /// align slot of `new DataColumn("Customer", track, Sortable: true)`: the column stopped being
    /// sortable on the client only, and hydration failed on the header it rendered.
    /// <para>
    /// A <c>params</c> parameter is a rest parameter on the twin, so an array passed whole, which C#
    /// also allows, is spread, or it would arrive as one element (an invocation does the same).
    /// </para>
    /// </summary>
    private static IReadOnlyList<JsExpr> ConstructorArguments(BaseObjectCreationExpressionSyntax creation, IMethodSymbol? ctor,
        ConversionContext context)
    {
        if (creation.ArgumentList is not { Arguments.Count: > 0 } list) return [];
        var converted = list.Arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList();
        if (ctor is { Parameters.Length: > 0 } && ctor.Parameters[^1].IsParams
            && list.Arguments.Count == ctor.Parameters.Length && list.Arguments[^1].NameColon is null
            && context.SemanticHelper.GetType(list.Arguments[^1].Expression) is IArrayTypeSymbol)
            converted[^1] = JsExpr.Spread(converted[^1]);
        if (ctor is null || !list.Arguments.Any(argument => argument.NameColon != null)) return converted;

        var slots = new JsExpr?[ctor.Parameters.Length];
        for (var i = 0; i < list.Arguments.Count; i++)
        {
            // A positional argument's slot is its list position: C# allows one after a named argument
            // only when that one sits in its own position.
            var ordinal = list.Arguments[i].NameColon is { } named
                ? ctor.Parameters.FirstOrDefault(parameter => parameter.Name == named.Name.Identifier.ValueText)?.Ordinal ?? -1
                : i;
            if (ordinal >= 0 && ordinal < slots.Length) slots[ordinal] = converted[i];
        }
        var last = Array.FindLastIndex(slots, slot => slot != null);
        return slots.Take(last + 1).Select(slot => slot ?? Undefined).ToList();
    }

    private JsExpr ConvertImplicit(ImplicitObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var ms = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;
        // The constructed type's OWN definition, its type parameters and not its arguments: a list is a
        // list by what it is, and `ServerTopic<List<long>>` was built as an empty array because its
        // argument's text holds `List<`.
        var typeDisplay = ms?.ContainingType.OriginalDefinition.ToDisplayString() ?? context.ExpectedType ?? "";

        // TARGET-TYPED construction is the same naming, spelled shorter: `Matrix2D m = new(…)`.
        // The explicit path was fenced and this one was not, so the type came back by inference and
        // went straight to an emit — which is the third time the host-only fence has been found
        // guarding some of the ways a symbol can be named and reading like protection for all.
        // Found in review of that fix.
        if (ms?.ContainingType.ReportIfHostOnlyType(creation, context) == true) return Undefined;

        // `Color c = new(1, 2, 3, 4)` builds the data as the explicit form does.
        if (ms?.ContainingType is { } dataTarget && dataTarget.TwinIsData())
            return DataConstruction(creation, dataTarget, context);

        if (creation.Initializer != null)
        {
            var target = ms?.ContainingType;

            // Records and user structs keep full value semantics: built exactly like the explicit
            // `new T(...) { … }` path, the constructor and then the initializer applied to it.
            if (target is { IsRecord: true }
                || (target is { TypeKind: TypeKind.Struct } && target.IsStructuralValueType()))
            {
                return BuildValueTypeConstruction(creation, target, context);
            }

            // A plain class eqc writes, as the explicit `new C(…) { … }` builds it.
            if (target is not null && IsBuiltAsCSharp(target))
                return BuildClassConstruction(creation, target, context);

            // A named class target (`Badge b = new(0, 99, variant) { Dot = true }`) must CONSTRUCT —
            // ordered args, skipped parameters filled from their C# defaults, initializer as the
            // trailing config object (the runtime component classes' contract). Delegating to the
            // initializer here would silently return a bare object instead of an instance.
            if (target is { SpecialType: SpecialType.None, TypeKind: TypeKind.Class }
                && !typeDisplay.Contains("List<")
                && !typeDisplay.Contains("IEnumerable<") && !typeDisplay.Contains("Collection<"))
            {
                var ctorArgs = creation.ArgumentList is { Arguments.Count: > 0 }
                    ? OrderedArguments(creation, context).ToList()
                    : new List<JsExpr>();
                // Target-typed `new(gap) { a, b }` on a node class: Add-per-element, exactly like
                // the explicit form — the trailing config slot is for OBJECT initializers only.
                if (creation.Initializer.Kind() == SyntaxKind.CollectionInitializerExpression)
                {
                    return AddPerElementConstruction(creation.Initializer,
                        JsExpr.New(JsExpr.Identifier(target.Name), ctorArgs), context);
                }
                // An initializer that adds to a member or writes an entry, applied once the object
                // exists, as the explicit form applies it (#462), to the construction as IR: written as
                // text, a lambda among its arguments lost every line of its block from the map.
                if (TwinIsWritten(target) && !ObjectInitializer.OnlyAssigns(creation.Initializer))
                    return ObjectInitializer.Apply(JsExpr.New(JsExpr.Identifier(target.Name), ctorArgs), creation.Initializer, context);
                if (ms != null && ctorArgs.Count < ms.Parameters.Length)
                    ctorArgs.AddRange(ms.Parameters.Skip(ctorArgs.Count).Select(parameter => DefaultOf(parameter, context)));
                ctorArgs.Add(context.Converter.ConvertIr(creation.Initializer));
                return JsExpr.New(JsExpr.Identifier(target.Name), ctorArgs);
            }

            // A list one of whose elements an extension adds, as the explicit form applies it.
            if (ListAddedByAnExtension(creation.Initializer, target, context) is { } listed)
                return ObjectInitializer.Apply(
                    creation.ArgumentList is { Arguments.Count: 1 } && !IsCapacityArgument(creation, context)
                        ? JsExpr.Array([JsExpr.Spread(context.Converter.ConvertIr(creation.ArgumentList.Arguments[0].Expression))])
                        : JsExpr.Array([]),
                    listed, context);

            // `new() { … }` on a collection (or with no resolvable named target) → the initializer IS
            // the value. A dictionary target is DictionaryStrategy's.
            return context.Converter.ConvertIr(creation.Initializer);
        }

        // Collection target with no initializer → empty literal.
        if (typeDisplay.Contains("List<") || typeDisplay.Contains("IEnumerable<") ||
            typeDisplay.Contains("Collection<") || typeDisplay.TrimEnd('?').EndsWith("[]"))
        {
            return JsExpr.Array([]);
        }
        // Records and user structs go the SAME way they do with an initializer, and the same way the
        // explicit `new T(...)` does. Converting the arguments here in written order was the third
        // copy of that rule and the one that was wrong.
        if (ms?.ContainingType is { IsRecord: true } record)
            return BuildValueTypeConstruction(creation, record, context);
        if (ms?.ContainingType is { TypeKind: TypeKind.Struct } value && value.IsStructuralValueType())
            return BuildValueTypeConstruction(creation, value, context);
        if (ms?.ContainingType is { } plain && IsBuiltAsCSharp(plain))
            return BuildClassConstruction(creation, plain, context);

        // Target-typed `new(args)` on a named type: `Item _x = new(9, "z")` → `new Item(9, 'z')`.
        var args = creation.ArgumentList is { Arguments.Count: > 0 }
            ? OrderedArguments(creation, context)
            : [];
        var typeName = ms?.ContainingType.Name;
        if (string.IsNullOrEmpty(typeName) || typeName == "Object")
        {
            // No semantic info: fall back to the field/var's declared type (without nullability/generics noise).
            var et = (context.ExpectedType ?? "").Trim().TrimEnd('?');
            if (et.Contains("<")) et = et.Substring(0, et.IndexOf('<'));
            typeName = string.IsNullOrEmpty(et) ? null : et;
        }
        if (!string.IsNullOrEmpty(typeName))
        {
            return JsExpr.New(JsExpr.Identifier(typeName), args);
        }

        return JsExpr.Object([]);
    }

    public int Priority => 5;
}
