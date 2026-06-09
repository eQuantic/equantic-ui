using eQuantic.UI.Components.Inputs;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Regression tests for multiple-select pre-selection (#28). Previously a Select with
/// Multiple=true never reported any option as selected.
/// </summary>
public class SelectMultipleTests
{
    [Fact]
    public void Multiple_SelectsAllValuesInTheSet()
    {
        var select = new Select { Multiple = true, Values = new[] { "a", "c" } };

        select.IsValueSelected("a").Should().BeTrue();
        select.IsValueSelected("b").Should().BeFalse();
        select.IsValueSelected("c").Should().BeTrue();
    }

    [Fact]
    public void Multiple_FallsBackToDefaultValues()
    {
        var select = new Select { Multiple = true, DefaultValues = new[] { "b" } };

        select.IsValueSelected("a").Should().BeFalse();
        select.IsValueSelected("b").Should().BeTrue();
    }

    [Fact]
    public void Multiple_WithNoSelection_SelectsNothing()
    {
        var select = new Select { Multiple = true };

        select.IsValueSelected("a").Should().BeFalse();
    }

    [Fact]
    public void Single_StillMatchesScalarValue()
    {
        var select = new Select { Value = "x" };

        select.IsValueSelected("x").Should().BeTrue();
        select.IsValueSelected("y").Should().BeFalse();
    }
}
