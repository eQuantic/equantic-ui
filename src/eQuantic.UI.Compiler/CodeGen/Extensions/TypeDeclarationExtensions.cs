using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>One value member of a record/struct — its declared name, camelCased JS name, the JS
/// literal for its <c>default(T)</c> (used for omitted constructor arguments), and the TS type for its
/// type-only declaration.</summary>
public readonly record struct ValueMember(string Display, string Js, string Default, string TsType);

/// <summary>
/// Extracts the value members of a record/struct declaration — the data that participates in
/// construction, equality, <c>with</c> and <c>toString</c> — in a single canonical order shared by the
/// emitter and the construction site, so they never disagree. Order: positional (primary-constructor)
/// parameters, then body auto-properties, then public instance fields, each in source order.
/// </summary>
public static class TypeDeclarationExtensions
{
    /// <param name="model">The semantic model, when the caller has one. A default that is not a
    /// literal — <c>TextAlignment.Start</c>, a named constant — can only be lowered by ASKING what it
    /// is, and getting it wrong is silent: the C# side used Start while the emitted class defaulted to
    /// null, so the same column aligned one way on the server and the other on the client. Without a
    /// model the literal rules still apply and everything else stays <c>default(T)</c>.</param>
    public static IReadOnlyList<ValueMember> ValueMembers(this TypeDeclarationSyntax type,
        SemanticModel? model = null)
    {
        var members = new List<ValueMember>();

        // Positional (primary constructor) parameters.
        if (type.ParameterList != null)
        {
            foreach (var p in type.ParameterList.Parameters)
            {
                // An OPTIONAL parameter's own default wins over `default(T)` — `string Tag = ""`
                // must construct as `''`, not null, or every call site that omits it reads null
                // (and `Tag.Length` throws the moment the page hydrates).
                var fallback = p.Default is { } declared
                    ? DefaultLiteral(declared.Value, model)
                    : DefaultFor(p.Type);
                members.Add(new ValueMember(
                    p.Identifier.Text, p.Identifier.Text.ToCamelCase(), fallback, TsTypeFor(p.Type, model)));
            }
        }

        foreach (var member in type.Members)
        {
            switch (member)
            {
                // AUTO-properties only. A `get` with a BODY is computed — it is behaviour, not
                // state, and counting it as a member gave the class both a stored field and a
                // getter of the same name (a duplicate identifier, and the getter shadowed).
                case PropertyDeclarationSyntax prop
                    when !prop.Modifiers.Any(SyntaxKind.StaticKeyword)
                         && prop.ExpressionBody == null
                         && prop.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)
                             && a.Body == null && a.ExpressionBody == null) == true:
                    members.Add(new ValueMember(
                        prop.Identifier.Text,
                        prop.Identifier.Text.ToCamelCase(),
                        prop.Initializer is { } init ? DefaultLiteral(init.Value, model) : DefaultFor(prop.Type),
                        TsTypeFor(prop.Type, model)));
                    break;

                // Public instance fields (common in plain structs).
                case FieldDeclarationSyntax field
                    when field.Modifiers.Any(SyntaxKind.PublicKeyword) && !field.Modifiers.Any(SyntaxKind.StaticKeyword):
                    foreach (var v in field.Declaration.Variables)
                        members.Add(new ValueMember(v.Identifier.Text, v.Identifier.Text.ToCamelCase(), DefaultFor(field.Declaration.Type), TsTypeFor(field.Declaration.Type, model)));
                    break;
            }
        }

        return members;
    }

    /// <summary>
    /// The JS literal for a DECLARED default (`= ""`, `= 0`, `= true`, `= null`). Anything more
    /// interesting than a literal (a const reference, an expression) falls back to the type's
    /// `default(T)` — the emitter runs pre-symbol and must not guess.
    /// </summary>
    private static string DefaultLiteral(ExpressionSyntax expression, SemanticModel? model = null)
    {
        // An enum member is a camelCase STRING everywhere in the emitted world, and a named constant
        // is its value. Both need the model to be recognised; neither can be guessed from the syntax,
        // since `Space.S2` and `TextAlignment.Start` are written identically.
        if (expression is MemberAccessExpressionSyntax or IdentifierNameSyntax
            && model is not null
            && ReferenceEquals(expression.SyntaxTree, model.SyntaxTree))
        {
            var symbol = model.GetSymbolInfo(expression).Symbol;
            if (symbol is IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } member)
                return "'" + member.Name.ToCamelCase() + "'";
            if (symbol is IFieldSymbol { HasConstantValue: true, ConstantValue: { } constant })
                return constant switch
                {
                    string text => "'" + text.Replace("\\", "\\\\").Replace("'", "\\'") + "'",
                    bool flag => flag ? "true" : "false",
                    null => "null",
                    _ => System.Convert.ToString(constant, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
                };
        }

        return LiteralOf(expression);
    }

    private static string LiteralOf(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
            "'" + literal.Token.ValueText.Replace("\\", "\\\\").Replace("'", "\\'") + "'",
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression) => "true",
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.FalseLiteralExpression) => "false",
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression) =>
            literal.Token.ValueText,
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NullLiteralExpression) => "null",
        // A collection expression default (`= []`) is an empty array on both sides.
        CollectionExpressionSyntax { Elements.Count: 0 } => "[]",
        _ => "null",
    };

    /// <summary>
    /// TS type for a value member's TYPE-ONLY declaration, from the declared type syntax (name-based —
    /// this emitter runs pre-symbol). Only types whose TS counterpart is certain WITHOUT an import are
    /// named; everything else (records, enums, vocabulary types, and <c>decimal</c>/<c>long</c>, which
    /// lower to <c>$eq</c> objects rather than JS numbers) stays <c>any</c>, since a name the emitted
    /// module cannot resolve would just trade one error for another. A member that defaults to null is
    /// declared nullable, matching the constructor's own default.
    /// </summary>
    internal static string TsTypeFor(TypeSyntax? type, SemanticModel? model = null)
    {
        var raw = type?.ToString() ?? "";
        var name = raw.TrimEnd('?');

        // An ENUM crosses as its member STRING and a USER interface has no emitted twin to name.
        // The kind is only asked once the mapper has passed, though: `IReadOnlyList<T>` is an
        // interface too, and answering `any` for it threw away every element type in the model.
        var asked = type is NullableTypeSyntax wrapper ? wrapper.ElementType : type;
        if (((model?.GetSymbolInfo(asked!).Symbol as ITypeSymbol) ?? model?.GetTypeInfo(asked!).Type) is { } resolved
            && TypeScriptEmitter.CSharpTypeToTypeScript(name) == name)
        {
            // `Icons?` is a Nullable<Icons> STRUCT — asking it whether it is an enum answers no,
            // and the member came out annotated `Icons | null`, a type nothing declares.
            var core = resolved is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                ? nullable.TypeArguments[0]
                : resolved;
            if (core.TypeKind == TypeKind.Enum)
            {
                // [Flags] members COMBINE, so they cross as the number the bitwise operators need.
                var lowered = core.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute")
                    ? "number" : "string";
                return type is NullableTypeSyntax ? $"{lowered} | null" : lowered;
            }
            if (core.TypeKind == TypeKind.Interface) return "any";
        }

        // ONE mapper for the whole emission. This used to keep its own short list and answer `any`
        // to everything else, so a record member typed `IReadOnlyList<(char, char)>` arrived as
        // `any` and every lambda over it lost its parameter types with it.
        var ts = TypeScriptEmitter.CSharpTypeToTypeScript(name);
        // With no model to ask, a NAME could be an enum (a string at runtime), an interface (no
        // emitted twin), or a class — and annotating the wrong one is a type nothing declares.
        // Only what is unambiguous survives; the rest stays open, as it always did.
        if (model is null && !System.Text.RegularExpressions.Regex.IsMatch(
                ts, @"^(string|number|boolean|any|void|Date)(\[\])*$"))
            ts = "any";

        // Nullable iff the DECLARED TYPE says so. Keying off the default made every `string` member
        // nullable — a C# string's default IS null — so `string Header` was declared `string | null`
        // and every read of it looked unsafe to TypeScript. The declaration is type-only; the
        // constructor is what assigns, and the C# signature is the truth about what it assigns.
        return ts != "any" && raw.EndsWith("?") ? $"{ts} | null" : ts;
    }

    /// <summary>JS literal for <c>default(T)</c> from the declared type syntax (name-based — the emitter
    /// runs pre-symbol). Nullable and reference types default to <c>null</c>.</summary>
    internal static string DefaultFor(TypeSyntax? type)
    {
        var name = type?.ToString() ?? "";
        if (name.EndsWith("?")) return "null"; // Nullable<T> / nullable reference

        return name switch
        {
            "int" or "Int32" or "short" or "Int16" or "byte" or "sbyte"
                or "uint" or "UInt32" or "ushort" or "UInt16" => "0",
            "double" or "Double" or "float" or "Single" => "0",
            "bool" or "Boolean" => "false",
            "decimal" or "Decimal" => "$eq.num.dec(0)",
            "long" or "Int64" or "ulong" or "UInt64" => "$eq.num.long(0)",
            _ => "null",
        };
    }
}
