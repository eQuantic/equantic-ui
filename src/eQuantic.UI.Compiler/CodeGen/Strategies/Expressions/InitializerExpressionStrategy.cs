using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for object and collection initializers.
/// Handles:
/// - { new A(), new B() } → [ new A(), new B() ]
/// - { Prop = val } → { prop: val }
/// - { {k, v} } → { k: v }, and the runtime's dictionary under a dictionary-typed member
/// <para>
/// Built as IR, each value as its own node, so a lambda an initializer holds (the handler a node is
/// configured with) reaches the writer as an arrow whose block maps line by line (#492).
/// </para>
/// </summary>
public class InitializerExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is InitializerExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        return ConvertInitializer((InitializerExpressionSyntax)node, context);
    }

    public JsExpr ConvertInitializer(InitializerExpressionSyntax initializer, ConversionContext context)
    {
        // An initializer nested under a dictionary-typed member (`Map = { ["a"] = 1 }`) seeds the
        // runtime's dictionary class, as a constructed dictionary is seeded.
        if (initializer.Parent is AssignmentExpressionSyntax { Left: var member } parent && parent.Right == initializer
            && context.SemanticHelper.GetType(member) is { } memberType && memberType.IsDictionary())
            return DictionaryStrategy.Seeded(memberType, initializer, context);

        // Collection Initializer: { new A(), new B() } -> [ new A(), new B() ]
        if (initializer.Kind() == SyntaxKind.CollectionInitializerExpression)
        {
            // Pairs for a target that is not a dictionary: { {k, v}, {k, v} }
            if (initializer.Expressions.Count > 0 && initializer.Expressions.All(e => e is InitializerExpressionSyntax ie && ie.Expressions.Count == 2))
            {
                return JsExpr.Object(initializer.Expressions.Cast<InitializerExpressionSyntax>()
                    .Select(ie => new JsProperty(context.Converter.ConvertExpression(ie.Expressions[0]),
                        context.Converter.ConvertIr(ie.Expressions[1])))
                    .ToList());
            }

            return JsExpr.Array(initializer.Expressions.Select(e => context.Converter.ConvertIr(e)).ToList());
        }

        // Object Initializer: { Prop = Value } -> { prop: value }
        if (initializer.Kind() == SyntaxKind.ObjectInitializerExpression)
        {
            var props = new List<JsProperty>();
            foreach (var expr in initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    // Indexer keys in an initializer (`["cs"] = CSharp`, `[TokenKind.X] = v`): a JS
                    // COMPUTED key, with the key expression properly converted (an enum key lowers
                    // to its member string, where the old raw text named a class that doesn't
                    // exist). C# 13's from-the-END form (`[^1] = v`) means "mutate the EXISTING
                    // member's tail", which an object literal cannot say — that one is fenced.
                    if (assignment.Left is ImplicitElementAccessSyntax elementKey)
                    {
                        if (elementKey.ArgumentList.Arguments.Count != 1
                            || elementKey.ArgumentList.Arguments[0].Expression
                                is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.IndexExpression })
                        {
                            context.Report(assignment, ConversionSeverity.Error, "EQ2008",
                                "from-the-end indexer assignments inside initializers "
                                + "(`X = { [^i] = v }`) are not lowered yet — assign after construction.");
                            continue;
                        }

                        var key = context.Converter.ConvertExpression(elementKey.ArgumentList.Arguments[0].Expression);
                        props.Add(new JsProperty($"[{key}]", context.Converter.ConvertIr(assignment.Right)));
                        continue;
                    }

                    var propName = assignment.Left.ToString();

                    // Children in an initializer: a nested initializer is converted here, where its
                    // parent is known, and an empty one is an empty list of children.
                    var value = propName == "Children" && assignment.Right is InitializerExpressionSyntax childInit
                        ? ConvertInitializer(childInit, context)
                        : context.Converter.ConvertIr(assignment.Right);
                    if (propName == "Children" && IsEmptyObject(value)) value = JsExpr.Array([]);

                    // Event handler binding: Use semantic model to detect delegate/action assignments
                    var isEventHandler = false;
                    var leftType = context.SemanticHelper.GetType(assignment.Left);
                    if (leftType != null)
                    {
                        if (leftType.TypeKind == TypeKind.Delegate) isEventHandler = true;
                        else if ((leftType.Name == "Action" || leftType.Name == "Func") && context.SemanticHelper.IsSystemType(leftType))
                            isEventHandler = true;
                    }

                    if (isEventHandler)
                    {
                        var rightSymbol = context.SemanticHelper.GetSymbol(assignment.Right);
                        if (rightSymbol is IMethodSymbol methodSymbol && !methodSymbol.IsStatic)
                        {
                            // If it's an instance method reference and not already bound or a lambda
                            var text = JsExprWriter.Write(value);
                            if (!text.Contains("=>") && !text.Contains("function") && !text.Contains(".bind("))
                            {
                                value = JsExpr.Call(JsExpr.Member(value, "bind"), JsExpr.This);
                            }
                        }
                    }

                    props.Add(new JsProperty(propName.ToCamelCase(), value));
                }
            }
            return JsExpr.Object(props);
        }

        // Bare array initializer: `string[] N = { "a", "b" }` (no `new[]`) — an ArrayInitializerExpression,
        // not a collection/object initializer. Map its elements to a JS array, same as `new[] { … }`.
        if (initializer.Kind() == SyntaxKind.ArrayInitializerExpression)
        {
            return JsExpr.Array(initializer.Expressions.Select(e => context.Converter.ConvertIr(e)).ToList());
        }

        return JsExpr.Object([]);
    }

    /// <summary>An object with nothing in it, as a node or as text a strategy wrote: what an empty
    /// initializer of children is.</summary>
    private static bool IsEmptyObject(JsExpr value) =>
        value is JsObject { Properties.Count: 0 }
        || JsExprWriter.Write(value).Trim() is var text
            && (text.Length == 0 || (text.StartsWith('{') && text.EndsWith('}') && string.IsNullOrWhiteSpace(text[1..^1])));

    public int Priority => 10;
}
