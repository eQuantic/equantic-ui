using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A member of a vocabulary node that only a realizer asks has no runtime twin, so a page that reads
/// it is refused at the build (#677): <c>Text.IsHiddenFromReaders</c> would read <c>undefined</c> in the
/// browser, where .NET answers a bool.
/// </summary>
public sealed class AHostOnlyNodeMemberTests
{
    [Fact]
    public void APageThatReadsTextIsHiddenFromReaders_FailsTheBuild()
    {
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories, files:
        [
            ("Screens/Home.cs",
                """
                using eQuantic.UI.Primitives;
                namespace App.Screens;

                public sealed class Home : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context)
                    {
                        var heading = new Text("Next", TypeRole.Caption) { ExcludeFromSemantics = true };
                        return Text(heading.IsHiddenFromReaders ? "hidden" : "read", TypeRole.BodyM);
                    }
                }
                """),
        ]);

        var built = solution.Build();

        built.Failed.Should().BeTrue("a realizer's question has no twin in the browser");
        built.ErrorsIn("App").Should().Contain(error => error.Contains("error EQ2010") && error.Contains("IsHiddenFromReaders"));
    }
}
