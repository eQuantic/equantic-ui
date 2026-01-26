using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests;

public class BulletproofTests
{
    private readonly CSharpToJsConverter _converter = new CSharpToJsConverter();

    private string ConvertMethodBody(string bodyCode)
    {
        var classCode = $"class Wrapper {{ void Method() {{ {bodyCode} }} }}";
        var root = CSharpSyntaxTree.ParseText(classCode).GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        // We trim the result to make assertions easier, and remove outer braces if present
        var result = _converter.Convert(method.Body).Trim();
        if (result.StartsWith("{") && result.EndsWith("}"))
        {
             // return result.Substring(1, result.Length - 2).Trim();
             // Actually, keep braces to match block structure of the method body
             return result;
        }
        return result;
    }

    [Fact]
    public void Using_Statement_Generates_TryFinally()
    {
        var code = @"
        using (var res = new Resource()) 
        {
            Console.WriteLine(res);
        }";

        var js = ConvertMethodBody(code);

        Assert.Contains("const res = new Resource();", js);
        Assert.Contains("try {", js);
        Assert.Contains("finally {", js);
        Assert.Contains("if (res && typeof res.dispose === 'function')", js);
        Assert.Contains("res.dispose();", js);
    }

    [Fact]
    public void Async_Foreach_Generates_ForAwait()
    {
        var code = @"
        await foreach (var item in stream)
        {
            Console.WriteLine(item);
        }";

        var js = ConvertMethodBody(code);

        Assert.Contains("for await (const item of stream)", js);
    }

    [Fact]
    public void Switch_Expression_Generates_IIFE()
    {
        var code = @"
        var y = x switch 
        {
            > 10 => ""Big"",
            { Status: ""Active"" } => ""Active"",
            _ => ""Default""
        };";

        var js = ConvertMethodBody(code);

        Assert.Contains("(() => {", js);
        Assert.Contains("const _s = x;", js);
        Assert.Contains("if (_s > 10) return 'Big';", js);
        // Note: Simple casing assumption in current logic, might depend on casing helpers
        // The implementation uses camelCase for properties if they match conventions, 
        // OR explicit recursive pattern logic uses char.ToLowerInvariant
        // Let's check regex or flexible assertion if exact string is tricky
        Assert.Contains("if (_s.status === 'Active') return 'Active';", js);
        Assert.Contains("return 'Default';", js);
        Assert.Contains("})()", js);
    }
}
