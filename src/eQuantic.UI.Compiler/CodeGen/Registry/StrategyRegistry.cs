using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen.Registry;

/// <summary>
/// Registry for managing and retrieving conversion strategies.
/// </summary>
public class StrategyRegistry
{
    private readonly List<IConversionStrategy> _strategies = new();

    /// <summary>
    /// Registers a new strategy type properly instantiated.
    /// </summary>
    public void Register<T>() where T : IConversionStrategy, new()
    {
        _strategies.Add(new T());
    }

    /// <summary>
    /// Registers a strategy instance.
    /// </summary>
    public void Register(IConversionStrategy strategy)
    {
        _strategies.Add(strategy);
    }

    /// <summary>
    /// Finds the highest priority strategy that can convert the given node.
    /// </summary>
    public IConversionStrategy? FindStrategy(SyntaxNode node, ConversionContext context)
    {
        return _strategies
            .Where(s => s.CanConvert(node, context))
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault();
    }
}
