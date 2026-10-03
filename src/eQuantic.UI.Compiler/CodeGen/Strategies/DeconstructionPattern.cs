using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What a C# deconstruction destructures, ONE lowering for its three shapes: the declaration
/// (<c>var (a, b) = p</c>), the assignment (<c>(a, b) = p</c>, <c>(var a, b) = p</c>) and the loop
/// (<c>foreach (var (a, b) in ps)</c>). A tuple, and a dictionary's pair, are arrays here and
/// destructure by position. Anything else deconstructs through the <c>Deconstruct</c> the bound
/// tree says each level calls. A record's own, and a BCL type's, read the members its out
/// parameters name (a record's positional properties). One the app wrote is CALLED, at the top
/// level, and its outs come back as the object every method with outs returns
/// (<see cref="OutParameters"/>), so a <c>Deconstruct</c> that computes a part, or names it
/// differently from a member, still answers as it does in .NET. A nested deconstruction nests the
/// two kinds, each part by its own type.
/// <para>
/// Only the declaration had it, from the type's first <c>Deconstruct</c> whatever its arity: the
/// assignment and the loop wrote array destructuring, and a record is not iterable, so
/// <c>(a, b) = point</c> and <c>foreach (var (a, b) in points)</c> threw <c>{} is not iterable</c>,
/// and a nested declaration left its inner names undeclared (#486).
/// </para>
/// </summary>
internal static class DeconstructionPattern
{
    /// <summary>
    /// The destructuring pattern of <paramref name="left"/> (an assignment's tuple, a declaration's
    /// designation, a loop's variable) from a value of <paramref name="type"/>, and the
    /// <c>Deconstruct</c> its value goes through first when the app wrote it. Null when no level of
    /// it deconstructs through a <c>Deconstruct</c>, tuples all the way down, which the array
    /// destructuring every caller already writes is right for.
    /// </summary>
    internal static (string Pattern, IMethodSymbol? Called)? Of(SyntaxNode left, DeconstructionInfo? info,
        ITypeSymbol? type, ConversionContext context)
    {
        if (Parts(left) is not { } parts) return null;
        var deconstructs = false;
        var top = Positional(type) ? null : Deconstruct(info, type);
        var called = top is not null && IsTheApps(top) ? top : null;
        var pattern = Composite(parts, info, type, called is not null, context, ref deconstructs);
        return deconstructs ? (pattern, called) : null;
    }

    /// <summary>The value a deconstruction reads, through the <c>Deconstruct</c> the app wrote: an
    /// instance method on the value, or an extension's static with the value first.</summary>
    internal static JsExpr Through(IMethodSymbol called, JsExpr value, ConversionContext context)
    {
        var name = called.Name.ToCamelCase();
        if ((called.ReducedFrom ?? called) is { IsExtensionMethod: true } extension)
        {
            extension.ContainingType.RegisterIntroduced(context);
            return JsExpr.Call(JsExpr.Member(JsExpr.Identifier(extension.ContainingType.Name), name), value);
        }
        if (called.ExtensionBlockHome() is { } home)
        {
            home.RegisterIntroduced(context);
            return JsExpr.Call(JsExpr.Member(JsExpr.Identifier(home.Name), name), value);
        }
        return JsExpr.Call(JsExpr.Member(value, name));
    }

    /// <summary>The parts a target splits into, or null for a target that is one part.</summary>
    private static IReadOnlyList<SyntaxNode>? Parts(SyntaxNode target) => target switch
    {
        TupleExpressionSyntax tuple => tuple.Arguments.Select(argument => (SyntaxNode)argument.Expression).ToList(),
        DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax designation } =>
            designation.Variables.Cast<SyntaxNode>().ToList(),
        ParenthesizedVariableDesignationSyntax designation => designation.Variables.Cast<SyntaxNode>().ToList(),
        _ => null,
    };

    /// <summary>
    /// One level: its parts by position, for a tuple, a pair and a value with no
    /// <c>Deconstruct</c>; otherwise by the names its <c>Deconstruct</c>'s outs carry, which a
    /// called one hands back under their own names and a read one finds on the members, in
    /// camelCase. A discard takes nothing: a hole by position, no entry by name.
    /// </summary>
    private static string Composite(IReadOnlyList<SyntaxNode> parts, DeconstructionInfo? info, ITypeSymbol? type,
        bool called, ConversionContext context, ref bool deconstructs)
    {
        var deconstruct = Positional(type) ? null : Deconstruct(info, type);
        if (deconstruct is null)
        {
            var elements = new List<string>();
            for (var i = 0; i < parts.Count; i++)
                elements.Add(Part(parts[i], Nested(info, i), ElementType(type, i), context, ref deconstructs) ?? "");
            return $"[{string.Join(", ", elements)}]";
        }

        deconstructs = true;
        var outs = deconstruct.Parameters.Where(parameter => parameter.RefKind == RefKind.Out).ToList();
        var members = new List<string>();
        for (var i = 0; i < parts.Count && i < outs.Count; i++)
        {
            if (Part(parts[i], Nested(info, i), outs[i].Type, context, ref deconstructs) is not { } bound) continue;
            var key = called ? outs[i].Name.ToJsIdentifier() : outs[i].Name.ToCamelCase();
            members.Add($"{key}: {bound}");
        }
        return members.Count == 0 ? "{}" : $"{{ {string.Join(", ", members)} }}";
    }

    /// <summary>
    /// One part: the name a declaration binds, the target an assignment writes (converted as any
    /// target is), a level of its own when it splits again, and null for a discard. Below the top
    /// level a <c>Deconstruct</c> is read and never called, since a pattern cannot call one.
    /// </summary>
    private static string? Part(SyntaxNode target, DeconstructionInfo? info, ITypeSymbol? type,
        ConversionContext context, ref bool deconstructs)
    {
        if (target is DeclarationExpressionSyntax declaration) target = declaration.Designation;
        if (target is DiscardDesignationSyntax) return null;
        if (target is SingleVariableDesignationSyntax single) return single.Identifier.Text.ToJsIdentifier();
        if (target is IdentifierNameSyntax { Identifier.ValueText: "_" } discard
            && context.SemanticHelper.GetSymbol(discard) is null or IDiscardSymbol)
            return null;
        if (Parts(target) is { } nested) return Composite(nested, info, type, called: false, context, ref deconstructs);
        return target is ExpressionSyntax assigned ? context.Converter.ConvertExpression(assigned) : null;
    }

    /// <summary>
    /// The <c>Deconstruct</c> a level calls: the bound tree's, which is none for a tuple's level.
    /// Where the model cannot be asked, the type's own, the first whose parameters are all outs.
    /// </summary>
    private static IMethodSymbol? Deconstruct(DeconstructionInfo? info, ITypeSymbol? type) =>
        info is { } bound
            ? bound.Method
            : type?.GetMembers("Deconstruct").OfType<IMethodSymbol>()
                .FirstOrDefault(method => method.Parameters.Length > 0
                    && method.Parameters.All(parameter => parameter.RefKind == RefKind.Out));

    /// <summary>A tuple, and a dictionary's pair, whose runtime value is iterable as its parts.</summary>
    private static bool Positional(ITypeSymbol? type) =>
        type is INamedTypeSymbol { IsTupleType: true }
        || type is INamedTypeSymbol { MetadataName: "KeyValuePair`2" } pair
            && pair.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

    /// <summary>A <c>Deconstruct</c> the app wrote, which its twin carries as a method. A record's
    /// own is the compiler's and has none, nor has a BCL type's.</summary>
    private static bool IsTheApps(IMethodSymbol deconstruct) =>
        !deconstruct.IsImplicitlyDeclared && deconstruct.Locations.Any(location => location.IsInSource);

    private static DeconstructionInfo? Nested(DeconstructionInfo? info, int index) =>
        info is { } bound && index < bound.Nested.Length ? bound.Nested[index] : null;

    private static ITypeSymbol? ElementType(ITypeSymbol? type, int index) => type switch
    {
        INamedTypeSymbol { IsTupleType: true } tuple when index < tuple.TupleElements.Length =>
            tuple.TupleElements[index].Type,
        INamedTypeSymbol { MetadataName: "KeyValuePair`2" } pair when index < pair.TypeArguments.Length =>
            pair.TypeArguments[index],
        _ => null,
    };
}
