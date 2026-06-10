using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Enums are represented at runtime as their member-name string (EnumStrategy), so numeric casts can't
/// reach the underlying value the way .NET does — `(int)'pending'` would be NaN. The cast strategy bridges
/// it with the enum's compile-time name↔value table: constant-fold to a literal when possible, else inline
/// a generated map indexed by the operand. (TestHelper's enums: Status { Active=0, Pending=1, Inactive=2 }.)
/// </summary>
public class EnumCastStrategyTests
{
    // ============ (int)enum — to the underlying value ============

    [Fact]
    public void IntCast_OfEnumConstant_FoldsToLiteralValue()
    {
        var js = TestHelper.ConvertCodeBlock("var z = (int)Status.Pending;");
        js.Should().Contain("= 1");
        js.Should().NotContain("Math.trunc");
        js.Should().NotContain("'pending'");
    }

    [Fact]
    public void IntCast_OfEnumVariable_InlinesNameToValueMap()
    {
        var js = TestHelper.ConvertCodeBlock("var z = (int)status;");
        js.Should().Contain("({ 'active': 0, 'pending': 1, 'inactive': 2 })[this.status]");
        js.Should().NotContain("Math.trunc");
    }

    // ============ (EnumType)int — to the member-name string ============

    [Fact]
    public void EnumCast_OfConstantInt_FoldsToMemberNameString()
    {
        var js = TestHelper.ConvertCodeBlock("var z = (Status)2;");
        js.Should().Contain("'inactive'");
    }

    [Fact]
    public void EnumCast_OfIntVariable_InlinesValueToNameMap()
    {
        var js = TestHelper.ConvertCodeBlock("var z = (Status)x;");
        js.Should().Contain("({ 0: 'active', 1: 'pending', 2: 'inactive' })[this.x]");
    }

    // ============ comparison / non-enum casts are untouched ============

    [Fact]
    public void EnumEquality_StillComparesMemberStrings()
    {
        var js = TestHelper.ConvertCodeBlock("var z = status == Status.Pending;");
        js.Should().Contain("this.status === 'pending'");
    }
}
