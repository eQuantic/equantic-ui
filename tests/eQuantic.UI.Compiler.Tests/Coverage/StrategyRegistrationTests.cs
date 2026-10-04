using System.Reflection;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Registry;
using eQuantic.UI.Compiler.CodeGen.Strategies;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// Every expression strategy the compiler declares is one the converter registers. A strategy no
/// registry holds compiles, can be unit-tested by itself, and never runs: <c>GetHashCodeStrategy</c>
/// was written and the calls it lowers still reached the browser as a <c>getHashCode</c> nothing
/// defines, until the conformance suite executed one (#519). The registration ORDER stays by hand,
/// since it breaks ties between equal priorities, so this reads the registry rather than filling it.
/// </summary>
public class StrategyRegistrationTests
{
    [Fact]
    public void EveryExpressionStrategy_IsRegistered()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        var registry = typeof(CSharpToJsConverter).GetField("_strategyRegistry", Private)!.GetValue(new CSharpToJsConverter())!;
        var registered = ((IEnumerable<IConversionStrategy>)typeof(StrategyRegistry).GetField("_strategies", Private)!
            .GetValue(registry)!).Select(strategy => strategy.GetType()).ToHashSet();

        var declared = typeof(IConversionStrategy).Assembly.GetTypes()
            .Where(type => typeof(IConversionStrategy).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
            .ToList();

        declared.Should().NotBeEmpty();
        declared.Where(type => !registered.Contains(type)).Select(type => type.FullName).Should().BeEmpty(
            "a strategy no registry holds never runs: register it in CSharpToJsConverter.RegisterStrategies");
    }
}
