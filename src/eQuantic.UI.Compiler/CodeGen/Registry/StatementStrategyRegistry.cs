using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Strategies;


namespace eQuantic.UI.Compiler.CodeGen.Registry;

public class StatementStrategyRegistry
{
    private readonly List<IStatementStrategy> _strategies = new();

    public void Register<T>() where T : IStatementStrategy, new()
    {
        _strategies.Add(new T());
    }
    
    // Sort by priority descending (higher priority first)
    // Cache generic order once
    private bool _ordered = false;

    public IStatementStrategy? FindStrategy(StatementSyntax node, ConversionContext context)
    {
        if (!_ordered)
        {
            _strategies.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _ordered = true;
        }

        foreach (var strategy in _strategies)
        {
            if (strategy.CanConvert(node, context))
            {
                return strategy;
            }
        }
        return null; // No strategy found
    }
}
