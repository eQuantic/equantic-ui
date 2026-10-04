using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// <see cref="TypeSymbolExtensions.IsNamed"/> reads a type's NAME: a <c>Nullable&lt;T&gt;</c> is
/// unwrapped, and a reference type's nullable annotation is not part of it. The display string
/// carries the annotation, so a <c>CultureInfo?</c> parameter, which every BCL one is, never matched,
/// and a string overload taking one slipped past its refusal (#528).
/// </summary>
public class TypeIsNamedTests
{
    [Theory]
    [InlineData("System.Globalization.CultureInfo? value", "System.Globalization.CultureInfo")]
    [InlineData("System.Globalization.CultureInfo value", "System.Globalization.CultureInfo")]
    [InlineData("System.DateTime? value", "System.DateTime")]
    [InlineData("System.StringComparison value", "System.StringComparison")]
    public void AParametersType_IsNamedWithoutItsAnnotation(string parameter, string name) =>
        ParameterType(parameter).IsNamed(name).Should().BeTrue();

    [Fact]
    public void AnotherType_IsNotNamedSo() =>
        ParameterType("System.IFormatProvider? value").IsNamed("System.Globalization.CultureInfo").Should().BeFalse();

    private static ITypeSymbol ParameterType(string parameter)
    {
        var tree = CSharpSyntaxTree.ParseText($"#nullable enable\nclass C {{ void M({parameter}) {{ }} }}");
        var compilation = CSharpCompilation.Create("IsNamed", [tree],
            [TestReferences.Of(typeof(object))]);
        var declared = tree.GetRoot().DescendantNodes().OfType<ParameterSyntax>().Single();
        return compilation.GetSemanticModel(tree).GetDeclaredSymbol(declared)!.Type;
    }
}
