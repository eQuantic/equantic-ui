using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Whether a class is a component is asked of the model, by one rule, wherever eqc asks it (#1839). A construction
/// site and the field-naming rule walked the bases for the names <c>StatelessComponent</c>, <c>StatefulComponent</c>
/// and <c>UiComponent</c> in any namespace, so an app's own class named <c>UiComponent</c>, which is no component,
/// made every class over it one there, and its field kept a name a member of the same name took, which the build
/// refused (EQ1007). Built as an app builds it, it answers as .NET does. The construction site's walk changed nothing a
/// run can see (measured: an argument of the same class's dependency is kept with either rule), and asks the same rule
/// now so the two never disagree.
/// </summary>
public class ComponentRuleConformanceTests
{
    // A namespace block, not a file-scoped one: the harness appends the cases' class to the source, and a file-scoped
    // namespace would take it in.
    private const string Field = """
        namespace Shop
        {
            // The app's own class of the vocabulary's name: no component, and nothing over it is one.
            public class UiComponent { }

            public sealed class Card : UiComponent
            {
                private int area = 3;
                public int Area => area * 2;
            }
        }
        """;

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassOverTheAppsOwnUiComponent_MovesAFieldAMemberNamesAlike(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Field, typeAnnotations, ("area", "return new Shop.Card().Area;"));
}
