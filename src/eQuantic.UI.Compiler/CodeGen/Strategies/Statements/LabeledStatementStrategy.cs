using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// A C# label survives as a JS label — same syntax, same meaning. C# 15's labeled
/// <c>break</c>/<c>continue</c> (<c>break outer;</c>) target it exactly as JavaScript's do, so the
/// pair translates 1:1 (see <see cref="BreakStatementStrategy"/>/<see cref="ContinueStatementStrategy"/>).
/// A label whose only consumer would be <c>goto</c> is harmless in the output — <c>goto</c> itself
/// stays a build error (EQ2002).
/// </summary>
public class LabeledStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is LabeledStatementSyntax;

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var labeled = (LabeledStatementSyntax)node;
        return $"{labeled.Identifier.Text}: {context.Converter.ConvertStatement(labeled.Statement)}";
    }

    public int Priority => 10;
}
