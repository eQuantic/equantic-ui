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
/// static constructor) holds every one of them in a <c>$slots</c> object its <c>$init()</c> builds on
/// first use, zeros first; each static is an accessor pair over its slot, so a read during the
/// initialization sees what C# sees, a zero or a value already set, and every reader of the type, a
/// cycle's included, waits for nothing. A static with no initializer takes part too: the static
/// constructor is what sets it, and a read of it is what has to run that constructor; so does a
/// static field-like event, whose subscription is a use of the type. A type whose initializers are
/// constants, which read nothing, keeps its statics as fields. One mechanism for a record, a struct, a
/// class, a static class and a component.
/// </para>
/// <para>
/// A type whose initialization threw stays failed: every use of it throws the
/// TypeInitializationException that carries the exception, as .NET's does (<see cref="Members"/>).
/// </para>
/// <para>
/// What starts the initialization is a read or a write of one of the statics. A type that declares a
/// static constructor starts it in its instance constructor and in each static member too, a method,
/// a computed property and an operator included (<see cref="StartedIn"/>), since C# runs that
/// constructor before the first instance and the first use of any static member, and its effects can
/// be seen outside the type. A type without one has no such moment in C# either: its initializers run
/// at some point before the first read of a static, which is when they run here.
/// </para>
/// </summary>
internal static class TypeInitializer
{
    /// <summary>The holder of a type's statics, built by <c>$init()</c>, and the name of the local
    /// <c>$init()</c> builds it in: a name no C# local can take.</summary>
    public const string Slots = "$slots";

    /// <summary>The static method that builds <see cref="Slots"/> the first time it is called.</summary>
    public const string Init = "$init";

    /// <summary>The TypeInitializationException of a type whose initialization threw, which every use
    /// of the type throws from then on; null until then.</summary>
    public const string Failure = "$failure";

    /// <summary>What <c>$init()</c> calls the exception an initializer threw.</summary>
    private const string Thrown = "$error";

    /// <summary>
    /// The statement that starts a type's initialization: what a type with a static constructor runs
    /// first in its instance constructor and in each of its static members, since C# runs that
    /// constructor before the first instance and the first use of any static member, a method
    /// included, and not only before the first read of a static.
    /// </summary>
    public static JsStatement Start(string className) => JsStatement.Raw($"{className}.{Init}();");

    /// <summary>
    /// <paramref name="member"/> starting the type's initialization first, for a type that declares a
    /// static constructor (#417): its instance constructor, and each static method and accessor the app
    /// declared. A member the compiler writes for itself (a name starting with <c>$</c>) is left as it
    /// is, and so is an accessor over a static's slot (<paramref name="slots"/>), which starts it already.
    /// </summary>
    public static JsClassMember StartedIn(JsClassMember member, string className, IReadOnlySet<string> slots) => member switch
    {
        JsConstructorMember constructor => constructor with { Body = Prepended(constructor.Body, className) },
        JsMethodMember method when Applies(method.Modifiers, method.Name, slots) =>
            method with { Body = Prepended(method.Body, className) },
        JsAccessorMember accessor when Applies(accessor.Modifiers, accessor.Name, slots) =>
            accessor with { Body = Prepended(accessor.Body, className) },
        _ => member,
    };

    private static bool Applies(string modifiers, string name, IReadOnlySet<string> slots) =>
        modifiers.Contains("static", StringComparison.Ordinal) && !name.StartsWith('$') && !slots.Contains(name);

    private static JsStatement Prepended(JsStatement body, string className) =>
        JsStatement.Block([Start(className), .. body is JsBlock block ? block.Statements : [body]]);

    /// <summary>One static that initializes in order: its name on the twin, its annotation, its zero,
    /// its initializer converted (none for a static that holds its zero until something sets it), and
    /// the C# it came from.</summary>
    public readonly record struct Ordered(string Name, string Type, string Zero, string? Value, SyntaxNode? Origin);

    /// <summary>
    /// Whether the type's statics initialize in order: one of them has an initializer that is not a
    /// constant, which may read another static, call a method or build an instance, or the type has a
    /// static constructor. A constant reads nothing, so a type of constants keeps its fields.
    /// </summary>
    public static bool Orders(TypeDeclarationSyntax type, Func<SyntaxNode, SemanticModel?> modelFor) =>
        type.Members.OfType<ConstructorDeclarationSyntax>().Any(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
        || Initializers(type).Any(initializer => !IsConstant(initializer, modelFor(initializer)));

    /// <summary>The initializers of the type's statics: a static field's, a constant's excepted, a
    /// static property's and a static field-like event's.</summary>
    private static IEnumerable<ExpressionSyntax> Initializers(TypeDeclarationSyntax type) =>
        type.Members.SelectMany(member => member switch
        {
            BaseFieldDeclarationSyntax field when field.Modifiers.Any(SyntaxKind.StaticKeyword)
                && !field.Modifiers.Any(SyntaxKind.ConstKeyword) => field.Declaration.Variables
                .Select(variable => variable.Initializer?.Value).OfType<ExpressionSyntax>(),
            PropertyDeclarationSyntax { Initializer: { } initializer } property
                when property.Modifiers.Any(SyntaxKind.StaticKeyword) => [initializer.Value],
            _ => [],
        });

    private static bool IsConstant(ExpressionSyntax initializer, SemanticModel? model) =>
        model is not null
            ? model.GetConstantValue(initializer).HasValue
            : initializer is LiteralExpressionSyntax
                or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax };

    /// <summary>
    /// The members that hold the ordered statics: the slots, the failure, the initializer that builds
    /// them, and an accessor pair per static. <paramref name="constructor"/> is the static
    /// constructor's body, which runs after the initializers.
    /// <para>
    /// <c>$init()</c> builds the slots as C# initializes a type. The holder is assigned FIRST, empty, so
    /// a use of the type while it initializes (a static's zero that constructs a struct whose own
    /// constructor starts the type, a static constructor calling a static method of its type) finds it
    /// and sees what C# sees then, where it found none and started the initialization again, without
    /// end. Then every static is set to its zero, then the initializers run in declaration order, then
    /// the static constructor's body, in a function of its own: a <c>return</c> in it ends the
    /// constructor alone, where it returned from <c>$init()</c> before the slots were handed back, and
    /// its locals meet nothing of <c>$init()</c>'s own, whose names take a <c>$</c> no C# name can take
    /// (a local named <c>slots</c> redeclared the holder, and the module did not load). C# allows no
    /// <c>await</c> and no <c>yield</c> in a static constructor, and it runs once.
    /// </para>
    /// <para>
    /// An initializer or the static constructor that throws leaves the type failed for good: the
    /// exception is kept, wrapped as the TypeInitializationException .NET throws, and that is what
    /// every use of the type throws from then on, the first included. The type initialized nothing
    /// more and answered whatever it held, while .NET throws on every access.
    /// </para>
    /// </summary>
    /// <param name="className">The twin's name, which the members reach the slots through.</param>
    /// <param name="typeName">The type's full name, which the TypeInitializationException's message says.</param>
    /// <param name="statics">The statics, in declaration order.</param>
    /// <param name="constructor">The static constructor's statements; none when it declares none.</param>
    /// <param name="annotate">Whether the module is TypeScript.</param>
    /// <param name="layout">The layout the members are written in, which the static constructor's
    /// function is laid out in too.</param>
    public static IEnumerable<JsClassMember> Members(string className, string typeName, IReadOnlyList<Ordered> statics,
        IReadOnlyList<JsStatement> constructor, bool annotate, JsLayout layout)
    {
        var any = annotate ? ": any" : "";
        yield return JsClassMember.Field("static ", Slots, any, "null");
        yield return JsClassMember.Field("static ", Failure, any, "null");

        var type = JsExpr.Identifier(className);
        var holder = JsExpr.Identifier(Slots);
        var build = new List<JsStatement>();
        build.AddRange(statics.Select(member =>
            JsStatement.Expression(JsExpr.Binary(JsExpr.Member(holder, member.Name), "=", JsExpr.Opaque(member.Zero)))));
        build.AddRange(statics.Where(member => member.Value is not null).Select(member =>
            JsStatement.Expression(JsExpr.Binary(JsExpr.Member(holder, member.Name), "=", JsExpr.Opaque(member.Value!)))
                with { Origin = member.Origin }));
        // The function's block stands inside $init's `if`, and that inside its `try`: three levels in.
        if (constructor.Count > 0)
            build.Add(JsStatement.Expression(JsExpr.Call(JsExpr.ArrowBlock("", JsStatement.Block(constructor), layout, 3))));

        yield return JsClassMember.Method("static ", Init, "", "", any, JsStatement.Block([
            JsStatement.If(JsExpr.Binary(JsExpr.Member(type, Slots), "===", JsExpr.Literal("null")),
                JsStatement.Block([
                    JsStatement.If(JsExpr.Binary(JsExpr.Member(type, Failure), "!==", JsExpr.Literal("null")),
                        JsStatement.Throw(JsExpr.Member(type, Failure)), null),
                    JsStatement.Let(Slots, any, JsExpr.Binary(JsExpr.Member(type, Slots), "=", JsExpr.Object([]))),
                    JsStatement.Try(JsStatement.Block(build), new JsCatch($"({Thrown})", JsStatement.Block([
                        JsStatement.Expression(JsExpr.Binary(JsExpr.Member(type, Slots), "=", JsExpr.Literal("null"))),
                        JsStatement.Throw(JsExpr.Binary(JsExpr.Member(type, Failure), "=",
                            JsExpr.Call(JsExpr.Identifier(Eq.TypeInitialization),
                                JsExpr.Literal(JsStringLiteral.Quote(typeName)), JsExpr.Identifier(Thrown)))),
                    ])), null),
                ]), null),
            JsStatement.Return(JsExpr.Member(type, Slots)),
        ]));

        foreach (var member in statics)
        {
            yield return JsClassMember.Getter("static ", member.Name, annotate ? $": {member.Type}" : "",
                JsStatement.Block([JsStatement.Raw($"return {className}.{Init}().{member.Name};")])) with { Origin = new JsOrigin(member.Origin) };
            yield return JsClassMember.Setter("static ", member.Name, annotate ? $"value: {member.Type}" : "value",
                JsStatement.Block([JsStatement.Raw($"{className}.{Init}().{member.Name} = value;")])) with { Origin = new JsOrigin(member.Origin) };
        }
    }
}
