using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// A TYPE DECLARES A METHOD NAME ONCE, because a JavaScript class has one member per name.
///
/// <para>
/// C# tells overloads apart by their parameters, and eqc names a method by its name alone, so two
/// overloads reached the twin as one. Measured on every emitter, and silent on every one. A plain
/// class, a record and a static class wrote both, and JavaScript keeps the LAST: the twin of
/// <c>CodeDiffer.Compare(CodeDocument, CodeDocument)</c>, declared before its sibling over two lists
/// of lines, was never called, and a document reached the other body, which read its
/// <c>length</c>. A component's parser kept the FIRST and dropped the rest, so <c>Label("a")</c>
/// ran <c>Label(int)</c>'s body. TypeScript refuses the duplicate, but only where a twin is
/// type-checked, which is the runtime's own: an app's module is bundled unchecked, and the failure
/// waited for the browser.
/// </para>
///
/// <para>
/// A gap and not the target's shape, so an EQ1xxx: the bound tree says which overload each call
/// binds, and a twin could carry every overload under a name of its own, as it carries a
/// user-defined operator. Until something does, the build stops at the second declaration and
/// names the first.
/// </para>
///
/// <para>
/// The key is the one the emitters write: the lowered name, and whether the member is static. A
/// static and an instance method may share a name, since one lives on the class and the other on its
/// prototype, while <c>Foo</c> and <c>foo</c> may not. Every member of a static class is static, and
/// so is every member of an extension block, which lowers to a static of the class holding it.
/// </para>
///
/// <para>
/// The name is taken along the CHAIN too: <c>Derived extends Base</c> has one member per name, so a
/// derived <c>Format(int)</c> beside the base's <c>Format(string)</c> answered every call on both.
/// With a model, the bases declared in the source are walked, the runtime's own being EQ2011's to
/// hold, and an override is the method it overrides rather than a second one.
/// </para>
/// </summary>
internal static class OverloadedMethods
{
    /// <param name="type">The declaration whose module is being emitted.</param>
    /// <param name="sourcePath">Where it lives, for the error.</param>
    /// <param name="isComponent">A component's parser leaves a <c>[ServerOnly]</c> method out of the
    /// twin, and its module embeds the static classes nested in it, each a class of its own.</param>
    /// <param name="model">The model of the declaration's tree, which walks its bases; without one only
    /// the declaration itself is checked.</param>
    public static List<CompilationError> Check(TypeDeclarationSyntax type, string sourcePath, bool isComponent,
        SemanticModel? model = null)
    {
        var errors = new List<CompilationError>();
        CheckOne(type, sourcePath, isComponent, errors);
        if (errors.Count == 0 && model is not null) CheckInherited(type, sourcePath, isComponent, model, errors);
        if (isComponent)
        {
            foreach (var nested in type.Members.OfType<ClassDeclarationSyntax>()
                         .Where(nested => nested.Modifiers.Any(SyntaxKind.StaticKeyword)))
            {
                CheckOne(nested, sourcePath, isComponent: false, errors);
            }
        }
        return errors;
    }

    private static void CheckOne(TypeDeclarationSyntax type, string sourcePath, bool isComponent,
        List<CompilationError> errors)
    {
        var allStatic = type.Modifiers.Any(SyntaxKind.StaticKeyword);
        var first = new Dictionary<(bool Static, string Name), MethodDeclarationSyntax>();
        foreach (var (method, isStatic) in Methods(type, allStatic, isComponent))
        {
            var key = (isStatic, method.Identifier.Text.ToCamelCase());
            if (!first.TryGetValue(key, out var earlier))
            {
                first[key] = method;
                continue;
            }
            var position = method.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            var earlierLine = earlier.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                Message =
                    $"'{type.Identifier.Text}.{Signature(method)}' lowers to `{(isStatic ? "static " : "")}{key.Item2}()`, "
                    + $"and so does '{Signature(earlier)}' (line {earlierLine}). C# tells overloads apart by "
                    + "their parameters, and a JavaScript class has one member per name, so the twin would keep "
                    + "one of them and every call would reach it. Give each its own name.",
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
        }
    }

    /// <summary>
    /// A method whose name a BASE already takes (see the type's remarks): the first inherited method
    /// under the same lowered name and static-ness that this one does not override, in the bases the
    /// source declares.
    /// </summary>
    private static void CheckInherited(TypeDeclarationSyntax type, string sourcePath, bool isComponent,
        SemanticModel model, List<CompilationError> errors)
    {
        if (model.SyntaxTree != type.SyntaxTree || model.GetDeclaredSymbol(type) is not INamedTypeSymbol declared)
            return;
        var allStatic = type.Modifiers.Any(SyntaxKind.StaticKeyword);
        foreach (var (method, isStatic) in Methods(type, allStatic, isComponent))
        {
            if (model.GetDeclaredSymbol(method) is not IMethodSymbol symbol) continue;
            var name = method.Identifier.Text.ToCamelCase();
            if (Inherited(declared, symbol, name, isStatic) is not { } inherited) continue;
            var position = method.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            var where = inherited.Locations.FirstOrDefault(location => location.IsInSource)?.GetLineSpan();
            var at = where is { } span ? $" ({Path.GetFileName(span.Path)} line {span.StartLinePosition.Line + 1})" : "";
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                Message =
                    $"'{type.Identifier.Text}.{Signature(method)}' lowers to `{(isStatic ? "static " : "")}{name}()`, "
                    + $"and so does '{inherited.ContainingType.Name}.{inherited.Name}("
                    + $"{string.Join(", ", inherited.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))})', "
                    + $"which it inherits{at}. A JavaScript class chain has one member per name, so the derived "
                    + "one would answer every call on both. Give each its own name.",
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
        }
    }

    /// <summary>The inherited method <paramref name="method"/> would take over, or null. A base the
    /// source does not declare ends the walk: its twin is the runtime's.</summary>
    private static IMethodSymbol? Inherited(INamedTypeSymbol declared, IMethodSymbol method, string name, bool isStatic)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        for (var current = declared.BaseType;
             current is { DeclaringSyntaxReferences.Length: > 0 } && seen.Add(current);
             current = current.BaseType)
        {
            foreach (var member in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind != MethodKind.Ordinary || member.IsImplicitlyDeclared) continue;
                if (member.IsStatic != isStatic || member.Name.ToCamelCase() != name) continue;
                if (member.ExplicitInterfaceImplementations.Length > 0 || Overrides(method, member)) continue;
                // A defining half of a partial method, or a server-only one, reaches no twin.
                if (member.IsPartialDefinition && member.PartialImplementationPart is null) continue;
                if (member.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is "ServerOnlyAttribute" or "ServerOnly"))
                    continue;
                return member;
            }
        }
        return null;
    }

    private static bool Overrides(IMethodSymbol method, IMethodSymbol candidate)
    {
        for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, candidate.OriginalDefinition))
                return true;
        return false;
    }

    /// <summary>The methods that take a name in the twin, in declaration order, with whether each
    /// one is static there.</summary>
    private static IEnumerable<(MethodDeclarationSyntax Method, bool Static)> Methods(TypeDeclarationSyntax type,
        bool allStatic, bool isComponent)
    {
        foreach (var member in type.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method when TakesAName(method, isComponent):
                    yield return (method, allStatic || method.Modifiers.Any(SyntaxKind.StaticKeyword));
                    break;

                case ExtensionBlockDeclarationSyntax block:
                    foreach (var extension in block.Members.OfType<MethodDeclarationSyntax>())
                        yield return (extension, true);
                    break;
            }
        }
    }

    /// <summary>
    /// The defining half of a partial method is the same method as its implementation, and names
    /// nothing of its own. A component's <c>[ServerOnly]</c> method never reaches the twin. An
    /// abstract method does take its name, though the base's twin writes nothing for it: the
    /// subclass that implements it writes it, over any overload the base carried.
    /// <para>
    /// An explicit interface implementation (<c>IEnumerable.GetEnumerator()</c> beside the generic
    /// one) is left out: its name is the interface's, which the author cannot give another, and
    /// "rename it" would be an answer nobody can follow. How it lowers is its own question.
    /// </para>
    /// </summary>
    private static bool TakesAName(MethodDeclarationSyntax method, bool isComponent)
    {
        if (method.ExplicitInterfaceSpecifier is not null) return false;
        if (method.Modifiers.Any(SyntaxKind.PartialKeyword) && method.Body is null && method.ExpressionBody is null)
            return false;
        return !isComponent || !method.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.IsNamed("ServerOnly"));
    }

    /// <summary><c>Compare(CodeDocument, CodeDocument)</c>: the name and the parameter types, which is
    /// what tells a reader the two apart.</summary>
    private static string Signature(MethodDeclarationSyntax method) =>
        $"{method.Identifier.Text}({string.Join(", ", method.ParameterList.Parameters.Select(parameter => parameter.Type?.ToString() ?? parameter.Identifier.Text))})";
}
