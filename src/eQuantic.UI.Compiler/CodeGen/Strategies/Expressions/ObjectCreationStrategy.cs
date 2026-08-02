using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for object creation (new T() or new()).
/// Handles:
/// - List<T> -> []
/// - Dictionary<K,V> -> {}
/// - HtmlNode -> {} (UI config)
/// - UI Components -> new Component(config) or just config
/// </summary>
public class ObjectCreationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ObjectCreationExpressionSyntax || node is ImplicitObjectCreationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is ObjectCreationExpressionSyntax objCreation)
        {
            return ConvertExplicit(objCreation, context);
        }
        else if (node is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            return ConvertImplicit(implicitCreation, context);
        }
        throw new InvalidOperationException("Invalid node type");
    }

    private string ConvertExplicit(ObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var typeName = creation.Type.ToString();
        // TYPE ARGUMENTS are erased at runtime, and `new Bucket<string>()` is not even valid JS —
        // `<` parses as a comparison. The generic COLLECTIONS below still match on their full text,
        // so the erasure happens after they have had their say.
        var genericTypeName = creation.Type is GenericNameSyntax generic
            ? generic.Identifier.Text
            : null;
        // A qualified creation (`new eQuantic.UI.Primitives.Image(...)` — the disambiguation idiom
        // when a section class shares a name) must construct the IMPORTED simple name: module
        // bindings exist in JS, namespace chains do not. The List/Dictionary specials below keep
        // matching (they see the simple generic name).
        if (creation.Type is QualifiedNameSyntax qualifiedName)
            typeName = qualifiedName.Right.ToString();
        else if (creation.Type is AliasQualifiedNameSyntax aliasQualified)
            typeName = aliasQualified.Name.ToString();
        var createdType = context.SemanticHelper.GetType(creation);

        // `new T()` where T is a generic type parameter cannot be transpiled: JS erases generic type
        // arguments, so the concrete constructor is unknown at runtime (emitting `new T()` would throw
        // "T is not defined"). Fail the build with guidance instead of shipping broken code.
        if (createdType is ITypeParameterSymbol)
        {
            context.Report(creation, ConversionSeverity.Error, "EQ2003",
                $"Cannot instantiate type parameter '{typeName}' with `new {typeName}()` — generic type " +
                "arguments are erased at runtime in JavaScript, so the concrete type is unknown. Pass a " +
                "factory (e.g. Func<T>) or the constructed value as a parameter instead.");
            return "undefined";
        }

        // Records and user structs are emitted as named JS classes (they carry instance methods) —
        // construct via `new`, mapping positional args and any object initializer onto the constructor.
        if (createdType is { IsRecord: true }
            || (createdType is { TypeKind: TypeKind.Struct } && createdType.IsStructuralValueType()))
        {
            return BuildValueTypeConstruction(creation, createdType, context);
        }

        var arguments = "";

        if (creation.ArgumentList != null && creation.ArgumentList.Arguments.Count > 0)
        {
             arguments = string.Join(", ", OrderedArguments(creation, context));
        }

        var initializer = "";
        if (creation.Initializer != null)
        {
            initializer = context.Converter.ConvertExpression(creation.Initializer);
            // The config object lands in the constructor's TRAILING config slot — when the call
            // site supplied fewer positional arguments than the resolved constructor's arity, the
            // skipped parameters fill from their C# defaults first (`new Stack { Width = … }` must
            // emit `new Stack('topStart', {…})`, never the config in the align slot).
            if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol ctor)
            {
                var supplied = creation.ArgumentList?.Arguments.Count ?? 0;
                if (supplied < ctor.Parameters.Length)
                {
                    var defaults = ctor.Parameters.Skip(supplied).Select(ParameterDefaultLiteral);
                    var filler = string.Join(", ", defaults);
                    arguments = string.IsNullOrEmpty(arguments) ? filler : arguments + ", " + filler;
                }
            }
            // Append initializer to arguments if likely a UI component
            if (string.IsNullOrEmpty(arguments))
            {
                arguments = initializer;
            }
            else
            {
                arguments += ", " + initializer;
            }
        }

        // Special handling for Collections (handle both short and fully-qualified names)
        if (typeName.StartsWith("List<") || typeName.Contains(".List<")
            || typeName.StartsWith("IEnumerable<") || typeName.Contains(".IEnumerable<"))
        {
            return string.IsNullOrEmpty(arguments) || arguments == "{}" ? "[]" : arguments;
        }
        if (typeName.StartsWith("Dictionary<") || typeName.Contains(".Dictionary<"))
        {
            return string.IsNullOrEmpty(arguments) || arguments == "[]" ? "{}" : arguments;
        }
        
        // HtmlNode -> Plain Object
        if (typeName == "HtmlNode")
        {
            return string.IsNullOrEmpty(arguments) ? "{}" : arguments;
        }
        
        // RenderContext -> Mock or Plain Object (since it's a TS interface)
        if (typeName == "RenderContext")
        {
            return "{ getService: () => null }";
        }

        // Exception types -> JavaScript Error. Error takes ONE message argument — pick the C#
        // constructor's `message` PARAMETER (signatures differ: ArgumentException(message, param)
        // vs ArgumentOutOfRangeException(param, message)); emitting all arguments positionally
        // would silently make the param NAME the thrown message.
        if (typeName.EndsWith("Exception") || typeName == "Exception")
        {
            return $"new Error({ExceptionMessageArgument(creation, context) ?? arguments})";
        }

        return $"new {genericTypeName ?? typeName}({arguments})";
    }

    /// <summary>The converted argument bound to the exception constructor's <c>message</c> parameter
    /// (semantic when resolvable, else the LAST argument of a multi-arg call — every BCL exception
    /// with a paramName overload puts the message beside it); null = keep whatever was converted.</summary>
    private static string? ExceptionMessageArgument(ObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var args = creation.ArgumentList?.Arguments;
        if (args is not { Count: > 1 }) return null;

        if (context.SemanticHelper.Knows(creation)
            && context.SemanticModel?.GetSymbolInfo(creation).Symbol is IMethodSymbol ctor)
        {
            for (var i = 0; i < args.Value.Count && i < ctor.Parameters.Length; i++)
            {
                var parameter = args.Value[i].NameColon?.Name.Identifier.ValueText is { } named
                    ? ctor.Parameters.FirstOrDefault(p => p.Name == named)
                    : ctor.Parameters[i];
                if (parameter?.Name == "message")
                    return context.Converter.ConvertExpression(args.Value[i].Expression);
            }
        }

        return context.Converter.ConvertExpression(args.Value[^1].Expression);
    }

    /// <summary>
    /// Converts a creation's arguments in the CONSTRUCTOR's parameter order. JS has no named arguments,
    /// so `new Button("x", onPressed: f)` must emit `f` at the parameter's real position with the
    /// skipped parameters filled from their C# defaults (`'primary'`, `'medium'`) — emitting call-site
    /// order would silently bind values to the wrong parameters. Without a resolvable constructor
    /// symbol or named arguments, syntactic order passes through untouched.
    /// </summary>
    private static IReadOnlyList<string> OrderedArguments(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var args = creation.ArgumentList!.Arguments;
        var converted = args.Select(a => context.Converter.ConvertExpression(a.Expression)).ToList();
        if (!args.Any(a => a.NameColon != null)) return converted;

        if (context.SemanticHelper.GetSymbol(creation) is not IMethodSymbol ctor)
            return converted;

        var slots = new string?[ctor.Parameters.Length];
        var positional = 0;
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i].NameColon?.Name.Identifier.Text;
            var ordinal = name == null
                ? positional++
                : ctor.Parameters.FirstOrDefault(p => p.Name == name)?.Ordinal ?? -1;
            if (ordinal >= 0 && ordinal < slots.Length) slots[ordinal] = converted[i];
        }

        var lastSet = Array.FindLastIndex(slots, s => s != null);
        var ordered = new List<string>();
        for (var i = 0; i <= lastSet; i++)
            ordered.Add(slots[i] ?? ParameterDefaultLiteral(ctor.Parameters[i]));
        return ordered;
    }

    /// <summary>The TS literal for a parameter's C# default value — enum members lower to their
    /// camelCase member-name string, matching the enum representation everywhere else. Shared with
    /// InvocationStrategy (named INVOCATION arguments reorder the same way creations do).</summary>
    internal static string DefaultLiteralFor(IParameterSymbol parameter) => ParameterDefaultLiteral(parameter);

    private static string ParameterDefaultLiteral(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue || parameter.ExplicitDefaultValue is null) return "null";
        var value = parameter.ExplicitDefaultValue;

        var enumType = parameter.Type.TypeKind == TypeKind.Enum ? parameter.Type
            : parameter.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                ? nullable.TypeArguments[0]
                : null;
        if (enumType is { TypeKind: TypeKind.Enum })
        {
            var member = enumType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, value));
            if (member != null) return $"'{member.Name.ToCamelCase()}'";
        }

        return value switch
        {
            bool flag => flag ? "true" : "false",
            string text => $"'{text}'",
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
        };
    }

    /// <summary>
    /// Value members of a record/struct we have no syntax for, in the SAME order
    /// <see cref="TypeDeclarationExtensions.ValueMembers"/> produces (and therefore the same order
    /// RecordTypeEmitter emitted the constructor in): primary-constructor parameters, then settable
    /// instance properties, then public instance fields. Empty when the type has no usable symbol.
    /// </summary>
    private static IReadOnlyList<ValueMember> SymbolValueMembers(INamedTypeSymbol? type)
    {
        if (type == null) return new List<ValueMember>();

        // The abstract VOCABULARY (BoxStyle, EdgeInsets, …) is runtime-provided by a HAND-WRITTEN TS twin
        // that takes a trailing config object — only types whose twin the compiler EMITS (RecordTypeEmitter,
        // e.g. the shared component library's NavItem) have the positional constructor this mapping needs.
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns == "eQuantic.UI.Primitives" || ns.StartsWith("eQuantic.UI.Primitives."))
            return new List<ValueMember>();

        // The primary constructor: the widest one that is not the record's synthesized copy constructor.
        var primary = type.InstanceConstructors
            .Where(c => c.Parameters.Length > 0
                        && !(c.Parameters.Length == 1
                             && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, type)))
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        var members = new List<ValueMember>();
        var seen = new HashSet<string>();

        if (primary != null)
            foreach (var p in primary.Parameters)
                if (seen.Add(p.Name))
                    members.Add(new ValueMember(p.Name, p.Name.ToCamelCase(), SymbolDefault(p.Type), "any"));

        foreach (var member in type.GetMembers())
        {
            // `EqualityContract` is the record's synthesized type discriminator, never a value member.
            if (member is IPropertySymbol { IsStatic: false, IsImplicitlyDeclared: false } prop
                && prop.DeclaredAccessibility == Accessibility.Public
                && prop.SetMethod != null
                && prop.Name != "EqualityContract"
                && seen.Add(prop.Name))
            {
                members.Add(new ValueMember(prop.Name, prop.Name.ToCamelCase(), SymbolDefault(prop.Type), "any"));
            }
            else if (member is IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field
                     && field.DeclaredAccessibility == Accessibility.Public
                     && seen.Add(field.Name))
            {
                members.Add(new ValueMember(field.Name, field.Name.ToCamelCase(), SymbolDefault(field.Type), "any"));
            }
        }

        return members;
    }

    /// <summary>JS literal for <c>default(T)</c> from a type SYMBOL — mirrors the syntax-side
    /// <c>DefaultFor</c> so both paths fill unset slots identically.</summary>
    private static string SymbolDefault(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated) return "null";
        if (type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T) return "null";

        return type.SpecialType switch
        {
            SpecialType.System_Int32 or SpecialType.System_Int16 or SpecialType.System_Byte
                or SpecialType.System_SByte or SpecialType.System_UInt32 or SpecialType.System_UInt16
                or SpecialType.System_Double or SpecialType.System_Single => "0",
            SpecialType.System_Boolean => "false",
            SpecialType.System_Decimal => "$eq.num.dec(0)",
            SpecialType.System_Int64 or SpecialType.System_UInt64 => "$eq.num.long(0)",
            _ => "null",
        };
    }

    /// <summary>
    /// Builds a <c>new T(...)</c> construction for a record/struct, mapping positional arguments and any
    /// object initializer (<c>{ Name = … }</c>) onto the constructor's positional value members (in the
    /// type's declaration order). Members left unset before the last supplied one get their default
    /// literal; trailing unset members are omitted (the constructor's parameter defaults cover them).
    /// </summary>
    private static string BuildValueTypeConstruction(BaseObjectCreationExpressionSyntax creation, ITypeSymbol type, ConversionContext context)
    {
        var declSyntax = type.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        // No declaration syntax (external/metadata type — a record from a referenced assembly, e.g. the
        // shared component library's NavItem): the SYMBOL still carries the member order, so recover it
        // there and map positionally exactly as the syntax path does. A record's emitted constructor is
        // positional-only, so a trailing config object would silently land in the next positional slot
        // (`new NavItem(icon, label, { badgeCount: 3 })` sets selectedIcon and leaves badgeCount 0 —
        // SSR renders the badge, the hydrated client does not).
        var members = declSyntax != null
            ? declSyntax.ValueMembers()
            : SymbolValueMembers(type as INamedTypeSymbol);

        // Neither syntax nor a usable symbol: fall back to a trailing CONFIG OBJECT — the shape
        // UI-component classes accept (`new Row(gap, { height: … })`). Positional args pass through.
        if (members.Count == 0)
        {
            var parts = new List<string>();
            if (creation.ArgumentList != null)
                parts.AddRange(OrderedArguments(creation, context));
            if (creation.Initializer != null)
            {
                // The config object rides the TRAILING slot: a call that supplied fewer positional
                // arguments than the constructor's arity must first fill the skipped parameters
                // from their C# defaults — otherwise `new TransitionSpec(channels, 300) { Easing = … }`
                // emits the config in the `delayMs` slot and the easing silently reverts to default.
                if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol ctor)
                {
                    var supplied = creation.ArgumentList?.Arguments.Count ?? 0;
                    for (var i = supplied; i < ctor.Parameters.Length; i++)
                        parts.Add(ParameterDefaultLiteral(ctor.Parameters[i]));
                }
                parts.Add(context.Converter.ConvertExpression(creation.Initializer));
            }
            return $"new {type.Name}({string.Join(", ", parts)})";
        }

        var values = new string?[members.Count];

        // Positional constructor arguments fill the leading members.
        if (creation.ArgumentList != null)
        {
            var args = creation.ArgumentList.Arguments;
            for (var i = 0; i < args.Count && i < members.Count; i++)
                values[i] = context.Converter.ConvertExpression(args[i].Expression);
        }

        // Object initializer `{ Name = …, Age = … }` fills the named members by position.
        if (creation.Initializer != null)
        {
            foreach (var expr in creation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    var key = assignment.Left.ToString().ToCamelCase();
                    var idx = -1;
                    for (var i = 0; i < members.Count; i++) if (members[i].Js == key) { idx = i; break; }
                    if (idx >= 0) values[idx] = context.Converter.ConvertExpression(assignment.Right);
                }
            }
        }

        var lastSet = -1;
        for (var i = 0; i < values.Length; i++) if (values[i] != null) lastSet = i;

        var ctorArgs = new List<string>();
        for (var i = 0; i <= lastSet; i++) ctorArgs.Add(values[i] ?? members[i].Default);

        return $"new {type.Name}({string.Join(", ", ctorArgs)})";
    }

    private string ConvertImplicit(ImplicitObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var ms = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;
        var typeDisplay = ms?.ContainingType.ToDisplayString() ?? context.ExpectedType ?? "";

        if (creation.Initializer != null)
        {
            var target = ms?.ContainingType;

            // Records and user structs keep full value semantics: map args + initializer onto the
            // positional constructor exactly like the explicit `new T(...) { … }` path.
            if (target is { IsRecord: true }
                || (target is { TypeKind: TypeKind.Struct } && target.IsStructuralValueType()))
            {
                return BuildValueTypeConstruction(creation, target, context);
            }

            // A named class target (`Badge b = new(0, 99, variant) { Dot = true }`) must CONSTRUCT —
            // ordered args, skipped parameters filled from their C# defaults, initializer as the
            // trailing config object (the runtime component classes' contract). Delegating to the
            // initializer here would silently return a bare object instead of an instance.
            // `new() { a, b }` on a HashSet target seeds a JS Set (the HashSetStrategy contract).
            if (typeDisplay.Contains("HashSet<"))
                return $"new Set({context.Converter.ConvertExpression(creation.Initializer)})";

            if (target is { SpecialType: SpecialType.None, TypeKind: TypeKind.Class }
                && !typeDisplay.Contains("List<") && !typeDisplay.Contains("Dictionary<")
                && !typeDisplay.Contains("IEnumerable<") && !typeDisplay.Contains("Collection<"))
            {
                var ctorArgs = creation.ArgumentList is { Arguments.Count: > 0 }
                    ? OrderedArguments(creation, context).ToList()
                    : new List<string>();
                if (ms != null && ctorArgs.Count < ms.Parameters.Length)
                    ctorArgs.AddRange(ms.Parameters.Skip(ctorArgs.Count).Select(ParameterDefaultLiteral));
                ctorArgs.Add(context.Converter.ConvertExpression(creation.Initializer));
                return $"new {target.Name}({string.Join(", ", ctorArgs)})";
            }

            // `new() { … }` / `new() { [k]=v }` on collections/dictionaries (or with no resolvable
            // named target) → the initializer IS the value (array / object / dictionary literal).
            return context.Converter.ConvertExpression(creation.Initializer);
        }

        // Collection / dictionary target with no initializer → empty literal.
        if (typeDisplay.Contains("List<") || typeDisplay.Contains("IEnumerable<") ||
            typeDisplay.Contains("Collection<") || typeDisplay.TrimEnd('?').EndsWith("[]"))
        {
            return "[]";
        }
        if (typeDisplay.Contains("Dictionary<") || typeDisplay.Contains("IDictionary<"))
        {
            return "{}";
        }
        // Bare `new()` on a HashSet target — the runtime representation is a JS Set.
        if (typeDisplay.Contains("HashSet<"))
        {
            return "new Set()";
        }

        // Target-typed `new(args)` on a named type (record/class): `Item _x = new(9, "z")` → `new Item(9, 'z')`.
        var args = string.Join(", ",
            creation.ArgumentList?.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression))
            ?? System.Linq.Enumerable.Empty<string>());
        var typeName = ms?.ContainingType.Name;
        if (string.IsNullOrEmpty(typeName) || typeName == "Object")
        {
            // No semantic info: fall back to the field/var's declared type (without nullability/generics noise).
            var et = (context.ExpectedType ?? "").Trim().TrimEnd('?');
            if (et.Contains("<")) et = et.Substring(0, et.IndexOf('<'));
            typeName = string.IsNullOrEmpty(et) ? null : et;
        }
        if (!string.IsNullOrEmpty(typeName))
        {
            return $"new {typeName}({args})";
        }

        return "{}";
    }

    public int Priority => 5;
}
