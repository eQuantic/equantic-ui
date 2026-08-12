using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Track L D2: a resx accessor (<c>Strings.Hero_Title</c>) is REWRITTEN to a runtime lookup —
/// <c>$eq.str("Strings", "Hero_Title")</c> — never emitted as a member read and never inlined.
/// The accessor looks constant-ish and is not: it resolves against <c>CurrentUICulture</c> at
/// call time, and an inlined value would bake the build machine's culture into the bundle (the
/// silent-wrong-code failure the plan calls out by name). Detection is the ResXFileCodeGenerator
/// shape via the semantic model (<see cref="ResourceClasses"/>); every rewrite records its
/// (id, key) pair so the CLI emits exactly the catalog the reachable tree needs (D3).
/// </summary>
public class ResourceAccessorStrategy : IConversionStrategy
{
    public int Priority => 60; // above InlinedConstantStrategy (25): the rewrite owns this shape

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax) return false;
        return context.SemanticHelper.GetSymbol(node) is IPropertySymbol property
            && ResourceClasses.IsResourceAccessor(property);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var property = (IPropertySymbol)context.SemanticHelper.GetSymbol(node)!;
        var id = ResourceClasses.IdFor(context.SemanticModel!.Compilation, property.ContainingType);
        var key = ResourceClasses.KeyFor(property);
        context.ResourceUses.Add(new ResourceUse(
            id, key, ResourceClasses.DesignerPathFor(property.ContainingType)));
        context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.Str}(\"{id}\", \"{key}\")";
    }
}
