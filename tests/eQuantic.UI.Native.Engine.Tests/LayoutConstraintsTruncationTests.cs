using System.Reflection;
using eQuantic.UI.Native.Framework;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// <see cref="LayoutConstraints.Truncating"/> travels on the constraints, and this asks EVERY
/// restating helper whether it carries it — by reflection, because the one that dropped it did so
/// silently and I wrote the bug it catches.
///
/// <para>
/// A positional record's `new(…)` constructor does not see an `init` property, so
/// <c>new(Width.WithMax(w), Height.WithMax(h))</c> compiled perfectly and quietly reset the flag to
/// false. The symptom was one wrapper out of twenty — <c>SafeArea</c>, the only door that restates
/// its child's room with <c>WithMax</c> rather than <c>ForChild</c> — whose text wrapped to eight
/// lines while the other nineteen were cut to one.
/// </para>
///
/// <para>
/// That is the failure mode <see cref="LayoutConstraints"/>'s own doc argues against: "an invariant
/// maintained by remembering is one that breaks at the fourteenth call site". A helper added later
/// with `new(…)` breaks it again, and nothing in the compiler minds — so this asks all of them
/// rather than the three that exist today.
/// </para>
/// </summary>
public class LayoutConstraintsTruncationTests
{
    /// <summary>Every public instance method that hands back constraints — found, not listed.</summary>
    private static IEnumerable<MethodInfo> RestatingHelpers() =>
        typeof(LayoutConstraints)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(LayoutConstraints) && !m.IsSpecialName)
            .OrderBy(m => m.Name, StringComparer.Ordinal);

    private static object?[] Arguments(MethodInfo method) => method.GetParameters()
        .Select(p => p.ParameterType == typeof(float) ? (object)10f
            : p.ParameterType == typeof(bool) ? false
            : p.ParameterType == typeof(StretchKind) ? StretchKind.Flex
            : throw new InvalidOperationException(
                $"{method.Name} takes a {p.ParameterType.Name} this test does not know how to supply"))
        .ToArray();

    [Fact]
    public void EveryRestatingHelper_CarriesTheCut_ExceptTheOneThatMeasuresAChildForItsOwnSake()
    {
        var cut = LayoutConstraints.Of(100, 100).Truncated();
        cut.Truncating.Should().BeTrue("otherwise this test asserts nothing at all");

        foreach (var helper in RestatingHelpers())
        {
            var result = (LayoutConstraints)helper.Invoke(cut, Arguments(helper))!;

            if (helper.Name == nameof(LayoutConstraints.ForChild))
            {
                result.Truncating.Should().BeFalse(
                    "ForChild is how a container measures a child for its own sake — a Row nested "
                    + "inside a cut wrapper must wrap its own text, not ellipsize it");
                continue;
            }

            result.Truncating.Should().BeTrue(
                $"{helper.Name} restates one fact and keeps the rest; a cut travels through a "
                + "layout-transparent wrapper to reach the text it is cutting, and three of those "
                + "wrappers reach their child through Inline() while SafeArea uses WithMax()");
        }
    }

    [Fact]
    public void TheHelpersAreFoundRatherThanListed()
    {
        // Guards the guard: a reflection query that silently matched nothing would pass the test
        // above without asking a single question.
        RestatingHelpers().Select(m => m.Name).Should().Contain(
            [nameof(LayoutConstraints.ForChild), nameof(LayoutConstraints.WithMax),
             nameof(LayoutConstraints.Inline), nameof(LayoutConstraints.Released)]);
    }
}
