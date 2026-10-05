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

    /// <summary>The construction, with the initializer applied to what it builds. IR in and out, so a
    /// lambda among the values keeps its own lines in the source map (#566).</summary>
    public static JsExpr Apply(JsExpr construction, InitializerExpressionSyntax initializer, ConversionContext context)
    {
        // Assignments alone: the literal's values are evaluated after the construction, in their
        // order, and assigned in it.
        if (OnlyAssigns(initializer))
        {
            var members = initializer.Expressions.Cast<AssignmentExpressionSyntax>()
                .Select(assignment => new JsProperty(Member(assignment.Left), context.Converter.ConvertIr(assignment.Right)))
                .ToList();
            return JsExpr.Call(JsExpr.Identifier("Object.assign"), construction, JsExpr.Object(members));
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
        private readonly List<JsStatement> _statements = [];
        private readonly List<JsExpr> _arguments = [];

        /// <summary>The parameter a converted part of the C# arrives in.</summary>
        private string Argument(ExpressionSyntax expression) => Argument(context.Converter.ConvertIr(expression));

        /// <summary>The parameter a part already converted arrives in.</summary>
        private string Argument(JsExpr converted)
        {
            _arguments.Add(converted);
            return "$" + _arguments.Count;
        }

        /// <summary>A statement over the function's parameters, which hold no C# of their own.</summary>
        private void Statement(string text) => _statements.Add(JsStatement.Raw(text));

        /// <summary>The function that applies the statements, invoked in place with the construction and
        /// every argument. An IR arrow: the C# it applies is in its arguments, never in its body.</summary>
        public JsExpr Written(JsExpr construction)
        {
            var annotation = context.TypeAnnotations ? ": any" : "";
            var parameters = string.Join(", ", new[] { Target }.Concat(_arguments.Select((_, i) => "$" + (i + 1)))
                .Select(parameter => parameter + annotation));
            var body = JsStatement.Block([.. _statements, JsStatement.Return(JsExpr.Identifier(Target))]);
            return JsExpr.Call(JsExpr.ArrowBlock(parameters, body, context.Layout, context.Depth), [construction, .. _arguments]);
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
                            Statement(written);
                        else
                            Refuse(entry, "an entry of this type");
                        break;

                    // `Name = { … }`: the initializer applied to what the member holds.
                    case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name, Right: InitializerExpressionSyntax nested }:
                        Collect($"{target}.{Member(name)}", context.SemanticHelper.GetType(name), nested);
                        break;

                    case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } assignment:
                        Statement($"{target}.{Member(name)} = {Argument(assignment.Right)};");
                        break;

                    // An element of a collection initializer: its Add, with one argument or with several.
                    default:
                        var arguments = element is InitializerExpressionSyntax { RawKind: (int)SyntaxKind.ComplexElementInitializerExpression } several
                            ? several.Expressions.ToList()
                            : [element];
                        var add = context.SemanticHelper.CollectionInitializerMethod(element);
                        if (Add(target, targetType, add, arguments, element) is { } call)
                            Statement(call);
                        else
                            Refuse(element, $"'{add?.ContainingType.Name ?? targetType?.Name ?? "this"}.Add'");
                        break;
                }
            }
        }

        /// <summary>
        /// One element added to the collection a member holds, as every call to the <c>Add</c> the bound
        /// tree binds it to lowers: an EXTENSION's through its home's static, with the collection first
        /// (<see cref="InvocationStrategy.Extension"/>), a dictionary's pair through the lowering every
        /// call to its <c>Add</c> has, a set's value through its <c>add</c>, a list's or a collection
        /// interface's through the array's <c>push</c>, as <c>Items.Add(1)</c> lowers
        /// (<see cref="Primitives.ListMethodStrategy"/>), and the <c>Add</c> of a type whose twin eqc
        /// writes, or of a vocabulary node, through the method itself. Each collection's own lowering
        /// applies only to its own <c>Add</c>: an extension <c>Add(this List&lt;string&gt;, int)</c> was
        /// written as the list's <c>add</c>, which a list does not have, and a set's or a dictionary's
        /// own ran in its place. Where no model can be asked, the member's type decides. Null for any
        /// other, before any of its arguments is taken.
        /// </summary>
        private string? Add(string target, ITypeSymbol? targetType, IMethodSymbol? add, IReadOnlyList<ExpressionSyntax> arguments,
            SyntaxNode element)
        {
            var receiver = JsExpr.Identifier(target);
            // The bound tree names a classic extension in its static form: the call's lowering takes
            // the reduced one, on the collection's type.
            if (add is { IsExtensionMethod: true } || add.ExtensionBlockHome() is not null)
            {
                var extension = add is { IsExtensionMethod: true, ReducedFrom: null }
                    ? targetType is null ? null : add.ReduceExtensionMethod(targetType)
                    : add;
                if (extension is null) return null;
                List<JsExpr> passed = [.. arguments.Select(argument => JsExpr.Identifier(Argument(argument)))];
                return JsExprWriter.Write(InvocationStrategy.Extension(extension, extension.Name, receiver, passed, element, context)!) + ";";
            }

            var declaring = add?.ContainingType;
            if (add is null ? targetType.IsDictionary() : declaring.IsDictionary())
            {
                if (arguments.Count != 2) return null;
                var (key, value) = (Argument(arguments[0]), Argument(arguments[1]));
                return JsExprWriter.Write(Types.DictionaryStrategy.Add(receiver, JsExpr.Identifier(key), JsExpr.Identifier(value))) + ";";
            }

            var owner = declaring?.OriginalDefinition.ToDisplayString() ?? "";
            if (owner is "System.Collections.Generic.HashSet<T>" or "System.Collections.Generic.ISet<T>"
                or "System.Collections.Generic.SortedSet<T>")
                return arguments.Count == 1 ? $"{target}.add({Argument(arguments[0])});" : null;
            if (Primitives.ListMethodStrategy.Lowers(add))
                return JsExprWriter.Write(Primitives.ListMethodStrategy.Add(receiver,
                    [.. arguments.Select(argument => JsExpr.Identifier(Argument(argument)))])) + ";";

            if (declaring is not null && TwinCarries(declaring))
                return $"{target}.{add!.Name.ToCamelCase()}({string.Join(", ", arguments.Select(Argument))});";
            return null;
        }

        /// <summary>An entry written by its key: a Place over the object, a dictionary's through
        /// <c>$eq.mapSet</c>, as every write to an entry goes, and an indexer the type declares through
        /// the twin's <c>setItem</c>, its keys and its value arguments in the order C# evaluates them;
        /// or an array's slot, or a list's, which is an array on this side.</summary>
        private string? Entry(string target, ITypeSymbol? targetType, ImplicitElementAccessSyntax key, ExpressionSyntax value)
        {
            if (value is InitializerExpressionSyntax) return null;
            var receiver = JsExpr.Identifier(target);
            if (Place.Of(key, receiver, context) is { } place)
            {
                List<JsExpr> evaluated = [receiver, .. place.Evaluated.Skip(1).Select(part => JsExpr.Identifier(Argument(part)))];
                var entered = JsExpr.Identifier(Argument(value));
                return JsExprWriter.Write(place.Over(evaluated).Write(entered)) + ";";
            }
            var slot = key.ArgumentList.Arguments.Count == 1 && (targetType is IArrayTypeSymbol || IsList(targetType));
            if (!slot) return null;
            var index = Argument(key.ArgumentList.Arguments[0].Expression);
            return $"{target}[{index}] = {Argument(value)};";
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
