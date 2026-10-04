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
            // The range refused where it leaves the array, as .NET refuses it: `slice` clamped it.
            [IArrayTypeSymbol, { SpecialType: SpecialType.System_Int32 }, { SpecialType: SpecialType.System_Int32 }]
                => $"{Eq.TextChars}({{0}}, {{1}}, {{2}}).join('')",
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
        // construct via `new`, mapping positional args and any object initializer onto the constructor.
        if (createdType is { IsRecord: true }
            || (createdType is { TypeKind: TypeKind.Struct } && createdType.IsStructuralValueType()))
        {
            return BuildValueTypeConstruction(creation, createdType, context);
        }

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
                    arguments.AddRange(ctor.Parameters.Skip(emittedSlots).Select(DefaultOf));
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

        // Exception types -> JavaScript Error. Error takes ONE message argument — pick the C#
        // constructor's `message` PARAMETER (signatures differ: ArgumentException(message, param)
        // vs ArgumentOutOfRangeException(param, message)); emitting all arguments positionally
        // would silently make the param NAME the thrown message.
        if (typeName.EndsWith("Exception") || typeName == "Exception")
        {
            return JsExpr.New(JsExpr.Identifier("Error"),
                ExceptionMessageArgument(creation, context) is { } message ? [message] : arguments);
        }

        if (assignInitializerAfterConstruction)
            return JsExpr.Call(JsExpr.Identifier("Object.assign"),
                JsExpr.New(JsExpr.Identifier(genericTypeName ?? typeName), arguments), initializer!);
        return JsExpr.New(JsExpr.Identifier(genericTypeName ?? typeName), arguments);
    }

    /// <summary>
    /// Values that stand where ONE expression is read, as they always stood: the only one, or, where
    /// a creation's arguments and its initializer meet in a collection's literal, all of them
    /// spliced in as text. That second shape is a comma expression and was never right: kept as it
    /// was, for a change that is about where lines map rather than what a collection holds.
    /// </summary>
    private static JsExpr Spliced(IReadOnlyList<JsExpr> values) =>
        values.Count == 1 ? values[0] : JsExpr.Opaque(string.Join(", ", values.Select(JsExprWriter.Write)));

    /// <summary>A parameter's C# default as the literal a skipped argument is filled with.</summary>
    private static JsExpr DefaultOf(IParameterSymbol parameter) => JsExpr.Literal(ParameterDefaultLiteral(parameter));

    /// <summary>Type names whose creations lower to JS literals (array/object/Set) — a collection
    /// initializer on THESE is the literal itself, never Add-per-element on a constructed node.</summary>
    private static bool IsCollectionLikeTypeName(string typeName) =>
        typeName.StartsWith("List<") || typeName.Contains(".List<")
        || typeName.StartsWith("IEnumerable<") || typeName.Contains(".IEnumerable<")
        || typeName.StartsWith("HashSet<") || typeName.Contains(".HashSet<")
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

    /// <summary>The converted argument bound to the exception constructor's <c>message</c> parameter
    /// (semantic when resolvable, else the LAST argument of a multi-arg call — every BCL exception
    /// with a paramName overload puts the message beside it); null = keep whatever was converted.</summary>
    private static JsExpr? ExceptionMessageArgument(ObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var args = creation.ArgumentList?.Arguments;
        if (args is not { Count: > 1 }) return null;

        if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol ctor)
        {
            for (var i = 0; i < args.Value.Count && i < ctor.Parameters.Length; i++)
            {
                var parameter = args.Value[i].NameColon?.Name.Identifier.ValueText is { } named
                    ? ctor.Parameters.FirstOrDefault(p => p.Name == named)
                    : ctor.Parameters[i];
                if (parameter?.Name == "message")
                    return context.Converter.ConvertIr(args.Value[i].Expression);
            }
        }

        return context.Converter.ConvertIr(args.Value[^1].Expression);
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
            ordered.Add(slots[keep[k]] ?? DefaultOf(ctor.Parameters[keep[k]]));
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

    /// <summary>The TS literal for a parameter's C# default value — enum members lower to their
    /// camelCase member-name string, matching the enum representation everywhere else. Shared with
    /// InvocationStrategy (named INVOCATION arguments reorder the same way creations do).</summary>
    internal static string DefaultLiteralFor(IParameterSymbol parameter) => ParameterDefaultLiteral(parameter);

    private static string ParameterDefaultLiteral(IParameterSymbol parameter)
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
        var value = parameter.ExplicitDefaultValue;

        var enumType = parameter.Type.TypeKind == TypeKind.Enum ? parameter.Type
            : parameter.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                ? nullable.TypeArguments[0]
                : null;
        if (enumType is { TypeKind: TypeKind.Enum })
        {
            var member = enumType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, value));
            if (member != null) return $"'{member.Name.ToCamelCase()}'";
        }

        // A string or a char is spelled by the one writer of JavaScript strings: quoted by hand, a
        // default of "it's" closed its own quotes and a char had none at all (`M.g(,, 1)`, #520).
        return value switch
        {
            bool flag => flag ? "true" : "false",
            string text => JsStringLiteral.Quote(text),
            char character => JsStringLiteral.Quote(character.ToString()),
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
        };
    }

    /// <summary>
    /// Value members of a record/struct we have no syntax for, in the SAME order
    /// <see cref="TypeDeclarationExtensions.ValueMembers"/> produces (and therefore the same order
    /// RecordTypeEmitter emitted the constructor in): primary-constructor parameters, then settable
    /// instance properties, then public instance fields. Empty when the type has no usable symbol.
    /// </summary>
    private static IReadOnlyList<ValueMember> SymbolValueMembers(INamedTypeSymbol? type)
    {
        if (type == null) return new List<ValueMember>();

        // The abstract VOCABULARY (BoxStyle, EdgeInsets, …) is runtime-provided by a HAND-WRITTEN TS twin
        // that takes a trailing config object — only types whose twin the compiler EMITS (RecordTypeEmitter,
        // e.g. the shared component library's NavItem) have the positional constructor this mapping needs.
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (Services.RuntimeProvidedTypeScanner.IsVocabularyNamespace(ns))
            return new List<ValueMember>();

        // The primary constructor: the widest one that is not the record's synthesized copy constructor.
        var primary = type.InstanceConstructors
            .Where(c => c.Parameters.Length > 0
                        && !(c.Parameters.Length == 1
                             && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, type)))
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        var members = new List<ValueMember>();
        var seen = new HashSet<string>();

        if (primary != null)
            foreach (var p in primary.Parameters)
                if (seen.Add(p.Name))
                    members.Add(new ValueMember(p.Name, p.Name.ToCamelCase(), "any"));

        foreach (var member in type.GetMembers())
        {
            // `EqualityContract` is the record's synthesized type discriminator, never a value member.
            if (member is IPropertySymbol { IsStatic: false, IsImplicitlyDeclared: false } prop
                && prop.DeclaredAccessibility == Accessibility.Public
                && prop.SetMethod != null
                && prop.Name != "EqualityContract"
                && seen.Add(prop.Name))
            {
                members.Add(new ValueMember(prop.Name, prop.Name.ToCamelCase(), "any"));
            }
            else if (member is IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field
                     && field.DeclaredAccessibility == Accessibility.Public
                     && seen.Add(field.Name))
            {
                members.Add(new ValueMember(field.Name, field.Name.ToCamelCase(), "any"));
            }
        }

        return members;
    }

    /// <summary>
    /// Builds a <c>new T(...)</c> construction for a record/struct, mapping positional arguments and any
    /// object initializer (<c>{ Name = … }</c>) onto the constructor's positional value members (in the
    /// type's declaration order). Members left unset before the last supplied one are passed
    /// <c>undefined</c>, and trailing unset members are omitted: either way the constructor's own
    /// parameter defaults cover them.
    /// </summary>
    /// <summary>
    /// A model that can answer about THIS node. Roslyn throws when asked about a node from another
    /// tree, and a record is usually declared in another file — the component library's, not the
    /// page's — so the COMPILATION is asked for that tree's own model rather than giving up. Giving
    /// up is what left `TextAlignment.Start` as a null the client aligned by and the server did not.
    /// </summary>
    private static SemanticModel? ModelOf(SyntaxNode node, ConversionContext context)
    {
        if (context.SemanticModel is not { } model) return null;
        if (ReferenceEquals(node.SyntaxTree, model.SyntaxTree)) return model;
        return model.Compilation.ContainsSyntaxTree(node.SyntaxTree)
            ? model.Compilation.GetSemanticModel(node.SyntaxTree)
            : null;
    }

    /// <summary>The slot a member name occupies, or -1. One lookup for both ways a caller can name
    /// a member: a constructor argument's <c>NameColon</c> and an object initializer's left side.</summary>
    private static int IndexOfMember(IReadOnlyList<ValueMember> members, string js)
    {
        for (var i = 0; i < members.Count; i++)
            if (members[i].Js == js) return i;
        return -1;
    }

    private static JsExpr BuildValueTypeConstruction(BaseObjectCreationExpressionSyntax creation, ITypeSymbol type, ConversionContext context)
    {
        var declSyntax = type.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        // No declaration syntax (external/metadata type — a record from a referenced assembly, e.g. the
        // shared component library's NavItem): the SYMBOL still carries the member order, so recover it
        // there and map positionally exactly as the syntax path does. A record's emitted constructor is
        // positional-only, so a trailing config object would silently land in the next positional slot
        // (`new NavItem(icon, label, { badgeCount: 3 })` sets selectedIcon and leaves badgeCount 0 —
        // SSR renders the badge, the hydrated client does not).
        var members = declSyntax != null
            ? declSyntax.ValueMembers(ModelOf(declSyntax, context))
            : SymbolValueMembers(type as INamedTypeSymbol);

        // Neither syntax nor a usable symbol: fall back to a trailing CONFIG OBJECT — the shape
        // UI-component classes accept (`new Row(gap, { height: … })`). Positional args pass through.
        var constructed = JsExpr.Identifier(type.Name);
        if (members.Count == 0)
        {
            var parts = new List<JsExpr>();
            if (creation.ArgumentList != null)
                parts.AddRange(OrderedArguments(creation, context));
            if (creation.Initializer != null)
            {
                // The config object rides the TRAILING slot: a call that supplied fewer positional
                // arguments than the constructor's arity must first fill the skipped parameters
                // from their C# defaults — otherwise `new TransitionSpec(channels, 300) { Easing = … }`
                // emits the config in the `delayMs` slot and the easing silently reverts to default.
                if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol ctor)
                {
                    var supplied = creation.ArgumentList?.Arguments.Count ?? 0;
                    for (var i = supplied; i < ctor.Parameters.Length; i++)
                        parts.Add(DefaultOf(ctor.Parameters[i]));
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
            return JsExpr.New(constructed, parts);
        }

        var values = new JsExpr?[members.Count];

        // Constructor arguments fill the members — a NAMED one by its name, and only the unnamed
        // ones by position. C# lets a caller skip optionals by naming a later parameter
        // (`new DataColumn("Customer", track, Sortable: true)`), and filling by WRITTEN order put
        // that `true` in the align slot: the column stopped being sortable on the client only, so
        // SSR rendered a header button the hydrated page did not, hydration failed on the tag
        // mismatch, and the whole page fell back to a full re-render.
        if (creation.ArgumentList != null)
        {
            var args = creation.ArgumentList.Arguments;
            var position = 0;
            foreach (var argument in args)
            {
                var index = argument.NameColon is { } named
                    ? IndexOfMember(members, named.Name.Identifier.Text.ToCamelCase())
                    : position++;
                if (index >= 0 && index < members.Count)
                    values[index] = context.Converter.ConvertIr(argument.Expression);
            }
        }

        // Object initializer `{ Name = …, Age = … }` fills the named members by position.
        if (creation.Initializer != null)
        {
            foreach (var expr in creation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    var idx = IndexOfMember(members, assignment.Left.ToString().ToCamelCase());
                    if (idx >= 0) values[idx] = context.Converter.ConvertIr(assignment.Right);
                }
            }
        }

        var lastSet = -1;
        for (var i = 0; i < values.Length; i++) if (values[i] != null) lastSet = i;

        // A member the creation does not set is `undefined`, which lets the twin's constructor write
        // its default: the constructor is where a member's initializer is converted, in its own
        // module (#385). A default copied here had to be a literal, so a field's `= "x"` and a
        // property's `= new()` were lost at every `new Fields { N = 3 }`.
        var ctorArgs = new List<JsExpr>();
        for (var i = 0; i <= lastSet; i++) ctorArgs.Add(values[i] ?? Undefined);

        return JsExpr.New(constructed, ctorArgs);
    }

    private JsExpr ConvertImplicit(ImplicitObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var ms = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;
        var typeDisplay = ms?.ContainingType.ToDisplayString() ?? context.ExpectedType ?? "";

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

            // Records and user structs keep full value semantics: map args + initializer onto the
            // positional constructor exactly like the explicit `new T(...) { … }` path.
            if (target is { IsRecord: true }
                || (target is { TypeKind: TypeKind.Struct } && target.IsStructuralValueType()))
            {
                return BuildValueTypeConstruction(creation, target, context);
            }

            // A named class target (`Badge b = new(0, 99, variant) { Dot = true }`) must CONSTRUCT —
            // ordered args, skipped parameters filled from their C# defaults, initializer as the
            // trailing config object (the runtime component classes' contract). Delegating to the
            // initializer here would silently return a bare object instead of an instance.
            // `new() { a, b }` on a HashSet target seeds a JS Set (the HashSetStrategy contract).
            if (typeDisplay.Contains("HashSet<"))
                return JsExpr.New(JsExpr.Identifier("Set"), [context.Converter.ConvertIr(creation.Initializer)]);

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
                if (ms != null && ctorArgs.Count < ms.Parameters.Length)
                    ctorArgs.AddRange(ms.Parameters.Skip(ctorArgs.Count).Select(DefaultOf));
                ctorArgs.Add(context.Converter.ConvertIr(creation.Initializer));
                return JsExpr.New(JsExpr.Identifier(target.Name), ctorArgs);
            }

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
        // Bare `new()` on a HashSet target — the runtime representation is a JS Set.
        if (typeDisplay.Contains("HashSet<"))
        {
            return JsExpr.New(JsExpr.Identifier("Set"), []);
        }

        // Records and user structs go the SAME way they do with an initializer, and the same way the
        // explicit `new T(...)` does. Converting the arguments here in written order was the third
        // copy of that rule and the one that was wrong.
        if (ms?.ContainingType is { IsRecord: true } record)
            return BuildValueTypeConstruction(creation, record, context);
        if (ms?.ContainingType is { TypeKind: TypeKind.Struct } value && value.IsStructuralValueType())
            return BuildValueTypeConstruction(creation, value, context);

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
