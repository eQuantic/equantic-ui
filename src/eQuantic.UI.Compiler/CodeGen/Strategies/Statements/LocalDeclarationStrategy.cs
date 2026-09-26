using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis.CSharp;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class LocalDeclarationStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LocalDeclarationStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var decl = (LocalDeclarationStatementSyntax)node;

        // EVERY declarator: `float x0, y0, x1, y1;` is four variables, and emitting only the
        // first left `y0 is not defined` waiting at runtime — the mermaid layout was the first
        // shared code to write one and the first hydration to throw on it.
        var statements = new List<JsStatement>();
        foreach (var variable in decl.Declaration.Variables)
        {
            // A reserved JS word takes a trailing underscore — declaration and references go
            // through the same rule, so `var package = …` stays one identifier on both sides.
            var name = variable.Identifier.Text.ToJsIdentifier();
            var patternVars = PatternVariableScanner.Declarations(variable.Initializer?.Value, context.TypeAnnotations);
            var init = variable.Initializer != null
                ? context.Converter.ConvertIr(variable.Initializer.Value)
                : JsExpr.Literal("null");

            if (decl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
            {
                // A `using var` is a const; the BLOCK it sits in wraps what follows in the
                // try/finally that disposes it (CSharpToJsConverter.ConvertBlockIr).
                // Since this strategy only sees the statement, we'll emit a declaration
                // and a comment. The true 100% implementation requires block-aware conversion.
                statements.Add($"{patternVars}const {name} = {JsExprWriter.Write(init)};");
                continue;
            }

            statements.Add(JsStatement.Hoisted(patternVars,
                JsStatement.Let(name, Annotation(decl, variable, context), init)));
        }
        return JsStatement.Sequence(statements);
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
        // Plain JavaScript carries no annotation, and every branch below writes one. The design host
        // compiles with none and inlines the modules as one script, with nothing to strip a `: T`
        // from it: `string? label = null` was written `let label: string | null = null`, and the
        // preview did not load.
        if (!context.TypeAnnotations) return "";

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

        if (!decl.Declaration.Type.IsVar && StartsNull(decl, variable, context)
            && StartingNullAnnotation(decl, context) is { } startsNull)
            return startsNull;

        if (decl.Declaration.Type.IsVar || variable.Initializer is null) return "";

        return DeclaredTypeAnnotation(decl, variable.Initializer.Value, context);
    }

    /// <summary>
    /// Whether the local STARTS as null: initialized with <c>null</c>, or with <c>default</c> of a
    /// type that holds a null, or declared nullable with no initializer (which this strategy writes
    /// as <c>= null</c>). A local with no initializer and a type that holds no null is definitely
    /// assigned before C# lets anything read it, and is not one.
    /// </summary>
    private static bool StartsNull(LocalDeclarationStatementSyntax decl, VariableDeclaratorSyntax variable,
        ConversionContext context)
    {
        if (variable.Initializer is null) return decl.Declaration.Type is NullableTypeSyntax;
        var value = variable.Initializer.Value;
        if (value.IsKind(SyntaxKind.NullLiteralExpression)) return true;
        if (!value.IsKind(SyntaxKind.DefaultLiteralExpression) && value is not DefaultExpressionSyntax) return false;
        return context.SemanticHelper.GetType(decl.Declaration.Type) is { } type
            && (type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
    }

    /// <summary>
    /// The annotation of a local that starts as null (see <see cref="StartsNull"/>): its declared type,
    /// with the null it holds. TypeScript has nothing to infer from a null: it types <c>let path =
    /// null</c> as it goes, and a closure that reads it sees <c>any</c>, which the runtime's own build
    /// refuses; the patch reader resets its paths from a local function and was the first to hit it.
    /// Null, and no annotation, for a type TypeScript cannot name here: a type parameter, and what
    /// maps to <c>any</c>. An enum crosses as the union its members lower to, the vocabulary's
    /// named one when it has one.
    /// </summary>
    private static string? StartingNullAnnotation(LocalDeclarationStatementSyntax decl, ConversionContext context)
    {
        var declared = context.SemanticHelper.GetType(decl.Declaration.Type);
        if (declared is null) return null;
        var underlying = declared is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapper
            ? wrapper.TypeArguments[0]
            : declared;
        if (underlying is ITypeParameterSymbol) return null;

        string ts;
        if (underlying is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            if (enumType.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute")) ts = "number";
            else if (CodeGen.TypeScriptEmitter.VocabularyUnionFor(enumType) is { } union)
            {
                context.UsedRuntimeTypes.Add(union);
                ts = union;
            }
            else ts = "string";
        }
        else ts = CodeGen.TypeScriptEmitter.CSharpTypeToTypeScript(decl.Declaration.Type.ToString());

        if (ts is "any" or "void") return null;
        if (!ts.EndsWith(" | null", StringComparison.Ordinal)) ts = CodeGen.TypeScriptEmitter.OrNull(ts);
        // A decimal is the runtime's class, a name the C# never spells: the module imports it only
        // when something says so.
        if (System.Text.RegularExpressions.Regex.IsMatch(ts, @"(?<![\w$])Decimal(?![\w$])"))
            context.UsedRuntimeTypes.Add("Decimal");
        return $": {ts}";
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

    /// <summary>
    /// Takes the initializer's VALUE, not the declarator: `int x;` is legal C# and carries no
    /// initializer, and the caller already refuses that case. Passing the declarator made the
    /// guarantee live in the caller while the dereference lived here, which reads as a null
    /// dereference to anyone — the compiler included — who looks at this method alone.
    /// </summary>
    private static string DeclaredTypeAnnotation(LocalDeclarationStatementSyntax decl,
        ExpressionSyntax initializer, ConversionContext context)
    {
        var declared = context.SemanticHelper.GetType(decl.Declaration.Type);
        var actual = context.SemanticHelper.GetType(initializer);
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
