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
            
            // Is Pattern: x is string s
            IsPatternExpressionSyntax isPattern => ConvertIsPattern(isPattern),
            
            // Binary: a + b, a == b
            BinaryExpressionSyntax binary => ConvertBinary(binary),
            
            // Member access: obj.property
            MemberAccessExpressionSyntax memberAccess => ConvertMemberAccess(memberAccess),
            
            // Interpolated string: $"text {expr}"
            InterpolatedStringExpressionSyntax interpolated => ConvertInterpolatedString(interpolated),
            
            // Literals
            LiteralExpressionSyntax literal => ConvertLiteral(literal),
            
            // Object creation: handled below
            ImplicitObjectCreationExpressionSyntax implicitCreation => ConvertImplicitObjectCreation(implicitCreation),
            
            // Conditional: condition ? a : b
            ConditionalExpressionSyntax conditional => ConvertConditional(conditional),
            
            // Parenthesized: (expr)
            ParenthesizedExpressionSyntax parens => $"({ConvertExpression(parens.Expression)})",
            
            
            // Object Creation: new Button { Text = "X" }
            ObjectCreationExpressionSyntax objCreation => ConvertObjectCreation(objCreation),
            
            // Initialization: { ... }
            InitializerExpressionSyntax initializer => ConvertInitializer(initializer),

            // Lambda: () => ...
            LambdaExpressionSyntax lambda => ConvertLambda(lambda),

            // Default: return as-is
            _ => expression.ToString()
        };
    }

    private string ConvertIsPattern(IsPatternExpressionSyntax isPattern)
    {
        var expr = ConvertExpression(isPattern.Expression);
        if (isPattern.Pattern is DeclarationPatternSyntax decl)
        {
            var type = decl.Type.ToString();
            var name = decl.Designation is SingleVariableDesignationSyntax variable ? variable.Identifier.Text : "_";
            
            // Very simplified conversion: (name = expr, typeof expr === 'type')
            var jsType = type switch
            {
                "string" => "'string'",
                "int" or "double" or "float" or "long" or "decimal" or "number" => "'number'",
                "bool" or "boolean" => "'boolean'",
                _ => null
            };

            if (jsType != null)
            {
                return $"((() => {{ {name} = {expr}; return typeof {expr} === {jsType}; }})())";
            }
        }
        return "false";
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
        
        // Convert private field access or PascalCase properties to this.camelCase
        if (name.StartsWith("_"))
        {
            return $"this.{name}";
        }
        
        // If it starts with Uppercase, it's likely a property of the component
        if (char.IsUpper(name[0]))
        {
            return $"this.{ToCamelCase(name)}";
        }
        
        return name;
    }

    private string ConvertInvocation(InvocationExpressionSyntax invocation)
    {
        var methodExpression = invocation.Expression;
        var fullMethodName = methodExpression.ToString();
        var methodName = fullMethodName;
        
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
        if (targetMethod == "" || targetMethod == "ToList" || targetMethod == "ToArray")
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 return ConvertExpression(access.Expression);
             }
             return "";
        }
        
        // 4. Resolve Arguments
        var argsList = new List<string>();
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.RefOrOutKeyword.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword)
            {
                if (arg.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.DeclarationExpressionSyntax decl)
                {
                    argsList.Add(decl.Designation.ToString());
                }
                else
                {
                    argsList.Add(arg.Expression.ToString().Trim());
                }
            }
            else
            {
                argsList.Add(ConvertExpression(arg.Expression));
            }
        }
        var args = string.Join(", ", argsList);
        
        // Handle Nullable properties
        if (targetMethod == "HasValue" && fullMethodName.EndsWith(".HasValue"))
        {
            if (methodExpression is MemberAccessExpressionSyntax access)
            {
                return $"{ConvertExpression(access.Expression)} !== null";
            }
        }
        if (targetMethod == "Value" && fullMethodName.EndsWith(".Value"))
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 return ConvertExpression(access.Expression);
             }
        }
        
        // Handle common library mappings
        if (targetMethod == "Join" && (libraryMethodName == "System.String.Join" || fullMethodName.Contains("String.Join") || fullMethodName.Contains("string.Join")))
        {
            if (argsList.Count >= 2)
            {
                var separator = argsList[0];
                var array = argsList[1];
                return $"{array}.join({separator})";
            }
        }
        
        if (targetMethod == "IsNullOrEmpty" && (libraryMethodName == "System.String.IsNullOrEmpty" || fullMethodName.Contains("String.IsNullOrEmpty") || fullMethodName.Contains("string.IsNullOrEmpty")))
        {
            if (argsList.Count > 0)
            {
                return $"!{argsList[0]}";
            }
        }

        if (libraryMethodName?.StartsWith("System.Math") == true || fullMethodName.StartsWith("Math.") || fullMethodName.StartsWith("math."))
        {
            if (targetMethod == "Clamp" && argsList.Count >= 3)
            {
                return $"Math.min(Math.max({argsList[0]}, {argsList[1]}), {argsList[2]})";
            }
            return $"Math.{ToCamelCase(targetMethod)}({args})";
        }
        
        // Handle string.IsNullOrWhiteSpace / IsNullOrEmpty
        if (methodExpression is MemberAccessExpressionSyntax staticAccess && ConvertExpression(staticAccess.Expression) == "string")
        {
            var arg = invocation.ArgumentList.Arguments.First().Expression;
            var argJs = ConvertExpression(arg);
            
            if (targetMethod == "IsNullOrWhiteSpace")
                return $"(!{argJs} || {argJs}.trim() === '')";
                
            if (targetMethod == "IsNullOrEmpty")
                return $"(!{argJs} || {argJs} === '')";
        }

        // 5. Handle Special Prefixes (e.g., "!")
        if (targetMethod == "!")
        {
            return $"!({args})";
        }
        if (libraryMethodName == "eQuantic.UI.Core.HtmlNode.Text" || fullMethodName == "HtmlNode.Text" || fullMethodName == "htmlNode.Text")
        {
            return $"{{ tag: '#text', textContent: {argsList[0]} }}";
        }
        
        // Dictionary Methods Mapped to JS/TS patterns
        if (targetMethod == "TryGetValue" || targetMethod == "TryGetValueOrDefault" || targetMethod == "GetValueOrDefault")
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 var caller = ConvertExpression(access.Expression);
                 var key = argsList.Count > 0 ? argsList[0] : "''";
                 
                 // Handle 'out var s' pattern by assigning it
                 if (argsList.Count > 1)
                 {
                     var outVar = argsList[1];
                     return $"({outVar} = {caller}[{key}]) !== undefined";
                 }
                 
                 return $"{caller}[{key}]";
             }
        }

        // 6. Handle Member Access invocation
        if (methodExpression is MemberAccessExpressionSyntax memAccess)
        {
             var caller = ConvertExpression(memAccess.Expression);
             
             // Map common LINQ/Collection methods to JS
             var jsMethod = targetMethod switch
             {
                 "Select" => "map",
                 "Where" => "filter",
                 "Any" => "some",
                 "All" => "every",
                 "First" => "find",
                 "FirstOrDefault" => "find",
                 "Add" => "push",
                 "AddRange" => "push",
                 "AddChild" => "Children.push", // Specific to framework
                 _ => ToCamelCase(targetMethod)
             };

             if (jsMethod == "Children.push")
             {
                 return $"{caller}.Children.push({args})";
             }

             return $"{caller}.{jsMethod}({args})";
        }
        
        // 7. Handle Local Method Calls (this.Method) using Semantic Model
        if (_semanticModel != null)
        {
            var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol != null && !symbol.IsStatic && (symbol.ContainingType.Name == "StatefulComponent" || symbol.ContainingType.Name.EndsWith("State") || symbol.ContainingType.Name == "HtmlElement" || (symbol.ContainingType is ITypeSymbol typeSymbol && typeSymbol.BaseType?.Name == "HtmlElement")))
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
    
    private string ConvertLambda(LambdaExpressionSyntax lambda)
    {
        if (lambda is ParenthesizedLambdaExpressionSyntax parenthesized)
        {
            var parameters = string.Join(", ", parenthesized.ParameterList.Parameters.Select(p => p.Identifier.Text));
            var body = parenthesized.Block != null ? ConvertBlock(parenthesized.Block) : ConvertExpression(parenthesized.ExpressionBody);
            return $"({parameters}) => {body}";
        }
        
        if (lambda is SimpleLambdaExpressionSyntax simple)
        {
            var param = simple.Parameter.Identifier.Text;
            var body = simple.Block != null ? ConvertBlock(simple.Block) : ConvertExpression(simple.ExpressionBody);
            return $"({param}) => {body}";
        }
        
        return "() => {}";
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
        
        // Handle discard _ = ...
        if (left == "_" || left == "this._") return right;

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
        // Check BEFORE converting if this is an eQuantic namespace that should be removed
        var originalExpr = memberAccess.Expression.ToString();
        var shouldRemoveNamespace = originalExpr.StartsWith("eQuantic.UI.Core.") ||
                                     originalExpr.StartsWith("eQuantic.UI.");

        var expr = ConvertExpression(memberAccess.Expression);
        var name = memberAccess.Name.Identifier.Text;

        // Remove eQuantic.UI.Core namespace prefix (enums, types that don't exist in JS runtime)
        if (shouldRemoveNamespace)
        {
            // Extract just the type name (e.g., "Display" from "eQuantic.UI.Core.Display")
            var parts = originalExpr.Split('.');
            expr = ToCamelCase(parts[^1]); // Last part is the type name, apply camelCase
        }

        // Convert C# properties to JS
        name = name switch
        {
            "Length" => "length",
            "Count" => "length",
            "HasValue" => "!= null",
            "Value" => "",
            _ => ToCamelCase(name)
        };

        if (name == "!= null")
        {
            return $"({expr} != null)";
        }
        if (name == "")
        {
            return expr;
        }

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
        var typeName = objCreation.Type.ToString();
        var arguments = "";
        
        if (objCreation.ArgumentList != null && objCreation.ArgumentList.Arguments.Count > 0)
        {
             arguments = string.Join(", ", objCreation.ArgumentList.Arguments.Select(a => ConvertExpression(a.Expression)));
        }

        var initializer = "";
        if (objCreation.Initializer != null)
        {
            initializer = ConvertInitializer(objCreation.Initializer);
            // Verify if constructor args exist to decide on merging or appending
            if (string.IsNullOrEmpty(arguments))
            {
                arguments = initializer;
            }
            else
            {
                // If arguments exist, usually the initializer is a separate argument or merged options?
                // For UI libraries, usually new Widget(arg, { options }) or new Widget({ ... })
                // Assuming typical pattern: append as last argument
                arguments += ", " + initializer;
            }
        }

        // Special handling for common .NET Generic Collections
        if (typeName.StartsWith("List<") || typeName.StartsWith("IEnumerable<"))
        {
            return string.IsNullOrEmpty(arguments) || arguments == "{}" ? "[]" : arguments;
        }
        if (typeName.StartsWith("Dictionary<"))
        {
            return string.IsNullOrEmpty(arguments) || arguments == "[]" ? "{}" : arguments;
        }
        
        // Handle framework types that are interfaces in TS
        if (typeName == "HtmlNode" || typeName == "HtmlStyle")
        {
            return string.IsNullOrEmpty(arguments) ? "{}" : arguments;
        }
        
        return $"new {typeName}({arguments})";
    }

    private string ConvertImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax creation)
    {
        if (creation.Initializer != null)
        {
             // Implicit creation usually implies component structure in this framework context if inside Children
             // But valid JS needs explicit type or just config object? 
             // If we don't know the type, we return the config object.
             // Ideally we should know the type from context, but we lack that here.
             // Given the failure was "Expected }", sticking to object literal is safer than invalid "new ()".
            return ConvertInitializer(creation.Initializer);
        }
        return "{}";
    }

    private string ConvertInitializer(InitializerExpressionSyntax? initializer)
    {
        if (initializer == null) return "{}";
        
        // Collection Initializer: { new A(), new B() } -> [ new A(), new B() ]
        if (initializer.Kind() == SyntaxKind.CollectionInitializerExpression)
        {
            var elements = initializer.Expressions.Select(e => ConvertExpression(e));
            return $"[{string.Join(", ", elements)}]";
        }
        
        // Object Initializer: { Prop = Value } -> { prop: value }
        if (initializer.Kind() == SyntaxKind.ObjectInitializerExpression)
        {
            var props = new List<string>();
            foreach (var expr in initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    var propName = assignment.Left.ToString();
                    var value = ConvertExpression(assignment.Right);
                    
                    // Special handling for Children in initialization
                    if (propName == "Children" && assignment.Right is InitializerExpressionSyntax childInit)
                    {
                        // Explicitly convert collection initializer to array
                         value = ConvertInitializer(childInit);
                    }
                    
                    props.Add($"{ToCamelCase(propName)}: {value}");
                }
            }
            return $"{{ {string.Join(", ", props)} }}";
        }
        
        return "{}";
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
