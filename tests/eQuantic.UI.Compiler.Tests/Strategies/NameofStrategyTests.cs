using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class NameofStrategyTests
{
    [Fact]
    public void Nameof_SimpleVariable_ReturnsVariableName()
    {
        var result = TestHelper.ConvertExpression("nameof(x)");
        result.Should().Be("'x'");
    }

    [Fact]
    public void Nameof_Property_ReturnsPropertyName()
    {
        var result = TestHelper.ConvertExpression("nameof(obj.Property)");
        result.Should().Be("'Property'");
    }

    [Fact]
    public void Nameof_NestedProperty_ReturnsLastPropertyName()
    {
        var result = TestHelper.ConvertExpression("nameof(obj.SubObj.Value)");
        result.Should().Be("'Value'");
    }

    [Fact]
    public void Nameof_Method_ReturnsMethodName()
    {
        var result = TestHelper.ConvertExpression("nameof(GetData)");
        result.Should().Be("'GetData'");
    }

    [Fact]
    public void Nameof_GenericType_ReturnsTypeName()
    {
        var result = TestHelper.ConvertExpression("nameof(List)");
        result.Should().Be("'List'");
    }

    [Fact]
    public void Nameof_Parameter_ReturnsParameterName()
    {
        var result = TestHelper.ConvertExpression("nameof(value)");
        result.Should().Be("'value'");
    }

    [Fact]
    public void Nameof_Field_ReturnsFieldName()
    {
        var result = TestHelper.ConvertExpression("nameof(_data)");
        result.Should().Be("'_data'");
    }

    [Fact]
    public void Nameof_Constant_ReturnsConstantName()
    {
        var result = TestHelper.ConvertExpression("nameof(MaxValue)");
        result.Should().Be("'MaxValue'");
    }
}
