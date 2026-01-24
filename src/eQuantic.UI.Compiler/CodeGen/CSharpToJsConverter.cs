using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Strategies;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;
using eQuantic.UI.Compiler.CodeGen.Strategies.Special;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using eQuantic.UI.Compiler.CodeGen.Registry;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Robust C# to JavaScript expression converter using Roslyn AST
/// </summary>
public class CSharpToJsConverter
{
    private readonly TypeMappingRegistry _registry;
    private readonly StrategyRegistry _strategyRegistry;
    private readonly ConversionContext _context;
    private SemanticModel? _semanticModel;

    public CSharpToJsConverter()
    {
        _registry = new TypeMappingRegistry();
        _context = new ConversionContext { Converter = this };
        _strategyRegistry = new StrategyRegistry();

        RegisterStrategies();
    }
    
    public void SetSemanticModel(SemanticModel? semanticModel)
    {
        _semanticModel = semanticModel;
        _context.SemanticModel = semanticModel;
        _context.SemanticHelper = new SemanticHelper(semanticModel);
    }

    private void RegisterStrategies()
    {
        _strategyRegistry.Register<AnyStrategy>();
        _strategyRegistry.Register<EnumStrategy>();
        _strategyRegistry.Register<NullableStrategy>();
        _strategyRegistry.Register<NamespaceRemovalStrategy>();
        
        // LINQ Strategies
        _strategyRegistry.Register<SelectStrategy>();
        _strategyRegistry.Register<WhereStrategy>();
        _strategyRegistry.Register<FirstStrategy>();
        _strategyRegistry.Register<AllStrategy>();
        
        // Expression Strategies
        _strategyRegistry.Register<InvocationStrategy>();
        _strategyRegistry.Register<MemberAccessStrategy>();
        _strategyRegistry.Register<ObjectCreationStrategy>();
        _strategyRegistry.Register<BinaryExpressionStrategy>();
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
        // Check cache
        var cached = _context.GetCached(expression);
        if (cached != null) return cached;

        // Try strategies
        var strategy = _strategyRegistry.FindStrategy(expression, _context);
        if (strategy != null)
        {
            var result = strategy.Convert(expression, _context);
            _context.SetCached(expression, result);
            return result;
        }

        return expression switch
        {
            // Identifier: _count -> this._count (for private fields)
            IdentifierNameSyntax identifier => ConvertIdentifier(identifier),
            

            
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
            

            
            // Interpolated string: $"text {expr}"
            InterpolatedStringExpressionSyntax interpolated => ConvertInterpolatedString(interpolated),
            
            // Literals
            LiteralExpressionSyntax literal => ConvertLiteral(literal),
            

            
            // Conditional: condition ? a : b
            ConditionalExpressionSyntax conditional => ConvertConditional(conditional),
            
            // Parenthesized: (expr)
            ParenthesizedExpressionSyntax parens => $"({ConvertExpression(parens.Expression)})",
            
            

            
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

        // Map 'Console' to global 'console'
        // Priority: Semantic Check > String Check (Fallback)
        var symbol = _context.SemanticHelper.GetSymbol(identifier);
        if (_context.SemanticHelper.IsSystemConsole(symbol)) return "console";
        if (_semanticModel == null && name == "Console") return "console";
        
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
    


    public string ConvertInitializer(InitializerExpressionSyntax? initializer)
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
