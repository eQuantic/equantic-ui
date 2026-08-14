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

        // EVERY declarator: `float x0, y0, x1, y1;` is four variables, and emitting only the
        // first left `y0 is not defined` waiting at runtime — the mermaid layout was the first
        // shared code to write one and the first hydration to throw on it.
        var statements = new List<string>();
        foreach (var variable in decl.Declaration.Variables)
        {
            // A reserved JS word takes a trailing underscore — declaration and references go
            // through the same rule, so `var package = …` stays one identifier on both sides.
            var name = variable.Identifier.Text.ToJsIdentifier();
            var patternVars = PatternVariableScanner.Declarations(variable.Initializer?.Value, context.TypeAnnotations);
            var init = variable.Initializer != null
                ? context.Converter.ConvertExpression(variable.Initializer.Value)
                : "null";

            if (decl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
            {
                // For a 'using var', we should ideally wrap the remainder of the block.
                // Since this strategy only sees the statement, we'll emit a declaration
                // and a comment. The true 100% implementation requires block-aware conversion.
                statements.Add($"{patternVars}const {name} = {init}; /* using */");
                continue;
            }

            statements.Add($"{patternVars}let {name}{Annotation(decl, variable, context)} = {init};");
        }
        return string.Join("", statements);
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
        // A local holding a vocabulary ENUM. TypeScript widens `command ? 'dataEdge' : 'cell'` to
        // `string`, and `string` is wider than the slot this local is on its way to — the keymaps
        // were the first to hit it, choosing a motion and handing it to the controller.
        if (variable.Initializer is not null
            && context.SemanticHelper.GetType(variable.Initializer.Value) is { TypeKind: TypeKind.Enum } chosen
            && !chosen.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute")
            && CodeGen.TypeScriptEmitter.VocabularyUnionFor(chosen) is { } union)
        {
            // The name has to travel with the module, like every other runtime-provided one.
            context.UsedRuntimeTypes.Add(union);
            return $": {union}";
        }

        // An EMPTY collection is the one case `var` still needs an annotation: `new List<Token>()`
        // emits `[]`, and TypeScript infers `any[]` from it — every push into it and every read
        // out of it then goes unchecked, which is the opposite of what two type layers are for.
        if (variable.Initializer is not null
            && context.SemanticHelper.GetType(variable.Initializer.Value)
                is INamedTypeSymbol { IsGenericType: true } collection
            // List, HashSet and Dictionary ONLY: a Queue or a Stack lowers to a runtime helper,
            // not an array, and annotating one says it is something it is not.
            && variable.Initializer.Value is BaseObjectCreationExpressionSyntax { Initializer: null } creation
            && (creation.ArgumentList?.Arguments.Count ?? 0) <= 1)
        {
            if (collection.Name is "List" or "HashSet" && collection.TypeArguments.Length == 1)
            {
                var item = collection.TypeArguments[0];
                // A list OF LISTS of simple items still has a TS spelling — `number[][]` — and
                // without it `new List<List<int>>()` infers `never[]` and rejects every push
                // (the mermaid layout's per-rank orders were the first to hit it).
                if (item is INamedTypeSymbol { Name: "List", IsGenericType: true, TypeArguments.Length: 1 } inner
                    && collection.Name == "List"
                    && SimpleItemName(inner.TypeArguments[0]) is { } innerName)
                    return $": {innerName}[][]";
                // A GENERIC item (KeyValuePair<,>, a tuple) has no TS name to annotate with — its
                // bare C# name names nothing over there. Inference from the initializer is right.
                if (SimpleItemName(item) is not { } itemName) return "";
                return collection.Name == "HashSet" ? $": Set<{itemName}>" : $": {itemName}[]";
            }

            // A string-keyed Dictionary lowers to a plain object, and an unannotated `{}` refuses
            // string indexing under strict TS — `: Record<string, V>` is exactly what it is.
            if (collection.Name == "Dictionary" && collection.TypeArguments.Length == 2
                && collection.TypeArguments[0].SpecialType == SpecialType.System_String
                && SimpleItemName(collection.TypeArguments[1]) is { } valueName)
                return $": Record<string, {valueName}>";
            return "";
        }

        if (decl.Declaration.Type.IsVar || variable.Initializer is null) return "";

        return DeclaredTypeAnnotation(decl, variable, context);
    }

    /// <summary>The TS spelling of a SIMPLE item type, or null when TS has none to write.</summary>
    private static string? SimpleItemName(ITypeSymbol item)
    {
        if (item is INamedTypeSymbol { IsGenericType: true }) return null;
        return item.SpecialType switch
        {
            SpecialType.System_String or SpecialType.System_Char => "string",
            SpecialType.System_Boolean => "boolean",
            SpecialType.None => item.Name,
            _ => "number",
        };
    }

    private static string DeclaredTypeAnnotation(LocalDeclarationStatementSyntax decl,
        VariableDeclaratorSyntax variable, ConversionContext context)
    {

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
