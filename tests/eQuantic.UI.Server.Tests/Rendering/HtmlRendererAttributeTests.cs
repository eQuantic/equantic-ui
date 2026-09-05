using System.Collections.Generic;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// Regression tests for the SSR attribute-collapse bug: value="true" used to be rendered
/// as a bare valueless attribute, which corrupts real values and breaks ARIA-state and
/// enumerated attributes.
/// </summary>
public class HtmlRendererAttributeTests
{
    private static string Render(Dictionary<string, string?> attributes) =>
        HtmlRenderer.RenderNode(new HtmlNode { Tag = "div", Attributes = attributes });

    [Fact]
    public void AriaStateAttributes_KeepExplicitTrueFalse()
    {
        var html = Render(new Dictionary<string, string?>
        {
            ["aria-checked"] = "true",
            ["aria-expanded"] = "false",
        });

        html.Should().Contain("aria-checked=\"true\"");
        html.Should().Contain("aria-expanded=\"false\"");
    }

    [Fact]
    public void EnumeratedAttribute_KeepsTrueValue()
    {
        // draggable is enumerated, not boolean: draggable="true" must NOT collapse to draggable.
        Render(new Dictionary<string, string?> { ["draggable"] = "true" })
            .Should().Contain("draggable=\"true\"");
    }

    [Fact]
    public void RealValueAttribute_IsNotCorruptedByTrue()
    {
        Render(new Dictionary<string, string?> { ["value"] = "true" })
            .Should().Contain("value=\"true\"");
    }

    [Fact]
    public void ValueEqualToKey_CollapsesToBareAttribute()
    {
        // The disabled="disabled" idiom still collapses to a valueless attribute.
        var html = Render(new Dictionary<string, string?> { ["disabled"] = "disabled" });
        html.Should().Contain(" disabled");
        html.Should().NotContain("disabled=");
    }
}
