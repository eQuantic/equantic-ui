using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>lock (o) body</c> — single-threaded on the other side, so the body alone.</summary>
public class LockStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LockStatementSyntax;
    }

    /// <summary>
    /// The body, with nothing to hold: a page has one thread. The expression still runs, in front, as
    /// C# evaluates it before it takes the lock: a call in it ran nowhere (#475), and one that
    /// DECLARES something (<c>lock (Gate(out var held))</c>) declares it where Roslyn scopes it, the
    /// enclosing block, for the body to read. Only a read with nothing to run is left out.
    /// </summary>
    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var lockStmt = (LockStatementSyntax)node;
        var body = context.Converter.ConvertStatementIr(lockStmt.Statement);
        if (ExpressionVariableScanner.Names(lockStmt.Expression).Count == 0
            && ReadsOnly(context.SemanticHelper.GetOperation(lockStmt.Expression)))
            return body;
        var expression = JsStatement.Expression(context.Converter.ConvertIr(lockStmt.Expression));
        // Declared in front, unless a switch declares them for the section this lock stands in.
        var declared = ExpressionVariableScanner.InFrontOf(lockStmt, lockStmt.Expression, context.TypeAnnotations);
        return declared.Length == 0
            ? JsStatement.Sequence(expression, body)
            : JsStatement.Sequence(JsStatement.Raw(declared.TrimEnd()), expression, body);
    }

    /// <summary>Whether the expression only reads what is already there (a local, a parameter, a
    /// field, <c>this</c>), so evaluating it does nothing. A property may run a getter, and runs.</summary>
    private static bool ReadsOnly(IOperation? operation) => operation switch
    {
        IConversionOperation conversion => ReadsOnly(conversion.Operand),
        ILocalReferenceOperation or IParameterReferenceOperation or IInstanceReferenceOperation
            or ILiteralOperation => true,
        IFieldReferenceOperation field => field.Instance is null || ReadsOnly(field.Instance),
        _ => false,
    };

    public int Priority => 10;
}
