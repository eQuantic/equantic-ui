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
    private static readonly MethodInfo[] Factories =
        typeof(FactorySurface).GetMethods(BindingFlags.Public | BindingFlags.Static);

    [Fact]
    public void EveryFactory_IsNamedExactlyLikeItsReturnType()
    {
        foreach (var factory in Factories)
            factory.Name.Should().Be(factory.ReturnType.Name,
                "a factory is its type minus `new` — any other name breaks the mental model");
    }

    [Fact]
    public void NoFactoryOverloads_TheTwinIsJavaScript()
    {
        Factories.GroupBy(m => m.Name).Where(g => g.Count() > 1).Select(g => g.Key)
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
