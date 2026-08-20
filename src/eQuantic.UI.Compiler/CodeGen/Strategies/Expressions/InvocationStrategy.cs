using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// General strategy for method invocations (fallback).
/// Handles:
/// - Console.WriteLine -> console.log
/// - Math methods
/// - Dictionary methods (TryGetValue, ContainsKey)
/// - Service provider methods (GetService, GetRequiredService)
/// - General method calls
/// Note: String and List methods are handled by dedicated strategies in Primitives/
/// </summary>
public class InvocationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is InvocationExpressionSyntax;
    }

    /// <summary>
    /// The call itself, plus the unwrapping a call with `out`/`ref` arguments needs — see
    /// <see cref="OutParameters"/> for the shape and why it exists.
    /// </summary>
    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var call = ConvertCall(invocation, context);

        var byReference = ByReferenceArguments(invocation, context);
        if (byReference.Count == 0) return call;
        // Only a method the compiler EMITS returns the object this unwraps. A framework method with
        // an `out` either has a strategy of its own (higher priority, never reaching here) or is
        // already reported as untranslatable.
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol method
            || !method.Locations.Any(location => location.IsInSource))
            return call;

        var assignments = byReference
            .Where(entry => entry.Target.Length > 0)
            .Select(entry => $"{entry.Target} = $o.{entry.Field}, ");
        return $"($o => ({string.Concat(assignments)}$o.$))({call})";
    }

    /// <summary>Each `out`/`ref` argument: what it assigns to, and the field of the result object it
    /// takes that from (the callee's parameter name). `out _` discards, so it has no target.</summary>
    private static List<(string Target, string Field, bool IsOut)> ByReferenceArguments(
        InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var result = new List<(string, string, bool)>();
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var arguments = invocation.ArgumentList.Arguments;
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            var isOut = argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword);
            if (!isOut && !argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword)) continue;

            var target = argument.Expression switch
            {
                DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax single }
                    => single.Identifier.Text.ToJsIdentifier(),
                // A discard, written either way: `out int _` carries a type, plain `out _` does not.
                DeclarationExpressionSyntax => "",
                IdentifierNameSyntax { Identifier.Text: "_" } => "",
                var expression => context.Converter.ConvertExpression(expression),
            };
            var field = argument.NameColon is { } named
                ? named.Name.Identifier.Text.ToJsIdentifier()
                : symbol is not null && index < symbol.Parameters.Length
                    ? symbol.Parameters[index].Name.ToJsIdentifier()
                    : $"$out{index}";
            result.Add((target, field, isOut));
        }
        return result;
    }

    private string ConvertCall(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var methodExpression = invocation.Expression;
        var methodName = methodExpression.ToString();

        if (methodExpression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.Text;
        }
        // `a?.M(x)` — the member is a BINDING, and `.M` (dot included) as the name is what used to
        // leak a leading dot into the fallback's output. The conditional-access strategy owns the
        // guard and the receiver; the fallback only names the call.
        else if (methodExpression is MemberBindingExpressionSyntax methodBinding)
        {
            methodName = methodBinding.Name.Identifier.Text;
        }

        // 2. Semantic Resolution (needed BEFORE argument conversion — named arguments reorder
        // against the resolved signature).
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;

        // 1. Resolve Arguments
        var argsList = new List<string>();
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            // `out` is not passed IN — it comes back in the result object. `ref` is read before it
            // is written, so it stays an argument as well as a field of the result.
            if (arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword)) continue;
            argsList.Add(arg.Expression is DeclarationExpressionSyntax declaration
                ? declaration.Designation.ToString().ToJsIdentifier()
                : context.Converter.ConvertExpression(arg.Expression));
        }

        // A `params` parameter is a REST parameter on the other side, so the two C# call forms
        // diverge here. `Count("a", "b")` already lines up; `Count(new[] { "a" })` — passing the
        // array whole, which C# also allows — would arrive as ONE element rather than the array,
        // so it spreads. Recognised from the SIGNATURE plus the argument's own type, because that
        // is the only thing that tells the two forms apart.
        if (symbol is { Parameters.Length: > 0 } && symbol.Parameters[^1].IsParams
            && argsList.Count == symbol.Parameters.Length
            && invocation.ArgumentList.Arguments.Count == symbol.Parameters.Length
            && context.SemanticHelper.GetType(invocation.ArgumentList.Arguments[^1].Expression)
                is IArrayTypeSymbol)
        {
            argsList[^1] = $"...{argsList[^1]}";
        }

        // NAMED arguments bind by NAME in C# but JS only has positions: reorder into the
        // signature's slots, filling skipped parameters from their defaults — otherwise
        // `Primary(label, compact: true)` lands `true` in the `leading` slot (an 8px ghost
        // icon and the wrong size, found on the header CTA).
        if (symbol != null && invocation.ArgumentList.Arguments.Any(a => a.NameColon != null))
        {
            var slots = new string?[symbol.Parameters.Length];
            var arguments = invocation.ArgumentList.Arguments;
            for (var i = 0; i < arguments.Count && i < argsList.Count; i++)
            {
                var name = arguments[i].NameColon?.Name.Identifier.Text;
                // A positional argument's slot is its LIST position — C# only allows positionals
                // after a named argument when that named argument sits IN its own position
                // (`Column(gap: Space.S3, [children])`), so the list index is the parameter index.
                // An independent counter restarted at 0 after the named ones and CLOBBERED slot 0.
                var ordinal = name == null
                    ? i
                    : symbol.Parameters.FirstOrDefault(p => p.Name == name)?.Ordinal ?? -1;
                if (ordinal >= 0 && ordinal < slots.Length) slots[ordinal] = argsList[i];
            }
            var lastSet = Array.FindLastIndex(slots, s => s != null);
            argsList = new List<string>();
            for (var i = 0; i <= lastSet; i++)
                argsList.Add(slots[i] ?? ObjectCreationStrategy.DefaultLiteralFor(symbol.Parameters[i]));
        }
        var args = string.Join(", ", argsList);

        // 4. General Method Call (Fallback)
        if (methodExpression is MemberAccessExpressionSyntax genAccess)
        {
            var caller = context.Converter.ConvertExpression(genAccess.Expression);

            // Handle delegate/action Invoke() calls
            if (methodName == "Invoke")
            {
                return $"{caller}({args})";
            }

            // EXTENSION METHOD in reduced form (`node.Also(x => …)`): JS has no extensions, so the
            // call goes back to its static home with the receiver as the first argument —
            // `NodeExtensions.also(node, x => …)`. The declaring static class is emitted as its own
            // module by the app-type pipeline, and the qualified name here is what makes the
            // import scanner pick it up. BCL extensions (LINQ et al.) never reach this branch —
            // their dedicated strategies run at higher priority.
            if (symbol is { IsExtensionMethod: true, ReducedFrom: not null, ContainingType: not null })
            {
                // An extension over the RUNTIME VOCABULARY has no module to go home to — the
                // hand-written twin carries the behaviour as an instance method, so the reduced
                // form survives as a reduced form. Importing the C#-side static class would ask
                // for a file the runtime never emits.
                if (IsRuntimeVocabulary(symbol.ContainingType))
                    return $"{caller}.{methodName.ToCamelCase()}({args})";

                // An extension declared OUTSIDE this compilation has no module to go home to:
                // emitting `MemoryExtensions.startsWith(...)` names a class the bundle never
                // contains, and the failure surfaces as a bare "is not defined" in the browser.
                if (!symbol.ContainingType.Locations.Any(location => location.IsInSource)
                    && !IsFrameworkProvided(symbol.ContainingType))
                {
                    context.Report(invocation, ConversionSeverity.Error, "EQ2004",
                        $"'{symbol.ContainingType.ToDisplayString()}.{symbol.Name}' is an extension "
                        + "method with no JavaScript translation — the class that declares it is not "
                        + "part of this compilation, so nothing emits it. Use an instance member, or "
                        + "add a strategy for it.");
                }
                // The declaring class never appears in the SOURCE (the call is reduced), so the
                // syntax-walking import collector can't see it — register the name we introduced.
                context.UsedAppTypes.Add(symbol.ContainingType.Name);
                var receiverFirst = string.IsNullOrEmpty(args) ? caller : $"{caller}, {args}";
                return $"{symbol.ContainingType.Name}.{methodName.ToCamelCase()}({receiverFirst})";
            }

            // C# 14 extension-BLOCK method (`extension(T receiver) { … }`): the emitter lowers it
            // to a static on the declaring class with the receiver first, and the call follows it
            // there. A STATIC extension member (receiver-type-only block) takes no receiver.
            if (symbol.ExtensionBlockHome() is { } extensionHome)
            {
                extensionHome.RegisterIntroduced(context);
                var extensionArgs = symbol!.IsStatic
                    ? args
                    : string.IsNullOrEmpty(args) ? caller : $"{caller}, {args}";
                return $"{extensionHome.Name}.{methodName.ToCamelCase()}({extensionArgs})";
            }

            ReportIfUntranslatable(symbol, methodName, invocation, context);
            return $"{caller}.{methodName.ToCamelCase()}({args})";
        }

        // Invoking a DELEGATE VALUE by bare name (`configure(node)`, `OnSelect(i)`): the invocation
        // symbol is the delegate's Invoke, so resolve what the NAME binds to. A parameter/local is
        // a plain callable in scope — VERBATIM (it must match the binding, not our casing rules);
        // a delegate-typed MEMBER is `this.<camel>(…)` like every other member access.
        if (symbol is { MethodKind: MethodKind.DelegateInvoke }
            && methodExpression is IdentifierNameSyntax delegateIdentifier)
        {
            var delegateTarget = context.SemanticHelper.Knows(delegateIdentifier)
                ? context.SemanticModel?.GetSymbolInfo(delegateIdentifier).Symbol
                : null;
            // …but a PRIMARY-CONSTRUCTOR parameter is neither: Roslyn models it as a parameter and
            // it behaves like an instance field, so emitting it bare compiles and then throws a
            // ReferenceError the moment the callback runs — long after the page looked fine.
            if (delegateTarget.IsInScopeBinding())
                return $"{delegateIdentifier.Identifier.Text}({args})";
            return $"this.{delegateIdentifier.Identifier.Text.ToCamelCase()}({args})";
        }

        // Direct invocation (Function() -> function())
        bool needsThis = false;

        // A STATIC method reached unqualified is a sibling on the enclosing type (or a `using static`
        // import) — it compiles to a class static, so it must be called THROUGH the class:
        // `DashboardView.thousands(...)`, never bare `thousands(...)` (undefined at runtime) nor
        // `this.thousands(...)`. Local functions are excluded (they are plain in-scope functions).
        if (symbol != null && symbol.IsStatic
            && symbol.MethodKind != MethodKind.LocalFunction
            && symbol.ContainingType != null)
        {
            // The declaring class never appears in the SOURCE (the call is unqualified), so the
            // syntax-walking import collector cannot see it — register the name we introduce, in
            // the bucket its NAMESPACE decides. The declarative factory surface lives in the
            // shared library, so `UI.column(…)` must import UI from the runtime.
            var declaring = symbol.ContainingType;
            var declaringNamespace = declaring.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (RuntimeProvidedTypeScanner.IsRuntimeProvidedNamespace(declaringNamespace))
                context.UsedRuntimeTypes.Add(declaring.Name);
            else
                context.UsedAppTypes.Add(declaring.Name);
            return $"{declaring.Name}.{methodName.ToCamelCase()}({args})";
        }

        // STANDALONE factory calls (no semantic model — the playground's mode): nothing can RESOLVE
        // `Column(...)` to the declarative surface, but the SOURCE names it — a
        // `using static …eQuantic.UI.Components.UI;` directive, a Capitalized call, and an
        // enclosing type that declares no such member can only be the factory class. Emit it
        // through the class exactly as the semantic path does, and register the introduced name
        // for the import scanner (the standalone fallback routes it to the runtime).
        if (symbol == null && methodExpression is IdentifierNameSyntax
            && methodName.Length > 0 && char.IsUpper(methodName[0])
            && !RuntimeProvidedTypeScanner.ComponentModelMemberNames.Contains(methodName)
            && HasFactoryUsingStatic(invocation)
            && !EnclosingTypeDeclares(invocation, methodName))
        {
            context.UsedRuntimeTypes.Add("UI");
            return $"UI.{methodName.ToCamelCase()}({args})";
        }

        // Use semantic resolution if available. Local functions are NOT members of `this` even
        // though they have a containing type — they compile to a plain `function` in the same scope.
        if (symbol != null && !symbol.IsStatic && symbol.MethodKind != MethodKind.LocalFunction)
        {
            if (symbol.ContainingType != null)
            {
                needsThis = true;
            }
        }
        
        // Heuristic fallback
        if (!needsThis && !string.IsNullOrEmpty(context.CurrentClassName))
        {
            if (char.IsUpper(methodName[0]) && symbol == null)
            {
                 needsThis = true;
            }
        }

        if (needsThis)
        {
            // A `?.`-shape call passes through here with a THROWAWAY result: the conditional-access
            // strategy discards anything not starting with '.', then emits its own `?.name(…)`.
            // Reporting on a transit that never ships poisoned real builds — `_tick?.Dispose()`
            // raised EQ2004 while the emitted `?.dispose()` was the working translation.
            if (!invocation.IsNullConditional())
                ReportIfUntranslatable(symbol, methodName, invocation, context);
            return $"this.{methodName.ToCamelCase()}({args})";
        }

        ReportIfUntranslatable(symbol, methodName, invocation, context);
        return $"{methodName.ToCamelCase()}({args})";
    }

    /// <summary>
    /// The fallback has reached a call the compiler has NO translation for. Emitting
    /// <c>Enumerable.range(…)</c> and hoping is how a page dies in the browser with
    /// "Enumerable is not defined" — long after the file and line that caused it stopped existing.
    /// This puts that failure back at build time.
    /// <para>
    /// A call is translatable when the compiler can SEE its declaration: anything the app declares
    /// becomes a module, so it stays silent. So does the framework, whose twins the runtime ships,
    /// and a delegate invocation, which is just a call. What is left is a method that arrives as
    /// metadata from somewhere the browser will never have — and that is the error.
    /// </para>
    /// </summary>
    private static void ReportIfUntranslatable(IMethodSymbol? symbol, string methodName, SyntaxNode node,
        ConversionContext context)
    {
        if (symbol is null)
        {
            // An AUTHORITATIVE model knows this node and could not bind it. That is never a
            // translation target — it is a missing reference or code that does not compile — and
            // the camelCase guess the caller is about to emit is exactly how a missing ref once
            // shipped `List.Add` as a JavaScript `.add` that died in the browser. Guessing stays
            // legal where it is honest (see ConversionContext.CanGuess): snippet mode, rewritten
            // nodes, and hosts that never promised a complete model.
            if (!context.CanGuess(node))
            {
                context.Report(node, ConversionSeverity.Error, "EQ2006",
                    $"'{methodName}' does not bind in the compiler's semantic model, so any translation "
                    + "would be a guess. Either this code does not compile, or the compiler is missing "
                    + "references/generated sources — the SDK passes them via --refs/--generated; a "
                    + "custom host must do the same.");
            }
            return;
        }
        // Calling a delegate IS a call — `OnChanged(value)` needs no translation.
        if (symbol.MethodKind is MethodKind.DelegateInvoke or MethodKind.LocalFunction) return;

        var declaring = symbol.ContainingType;
        if (declaring is null) return;
        // Declared in this compilation → it becomes a module of its own.
        if (declaring.Locations.Any(location => location.IsInSource)) return;
        if (IsFrameworkProvided(declaring)) return;

        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"'{declaring.ToDisplayString()}.{symbol.Name}' has no JavaScript translation. "
            + "The transpiler only knows the constructs it maps explicitly; add a strategy for it, "
            + "or move the call behind a [ServerAction].");
    }

    /// <summary>Does the file import the declarative factory surface with `using static`? Matched on
    /// the directive's own text — in standalone mode the directive is the only evidence there is.</summary>
    private static bool HasFactoryUsingStatic(SyntaxNode node) =>
        node.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Any(directive => directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)
                && directive.Name?.ToString().EndsWith("eQuantic.UI.Components.UI") == true);

    /// <summary>Does the enclosing type declare a method — or any enclosing block a local function —
    /// with this name? Then the bare call is THEIRS, and the local/`this` paths keep owning it.</summary>
    private static bool EnclosingTypeDeclares(SyntaxNode node, string methodName)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is BlockSyntax block
                && block.Statements.OfType<LocalFunctionStatementSyntax>()
                    .Any(local => local.Identifier.Text == methodName))
                return true;
            if (ancestor is TypeDeclarationSyntax type)
                return type.Members.OfType<MethodDeclarationSyntax>()
                    .Any(method => method.Identifier.Text == methodName);
        }
        return false;
    }

    /// <summary>Namespaces whose twins the runtime ships — the framework itself, and its icon packs.</summary>
    private static bool IsFrameworkProvided(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns == "eQuantic" || ns.StartsWith("eQuantic.");
    }

    /// <summary>
    /// Types the RUNTIME provides a hand-written twin for — the shared vocabulary. Same rule the
    /// object-creation and <c>with</c> paths use.
    /// </summary>
    private static bool IsRuntimeVocabulary(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns == "eQuantic.UI.Primitives" || ns.StartsWith("eQuantic.UI.Primitives.");
    }

    public int Priority => 1; // Lowest priority (fallback)
}
