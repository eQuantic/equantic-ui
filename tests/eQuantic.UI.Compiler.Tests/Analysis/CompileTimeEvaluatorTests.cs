using eQuantic.UI.Compiler.Analysis;
using eQuantic.UI.Web.Styling;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Analysis;

public class CompileTimeEvaluatorTests
{
    private (SemanticModel semanticModel, SyntaxTree tree) CreateCompilation(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CompileTimeEvaluateAttribute).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        return (semanticModel, syntaxTree);
    }

    [Fact]
    public void TryEvaluate_SimpleReadonlyField_ReturnsValue()
    {
        // Arrange
        var code = @"
using eQuantic.UI.Web.Styling;

namespace Test;

[CompileTimeEvaluate]
public readonly struct TestClass
{
    private readonly string _value;
    public TestClass(string value) { _value = value; }
    public static implicit operator string(TestClass tc) => tc._value;
}

public static class TestData
{
    public static readonly TestClass Flex = new TestClass(""flex"");
}

public class Usage
{
    public void Method()
    {
        var x = TestData.Flex;
    }
}
";

        var (semanticModel, tree) = CreateCompilation(code);
        var root = tree.GetRoot();
        var expression = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .First(n => n.Identifier.Text == "Flex" &&
                       n.Parent is MemberAccessExpressionSyntax);

        var evaluator = new CompileTimeEvaluator(semanticModel);

        // Act
        var result = evaluator.TryEvaluate(expression.Parent as ExpressionSyntax);

        // Assert
        Assert.Equal("flex", result);
    }

    [Fact]
    public void TryEvaluate_ObjectCreationWithStringLiteral_ReturnsValue()
    {
        // Arrange
        var code = @"
using eQuantic.UI.Web.Styling;

namespace Test;

[CompileTimeEvaluate]
public readonly struct TestClass
{
    private readonly string _value;
    public TestClass(string value) { _value = value; }
    public static implicit operator string(TestClass tc) => tc._value;
}

public class Usage
{
    public void Method()
    {
        var x = new TestClass(""flex"");
    }
}
";

        var (semanticModel, tree) = CreateCompilation(code);
        var root = tree.GetRoot();
        var expression = root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .First();

        var evaluator = new CompileTimeEvaluator(semanticModel);

        // Act
        var result = evaluator.TryEvaluate(expression);

        // Assert
        Assert.Equal("flex", result);
    }

    [Fact]
    public void TryEvaluate_BinaryAddExpression_CombinesValues()
    {
        // Arrange
        var code = @"
using eQuantic.UI.Web.Styling;

namespace Test;

[CompileTimeEvaluate]
public readonly struct TestClass
{
    private readonly string _value;
    public TestClass(string value) { _value = value; }
    public static implicit operator string(TestClass tc) => tc._value;
    public static TestClass operator +(TestClass left, TestClass right)
    {
        if (string.IsNullOrEmpty(left._value)) return right;
        if (string.IsNullOrEmpty(right._value)) return left;
        return new TestClass($""{left._value} {right._value}"");
    }
}

public static class TestData
{
    public static readonly TestClass Flex = new TestClass(""flex"");
    public static readonly TestClass Row = new TestClass(""flex-row"");
}

public class Usage
{
    public void Method()
    {
        var x = TestData.Flex + TestData.Row;
    }
}
";

        var (semanticModel, tree) = CreateCompilation(code);
        var root = tree.GetRoot();
        var expression = root.DescendantNodes()
            .OfType<BinaryExpressionSyntax>()
            .First();

        var evaluator = new CompileTimeEvaluator(semanticModel);

        // Act
        var result = evaluator.TryEvaluate(expression);

        // Assert
        Assert.Equal("flex flex-row", result);
    }

    [Fact]
    public void TryEvaluate_TypeWithoutAttribute_ReturnsNull()
    {
        // Arrange
        var code = @"
namespace Test;

public readonly struct TestClass
{
    private readonly string _value;
    public TestClass(string value) { _value = value; }
}

public static class TestData
{
    public static readonly TestClass Flex = new TestClass(""flex"");
}

public class Usage
{
    public void Method()
    {
        var x = TestData.Flex;
    }
}
";

        var (semanticModel, tree) = CreateCompilation(code);
        var root = tree.GetRoot();
        var expression = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .First(n => n.Identifier.Text == "Flex" &&
                       n.Parent is MemberAccessExpressionSyntax);

        var evaluator = new CompileTimeEvaluator(semanticModel);

        // Act
        var result = evaluator.TryEvaluate(expression.Parent as ExpressionSyntax);

        // Assert
        Assert.Null(result);
    }
}
