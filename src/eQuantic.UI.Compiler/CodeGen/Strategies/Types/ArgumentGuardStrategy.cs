using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// The argument guards .NET 8 added to its argument exceptions, as the runtime's <c>$eq.guard</c>
/// (<c>utils/guards.ts</c>), which throws where .NET throws, the exception .NET throws, with its words
/// in the current culture (#1202): <c>ArgumentNullException.ThrowIfNull</c>,
/// <c>ArgumentException.ThrowIfNullOrEmpty</c> and <c>ThrowIfNullOrWhiteSpace</c>, and the nine range
/// guards of <c>ArgumentOutOfRangeException</c>. Each reached the invocation fallback and was refused
/// (EQ2004), so write-once code that checks its arguments as the analyzers recommend could not cross.
/// <para>
/// What the browser cannot see is handed over: the parameter's name, which C# fills from the caller's
/// argument text where the call leaves it out (<c>CallerArgumentExpression</c>) and the bound tree
/// carries as the default argument's constant; the type's default equality (<see cref="ElementEquality"/>)
/// or its ordering (<see cref="ValueOrdering"/>); and how a value of the type is written in the current
/// culture (<see cref="StringConversion"/>), an arrow the guard calls only to throw. The arguments are
/// passed in parameter order through a template over the written ones, so one named out of order is
/// still evaluated where it is written.
/// </para>
/// </summary>
internal sealed class ArgumentGuardStrategy : IExpressionIrStrategy
{
    /// <summary>What a guard is handed besides its arguments, by what it checks.</summary>
    private enum Check
    {
        /// <summary>A null, an empty or a white string: nothing.</summary>
        Argument,
        /// <summary>A sign: the value's text.</summary>
        Sign,
        /// <summary>The type's default equality, and the value's text.</summary>
        Equality,
        /// <summary>The type's ordering, and the value's text.</summary>
        Order,
    }

    private static readonly Dictionary<string, Check> Guards = new()
    {
        ["ThrowIfNull"] = Check.Argument,
        ["ThrowIfNullOrEmpty"] = Check.Argument,
        ["ThrowIfNullOrWhiteSpace"] = Check.Argument,
        ["ThrowIfZero"] = Check.Sign,
        ["ThrowIfNegative"] = Check.Sign,
        ["ThrowIfNegativeOrZero"] = Check.Sign,
        ["ThrowIfEqual"] = Check.Equality,
        ["ThrowIfNotEqual"] = Check.Equality,
        ["ThrowIfGreaterThan"] = Check.Order,
        ["ThrowIfGreaterThanOrEqual"] = Check.Order,
        ["ThrowIfLessThan"] = Check.Order,
        ["ThrowIfLessThanOrEqual"] = Check.Order,
    };

    private static readonly HashSet<string> Declaring =
        ["System.ArgumentNullException", "System.ArgumentException", "System.ArgumentOutOfRangeException"];

    /// <summary>The numbers a sign guard reads in the browser: a number, a long as a BigInt, a decimal as the
    /// runtime's, and a char as the one-character string it is held as, which is zero only as U+0000 and
    /// never negative.</summary>
    private static readonly HashSet<SpecialType> Signed =
    [
        SpecialType.System_SByte, SpecialType.System_Byte, SpecialType.System_Int16, SpecialType.System_UInt16,
        SpecialType.System_Int32, SpecialType.System_UInt32, SpecialType.System_Int64, SpecialType.System_UInt64,
        SpecialType.System_Single, SpecialType.System_Double, SpecialType.System_Decimal, SpecialType.System_Char,
    ];

    /// <summary>The parameter the arrow that writes a value's text takes.</summary>
    private const string Shown = "$value";

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax invocation && GuardOf(invocation, context) is not null;

    /// <summary>The guard a call is bound to, by its symbol: an alias or <c>using static</c> is the same call.</summary>
    private static IMethodSymbol? GuardOf(InvocationExpressionSyntax invocation, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { IsStatic: true } method
        && Guards.ContainsKey(method.Name)
        && Declaring.Contains(method.ContainingType.OriginalDefinition.ToDisplayString())
            ? method
            : null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var method = GuardOf(invocation, context)!;
        var check = Guards[method.Name];
        // The guard's own arguments, then what it is handed of its type, which the browser cannot see.
        var handed = new List<JsExpr>();
        if (check != Check.Argument)
        {
            var type = method.TypeArguments[0];
            if (Unheld(type, check) is { } reason) return Refused(invocation, method, reason, context);
            if (check == Check.Equality) handed.Add(JsExpr.Opaque(ElementEquality.Comparer(type)));
            if (check == Check.Order) handed.Add(JsExpr.Opaque(ValueOrdering.Of(type)!));
            handed.Add(JsExpr.Arrow(Shown, StringConversion.Of(type, JsExpr.Identifier(Shown), context)));
        }
        else if (method.Parameters[0].Type is IPointerTypeSymbol)
        {
            return Refused(invocation, method, "a pointer never reaches the browser", context);
        }

        context.UsedHelpers.Add(Eq.Import);
        var arguments = invocation.ArgumentList.Arguments;
        var written = ParameterTemplate.WrittenFor(arguments, method);
        var parts = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList();
        var holes = new List<string>();
        foreach (var parameter in method.Parameters)
        {
            if (written[parameter.Ordinal] >= 0)
            {
                holes.Add("{" + written[parameter.Ordinal] + "}");
                continue;
            }
            // The one parameter a guard lets a call leave out is the name, which C# fills from the text of
            // the argument it checks.
            holes.Add("{" + parts.Count + "}");
            parts.Add(Omitted(invocation, parameter, context));
        }
        foreach (var part in handed)
        {
            holes.Add("{" + parts.Count + "}");
            parts.Add(part);
        }
        return JsExpr.Template($"{Eq.Guard}.{method.Name.ToCamelCase()}({string.Join(", ", holes)})", parts);
    }

    /// <summary>
    /// What C# passes for a parameter the call leaves out: the default argument the bound tree carries,
    /// whose constant <c>CallerArgumentExpression</c> fills with the checked argument's text.
    /// </summary>
    private static JsExpr Omitted(InvocationExpressionSyntax invocation, IParameterSymbol parameter, ConversionContext context)
    {
        var argument = (context.SemanticHelper.GetOperation(invocation) as IInvocationOperation)?.Arguments
            .FirstOrDefault(bound => SymbolEqualityComparer.Default.Equals(bound.Parameter?.OriginalDefinition, parameter.OriginalDefinition));
        return argument?.Value.ConstantValue is { HasValue: true, Value: string text }
            ? JsExpr.Literal(JsStringLiteral.Quote(text))
            : JsExpr.Literal("null");
    }

    /// <summary>
    /// Why the browser cannot answer a guard over <paramref name="type"/>, or null where it can: a sign guard
    /// reads a number, a long, a decimal and a char, and no other <c>INumberBase</c> the browser holds
    /// (<c>Int128</c>, <c>Half</c>, <c>BigInteger</c>, <c>nint</c>); an ordering guard needs the order .NET's
    /// comparer gives the type, which the browser cannot reproduce for every type (a <c>Guid</c>, a
    /// <c>CompareTo</c> written as an explicit interface member, a type parameter).
    /// </summary>
    private static string? Unheld(ITypeSymbol type, Check check) => check switch
    {
        Check.Sign when !Signed.Contains(type.SpecialType) =>
            $"the browser holds no {type.ToDisplayString()} as a number whose sign it can read",
        Check.Order when ValueOrdering.Of(type) is null =>
            $"the browser cannot order a {type.ToDisplayString()} as .NET's comparer orders it",
        _ => null,
    };

    private static JsExpr Refused(InvocationExpressionSyntax invocation, IMethodSymbol method, string reason, ConversionContext context)
    {
        context.Report(invocation, ConversionSeverity.Error, "EQ2004",
            $"'{method.ContainingType.ToDisplayString()}.{method.Name}' has no JavaScript translation here: {reason}. "
            + "Check the argument with a comparison and throw the exception yourself.");
        return JsExpr.Opaque("undefined");
    }

    public int Priority => 30;
}
