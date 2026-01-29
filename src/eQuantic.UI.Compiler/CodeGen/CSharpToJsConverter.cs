using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Strategies;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;
using eQuantic.UI.Compiler.CodeGen.Strategies.Special;
using eQuantic.UI.Compiler.CodeGen.Strategies.Statements;
using eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;
using eQuantic.UI.Compiler.CodeGen.Registry;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Robust C# to JavaScript expression converter using Roslyn AST
/// </summary>
public class CSharpToJsConverter
{
    private readonly TypeMappingRegistry _registry;
    private readonly StrategyRegistry _strategyRegistry;
    private readonly StatementStrategyRegistry _statementRegistry;
    private readonly ConversionContext _context;
    private SemanticModel? _semanticModel;

    public CSharpToJsConverter()
    {
        _registry = new TypeMappingRegistry();
        _context = new ConversionContext 
        { 
            Converter = this,
            SemanticHelper = new SemanticHelper(null)
        };
        _strategyRegistry = new StrategyRegistry();
        _statementRegistry = new StatementStrategyRegistry();

        RegisterStrategies();
    }
    
    public void SetSemanticModel(SemanticModel? semanticModel)
    {
        _semanticModel = semanticModel;
        _context.SemanticModel = semanticModel;
        _context.SemanticHelper = new SemanticHelper(semanticModel);
    }

    public void SetCurrentClass(string? className)
    {
        _context.CurrentClassName = className;
    }

    public HashSet<string> UsedHelpers => _context.UsedHelpers;

    private void RegisterStrategies()
    {
        _strategyRegistry.Register<AnyStrategy>();
        _strategyRegistry.Register<EnumStrategy>();
        _strategyRegistry.Register<NullableStrategy>();
        _strategyRegistry.Register<TupleExpressionStrategy>();
        _strategyRegistry.Register<GuidTypeStrategy>();
        _strategyRegistry.Register<NamespaceRemovalStrategy>();
        
        // LINQ Strategies
        _strategyRegistry.Register<SelectStrategy>();
        _strategyRegistry.Register<WhereStrategy>();
        _strategyRegistry.Register<FirstStrategy>();
        _strategyRegistry.Register<LastStrategy>();
        _strategyRegistry.Register<SingleStrategy>();
        _strategyRegistry.Register<AllStrategy>();
        _strategyRegistry.Register<AnyStrategy>();
        _strategyRegistry.Register<CountStrategy>();
        _strategyRegistry.Register<OrderByStrategy>();
        _strategyRegistry.Register<SkipStrategy>();
        _strategyRegistry.Register<TakeStrategy>();
        _strategyRegistry.Register<DistinctStrategy>();
        _strategyRegistry.Register<ContainsStrategy>();
        _strategyRegistry.Register<SelectManyStrategy>();
        _strategyRegistry.Register<SumStrategy>();
        _strategyRegistry.Register<AverageStrategy>();
        _strategyRegistry.Register<MinMaxStrategy>();
        _strategyRegistry.Register<ReverseStrategy>();
        
        // Primitive Type Strategies (higher priority than InvocationStrategy)
        _strategyRegistry.Register<StringMethodStrategy>();
        _strategyRegistry.Register<StringStaticStrategy>();
        _strategyRegistry.Register<ListMethodStrategy>();

        // Expression Strategies
        _strategyRegistry.Register<InvocationStrategy>();
        _strategyRegistry.Register<MemberAccessStrategy>();
        _strategyRegistry.Register<ElementAccessStrategy>();
        _strategyRegistry.Register<ObjectCreationStrategy>();
        _strategyRegistry.Register<BinaryExpressionStrategy>();
        _strategyRegistry.Register<SwitchExpressionStrategy>();
        _strategyRegistry.Register<NullCoalescingStrategy>();
        _strategyRegistry.Register<ConditionalAccessStrategy>();
        _strategyRegistry.Register<IndexFromEndStrategy>();
        
        // Statement Strategies
        _statementRegistry.Register<IfStatementStrategy>();
        _statementRegistry.Register<ForStatementStrategy>();
        _statementRegistry.Register<ForEachStatementStrategy>();
        _statementRegistry.Register<ReturnStatementStrategy>();
        _statementRegistry.Register<LocalDeclarationStrategy>();
        _statementRegistry.Register<ExpressionStatementStrategy>();
        _statementRegistry.Register<SwitchStatementStrategy>();
        _statementRegistry.Register<WhileStatementStrategy>();
        _statementRegistry.Register<DoWhileStatementStrategy>();
        _statementRegistry.Register<BreakStatementStrategy>();
        _statementRegistry.Register<ContinueStatementStrategy>();
        _statementRegistry.Register<ThrowStatementStrategy>();
        _statementRegistry.Register<TryStatementStrategy>();
        _statementRegistry.Register<UsingStatementStrategy>();
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
    /// Convert a parsed expression to JavaScript with an expected type hint
    /// </summary>
    public string ConvertExpression(ExpressionSyntax expression, string? expectedType = null)
    {
        _context.ExpectedType = expectedType;
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
            
            // Declaration: var (a, b)
            DeclarationExpressionSyntax declExpr => ConvertDeclarationExpression(declExpr),
            

            
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
        
        // Use the robust pattern conversion logic
        // We need to find a way to reuse the ConvertPattern from SwitchExpressionStrategy.
        // I'll create a shared utility or just implement it here for now.
        return ConvertPattern(isPattern.Pattern, expr);
    }

    private string ConvertPattern(PatternSyntax pattern, string varName)
    {
        return pattern switch
        {
            ConstantPatternSyntax constant => $"{varName} === {ConvertExpression(constant.Expression)}",
            RelationalPatternSyntax relational => $"{varName} {relational.OperatorToken.Text} {ConvertExpression(relational.Expression)}",
            DeclarationPatternSyntax declaration => ConvertDeclarationPattern(declaration, varName),
            RecursivePatternSyntax recursive => ConvertRecursivePattern(recursive, varName),
            UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword) => $"!({ConvertPattern(unary.Pattern, varName)})",
            BinaryPatternSyntax binary => $"({ConvertPattern(binary.Left, varName)} {(binary.OperatorToken.IsKind(SyntaxKind.OrKeyword) ? "||" : "&&")} {ConvertPattern(binary.Right, varName)})",
            DiscardPatternSyntax or VarPatternSyntax => "true",
            _ => "false"
        };
    }

    private string ConvertDeclarationPattern(DeclarationPatternSyntax declaration, string varName)
    {
        var type = declaration.Type.ToString();
        var name = declaration.Designation is SingleVariableDesignationSyntax variable ? variable.Identifier.Text : null;
        
        string typeCheck = type switch
        {
            "string" => $"typeof {varName} === 'string'",
            "int" or "double" or "float" or "long" or "decimal" or "number" => $"typeof {varName} === 'number'",
            "bool" or "boolean" => $"typeof {varName} === 'boolean'",
            _ => $"{varName} != null"
        };

        if (name != null && name != "_")
        {
            // IIFE to allow assignment and check
            return $"((() => {{ {name} = {varName}; return {typeCheck}; }})())";
        }
        return typeCheck;
    }

    private string ConvertRecursivePattern(RecursivePatternSyntax recursive, string varName)
    {
        var checks = new List<string>();
        if (recursive.Type != null) checks.Add($"{varName} != null");
        
        if (recursive.PositionalPatternClause != null)
        {
            for (int i = 0; i < recursive.PositionalPatternClause.Subpatterns.Count; i++)
            {
                checks.Add(ConvertPattern(recursive.PositionalPatternClause.Subpatterns[i].Pattern, $"{varName}[{i}]"));
            }
        }

        if (recursive.PropertyPatternClause != null)
        {
            foreach (var sub in recursive.PropertyPatternClause.Subpatterns)
            {
                var propName = sub.NameColon?.Name.ToString();
                if (propName != null)
                {
                    checks.Add(ConvertPattern(sub.Pattern, $"{varName}.{ToCamelCase(propName)}"));
                }
            }
        }

        return checks.Count > 0 ? string.Join(" && ", checks) : $"{varName} != null";
    }

    public string ConvertBlock(BlockSyntax block)
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
    
    public string ConvertStatement(StatementSyntax stmt)
    {
        var strategy = _statementRegistry.FindStrategy(stmt, _context);
        if (strategy != null)
        {
            return strategy.Convert(stmt, _context);
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

        // Priority: Semantic Check > String Check (Fallback)
        var symbol = _context.SemanticHelper.GetSymbol(identifier);
        
        // If it's a type symbol, return as is (to allow EnumStrategy to work)
        if (symbol is ITypeSymbol || symbol is INamedTypeSymbol) return name;

        if (_context.SemanticHelper.IsSystemConsole(symbol)) return "console";
        if (_semanticModel == null && name == "Console") return "console";
        
        // Resolve member access prefix (this.) using semantic model
        if (symbol != null)
        {
            if (symbol.Kind == SymbolKind.Field || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Method)
            {
                // If it's a member of the current class and not static, add 'this.'
                if (!symbol.IsStatic && symbol.ContainingType != null)
                {
                    // Check if it's a member of the currently compiling class or its bases
                    // Note: CurrentClassName might be null if we are not inside a class definition context
                    // but usually we set it in Emitter.
                    
                    // IMPROVEMENT: Check if the identifier is part of a member access already.
                    // If it's 'other.Property', identifier 'Property' shouldn't get 'this.'
                    if (identifier.Parent is MemberAccessExpressionSyntax ma && ma.Name == identifier)
                    {
                        return ToCamelCase(name);
                    }

                    return $"this.{ToCamelCase(name)}";
                }
            }
        }

        // Fallback Heuristics
        if (name.StartsWith("_"))
        {
            return $"this.{name}";
        }
        
        // If it starts with Uppercase and not obviously a local/param, it's likely a property
        if (char.IsUpper(name[0]))
        {
            // Simple check: is it in local scopes? (If we had scope tracking)
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
            var body = parenthesized.Block != null ? ConvertBlock(parenthesized.Block) : ConvertExpression(parenthesized.ExpressionBody!);
            return $"({parameters}) => {body}";
        }
        
        if (lambda is SimpleLambdaExpressionSyntax simple)
        {
            var param = simple.Parameter.Identifier.Text;
            var body = simple.Block != null ? ConvertBlock(simple.Block) : ConvertExpression(simple.ExpressionBody!);
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

        // If it's a declaration deconstruction, prefix with 'let ' if not already handled
        if (assignment.Left is DeclarationExpressionSyntax && !left.StartsWith("let "))
        {
            return $"let {left} {op} {right}";
        }

        return $"{left} {op} {right}";
    }

    private string ConvertDeclarationExpression(DeclarationExpressionSyntax decl)
    {
        if (decl.Designation is ParenthesizedVariableDesignationSyntax deconstruction)
        {
            var names = string.Join(", ", deconstruction.Variables
                .OfType<SingleVariableDesignationSyntax>()
                .Select(v => v.Identifier.Text));
            return $"[{names}]";
        }
        
        if (decl.Designation is SingleVariableDesignationSyntax single)
        {
            return single.Identifier.Text;
        }

        return decl.Designation.ToString();
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
                    // Escape backticks in template literal
                    sb.Append(text.TextToken.Text.Replace("`", "\\`"));
                    break;
                case InterpolationSyntax interpolation:
                    sb.Append("${");
                    var expr = ConvertExpression(interpolation.Expression);
                    
                    var format = interpolation.FormatClause?.FormatStringToken.ValueText;
                    var alignment = interpolation.AlignmentClause?.Value.ToString();
                    
                    if (format != null || alignment != null)
                    {
                        _context.UsedHelpers.Add("format");
                        var fmtArg = format != null ? $"'{format}'" : "null";
                        var alignArg = alignment != null ? $", {alignment}" : "";
                        sb.Append($"format({expr}, {fmtArg}{alignArg})");
                    }
                    else
                    {
                        sb.Append(expr);
                    }

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
            // Check if it's a Dictionary initializer: { {k, v}, {k, v} }
            if (initializer.Expressions.Count > 0 && initializer.Expressions.All(e => e is InitializerExpressionSyntax ie && ie.Expressions.Count == 2))
            {
                var pairs = initializer.Expressions.Cast<InitializerExpressionSyntax>()
                    .Select(ie => $"{ConvertExpression(ie.Expressions[0])}: {ConvertExpression(ie.Expressions[1])}");
                return $"{{ {string.Join(", ", pairs)} }}";
            }
            
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
                    if (propName == "Children")
                    {
                        if (assignment.Right is InitializerExpressionSyntax childInit)
                        {
                            value = ConvertInitializer(childInit);
                            // Handle empty {} with any whitespace as array for Children
                            var trimmedValue = value?.Trim();
                            if (string.IsNullOrEmpty(trimmedValue) || (trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}") && string.IsNullOrWhiteSpace(trimmedValue.Substring(1, trimmedValue.Length - 2))))
                                value = "[]";
                        }
                        else 
                        {
                             var trimmedValue = value?.Trim();
                             if (string.IsNullOrEmpty(trimmedValue) || (trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}") && string.IsNullOrWhiteSpace(trimmedValue.Substring(1, trimmedValue.Length - 2))))
                                value = "[]";
                        }
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
