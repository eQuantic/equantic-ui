using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Strategy interface for C# to TypeScript conversion.
/// </summary>
public interface IConversionStrategy
{
    /// <summary>
    /// Checks if this strategy can convert the given node.
    /// </summary>
    bool CanConvert(SyntaxNode node, ConversionContext context);

    /// <summary>
    /// Converts the node to TypeScript.
    /// </summary>
    string Convert(SyntaxNode node, ConversionContext context);

    /// <summary>
    /// Priority of the strategy (higher = executed first).
    /// </summary>
    int Priority => 0;
}
