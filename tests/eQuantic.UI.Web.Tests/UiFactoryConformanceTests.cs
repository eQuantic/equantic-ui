using System.Linq.Expressions;
using System.Reflection;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Components.UI;
using FactorySurface = eQuantic.UI.Components.UI;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The declarative factory contract (<see cref="UI"/>): every factory is named EXACTLY like the
/// type it returns and mirrors one of that type's public constructors parameter-for-parameter
/// (same names, same types, same defaults) — named arguments must carry between `new X(…)` and
/// `X(…)` unchanged. Two legal tails may follow the mirrored prefix: OPTIONAL parameters that
/// each correspond to a public init-only property of the type (same name PascalCased, same type
/// — the factory applies them via an object initializer, which is how a declarative screen
/// reaches semantics like <c>Label</c>/<c>Selected</c> that the constructor deliberately does not
/// carry), and, on containers, one final <c>children</c> parameter. And because the class
/// transpiles to a JS twin, no factory may overload — JS methods cannot.
/// </summary>
public class UiFactoryConformanceTests
{
    /// <summary>
    /// Every surface that MAKES this promise, not only the one that made it first.
    /// <c>eQuantic.UI.Charts.ChartsUI</c> states the identical contract in its own doc — "every
    /// chart and every chart value as a factory named exactly like its type, mirroring its
    /// constructor parameter for parameter" — and nothing read it: before this list, no test in
    /// the tree so much as named the class. A rule that holds on one surface and is unchecked on
    /// the next is a convention, not a contract.
    /// </summary>
    private static readonly Type[] Surfaces =
        [typeof(FactorySurface), typeof(eQuantic.UI.Charts.ChartsUI)];

    private static MethodInfo[] AllFactoriesOf(Type surface) =>
        surface.GetMethods(BindingFlags.Public | BindingFlags.Static);

    /// <summary>
    /// Factories that deliberately do NOT mirror a constructor, each for a stated reason. The rule
    /// below is the contract; this is its one documented exception, kept as a list of names so a
    /// second one cannot slip in unnoticed.
    /// <list type="bullet">
    /// <item><c>Gap</c> — wraps the STATIC factory <c>Spacer.Fixed</c>, and cannot be called
    /// <c>Spacer</c> because that name is already the flex factory (no overloads here). Without a
    /// name of its own the rigid spacer is unreachable in any file importing this surface: the
    /// mirrored method shadows the type, so <c>Spacer.Fixed(34)</c> stops compiling.</item>
    /// <item><c>DotBadge</c> — the same shape, for <c>Badge.AsDot</c>. The rule below is what
    /// FINDS these: any type whose static factory sits behind a mirrored name needs one of
    /// these, and <see cref="AStaticFactoryBehindAMirroredName_HasANamedFactory"/> fails until
    /// it gets one.</item>
    /// </list>
    ///
    /// <para>
    /// The list SHRANK once, and how is worth keeping: <c>Glyph</c> was here for an icon PACK's
    /// glyph, because <c>Icon</c> mirrored the curated-enum constructor and this surface has no
    /// overloads. The hole was one layer down — <c>Icon</c> having a constructor per glyph family —
    /// and a curated glyph converting to an <c>IconGlyph</c> implicitly closed it, so the mirrored
    /// name reaches both spellings and the exception had nothing left to do. An entry leaves here
    /// by the shape it names going away, never by being waved through.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> NamedFactories = new() { "Gap", "DotBadge" };

    private static MethodInfo[] FactoriesOf(Type surface) =>
        AllFactoriesOf(surface).Where(method => !NamedFactories.Contains(method.Name)).ToArray();

    /// <summary>Every mirrored factory on every surface — what the contract rules are asked of.</summary>
    private static IEnumerable<MethodInfo> Factories => Surfaces.SelectMany(FactoriesOf);

    [Fact]
    public void EveryFactory_IsNamedExactlyLikeItsReturnType()
    {
        foreach (var factory in Factories)
            factory.Name.Should().Be(factory.ReturnType.Name,
                "a factory is its type minus `new` — any other name breaks the mental model");
    }

    [Fact]
    public void ANamedFactory_StillReturnsAVocabularyNode()
    {
        // The exception is about the NAME, never about what comes back: every factory — mirrored
        // or named — hands back a node the vocabulary already knows.
        foreach (var name in NamedFactories)
        {
            var factory = AllFactoriesOf(typeof(FactorySurface)).Single(m => m.Name == name);
            typeof(eQuantic.UI.Primitives.VisualNode).IsAssignableFrom(factory.ReturnType)
                .Should().BeTrue($"{name} must return a vocabulary node");
        }
    }

    /// <summary>
    /// The trap this whole surface carries, stated as a rule instead of a memory: a mirrored
    /// factory SHADOWS its own type, so any <c>Type.StaticFactory(…)</c> on that type stops
    /// compiling wherever the surface is imported — and the SDK imports it into every file of a
    /// consumer's project, so "wherever" means everywhere. Each one therefore needs a factory
    /// under a name of its own, or it is simply unreachable. <c>Spacer.Fixed</c> was found by a
    /// person writing a page; <c>Badge.AsDot</c> was found by a release review. This finds the
    /// third one on the push that introduces it.
    /// </summary>
    [Fact]
    public void AStaticFactoryBehindAMirroredName_HasANamedFactory()
    {
        var reachableByName = Surfaces.SelectMany(AllFactoriesOf)
            .Where(m => NamedFactories.Contains(m.Name))
            .Select(m => m.ReturnType)
            .ToHashSet();

        var stranded = new List<string>();
        foreach (var factory in Factories)
        {
            var shadowed = factory.ReturnType;
            // A static member of the type that HANDS BACK the type is a construction path — the
            // exact thing the shadowing puts out of reach.
            var staticFactories = shadowed
                .GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(member => member switch
                {
                    MethodInfo method => method.ReturnType == shadowed && !method.IsSpecialName,
                    PropertyInfo property => property.PropertyType == shadowed,
                    FieldInfo field => field.FieldType == shadowed,
                    _ => false,
                })
                .Select(member => $"{shadowed.Name}.{member.Name}");

            foreach (var member in staticFactories)
                if (!reachableByName.Contains(shadowed))
                    stranded.Add(member);
        }

        stranded.Should().BeEmpty(
            "each of these is unreachable while this surface is imported — give it a named factory "
            + "(the Gap/DotBadge shape) and list it in NamedFactories");
    }

    [Fact]
    public void NoFactoryOverloads_TheTwinIsJavaScript()
    {
        // Overloads are checked across EVERY factory, exception or not: the JS twin has one method
        // per name whatever the C# name rule says. Grouped per SURFACE, because each one transpiles
        // to a class of its own — two surfaces sharing a name is not an overload.
        foreach (var surface in Surfaces)
            AllFactoriesOf(surface).GroupBy(m => m.Name).Where(g => g.Count() > 1).Select(g => g.Key)
                .Should().BeEmpty(
                    $"JS class methods cannot overload — one canonical signature per node ({surface.Name})");
    }

    /// <summary>
    /// The mirrored PREFIX of a factory: every parameter but a container's trailing
    /// <c>children</c>, which mirrors no constructor parameter and is checked on its own.
    /// </summary>
    private static ParameterInfo[] MirroredParameters(MethodInfo factory)
    {
        var parameters = factory.GetParameters();
        var hasChildren = parameters.Length > 0 && parameters[^1].Name == "children";
        return hasChildren ? parameters[..^1] : parameters;
    }

    /// <summary>
    /// The public constructor a factory mirrors — the WIDEST one that does, which is the rule the
    /// generator already applies to an app's own components, and the one a factory body calls.
    /// <para>
    /// Both tests below read this same answer on purpose: one asserts that it exists, the other
    /// CALLS it. A mirroring rule that drifted between them would measure a factory against a
    /// constructor it does not actually mirror, and report the difference as the factory's fault.
    /// </para>
    /// </summary>
    private static ConstructorInfo? MirroredConstructor(MethodInfo factory)
    {
        var mirrored = MirroredParameters(factory);
        return factory.ReturnType.GetConstructors()
            .OrderByDescending(ctor => ctor.GetParameters().Length)
            .FirstOrDefault(ctor =>
            {
                var ctorParameters = ctor.GetParameters();
                if (ctorParameters.Length > mirrored.Length) return false;
                var prefixMirrors = ctorParameters.Zip(mirrored[..ctorParameters.Length]).All(pair =>
                    pair.First.Name == pair.Second.Name
                    && pair.First.ParameterType == pair.Second.ParameterType
                    && pair.First.HasDefaultValue == pair.Second.HasDefaultValue
                    && Equals(pair.First.RawDefaultValue, pair.Second.RawDefaultValue));
                if (!prefixMirrors) return false;

                // Parameters past the constructor are the SEMANTIC tail: each one must be optional
                // and land on a public INIT-ONLY property of the same name and type —
                // that is the object initializer the factory body writes, stated as a rule. The
                // constructor itself stays narrow on purpose: its trailing slot is where the
                // compiler lands object initializers, so widening it would break emitted code.
                return mirrored[ctorParameters.Length..].All(parameter =>
                    parameter.HasDefaultValue && TailProperty(factory.ReturnType, parameter) is not null);
            });
    }

    /// <summary>
    /// The init-only property a tail parameter lands on: same name PascalCased, same type, and
    /// INIT-ONLY as claimed — the compiler marks an init accessor with the IsExternalInit modreq.
    /// A plain mutable setter is a different contract; the vocabulary's nodes are immutable after
    /// construction, and a tail that quietly landed on a mutable property would hide that a node
    /// broke the rule.
    /// </summary>
    private static PropertyInfo? TailProperty(Type node, ParameterInfo parameter) =>
        parameter.Name is { Length: > 0 } name
        && node.GetProperty(char.ToUpperInvariant(name[0]) + name[1..])
            is { SetMethod: { IsPublic: true } setter } property
        && property.PropertyType == parameter.ParameterType
        && setter.ReturnParameter.GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit")
            ? property
            : null;

    [Fact]
    public void EveryFactory_MirrorsAPublicConstructorParameterForParameter()
    {
        foreach (var factory in Factories)
        {
            var parameters = factory.GetParameters();
            if (parameters.Length > 0 && parameters[^1].Name == "children")
            {
                parameters[^1].ParameterType.Should().Be(typeof(VisualNode[]),
                    $"{factory.Name}: children is always a VisualNode[] collection expression");
                parameters[^1].HasDefaultValue.Should().BeTrue(
                    $"{factory.Name}: children must be omittable");
            }

            MirroredConstructor(factory).Should().NotBeNull(
                $"{factory.Name} must mirror a public {factory.ReturnType.Name} constructor exactly "
                + "(names, types, defaults), optionally followed by a tail of optional parameters "
                + "that each match an init-only property (name, type) — named arguments carry "
                + "between the two forms");
        }
    }

    /// <summary>
    /// The VALUE question the shape rule above cannot ask (#259): a tail parameter's default is
    /// written in the FACTORY's signature, and the body applies it through an object initializer —
    /// so an omitted argument OVERWRITES whatever the property initializes itself to. Where the two
    /// disagree, <c>X(…)</c> and <c>new X(…)</c> build different nodes from the same arguments,
    /// which is the one promise this surface exists to make.
    ///
    /// <para>
    /// Asked by INVOCATION rather than by reading the signature, because only invocation can see an
    /// initializer: reflection cannot read <c>= SizeValue.Fill</c> at all. So the factory is called
    /// with every optional argument omitted, the mirrored constructor is called with the SAME
    /// prefix, and each tail property is compared across the two. That also keeps a legitimate tail
    /// whose default REPEATS the initializer (<c>WebFrame.Sandbox</c>) — a rule that merely banned
    /// tails over initialized properties would have failed that one and cost the parameter.
    /// </para>
    ///
    /// <para>
    /// Measured on the mutation #251 fenced by hand: adding
    /// <c>SizeValue width = default, SizeValue height = default</c> to <c>UI.WebFrame</c> and
    /// applying them fails HERE, naming both properties.
    /// </para>
    /// </summary>
    [Fact]
    public void OmittingEveryTail_BuildsWhatNewBuilds()
    {
        var disagreements = new List<string>();
        var unmeasured = new List<string>();

        foreach (var factory in Factories)
        {
            // A factory that mirrors nothing is the test above's failure, not this one's.
            if (MirroredConstructor(factory) is not { } constructor) continue;

            var parameters = factory.GetParameters();
            var prefixParameters = constructor.GetParameters();
            var tail = MirroredParameters(factory)[prefixParameters.Length..];
            if (tail.Length == 0) continue;

            if (!FactoryArguments.For(prefixParameters, out var prefix, out var why))
            {
                unmeasured.Add($"{Named(factory)}: {why}");
                continue;
            }

            var arguments = new object?[parameters.Length];
            Array.Copy(prefix, arguments, prefix.Length);
            for (var i = prefix.Length; i < arguments.Length; i++)
                arguments[i] = FactoryArguments.Omitted(parameters[i]);

            object written, made;
            try
            {
                written = constructor.Invoke(prefix);
                made = factory.Invoke(null, arguments)!;
            }
            catch (TargetInvocationException failure)
            {
                unmeasured.Add($"{Named(factory)}: {failure.InnerException?.Message ?? failure.Message}");
                continue;
            }

            foreach (var parameter in tail)
            {
                var property = TailProperty(factory.ReturnType, parameter)!;
                var omitted = property.GetValue(made);
                var byNew = property.GetValue(written);
                if (!Equals(omitted, byNew))
                    disagreements.Add($"{Named(factory)}: omitting `{parameter.Name}` gives "
                        + $"{Show(omitted)} where new gives {Show(byNew)}");
            }
        }

        // Joined rather than asserted as collections: a sweep that found four disagreements must
        // NAME four, and an empty-collection failure prints one of them.
        string.Join("\n", unmeasured).Should().BeEmpty(
            "a factory this check cannot CALL is a factory nobody measured — teach FactoryArguments the "
            + "type it names, rather than letting the surface answer green for a tail no one read");
        string.Join("\n", disagreements).Should().BeEmpty(
            "a tail parameter's default IS the property's default once the factory applies it "
            + "through an object initializer — where the two differ, the same arguments build two "
            + "different nodes and every contract test stays green");
    }

    /// <summary>
    /// The other half of the same question: a tail the factory DECLARES and never assigns. The
    /// shape rule cannot see a body, and <see cref="OmittingEveryTail_BuildsWhatNewBuilds"/> reads
    /// only what an omitted argument leaves behind — so a factory that takes <c>label</c> and
    /// forgets <c>Label = label</c> satisfies both, and drops the argument a screen passed it.
    ///
    /// <para>
    /// Each tail is varied ALONE, from the value omitting it gives, so a failure names the one
    /// parameter that went missing rather than the factory. Mutation: delete any single assignment
    /// from an initializer in <c>UI</c> and this names it.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryTailTheFactoryDeclares_IsAppliedByItsBody()
    {
        var dropped = new List<string>();
        var unmeasured = new List<string>();

        foreach (var factory in Factories)
        {
            if (MirroredConstructor(factory) is not { } constructor) continue;

            var parameters = factory.GetParameters();
            var prefixParameters = constructor.GetParameters();
            var tail = MirroredParameters(factory)[prefixParameters.Length..];
            if (tail.Length == 0) continue;

            if (!FactoryArguments.For(prefixParameters, out var prefix, out var why))
            {
                unmeasured.Add($"{Named(factory)}: {why}");
                continue;
            }

            for (var t = prefix.Length; t < prefix.Length + tail.Length; t++)
            {
                var parameter = parameters[t];
                var omitted = FactoryArguments.Omitted(parameter);

                // A type with ONE inhabitant has no other value to arrive as, so a dropped tail
                // over it is unobservable — there is nothing to measure rather than something
                // unmeasured. Asked of the type rather than listed by name: the day the enum takes
                // a second member, this check starts varying it without anyone remembering to.
                if (parameter.ParameterType.IsEnum && Enum.GetValues(parameter.ParameterType).Length == 1)
                    continue;

                if (!FactoryArguments.TryVary(parameter.ParameterType, omitted, 0, out var varied))
                {
                    unmeasured.Add($"{Named(factory)}.{parameter.Name}: nothing tells a "
                        + $"{parameter.ParameterType.Name} apart from the value omitting it gives");
                    continue;
                }

                var arguments = new object?[parameters.Length];
                Array.Copy(prefix, arguments, prefix.Length);
                for (var i = prefix.Length; i < arguments.Length; i++)
                    arguments[i] = FactoryArguments.Omitted(parameters[i]);
                arguments[t] = varied;

                object made;
                try
                {
                    made = factory.Invoke(null, arguments)!;
                }
                catch (TargetInvocationException failure)
                {
                    unmeasured.Add($"{Named(factory)}.{parameter.Name}: "
                        + (failure.InnerException?.Message ?? failure.Message));
                    continue;
                }

                var landed = TailProperty(factory.ReturnType, parameter)!.GetValue(made);
                if (!Equals(landed, varied))
                    dropped.Add($"{Named(factory)}: `{parameter.Name}` = {Show(varied)} arrives as "
                        + $"{Show(landed)}");
            }
        }

        string.Join("\n", unmeasured).Should().BeEmpty(
            "a tail this check cannot vary is a tail nobody measured — teach FactoryArguments the type it "
            + "names");
        string.Join("\n", dropped).Should().BeEmpty(
            "a factory declaring a parameter it never applies takes an argument from a screen and "
            + "throws it away — silently, since the node it hands back is perfectly valid");
    }

    /// <summary>A factory as a reader finds it: the surface it is on, then its own name.</summary>
    private static string Named(MethodInfo factory) =>
        $"{factory.DeclaringType!.Name}.{factory.Name}";

    private static string Show(object? value) => value switch
    {
        null => "null",
        // Quoted, because the empty string a dropped tail answers with is otherwise invisible in
        // the failure — "arrives as " reads as a truncated message rather than as the finding.
        string text => $"\"{text}\"",
        _ => value.ToString() ?? value.GetType().Name,
    };

    /// <summary>
    /// The vocabulary nodes a consumer can still only reach with <c>new</c> — EMPTY, and kept as
    /// an empty list rather than deleted so a new node with no factory still has somewhere it
    /// would have to be written down.
    ///
    /// <para>
    /// "Trees are written with FACTORIES, never <c>new</c>" is the authoring rule, and until this
    /// test nothing checked it against the vocabulary — <see cref="TheCoreVocabulary_IsCovered"/>
    /// names seven factories every screen starts from and says nothing about the other thirty. A
    /// reviewer caught <c>LiveRegion</c> shipping without one, which is how the six that used to
    /// be listed here came to be counted at all (#251).
    /// </para>
    ///
    /// <para>
    /// Emptying it cost two SHAPE changes rather than six methods, which is what the list was for:
    /// <c>Navigable</c> took its rows FIRST, alone among the vocabulary's multi-child nodes, and
    /// <c>WebFrame</c> had no constructor at all — an address and an inline document as two
    /// nullable strings, with which one won written down in the web realizer's prose. A factory
    /// over that shape would have put the precedence in the SDK's public signature, so the
    /// exclusion became <see cref="WebContent"/> instead.
    /// </para>
    /// </summary>
    private static readonly string[] ReachableOnlyByNew = [];

    /// <summary>
    /// Every node in the vocabulary is reachable through the declarative surface, or is named above.
    /// The set is compared BOTH WAYS on purpose: a node that gains a factory fails here too, so the
    /// list above cannot quietly keep an entry it no longer owns.
    /// </summary>
    [Fact]
    public void EveryVocabularyNode_IsReachableThroughTheSurface()
    {
        var factories = AllFactoriesOf(typeof(FactorySurface))
            .Select(method => method.Name).ToHashSet(StringComparer.Ordinal);

        var missing = typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(VisualNode).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Where(name => !factories.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        missing.Should().BeEquivalentTo(ReachableOnlyByNew,
            "a node with no factory is a node a screen has to reach with `new`, against the one "
            + "authoring rule this surface exists to keep — add the factory, or add the name above "
            + "with the reason it cannot have one");
    }

    [Fact]
    public void TheCoreVocabulary_IsCovered()
    {
        var names = FactoriesOf(typeof(FactorySurface)).Select(m => m.Name).ToHashSet();
        names.Should().Contain(["Column", "Row", "Grid", "Stack", "Box", "Text", "Button"],
            "the layout containers and the flagship atoms are the factories every screen starts from");
    }

    [Fact]
    public void ContainerFactories_ActuallyCollectTheirChildren()
    {
        // Written exactly as a consumer writes it: `using static eQuantic.UI.Components.UI`.
        // children is TRAILING (the container contract), so it is named once a container takes more
        // than a gap — the alignment knobs sit between them.
        var column = Column(Space.S3, children: [Text("a"), Spacer()]);
        column.Gap.Should().Be(Space.S3);
        column.Children.Should().HaveCount(2);

        Row().Children.Should().BeEmpty();
        Grid([GridTrack.Flex(), GridTrack.Flex()], Space.S2, children: [Text("x")])
            .Children.Should().HaveCount(1);
        Stack(Alignment.Center, children: [Text("y")]).Children.Should().HaveCount(1);
    }
}
