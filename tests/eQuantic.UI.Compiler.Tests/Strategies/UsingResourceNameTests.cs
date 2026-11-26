using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A <c>using</c> with no declaration, <c>using (Open())</c>, holds its resource under one fixed name, so the same C#
/// writes the same module on every compile. It drew the name from a fresh <c>Guid</c>, and the module changed each
/// time, which no pin could hold and no build could reproduce (found by #1865's emission shape). The block the lowering
/// opens is the name's scope, so a nested one shadows it there and disposes its own, which the conformance suite runs.
/// </summary>
public class UsingResourceNameTests
{
    private const string Code = """
        using ((System.IDisposable?)null)
        {
            var x = 1;
        }
        """;

    [Fact]
    public void AUsingWithNoDeclaration_WritesTheSameModuleOnEveryCompile()
    {
        var first = TestHelper.ConvertCodeBlock(Code);
        var second = TestHelper.ConvertCodeBlock(Code);

        second.Should().Be(first);
        first.Should().Contain("const $disposable = ");
    }
}
