using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// An exception type as the browser knows it: a JavaScript <c>Error</c> that carries the .NET types it
/// is, the most derived first and <c>System.Exception</c> last (the runtime's <c>utils/exceptions</c>).
/// ONE place for what the translation asks of a type that derives from <c>System.Exception</c>, each
/// answered from its SYMBOL:
/// <list type="bullet">
/// <item><see cref="Construction(IReadOnlyList{string}, BaseObjectCreationExpressionSyntax, ConversionContext)"/>:
/// <c>new T(message)</c> for a type with no twin of its own, one of .NET's, with T's chain written out.</item>
/// <item><see cref="HasTwin"/>: whether T is a class of the app's, which is built as any class is, over
/// the runtime's <see cref="Eq.ExceptionBase"/>, its twin saying its chain (<see cref="TypesOf"/>, #611).</item>
/// <item><see cref="Test"/>: whether a value is a T, which a typed <c>catch</c>, a type pattern and an
/// <c>as</c> write alike.</item>
/// <item><see cref="IsRoot"/>: whether T is <c>System.Exception</c> itself, which a <c>catch</c> takes
/// without a test, as every thrown value is one.</item>
/// </list>
/// <para>
/// A type was an exception when its NAME ended in "Exception", and it lowered to a bare <c>Error</c>:
/// <c>Exception e = new("x")</c> constructed a class JavaScript does not have, a class of the app's
/// named otherwise was not one, and no test could tell one exception from another, so a catch took
/// whatever came (#474).
/// </para>
/// </summary>
internal static class ExceptionTypes
{
    /// <summary>Whether <paramref name="type"/> is <c>System.Exception</c> or derives from it.</summary>
    public static bool Is(ITypeSymbol? type)
    {
        for (var at = type as INamedTypeSymbol; at is not null; at = at.BaseType)
            if (IsRoot(at)) return true;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="type"/> is an exception class whose twin eqc writes, one the app declares
    /// on its own: a class like any other (#611), its members, its constructors and its methods its
    /// twin's, built with <c>new</c> as a class is. It was an <c>Error</c> built by its symbol, with no
    /// member of its own. A class with no module of its own (<see cref="Services.PlainClassModule"/>), a
    /// nested one or one that stays on the server, is still built as that <c>Error</c>.
    /// </summary>
    public static bool HasTwin(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Class, ContainingType: null } named && Is(named)
        && Expressions.ObjectCreationStrategy.TwinIsWritten(named)
        && !Services.PlainClassModule.ServerOnlyAlongChain(named)
        && !named.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is "RuntimeProvided" or "RuntimeProvidedAttribute");

    /// <summary>
    /// The twin's <c>static $types</c>: the chain of the class the twin is written from, which the
    /// runtime's base reads off the class an exception is constructed as, so an exception of a derived
    /// class carries the derived class's. A generic class's says its type parameters (<c>Failed&lt;T&gt;</c>),
    /// and each construction of it is tagged with its own (<see cref="Typed"/>).
    /// </summary>
    public static JsExpr TypesOf(INamedTypeSymbol type) =>
        JsExpr.Array(ChainOf(type.OriginalDefinition).Select(name => JsExpr.Literal(JsStringLiteral.Quote(name))).ToList());

    /// <summary>
    /// A construction of an exception class of the app's, tagged with the chain its type has where the
    /// twin's <c>$types</c> cannot say it: a constructed generic class (<c>new Failed&lt;int&gt;()</c>),
    /// whose twin knows only <c>Failed&lt;T&gt;</c>, while a typed <c>catch</c> tells
    /// <c>Failed&lt;int&gt;</c> from <c>Failed&lt;string&gt;</c>. Any other is the construction as it is.
    /// </summary>
    public static JsExpr Typed(JsExpr construction, INamedTypeSymbol type, ConversionContext context)
    {
        var chain = ChainOf(type);
        if (chain.SequenceEqual(ChainOf(type.OriginalDefinition))) return construction;
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.ExceptionTyped), construction,
            JsExpr.Array(chain.Select(name => JsExpr.Literal(JsStringLiteral.Quote(name))).ToList()));
    }

    /// <summary>Whether <paramref name="type"/> is <c>System.Exception</c> itself.</summary>
    public static bool IsRoot(ITypeSymbol? type) =>
        type is INamedTypeSymbol { Name: "Exception", ContainingType: null, Arity: 0 } named
        && named.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };

    /// <summary>
    /// The name a type is known by in the browser: its C# name in full, as <c>ToDisplayString</c>
    /// writes it (<c>System.Collections.Generic.KeyNotFoundException</c>, <c>App.Outer.Inner</c>,
    /// <c>App.Failed&lt;int&gt;</c>), which is also how the runtime names the exceptions it throws.
    /// </summary>
    public static string NameOf(ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();

    /// <summary>The type and every type it derives from, the most derived first, down to
    /// <c>System.Exception</c>.</summary>
    public static IReadOnlyList<string> ChainOf(INamedTypeSymbol type)
    {
        var chain = new List<string>();
        for (var at = type; at is not null; at = at.BaseType)
        {
            chain.Add(NameOf(at));
            if (IsRoot(at)) break;
        }
        return chain;
    }

    /// <summary>
    /// <c>new T(…)</c> with every argument the creation writes evaluated where C# evaluates it, in
    /// written order, and the message picked by its parameter
    /// (<see cref="Expressions.ExceptionCreationStrategy.MessageIndex"/>): the arguments are the
    /// template's parts, so the writer keeps their order however the holes are placed. Where the
    /// message is absent, or may be null, the text .NET writes then is handed with it
    /// (<see cref="DefaultMessage"/>), and the runtime writes <c>Exception.Message</c>'s own where
    /// there is none.
    /// </summary>
    public static JsExpr Construction(IReadOnlyList<string> chain, BaseObjectCreationExpressionSyntax creation,
        ConversionContext context)
    {
        var arguments = creation.ArgumentList?.Arguments ?? default;
        var constructor = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;
        var fallback = constructor is null ? null : DefaultMessage(constructor, arguments.Count);
        if (arguments.Count == 0 && fallback is null) return Construction(chain, (JsExpr?)null, context);
        context.UsedHelpers.Add(Eq.Import);
        var message = arguments.Count == 0 ? -1 : Expressions.ExceptionCreationStrategy.MessageIndex(creation, context);
        var taken = new HashSet<int> { message };
        // What a framework constructor takes besides the message, each by its parameter: the message
        // is composed from them as .NET composes it, and the members read them (#558).
        var members = new List<string>();
        if (constructor is not null && IsFramework(constructor.ContainingType))
        {
            foreach (var (parameter, member) in Parts)
            {
                var at = Expressions.ExceptionCreationStrategy.ArgumentIndex(creation, constructor, parameter);
                if (at < 0) continue;
                // A params array written out takes every argument from its own on: `new
                // AggregateException(a, b)` hands both to `innerExceptions`.
                var spread = constructor.Parameters.FirstOrDefault(p => p.Name == parameter) is { IsParams: true }
                    && (arguments.Count > constructor.Parameters.Length
                        || context.SemanticHelper.GetType(arguments[at].Expression) is not IArrayTypeSymbol)
                    ? Enumerable.Range(at, arguments.Count - at).ToList()
                    : null;
                members.Add(spread is null
                    ? $"{member}: {{{at}}}"
                    : $"{member}: [{string.Join(", ", spread.Select(index => $"{{{index}}}"))}]");
                foreach (var index in spread ?? [at]) taken.Add(index);
            }
        }
        var parts = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList();
        var holes = new List<string> { MessageHole(message, fallback, arguments, parts, context) };
        var rest = Enumerable.Range(0, arguments.Count).Where(index => !taken.Contains(index)).Select(index => $"{{{index}}}").ToList();
        if (members.Count > 0 || rest.Count > 0) holes.Add(members.Count > 0 ? $"{{ {string.Join(", ", members)} }}" : "undefined");
        holes.AddRange(rest);
        var types = $"[{string.Join(", ", chain.Select(JsStringLiteral.Quote))}]";
        return JsExpr.Template($"{Eq.ExceptionCreate}({types}, {string.Join(", ", holes)})", parts, context.TypeAnnotations);
    }

    /// <summary>
    /// The message's place in the call: the argument written for it, the text .NET writes where none
    /// was, and the argument falling back to that text where it may be null. The text is a part of
    /// its own, after the arguments, so it is never read as code.
    /// </summary>
    private static string MessageHole(int message, string? fallback, SeparatedSyntaxList<ArgumentSyntax> arguments,
        List<JsExpr> parts, ConversionContext context)
    {
        if (fallback is null) return message < 0 ? "undefined" : $"{{{message}}}";
        var written = message < 0 ? null : context.SemanticHelper.GetOperation(arguments[message].Expression);
        if (written is not null && NeverNull(written)) return $"{{{message}}}";
        parts.Add(JsExpr.Literal(JsStringLiteral.Quote(fallback)));
        var text = $"{{{parts.Count - 1}}}";
        return message < 0 || written is { ConstantValue: { HasValue: true, Value: null } } ? text : $"{{{message}}} ?? {text}";
    }

    /// <summary>A message no run can find null: a constant with a value, an interpolated string, or a
    /// concatenation, which C# never makes null.</summary>
    internal static bool NeverNull(IOperation operation) => operation switch
    {
        { ConstantValue: { HasValue: true, Value: var value } } => value is not null,
        IInterpolatedStringOperation => true,
        IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } => true,
        _ => false,
    };

    /// <summary>
    /// The text .NET writes for this creation where its message is absent or null. A framework type's
    /// is its constructor's own (<see cref="FrameworkText"/>). An app's type with no twin of its own (a
    /// nested one) built with no argument takes the text of the nearest framework type it derives from,
    /// whose parameterless constructor its own calls unless it says otherwise; one with arguments hands
    /// its message as it always did. An app's type with a twin is no creation of this kind: its base
    /// call hands the text (<see cref="TwinConstructor"/>, #611).
    /// </summary>
    private static string? DefaultMessage(IMethodSymbol constructor, int written)
    {
        if (IsFramework(constructor.ContainingType)) return FrameworkText(constructor);
        if (written > 0) return null;
        var framework = constructor.ContainingType.BaseType;
        while (framework is not null && !IsFramework(framework)) framework = framework.BaseType;
        return framework?.InstanceConstructors.FirstOrDefault(candidate => candidate.Parameters.IsEmpty) is { } parameterless
            ? FrameworkText(parameterless)
            : null;
    }

    /// <summary>
    /// The text .NET writes where a framework exception constructor is handed no message, or a null
    /// one, read from .NET itself (<see cref="ExceptionDefaultMessage"/>), or null where there is none
    /// to hand: <c>TypeInitializationException</c>'s, which the runtime composes from the type's name,
    /// and <c>Exception.Message</c>'s own, which names the type the app created. The same for a
    /// <c>new</c> of the type and for the base call of an exception class of the app's over it (#611).
    /// </summary>
    internal static string? FrameworkText(IMethodSymbol constructor) =>
        constructor.Parameters.Any(parameter => parameter.Name == "fullTypeName") ? null : ExceptionDefaultMessage.Of(constructor);

    /// <summary>A framework exception constructor's parameters besides the message, and the member
    /// of the runtime's exception each one fills: by a <c>new</c> of the type, and by the base call of an
    /// exception class of the app's over it (<see cref="TwinConstructor"/>, #611).</summary>
    internal static readonly (string Parameter, string Member)[] Parts =
    [
        ("paramName", "paramName"),
        ("actualValue", "actualValue"),
        ("innerException", "innerException"),
        ("objectName", "objectName"),
        // The member is .NET's TypeInitializationException.TypeName, which C# reads as `typeName`.
        ("fullTypeName", "typeName"),
        ("innerExceptions", "innerExceptions"),
    ];

    /// <summary>A type of the framework's, which says its arguments by its parameters' names and
    /// composes its message from them: one in a <c>System</c> namespace.</summary>
    internal static bool IsFramework(INamedTypeSymbol type) =>
        type.ContainingNamespace?.ToDisplayString() is { } space
        && (space == "System" || space.StartsWith("System.", StringComparison.Ordinal));

    /// <summary>The construction from a chain the caller already has.</summary>
    public static JsExpr Construction(IReadOnlyList<string> chain, JsExpr? message, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var types = JsExpr.Array(chain.Select(name => JsExpr.Literal(JsStringLiteral.Quote(name))).ToList());
        return message is null
            ? JsExpr.Call(JsExpr.Identifier(Eq.ExceptionCreate), types)
            : JsExpr.Call(JsExpr.Identifier(Eq.ExceptionCreate), types, message);
    }

    /// <summary>
    /// An exception of a type the runtime throws itself (its <c>bases</c> table), for a lowering that
    /// throws on .NET's behalf where the C# names no type: <c>Convert.ToBoolean('a')</c> is an
    /// <c>InvalidCastException</c>.
    /// </summary>
    public static JsExpr Known(string type, string message, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.ExceptionOf),
            JsExpr.Literal(JsStringLiteral.Quote(type)), JsExpr.Literal(JsStringLiteral.Quote(message)));
    }

    /// <summary>Whether the value at <paramref name="access"/> is a <paramref name="type"/>:
    /// <c>$eq.exceptions.is(access, 'System.ArgumentException')</c>.</summary>
    public static string Test(string access, INamedTypeSymbol type, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.ExceptionIs}({access}, {JsStringLiteral.Quote(NameOf(type))})";
    }
}
