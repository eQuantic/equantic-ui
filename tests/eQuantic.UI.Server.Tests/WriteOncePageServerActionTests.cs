using System.Reflection;
using eQuantic.UI.Core;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The registry must see actions on BOTH component families. The write-once page
/// (Primitives.StatefulComponent) answered "Action not found" to its own stub, because the scan
/// only knew the Core bases — the client half worked, the server half had never met the shape.
/// </summary>
public class WriteOncePageServerActionTests
{
    private sealed class ProbePage : eQuantic.UI.Primitives.StatefulComponent
    {
        [ServerAction]
        public Task<string> Compile(string code) => Task.FromResult(code);

        public override eQuantic.UI.Primitives.VisualNode Build(eQuantic.UI.Primitives.ComponentContext context) =>
            new eQuantic.UI.Primitives.Column(gap: 0);
    }

    [Fact]
    public void ScanAssembly_FindsActions_OnWriteOncePages()
    {
        var registry = new ServerActionRegistry();
        registry.ScanAssembly(typeof(WriteOncePageServerActionTests).Assembly);

        var action = registry.GetAction("ProbePage/Compile");
        action.Should().NotBeNull("the write-once page family carries server actions too");
        action!.ComponentType.Should().Be(typeof(ProbePage));
    }
}
