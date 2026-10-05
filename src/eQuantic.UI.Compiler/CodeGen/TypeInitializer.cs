using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
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

    /// <summary>A static that holds a value of its own: its name on the twin, its declared type, its
    /// initializer, and the declaration it came from (a field's or an event's variable, a property).</summary>
    public readonly record struct Store(string Name, TypeSyntax Type, EqualsValueClauseSyntax? Initializer, SyntaxNode Declaration);

    /// <summary>
    /// The type's statics that hold a value, in DECLARATION order, which is the order C# initializes
    /// them in: every static field but a constant (which C# never initializes, its value being
    /// written where it is read), every static property with a store of its own (an auto-property,
    /// and one that guards its store with <c>field</c>, whose store is named as its accessors name
    /// it), and every static field-like event. The one list the type initializer reads, for a record,
    /// a struct, a class, a static class and a component alike: each emitter collected its own, a
    /// class and a component in two passes, every field and then every property, so
    /// <c>static int Base { get; } = Compute();</c> above <c>static readonly int Doubled = Base * 2;</c>
    /// read Base's zero where .NET reads 21.
    /// </summary>
    public static IEnumerable<Store> Stores(TypeDeclarationSyntax type) => type.Members.SelectMany(StoresOf);

    /// <summary>The stores one member of a type declares (<see cref="Stores"/>): none for a member that
    /// is no static store, one per variable of a field or an event.</summary>
    public static IEnumerable<Store> StoresOf(MemberDeclarationSyntax member)
    {
        if (!member.Modifiers.Any(SyntaxKind.StaticKeyword)) yield break;
        switch (member)
        {
            case BaseFieldDeclarationSyntax field when !field.Modifiers.Any(SyntaxKind.ConstKeyword):
                foreach (var variable in field.Declaration.Variables)
                    yield return new(variable.Identifier.ValueText.ToCamelCase(), field.Declaration.Type, variable.Initializer, variable);
                break;
            case PropertyDeclarationSyntax property when Strategies.Expressions.FieldExpressionStrategy.UsesBackingField(property):
                yield return new(Strategies.Expressions.FieldExpressionStrategy.BackingSlot(property), property.Type,
                    property.Initializer, property);
                break;
            case PropertyDeclarationSyntax { ExpressionBody: null, AccessorList: { } accessors } property
                when !property.Modifiers.Any(SyntaxKind.AbstractKeyword)
                    && accessors.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null):
                yield return new(property.Identifier.ValueText.ToCamelCase(), property.Type, property.Initializer, property);
                break;
        }
    }

    /// <summary>
    /// The type's statics as its initializer runs them (<see cref="Stores"/>), each written by the
    /// emitter: its <paramref name="annotation"/>, its <paramref name="zero"/>, and its initializer's
    /// <paramref name="value"/>.
    /// </summary>
    public static IReadOnlyList<Ordered> Collect(TypeDeclarationSyntax type, Func<TypeSyntax, string> annotation,
        Func<TypeSyntax, string> zero, Func<EqualsValueClauseSyntax, TypeSyntax, string> value) =>
        Stores(type).Select(store => new Ordered(store.Name, annotation(store.Type), zero(store.Type),
            store.Initializer is { } initializer ? value(initializer, store.Type) : null, store.Declaration)).ToList();

    /// <summary>The static constructor a type declares, or null.</summary>
    public static ConstructorDeclarationSyntax? StaticConstructorOf(TypeDeclarationSyntax type) =>
        type.Members.OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword));

    /// <summary>Whether a type declares a static constructor, which C# runs before its first instance
    /// and the first use of any of its static members (<see cref="StartedIn"/>).</summary>
    public static bool HasStaticConstructor(TypeDeclarationSyntax type) => StaticConstructorOf(type) is not null;

    /// <summary>
    /// Whether the type's statics initialize in order: one of them has an initializer that is not a
    /// constant, which may read another static, call a method or build an instance; or one with no
    /// initializer starts at a zero that constructs a twin (<c>static Cell Origin;</c>), which built
    /// while the module is evaluated could meet, through an import cycle, a class not yet defined
    /// (measured: <c>Cannot access 'Alpha' before initialization</c>); or the type has a static
    /// constructor. A constant reads nothing, being written as its value (<see cref="Constant"/>), so a
    /// type of constants keeps its fields.
    /// </summary>
    public static bool Orders(TypeDeclarationSyntax type, Func<SyntaxNode, SemanticModel?> modelFor) =>
        HasStaticConstructor(type)
        || Stores(type).Any(store => store.Initializer is { } initializer
            ? !IsConstant(initializer, modelFor(initializer))
            : Strategies.DefaultValue.Constructs(modelFor(store.Type)?.GetTypeInfo(store.Type).Type));

    private static bool IsConstant(EqualsValueClauseSyntax initializer, SemanticModel? model) =>
        model is not null
            ? ConstantValue(initializer, model) is not null
            : initializer.Value is LiteralExpressionSyntax
                or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax };

    /// <summary>
    /// The value a static's initializer folds to, in the static's own type, the conversion C# applies
    /// included (<c>static long L = 1;</c> is the long 1); null for one it does not fold. A nullable or
    /// a reference conversion holds the constant it converts as it is, as the twin holds it:
    /// <c>static int? N = Default;</c> and <c>static object O = Default;</c> are the constant itself,
    /// which C# does not fold for them, and which their initializer named all the same.
    /// </summary>
    private static IOperation? ConstantValue(EqualsValueClauseSyntax initializer, SemanticModel model)
    {
        if (model.GetOperation(initializer) is not ISymbolInitializerOperation { Value: { } value }) return null;
        while (!value.ConstantValue.HasValue
               && value is IConversionOperation { Operand: { } operand } conversion
               && (conversion.Conversion.IsNullable || conversion.Type is { IsReferenceType: true }))
            value = operand;
        return value.ConstantValue.HasValue ? value : null;
    }

    /// <summary>
    /// A static's initializer as its VALUE, where C# folds it to a constant (a field, a property or a
    /// <c>const</c>), written by the writer of every constant's value (ConstantLiteral): a long is its
    /// BigInt, a decimal the runtime's Decimal, a float the double it is, an enum its representation.
    /// Written as the expression, it named the constants it folds, and an in-source constant read by its
    /// bare name is its twin's static, defined where it is declared: <c>static readonly int Max =
    /// Default * 2;</c> above <c>const int Default = 50;</c> read it before it was defined, and answered
    /// NaN where .NET answers 100. Null for an initializer C# does not fold, which is converted as the
    /// expression it is.
    /// </summary>
    public static string? Constant(EqualsValueClauseSyntax initializer, SemanticModel? model, CSharpToJsConverter converter) =>
        model is not null && ConstantValue(initializer, model) is { } value
            ? converter.ConstantOf(value.ConstantValue.Value, value.Type)
            : null;

    /// <summary>
    /// The members that hold the ordered statics: the slots, the failure, the initializer that builds
    /// them, and an accessor pair per static. The static constructor's body runs after the
    /// initializers.
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
    /// <param name="declaration">The type, whose static constructor runs last and whose full name the
    /// TypeInitializationException's message says.</param>
    /// <param name="className">The twin's name, which the members reach the slots through.</param>
    /// <param name="statics">The statics, in declaration order (<see cref="Collect"/>).</param>
    /// <param name="converter">The emitter's converter, which converts the static constructor's body.</param>
    /// <param name="lowering">The emitter's lowering, which lowers an expression-bodied one.</param>
    /// <param name="annotate">Whether the module is TypeScript.</param>
    /// <param name="layout">The layout the members are written in, which the static constructor's
    /// function is laid out in too.</param>
    public static IReadOnlyList<JsClassMember> Members(TypeDeclarationSyntax declaration, string className,
        IReadOnlyList<Ordered> statics, CSharpToJsConverter converter, MethodLowering lowering, bool annotate, JsLayout layout)
    {
        converter.SetCurrentClass(className);
        var typeName = Parser.ComponentParser.ClrIdentity(declaration);
        var constructor = ConstructorBody(declaration, converter, lowering);
        var any = annotate ? ": any" : "";
        var members = new List<JsClassMember>
        {
            JsClassMember.Field("static ", Slots, any, "null"),
            JsClassMember.Field("static ", Failure, any, "null"),
        };

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

        members.Add(JsClassMember.Method("static ", Init, "", "", any, JsStatement.Block([
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
        ])));

        foreach (var member in statics)
        {
            members.Add(JsClassMember.Getter("static ", member.Name, annotate ? $": {member.Type}" : "",
                JsStatement.Block([JsStatement.Raw($"return {className}.{Init}().{member.Name};")])) with { Origin = new JsOrigin(member.Origin) });
            members.Add(JsClassMember.Setter("static ", member.Name, annotate ? $"value: {member.Type}" : "value",
                JsStatement.Block([JsStatement.Raw($"{className}.{Init}().{member.Name} = value;")])) with { Origin = new JsOrigin(member.Origin) });
        }
        return members;
    }

    /// <summary>The statements of the type's static constructor, converted by the emitter's own
    /// converter, which run after its static initializers; none when it declares none. Nothing emitted
    /// one before #417, so its body never ran.</summary>
    private static IReadOnlyList<JsStatement> ConstructorBody(TypeDeclarationSyntax type, CSharpToJsConverter converter,
        MethodLowering lowering)
    {
        if (StaticConstructorOf(type) is not { } constructor) return [];
        if (constructor.Body is { } block && converter.ConvertBlockIr(block) is JsBlock converted) return converted.Statements;
        return constructor.ExpressionBody is { } arrow ? [lowering.ExpressionBody(arrow.Expression, returns: false)] : [];
    }
}
