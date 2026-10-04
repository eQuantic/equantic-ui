using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// <c>new { A = a, b.C }</c> as the object literal <c>{ a: a, c: b.c }</c>, each value its own node,
/// so a lambda an anonymous object holds keeps its lines as one an initializer holds does (#492).
/// </summary>
public class AnonymousObjectCreationStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AnonymousObjectCreationExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var creation = (AnonymousObjectCreationExpressionSyntax)node;
        var members = new List<JsProperty>();

        foreach (var decl in creation.Initializers)
        {
            var expression = context.Converter.ConvertIr(decl.Expression);
            // A member without a name takes the one C# infers, which Roslyn answers for every form a
            // projection may take: `x`, `a.b`, and `a?.b` and `a!` too, which a name read off a
            // simple name or a member access alone missed. Code C# accepts always has one.
            var name = decl.NameEquals?.Name.Identifier.Text ?? SyntaxFacts.TryGetInferredMemberName(decl.Expression);
            members.Add(new JsProperty(name?.ToCamelCase() ?? JsExprWriter.Write(expression), expression));
        }

        return JsExpr.Object(members);
    }

    public int Priority => 10;
}
