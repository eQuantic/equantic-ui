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
/// Every part of the C# is evaluated in the function it is written in, where C# evaluates it: the
/// object lives in a temporary that function declares, so an <c>await</c> in an element
/// (<c>Items = { await Next(), 4 }</c>) is that function's own (<see cref="Apply"/>).
/// </para>
/// </summary>
internal static class ObjectInitializer
{
    /// <summary>Whether an initializer only assigns members, each a value of its own: what a config
    /// object can carry.</summary>
    public static bool OnlyAssigns(InitializerExpressionSyntax initializer) =>
        initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
        && initializer.Expressions.All(expression => expression is AssignmentExpressionSyntax
        {
            Left: IdentifierNameSyntax,
            Right: not InitializerExpressionSyntax,
        });

    /// <summary>
    /// The construction, with the initializer applied to what it builds, element by element in the
    /// order C# evaluates and applies them. IR in and out, so a lambda among the values keeps its own
    /// lines in the source map (#566).
    /// <para>
    /// Where a statement or a body is open to declare one, the object is a temporary of the function
    /// the C# is written in, and the initializer is a sequence over it:
    /// <c>($n0 = new X(…), $n0.a = f(), $n0.items.push(g()), $n0)</c>. Each element is applied before
    /// the next one's parts are evaluated, a member's getter is read before the parts of the element
    /// that adds to what it holds, and an <c>await</c> in a part is the enclosing function's own, as
    /// C# runs them all. Arrows that took the parts as arguments came before it: one for every part
    /// evaluated them all first, and one per element still evaluated an element's parts before the
    /// getter of the member it adds to, `get a get b inner n ` in .NET against `a get b get n inner `
    /// (both found by Copilot's review of #608).
    /// </para>
    /// <para>
    /// Where nothing is open to declare one (a field's or a property's initializer, a constructor's
    /// base call), C# lets no part await, so a function invoked in place holds the same statements and
    /// runs them once, in the same order.
    /// </para>
    /// </summary>
    public static JsExpr Apply(JsExpr construction, InitializerExpressionSyntax initializer, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(initializer.Parent as ExpressionSyntax ?? initializer);
        var application = new Application(context);
        if (context.Temporaries.CanBind)
        {
            var name = context.Temporaries.Fresh();
            context.Temporaries.Bind(name);
            var bound = JsExpr.Identifier(name);
            application.Collect(bound, type, initializer);
            var sequence = application.Elements.Aggregate(JsExpr.Binary(bound, "=", construction),
                (applied, element) => JsExpr.Binary(applied, ",", element));
            return JsExpr.Group(JsExpr.Binary(sequence, ",", bound));
        }

        var target = JsExpr.Identifier(Target);
        application.Collect(target, type, initializer);
        var body = JsStatement.Block([
            JsStatement.Const(Target, construction),
            .. application.Elements.Select(JsStatement.Expression),
            JsStatement.Return(target),
        ]);
        return JsExpr.Call(JsExpr.ArrowBlock("", body, context.Layout, context.Depth));
    }

    /// <summary>The local the object lives in where no temporary can be declared.</summary>
    private const string Target = "$o";

    /// <summary>The member an initializer's entry writes on the twin: a field's slot, which moves a case apart
    /// from another member (FieldSlotExtensions, #396), or the member's twin name.</summary>
    private static string Member(IdentifierNameSyntax name, ConversionContext context) => name.MemberSlot(context);

    /// <summary>The expressions an initializer applies to the object, one per element, in its order,
    /// each with its parts converted where it stands.</summary>
    private sealed class Application(ConversionContext context)
    {
        private readonly List<JsExpr> _elements = [];

        /// <summary>The elements, in the order C# applies them.</summary>
        public IReadOnlyList<JsExpr> Elements => _elements;

        private JsExpr Part(ExpressionSyntax expression) => context.Converter.ConvertIr(expression);

        /// <summary>The elements an initializer applies to <paramref name="target"/>, in its order.</summary>
        public void Collect(JsExpr target, ITypeSymbol? targetType, InitializerExpressionSyntax initializer)
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
                            _elements.Add(written);
                        else
                            Refuse(entry, "an entry of this type");
                        break;

                    // `Name = { … }`: the initializer applied to what the member holds, read again for
                    // each element, as C# reads it.
                    case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name, Right: InitializerExpressionSyntax nested }:
                        Collect(JsExpr.Member(target, Member(name, context)), context.SemanticHelper.GetType(name), nested);
                        break;

                    case AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } assignment:
                        _elements.Add(JsExpr.Binary(JsExpr.Member(target, Member(name, context)), "=", Part(assignment.Right)));
                        break;

                    // An element of a collection initializer: its Add, with one argument or with several.
                    default:
                        var arguments = element is InitializerExpressionSyntax { RawKind: (int)SyntaxKind.ComplexElementInitializerExpression } several
                            ? several.Expressions.ToList()
                            : [element];
                        var add = context.SemanticHelper.CollectionInitializerMethod(element);
                        if (Add(target, targetType, add, arguments, element) is { } call)
                            _elements.Add(call);
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
        /// other, before any of its parts is converted.
        /// </summary>
        private JsExpr? Add(JsExpr receiver, ITypeSymbol? targetType, IMethodSymbol? add, IReadOnlyList<ExpressionSyntax> arguments,
            SyntaxNode element)
        {
            // The bound tree names a classic extension in its static form: the call's lowering takes
            // the reduced one, on the collection's type.
            if (add is { IsExtensionMethod: true } || add.ExtensionBlockHome() is not null)
            {
                var extension = add is { IsExtensionMethod: true, ReducedFrom: null }
                    ? targetType is null ? null : add.ReduceExtensionMethod(targetType)
                    : add;
                if (extension is null) return null;
                return InvocationStrategy.Extension(extension, extension.Name, receiver, [.. arguments.Select(Part)], element, context);
            }

            var declaring = add?.ContainingType;
            if (add is null ? targetType.IsDictionary() : declaring.IsDictionary())
                return arguments.Count == 2 ? Types.DictionaryStrategy.Add(receiver, Part(arguments[0]), Part(arguments[1])) : null;

            var owner = declaring?.OriginalDefinition.ToDisplayString() ?? "";
            if (owner is "System.Collections.Generic.HashSet<T>" or "System.Collections.Generic.ISet<T>"
                or "System.Collections.Generic.SortedSet<T>")
                return arguments.Count == 1 ? JsExpr.Call(JsExpr.Member(receiver, "add"), Part(arguments[0])) : null;
            if (Primitives.ListMethodStrategy.Lowers(add))
                return Primitives.ListMethodStrategy.Add(receiver, [.. arguments.Select(Part)]);

            if (declaring is not null && TwinCarries(declaring))
                return JsExpr.Call(JsExpr.Member(receiver, add!.Name.ToCamelCase()), [.. arguments.Select(Part)]);
            return null;
        }

        /// <summary>An entry written by its key: a Place over the object, a dictionary's through
        /// <c>$eq.mapSet</c>, as every write to an entry goes, and an indexer the type declares through
        /// the twin's <c>setItem</c>, its receiver, its keys and its value in the order C# evaluates
        /// them; or an array's slot, or a list's, which is an array on this side.</summary>
        private JsExpr? Entry(JsExpr receiver, ITypeSymbol? targetType, ImplicitElementAccessSyntax key, ExpressionSyntax value)
        {
            if (value is InitializerExpressionSyntax) return null;
            if (Place.Of(key, receiver, context) is { } place)
                return place.Write(Part(value));
            var slot = key.ArgumentList.Arguments.Count == 1 && (targetType is IArrayTypeSymbol || IsList(targetType));
            return slot ? JsExpr.Binary(JsExpr.Index(receiver, Part(key.ArgumentList.Arguments[0].Expression)), "=", Part(value)) : null;
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
