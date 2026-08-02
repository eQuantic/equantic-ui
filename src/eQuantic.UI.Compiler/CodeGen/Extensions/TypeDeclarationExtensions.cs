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
    public static IReadOnlyList<ValueMember> ValueMembers(this TypeDeclarationSyntax type)
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
                    ? DefaultLiteral(declared.Value)
                    : DefaultFor(p.Type);
                members.Add(new ValueMember(
                    p.Identifier.Text, p.Identifier.Text.ToCamelCase(), fallback, TsTypeFor(p.Type)));
            }
        }

        foreach (var member in type.Members)
        {
            switch (member)
            {
                // Body auto-properties (an instance get accessor — not static, not expression-bodied computed).
                case PropertyDeclarationSyntax prop
                    when !prop.Modifiers.Any(SyntaxKind.StaticKeyword)
                         && prop.ExpressionBody == null
                         && prop.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) == true:
                    members.Add(new ValueMember(
                        prop.Identifier.Text,
                        prop.Identifier.Text.ToCamelCase(),
                        prop.Initializer is { } init ? DefaultLiteral(init.Value) : DefaultFor(prop.Type),
                        TsTypeFor(prop.Type)));
                    break;

                // Public instance fields (common in plain structs).
                case FieldDeclarationSyntax field
                    when field.Modifiers.Any(SyntaxKind.PublicKeyword) && !field.Modifiers.Any(SyntaxKind.StaticKeyword):
                    foreach (var v in field.Declaration.Variables)
                        members.Add(new ValueMember(v.Identifier.Text, v.Identifier.Text.ToCamelCase(), DefaultFor(field.Declaration.Type), TsTypeFor(field.Declaration.Type)));
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
    private static string DefaultLiteral(ExpressionSyntax expression) => expression switch
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
    private static string TsTypeFor(TypeSyntax? type)
    {
        var raw = type?.ToString() ?? "";
        var name = raw.TrimEnd('?');

        var ts = name switch
        {
            "string" or "String" or "Guid" => "string",
            "int" or "Int32" or "short" or "Int16" or "byte" or "sbyte"
                or "uint" or "UInt32" or "ushort" or "UInt16"
                or "double" or "Double" or "float" or "Single" => "number",
            "bool" or "Boolean" => "boolean",
            _ => "any",
        };

        // Nullable iff the DECLARED TYPE says so. Keying off the default made every `string` member
        // nullable — a C# string's default IS null — so `string Header` was declared `string | null`
        // and every read of it looked unsafe to TypeScript. The declaration is type-only; the
        // constructor is what assigns, and the C# signature is the truth about what it assigns.
        return ts != "any" && raw.EndsWith("?") ? $"{ts} | null" : ts;
    }

    /// <summary>JS literal for <c>default(T)</c> from the declared type syntax (name-based — the emitter
    /// runs pre-symbol). Nullable and reference types default to <c>null</c>.</summary>
    private static string DefaultFor(TypeSyntax? type)
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
