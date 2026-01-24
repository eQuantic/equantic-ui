using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using System.Reflection;

namespace eQuantic.UI.Compiler.Tests;

public static class TestHelper
{
    public static string ConvertExpression(string code)
    {
        var converter = new CSharpToJsConverter();
        
        // Setup minimal semantic model environment
        var tree = CSharpSyntaxTree.ParseText($@"
            using System;
            using System.Linq;
            using System.Collections.Generic;
            
            public class Order {{
                public int Id {{ get; set; }}
                public decimal Total {{ get; set; }}
            }}

            public class TestClass {{
                public List<TestClass> list {{ get; set; }}
                public List<TestClass> otherList {{ get; set; }}
                public List<Order> Orders {{ get; set; }}
                public int Id {{ get; set; }}
                public string Name {{ get; set; }}
                public bool Active {{ get; set; }}
                
                public void Method() {{
                    {code};
                }}
            }}");
            
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { tree }, 
            new[] { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location), // Collections
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location) // Core Runtime
            });
            
        var semanticModel = compilation.GetSemanticModel(tree);
        converter.SetSemanticModel(semanticModel);

        var methodBody = tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First()
            .Body;

        var stmt = methodBody!.Statements.First();
        
        if (stmt is ExpressionStatementSyntax exprStmt)
        {
            return converter.ConvertExpression(exprStmt.Expression);
        }
        
        return converter.Convert(stmt);
    }
    
    public static string ConvertStatement(string code)
    {
        return ConvertExpression(code);
    }
}
