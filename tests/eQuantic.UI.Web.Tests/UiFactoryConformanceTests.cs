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
/// `X(…)` unchanged. Containers may append one trailing `children` parameter. And because the
/// class transpiles to a JS twin, no factory may overload — JS methods cannot.
/// </summary>
public class UiFactoryConformanceTests
{
    private static readonly MethodInfo[] AllFactories =
        typeof(FactorySurface).GetMethods(BindingFlags.Public | BindingFlags.Static);

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
    /// </summary>
    private static readonly HashSet<string> NamedFactories = new() { "Gap", "DotBadge" };

    private static readonly MethodInfo[] Factories =
        AllFactories.Where(m => !NamedFactories.Contains(m.Name)).ToArray();

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
            var factory = AllFactories.Single(m => m.Name == name);
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
        var reachableByName = AllFactories
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
        // per name whatever the C# name rule says.
        AllFactories.GroupBy(m => m.Name).Where(g => g.Count() > 1).Select(g => g.Key)
            .Should().BeEmpty("JS class methods cannot overload — one canonical signature per node");
    }

    [Fact]
    public void EveryFactory_MirrorsAPublicConstructorParameterForParameter()
    {
        foreach (var factory in Factories)
        {
            var parameters = factory.GetParameters();
            var hasChildren = parameters.Length > 0 && parameters[^1].Name == "children";
            var mirrored = hasChildren ? parameters[..^1] : parameters;

            if (hasChildren)
            {
                parameters[^1].ParameterType.Should().Be(typeof(VisualNode[]),
                    $"{factory.Name}: children is always a VisualNode[] collection expression");
                parameters[^1].HasDefaultValue.Should().BeTrue(
                    $"{factory.Name}: children must be omittable");
            }

            var match = factory.ReturnType.GetConstructors().Any(ctor =>
            {
                var ctorParameters = ctor.GetParameters();
                if (ctorParameters.Length != mirrored.Length) return false;
                return ctorParameters.Zip(mirrored).All(pair =>
                    pair.First.Name == pair.Second.Name
                    && pair.First.ParameterType == pair.Second.ParameterType
                    && pair.First.HasDefaultValue == pair.Second.HasDefaultValue
                    && Equals(pair.First.RawDefaultValue, pair.Second.RawDefaultValue));
            });
            match.Should().BeTrue(
                $"{factory.Name} must mirror a public {factory.ReturnType.Name} constructor exactly "
                + "(names, types, defaults) — named arguments carry between the two forms");
        }
    }

    [Fact]
    public void TheCoreVocabulary_IsCovered()
    {
        var names = Factories.Select(m => m.Name).ToHashSet();
        names.Should().Contain(["Column", "Row", "Grid", "Stack", "Box", "Text", "Button"],
            "the layout containers and the flagship atoms are the factories every screen starts from");
    }

    [Fact]
    public void ContainerFactories_ActuallyCollectTheirChildren()
    {
        // Written exactly as a consumer writes it: `using static eQuantic.UI.Components.UI`.
        var column = Column(Space.S3, [Text("a"), Spacer()]);
        column.Gap.Should().Be(Space.S3);
        column.Children.Should().HaveCount(2);

        Row().Children.Should().BeEmpty();
        Grid([GridTrack.Flex(), GridTrack.Flex()], Space.S2, children: [Text("x")])
            .Children.Should().HaveCount(1);
        Stack(Alignment.Center, [Text("y")]).Children.Should().HaveCount(1);
    }
}
