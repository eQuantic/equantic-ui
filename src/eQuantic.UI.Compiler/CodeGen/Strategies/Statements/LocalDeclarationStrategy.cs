using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class LocalDeclarationStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LocalDeclarationStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var decl = (LocalDeclarationStatementSyntax)node;
        // Simplified: only taking the first variable (C# allows 'int x, y;')
        // JS often uses one line per var or 'let x, y;'
        // We will assume standard single declaration for now or iterate

        var variable = decl.Declaration.Variables.First();
        // A reserved JS word takes a trailing underscore — declaration and references go through
        // the same rule, so `var package = …` stays one identifier on both sides.
        var name = variable.Identifier.Text.ToJsIdentifier();
        var patternVars = PatternVariableScanner.Declarations(variable.Initializer?.Value);
        var init = variable.Initializer != null
            ? context.Converter.ConvertExpression(variable.Initializer.Value)
            : "null";

        if (decl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
        {
            // For a 'using var', we should ideally wrap the remainder of the block.
            // Since this strategy only sees the statement, we'll emit a declaration
            // and a comment. The true 100% implementation requires block-aware conversion.
            // For now, let's at least emit the declaration.
            return $"{patternVars}const {name} = {init}; /* using */";
        }

        return $"{patternVars}let {name}{Annotation(decl, variable, context)} = {init};";
    }

    /// <summary>
    /// The TS annotation, exactly when C# had one that MATTERS: an explicit declared type that is
    /// not what the initializer already is. `VisualNode menu = new Anchored(...)` declares a base
    /// on purpose — the variable is reassigned to a Shortcut two lines later — and an unannotated
    /// `let` makes TypeScript infer the derived type and reject the reassignment. `var` stays bare:
    /// inference was the author's own choice there.
    /// </summary>
    private static string Annotation(LocalDeclarationStatementSyntax decl, VariableDeclaratorSyntax variable,
        ConversionContext context)
    {
        // An EMPTY collection is the one case `var` still needs an annotation: `new List<Token>()`
        // emits `[]`, and TypeScript infers `any[]` from it — every push into it and every read
        // out of it then goes unchecked, which is the opposite of what two type layers are for.
        if (variable.Initializer is not null
            && context.SemanticHelper.GetType(variable.Initializer.Value)
                is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } collection
            // List and HashSet ONLY: a Queue or a Stack lowers to a runtime helper, not an array,
            // and annotating one  says it is something it is not.
            && collection.Name is "List" or "HashSet"
            && variable.Initializer.Value is BaseObjectCreationExpressionSyntax { Initializer: null } creation
            && (creation.ArgumentList?.Arguments.Count ?? 0) <= 1)
        {
            var item = collection.TypeArguments[0];
            var itemName = item.SpecialType switch
            {
                SpecialType.System_String or SpecialType.System_Char => "string",
                SpecialType.System_Boolean => "boolean",
                SpecialType.None => item.Name,
                _ => "number",
            };
            return collection.Name == "HashSet" ? $": Set<{itemName}>" : $": {itemName}[]";
        }

        if (decl.Declaration.Type.IsVar || variable.Initializer is null) return "";

        var declared = context.SemanticHelper.GetType(decl.Declaration.Type);
        var actual = context.SemanticHelper.GetType(variable.Initializer.Value);
        if (declared is null || actual is null) return "";
        if (SymbolEqualityComparer.Default.Equals(declared, actual)) return "";

        // Only NAMED vocabulary/user types annotate — primitives, generics and arrays keep their
        // C# spellings, which are not TypeScript's, and inference is already right for them.
        if (declared is not INamedTypeSymbol { IsGenericType: false, SpecialType: SpecialType.None } named)
            return "";
        // `VisualNode?` crosses as the union it is — an annotation that rejects the null the C#
        // explicitly allowed would refuse `VisualNode? icon = selected ? new Icon(…) : null`.
        var nullable = decl.Declaration.Type is NullableTypeSyntax ? " | null" : "";
        return $": {named.Name}{nullable}";
    }

    public int Priority => 0;
}
