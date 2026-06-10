using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A `[Page]` file that co-declares its <c>ComponentState&lt;T&gt;</c> subclass must emit the state class
/// EXACTLY ONCE — complete, inside the page's own module (state fields + setState + handlers + ctor +
/// build), which is what <c>createState()</c> news up. It must NOT also produce a standalone
/// <c>&lt;State&gt;</c> module: that was a broken, duplicated emit carrying only <c>build()</c> and
/// referencing state fields/handlers it never declared — a latent landmine if ever imported.
/// </summary>
public class ComponentStateEmissionTests
{
    private const string Src = @"
using eQuantic.UI.Core;
using eQuantic.UI.Components;
namespace App;

[Page(""/counter"")]
public class Counter : StatefulComponent {
    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter> {
    private int _count = 0;
    public void Increment() => SetState(() => _count++);
    public void Reset() => SetState(() => _count = 0);
    public override IComponent Build(RenderContext context)
        => new Box { Children = { new Text(_count.ToString()) } };
}";

    private readonly ITestOutputHelper _out;
    public ComponentStateEmissionTests(ITestOutputHelper o) => _out = o;

    [Fact]
    public void StateClass_NotEmittedAsStandaloneModule()
    {
        var results = new ComponentCompiler().CompileSource(Src).ToList();
        _out.WriteLine("modules: " + string.Join(", ", results.Select(r => r.ComponentName)));

        results.Should().NotContain(r => r.ComponentName == "CounterState",
            "a ComponentState<T> subclass is emitted inside its owning page module, never as a standalone module");
    }

    [Fact]
    public void StateClass_EmittedCompleteInsidePageModule()
    {
        var page = new ComponentCompiler().CompileSource(Src).Single(r => r.ComponentName == "Counter");
        _out.WriteLine("=== Counter.ts ===");
        _out.WriteLine(page.TypeScript);

        // The page module carries the FULL state class, not just a build() shell.
        page.TypeScript.Should().Contain("class CounterState");
        page.TypeScript.Should().Contain("_count");                  // state field declared
        page.TypeScript.Should().Contain("setState");                // mutation entry point
        page.TypeScript.Should().Contain("increment");               // handler method
        page.TypeScript.Should().Contain("reset");                   // handler method
        page.TypeScript.Should().Contain("new CounterState(this)");  // createState() wires it from this module
    }
}
