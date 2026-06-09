using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>unsafe { … }</c> is just a context marker — it grants permission for pointer/memory operations,
/// which themselves raise EQ2001 when present. Unwrap the block and convert its body (consistent with
/// <see cref="FixedStatementStrategy"/>); plain code inside an unsafe block transpiles normally, while
/// any genuine pointer use inside still fails the build.
/// </summary>
public class UnsafeStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is UnsafeStatementSyntax;

    public string Convert(StatementSyntax node, ConversionContext context) =>
        context.Converter.ConvertBlock(((UnsafeStatementSyntax)node).Block);

    public int Priority => 10;
}
