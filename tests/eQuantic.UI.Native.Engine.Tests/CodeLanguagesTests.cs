using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The language registry finds a name however it is cased, and with or without an extension's dot.
/// It leaned on a case-insensitive comparer, which the transpiled twin never had: the browser keyed
/// a plain object exactly, so <c>For("CSharp")</c> coloured C# natively and plain text on the web
/// (found in review, #359). The keys are normalized by the registry itself now, on both sides, and
/// <c>code-editor.spec.ts</c> asks the twin the same questions.
/// </summary>
public class CodeLanguagesTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("CSharp")]
    [InlineData("C#")]
    [InlineData("cs")]
    [InlineData(".CS")]
    public void ANameFindsItsLanguageInAnyCase(string name)
    {
        CodeLanguages.For(name).Should().BeSameAs(CodeLanguages.CSharp);
    }

    [Fact]
    public void ARegisteredDialectIsFoundInAnyCase_AndByItsExtension()
    {
        var dialect = new JsonLanguage();

        CodeLanguages.Register(".EqProbeDialect", dialect);

        CodeLanguages.For("eqprobedialect").Should().BeSameAs(dialect);
        CodeLanguages.For(".EQPROBEDIALECT").Should().BeSameAs(dialect);
    }

    [Fact]
    public void AnUnknownNameIsPlainText_NotAnError()
    {
        CodeLanguages.For("NoSuchLanguage").Should().BeSameAs(CodeLanguages.PlainText);
    }
}
