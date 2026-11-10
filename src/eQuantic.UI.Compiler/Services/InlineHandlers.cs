using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Refuses the escape hatch's inline handlers in an app that turned on the Content-Security-Policy
/// (EQ2115, #1269).
/// <para>
/// <c>HtmlElement.OnClientClick</c> and <c>OnLoad</c> take JavaScript as text and write it as
/// <c>onclick</c> and <c>onload</c>, and <c>DynamicElement.CustomAttributes</c> writes any
/// attribute, an <c>on…</c> one included. They stay: <c>HtmlElement</c> transcribes the DOM on
/// purpose. But a strict policy refuses every inline handler in the browser, wherever its markup was
/// written, so the server's rendering is no exception, and the build says so where the property is
/// set instead of a console saying it on a page.
/// </para>
/// <para>
/// The app states the policy in <c>Program.cs</c>, which the SDK's generator turns into
/// <c>[assembly: ContentSecurityPolicy]</c>. Every source the build compiles is read, the app's and
/// its referenced projects', since a component with a handler can live in a library that never sees
/// the app's <c>Program.cs</c>.
/// </para>
/// </summary>
internal static class InlineHandlers
{
    internal const string Code = "EQ2115";
    private const string PolicyAttribute = "eQuantic.UI.Server.ContentSecurityPolicyAttribute";
    private const string HtmlElement = "eQuantic.UI.Web.HtmlElement";
    private const string DynamicElement = "eQuantic.UI.Web.DynamicElement";

    /// <summary>Whether the app's assembly says it turned the policy on.</summary>
    internal static bool PolicyDeclared(Compilation app) =>
        app.Assembly.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == PolicyAttribute);

    /// <summary>Every inline handler of the escape hatch in <paramref name="compilations"/>, when
    /// <paramref name="app"/> declared the policy, and none otherwise.</summary>
    internal static IReadOnlyList<CompilationError> Refused(Compilation app, IEnumerable<Compilation> compilations)
    {
        if (!PolicyDeclared(app)) return [];
        var refused = new List<CompilationError>();
        foreach (var compilation in compilations)
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                switch (name.Identifier.ValueText)
                {
                    // Written, never read: a read puts nothing in the markup.
                    case "OnClientClick" or "OnLoad" when IsMember(model, name, HtmlElement) && IsWritten(name):
                        refused.Add(Error(name, $"'{name.Identifier.ValueText}' writes a handler as text"));
                        break;
                    case "CustomAttributes" when IsMember(model, name, DynamicElement):
                        foreach (var key in HandlerKeys(model, name))
                            refused.Add(Error(key, $"the custom attribute \"{model.GetConstantValue(key).Value}\" writes a handler as text"));
                        break;
                }
            }
        }
        return refused;
    }

    private static CompilationError Error(SyntaxNode at, string what)
    {
        var position = at.GetLocation().GetLineSpan();
        return new CompilationError
        {
            Code = Code,
            SourcePath = position.Path,
            Line = position.StartLinePosition.Line + 1,
            Column = position.StartLinePosition.Character + 1,
            Message = $"{what}, and the app's Content-Security-Policy refuses every inline handler in the "
                + "browser. Handle the event in C# (an OnClick, or an entry of CustomEvents), which the "
                + "runtime attaches as a listener.",
        };
    }

    /// <summary>Whether <paramref name="name"/> names a property declared by <paramref name="type"/>
    /// or a type that derives from it.</summary>
    private static bool IsMember(SemanticModel model, SimpleNameSyntax name, string type) =>
        model.GetSymbolInfo(name).Symbol is IPropertySymbol property
        && Lineage(property.ContainingType).Any(declaring => declaring.ToDisplayString() == type);

    /// <summary>The expression that names the property: <c>x.Name</c> when it is reached through a
    /// receiver, the bare name in an initializer or inside the element's own type.</summary>
    private static ExpressionSyntax Accessed(IdentifierNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;

    /// <summary>Whether the property is assigned there, the only use that writes the attribute.</summary>
    private static bool IsWritten(IdentifierNameSyntax name) =>
        Accessed(name) is var member
        && member.Parent is AssignmentExpressionSyntax assignment && assignment.Left == member;

    private static IEnumerable<INamedTypeSymbol> Lineage(INamedTypeSymbol? type)
    {
        for (; type is not null; type = type.BaseType) yield return type;
    }

    /// <summary>
    /// The constant keys of a <c>CustomAttributes</c> use that name an <c>on…</c> attribute: an
    /// initializer's <c>["onclick"] = …</c> or <c>{ "onclick", … }</c>, an index
    /// <c>.CustomAttributes["onclick"]</c>, and an <c>.Add("onclick", …)</c>.
    /// </summary>
    private static IEnumerable<ExpressionSyntax> HandlerKeys(SemanticModel model, IdentifierNameSyntax name)
    {
        var keys = new List<ExpressionSyntax>();
        var member = Accessed(name);
        // An initializer's `CustomAttributes = …` and a statement's `element.CustomAttributes = …` alike.
        if (member.Parent is AssignmentExpressionSyntax assignment && assignment.Left == member)
        {
            foreach (var initializer in assignment.Right.DescendantNodesAndSelf().OfType<InitializerExpressionSyntax>())
            foreach (var entry in initializer.Expressions)
            {
                switch (entry)
                {
                    case AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax access }:
                        keys.AddRange(access.ArgumentList.Arguments.Take(1).Select(argument => argument.Expression));
                        break;
                    case InitializerExpressionSyntax { Expressions.Count: > 0 } pair:
                        keys.Add(pair.Expressions[0]);
                        break;
                }
            }
        }
        if (member.Parent is ElementAccessExpressionSyntax index && index.Expression == member)
            keys.AddRange(index.ArgumentList.Arguments.Take(1).Select(argument => argument.Expression));
        if (member.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" } add
            && add.Parent is InvocationExpressionSyntax call)
            keys.AddRange(call.ArgumentList.Arguments.Take(1).Select(argument => argument.Expression));
        return keys.Where(key => model.GetConstantValue(key) is { HasValue: true, Value: string text }
            && text.Length > 2 && text.StartsWith("on", StringComparison.OrdinalIgnoreCase));
    }
}
