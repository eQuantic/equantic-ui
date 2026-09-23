using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Whether an argument the twin has no use for can be left out of the emission. A format provider
/// is the case: the twin reads the invariant culture (EQ2110), so the value is never needed, and
/// its translation would not even run (<c>CultureInfo</c> has no twin). But C# evaluates every
/// argument in written order, so leaving one out is only faithful when evaluating it cannot be
/// observed: a local, a parameter, a field, a literal, or a property of the BCL itself
/// (<c>CultureInfo.InvariantCulture</c>). Anything that computes, a call, a new object, or a
/// property of the app's own whose getter may do anything, would run in C# and not here, and the
/// caller refuses it rather than dropping it in silence.
/// </summary>
internal static class DroppedArgument
{
    public static bool IsUnobservable(ExpressionSyntax argument, ConversionContext context) =>
        IsPureRead(context.SemanticHelper.GetOperation(argument));

    private static bool IsPureRead(IOperation? operation) => operation switch
    {
        IConversionOperation { Operand: var operand } => IsPureRead(operand),
        IParenthesizedOperation { Operand: var inner } => IsPureRead(inner),
        ILocalReferenceOperation or IParameterReferenceOperation or ILiteralOperation
            or IDefaultValueOperation or IInstanceReferenceOperation => true,
        IFieldReferenceOperation { Instance: var instance } => instance is null || IsPureRead(instance),
        IPropertyReferenceOperation { Property: var property, Instance: var instance } =>
            IsTheBcls(property) && (instance is null || IsPureRead(instance)),
        _ => false,
    };

    private static bool IsTheBcls(ISymbol symbol)
    {
        var home = symbol.ContainingNamespace?.ToDisplayString() ?? "";
        return home == "System" || home.StartsWith("System.", StringComparison.Ordinal);
    }
}
