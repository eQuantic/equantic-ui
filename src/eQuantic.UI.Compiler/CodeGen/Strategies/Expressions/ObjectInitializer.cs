using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// An OBJECT INITIALIZER as C# applies it: once the constructor has returned, element by element in
/// source order, to the object the constructor built (#413, #462).
/// <list type="bullet">
/// <item><c>Name = value</c> assigns the member, its value evaluated after every initializer the
/// constructor ran: <c>new Counted { B = Counted.N }</c> reads N after the initializers moved it.</item>
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
/// <para>
/// Every part of the C# is an ARGUMENT, evaluated in the caller's own function in the order C#
/// evaluates it: the construction, then each value, key and element. The function that applies them
/// reads only its parameters. Written inside it, an <c>await</c> in an element
/// (<c>Items = { await Next(), 4 }</c>) landed in a function that is not async, and the module did
/// not parse. What that costs is said where the values are: they are all evaluated before the first
/// of them is applied, where C# applies each before evaluating the next, which only a value that reads
/// the object's own member can tell (as the assignments' <c>Object.assign</c> does).
/// </para>
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
        // order, and assigned in it.
        if (OnlyAssigns(initializer))
        {
            var members = initializer.Expressions.Cast<AssignmentExpressionSyntax>().Select(assignment =>
                $"{Member(assignment.Left)}: {context.Converter.ConvertExpression(assignment.Right)}");
            return $"Object.assign({construction}, {{ {string.Join(", ", members)} }})";
        }

        var application = new Application(context);
        application.Collect(Target, context.SemanticHelper.GetType(initializer.Parent as ExpressionSyntax ?? initializer), initializer);
        return application.Written(construction);
    }

    /// <summary>The parameter the object the initializer applies to arrives in.</summary>
    private const string Target = "$o";

    /// <summary>A member's name on the twin.</summary>
    private static string Member(ExpressionSyntax name) => ((IdentifierNameSyntax)name).Identifier.ValueText.ToCamelCase();

    /// <summary>
    /// The statements an initializer applies, over parameters, and the converted C# that fills each
    /// parameter, in the order C# evaluates it.
    /// </summary>
    private sealed class Application(ConversionContext context)
    {
        private readonly List<string> _statements = [];
        private readonly List<string> _arguments = [];

        /// <summary>The parameter a converted part of the C# arrives in.</summary>
        private string Argument(ExpressionSyntax expression)
        {
            _arguments.Add(context.Converter.ConvertExpression(expression));
            return "$" + _arguments.Count;
        }

        /// <summary>The function that applies the statements, invoked in place with the construction and
        /// every argument.</summary>
        public string Written(string construction)
        {
            var annotation = context.TypeAnnotations ? ": any" : "";
            var parameters = string.Join(", ", new[] { Target }.Concat(_arguments.Select((_, i) => "$" + (i + 1)))
                .Select(parameter => parameter + annotation));
            var arguments = string.Concat(_arguments.Select(argument => ", " + argument));
            return $"(({parameters}) => {{ {string.Concat(_statements.Select(statement => statement + " "))}return {Target}; }})"
                + $"({construction}{arguments})";
        }

        /// <summary>The statements an initializer applies to <paramref name="target"/>, in its order.</summary>
        public void Collect(string target, ITypeSymbol? targetType, InitializerExpressionSyntax initializer)
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
                        if (Entry(target, targetType, key, entry.Right) is { } written)
                            _statements.Add(written);
                        else
                            Refuse(entry, "an entry of this type");
                        break;

                    // `Name = { … }`: the initializer applied to what the member holds.
                    case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name, Right: InitializerExpressionSyntax nested }:
                        Collect($"{target}.{Member(name)}", context.SemanticHelper.GetType(name), nested);
                        break;

                    case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } assignment:
                        _statements.Add($"{target}.{Member(name)} = {Argument(assignment.Right)};");
                        break;

                    // An element of a collection initializer: its Add, with one argument or with several.
                    default:
                        var arguments = element is InitializerExpressionSyntax { RawKind: (int)SyntaxKind.ComplexElementInitializerExpression } several
                            ? several.Expressions.ToList()
                            : [element];
                        var add = context.SemanticModel?.GetCollectionInitializerSymbolInfo(element).Symbol as IMethodSymbol;
                        if (Add(target, targetType, add, arguments) is { } call)
                            _statements.Add(call);
                        else
                            Refuse(element, $"'{add?.ContainingType.Name ?? targetType?.Name ?? "this"}.Add'");
                        break;
                }
            }
        }

        /// <summary>
        /// One element added to the collection a member holds, as its type adds one: a dictionary's pair
        /// through the lowering every call to its <c>Add</c> has, a set's value through its <c>add</c>, a
        /// list's through the array's <c>push</c>, and the <c>Add</c> of a type whose twin eqc writes, or
        /// of a vocabulary node, through the method itself. Null for any other, before any of its
        /// arguments is taken.
        /// </summary>
        private string? Add(string target, ITypeSymbol? targetType, IMethodSymbol? add, IReadOnlyList<ExpressionSyntax> arguments)
        {
            if (targetType.IsDictionary())
            {
                if (arguments.Count != 2) return null;
                var (key, value) = (Argument(arguments[0]), Argument(arguments[1]));
                return JsExprWriter.Write(Types.DictionaryStrategy.Add(JsExpr.Opaque(target), JsExpr.Identifier(key), JsExpr.Identifier(value))) + ";";
            }

            var owner = add?.ContainingType?.OriginalDefinition.ToDisplayString() ?? "";
            if (owner is "System.Collections.Generic.HashSet<T>" or "System.Collections.Generic.ISet<T>"
                or "System.Collections.Generic.SortedSet<T>")
                return arguments.Count == 1 ? $"{target}.add({Argument(arguments[0])});" : null;
            if (owner is "System.Collections.Generic.List<T>" or "System.Collections.Generic.IList<T>"
                or "System.Collections.Generic.ICollection<T>")
                return targetType.HasOpenCollectionShape() && !IsList(targetType)
                    ? null
                    : $"{target}.push({string.Join(", ", arguments.Select(Argument))});";

            if (add is { ContainingType: { } declaring } && TwinCarries(declaring))
                return $"{target}.{add.Name.ToCamelCase()}({string.Join(", ", arguments.Select(Argument))});";
            return null;
        }

        /// <summary>An entry written by its key: a dictionary's through <c>$eq.mapSet</c>, as every write
        /// to an entry goes, and an indexer the type declares through the twin's <c>setItem</c>.</summary>
        private string? Entry(string target, ITypeSymbol? targetType, ImplicitElementAccessSyntax key, ExpressionSyntax value)
        {
            if (value is InitializerExpressionSyntax) return null;
            var indexer = context.SemanticHelper.GetSymbol(key) as IPropertySymbol;
            var dictionary = targetType.IsDictionary() && key.ArgumentList.Arguments.Count == 1;
            var lowered = indexer is { IsIndexer: true } && Indexer.IsLowered(indexer);
            // An array's slot, or a list's, which is an array on this side.
            var slot = key.ArgumentList.Arguments.Count == 1 && (targetType is IArrayTypeSymbol || IsList(targetType));
            if (!dictionary && !lowered && !slot) return null;

            var keys = key.ArgumentList.Arguments.Select(argument => Argument(argument.Expression)).ToList();
            var written = Argument(value);
            if (dictionary)
            {
                context.UsedHelpers.Add(Eq.Import);
                return DictionaryEntry.Write(target, keys[0], written) + ";";
            }
            if (lowered) return $"{Indexer.Write(target, keys, written)};";
            return $"{target}[{keys[0]}] = {written};";
        }

        private void Refuse(SyntaxNode element, string what) =>
            context.Report(element, ConversionSeverity.Error, "EQ1004",
                $"An object initializer adds to {what} through a method the browser has no form for, so the element "
                + "would be lost. Add it after the construction, or initialize the member with a collection of its own.");
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
}
