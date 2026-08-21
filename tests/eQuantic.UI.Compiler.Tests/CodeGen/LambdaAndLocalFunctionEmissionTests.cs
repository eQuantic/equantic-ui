using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.CodeGen;

/// <summary>
/// Arrows through the authoritative harness: an object-literal body gets its parentheses (the
/// bare braces read as a block and the arrow returned undefined), a local function's two body
/// forms lay out the same, an async lambda keeps its modifier.
/// </summary>
public class LambdaAndLocalFunctionEmissionTests
{
    private static string Flat(string js) => Regex.Replace(js, @"\s+", "");

    [Theory]
    [InlineData("var h = list1.Select(s => new { Len = s.Length });", "(s) => ({ len: s.length })")]
    [InlineData("var g = () => new { Name = name };", "() => ({ name: this.name })")]
    [InlineData("var f = (int x) => new { A = x, B = 2 };", "(x) => ({ a: x, b: 2 })")]
    public void AnObjectLiteralBody_IsParenthesized(string code, string expected)
    {
        TestHelper.ConvertCodeBlock(code).Should().Contain(expected);
    }

    [Fact]
    public void AnExpressionBody_ThatIsNotALiteral_StaysBare()
    {
        TestHelper.ConvertCodeBlock("var m = list1.Select(s => s.Length + 1);").Should().Contain("(s) => s.length + 1");
    }

    [Fact]
    public void LocalFunctions_ExpressionAndBlockBodies_LayOutTheSame()
    {
        var js = TestHelper.ConvertCodeBlock(
            "int Twice(int x) => x * 2; int Thrice(int x) { return x * 3; } var r = Twice(1) + Thrice(1);");
        Flat(js).Should().Contain("consttwice=(x)=>{returnx*2;};");
        Flat(js).Should().Contain("constthrice=(x)=>{returnx*3;};");
        js.Should().NotContain("{ return");
        js.Should().Contain("=> {\n");
    }

    [Fact]
    public void AnAsyncLambda_KeepsItsModifier()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;

            public sealed class Probe : StatelessComponent
            {
                public Func<Task<int>> Later => async () => await Task.FromResult(1);
                public override VisualNode Build(ComponentContext context) => new Box();
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

        var result = compiler.CompileSource(source, "Probe.cs").Single();

        result.TypeScript.Should().Contain("async () => await");
    }
}
