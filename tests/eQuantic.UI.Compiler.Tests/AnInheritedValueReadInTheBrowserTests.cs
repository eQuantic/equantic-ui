using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An app's component that reads an inherited value compiles for the browser (#1095): the inherited
/// component is a vocabulary node an app subclasses, and <c>context.DependOn&lt;T&gt;()</c> and
/// <c>Find&lt;T&gt;()</c> reach the runtime's context with <c>T</c>'s twin, which the browser's position
/// looks the value up by (#1395). Built as an app is built, so the model binds both calls to the
/// context's members and no strategy claims one by its name (a list's <c>Find</c>).
/// </summary>
public class AnInheritedValueReadInTheBrowserTests
{
    private const string Score =
        """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class Score(int value, VisualNode child) : InheritedComponent(child)
        {
            public int Value { get; } = value;

            protected override bool UpdateShouldNotify(InheritedComponent old) => ((Score)old).Value != Value;
        }
        """;

    private const string Reader =
        """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class Reader : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                var score = context.DependOn<Score>();
                var once = context.Find<Score>();
                return Text($"{score?.Value} {once?.Value}", TypeRole.BodyM);
            }
        }
        """;

    [Fact]
    public void AComponentThatReadsAnInheritedValue_CompilesForTheBrowser()
    {
        using var solution = new Solution()
            .App("Shop", globalUsings: SharedLibrary.Factories, files: [("Score.cs", Score), ("Components/Reader.cs", Reader)]);

        var built = solution.Build();

        built.Errors.Split('\n').Where(line => line.Contains(": error ", StringComparison.Ordinal))
            .Should().BeEmpty("an app reads its inherited values on every target");
        // With the type's twin, which is what the browser looks the value up by (#1395): the type
        // argument itself does not reach JavaScript, and a lookup handed nothing found nothing.
        var reader = built.Module("Reader");
        reader.Should().Contain("context.dependOn(Score)").And.Contain("context.find(Score)");
        reader.Should().MatchRegex(@"import \{[^}]*\bScore\b[^}]*\} from", "the twin it names is imported");
    }
}
