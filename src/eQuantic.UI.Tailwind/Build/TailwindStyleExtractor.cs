using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using eQuantic.UI.Compiler.Analysis;
using eQuantic.UI.Core.Build;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Tailwind.Build;

/// <summary>
/// Tailwind-specific style extractor.
/// Extracts CSS classes from ClassBuilder calls and TW.* constants.
/// </summary>
public class TailwindStyleExtractor : CSharpSyntaxWalker, IStyleExtractor
{
    private readonly SemanticModel _semanticModel;
    private readonly CompileTimeEvaluator _evaluator;
    private readonly HashSet<string> _extractedClasses = new();
    
    // For source lookup
    private string _targetMethodName = "";
    private int _targetParamCount = 0;

    public TailwindStyleExtractor(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
        _evaluator = new CompileTimeEvaluator(semanticModel);
    }

    // Use explicit interface implementation to avoid confusing with CSharpSyntaxWalker.Visit
    void IStyleExtractor.Visit(object syntaxNode)
    {
        if (syntaxNode is SyntaxNode node)
        {
            // Call the base class Visit method directly for syntax tree traversal
            base.Visit(node);
        }
    }

    public IEnumerable<string> GetClasses() => _extractedClasses;

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        base.VisitInvocationExpression(node);
        
        var symbol = _semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
        if (symbol == null) return;

        // 1. Resolve Prefix via Symbolic Analysis
        var prefix = AnalyzeMethodForPrefix(symbol);
        
        // 2. Check if it's a Builder Method (ClassBuilder or Extension)
        bool isBuilderMethod = prefix != null || IsClassBuilder(symbol.ContainingType) || IsClassBuilderExtension(symbol);
        
        if (isBuilderMethod)
        {
            ExtractFromMethod(node, prefix ?? "");
        }
    }

    private void ExtractFromMethod(InvocationExpressionSyntax node, string prefix)
    {
        foreach (var arg in node.ArgumentList.Arguments)
        {
            string? val = null;
            
            // Strategy 1: Direct literal string extraction (most common case)
            if (arg.Expression is LiteralExpressionSyntax literal && 
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                val = literal.Token.ValueText;
            }
            // Strategy 2: Use the robust CompileTimeEvaluator for TW.* expressions
            else
            {
                val = _evaluator.TryEvaluate(arg.Expression);
            }
            
            // Strategy 3: Fallback to simple constant evaluation
            if (val == null)
            {
                var constant = _semanticModel.GetConstantValue(arg.Expression);
                if (constant.HasValue && constant.Value is string s)
                    val = s;
            }
            
            if (!string.IsNullOrWhiteSpace(val))
            {
                // Split by whitespace to handle multi-class strings like "from-blue-600 to-purple-600"
                var classes = val.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var cls in classes)
                {
                    _extractedClasses.Add(prefix + cls);
                }
            }
        }
    }

    private string? AnalyzeMethodForPrefix(IMethodSymbol method)
    {
        try 
        {
            var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
            SyntaxNode? methodNode = syntaxRef?.GetSyntax() ?? FindSourceForExternalSymbol(method);

            if (methodNode is MethodDeclarationSyntax methodDecl)
            {
                // Check Expression Body: => WithPrefix("dark:", classes)
                if (methodDecl.ExpressionBody != null)
                {
                    var p = AnalyzeExpressionForPrefix(methodDecl.ExpressionBody.Expression);
                    if (p != null) return p;
                }
                
                // Check Block Body
                if (methodDecl.Body != null)
                {
                     foreach (var stmt in methodDecl.Body.Statements)
                     {
                         if (stmt is ReturnStatementSyntax ret && ret.Expression != null)
                         {
                             var p = AnalyzeExpressionForPrefix(ret.Expression);
                             if (p != null) return p;
                         }
                     }
                }
            }
        }
        catch { }

        return null;
    }

    private SyntaxNode? FindSourceForExternalSymbol(IMethodSymbol method)
    {
        if (method.ContainingAssembly?.Name != "eQuantic.UI.Core") return null;
        
        _targetMethodName = method.Name;
        _targetParamCount = method.Parameters.Length;
        
        var currentPath = _semanticModel.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(currentPath)) return null;

        var dir = System.IO.Path.GetDirectoryName(currentPath);
        while (dir != null)
        {
            var directPath = System.IO.Path.Combine(dir, "eQuantic.UI.Core", "Styling", "ClassBuilder.cs");
            if (System.IO.File.Exists(directPath)) return ParseFile(directPath);

            var srcPath = System.IO.Path.Combine(dir, "src", "eQuantic.UI.Core", "Styling", "ClassBuilder.cs");
            if (System.IO.File.Exists(srcPath)) return ParseFile(srcPath);

            dir = System.IO.Path.GetDirectoryName(dir);
        }

        return null;
    }

    private SyntaxNode? ParseFile(string path)
    {
        try 
        {
            var code = System.IO.File.ReadAllText(path);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            
            return root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == _targetMethodName && 
                                   m.ParameterList.Parameters.Count == _targetParamCount);
        }
        catch
        {
            return null;
        }
    }

    private string? AnalyzeExpressionForPrefix(ExpressionSyntax expr)
    {
        if (expr is InvocationExpressionSyntax inv)
        {
             if (inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == "WithPrefix")
             {
                 if (inv.ArgumentList.Arguments.Count > 0)
                 {
                     var arg0 = inv.ArgumentList.Arguments[0].Expression;
                     if (arg0 is LiteralExpressionSyntax lit && lit.Token.Value is string val)
                     {
                         return val;
                     }
                 }
             }
        }
        return null;
    }

    private bool IsClassBuilder(INamedTypeSymbol? type)
    {
        if (type == null) return false;
        return type.Name == "ClassBuilder" && 
               type.ContainingNamespace.ToString() == "eQuantic.UI.Core.Styling";
    }

    private bool IsClassBuilderExtension(IMethodSymbol method)
    {
        return method.IsExtensionMethod && 
               method.Parameters.Length > 0 && 
               IsClassBuilder(method.Parameters[0].Type as INamedTypeSymbol);
    }
}
