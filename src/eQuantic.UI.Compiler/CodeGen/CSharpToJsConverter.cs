using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Robust C# to JavaScript expression converter using Roslyn AST
/// </summary>
public class CSharpToJsConverter
{
    private readonly TypeMappingRegistry _registry;
    private SemanticModel? _semanticModel;

    public CSharpToJsConverter()
    {
        _registry = new TypeMappingRegistry();
    }
    
    public void SetSemanticModel(SemanticModel? semanticModel)
    {
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Convert a C# expression string to JavaScript
    /// </summary>
    public string Convert(string code)
    {
        // Legacy/Fallback string conversion (compiles new tree)
        var parsed = CSharpSyntaxTree.ParseText(code).GetRoot();
        
        // Try to find the best node to convert
        var expr = parsed.DescendantNodes().OfType<ExpressionSyntax>().FirstOrDefault();
        if (expr != null) return ConvertExpression(expr);
        
        var block = parsed.DescendantNodes().OfType<BlockSyntax>().FirstOrDefault();
        if (block != null) return ConvertBlock(block);
        
        var stmt = parsed.DescendantNodes().OfType<StatementSyntax>().FirstOrDefault();
        if (stmt != null) return ConvertStatement(stmt);
        
        return code;
    }

    public string Convert(SyntaxNode node)
    {
        if (node is ExpressionSyntax expr) return ConvertExpression(expr);
        if (node is BlockSyntax block) return ConvertBlock(block);
        if (node is StatementSyntax stmt) return ConvertStatement(stmt);
        return node.ToString();
    }
    
    /// <summary>
    /// Convert a parsed expression to JavaScript
    /// </summary>
    public string ConvertExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            // Identifier: _count -> this._count (for private fields)
            IdentifierNameSyntax identifier => ConvertIdentifier(identifier),
            
            // Invocation: SetState(...) -> this.setState(...)
            InvocationExpressionSyntax invocation => ConvertInvocation(invocation),
            
            // Lambda: () => expr -> () => convertedExpr
            ParenthesizedLambdaExpressionSyntax lambda => ConvertLambda(lambda),
            SimpleLambdaExpressionSyntax simpleLambda => ConvertSimpleLambda(simpleLambda),
            
            // Prefix/Postfix: ++_count, _count++
            PrefixUnaryExpressionSyntax prefix => ConvertPrefixUnary(prefix),
            PostfixUnaryExpressionSyntax postfix => ConvertPostfixUnary(postfix),
            
            // Await: await Task
            AwaitExpressionSyntax awaitExpr => $"await {ConvertExpression(awaitExpr.Expression)}",

            // Assignment: _count = value
            AssignmentExpressionSyntax assignment => ConvertAssignment(assignment),
            
            // Binary: a + b, a == b
            BinaryExpressionSyntax binary => ConvertBinary(binary),
            
            // Member access: obj.property
            MemberAccessExpressionSyntax memberAccess => ConvertMemberAccess(memberAccess),
            
            // Interpolated string: $"text {expr}"
            InterpolatedStringExpressionSyntax interpolated => ConvertInterpolatedString(interpolated),
            
            // Literals
            LiteralExpressionSyntax literal => ConvertLiteral(literal),
            
            // Object creation: new Type() { ... }
            ObjectCreationExpressionSyntax objCreation => ConvertObjectCreation(objCreation),
            ImplicitObjectCreationExpressionSyntax implicitCreation => ConvertImplicitObjectCreation(implicitCreation),
            
            // Conditional: condition ? a : b
            ConditionalExpressionSyntax conditional => ConvertConditional(conditional),
            
            // Parenthesized: (expr)
            ParenthesizedExpressionSyntax parens => $"({ConvertExpression(parens.Expression)})",
            
            // Default: return as-is
            _ => expression.ToString()
        };
    }

    private string ConvertBlock(BlockSyntax block)
    {
        var sb = new StringBuilder();
        sb.Append("{"); // Use standard formatting
        foreach (var stmt in block.Statements)
        {
             sb.Append(ConvertStatement(stmt));
        }
        sb.Append("}");
        return sb.ToString();
    }
    
    private string ConvertStatement(StatementSyntax stmt)
    {
        if (stmt is ExpressionStatementSyntax exprStmt)
        {
            return ConvertExpression(exprStmt.Expression) + ";";
        }
        if (stmt is ReturnStatementSyntax retStmt)
        {
            return "return " + (retStmt.Expression != null ? ConvertExpression(retStmt.Expression) : "") + ";";
        }
        if (stmt is LocalDeclarationStatementSyntax decl)
        {
            var variable = decl.Declaration.Variables.First();
            var name = variable.Identifier.Text;
            var init = variable.Initializer != null ? ConvertExpression(variable.Initializer.Value) : "null";
            return $"let {name} = {init};";
        }
        if (stmt is IfStatementSyntax ifStmt)
        {
            var condition = ConvertExpression(ifStmt.Condition);
            var ifTrue = ConvertStatement(ifStmt.Statement);
            var ifFalse = ifStmt.Else != null ? " else " + ConvertStatement(ifStmt.Else.Statement) : "";
            return $"if ({condition}) {ifTrue}{ifFalse}";
        }
        if (stmt is ForEachStatementSyntax foreachStmt)
        {
             var item = foreachStmt.Identifier.Text;
             var collection = ConvertExpression(foreachStmt.Expression);
             var body = ConvertStatement(foreachStmt.Statement);
             return $"for (const {item} of {collection}) {body}";
        }
        if (stmt is BlockSyntax block)
        {
            return ConvertBlock(block);
        }
        
        return stmt.ToString(); 
    }
    
    private string ConvertIdentifier(IdentifierNameSyntax identifier)
    {
        var name = identifier.Identifier.Text;
        
        // Map 'Component' property (in State classes) to 'this._component'
        if (name == "Component") return "this._component";
        
        // Convert private field access: _fieldName -> this._fieldName
        if (name.StartsWith("_"))
        {
            return $"this.{name}";
        }
        
        return name;
    }

    private string ConvertInvocation(InvocationExpressionSyntax invocation)
    {
        var methodExpression = invocation.Expression;
        var methodName = methodExpression.ToString(); // Fallback for simple names
        
        // Handle member access (obj.Method)
        if (methodExpression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.Text;
        }

        // 1. Semantic Resolution (Phase 2 & 3)
        string? libraryMethodName = null;
        if (_semanticModel != null)
        {
            var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol != null)
            {
                // Get full name: System.Linq.Enumerable.Where
                var containingType = symbol.ContainingType.ToDisplayString();
                libraryMethodName = $"{containingType}.{symbol.Name}";
            }
        }
        
        // 2. Check Registry
        // First try semantic name, then fallback to simple name
        var mappedName = _semanticModel != null && libraryMethodName != null 
            ? _registry.GetMethodMapping(libraryMethodName) 
            : _registry.GetMethodMapping(methodName);
            
        var targetMethod = mappedName ?? methodName;
        
        // 3. Handle No-Op conversions (e.g. ToList -> "")
        if (targetMethod == "")
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 return ConvertExpression(access.Expression);
             }
             return "";
        }
        
        // 4. Resolve Arguments
        var args = string.Join(", ", invocation.ArgumentList.Arguments.Select(a => ConvertExpression(a.Expression)));

        // 5. Handle Special Prefixes (e.g., "!")
        if (targetMethod == "!")
        {
            return $"!({args})";
        }
        
        // 6. Handle Member Access invocation
        if (methodExpression is MemberAccessExpressionSyntax memAccess)
        {
             var caller = ConvertExpression(memAccess.Expression);
             return $"{caller}.{ToCamelCase(targetMethod)}({args})";
        }
        
        // 7. Handle Local Method Calls (this.Method) using Semantic Model
        if (_semanticModel != null)
        {
            var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol != null && !symbol.IsStatic && (symbol.ContainingType.Name == "StatefulComponent" || symbol.ContainingType.Name.EndsWith("State")))
            {
                 return $"this.{ToCamelCase(targetMethod)}({args})";
            }
        }
        // Fallback for Phase 1/2 without semantic model or if unsure: assume local if not standard library?
        if (targetMethod == methodName && char.IsUpper(targetMethod[0]))
        {
             return $"this.{ToCamelCase(targetMethod)}({args})";
        }

        return $"{targetMethod}({args})";
    }
    
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
    
    private string ConvertLambda(ParenthesizedLambdaExpressionSyntax lambda)
    {
        var parameters = string.Join(", ", lambda.ParameterList.Parameters.Select(p => p.Identifier.Text));
        var body = lambda.ExpressionBody != null 
            ? ConvertExpression(lambda.ExpressionBody)
            : lambda.Block != null 
                ? ConvertBlock(lambda.Block)
                : "";
        
        return $"({parameters}) => {body}";
    }
    
    private string ConvertSimpleLambda(SimpleLambdaExpressionSyntax lambda)
    {
        var param = lambda.Parameter.Identifier.Text;
        var body = lambda.ExpressionBody != null 
            ? ConvertExpression(lambda.ExpressionBody)
            : lambda.Block != null 
                ? ConvertBlock(lambda.Block)
                : "";
        
        return $"({param}) => {body}";
    }
    
    private string ConvertPrefixUnary(PrefixUnaryExpressionSyntax prefix)
    {
        var operand = ConvertExpression(prefix.Operand);
        var op = prefix.OperatorToken.Text;
        return $"{op}{operand}";
    }
    
    private string ConvertPostfixUnary(PostfixUnaryExpressionSyntax postfix)
    {
        var operand = ConvertExpression(postfix.Operand);
        var op = postfix.OperatorToken.Text;
        return $"{operand}{op}";
    }
    
    private string ConvertAssignment(AssignmentExpressionSyntax assignment)
    {
        var left = ConvertExpression(assignment.Left);
        var right = ConvertExpression(assignment.Right);
        var op = assignment.OperatorToken.Text;
        return $"{left} {op} {right}";
    }
    
    private string ConvertBinary(BinaryExpressionSyntax binary)
    {
        var left = ConvertExpression(binary.Left);
        var right = ConvertExpression(binary.Right);
        var op = binary.OperatorToken.Text;
        
        // Convert C# operators to JS equivalents
        op = op switch
        {
            "&&" => "&&",
            "||" => "||",
            "==" => "===", // Use strict equality in JS
            "!=" => "!==",
            _ => op
        };
        
        return $"{left} {op} {right}";
    }
    
    private string ConvertMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var expr = ConvertExpression(memberAccess.Expression);
        var name = memberAccess.Name.Identifier.Text;
        
        // Convert C# properties to JS
        name = name switch
        {
            "Length" => "length",
            "Count" => "length",
            _ => name
        };
        
        return $"{expr}.{name}";
    }
    
    private string ConvertInterpolatedString(InterpolatedStringExpressionSyntax interpolated)
    {
        var sb = new StringBuilder();
        sb.Append('`');
        
        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    sb.Append(text.TextToken.Text);
                    break;
                case InterpolationSyntax interpolation:
                    sb.Append("${");
                    sb.Append(ConvertExpression(interpolation.Expression));
                    sb.Append('}');
                    break;
            }
        }
        
        sb.Append('`');
        return sb.ToString();
    }
    
    private string ConvertLiteral(LiteralExpressionSyntax literal)
    {
        return literal.Kind() switch
        {
            SyntaxKind.StringLiteralExpression => $"'{EscapeString(literal.Token.ValueText)}'",
            SyntaxKind.TrueLiteralExpression => "true",
            SyntaxKind.FalseLiteralExpression => "false",
            SyntaxKind.NullLiteralExpression => "null",
            _ => literal.Token.Text
        };
    }
    
    private string ConvertObjectCreation(ObjectCreationExpressionSyntax objCreation)
    {
        var type = objCreation.Type.ToString();
        
        // Dictionary initializer: new() { ["key"] = "value" }
        if (type.StartsWith("Dictionary") || objCreation.Initializer != null)
        {
            return ConvertInitializer(objCreation.Initializer);
        }
        
        var args = objCreation.ArgumentList != null
            ? string.Join(", ", objCreation.ArgumentList.Arguments.Select(a => ConvertExpression(a.Expression)))
            : "";
        
        return $"new {type}({args})";
    }
    
    private string ConvertImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax creation)
    {
        if (creation.Initializer != null)
        {
            return ConvertInitializer(creation.Initializer);
        }
        return "{}";
    }
    
    private string ConvertInitializer(InitializerExpressionSyntax? initializer)
    {
        if (initializer == null) return "{}";
        
        var sb = new StringBuilder();
        sb.Append("{ ");
        
        var parts = new List<string>();
        foreach (var expr in initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax assignment)
            {
                var key = assignment.Left switch
                {
                    // Handle ["key"] syntax
                    ImplicitElementAccessSyntax elementAccess => 
                        elementAccess.ArgumentList.Arguments[0].Expression.ToString().Trim('"'),
                    _ => assignment.Left.ToString()
                };
                var value = ConvertExpression(assignment.Right);
                parts.Add($"{key}: {value}");
            }
        }
        
        sb.Append(string.Join(", ", parts));
        sb.Append(" }");
        return sb.ToString();
    }
    
    private string ConvertConditional(ConditionalExpressionSyntax conditional)
    {
        var condition = ConvertExpression(conditional.Condition);
        var whenTrue = ConvertExpression(conditional.WhenTrue);
        var whenFalse = ConvertExpression(conditional.WhenFalse);
        return $"{condition} ? {whenTrue} : {whenFalse}";
    }
    
    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
