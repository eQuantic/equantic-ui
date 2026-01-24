using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

public interface IStatementStrategy
{
    bool CanConvert(StatementSyntax node, ConversionContext context);
    string Convert(StatementSyntax node, ConversionContext context);
    int Priority { get; }
}
