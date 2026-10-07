using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// An exception type as the browser knows it: a JavaScript <c>Error</c> that carries the .NET types it
/// is, the most derived first and <c>System.Exception</c> last (the runtime's <c>utils/exceptions</c>).
/// ONE place for the three things the translation asks of a type that derives from
/// <c>System.Exception</c>, each answered from its SYMBOL:
/// <list type="bullet">
/// <item><see cref="Construction(INamedTypeSymbol, JsExpr?, ConversionContext)"/>: <c>new T(message)</c>,
/// with T's chain written out.</item>
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

    /// <summary><c>$eq.exceptions.create([T, …], message)</c>, and with no message where none was
    /// written.</summary>
    public static JsExpr Construction(INamedTypeSymbol type, JsExpr? message, ConversionContext context) =>
        Construction(ChainOf(type), message, context);

    /// <summary>
    /// <c>new T(…)</c> with every argument the creation writes evaluated where C# evaluates it, in
    /// written order, and the message picked by its parameter
    /// (<see cref="Expressions.ExceptionCreationStrategy.MessageIndex"/>): the arguments are the
    /// template's parts, so the writer keeps their order however the holes are placed.
    /// </summary>
    public static JsExpr Construction(IReadOnlyList<string> chain, BaseObjectCreationExpressionSyntax creation,
        ConversionContext context)
    {
        var arguments = creation.ArgumentList?.Arguments ?? default;
        if (arguments.Count == 0) return Construction(chain, (JsExpr?)null, context);
        context.UsedHelpers.Add(Eq.Import);
        var message = Expressions.ExceptionCreationStrategy.MessageIndex(creation, context);
        var taken = new HashSet<int> { message };
        // What a framework constructor takes besides the message, each by its parameter: the message
        // is composed from them as .NET composes it, and the members read them (#558).
        var members = new List<string>();
        if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol constructor && IsFramework(constructor.ContainingType))
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
        var holes = new List<string> { message < 0 ? "undefined" : $"{{{message}}}" };
        var rest = Enumerable.Range(0, arguments.Count).Where(index => !taken.Contains(index)).Select(index => $"{{{index}}}").ToList();
        if (members.Count > 0 || rest.Count > 0) holes.Add(members.Count > 0 ? $"{{ {string.Join(", ", members)} }}" : "undefined");
        holes.AddRange(rest);
        var types = $"[{string.Join(", ", chain.Select(JsStringLiteral.Quote))}]";
        var parts = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList();
        return JsExpr.Template($"{Eq.ExceptionCreate}({types}, {string.Join(", ", holes)})", parts, context.TypeAnnotations);
    }

    /// <summary>A framework exception constructor's parameters besides the message, and the member
    /// of the runtime's exception each one fills.</summary>
    private static readonly (string Parameter, string Member)[] Parts =
    [
        ("paramName", "paramName"),
        ("actualValue", "actualValue"),
        ("innerException", "innerException"),
        ("objectName", "objectName"),
        ("fullTypeName", "fullTypeName"),
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
