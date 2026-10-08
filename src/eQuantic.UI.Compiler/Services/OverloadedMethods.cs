using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
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
/// hold, and an override is the method it overrides rather than a second one. A method that HIDES an
/// inherited member holds a name of its own on the twin (<see cref="TwinMethodName"/>, #563), so it
/// takes no other's: what is compared is the name each one holds there.
/// </para>
///
/// <para>
/// One DECLARATION is what a twin is written from: eqc does not merge a type's partial halves, one
/// file holding two being EQ2009, and across files every half after the first is dropped and
/// reported (<c>EmittedTwins.Divided</c>). So an overload in another half never reaches a twin to
/// collide in. When partial types merge, this walks every declaration of the symbol.
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
        CheckOne(type, sourcePath, isComponent, model, errors);
        if (errors.Count == 0) CheckState(type, sourcePath, model, errors);
        if (errors.Count == 0 && model is not null) CheckInherited(type, sourcePath, isComponent, model, errors);
        if (errors.Count == 0 && model is not null) CheckHidingImplementations(type, sourcePath, model, errors);
        if (errors.Count == 0 && model is not null) CheckDefaults(type, sourcePath, isComponent, model, errors);
        if (errors.Count == 0 && model is not null) CheckInterfaceIndexers(type, sourcePath, model, errors);
        if (isComponent)
        {
            foreach (var nested in type.Members.OfType<ClassDeclarationSyntax>()
                         .Where(nested => nested.Modifiers.Any(SyntaxKind.StaticKeyword)))
            {
                CheckOne(nested, sourcePath, isComponent: false, model, errors);
            }
        }
        return errors;
    }

    /// <summary>What holds a name on the twin, which says what can be renamed: a method, an indexer, an
    /// explicit implementation of an interface's indexer, or a member the twin holds on each instance.</summary>
    private enum Bearer { Method, Indexer, ExplicitIndexer, Member }

    /// <summary>A name the twin gives a member: whether it is static there, how a message shows the
    /// member, where it is declared, and what holds it.</summary>
    private readonly record struct Name(string Lowered, bool Static, string Shown, SyntaxToken At, Bearer Bearer);

    private static void CheckOne(TypeDeclarationSyntax type, string sourcePath, bool isComponent, SemanticModel? model,
        List<CompilationError> errors)
    {
        var allStatic = type.Modifiers.Any(SyntaxKind.StaticKeyword);
        var first = new Dictionary<(bool Static, string Lowered), Name>();
        foreach (var name in Names(type, allStatic, isComponent, model).OrderBy(name => name.At.SpanStart))
        {
            var key = (name.Static, name.Lowered);
            if (!first.TryGetValue(key, out var earlier))
            {
                first[key] = name;
                continue;
            }
            var position = name.At.GetLocation().GetLineSpan().StartLinePosition;
            var earlierLine = earlier.At.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                Message = Message(type.Identifier.Text, name, earlier, earlierLine),
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
        }
    }

    /// <summary>The refusal of two members on one name, naming both and the one the author can rename.</summary>
    private static string Message(string type, Name later, Name earlier, int earlierLine)
    {
        var lowered = $"`{(later.Static ? "static " : "")}{later.Lowered}{(later.Bearer == Bearer.Member ? "" : "()")}`";
        var both = $"'{type}.{later.Shown}' lowers to {lowered}, and so does '{earlier.Shown}' (line {earlierLine}). ";
        Name? Held(Bearer bearer) => later.Bearer == bearer ? later : earlier.Bearer == bearer ? earlier : null;
        if (Held(Bearer.Member) is { } member)
            return both + $"The twin holds '{member.Shown}' on each instance, over the indexer's `{member.Lowered}` "
                + $"method, so every access to the indexer would reach '{member.Shown}' instead. Rename '{member.Shown}'.";
        if (Held(Bearer.Method) is { } method && (later.Bearer != Bearer.Method || earlier.Bearer != Bearer.Method))
            return both + "A JavaScript class has one member per name, so every access to the indexer would reach the "
                + $"method, or every call to the method the indexer. Rename '{method.Shown}'.";
        if (Held(Bearer.ExplicitIndexer) is not null)
            return both + "An explicit implementation answers its interface through the twin's "
                + $"`{later.Lowered}`, and a JavaScript class has one member per name, so one of them would answer the "
                + "other's accesses. Implement one of them implicitly, or keep one indexer.";
        return both + "C# tells overloads apart by their parameters, and a JavaScript class has one member per name, so "
            + "the twin would keep one of them and every call would reach it. Give each its own name, or keep one indexer.";
    }

    /// <summary>
    /// The names a type's twin gives its methods and its indexers, with whether each is static there,
    /// how a message shows it, where it is declared, and what holds it. An indexer takes two: its getter
    /// is the twin's <c>item</c> and its setter its <c>setItem</c> (#427), unless it is the type's own
    /// beside an explicit implementation of an interface's indexer, which holds those
    /// (<see cref="Indexer.NamesOf(IPropertySymbol)"/>). So a second indexer, or a method named
    /// <c>Item</c> or <c>SetItem</c> beside one, would land on the same member, and so would any member
    /// the twin holds on each instance under those names: a field, a property, an event, a primary
    /// constructor's parameter. Each shadowed the indexer's method, so an access reached the member
    /// (<c>class Box(int item)</c> answered the text of the function).
    /// </summary>
    private static IEnumerable<Name> Names(TypeDeclarationSyntax type, bool allStatic, bool isComponent, SemanticModel? model)
    {
        // Each by the name it holds on the twin: one that hides an inherited member holds its own (#563).
        foreach (var (method, isStatic) in Methods(type, allStatic, isComponent))
            yield return new(TwinMethodName.Of(method, model?.SyntaxTree == method.SyntaxTree ? model : null), isStatic,
                Signature(method), method.Identifier, Bearer.Method);

        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var indexer in type.Members.OfType<IndexerDeclarationSyntax>())
        {
            var (get, set) = Indexer.NamesOf(indexer, model);
            var bearer = indexer.ExplicitInterfaceSpecifier is null ? Bearer.Indexer : Bearer.ExplicitIndexer;
            var keys = string.Join(", ", indexer.ParameterList.Parameters.Select(parameter => parameter.Type?.ToString() ?? parameter.Identifier.Text));
            var shown = indexer.ExplicitInterfaceSpecifier is { } face ? $"{face.Name}.this[{keys}]" : $"this[{keys}]";
            if (indexer.ExpressionBody is not null
                || indexer.AccessorList?.Accessors.Any(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) == true)
            {
                taken.Add(get);
                yield return new(get, false, shown, indexer.ThisKeyword, bearer);
            }
            if (indexer.AccessorList?.Accessors.Any(accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.InitAccessorDeclaration)) == true)
            {
                taken.Add(set);
                yield return new(set, false, shown + "'s setter", indexer.ThisKeyword, bearer);
            }
        }
        if (taken.Count == 0 || allStatic) yield break;

        foreach (var (name, shown, at) in HeldOnEachInstance(type, model))
            if (taken.Contains(name.ToCamelCase()))
                yield return new(name.ToCamelCase(), false, shown, at, Bearer.Member);
    }

    /// <summary>
    /// A record's or a struct's instance members that land on one name on its twin. The twin holds the
    /// state of each instance as properties of its own, and its methods and computed properties on its
    /// prototype, one member per name, while C# tells names apart by case: a positional <c>X</c> beside a
    /// field <c>x</c>, a primary constructor's <c>x</c> captured beside a property <c>X</c>, the private
    /// field <c>celsius</c> beside <c>Celsius =&gt; celsius</c>. Two states shared one slot
    /// (<c>struct S(int x) { public int X { get; } = x * 2; }</c> answered "6|6" for "3|6"), and a state
    /// beside an accessor or a method of its name was written over it by the constructor, which threw
    /// at <c>new</c>. A member the body declares under a positional parameter's own name is that
    /// parameter's property (#546), one member, and two methods of one name are overloads, which
    /// <see cref="CheckOne"/> judges. A plain class keeps its state in class fields, which shadow the
    /// prototype's getter of the same name, so its backing-field idiom answers right and is left as it is.
    /// </summary>
    private static void CheckState(TypeDeclarationSyntax type, string sourcePath, SemanticModel? model, List<CompilationError> errors)
    {
        if (CheckPrimaryParameters(type, sourcePath, errors)) return;
        if (type is ClassDeclarationSyntax)
        {
            CheckHeldParameters(type, sourcePath, model, errors);
            return;
        }
        if (type is not (RecordDeclarationSyntax or StructDeclarationSyntax)) return;
        var instance = HeldOnEachInstance(type, model)
            .Concat(type.Members.OfType<MethodDeclarationSyntax>()
                .Where(method => !method.Modifiers.Any(SyntaxKind.StaticKeyword) && method.ExplicitInterfaceSpecifier is null)
                .Select(method => (Name: method.Identifier.ValueText, Shown: Signature(method), At: method.Identifier)));
        var first = new Dictionary<string, (string Name, string Shown, SyntaxToken At)>(StringComparer.Ordinal);
        foreach (var member in instance.OrderBy(member => member.At.SpanStart))
        {
            var lowered = member.Name.ToCamelCase();
            if (!first.TryGetValue(lowered, out var earlier))
            {
                first[lowered] = member;
                continue;
            }
            if (earlier.Name == member.Name) continue;
            var position = member.At.GetLocation().GetLineSpan().StartLinePosition;
            var earlierLine = earlier.At.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                Message = $"'{type.Identifier.Text}.{member.Shown}' lowers to `{lowered}`, and so does '{earlier.Shown}' "
                    + $"(line {earlierLine}). The twin of a record or a struct holds its state on each instance and its methods "
                    + "and computed properties on its prototype, one member per name, so the two would share one value, or "
                    + $"the constructor would write one over the other. Rename one of them (a private field `_{lowered}`, "
                    + "or a primary constructor's parameter by what it holds).",
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
            return;
        }
    }

    /// <summary>
    /// Two parameters of a primary constructor that land on one name (`x` beside `X`): the twin's
    /// constructor names each one camelCased, as the member a record makes of it, and a JavaScript
    /// function refuses two parameters of one name, so the module did not load, whether a member read
    /// them or not (found by Copilot's review of #621). A class's, a record's and a struct's alike.
    /// </summary>
    private static bool CheckPrimaryParameters(TypeDeclarationSyntax type, string sourcePath, List<CompilationError> errors)
    {
        if (type.ParameterList is not { Parameters.Count: > 1 } list) return false;
        var first = new Dictionary<string, ParameterSyntax>(StringComparer.Ordinal);
        foreach (var parameter in list.Parameters)
        {
            var lowered = CodeGen.TwinConstructor.ParameterName(parameter, primary: true);
            if (!first.TryGetValue(lowered, out var earlier))
            {
                first[lowered] = parameter;
                continue;
            }
            var position = parameter.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                Message = $"'{type.Identifier.Text}({parameter.Identifier.ValueText})' lowers to `{lowered}`, and so does "
                    + $"'{type.Identifier.Text}({earlier.Identifier.ValueText})'. The twin's constructor names a primary "
                    + "constructor's parameters camelCased, and a function cannot take two parameters of one name. Rename one "
                    + "of them by what it holds.",
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
            return true;
        }
        return false;
    }

    /// <summary>
    /// A class's primary constructor parameter that a member reads, which the twin holds on each
    /// instance under its camelCased name as a struct's (#583), beside a member that lands on the same
    /// name: `class Greeter(string name) { public string Name => name.ToUpper(); }` held "ada" where the
    /// getter `name` stands, and the instance's own value hid the getter, so `Name` answered "ada" where
    /// C# answers "ADA". A class's members among themselves are not judged here: its fields are class
    /// state its own accessors read through, and #396 is theirs.
    /// </summary>
    private static void CheckHeldParameters(TypeDeclarationSyntax type, string sourcePath, SemanticModel? model,
        List<CompilationError> errors)
    {
        if (type.ParameterList is null) return;
        var held = type.ParameterList.Parameters.Where(parameter => type.HoldsParameter(parameter, model)).ToList();
        if (held.Count == 0) return;
        var members = HeldOnEachInstance(type, model).Where(member => member.At.Parent is not ParameterSyntax)
            .Concat(type.Members.OfType<MethodDeclarationSyntax>()
                .Where(method => !method.Modifiers.Any(SyntaxKind.StaticKeyword) && method.ExplicitInterfaceSpecifier is null)
                .Select(method => (Name: method.Identifier.ValueText, Shown: Signature(method), At: method.Identifier)))
            .ToList();
        foreach (var parameter in held)
        {
            var name = parameter.Identifier.ValueText;
            var lowered = name.ToCamelCase();
            if (members.FirstOrDefault(member => member.Name != name && member.Name.ToCamelCase() == lowered) is not { Name: not null } other)
                continue;
            var position = parameter.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            var otherLine = other.At.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                Message = $"'{type.Identifier.Text}({name})' lowers to `{lowered}`, and so does '{other.Shown}' (line {otherLine}). "
                    + "A member reads the primary constructor's parameter, so the twin holds it on each instance under that "
                    + "name, where the member of the same name stands, and one would hide the other. Rename the parameter by "
                    + $"what it holds, or hold it in a field of your own (`private readonly {parameter.Type} _{lowered} = {name};`).",
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
            return;
        }
    }

    /// <summary>The members besides its methods a twin holds on each instance, by their C# names: its
    /// fields, properties and events, and a primary constructor's parameters the instance holds (a
    /// record's being its properties, a class's or a struct's those a member reads,
    /// <see cref="TypeDeclarationExtensions.HoldsParameter"/>).</summary>
    private static IEnumerable<(string Name, string Shown, SyntaxToken At)> HeldOnEachInstance(TypeDeclarationSyntax type,
        SemanticModel? model)
    {
        foreach (var parameter in type.ParameterList?.Parameters ?? default)
            if (type.HoldsParameter(parameter, model))
                yield return (parameter.Identifier.ValueText, $"{type.Identifier.Text}({parameter.Identifier.ValueText})", parameter.Identifier);
        foreach (var member in type.Members)
        {
            if (member.Modifiers.Any(SyntaxKind.StaticKeyword) || member.Modifiers.Any(SyntaxKind.ConstKeyword)) continue;
            switch (member)
            {
                case PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: null } property:
                    yield return (property.Identifier.ValueText, property.Identifier.ValueText, property.Identifier);
                    break;
                case EventDeclarationSyntax { ExplicitInterfaceSpecifier: null } handler:
                    yield return (handler.Identifier.ValueText, handler.Identifier.ValueText, handler.Identifier);
                    break;
                case BaseFieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                        yield return (variable.Identifier.ValueText, variable.Identifier.ValueText, variable.Identifier);
                    break;
            }
        }
    }

    /// <summary>
    /// A type that implements an interface's indexer explicitly, which every access through an
    /// interface reaches as the twin's <c>item</c>, and answers ANOTHER interface's indexer with an
    /// indexer of its own or with a default it takes: an access through that interface reaches
    /// <c>item</c> too, so one of the two would answer the other's accesses. Refused, as a name the twin
    /// cannot hold twice is.
    /// </summary>
    private static void CheckInterfaceIndexers(TypeDeclarationSyntax type, string sourcePath, SemanticModel model,
        List<CompilationError> errors)
    {
        if (model.SyntaxTree != type.SyntaxTree || model.GetDeclaredSymbol(type) is not INamedTypeSymbol declared) return;
        if (declared.GetMembers().OfType<IPropertySymbol>()
                .FirstOrDefault(member => member.IsIndexer && !member.ExplicitInterfaceImplementations.IsEmpty) is not { } held)
            return;
        foreach (var contract in declared.AllInterfaces.SelectMany(face => face.GetMembers().OfType<IPropertySymbol>())
                     .Where(member => member.IsIndexer))
        {
            if (declared.FindImplementationForInterfaceMember(contract) is not IPropertySymbol answer
                || !answer.ExplicitInterfaceImplementations.IsEmpty)
                continue;
            var what = answer.ContainingType.TypeKind == TypeKind.Interface
                ? $"the default '{answer.ContainingType.Name}.this[]', which it takes"
                : $"'{declared.Name}.this[]', which answers '{contract.ContainingType.Name}.this[]'";
            var position = type.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                Message = $"'{declared.Name}' implements '{Shown(held)}' explicitly, which an access through its interface "
                    + $"reaches as the twin's `{Indexer.Get}()`, and so does {what}: every access through an interface "
                    + $"reaches `{Indexer.Get}()`, so one of them would answer the other's. Answer both interfaces with "
                    + "one indexer, or implement one of them implicitly.",
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
            return;
        }
    }

    /// <summary>
    /// Defaults the type takes from its interfaces (#414) that share a name with anything else along
    /// its twin's chain. The twin of the class that first takes a default holds it as a member of its
    /// own, one per name like any other, while C# reaches it only through its interface, so a second
    /// member on its name answers in its place, or loses to it. In one class that is two defaults from
    /// different interfaces, or a default and a member the class declares. Along the chain (found in
    /// review, #418) it is a member a derived class declares on the name of a default its base takes,
    /// which shadowed the default for every call through the interface; a default on the name of a
    /// member a base declares; and two defaults of different interface members, one taken by a base
    /// and one by the derived class. The chain is the bases the source declares, as it is for
    /// <see cref="CheckInherited"/>, the runtime's own being EQ2011's. What the language itself picks
    /// is not a clash: a derived class's default for the SAME interface member, more specific than its
    /// base's, and a member that implements the default's interface member because the derived class
    /// lists the interface again.
    /// </summary>
    private static void CheckDefaults(TypeDeclarationSyntax type, string sourcePath, bool isComponent, SemanticModel model,
        List<CompilationError> errors)
    {
        if (model.SyntaxTree != type.SyntaxTree || model.GetDeclaredSymbol(type) is not INamedTypeSymbol declared)
            return;
        var position = type.Identifier.GetLocation().GetLineSpan().StartLinePosition;
        void Refuse(string message) => errors.Add(new CompilationError
        {
            Code = "EQ1007",
            Message = message,
            SourcePath = sourcePath,
            Line = position.Line + 1,
            Column = position.Character + 1,
        });

        // What the bases' twins hold, the nearest base first: the members each declares, and what
        // each takes from its interfaces.
        var inherited = new Dictionary<string, Holder>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        for (var current = declared.BaseType;
             current is { DeclaringSyntaxReferences.Length: > 0 } && seen.Add(current);
             current = current.BaseType)
        {
            foreach (var member in InstanceMembers(current, isComponent))
                inherited.TryAdd(Lowered(member), new Holder($"'{current.Name}.{Shown(member)}', which it inherits", FromInterface: false, Contract: null));
            foreach (var parameter in PrimaryParameters(current))
                inherited.TryAdd(parameter.ToCamelCase(),
                    new Holder($"'{current.Name}({parameter})', a primary constructor's parameter it inherits as a field", FromInterface: false, Contract: null));
            foreach (var (implementation, _, contract) in DefaultInterfaceMembers.Of(current, model.Compilation))
            {
                var owner = contract is null
                    ? $"{Owner(implementation)}, which it inherits from '{current.Name}' with the defaults that call it"
                    : $"the default {Owner(implementation)}, which it inherits from '{current.Name}'";
                inherited.TryAdd(Lowered(implementation), new Holder(owner, FromInterface: true, contract));
            }
        }

        var taken = new Dictionary<string, string>();
        foreach (var member in InstanceMembers(declared, isComponent))
        {
            var name = Lowered(member);
            taken.TryAdd(name, $"'{declared.Name}.{Shown(member)}'");
            // On the name of what a base takes from an interface, the member answers the interface's
            // calls in its place, unless it IS what the interface reaches for this class.
            if (!inherited.TryGetValue(name, out var holder) || !holder.FromInterface
                || (holder.Contract is { } contract
                    && SymbolEqualityComparer.Default.Equals(declared.FindImplementationForInterfaceMember(contract), member)))
                continue;
            Refuse($"'{declared.Name}.{Shown(member)}' lowers to `{name}`, and so does {holder.Owner}. C# reaches "
                + "that member only through its interface, and a JavaScript class chain has one member per name, so "
                + $"'{declared.Name}.{Shown(member)}' would answer the interface's calls in its place. Give it its own name.");
        }

        // A primary constructor's parameter is a field of the twin, which the emitter always assigns
        // (found in review, #418): `class C(int mark) : I` beside a default `I.Mark()` held `this.mark`
        // over the prototype's `mark()`. A record's are its positional properties, counted above.
        foreach (var parameter in PrimaryParameters(declared))
        {
            var name = parameter.ToCamelCase();
            var shown = $"'{declared.Name}({parameter})', a primary constructor's parameter the twin holds as a field";
            taken.TryAdd(name, shown);
            if (!inherited.TryGetValue(name, out var holder) || !holder.FromInterface) continue;
            Refuse($"{shown}, lowers to `{name}`, and so does {holder.Owner}. C# reaches that member only through its "
                + "interface, and a JavaScript class chain has one member per name, so the field would answer the "
                + "interface's calls in its place. Give it its own name.");
        }

        foreach (var (implementation, _, contract) in DefaultInterfaceMembers.Of(declared, model.Compilation))
        {
            var name = Lowered(implementation);
            var owner = Owner(implementation);
            if (taken.TryGetValue(name, out var earlier))
            {
                Refuse($"'{declared.Name}' takes the default {owner}, which lowers to `{name}`, and so does {earlier}. "
                    + "C# reaches a default through its interface, and the twin holds it as a member of its own, one "
                    + $"per name, so it would keep one of them. Declare {Simple(implementation.Name)} in "
                    + $"'{declared.Name}', or give one of them its own name.");
                continue;
            }
            taken[name] = owner;
            // The same interface member's default, more specific here than in the base, is the
            // override the language picks, and the prototype chain honours it.
            if (!inherited.TryGetValue(name, out var holder)
                || (holder.Contract is not null && contract is not null
                    && SymbolEqualityComparer.Default.Equals(holder.Contract, contract)))
                continue;
            Refuse($"'{declared.Name}' takes the default {owner}, which lowers to `{name}`, and so does {holder.Owner}. "
                + "A JavaScript class chain has one member per name, so one of them would answer the other's calls. "
                + "Give one of them its own name.");
        }
    }

    /// <summary>The parameters of a class's or a struct's primary constructor, which the twin assigns
    /// to fields of the same name. A record's are its positional properties, members already.</summary>
    private static IEnumerable<string> PrimaryParameters(INamedTypeSymbol type) =>
        type.IsRecord
            ? []
            : type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .SelectMany(declaration => declaration.ParameterList?.Parameters ?? default)
                .Select(parameter => parameter.Identifier.Text);

    /// <summary>A name along a twin's chain: who holds it, whether it came from an interface, and the
    /// interface member it answers (none for a member a class declares, or for a private helper).</summary>
    private readonly record struct Holder(string Owner, bool FromInterface, ISymbol? Contract);

    /// <summary>An explicit implementation is named after its interface (`IShape.Describe`), and
    /// lowers under the member's own name.</summary>
    private static string Simple(string name) => name[(name.LastIndexOf('.') + 1)..];

    private static string Lowered(ISymbol member) => Simple(member.Name).ToCamelCase();

    /// <summary>How a member reads in a message: an explicit implementation by its interface and its
    /// own name (<c>IOne.M</c>), which Roslyn names by the interface's full name.</summary>
    private static string Shown(ISymbol member) => member switch
    {
        IMethodSymbol { ExplicitInterfaceImplementations: [var method, ..] } => $"{method.ContainingType.Name}.{method.Name}",
        IPropertySymbol { ExplicitInterfaceImplementations: [var property, ..] } => $"{property.ContainingType.Name}.{property.Name}",
        _ => member.Name,
    };

    private static string Owner(ISymbol implementation) =>
        $"'{implementation.ContainingType.Name}.{Simple(implementation.Name)}'";

    /// <summary>
    /// The members a twin writes on an instance or its prototype: fields, events, properties and
    /// methods, an explicit implementation under its member's own name, and nothing the compiler
    /// declares itself. A field takes its name too: <c>class C : I { public int Mark; }</c> beside a
    /// default <c>I.Mark()</c> gave the twin two members named <c>mark</c>, and so did an explicit
    /// <c>IA.M()</c> beside a default <c>IB.M()</c>, and an event, which lowers to an instance field
    /// (all found in review, #418). An indexer takes the names its twin's two methods have, which
    /// <see cref="Names"/> checks within one declaration (#427); along the chain it is not checked yet.
    /// </summary>
    private static IEnumerable<ISymbol> InstanceMembers(INamedTypeSymbol type, bool isComponent) =>
        type.GetMembers().Where(member => !member.IsStatic && !member.IsImplicitlyDeclared
            && member is IFieldSymbol or IEventSymbol or IPropertySymbol { IsIndexer: false }
                or IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation }
            // A component's server-only method never reaches its twin, so it takes no name there
            // (asked in review, #418), as CheckInherited leaves it out too.
            && !(isComponent && member is IMethodSymbol
                && member.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is "ServerOnlyAttribute" or "ServerOnly")));

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
            var name = TwinMethodName.Of(symbol);
            if (Inherited(declared, symbol, name, isStatic, isComponent) is not { } inherited) continue;
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

    /// <summary>The inherited method <paramref name="method"/> would take over, or null: one that holds
    /// the name it holds on the twin (<see cref="TwinMethodName"/>) and that it does not override. A base
    /// the source does not declare ends the walk: its twin is the runtime's. A server-only method is left
    /// out of a COMPONENT's twin only: a plain class's twin writes it like any other, so along a plain
    /// chain it takes its name.</summary>
    private static IMethodSymbol? Inherited(INamedTypeSymbol declared, IMethodSymbol method, string name, bool isStatic,
        bool isComponent)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        for (var current = declared.BaseType;
             current is { DeclaringSyntaxReferences.Length: > 0 } && seen.Add(current);
             current = current.BaseType)
        {
            foreach (var member in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind != MethodKind.Ordinary || member.IsImplicitlyDeclared) continue;
                if (member.IsStatic != isStatic || TwinMethodName.Of(member) != name) continue;
                if (member.ExplicitInterfaceImplementations.Length > 0 || Overrides(method, member)) continue;
                // A defining half of a partial method reaches no twin, nor does a component's server-only one.
                if (member.IsPartialDefinition && member.PartialImplementationPart is null) continue;
                if (isComponent && member.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is "ServerOnlyAttribute" or "ServerOnly"))
                    continue;
                return member;
            }
        }
        return null;
    }

    /// <summary>
    /// A member of an interface the type answers with a method that holds a name of its own because it
    /// hides one (<see cref="TwinMethodName"/>, #563): a call through the interface reaches the member by
    /// the interface's name, which the hidden member holds on the twin, so it would reach the hidden one,
    /// where C# reaches the method that hides it. Refused, as a name the twin cannot hold twice is.
    /// </summary>
    private static void CheckHidingImplementations(TypeDeclarationSyntax type, string sourcePath, SemanticModel model,
        List<CompilationError> errors)
    {
        if (model.SyntaxTree != type.SyntaxTree || model.GetDeclaredSymbol(type) is not INamedTypeSymbol declared) return;
        foreach (var contract in declared.AllInterfaces.SelectMany(face => face.GetMembers().OfType<IMethodSymbol>()))
        {
            if (declared.FindImplementationForInterfaceMember(contract) is not IMethodSymbol { MethodKind: MethodKind.Ordinary } answer
                || !TwinMethodName.HoldsANameOfItsOwn(answer))
                continue;
            var position = type.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            var answers = $"'{answer.ContainingType.Name}.{answer.Name}' answers '{contract.ContainingType.Name}.{contract.Name}' "
                + $"for '{declared.Name}'";
            errors.Add(new CompilationError
            {
                Code = "EQ1007",
                // Beside an explicit implementation of its name, the explicit one holds that name (#708).
                Message = TwinMethodName.ExplicitBeside(answer) is { ExplicitInterfaceImplementations: [var other, ..] }
                    ? $"{answers}, and the explicit implementation of '{other.ContainingType.Name}.{other.Name}' beside it holds "
                        + $"`{Lowered(contract)}`, so its twin holds it as `{TwinMethodName.Of(answer)}()`. A call through "
                        + $"'{contract.ContainingType.Name}' reaches the member by its own name, so it would reach the explicit one. "
                        + $"Answer '{contract.ContainingType.Name}.{contract.Name}' explicitly too, or give this method a name of its own."
                    : $"{answers}, and it hides an inherited member, so its twin holds it as "
                        + $"`{TwinMethodName.Of(answer)}()` and leaves `{Lowered(contract)}` to the member it hides. A call "
                        + "through the interface reaches the member by its own name, so it would reach the hidden one. "
                        + "Answer the interface with a method that hides nothing: give this one its own name, or override "
                        + "the member it hides.",
                SourcePath = sourcePath,
                Line = position.Line + 1,
                Column = position.Character + 1,
            });
            return;
        }
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
