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

        return $"new {typeName}({arguments})";
    }

    /// <summary>The converted argument bound to the exception constructor's <c>message</c> parameter
    /// (semantic when resolvable, else the LAST argument of a multi-arg call — every BCL exception
    /// with a paramName overload puts the message beside it); null = keep whatever was converted.</summary>
    private static string? ExceptionMessageArgument(ObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var args = creation.ArgumentList?.Arguments;
        if (args is not { Count: > 1 }) return null;

        if (context.SemanticModel?.GetSymbolInfo(creation).Symbol is IMethodSymbol ctor)
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
    /// camelCase member-name string, matching the enum representation everywhere else.</summary>
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

        // No declaration available (external/metadata type — e.g. the shared vocabulary in
        // eQuantic.UI.Primitives): member order is unknowable, so an object initializer cannot be
        // mapped onto positional parameters. Emit it as a trailing CONFIG OBJECT instead — the same
        // shape UI-component classes already receive (`new Row(gap, { height: … })`), which the
        // runtime-provided classes accept. Positional args pass through unchanged.
        if (declSyntax == null)
        {
            var parts = new List<string>();
            if (creation.ArgumentList != null)
                parts.AddRange(OrderedArguments(creation, context));
            if (creation.Initializer != null)
                parts.Add(context.Converter.ConvertExpression(creation.Initializer));
            return $"new {type.Name}({string.Join(", ", parts)})";
        }

        var members = declSyntax.ValueMembers();
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
