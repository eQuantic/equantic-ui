using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Scenarios;

/// <summary>
/// Testes para verificar se a LÓGICA de transpilação existe (não é problema de infraestrutura)
/// </summary>
public class TranspilationCapabilityTests
{
    [Fact]
    public void Transpilation_NullCoalescingAssignmentWorks()
    {
        // Este teste usa apenas tipos básicos
        var code = @"
            name ??= ""default"";
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("??");
        result.Should().Contain("name = ");
    }

    [Fact]
    public void Transpilation_NameofWorks()
    {
        var code = @"
            var fieldName = nameof(name);
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("'name'");
    }

    [Fact]
    public void Transpilation_DefaultWorks()
    {
        var code = @"
            var defaultValue = default(string);
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("null");
    }

    [Fact]
    public void Transpilation_StringMethodsChainWorks()
    {
        var code = @"
            var result = name.Trim().ToLower();
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("trim()");
        result.Should().Contain("toLowerCase()");
    }

    [Fact]
    public void Transpilation_ConditionalAccessWorks()
    {
        var code = @"
            var length = name?.Length;
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("?.");
    }

    [Fact]
    public void Transpilation_StringIsNullOrWhiteSpaceWorks()
    {
        var code = @"
            var isEmpty = string.IsNullOrWhiteSpace(name);
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("(!this.name || !this.name.trim())");
    }

    [Fact]
    public void Transpilation_IfStatementWorks()
    {
        var code = @"
            if (x > 10)
                y = 20;
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("if");
        result.Should().Contain("x > 10");
    }

    [Fact]
    public void Transpilation_ComplexConditionalWorks()
    {
        var code = @"
            if (string.IsNullOrWhiteSpace(name))
                name = ""Guest"";
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("if");
        result.Should().Contain("(!this.name || !this.name.trim())");
        result.Should().Contain("this.name = 'Guest'");
    }

    [Fact]
    public void Transpilation_CombinedOperatorsWork()
    {
        var code = @"
            name ??= ""default"";
            var fieldName = nameof(name);
            var normalized = name.Trim().ToLower();
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("??");
        result.Should().Contain("'name'");
        result.Should().Contain("trim()");
        result.Should().Contain("toLowerCase()");
    }

    // ============ TESTES QUE VAMOS COMPARAR COM OS QUE FALHARAM ============

    [Fact]
    public void Transpilation_FormValidationPattern_WithBasicTypes()
    {
        // Mesmo padrão do RealWorldUITests mas SEM classes customizadas
        var code = @"
            name ??= """";
            if (string.IsNullOrWhiteSpace(name))
            {
                var fieldName = nameof(name);
            }
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        // Deve conter TODAS as conversões esperadas
        result.Should().Contain("this.name ?? (this.name = '')");
        result.Should().Contain("(!this.name || !this.name.trim())");
        result.Should().Contain("'name'");
    }

    [Fact]
    public void Transpilation_NullCoalescingWithDefault_Works()
    {
        // Padrão similar ao que falhou mas com tipos básicos
        var code = @"
            var defaultValue = default(int);
            var defaultStr = default(string);
            var nameOrDefault = name ?? defaultStr;
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("let defaultValue = 0");
        result.Should().Contain("let defaultStr = null");
        result.Should().Contain("this.name ?? defaultStr");
    }

    [Fact]
    public void Transpilation_ChainedNullCoalescing_Works()
    {
        var code = @"
            name ??= ""first"";
            name ??= ""second"";
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        // Deve ter 2 ocorrências de ??
        var count = result.Split("??").Length - 1;
        count.Should().BeGreaterThanOrEqualTo(2);
    }
}
