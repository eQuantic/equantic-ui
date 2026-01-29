using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Join.
/// Not strictly implemented due to complexity in JS without helper.
/// Returns placeholder or simple nested loop map if simple.
/// </summary>
public class JoinStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Join");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // Join(inner, outerKey, innerKey, resultSelector)
        // This usually requires a helper function.
        // For now, emit a warning wrapper.
        return "/* Join not fully supported without runtime helper */ []";
    }

    public int Priority => 10;
}
