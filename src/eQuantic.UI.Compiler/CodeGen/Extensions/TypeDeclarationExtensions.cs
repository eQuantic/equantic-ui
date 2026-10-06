using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// One member of a record's or a struct's own STATE — what its constructor writes, its <c>equals</c>
/// compares and its <c>with</c> copies: the declared name, the camelCased JS name, the TS type for its
/// type-only declaration, and the declaration that says what it starts as (a positional
/// <see cref="ParameterSyntax"/>, a <see cref="PropertyDeclarationSyntax"/> or a field's
/// <see cref="VariableDeclaratorSyntax"/>). It is not a constructor parameter: the twin's constructor
/// takes the C# constructor's parameters, and sets every member as C# does
/// (<see cref="RecordTypeEmitter"/>, #413). What a record's text prints is the symbol's to say.
/// </summary>
public readonly record struct ValueMember(string Display, string Js, string TsType, SyntaxNode Declaration);

/// <summary>
/// Extracts the state of a record/struct declaration — what its constructor writes, its equality
/// compares and its hash combines — in the order C# declares it: the positional parameters the record
/// turns into properties of its own (a class's or a struct's those a member reads), then the instance
/// fields, private ones included, and the auto-properties of the body, each in source order.
/// </summary>
public static class TypeDeclarationExtensions
{
    /// <param name="type">The declaration whose state to read.</param>
    /// <param name="model">The semantic model, when the caller has one, which the member TYPES are
    /// asked of (an enum crosses as its member string, an interface as nothing to name), and which says
    /// whether a positional parameter of a derived record is a property of its own or its base's.</param>
    public static IReadOnlyList<ValueMember> ValueMembers(this TypeDeclarationSyntax type,
        SemanticModel? model = null)
    {
        var members = new List<ValueMember>();
        var isRecord = type is RecordDeclarationSyntax;

        // A member the body declares under a positional parameter's name REPLACES the property the
        // record would have made of it (#546): `record Box(int X, int Y) { public int X { get; set; }
        // = X; }` has one X, the declared one, which the parameter initializes. Listed twice, the
        // twin's constructor bound `x` twice and the module did not load.
        var declaredInBody = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in type.Members)
        {
            if (member is PropertyDeclarationSyntax property) declaredInBody.Add(property.Identifier.ValueText);
            if (member is FieldDeclarationSyntax field)
                foreach (var variable in field.Declaration.Variables) declaredInBody.Add(variable.Identifier.ValueText);
        }

        // Positional (primary constructor) parameters: the properties a record makes of them, and the
        // state a C# 12 class or struct captures them in, which its members read as `this.<name>`, for
        // a parameter a member reads (HoldsParameter): one read only by initializers is the
        // constructor's, held by nothing.
        if (type.ParameterList != null)
        {
            var self = model?.GetDeclaredSymbol(type) as INamedTypeSymbol;
            foreach (var p in type.ParameterList.Parameters)
            {
                var name = p.Identifier.ValueText;
                if (declaredInBody.Contains(name)) continue;
                if (isRecord && InheritedProperty(type, self, name)) continue;
                if (!type.HoldsParameter(p, model)) continue;
                members.Add(new ValueMember(name, name.ToCamelCase(), TsTypeFor(p.Type, model), p));
            }
        }

        foreach (var member in type.Members)
        {
            switch (member)
            {
                // AUTO-properties only. A `get` with a BODY is computed — it is behaviour, not
                // state, and counting it as a member gave the class both a stored field and a
                // getter of the same name (a duplicate identifier, and the getter shadowed). An
                // ABSTRACT one holds nothing either: its accessors have no body because a derived
                // type gives them one, and as state the base's constructor wrote `this.name = null`
                // over the derived getter, which threw (`new Circle(2)` over `abstract record Shape {
                // public abstract string Name { get; } }`).
                case PropertyDeclarationSyntax prop
                    when !prop.Modifiers.Any(SyntaxKind.StaticKeyword)
                         && !prop.Modifiers.Any(SyntaxKind.AbstractKeyword)
                         && prop.ExpressionBody == null
                         && prop.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)
                             && a.Body == null && a.ExpressionBody == null) == true:
                    members.Add(new ValueMember(prop.Identifier.ValueText, prop.Identifier.ValueText.ToCamelCase(),
                        TsTypeFor(prop.Type, model), prop));
                    break;

                // Instance fields, whatever their accessibility: C# compares a record's private field
                // as it compares a public one, and a struct's too, and only PRINTS the public ones.
                // A field-like EVENT is one too: its delegate is a field the compiler declares, which
                // equality, the hash and a copy read as they read any other, so a record subscribed to
                // and one that is not are not equal (found by Copilot's review of #608).
                //
                // A `const` is NOT one of them, and it does not carry the `static` keyword to say so
                // — C# makes it static implicitly. Reading the syntax alone let `public const string
                // Marker` through as value state: the twin took an extra positional parameter C#
                // does not have, compared it in equals, offered it to `with`, and printed
                // `Marker = undefined` where .NET printed nothing.
                case BaseFieldDeclarationSyntax field
                    when !field.Modifiers.Any(SyntaxKind.StaticKeyword)
                         && !field.Modifiers.Any(SyntaxKind.ConstKeyword):
                    foreach (var v in field.Declaration.Variables)
                        members.Add(new ValueMember(v.Identifier.ValueText, v.Identifier.ValueText.ToCamelCase(),
                            TsTypeFor(field.Declaration.Type, model), v));
                    break;
            }
        }

        return members;
    }

    /// <summary>
    /// Whether a primary constructor's parameter is state each instance holds: always for a record,
    /// whose parameters are its properties, and for a class or a struct only where a member reads it
    /// outside an initializer, which is what C# captures. A parameter read only by initializers, the
    /// C# 12 idiom (<c>struct Point(int x) { public int X { get; } = x; }</c>), is the constructor's,
    /// which the twin's constructor reads as its own; held as well, it shared the slot of the property
    /// of its name.
    /// </summary>
    internal static bool HoldsParameter(this TypeDeclarationSyntax type, ParameterSyntax parameter, SemanticModel? model)
    {
        if (type is RecordDeclarationSyntax) return true;
        var name = parameter.Identifier.ValueText;
        var symbol = model is not null && model.SyntaxTree == parameter.SyntaxTree ? model.GetDeclaredSymbol(parameter) : null;
        foreach (var member in type.Members)
        {
            // A field's initializer runs in the constructor, and so does a property's (skipped below).
            if (member is BaseFieldDeclarationSyntax) continue;
            foreach (var identifier in member.DescendantNodes(node => node is not EqualsValueClauseSyntax).OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.ValueText != name) continue;
                if (symbol is null) return true;
                if (SymbolEqualityComparer.Default.Equals(model!.GetSymbolInfo(identifier).Symbol, symbol)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether a derived record's positional parameter names a property its BASE already has, so the
    /// record makes none of its own (<c>record Dog(string Name) : Animal(Name)</c>: the name is
    /// Animal's). Asked of the model, which knows what the record itself declares; without one, a
    /// parameter handed to the base by name is taken as the base's.
    /// </summary>
    private static bool InheritedProperty(TypeDeclarationSyntax type, INamedTypeSymbol? self, string name)
    {
        if (type.BaseList?.Types.FirstOrDefault() is not { } first) return false;
        if (self is not null)
            return !self.GetMembers(name).Any(member => member is IPropertySymbol or IFieldSymbol);
        return first is PrimaryConstructorBaseTypeSyntax { ArgumentList: { } passed }
            && passed.Arguments.Any(argument => argument.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == name);
    }

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
                // Everything else crosses as its member string — NAMED, when the enum belongs to
                // the vocabulary, so a record member can be passed where that union is expected
                // (see TypeScriptEmitter.EnumUnion for the whole story).
                var lowered = core.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute")
                    ? "number"
                    : TypeScriptEmitter.VocabularyUnionFor(core) ?? "string";
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
        return ts != "any" && raw.EndsWith("?") ? TypeScriptEmitter.OrNull(ts) : ts;
    }

    /// <summary>JS literal for <c>default(T)</c> from the declared type syntax (name-based — the emitter
    /// runs pre-symbol). Nullable and reference types default to <c>null</c>. A name qualified with
    /// <c>System.</c> is the same type as its keyword, and a char's default is U+0000.</summary>
    internal static string DefaultFor(TypeSyntax? type)
    {
        var name = type?.ToString() ?? "";
        if (name.EndsWith("?")) return "null"; // Nullable<T> / nullable reference
        if (name.StartsWith("System.", System.StringComparison.Ordinal)) name = name["System.".Length..];

        return name switch
        {
            "int" or "Int32" or "short" or "Int16" or "byte" or "Byte" or "sbyte" or "SByte"
                or "uint" or "UInt32" or "ushort" or "UInt16" => "0",
            "double" or "Double" or "float" or "Single" => "0",
            "bool" or "Boolean" => "false",
            "char" or "Char" => JsStringLiteral.Quote("\0"),
            "decimal" or "Decimal" => "$eq.num.dec(0)",
            "long" or "Int64" or "ulong" or "UInt64" => "$eq.num.long(0)",
            _ => "null",
        };
    }
}
