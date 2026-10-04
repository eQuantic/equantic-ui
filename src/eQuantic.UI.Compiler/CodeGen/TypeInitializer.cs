using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// A type's statics as C# initializes them (#417): every static starts at its type's zero, then the
/// initializers run in declaration order, then the static constructor's body, all at once, the first
/// time anything touches one of them. The twins defined each static where it was declared, so an
/// initializer that read a static declared after it read <c>undefined</c> (<c>static int A = B + 1;
/// static int B = 2;</c> answered NaN, where C# answers 1), and a record's static instance built
/// before a static its constructor reads saw it unset. A class kept a lazy getter per static instead,
/// which survived the runtime bundle's import cycles but initialized each static on its own first
/// read, in no order at all, and again whenever it held null.
/// <para>
/// So a type whose statics can observe one another (any initializer that is not a constant, or a
/// static constructor) holds them in a <c>$slots</c> object its <c>$init()</c> builds on first use,
/// zeros first; each static is an accessor pair over its slot, so a read during the initialization
/// sees what C# sees, a zero or a value already set, and every reader of the type, a cycle's included,
/// waits for nothing. A type whose initializers are constants, which read nothing, keeps its statics
/// as fields. One mechanism for a record, a struct, a class, a static class and a component.
/// </para>
/// </summary>
internal static class TypeInitializer
{
    /// <summary>The holder of a type's statics, built by <c>$init()</c>.</summary>
    public const string Slots = "$slots";

    /// <summary>One static that initializes in order: its name on the twin, its annotation, its zero,
    /// its initializer converted, and the C# it came from.</summary>
    public readonly record struct Ordered(string Name, string Type, string Zero, string Value, SyntaxNode Origin);

    /// <summary>
    /// Whether the type's statics initialize in order: one of them has an initializer that is not a
    /// constant, which may read another static, call a method or build an instance, or the type has a
    /// static constructor. A constant reads nothing, so a type of constants keeps its fields.
    /// </summary>
    public static bool Orders(TypeDeclarationSyntax type, Func<SyntaxNode, SemanticModel?> modelFor) =>
        type.Members.OfType<ConstructorDeclarationSyntax>().Any(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
        || Initializers(type).Any(initializer => !IsConstant(initializer, modelFor(initializer)));

    /// <summary>Whether a static member takes part: a static field, or a static property with an
    /// automatic getter or a store of its own, with an initializer. A constant never does.</summary>
    public static bool TakesPart(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.StaticKeyword)
            && !field.Modifiers.Any(SyntaxKind.ConstKeyword)
            && field.Declaration.Variables.Any(variable => variable.Initializer is not null),
        PropertyDeclarationSyntax property => property.Modifiers.Any(SyntaxKind.StaticKeyword) && property.Initializer is not null,
        _ => false,
    };

    private static IEnumerable<ExpressionSyntax> Initializers(TypeDeclarationSyntax type) =>
        type.Members.Where(TakesPart).SelectMany(member => member switch
        {
            FieldDeclarationSyntax field => field.Declaration.Variables
                .Select(variable => variable.Initializer?.Value).OfType<ExpressionSyntax>(),
            PropertyDeclarationSyntax { Initializer: { } initializer } => [initializer.Value],
            _ => [],
        });

    private static bool IsConstant(ExpressionSyntax initializer, SemanticModel? model) =>
        model is not null
            ? model.GetConstantValue(initializer).HasValue
            : initializer is LiteralExpressionSyntax
                or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax };

    /// <summary>
    /// The members that hold the ordered statics: the slots, the initializer that builds them, and an
    /// accessor pair per static. <paramref name="constructor"/> is the static constructor's body,
    /// which runs after the initializers.
    /// </summary>
    public static IEnumerable<JsClassMember> Members(string className, IReadOnlyList<Ordered> statics,
        IReadOnlyList<JsStatement> constructor, bool annotate)
    {
        yield return JsClassMember.Field("static ", Slots, annotate ? ": any" : "", "null");

        var zeros = string.Join(", ", statics.Select(member => $"{member.Name}: {member.Zero}"));
        var build = new List<JsStatement>
        {
            JsStatement.Raw($"const slots{(annotate ? ": any" : "")} = {className}.{Slots} = {{ {zeros} }};"),
        };
        build.AddRange(statics.Select(member =>
            JsStatement.Raw($"slots.{member.Name} = {member.Value};") with { Origin = member.Origin }));
        build.AddRange(constructor);
        yield return JsClassMember.Method("static ", "$init", "", "", annotate ? ": any" : "", JsStatement.Block([
            JsStatement.If(JsExpr.Binary(JsExpr.Member(JsExpr.Identifier(className), Slots), "===", JsExpr.Literal("null")),
                JsStatement.Block(build), null),
            JsStatement.Raw($"return {className}.{Slots};"),
        ]));

        foreach (var member in statics)
        {
            yield return JsClassMember.Getter("static ", member.Name, annotate ? $": {member.Type}" : "",
                JsStatement.Block([JsStatement.Raw($"return {className}.$init().{member.Name};")])) with { Origin = new JsOrigin(member.Origin) };
            yield return JsClassMember.Setter("static ", member.Name, annotate ? $"value: {member.Type}" : "value",
                JsStatement.Block([JsStatement.Raw($"{className}.$init().{member.Name} = value;")])) with { Origin = new JsOrigin(member.Origin) };
        }
    }
}
