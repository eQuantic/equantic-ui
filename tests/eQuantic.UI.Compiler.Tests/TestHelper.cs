using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using System.Reflection;

namespace eQuantic.UI.Compiler.Tests;

public static class TestHelper
{
    public static string ConvertExpression(string code, string? expectedType = null)
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
                public List<string> items {{ get; set; }}
                public Dictionary<string, string> dict {{ get; set; }}
                public int Id {{ get; set; }}
                public string Name {{ get; set; }}
                public string str {{ get; set; }}
                public bool Active {{ get; set; }}
                public string a {{ get; set; }}
                public string b {{ get; set; }}
                public string c {{ get; set; }}
                public TestClass item {{ get; set; }}

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
            return converter.ConvertExpression(exprStmt.Expression, expectedType);
        }
        
        return converter.Convert(stmt);
    }
    
    public static string ConvertStatement(string code, string? expectedType = null)
    {
        return ConvertExpression(code, expectedType);
    }

    /// <summary>
    /// Converts a multi-line code block with proper context
    /// SIMPLIFIED VERSION - for basic testing
    /// </summary>
    public static string ConvertCodeBlock(string code)
    {
        var converter = new CSharpToJsConverter();

        // Minimal working code with just what's needed
        var fullCode = $@"
            using System;
            using System.Linq;
            using System.Collections.Generic;

            public enum Status {{ Active, Pending, Inactive }}
            public enum OrderStatus {{ New, Processing, Shipped, Delivered }}

            public class Address {{
                public string City {{ get; set; }}
            }}

            public class User {{
                public string Name {{ get; set; }}
                public string FirstName {{ get; set; }}
                public string FullName {{ get; set; }}
                public string Email {{ get; set; }}
                public bool IsActive {{ get; set; }}
                public Address Address {{ get; set; }}
            }}

            public class Item {{
                public int Id {{ get; set; }}
                public string Name {{ get; set; }}
                public decimal Price {{ get; set; }}
                public bool IsActive {{ get; set; }}
                public int Priority {{ get; set; }}
            }}

            public class Order {{
                public int Id {{ get; set; }}
                public DateTime Date {{ get; set; }}
                public OrderStatus Status {{ get; set; }}
            }}

            public class TestClass {{
                public int x {{ get; set; }}
                public int y {{ get; set; }}
                public string name {{ get; set; }}
                public string input {{ get; set; }}
                public string filterText {{ get; set; }}
                public string key {{ get; set; }}
                public int? cachedCount {{ get; set; }}
                public User user {{ get; set; }}
                public Item newItem {{ get; set; }}
                public Item selectedItem {{ get; set; }}
                public List<Item> items {{ get; set; }}
                public List<Order> orders {{ get; set; }}
                public List<string> list1 {{ get; set; }}
                public List<string> list2 {{ get; set; }}
                public List<string> excludeList {{ get; set; }}
                public List<string> errors {{ get; set; }}
                public Dictionary<string, string> cache {{ get; set; }}
                public Status status {{ get; set; }}

                public string FetchData() {{ return """"; }}
                public string FetchValue(string k) {{ return """"; }}

                public void Method() {{
                    {code}
                }}
            }}";

        var tree = CSharpSyntaxTree.ParseText(fullCode);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);

        // Check for compilation errors
        var diagnostics = compilation.GetDiagnostics();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Any())
        {
            throw new InvalidOperationException(
                "Code compilation failed:\n" +
                string.Join("\n", errors.Select(e => e.GetMessage())));
        }

        converter.SetSemanticModel(semanticModel);

        // Find the Method() in TestClass (should be the last method in the tree)
        var method = tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .LastOrDefault(m => m.Identifier.Text == "Method");

        if (method == null)
            throw new InvalidOperationException("No method found in code");

        var methodBody = method.Body;
        if (methodBody == null || methodBody.Statements.Count == 0)
            return string.Empty;

        // Convert all statements in the block
        var results = new List<string>();
        foreach (var stmt in methodBody.Statements)
        {
            try
            {
                var converted = converter.Convert(stmt);
                if (!string.IsNullOrWhiteSpace(converted))
                {
                    results.Add(converted);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert statement: {stmt}\nError: {ex.Message}", ex);
            }
        }

        return string.Join("\n", results);
    }
}
