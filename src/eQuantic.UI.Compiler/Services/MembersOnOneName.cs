using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// EVERY MEMBER OF A TYPE HOLDS A NAME OF ITS OWN ON THE TWIN, whatever kind of member it is (#1146).
///
/// <para>
/// <see cref="OverloadedMethods"/> refused two methods of one name, and a record's or a struct's state
/// beside a member of its name. Every other pair that lands on one key of the twin, the same lowered
/// name and the same static-ness, passed the build, and the browser kept one of them. Measured on each
/// emitter: a static field beside a static property or method of its name (<c>static int next;</c> and
/// <c>static int Next() =&gt; ++next;</c>), where the field took the method's place when the class was
/// defined and <c>Seq.Next()</c> was "not a function"; a component's field beside its property, whose
/// instance field hid the getter (<c>Label =&gt; label.ToUpper()</c> answered "x"); a property beside a
/// method a case apart, an event beside either, an extension block's property or indexer beside a method
/// of the static class, two <c>operator +</c> of one type, a conversion beside a method of its name
/// (<c>FromInt</c>), and members on the names the twin writes itself (a record's <c>With(int)</c>, a
/// class's <c>ToString(string)</c>, a <c>Constructor()</c>, which no module parses). TypeScript refuses
/// most of them as duplicate identifiers, but only where a twin is type-checked, which is the runtime's
/// own: an app's module is bundled unchecked.
/// </para>
///
/// <para>
/// So every member the twin holds is put on its key, as the emitters write it: a field in its slot
/// (<see cref="FieldSlotExtensions.TwinSlot"/>, which moves a plain class's field out of a member's way,
/// #396), a method under <see cref="TwinMethodName"/> (a method that hides a member holds a name of its
/// own, #563), a property, an event and a held primary constructor's parameter camelCased, an operator
/// and a conversion under the static the twin calls, an extension block's members as statics of the
/// class. The two accessors of a property are one member, and so are the two halves of a partial property
/// or event, a positional parameter its record redeclares (#546), and a component's parameter that only
/// initializes the member of its name. The second member on a key is
/// refused, naming the first. An explicit interface implementation is left out, as the overload check
/// leaves it out: its name is the interface's (#1139).
/// </para>
///
/// <para>
/// A member on a name the twin holds itself is refused the same way: a class's <c>constructor</c> and its
/// static <c>prototype</c>, which JavaScript gives every class; a record's or a struct's <c>with</c>; and
/// <c>equals</c>, <c>getHashCode</c>, <c>toString</c> and <c>toJSON</c>, which the runtime and JavaScript
/// ask a value wherever .NET calls <c>Equals(object)</c>, <c>GetHashCode()</c>, <c>ToString()</c> and
/// never a <c>ToJSON</c>. An override of one of object's three is the member .NET calls there, and the
/// twin writes it in place of its own (<see cref="TwinOwnMembers"/>), so it stands. A component's twin
/// is asked those names as any value is, and its <c>constructor</c> is its runtime base's, which EQ2011
/// holds with the base's other names.
/// </para>
/// </summary>
internal static class MembersOnOneName
{
    /// <summary>A member of the twin: the key it is written under, how a message shows it, where it is
    /// declared, whether it is a value the twin holds (a field, an auto-property, a field-like event, a
    /// held parameter) rather than an accessor or a method, whether it is an operator or a conversion,
    /// whose name the author cannot change, and the method it is, for the overrides that stand on a name
    /// the twin holds.</summary>
    private readonly record struct Member(string Lowered, bool Static, string Shown, SyntaxToken At, bool Held,
        bool Operator = false, MethodDeclarationSyntax? Method = null);

    /// <param name="type">The declaration whose twin is being written.</param>
    /// <param name="sourcePath">Where it lives, for the error.</param>
    /// <param name="isComponent">Whether the twin is a component's, whose <c>[ServerAction]</c> crosses as a
    /// stub of its name, whose instance holds every parameter of its primary constructor, and whose runtime
    /// names are EQ2011's.</param>
    /// <param name="model">The model of the declaration's tree: it says where a field's slot moved, which
    /// method hides a member, and which primary constructor's parameter an instance holds.</param>
    /// <param name="errors">Where the refusal goes: the first member that lands on a taken key.</param>
    public static void Check(TypeDeclarationSyntax type, string sourcePath, bool isComponent, SemanticModel? model,
        List<CompilationError> errors)
    {
        var allStatic = type.Modifiers.Any(SyntaxKind.StaticKeyword);
        var first = new Dictionary<(bool Static, string Lowered), Member>();
        foreach (var member in Members(type, allStatic, isComponent, model).OrderBy(member => member.At.SpanStart))
        {
            if (TwinHolds(type, allStatic, isComponent, member.Lowered, member.Static, member.Method, model) is { } held)
            {
                errors.Add(Refusal(sourcePath, member.At,
                    $"'{member.Shown}' lowers to `{(member.Static ? "static " : "")}{member.Lowered}`, {held}"));
                return;
            }
            if (first.TryAdd((member.Static, member.Lowered), member)) continue;
            var earlier = first[(member.Static, member.Lowered)];
            errors.Add(Refusal(sourcePath, member.At,
                Clash(member, earlier, earlier.At.GetLocation().GetLineSpan().StartLinePosition.Line + 1)));
            return;
        }
    }

    /// <summary>The refusal of two members on one key, naming both, and the one to rename where only one
    /// of them can be.</summary>
    private static string Clash(Member later, Member earlier, int earlierLine)
    {
        var both = $"'{later.Shown}' lowers to `{(later.Static ? "static " : "")}{later.Lowered}`, and so does "
            + $"'{earlier.Shown}' (line {earlierLine}). ";
        if (later.Operator && earlier.Operator)
            return both + "C# tells the two apart by their operands, and the twin names an operator by its symbol and a "
                + "conversion by the other type, one static member per name, so it would keep one of them and every use "
                + "of the other would reach it. Keep one of them, and write the other as a method of its own name.";
        var where = later.Static ? "on the class itself, and is defined over the other when the class is"
            : "on each instance, where it hides the other";
        var why = (later.Held, earlier.Held) switch
        {
            (true, true) => "both are held in one slot, so the two would share one value. ",
            (true, false) => $"'{later.Shown}' is held {where}, so every access to '{earlier.Shown}' would reach it. ",
            (false, true) => $"'{earlier.Shown}' is held {where}, so every access to '{later.Shown}' would reach it. ",
            _ => "only one of them would stand, and every access to the other would reach it. ",
        };
        var renamed = later.Operator ? earlier.Shown : earlier.Operator ? later.Shown : null;
        return both + $"A JavaScript class holds one {(later.Static ? "static " : "")}member per name: " + why
            + (renamed is null ? "Rename one of them." : $"Rename '{renamed}'.");
    }

    /// <summary>
    /// Why a default <paramref name="type"/> takes from an interface, which its twin holds as a member of
    /// its own (<see cref="DefaultInterfaceMembers"/>), cannot hold <paramref name="lowered"/>, or null where
    /// it may: a default <c>Constructor()</c> was a second constructor, and the module did not parse.
    /// </summary>
    internal static string? ADefaultOn(TypeDeclarationSyntax type, bool isComponent, string lowered, SemanticModel? model) =>
        lowered == "constructor"
            // EQ2011 reads what a component declares, never a default it takes: a component's twin wrote a
            // default `Constructor()` over its own constructor, which then never called its base's.
            ? TwinHolds(type, allStatic: false, isComponent: false, lowered, isStatic: false, method: null, model)
            : TwinHolds(type, type.Modifiers.Any(SyntaxKind.StaticKeyword), isComponent, lowered, isStatic: false, method: null, model);

    /// <summary>
    /// Why a member lowered to <paramref name="lowered"/> cannot hold that name, since the twin holds it
    /// itself, or null where it may: a class's own <c>constructor</c> and static <c>prototype</c>, a value's
    /// <c>with</c>, and the names the runtime asks a value for its equality, its hash, its text and its
    /// JSON, where only <paramref name="method"/>'s override of object's member stands. A component's
    /// <c>constructor</c> is its runtime base's, which EQ2011 holds with the base's other names, and a
    /// component's twin writes none of the four itself.
    /// </summary>
    private static string? TwinHolds(TypeDeclarationSyntax type, bool allStatic, bool isComponent, string lowered,
        bool isStatic, MethodDeclarationSyntax? method, SemanticModel? model)
    {
        if (isStatic)
            return lowered == "prototype"
                ? "which JavaScript gives every class itself and refuses to a static member, so the module would not load. "
                    + "Give it a name of its own."
                : null;
        if (lowered == "constructor")
            return isComponent ? null : "the name of the class's own constructor, which the twin writes: a method of that name would be a second "
                + "constructor and a field or an accessor of it is refused, so the module would not load, and a record's "
                + "property of it would replace what each value says its class is. Give it a name of its own.";
        if (allStatic) return null;
        string Unless(string overridden, string what) =>
            method is not null && TwinOwnMembers.Overrides(method, overridden, model)
                ? ""
                : what + $" Only an override of `{overridden}({(overridden == nameof(Equals) ? "object" : "")})` takes "
                    + "that place. Give this one a name of its own.";
        var held = lowered switch
        {
            "with" when type is RecordDeclarationSyntax or StructDeclarationSyntax =>
                $"the member the twin of a record or a struct writes for C#'s `with` expression, so every `with` on "
                + $"'{type.Identifier.ValueText}' would reach it, or it would never be reached. Give it a name of its own.",
            "equals" => Unless(nameof(Equals),
                "the member the runtime asks for a value's equality wherever .NET calls `Equals(object)`, a dictionary's "
                + "keys and `==` on a record included, and which the twin of a record or a struct writes itself, so it "
                + "would answer there."),
            "getHashCode" => Unless(nameof(GetHashCode),
                "the member the runtime asks for a value's hash wherever .NET calls `GetHashCode()`, a dictionary's and a "
                + "set's included, and which the twin of a record or a struct writes itself, so it would answer there."),
            "toString" => Unless(nameof(ToString),
                "the member JavaScript asks for a value's text wherever .NET calls `ToString()`, a concatenation and an "
                + $"interpolation included, {(isComponent ? "" : "and which the twin writes itself, ")}so it would answer there."),
            "toJSON" =>
                "the member JSON serialization calls on the twin, "
                + (isComponent ? "" : "which writes one itself where a member keeps a store, and ")
                + "which .NET's serializer never calls, so a server action would receive what it returns where .NET "
                + "sends the members. Give it a name of its own.",
            _ => null,
        };
        return string.IsNullOrEmpty(held) ? null : held;
    }

    /// <summary>
    /// The members the twin holds under a name, each on its key (see the type's remarks). An instance
    /// indexer is <see cref="OverloadedMethods"/>' to judge, which knows the names it takes beside an
    /// explicit implementation of an interface's, and so is an explicit implementation. A record's
    /// positional parameter is its property, unless a member of the body declares its name, and a class's
    /// or a struct's is held only where a member reads it, and is that member where one declares its name.
    /// A component's is held whether or not a member reads it: its constructor writes every parameter it is
    /// handed onto the instance. One that only initializes the member of its name is that member.
    /// </summary>
    private static IEnumerable<Member> Members(TypeDeclarationSyntax type, bool allStatic, bool isComponent, SemanticModel? model)
    {
        var name = type.Identifier.ValueText;
        var ownModel = model?.SyntaxTree == type.SyntaxTree ? model : null;
        bool IsStatic(MemberDeclarationSyntax member) =>
            allStatic || member.Modifiers.Any(SyntaxKind.StaticKeyword) || member.Modifiers.Any(SyntaxKind.ConstKeyword);

        var declared = type.Members.SelectMany(DeclaredNames).ToHashSet(StringComparer.Ordinal);
        foreach (var parameter in type.ParameterList?.Parameters ?? default)
        {
            var held = parameter.Identifier.ValueText;
            if ((!isComponent && declared.Contains(held)) || !Holds(type, parameter, isComponent, model)) continue;
            yield return new(held.ToCamelCase(), false, $"{name}({held})", parameter.Identifier, Held: true);
        }

        foreach (var member in type.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                        yield return new(ownModel?.GetDeclaredSymbol(variable) is IFieldSymbol symbol
                                ? symbol.TwinSlot()
                                : variable.Identifier.ValueText.ToCamelCase(),
                            IsStatic(field), $"{name}.{variable.Identifier.ValueText}", variable.Identifier, Held: true);
                    break;

                // The defining half of a partial event is the event its implementing half declares.
                case EventFieldDeclarationSyntax handler when !handler.Modifiers.Any(SyntaxKind.PartialKeyword):
                    foreach (var variable in handler.Declaration.Variables)
                        yield return new(variable.Identifier.ValueText.ToCamelCase(), IsStatic(handler),
                            $"{name}.{variable.Identifier.ValueText}", variable.Identifier, Held: true);
                    break;

                case EventDeclarationSyntax { ExplicitInterfaceSpecifier: null } handler:
                    yield return new(handler.Identifier.ValueText.ToCamelCase(), IsStatic(handler),
                        $"{name}.{handler.Identifier.ValueText}", handler.Identifier, Held: false);
                    break;

                case PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: null } property
                    when !DefinesAPartial(property, ownModel):
                    yield return new(property.Identifier.ValueText.ToCamelCase(), IsStatic(property),
                        $"{name}.{property.Identifier.ValueText}", property.Identifier, Held: PropertyStore.IsAuto(property));
                    break;

                case MethodDeclarationSyntax method when OverloadedMethods.TakesAName(method, isComponent, ownModel):
                    yield return new(TwinMethodName.Of(method, ownModel), IsStatic(method),
                        $"{name}.{OverloadedMethods.Signature(method)}", method.Identifier, Held: false, Method: method);
                    break;

                case OperatorDeclarationSyntax op when !Parser.ComponentParser.StaysOnTheServer(op, inAComponent: false, ownModel)
                                                       && OperatorName(op) is { } lowered:
                    yield return new(lowered, true, $"{name}.operator {op.OperatorToken.Text}({Types(op.ParameterList)})",
                        op.OperatorToken, Held: false, Operator: true);
                    break;

                case ConversionOperatorDeclarationSyntax conversion
                    when !Parser.ComponentParser.StaysOnTheServer(conversion, inAComponent: false, ownModel):
                    yield return new(ConversionName(conversion, name, ownModel), true,
                        $"{name}.{conversion.ImplicitOrExplicitKeyword.Text} operator {conversion.Type}({Types(conversion.ParameterList)})",
                        conversion.ImplicitOrExplicitKeyword, Held: false, Operator: true);
                    break;

                case ExtensionBlockDeclarationSyntax block:
                    var receiver = $"{name}.extension({block.ParameterList?.Parameters.FirstOrDefault()?.Type})";
                    foreach (var extension in block.Members)
                    {
                        switch (extension)
                        {
                            case PropertyDeclarationSyntax property:
                                yield return new(property.Identifier.ValueText.ToCamelCase(), true,
                                    $"{receiver}.{property.Identifier.ValueText}", property.Identifier, Held: false);
                                break;
                            case IndexerDeclarationSyntax indexer:
                                yield return new(Indexer.Get, true, $"{receiver}.this[{Types(indexer.ParameterList)}]",
                                    indexer.ThisKeyword, Held: false);
                                break;
                            case MethodDeclarationSyntax method when OverloadedMethods.TakesAName(method, isComponent: false, ownModel):
                                yield return new(TwinMethodName.Of(method, ownModel), true,
                                    $"{receiver}.{OverloadedMethods.Signature(method)}", method.Identifier, Held: false, Method: method);
                                break;
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>The defining half of a partial property, which is the property its implementing half
    /// declares, as a partial method's two halves are one method: asked of the model, and of the syntax
    /// where there is none, the half whose accessors have no body.</summary>
    private static bool DefinesAPartial(PropertyDeclarationSyntax property, SemanticModel? model) =>
        model?.GetDeclaredSymbol(property) is IPropertySymbol symbol
            ? symbol.IsPartialDefinition
            : property.Modifiers.Any(SyntaxKind.PartialKeyword) && PropertyStore.IsAuto(property);

    /// <summary>
    /// Whether the twin holds a primary constructor's parameter on each instance as a member of its own,
    /// which every pass that puts the twin's members on their keys asks: a record's as its property, a
    /// class's or a struct's where a member reads it (<see cref="TypeDeclarationExtensions.HoldsParameter"/>),
    /// and a component's whether or not one does, since its constructor writes every parameter it is handed
    /// onto the instance, beside a method and an indexer's <c>item</c> alike. A component's parameter that
    /// only initializes the member of its name is that member.
    /// </summary>
    internal static bool Holds(TypeDeclarationSyntax type, ParameterSyntax parameter, bool isComponent, SemanticModel? model) =>
        isComponent ? !OnlyInitializesItsNamesake(type, parameter, model) : type.HoldsParameter(parameter, model);

    /// <summary>
    /// Whether a component's parameter is read nowhere but as the whole initializer of the property or the
    /// field of its name, whatever its case and in any parentheses (<c>PolicyHref { get; init; } = policyHref</c>,
    /// the C# 12 idiom), an auto-property's or a field's, with no conversion between the two: the twin's
    /// constructor writes the parameter into that member's
    /// slot, and the initializer, which runs only where the slot is still empty, would have written the same
    /// value, so the two are one member. Any other initializer is skipped there (<c>= policyHref?.Trim()</c>
    /// held the untrimmed value, and <c>long Count = count</c> an int where every read takes a long), and
    /// any other read sees the slot after a prop wrote it.
    /// </summary>
    private static bool OnlyInitializesItsNamesake(TypeDeclarationSyntax type, ParameterSyntax parameter, SemanticModel? model)
    {
        model = model?.SyntaxTree == parameter.SyntaxTree ? model : null;
        var name = parameter.Identifier.ValueText;
        var symbol = model?.GetDeclaredSymbol(parameter);
        var reads = type.Members.SelectMany(member => member.DescendantNodes().OfType<IdentifierNameSyntax>())
            .Where(identifier => identifier.Identifier.ValueText == name
                && (symbol is null || SymbolEqualityComparer.Default.Equals(model!.GetSymbolInfo(identifier).Symbol, symbol)))
            .Take(2)
            .ToList();
        if (reads is not [ExpressionSyntax whole]) return false;
        while (whole.Parent is ParenthesizedExpressionSyntax parenthesized) whole = parenthesized;
        if (whole.Parent is not EqualsValueClauseSyntax initializer) return false;
        var key = name.ToCamelCase();
        var (member, memberType) = initializer.Parent switch
        {
            // A property with an accessor of its own is written through it: the constructor's write ran its
            // setter (`init => field = value * 2` held 6 for 3), where C# initializes the store.
            PropertyDeclarationSyntax property when PropertyStore.IsAuto(property) => (property.Identifier.ValueText, property.Type),
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax } declaration } variable =>
                (variable.Identifier.ValueText, declaration.Type),
            _ => (null, null),
        };
        if (member?.ToCamelCase() != key) return false;
        // The same value only where the initializer converts nothing, asked of the bound tree, and of the
        // types as written where there is none.
        return model is not null
            ? model.GetConversion(initializer.Value).IsIdentity
            : parameter.Type?.ValueText() == memberType?.ValueText();
    }

    /// <summary>The C# names a member of the body declares, which a primary constructor's parameter of
    /// the same name is one with.</summary>
    private static IEnumerable<string> DeclaredNames(MemberDeclarationSyntax member) => member switch
    {
        BaseFieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
        PropertyDeclarationSyntax property => [property.Identifier.ValueText],
        EventDeclarationSyntax handler => [handler.Identifier.ValueText],
        MethodDeclarationSyntax method => [method.Identifier.ValueText],
        _ => [],
    };

    /// <summary>The static an operator is written as: a unary one named by what it does, a binary one by
    /// its symbol (<see cref="RecordTypeEmitter.OperatorMethodName"/>), and none for one with no lowering.</summary>
    private static string? OperatorName(OperatorDeclarationSyntax op) =>
        op.ParameterList.Parameters.Count == 1
            ? RecordTypeEmitter.UnaryOperatorMethodName(op.OperatorToken.Text)
            : RecordTypeEmitter.OperatorMethodName(op.OperatorToken.Text);

    /// <summary>The static a conversion is written as, from its symbol as every call site names it, and
    /// from its syntax where there is no model, as the emitters do.</summary>
    private static string ConversionName(ConversionOperatorDeclarationSyntax conversion, string type, SemanticModel? model)
    {
        if (model?.GetDeclaredSymbol(conversion) is IMethodSymbol symbol) return RecordTypeEmitter.ConversionNameFor(symbol);
        var toSelf = conversion.Type.ValueText() == type;
        return RecordTypeEmitter.ConversionMethodName(
            toSelf ? conversion.ParameterList.Parameters[0].Type!.ValueText() : conversion.Type.ValueText(), from: toSelf);
    }

    /// <summary>A parameter list's types, as a message names an operator or an indexer.</summary>
    private static string Types(BaseParameterListSyntax parameters) =>
        string.Join(", ", parameters.Parameters.Select(parameter => parameter.Type?.ValueText() ?? parameter.Identifier.ValueText));

    private static CompilationError Refusal(string sourcePath, SyntaxToken at, string message)
    {
        var position = at.GetLocation().GetLineSpan().StartLinePosition;
        return new CompilationError
        {
            Code = "EQ1007",
            Message = message,
            SourcePath = sourcePath,
            Line = position.Line + 1,
            Column = position.Character + 1,
        };
    }
}
