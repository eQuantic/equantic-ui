using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Where an instance property of a twin keeps its value (#591, #615). A C# property is a pair of
/// methods, virtual where the property is, and an auto-property's are the compiler's, over a backing
/// field of the type that declares it, which its initializer writes. In JavaScript a write under the
/// property's name reaches the first accessor of that name along the prototype chain, and an instance's
/// own value hides every accessor on the prototype, so a property a derived type overrides cannot be
/// held under its own name:
/// <list type="bullet">
/// <item>an abstract or a computed property keeps nothing;</item>
/// <item>a property with a backing field and an accessor of its own (one that uses <c>field</c>, or
/// writes one accessor and leaves the other to the compiler), and an auto-property declared
/// <c>virtual</c> or <c>override</c>, keep a STORE of their own, <c>$name</c>
/// (<see cref="TwinName.BackingSlot"/>), which accessors on the prototype read and write, and which the
/// twin's constructor starts;</item>
/// <item>every other auto-property keeps its value under its own name, the cheapest read there is: no
/// override can reach it.</item>
/// </list>
/// The syntax answers exactly, since a property a derived type may override says <c>virtual</c> or
/// <c>abstract</c>, and one that overrides says <c>override</c>.
/// </summary>
internal static class PropertyStore
{
    /// <summary>Whether every accessor of <paramref name="property"/> is the compiler's.</summary>
    public static bool IsAuto(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is null
        && property.AccessorList?.Accessors is { Count: > 0 } accessors
        && accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null);

    /// <summary>Whether <paramref name="property"/> has a backing field: it uses <c>field</c>, or an
    /// accessor of it is the compiler's.</summary>
    private static bool HasBackingField(PropertyDeclarationSyntax property) =>
        FieldExpressionStrategy.UsesBackingField(property)
        || property.AccessorList?.Accessors.Any(accessor => accessor.Body is null && accessor.ExpressionBody is null) == true;

    /// <summary>Whether <paramref name="property"/>, an instance one, keeps its value in a store of its
    /// own: it has a backing field and an accessor of its own, or it is an auto-property an override can
    /// reach.</summary>
    public static bool KeepsAStore(PropertyDeclarationSyntax property) =>
        !property.Modifiers.Any(SyntaxKind.StaticKeyword)
        && !property.Modifiers.Any(SyntaxKind.AbstractKeyword)
        && HasBackingField(property)
        && (!IsAuto(property)
            || property.Modifiers.Any(SyntaxKind.VirtualKeyword) || property.Modifiers.Any(SyntaxKind.OverrideKeyword));

    /// <summary>
    /// The slot an instance property's value lives in on each instance: its store, its own twin name for
    /// an auto-property no override can reach, and none for an abstract, a computed or a static one.
    /// </summary>
    public static string? SlotOf(PropertyDeclarationSyntax property) =>
        KeepsAStore(property) ? FieldExpressionStrategy.BackingSlot(property)
        : !property.Modifiers.Any(SyntaxKind.StaticKeyword) && !property.Modifiers.Any(SyntaxKind.AbstractKeyword)
          && IsAuto(property)
            ? TwinName.Of(property.Identifier.Text)
            : null;

    /// <summary>
    /// The accessors an override inherits: C# lets it declare only the getter, or only the setter, of a
    /// property whose base has both, and the other is the base's. A JavaScript accessor is one property
    /// with both halves, so a getter alone on the derived prototype hides the base's setter, and the twin
    /// writes the half it inherits as one that forwards to <c>super</c>. Asked of the symbol, whose
    /// overrides lead to the declaration that has the accessor; nothing where there is no model.
    /// </summary>
    public static (bool Getter, bool Setter) Inherited(PropertyDeclarationSyntax property, SemanticModel? model)
    {
        if (!property.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || model?.GetDeclaredSymbol(property) is not IPropertySymbol { OverriddenProperty: { } overridden })
            return (false, false);
        var declaresGetter = property.ExpressionBody is not null
            || property.AccessorList?.Accessors.Any(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) == true;
        var declaresSetter = property.AccessorList?.Accessors.Any(accessor =>
            accessor.IsKind(SyntaxKind.SetAccessorDeclaration) || accessor.IsKind(SyntaxKind.InitAccessorDeclaration)) == true;
        return (!declaresGetter && Along(overridden, candidate => candidate.GetMethod is not null),
            !declaresSetter && Along(overridden, candidate => candidate.SetMethod is not null));
    }

    /// <summary>
    /// Whether a property it overrides keeps the store <paramref name="property"/> keeps, in a base whose
    /// twin is written from its source: the store is one slot along the chain (<c>$name</c>), so the
    /// base's twin defines it on the instance, and the derived twin only writes it. Asked of the model;
    /// false where there is none, and for a base the twin cannot see the source of.
    /// </summary>
    public static bool BaseKeepsTheStore(PropertyDeclarationSyntax property, SemanticModel? model)
    {
        if (!property.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || model?.GetDeclaredSymbol(property) is not IPropertySymbol { OverriddenProperty: { } overridden })
            return false;
        return Along(overridden, candidate => candidate.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .Any(KeepsAStore));
    }

    /// <summary>
    /// The accessors the compiler writes for an instance property, the one rule both emitters write them
    /// by: over the store of a property that keeps one, a getter that returns it where C# writes no
    /// getter body, and a setter that writes it where C# writes no setter body, written for a property C#
    /// declares get-only too, since C# refuses every write to that one when it compiles and only the
    /// runtime's hydration reaches it (it adopts a member through its setter and skips one with a getter
    /// alone, as it skips a computed property); and the half an override inherits
    /// (<see cref="Inherited"/>), forwarded to <c>super</c>. The accessors C# writes a body for are the
    /// emitter's to lower.
    /// </summary>
    /// <param name="property">The instance property.</param>
    /// <param name="model">The model that can answer about it, or null.</param>
    /// <param name="getterAnnotation">What a getter it writes is annotated with (<c>: T</c>, or nothing).</param>
    /// <param name="valueParameter">The parameter of a setter it writes (<c>value: T</c>, or <c>value</c>).</param>
    public static IEnumerable<JsClassMember> CompilerAccessors(PropertyDeclarationSyntax property, SemanticModel? model,
        string getterAnnotation, string valueParameter)
    {
        var name = TwinName.Of(property.Identifier.Text);
        var accessors = property.AccessorList?.Accessors ?? default;
        var getter = accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        var setter = accessors.FirstOrDefault(accessor =>
            accessor.IsKind(SyntaxKind.SetAccessorDeclaration) || accessor.IsKind(SyntaxKind.InitAccessorDeclaration));
        var (inheritsGetter, inheritsSetter) = Inherited(property, model);
        var store = JsExpr.ThisMember(FieldExpressionStrategy.BackingSlot(property));
        var inherited = JsExpr.Member(JsExpr.Identifier("super"), name);
        var stored = KeepsAStore(property);

        if (stored && getter is { Body: null, ExpressionBody: null })
            yield return JsClassMember.Getter("", name, getterAnnotation, JsStatement.Return(store));
        else if (inheritsGetter)
            yield return JsClassMember.Getter("", name, getterAnnotation, JsStatement.Return(inherited));

        if (stored && setter is null or { Body: null, ExpressionBody: null })
            yield return JsClassMember.Setter("", name, valueParameter,
                JsStatement.Expression(JsExpr.Binary(store, "=", JsExpr.Identifier("value"))));
        else if (inheritsSetter)
            yield return JsClassMember.Setter("", name, valueParameter,
                JsStatement.Expression(JsExpr.Binary(inherited, "=", JsExpr.Identifier("value"))));
    }

    /// <summary>Whether <paramref name="property"/> or a property it overrides answers <paramref name="has"/>.</summary>
    private static bool Along(IPropertySymbol property, Func<IPropertySymbol, bool> has)
    {
        for (var candidate = property; candidate is not null; candidate = candidate.OverriddenProperty)
            if (has(candidate)) return true;
        return false;
    }
}
