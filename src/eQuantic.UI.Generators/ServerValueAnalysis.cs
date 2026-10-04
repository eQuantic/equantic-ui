using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Generators;

/// <summary>
/// The values a page receives from the server's container, and what of each the browser reads.
/// <para>
/// A page is built from the request's services, and a service registered as a class is an object the
/// browser has no way to build: the router builds the page with no arguments. It crossed whole all the
/// same, because it was not an interface, and a login page put its identity provider's authority into
/// the HTML that way (#510). What crosses now is its PROJECTION: whether it is null, and the members the
/// browser-side code reads, down to plain data (<see cref="ProjectionLeaves"/>).
/// </para>
/// <para>
/// The analysis reads the bound tree of every member the twin emits, which is every member but a
/// <c>[ServerOnly]</c> one and a <c>[ServerAction]</c>'s body, the rule eqc applies. It follows a value
/// through a null test, a member read, a pattern, a local, a field it is kept in, a member that returns
/// it, and a component or a method of the app that receives it, whose own reads join the page's. Any
/// other use stops it, and a stop is EQ2114: a use the build cannot follow is a use whose answer it
/// cannot send, and the browser would draw something the server did not.
/// </para>
/// </summary>
internal sealed class ServerValueAnalysis
{
    /// <summary>What a read of a member holding the value means while its type is still constructing.</summary>
    private enum Construction
    {
        /// <summary>The page's own: the browser constructs the page without the value, which arrives after.</summary>
        Refused,
        /// <summary>A component the value is handed to, which holds it from its construction on.</summary>
        Followed,
        /// <summary>Stored while the browser runs, so a read during construction sees the member's first value.</summary>
        Ignored,
    }

    private const string ReadWhileConstructed =
        "it is read while the page is constructed, and the browser constructs the page without it";

    private const string CheckedWhileConstructed =
        "it is checked while the page is constructed, where the browser has no value and the check throws "
        + "(the container already refuses to build the page without it)";

    /// <summary>
    /// A test that names a type, even the one the value is declared as: the twin tests it with
    /// <c>instanceof</c>, and what crosses is a plain copy no class constructed.
    /// </summary>
    private const string TestedForAType =
        "it is tested for a type, and what crosses to the browser is a plain copy of no type (test it against null)";

    /// <summary>
    /// A read the server would bind to another member: it binds each read on the type the value is
    /// declared as, and a value read through a base type whose member a derived one hides with <c>new</c>
    /// names, there, the derived one.
    /// </summary>
    private const string BoundElsewhere =
        "it is read through another type than it is declared as, where the name binds a member the declared type hides";

    /// <summary>
    /// A member a .NET type declares: eqc lowers it to the browser's own form of that type (a list's
    /// <c>Count</c> is an array's <c>length</c>, a set's is a Set's <c>size</c>), which a projected copy
    /// is not.
    /// </summary>
    private const string PlatformMember =
        "it is read through a member of a .NET type, which the browser answers from its own form of that type rather than from a copy";

    private const string WalkedThroughItself =
        "it is walked through a chain of its own members, which the build cannot bound";

    /// <summary>
    /// How many members deep a read is followed. A cycle is caught where it closes; this is the bound for
    /// any other growth, so the analysis always ends.
    /// </summary>
    private const int MaxDepth = 16;


    private readonly INamedTypeSymbol _page;
    private readonly Compilation _compilation;
    private readonly CancellationToken _token;
    private readonly Dictionary<ISymbol, Projection> _projections = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ISymbol> _storage = new(SymbolEqualityComparer.Default);
    private readonly List<BoundaryStop> _stops = new();
    private readonly HashSet<string> _stopped = new(System.StringComparer.Ordinal);
    private readonly HashSet<string> _followed = new(System.StringComparer.Ordinal);
    private readonly HashSet<string> _active = new(System.StringComparer.Ordinal);
    private readonly HashSet<IParameterSymbol> _roots = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<SyntaxTree, SemanticModel> _models = new();

    private ServerValueAnalysis(INamedTypeSymbol page, Compilation compilation, CancellationToken token)
    {
        _page = page;
        _compilation = compilation;
        _token = token;
    }

    /// <summary>Every member that holds a server value: a captured parameter, or a field or property the page keeps one in.</summary>
    public IReadOnlyCollection<ISymbol> Storage => _storage;

    /// <summary>What the browser reads of each member in <see cref="Storage"/> it reads at all.</summary>
    public IReadOnlyDictionary<ISymbol, Projection> Projections => _projections;

    /// <summary>The uses the projection cannot follow, each an EQ2114.</summary>
    public IReadOnlyList<BoundaryStop> Stops => _stops;

    /// <summary>
    /// Whether a constructor parameter of a page holds a server value: an object of a class, which the
    /// browser has no way to build. An interface from outside <c>System</c> is a dependency the browser
    /// resolves for itself (<c>CapabilityRule</c>), and plain data is data.
    /// </summary>
    public static bool IsServerValue(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Class && !ProjectionLeaves.IsLeaf(type);

    public static ServerValueAnalysis Of(INamedTypeSymbol page, Compilation compilation, CancellationToken token)
    {
        var analysis = new ServerValueAnalysis(page, compilation, token);
        analysis.Run();
        return analysis;
    }

    private void Run()
    {
        var parameters = _page.InstanceConstructors
            .Where(constructor => !constructor.IsImplicitlyDeclared)
            .SelectMany(constructor => constructor.Parameters)
            .Where(parameter => IsServerValue(parameter.Type))
            .ToList();

        foreach (var parameter in parameters) Root(parameter);
    }

    /// <summary>A constructor parameter of the page, or of a base the page hands one to, holding a server value.</summary>
    private void Root(IParameterSymbol parameter)
    {
        if (!_roots.Add(parameter)) return;
        // Never whole, read by the browser or not: one only a [ServerOnly] member reads is captured all
        // the same, and crossed whole before.
        _storage.Add(parameter);
        foreach (var (reference, construction) in References(_page, parameter))
        {
            if (!construction)
            {
                // Captured: a member reads it from the field the C# compiler gives it.
                Use(reference, parameter, "", _page);
            }
            else if (HandedOn(reference) is { } next)
            {
                // To the base's constructor, or another of the page's: the same object keeps it.
                Root(next);
            }
            else if (StoredInto(reference, guarded: false) is { } member)
            {
                if (_storage.Add(member)) FollowMember(member, member, "", Construction.Refused, _page, reference);
            }
            else if (Guard(reference) is { } check)
            {
                Stop(check, parameter, CheckedWhileConstructed);
            }
            else
            {
                Stop(reference, parameter, ReadWhileConstructed);
            }
        }
    }

    /// <summary>What is done with a value the browser holds: <paramref name="operation"/> evaluates to it.</summary>
    private void Use(IOperation operation, ISymbol root, string path, INamedTypeSymbol instance)
    {
        _token.ThrowIfCancellationRequested();
        var (parent, child) = Climb(operation);
        switch (parent)
        {
            case null:
            case INameOfOperation:
            case IExpressionStatementOperation:
                return;
            case IMemberReferenceOperation member when member.Instance == child:
                Member(member, root, path, instance);
                return;
            case IConditionalAccessOperation access when access.Operation == child:
                Read(root).Presence(path);
                if (InstanceOf(access) is { } conditional) Use(conditional, root, path, instance);
                return;
            case IConditionalAccessOperation access when access.WhenNotNull == child:
                // `identity?.Profile` evaluates to what the `?.` reached, or null.
                Use(access, root, path, instance);
                return;
            case IBinaryOperation binary when IsNullTest(binary, child):
                Read(root).Presence(path);
                return;
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } concatenation:
                Stop(concatenation, root, "it is converted to text");
                return;
            case IBinaryOperation comparison:
                Stop(comparison, root, "it is compared with another object");
                return;
            case IIsPatternOperation test when test.Value == child:
                Pattern(test.Pattern, root, path, instance);
                return;
            case IIsTypeOperation test when test.ValueOperand == child:
                Stop(test, root, TestedForAType);
                return;
            case ISwitchExpressionOperation choice when choice.Value == child:
                foreach (var arm in choice.Arms) Pattern(arm.Pattern, root, path, instance);
                return;
            case ISwitchOperation statement when statement.Value == child:
                foreach (var clause in statement.Cases.SelectMany(c => c.Clauses)) Clause(clause, root, path, instance);
                return;
            case ISimpleAssignmentOperation assignment when assignment.Value == child:
                Store(assignment, root, path, instance);
                return;
            case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator }:
                FollowLocal(declarator.Symbol, declarator, root, path, instance);
                return;
            case IArgumentOperation argument:
                Argument(argument, root, path, instance);
                return;
            case IReturnOperation returned when returned.ReturnedValue == child:
                Returned(returned, root, path, instance);
                return;
            case IInvocationOperation invocation when invocation.Instance == child:
                Stop(invocation, root, "a method is called on it");
                return;
            case IForEachLoopOperation loop when loop.Collection == child:
                Stop(child, root, "it is enumerated");
                return;
            case IInterpolationOperation:
            case IInterpolatedStringOperation:
                Stop(parent, root, "it is converted to text");
                return;
            case IConversionOperation conversion:
                Stop(conversion, root, "it is converted to another type");
                return;
            case IArrayInitializerOperation:
            case ICollectionExpressionOperation:
                Stop(parent, root, "it is stored in a collection");
                return;
            case ICoalesceOperation coalesce when coalesce.Value == child:
                // `identity ?? guest`: whether it is null decides, and the result is it when it is not.
                Read(root).Presence(path);
                Use(coalesce, root, path, instance);
                return;
            case ICoalesceOperation coalesce when coalesce.WhenNull == child:
            case IConditionalOperation choice when choice.WhenTrue == child || choice.WhenFalse == child:
                // The result is this value or the other one, and what is read of it is read of this.
                Use(parent, root, path, instance);
                return;
            case IInvalidOperation invalid:
                Unbound(invalid, child, root, path);
                return;
            default:
                Stop(parent, root, "the build cannot follow what is done with it");
                return;
        }
    }

    /// <summary>A member of the value: a leaf is read whole, an object is followed further.</summary>
    private void Member(IMemberReferenceOperation member, ISymbol root, string path, INamedTypeSymbol instance)
    {
        switch (member)
        {
            case IMethodReferenceOperation:
                Stop(member, root, "a method of it is taken as a delegate");
                return;
            case IEventReferenceOperation:
                Stop(member, root, "an event of it is subscribed to");
                return;
            case IPropertyReferenceOperation { Arguments.Length: > 0 }:
                Stop(member, root, "it is indexed");
                return;
        }
        if (IsWritten(member))
        {
            Stop(member, root, "a member of it is assigned in the browser");
            return;
        }
        if (member.Type is not { } type)
        {
            Stop(member, root, "the build cannot follow what is done with it");
            return;
        }

        // A nullable struct's `.Value` is the struct itself, which the twin reads without it, and
        // `.HasValue` asks whether it is there.
        if (member.Member.ContainingType?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            if (member.Member.Name == "HasValue") Read(root).Presence(path);
            else if (member.Member.Name == "Value") Use(member, root, path, instance);
            else Stop(member, root, "the build cannot follow what is done with it");
            return;
        }

        if (BoundaryShape.IsPlatform(member.Member.ContainingType))
        {
            Stop(member, root, PlatformMember);
            return;
        }
        var next = Combine(path, member.Member.Name);
        if (Depth(next) > MaxDepth)
        {
            Stop(member, root, "it is read through more members than the build follows");
            return;
        }
        if (!Agrees(member.Member, ServerBinds(root, next)))
        {
            Stop(member, root, BoundElsewhere);
            return;
        }
        if (ProjectionLeaves.IsLeaf(type)) Read(root).Value(next);
        else Use(member, root, next, instance);
    }

    private void Pattern(IPatternOperation pattern, ISymbol root, string path, INamedTypeSymbol instance)
    {
        switch (pattern)
        {
            case IConstantPatternOperation constant when IsNull(constant.Value):
                Read(root).Presence(path);
                return;
            case INegatedPatternOperation negated:
                Pattern(negated.Pattern, root, path, instance);
                return;
            case IBinaryPatternOperation both:
                Pattern(both.LeftPattern, root, path, instance);
                Pattern(both.RightPattern, root, path, instance);
                return;
            case IDiscardPatternOperation:
                return;
            case ITypePatternOperation:
                Stop(pattern, root, TestedForAType);
                return;
            case IDeclarationPatternOperation declaration:
                // `is var x` names no type and matches null too; `is SiteIdentity x` tests the type.
                if (!declaration.MatchesNull)
                {
                    Stop(pattern, root, TestedForAType);
                    return;
                }
                if (declaration.DeclaredSymbol is ILocalSymbol bound) FollowLocal(bound, pattern, root, path, instance);
                return;
            case IRecursivePatternOperation recursive:
                // `{ … }` asks whether it is there; `SiteIdentity { … }` tests the type as well.
                if (recursive.Syntax is RecursivePatternSyntax { Type: not null })
                {
                    Stop(pattern, root, TestedForAType);
                    return;
                }
                Read(root).Presence(path);
                if (recursive.DeconstructionSubpatterns.Length > 0)
                {
                    Stop(pattern, root, "it is deconstructed");
                    return;
                }
                foreach (var property in recursive.PropertySubpatterns) Subpattern(property, root, path, instance);
                if (recursive.DeclaredSymbol is ILocalSymbol named) FollowLocal(named, pattern, root, path, instance);
                return;
            default:
                Stop(pattern, root, "it is matched in a way the build cannot follow");
                return;
        }
    }

    /// <summary>
    /// <c>{ DisplayName: "x" }</c> reads <c>DisplayName</c>. Roslyn shapes the extended
    /// <c>{ Profile.Name: var n }</c> as <c>{ Profile: { Name: var n } }</c>, so each subpattern names one
    /// member, and a leaf on the way is met, and read whole, before anything beneath it.
    /// </summary>
    private void Subpattern(IPropertySubpatternOperation property, ISymbol root, string path, INamedTypeSymbol instance)
    {
        if (property.Member is not IMemberReferenceOperation member
            || member is IMethodReferenceOperation
            || member.Type is not { } type)
        {
            Stop(property, root, "it is matched in a way the build cannot follow");
            return;
        }
        if (BoundaryShape.IsPlatform(member.Member.ContainingType))
        {
            Stop(property, root, PlatformMember);
            return;
        }
        var next = Combine(path, member.Member.Name);
        if (Depth(next) > MaxDepth)
        {
            Stop(property, root, "it is read through more members than the build follows");
            return;
        }
        if (!Agrees(member.Member, ServerBinds(root, next)))
        {
            Stop(property, root, BoundElsewhere);
            return;
        }
        if (ProjectionLeaves.IsLeaf(type)) Read(root).Value(next);
        else Pattern(property.Pattern, root, next, instance);
    }

    private void Clause(ICaseClauseOperation clause, ISymbol root, string path, INamedTypeSymbol instance)
    {
        switch (clause)
        {
            case IPatternCaseClauseOperation matched:
                Pattern(matched.Pattern, root, path, instance);
                return;
            case ISingleValueCaseClauseOperation single when IsNull(single.Value):
                Read(root).Presence(path);
                return;
            case IDefaultCaseClauseOperation:
                return;
            default:
                Stop(clause, root, "it is matched in a way the build cannot follow");
                return;
        }
    }

    private void Store(ISimpleAssignmentOperation assignment, ISymbol root, string path, INamedTypeSymbol instance)
    {
        switch (assignment.Target)
        {
            case IDiscardOperation:
                return;
            case ILocalReferenceOperation local:
                FollowLocal(local.Local, assignment, root, path, instance);
                return;
        }
        if (MemberOfThis(assignment.Target) is { } member && IsInSource(member.ContainingType))
        {
            FollowMember(member, root, path,
                InConstruction(assignment) ? Construction.Followed : Construction.Ignored, instance, assignment);
            return;
        }
        Stop(assignment, root, "it is stored where the build cannot follow it");
    }

    private void Argument(IArgumentOperation argument, ISymbol root, string path, INamedTypeSymbol instance)
    {
        var at = argument.Parent ?? argument;
        IMethodSymbol? target = null;
        INamedTypeSymbol? created = null;
        switch (argument.Parent)
        {
            case IInvocationOperation invocation:
                target = invocation.TargetMethod;
                // A constructor reached by a call is the base's, or another of the same object's.
                if (target.MethodKind == MethodKind.Constructor) created = instance;
                break;
            case IObjectCreationOperation creation:
                target = creation.Constructor;
                created = creation.Type as INamedTypeSymbol;
                break;
        }
        if (target is null || argument.Parameter is null || argument.ArgumentKind == ArgumentKind.ParamArray
            || argument.Parameter.RefKind != RefKind.None)
        {
            Stop(at, root, "the build cannot follow what is done with it");
            return;
        }

        // `ReferenceEquals(identity, null)` asks only whether it is there.
        if (target is { Name: "ReferenceEquals", ContainingType.SpecialType: SpecialType.System_Object }
            && argument.Parent is IInvocationOperation equality
            && equality.Arguments.Any(other => other != argument && IsNull(other.Value)))
        {
            Read(root).Presence(path);
            return;
        }

        var definition = target.OriginalDefinition;
        if (definition.DeclaringSyntaxReferences.Length == 0)
        {
            Stop(at, root, $"it is passed to {Describe(definition)}, whose source the build does not have");
            return;
        }
        if (RunsOnTheServer(definition) || RunsOnTheServer(definition.ContainingType))
        {
            Stop(at, root, $"it is passed to {Describe(definition)}, which runs on the server");
            return;
        }
        FollowParameter(argument.Parameter.OriginalDefinition, root, path, created, at);
    }

    /// <summary>
    /// A call that binds to nothing here. The app's own factories are written by another generator,
    /// which this one cannot see, so a factory call is resolved by the rule that writes the factory.
    /// </summary>
    private void Unbound(IInvalidOperation invalid, IOperation child, ISymbol root, string path)
    {
        if (invalid.Syntax is InvocationExpressionSyntax { Expression: IdentifierNameSyntax name } call
            && FactorySurface.Resolve(_compilation, name.Identifier.ValueText) is { } component
            && FactorySurface.Elect(component, out _) is { } constructor
            && ParameterOf(call, child.Syntax, FactorySurface.Parameters(constructor)) is { } parameter)
        {
            FollowParameter(parameter, root, path, component, invalid);
            return;
        }
        Stop(invalid, root, "the call it is passed to does not bind");
    }

    private void Returned(IReturnOperation returned, ISymbol root, string path, INamedTypeSymbol instance)
    {
        var owner = Model(returned.Syntax.SyntaxTree).GetEnclosingSymbol(returned.Syntax.SpanStart, _token) as IMethodSymbol;
        switch (owner)
        {
            case { MethodKind: MethodKind.PropertyGet, AssociatedSymbol: IPropertySymbol property }:
                FollowMember(property, root, path,
                    SymbolEqualityComparer.Default.Equals(instance, _page) ? Construction.Refused : Construction.Followed,
                    instance, returned);
                return;
            case { MethodKind: MethodKind.Ordinary } method:
                if (Enter("returns", root, method, path, returned) is not { } calls) return;
                try
                {
                    foreach (var (body, _) in Chain(instance).SelectMany(Bodies))
                    foreach (var call in body.DescendantsAndSelf().OfType<IInvocationOperation>())
                        if (Same(call.TargetMethod, method))
                            Use(call, root, path, instance);
                }
                finally
                {
                    _active.Remove(calls);
                }
                return;
            default:
                Stop(returned, root, "it is returned where the build cannot follow it");
                return;
        }
    }

    private void FollowLocal(ILocalSymbol local, IOperation near, ISymbol root, string path, INamedTypeSymbol instance)
    {
        if (Enter("local", root, local, path, near) is not { } following) return;
        try
        {
            var body = near;
            while (body.Parent is not null) body = body.Parent;
            foreach (var reference in ReferencesIn(body, local))
                if (!IsWritten(reference))
                    Use(reference, root, path, instance);
        }
        finally
        {
            _active.Remove(following);
        }
    }

    private void FollowMember(
        ISymbol member, ISymbol root, string path, Construction construction, INamedTypeSymbol instance, IOperation at)
    {
        if (Enter("member", root, member, path, at) is not { } following) return;
        try
        {
            foreach (var (reference, constructing) in References(instance, member))
            {
                if (IsWritten(reference)) continue;
                if (constructing && construction == Construction.Ignored) continue;
                if (constructing && construction == Construction.Refused)
                {
                    Stop(reference, root, ReadWhileConstructed);
                    continue;
                }
                Use(reference, root, path, instance);
            }
        }
        finally
        {
            _active.Remove(following);
        }
    }

    /// <summary>
    /// A method or a constructor of the app's own code that receives the value: what it reads of the
    /// parameter is what the browser reads of the value. A constructor's object holds it from then on,
    /// so the members it is stored in are followed through the type constructed and its bases.
    /// </summary>
    private void FollowParameter(
        IParameterSymbol parameter, ISymbol root, string path, INamedTypeSymbol? created, IOperation at)
    {
        if (parameter.ContainingSymbol is not IMethodSymbol method) return;
        if (Enter("parameter", root, parameter, path, at) is not { } following) return;
        try
        {
            Follow(parameter, method, root, path, created);
        }
        finally
        {
            _active.Remove(following);
        }
    }

    private void Follow(IParameterSymbol parameter, IMethodSymbol method, ISymbol root, string path, INamedTypeSymbol? created)
    {
        if (method.MethodKind == MethodKind.Constructor)
        {
            var type = created ?? method.ContainingType;
            var primary = method.DeclaringSyntaxReferences.Any(r => r.GetSyntax(_token) is TypeDeclarationSyntax);
            var bodies = primary
                ? Chain(type).SelectMany(Bodies).Select(b => (IOperation?)b.Body)
                : method.DeclaringSyntaxReferences.Select(r => Model(r.SyntaxTree).GetOperation(r.GetSyntax(_token), _token));
            foreach (var body in bodies)
            {
                if (body is null) continue;
                foreach (var reference in ReferencesIn(body, parameter))
                {
                    // The value arrives with the object, so a check against null passes in the browser too.
                    if (StoredInto(reference, guarded: true) is { } member)
                        FollowMember(member, root, path, Construction.Followed, type, reference);
                    else Use(reference, root, path, type);
                }
            }
            return;
        }

        foreach (var declaration in method.DeclaringSyntaxReferences)
        {
            if (Model(declaration.SyntaxTree).GetOperation(declaration.GetSyntax(_token), _token) is not { } body) continue;
            foreach (var reference in ReferencesIn(body, parameter))
                Use(reference, root, path, method.ContainingType);
        }
    }

    /// <summary>
    /// The member of <c>this</c> a constructing reference is stored in, whole. With <paramref name="guarded"/>,
    /// <c>options ?? throw new ArgumentNullException(…)</c> stores options, which is true where the value
    /// arrives with the object, and not for a page, whose browser half is constructed without it.
    /// </summary>
    private static ISymbol? StoredInto(IOperation reference, bool guarded)
    {
        var (parent, child) = Climb(reference);
        while (guarded && parent is ICoalesceOperation coalesce && coalesce.Value == child && Unwrap(coalesce.WhenNull) is IThrowOperation)
            (parent, child) = Climb(coalesce);
        return parent switch
        {
            ISimpleAssignmentOperation assignment when assignment.Value == child => MemberOfThis(assignment.Target),
            IFieldInitializerOperation field when field.Value == child && field.InitializedFields.Length == 1
                => field.InitializedFields[0],
            IPropertyInitializerOperation property when property.Value == child && property.InitializedProperties.Length == 1
                => property.InitializedProperties[0],
            _ => null,
        };
    }

    /// <summary>The check a constructing reference is the subject of: <c>options ?? throw …</c>.</summary>
    private static IOperation? Guard(IOperation reference) =>
        Climb(reference) is (ICoalesceOperation coalesce, var child) && coalesce.Value == child
            && Unwrap(coalesce.WhenNull) is IThrowOperation
            ? coalesce
            : null;

    /// <summary>The parameter of a constructor of the same object a constructing reference is handed to.</summary>
    private static IParameterSymbol? HandedOn(IOperation reference) =>
        Climb(reference) is (IArgumentOperation
            {
                Parent: IInvocationOperation { TargetMethod.MethodKind: MethodKind.Constructor } call,
                Parameter: { } parameter,
                ArgumentKind: not ArgumentKind.ParamArray,
            }, _)
        && call.TargetMethod.OriginalDefinition.DeclaringSyntaxReferences.Length > 0
            ? parameter.OriginalDefinition
            : null;

    private static ISymbol? MemberOfThis(IOperation target) => target switch
    {
        IFieldReferenceOperation { Instance: IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } } field
            => field.Field,
        IPropertyReferenceOperation { Instance: IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }, Arguments.Length: 0 } property
            => property.Property,
        _ => null,
    };

    /// <summary>Every reference to <paramref name="symbol"/> in the bodies of <paramref name="instance"/> and its bases in source.</summary>
    private IEnumerable<(IOperation Reference, bool Construction)> References(INamedTypeSymbol instance, ISymbol symbol)
    {
        foreach (var (body, construction) in Chain(instance).SelectMany(Bodies))
        foreach (var reference in ReferencesIn(body, symbol))
            yield return (reference, construction);
    }

    private static IEnumerable<IOperation> ReferencesIn(IOperation body, ISymbol symbol) =>
        body.DescendantsAndSelf().Where(operation => operation.Parent is not INameOfOperation && operation switch
        {
            IParameterReferenceOperation parameter => Same(parameter.Parameter, symbol),
            ILocalReferenceOperation local => Same(local.Local, symbol),
            IFieldReferenceOperation field => Same(field.Field, symbol) && OfThis(field.Instance),
            IPropertyReferenceOperation property => Same(property.Property, symbol) && OfThis(property.Instance),
            _ => false,
        });

    private static bool OfThis(IOperation? instance) =>
        instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance };

    /// <summary>
    /// The bodies the twin emits, each with whether it runs while the object constructs: the base
    /// call, the constructors and the initializers do; a method or an accessor runs later. A
    /// <c>[ServerOnly]</c> method and a <c>[ServerAction]</c>'s body never reach the browser.
    /// </summary>
    private IEnumerable<(IOperation Body, bool Construction)> Bodies(INamedTypeSymbol type)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(_token) is not TypeDeclarationSyntax declaration) continue;
            var model = Model(declaration.SyntaxTree);
            if (declaration.BaseList is { } bases)
                foreach (var call in bases.Types.OfType<PrimaryConstructorBaseTypeSyntax>())
                    if (model.GetOperation(call, _token) is { } baseCall) yield return (baseCall, true);

            foreach (var member in declaration.Members)
            {
                switch (member)
                {
                    case MethodDeclarationSyntax method:
                        if (!RunsOnTheServer(model.GetDeclaredSymbol(method, _token))
                            && model.GetOperation(method, _token) is { } body)
                            yield return (body, false);
                        break;
                    case ConstructorDeclarationSyntax constructor when !constructor.Modifiers.Any(SyntaxKind.StaticKeyword):
                        if (model.GetOperation(constructor, _token) is { } constructing) yield return (constructing, true);
                        break;
                    case BasePropertyDeclarationSyntax property:
                        foreach (var accessor in Accessors(property, model)) yield return (accessor, false);
                        if (property is PropertyDeclarationSyntax { Initializer: { } initializer }
                            && model.GetOperation(initializer, _token) is { } initial)
                            yield return (initial, true);
                        break;
                    case FieldDeclarationSyntax field when !field.Modifiers.Any(SyntaxKind.StaticKeyword):
                        foreach (var variable in field.Declaration.Variables)
                            if (variable.Initializer is { } value && model.GetOperation(value, _token) is { } initialized)
                                yield return (initialized, true);
                        break;
                }
            }
        }
    }

    private IEnumerable<IOperation> Accessors(BasePropertyDeclarationSyntax property, SemanticModel model)
    {
        var arrow = property switch
        {
            PropertyDeclarationSyntax p => p.ExpressionBody,
            IndexerDeclarationSyntax i => i.ExpressionBody,
            _ => null,
        };
        if (arrow is not null && model.GetOperation(arrow, _token) is { } expression) yield return expression;
        foreach (var accessor in property.AccessorList?.Accessors ?? default)
            if (model.GetOperation(accessor, _token) is { } body)
                yield return body;
    }

    /// <summary>The type and its bases declared in source: the one object whose members read a value stored in it.</summary>
    private static IEnumerable<INamedTypeSymbol> Chain(INamedTypeSymbol type)
    {
        for (var current = type; current is not null && IsInSource(current); current = current.BaseType)
            yield return current;
    }

    private static bool IsInSource(INamedTypeSymbol type) => type.Locations.Any(location => location.IsInSource);

    private static bool RunsOnTheServer(ISymbol? symbol) =>
        symbol is not null && symbol.GetAttributes().Any(a => a.AttributeClass?.Name
            is "ServerOnlyAttribute" or "ServerOnly" or "ServerActionAttribute" or "ServerAction");

    /// <summary>Whether a statement runs while its object constructs: in a constructor or an initializer, not in a function it creates.</summary>
    private static bool InConstruction(IOperation operation)
    {
        for (var current = operation; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case IAnonymousFunctionOperation:
                case ILocalFunctionOperation:
                    return false;
                case IConstructorBodyOperation:
                case IFieldInitializerOperation:
                case IPropertyInitializerOperation:
                    return true;
            }
        }
        return false;
    }

    private static bool IsWritten(IOperation operation) => operation.Parent switch
    {
        IAssignmentOperation assignment => assignment.Target == operation,
        IIncrementOrDecrementOperation step => step.Target == operation,
        IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } => true,
        _ => false,
    };

    /// <summary>The operation that uses <paramref name="operation"/>'s value, past the implicit conversions C# writes in between.</summary>
    private static (IOperation? Parent, IOperation Child) Climb(IOperation operation)
    {
        var child = operation;
        var parent = operation.Parent;
        while (parent is IConversionOperation { IsImplicit: true } conversion && !conversion.Conversion.IsUserDefined)
        {
            child = parent;
            parent = parent.Parent;
        }
        return (parent, child);
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion) operation = conversion.Operand;
        return operation;
    }

    private static bool IsNull(IOperation operation) => Unwrap(operation).ConstantValue is { HasValue: true, Value: null };

    private static bool IsNullTest(IBinaryOperation binary, IOperation child) =>
        binary.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals
        && (binary.LeftOperand == child ? binary.RightOperand : binary.RightOperand == child ? binary.LeftOperand : null) is { } other
        && IsNull(other);

    /// <summary>The <c>?.</c>'s own receiver inside what it evaluates when the value is not null.</summary>
    private static IOperation? InstanceOf(IConditionalAccessOperation access) =>
        access.WhenNotNull.DescendantsAndSelf().OfType<IConditionalAccessInstanceOperation>()
            .FirstOrDefault(instance => OwnedBy(instance, access));

    private static bool OwnedBy(IConditionalAccessInstanceOperation instance, IConditionalAccessOperation access)
    {
        IOperation previous = instance;
        for (var current = instance.Parent; current is not null; previous = current, current = current.Parent)
            if (current is IConditionalAccessOperation candidate && candidate.WhenNotNull == previous)
                return candidate == access;
        return false;
    }

    /// <summary>The factory's parameter an argument of <paramref name="call"/> lands in, by position or by name.</summary>
    private static IParameterSymbol? ParameterOf(
        InvocationExpressionSyntax call, SyntaxNode value, IReadOnlyList<IParameterSymbol> parameters)
    {
        var arguments = call.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (!arguments[i].Span.Contains(value.Span)) continue;
            if (arguments[i].NameColon is { } named)
                return parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.ValueText);
            return i < parameters.Count ? parameters[i] : null;
        }
        return null;
    }

    private Projection Read(ISymbol root)
    {
        if (!_projections.TryGetValue(root, out var projection))
            _projections[root] = projection = new Projection();
        return projection;
    }

    private void Stop(IOperation at, ISymbol root, string reason)
    {
        var syntax = at.Syntax;
        if (!_stopped.Add($"{syntax.SyntaxTree.FilePath}:{syntax.SpanStart}:{syntax.Span.Length}:{reason}")) return;
        _stops.Add(new BoundaryStop(syntax.GetLocation(), _page.Name, root.Name, Expression(syntax), reason));
    }

    private static string Expression(SyntaxNode syntax)
    {
        var text = string.Join(" ", syntax.ToString().Split(
            new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= 80 ? text : text.Substring(0, 77) + "...";
    }

    /// <summary>
    /// Starts following <paramref name="symbol"/> for a value at <paramref name="path"/>, and answers the
    /// key to release when done, or null. Not again at a path already followed; and a stop where the value
    /// reaches the symbol again while it is still being followed, as a walk down a chain does
    /// (<c>_node = _node.Next</c>) and a method calling itself with a member does, since neither has an end.
    /// </summary>
    private string? Enter(string what, ISymbol root, ISymbol symbol, string path, IOperation at)
    {
        var key = $"{what}|{Key(root)}|{Key(symbol)}";
        if (!_followed.Add($"{key}|{path}")) return null;
        if (!_active.Add(key))
        {
            Stop(at, root, WalkedThroughItself);
            return null;
        }
        return key;
    }

    private static int Depth(string path) => path.Length == 0 ? 0 : path.Count(c => c == '.') + 1;

    /// <summary>The type a stored value is declared as, where the server starts binding its reads.</summary>
    private static ITypeSymbol? DeclaredType(ISymbol root) => root switch
    {
        IParameterSymbol parameter => parameter.Type,
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        _ => null,
    };

    /// <summary>
    /// The member the server binds the read at <paramref name="path"/> to, as <c>HydrationProjection</c>
    /// does: each segment by name on the type the previous one is declared as, then the types it derives
    /// from (or, on an interface, the interfaces it extends), a nullable struct read as the struct.
    /// </summary>
    private static ISymbol? ServerBinds(ISymbol root, string path)
    {
        var type = DeclaredType(root);
        ISymbol? bound = null;
        foreach (var segment in path.Split('.'))
        {
            if (type is null) return null;
            bound = Lookup(Unwrapped(type), segment);
            type = bound switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => null,
            };
        }
        return bound;
    }

    private static ITypeSymbol Unwrapped(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    private static ISymbol? Lookup(ITypeSymbol type, string name)
    {
        IEnumerable<ITypeSymbol> bound = type.TypeKind == TypeKind.Interface
            ? new[] { type }.Concat(type.AllInterfaces)
            : Bases(type);
        foreach (var candidate in bound)
        foreach (var member in candidate.GetMembers(name))
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, GetMethod: not null }
                or IFieldSymbol { IsStatic: false })
                return member;
        return null;
    }

    private static IEnumerable<ITypeSymbol> Bases(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }

    /// <summary>
    /// Whether the member C# bound and the one the server binds answer the same on one object: the
    /// same member, one overriding the other, or the implementation of the interface member C# bound.
    /// </summary>
    private static bool Agrees(ISymbol bound, ISymbol? server)
    {
        if (server is null) return false;
        // An interface's member answers with what implements it on the type the server binds on, and a
        // virtual one with whichever override that type reaches: one chain of overrides, read either way.
        if (bound.ContainingType is { TypeKind: TypeKind.Interface }
            && server.ContainingType?.TypeKind != TypeKind.Interface)
        {
            if (server.ContainingType?.FindImplementationForInterfaceMember(bound) is not { } implementation)
                return false;
            bound = implementation;
        }
        return Same(bound, server) || Overrides(server, bound) || Overrides(bound, server);
    }

    /// <summary>Whether <paramref name="member"/> overrides <paramref name="ancestor"/>, directly or further down its chain.</summary>
    private static bool Overrides(ISymbol member, ISymbol ancestor)
    {
        for (var overridden = (member as IPropertySymbol)?.OverriddenProperty; overridden is not null; overridden = overridden.OverriddenProperty)
            if (Same(overridden, ancestor)) return true;
        return false;
    }

    private static string Key(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault();
        return $"{symbol.Kind}:{symbol.ToDisplayString()}@{location?.SourceTree?.FilePath}:{location?.SourceSpan.Start}";
    }

    private static bool Same(ISymbol symbol, ISymbol other) =>
        SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, other.OriginalDefinition);

    private static string Combine(string path, string name) => path.Length == 0 ? name : path + "." + name;

    private static string Describe(IMethodSymbol method) => method.MethodKind == MethodKind.Constructor
        ? $"new {method.ContainingType.Name}(…)"
        : $"{method.ContainingType.Name}.{method.Name}";

    private SemanticModel Model(SyntaxTree tree)
    {
        if (!_models.TryGetValue(tree, out var model))
            _models[tree] = model = _compilation.GetSemanticModel(tree);
        return model;
    }
}
