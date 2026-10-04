using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// An OBJECT INITIALIZER as C# applies it: once the constructor has returned, element by element in
/// source order, to the object the constructor built (#413, #462).
/// <list type="bullet">
/// <item><c>Name = value</c> assigns the member, its value evaluated then, after every initializer
/// the constructor ran: <c>new Counted { B = Counted.N }</c> reads N after the initializers moved it.</item>
/// <item><c>Name = { a, b }</c>, a nested COLLECTION initializer, adds to the collection the member
/// already holds, through its <c>Add</c>: it was written as a new collection handed to the member,
/// so whatever the member was initialized with was gone.</item>
/// <item><c>Name = { X = 1 }</c>, a nested OBJECT initializer, assigns into the object the member
/// already holds.</item>
/// <item><c>[key] = value</c> writes the entry, through the indexer the type declares.</item>
/// </list>
/// A record's or a struct's twin takes only its constructor's parameters, so every initializer it is
/// built with comes through here. A class keeps its trailing config object for an initializer that
/// only assigns, which its constructor applies last, where C# applies it; one that adds or writes an
/// entry comes through here, as nothing in a config object can say that.
/// </summary>
internal static class ObjectInitializer
{
    /// <summary>Whether an initializer only assigns members, each a value of its own: what
    /// <c>Object.assign</c> and a config object can carry.</summary>
    public static bool OnlyAssigns(InitializerExpressionSyntax initializer) =>
        initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
        && initializer.Expressions.All(expression => expression is AssignmentExpressionSyntax
        {
            Left: IdentifierNameSyntax,
            Right: not InitializerExpressionSyntax,
        });

    /// <summary>The construction, with the initializer applied to what it builds.</summary>
    public static string Apply(string construction, InitializerExpressionSyntax initializer, ConversionContext context)
    {
        // Assignments alone: the literal's values are evaluated after the construction, in their
        // order, and assigned in it, which is the initializer's own semantics.
        if (OnlyAssigns(initializer))
        {
            var members = initializer.Expressions.Cast<AssignmentExpressionSyntax>().Select(assignment =>
                $"{Member(assignment.Left)}: {context.Converter.ConvertExpression(assignment.Right)}");
            return $"Object.assign({construction}, {{ {string.Join(", ", members)} }})";
        }

        var statements = new List<string>();
        Collect("$o", context.SemanticHelper.GetType(initializer.Parent as ExpressionSyntax ?? initializer),
            initializer, statements, context);
        var parameter = context.TypeAnnotations ? "($o: any)" : "$o";
        return $"({parameter} => {{ {string.Concat(statements.Select(statement => statement + " "))}return $o; }})({construction})";
    }

    /// <summary>The statements an initializer applies to <paramref name="target"/>, in its order.</summary>
    private static void Collect(string target, ITypeSymbol? targetType, InitializerExpressionSyntax initializer,
        List<string> statements, ConversionContext context)
    {
        foreach (var element in initializer.Expressions)
        {
            switch (element)
            {
                // `[key] = value`: the entry, written as the type's indexer writes it. C# 13's
                // from-the-end key (`[^1] = v`) writes the tail of what the member holds, which is
                // fenced here as it is in a config object.
                case AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax key } entry
                    when key.ArgumentList.Arguments.Any(argument =>
                        argument.Expression is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.IndexExpression }):
                    context.Report(entry, ConversionSeverity.Error, "EQ2008",
                        "from-the-end indexer assignments inside initializers (`X = { [^i] = v }`) are not lowered yet "
                        + "— assign after construction.");
                    break;
                case AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax key } entry:
                    if (Entry(target, targetType, key, entry.Right, context) is { } written)
                        statements.Add(written);
                    else
                        Refuse(entry, "an entry of this type", context);
                    break;

                // `Name = { … }`: the initializer applied to what the member holds.
                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name, Right: InitializerExpressionSyntax nested }:
                    Collect($"{target}.{Member(name)}", context.SemanticHelper.GetType(name), nested, statements, context);
                    break;

                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } assignment:
                    statements.Add($"{target}.{Member(name)} = {context.Converter.ConvertExpression(assignment.Right)};");
                    break;

                // An element of a collection initializer: its Add, with one argument or with several.
                default:
                    var arguments = element is InitializerExpressionSyntax { RawKind: (int)SyntaxKind.ComplexElementInitializerExpression } several
                        ? several.Expressions.ToList()
                        : [element];
                    var add = context.SemanticModel?.GetCollectionInitializerSymbolInfo(element).Symbol as IMethodSymbol;
                    if (Add(target, targetType, add, arguments, context) is { } call)
                        statements.Add(call);
                    else
                        Refuse(element, $"'{add?.ContainingType.Name ?? targetType?.Name ?? "this"}.Add'", context);
                    break;
            }
        }
    }

    /// <summary>A member's name on the twin.</summary>
    private static string Member(ExpressionSyntax name) => ((IdentifierNameSyntax)name).Identifier.ValueText.ToCamelCase();

    /// <summary>
    /// One element added to the collection a member holds, as its type adds one: a dictionary's pair
    /// through its <c>set</c> (the lowering its <c>Add</c> has everywhere), a set's value through its
    /// <c>add</c>, a list's through the array's <c>push</c>, and the <c>Add</c> of a type whose twin
    /// eqc writes, or of a vocabulary node, through the method itself. Null for any other.
    /// </summary>
    private static string? Add(string target, ITypeSymbol? targetType, IMethodSymbol? add,
        IReadOnlyList<ExpressionSyntax> arguments, ConversionContext context)
    {
        var values = arguments.Select(argument => context.Converter.ConvertExpression(argument)).ToList();
        if (targetType.IsDictionary())
            return values.Count == 2 ? $"{target}.set({values[0]}, {values[1]});" : null;

        var owner = add?.ContainingType?.OriginalDefinition.ToDisplayString() ?? "";
        if (owner is "System.Collections.Generic.HashSet<T>" or "System.Collections.Generic.ISet<T>"
            or "System.Collections.Generic.SortedSet<T>")
            return values.Count == 1 ? $"{target}.add({values[0]});" : null;
        if (owner is "System.Collections.Generic.List<T>" or "System.Collections.Generic.IList<T>"
            or "System.Collections.Generic.ICollection<T>")
            return targetType.HasOpenCollectionShape() && !IsList(targetType)
                ? null
                : $"{target}.push({string.Join(", ", values)});";

        if (add is { ContainingType: { } declaring } && TwinCarries(declaring))
            return $"{target}.{add.Name.ToCamelCase()}({string.Join(", ", values)});";
        return null;
    }

    /// <summary>Whether a type is a list, whose twin is an array, rather than an interface a set satisfies too.</summary>
    private static bool IsList(ITypeSymbol? type) =>
        type?.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.List<T>" or "System.Collections.Generic.IList<T>";

    /// <summary>Whether the twin of <paramref name="type"/> carries the type's own methods: one eqc
    /// writes (declared in the source, or in a namespace it transpiles whole), or a vocabulary node's.</summary>
    private static bool TwinCarries(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        return type.Locations.Any(location => location.IsInSource)
            || Services.RuntimeProvidedTypeScanner.IsTranspiledNamespace(ns)
            || Services.RuntimeProvidedTypeScanner.IsVocabularyNamespace(ns);
    }

    /// <summary>An entry written by its key: a dictionary's through <c>$eq.mapSet</c>, as every write
    /// to an entry goes, and an indexer the type declares through the twin's <c>setItem</c>.</summary>
    private static string? Entry(string target, ITypeSymbol? targetType, ImplicitElementAccessSyntax key,
        ExpressionSyntax value, ConversionContext context)
    {
        if (value is InitializerExpressionSyntax) return null;
        var keys = key.ArgumentList.Arguments.Select(argument => context.Converter.ConvertExpression(argument.Expression)).ToList();
        var written = context.Converter.ConvertExpression(value);
        if (targetType.IsDictionary() && keys.Count == 1)
        {
            context.UsedHelpers.Add(Eq.Import);
            return DictionaryEntry.Write(target, keys[0], written) + ";";
        }
        if (context.SemanticHelper.GetSymbol(key) is IPropertySymbol { IsIndexer: true } indexer && Indexer.IsLowered(indexer))
            return $"{Indexer.Write(target, keys, written)};";
        // An array's slot, or a list's, which is an array on this side.
        if (keys.Count == 1 && (targetType is IArrayTypeSymbol || IsList(targetType)))
            return $"{target}[{keys[0]}] = {written};";
        return null;
    }

    private static void Refuse(SyntaxNode element, string what, ConversionContext context) =>
        context.Report(element, ConversionSeverity.Error, "EQ1004",
            $"An object initializer adds to {what} through a method the browser has no form for, so the element "
            + "would be lost. Add it after the construction, or initialize the member with a collection of its own.");
}
