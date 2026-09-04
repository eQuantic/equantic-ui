using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for IServiceProvider method invocations.
/// Handles:
/// - GetService&lt;T&gt;() → getService('T')
/// - GetRequiredService&lt;T&gt;() → getRequiredService('T')
/// - GetService(typeof(T)) → getService('T')
/// <para>
/// …and the same call with NO receiver, which is the component's own
/// <c>UiComponent.GetService&lt;T&gt;()</c> — what <c>OnMount</c> uses, having no context to ask.
/// That one cannot go through a receiver at all: it resolves from the browser's registry, the same
/// place a constructor dependency comes from.
/// </para>
/// </summary>
public class ServiceProviderStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        // No receiver: `GetService<IClock>()` inside a component. Only the model can tell that from
        // any other method of that name, and without one there is nothing to go on — the playground
        // compiles a buffer alone, and a bare name there stays an ordinary call.
        if (invocation.Expression is SimpleNameSyntax)
            return IsComponentCapability(invocation, context);

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        // `CapabilityScope.Resolve<T>()` — the STATIC resolver, which is what a generated factory
        // reaches for: it has no component and no context to ask. Without this the emitted factory
        // imports a name the runtime does not export and the module fails to load whole.
        if (IsCapabilityScope(invocation, context)) return true;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("GetService" or "GetRequiredService"))
            return false;

        // Check via semantic model if available
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (symbol != null)
        {
            var containingType = symbol.ContainingType.ToDisplayString();

            // Check both the short name and full name. ComponentContext is the WRITE-ONCE context,
            // and it was the one missing: the capability API landed there, this list knew only the
            // older Core RenderContext, so the call fell through to the ordinary invocation path —
            // which drops type arguments. Every page asked for a capability by no name at all.
            return containingType.Contains("IServiceProvider") ||
                   containingType.Contains("ServiceProvider") ||
                   containingType == "eQuantic.UI.Web.RenderContext" ||
                   containingType.EndsWith(".RenderContext") ||
                   containingType == "eQuantic.UI.Primitives.ComponentContext" ||
                   containingType.EndsWith(".ComponentContext") ||
                   // `this.GetService<T>()` inside a component — the same accessor as the bare call.
                   containingType == "eQuantic.UI.Primitives.UiComponent";
        }

        // Allow fallback - these method names are specific enough
        return true;
    }

    /// <summary>The static resolver on CapabilityScope — same registry, no receiver to speak of.</summary>
    private static bool IsCapabilityScope(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol symbol) return false;
        if (symbol.Name != "Resolve") return false;
        return symbol.ContainingType?.ToDisplayString() == "eQuantic.UI.Primitives.CapabilityScope";
    }

    /// <summary>The component's OWN capability accessor — declared on UiComponent, so a call with
    /// no receiver (or through `this`) inside a component is this one and nothing else.</summary>
    private static bool IsComponentCapability(InvocationExpressionSyntax invocation,
        ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol symbol) return false;
        if (symbol.Name is not ("GetService" or "GetRequiredService")) return false;
        return symbol.ContainingType?.ToDisplayString() == "eQuantic.UI.Primitives.UiComponent";
    }

    /// <summary>The type argument of a `GetService&lt;T&gt;()`, by its simple name — the key the
    /// registry is keyed by on every target.</summary>
    private static string? CapabilityName(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression as SimpleNameSyntax
            ?? (invocation.Expression as MemberAccessExpressionSyntax)?.Name;
        if (name is not GenericNameSyntax generic || generic.TypeArgumentList.Arguments.Count == 0)
            return null;

        var typeName = generic.TypeArgumentList.Arguments[0].ToString();
        return typeName.Contains('.') ? typeName.Split('.')[^1] : typeName;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;

        // The component's own accessor: it reads the same registry a constructor dependency is
        // resolved from, so OnMount and the constructor find the same capability. Written with no
        // receiver, or through `this` — and only the MODEL says which method that is, so a `this.`
        // call the model cannot resolve (the playground compiles a buffer alone) keeps the receiver
        // it was written with rather than being rewritten on a guess.
        if (invocation.Expression is SimpleNameSyntax
            || IsComponentCapability(invocation, context)
            || IsCapabilityScope(invocation, context))
        {
            var capability = CapabilityName(invocation);
            if (capability is null)
            {
                context.Report(node, ConversionSeverity.Error, "EQ2111",
                    "GetService needs its type argument to cross — the registry is keyed by the "
                    + "interface NAME, and there is nothing to key on here.");
                return "null";
            }

            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.ResolveService}('{capability}')";
        }

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var methodName = memberAccess.Name.Identifier.Text;
        var jsMethodName = methodName == "GetRequiredService" ? "getService" : methodName.ToCamelCase();

        // Check if generic: GetService<T>()
        if (memberAccess.Name is GenericNameSyntax genericName &&
            genericName.TypeArgumentList.Arguments.Count > 0)
        {
            var typeArg = genericName.TypeArgumentList.Arguments[0];
            var typeName = typeArg.ToString();

            // Extract the simple interface name (e.g., IAppTheme from eQuantic.UI.Core.Theme.IAppTheme)
            var simpleTypeName = typeName;
            if (typeName.Contains('.'))
            {
                var parts = typeName.Split('.');
                simpleTypeName = parts[^1]; // Get last part
            }

            return $"{caller}.{jsMethodName}('{simpleTypeName}')";
        }

        // Check for typeof(T) argument: GetService(typeof(T))
        var args = invocation.ArgumentList.Arguments;
        if (args.Count > 0)
        {
            var argExpr = args[0].Expression.ToString();
            if (argExpr.StartsWith("typeof("))
            {
                var typeName = argExpr.Substring(7, argExpr.Length - 8);

                // Extract simple name
                var simpleTypeName = typeName;
                if (typeName.Contains('.'))
                {
                    var parts = typeName.Split('.');
                    simpleTypeName = parts[^1];
                }

                return $"{caller}.{jsMethodName}('{simpleTypeName}')";
            }

            var arg = context.Converter.ConvertExpression(args[0].Expression);
            return $"{caller}.{jsMethodName}({arg})";
        }

        // If no generic type or argument, this is an error - we should warn
        // But fallback to empty call for backwards compatibility
        return $"{caller}.{jsMethodName}()";
    }

    public int Priority => 10;
}
