using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// An enum member is its name as a string whatever it is called (#631). One named like a member of
/// <c>Nullable&lt;T&gt;</c> was refused by its name before the model was asked, and reached the browser
/// as a read of an object nothing defines: <c>CodeCompletionKind.Value</c> became
/// <c>CodeCompletionKind.value</c>.
/// </summary>
public class EnumMemberNameTests
{
    [Fact]
    public void AMemberNamedValueOrHasValue_IsItsNameAsAString()
    {
        var js = TestHelper.ConvertClass("""
            public enum Reading { Value, HasValue, Other }

            public string Read(Reading reading) =>
                reading == Reading.Value ? "v" : reading == Reading.HasValue ? "h" : "o";
            """);

        js.Should().Contain("=== 'value'").And.Contain("=== 'hasValue'");
        js.Should().NotContain("Reading.value").And.NotContain("Reading.hasValue");
    }

    [Fact]
    public void NullablesOwnValueAndHasValue_StillReadAsANullables()
    {
        var js = TestHelper.ConvertCodeBlock("var known = cachedCount.HasValue; var count = cachedCount.Value;");

        js.Should().NotContain(".hasValue").And.NotContain(".value",
            "a nullable is its value or null in the browser, with no members of its own");
    }
}
