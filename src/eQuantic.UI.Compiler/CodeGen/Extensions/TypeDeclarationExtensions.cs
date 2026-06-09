using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>One value member of a record/struct — its declared name, camelCased JS name, and the JS
/// literal for its <c>default(T)</c> (used for omitted constructor arguments).</summary>
public readonly record struct ValueMember(string Display, string Js, string Default);

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
                members.Add(new ValueMember(p.Identifier.Text, p.Identifier.Text.ToCamelCase(), DefaultFor(p.Type)));
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
                    members.Add(new ValueMember(prop.Identifier.Text, prop.Identifier.Text.ToCamelCase(), DefaultFor(prop.Type)));
                    break;

                // Public instance fields (common in plain structs).
                case FieldDeclarationSyntax field
                    when field.Modifiers.Any(SyntaxKind.PublicKeyword) && !field.Modifiers.Any(SyntaxKind.StaticKeyword):
                    foreach (var v in field.Declaration.Variables)
                        members.Add(new ValueMember(v.Identifier.Text, v.Identifier.Text.ToCamelCase(), DefaultFor(field.Declaration.Type)));
                    break;
            }
        }

        return members;
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
