using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen.Registry;

/// <summary>
/// Registry for managing and retrieving conversion strategies.
/// </summary>
public class StrategyRegistry
{
    private readonly List<IConversionStrategy> _strategies = new();
    private bool _ordered;

    /// <summary>
    /// Registers a new strategy type properly instantiated.
    /// </summary>
    public void Register<T>() where T : IConversionStrategy, new()
    {
        _strategies.Add(new T());
        _ordered = false;
    }

    /// <summary>
    /// Registers a strategy instance.
    /// </summary>
    public void Register(IConversionStrategy strategy)
    {
        _strategies.Add(strategy);
        _ordered = false;
    }

    /// <summary>
    /// Finds the highest priority strategy that can convert the given node. The list is sorted ONCE
    /// (stable, so equal priorities keep registration order — the tie-break the registration
    /// sequence has always encoded) and the scan stops at the first match, instead of running every
    /// strategy's <c>CanConvert</c> on every node and sorting the matches each time.
    /// </summary>
    public IConversionStrategy? FindStrategy(SyntaxNode node, ConversionContext context)
    {
        if (!_ordered)
        {
            // List<T>.Sort is unstable; OrderByDescending is the stable sort this ordering needs.
            var sorted = _strategies.OrderByDescending(s => s.Priority).ToList();
            _strategies.Clear();
            _strategies.AddRange(sorted);
            _ordered = true;
        }

        foreach (var strategy in _strategies)
        {
            if (strategy.CanConvert(node, context)) return strategy;
        }
        return null;
    }
}
