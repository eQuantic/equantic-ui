using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A subscription from client code that calls an event's accessor kept on the server is refused (EQ2010, #1445),
/// written plainly or through C# 14's null-conditional <c>?.</c>: no twin holds the accessor, and the
/// subscription would call a method the browser does not have. The null-conditional one went through the guarded
/// assignment, which asked nothing of the event, and built.
/// </summary>
public sealed class AServerOnlyEventAccessorTests
{
    private static Solution App(string subscription) => new Solution().App("App",
        globalUsings: SharedLibrary.Factories,
        files:
        [
            ("Model/Signal.cs",
                """
                using System;
                using eQuantic.UI.Primitives;
                namespace App.Model;

                public sealed record Signal(int Id)
                {
                    private Action? _fired;
                    public event Action Fired { [ServerOnly] add => _fired += value; remove => _fired -= value; }
                }
                """),
            ("Screens/Home.cs",
                $$"""
                using eQuantic.UI.Primitives;
                using App.Model;
                namespace App.Screens;

                public sealed class Home : StatelessComponent
                {
                    public void Listen(Signal? s) => {{subscription}};

                    public override VisualNode Build(ComponentContext context) =>
                        Text("home", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                }
                """),
        ]);

    [Theory]
    [InlineData("s!.Fired += () => { }")]
    [InlineData("s?.Fired += () => { }")]
    public void ASubscriptionToAServerOnlyAccessor_FailsTheBuild_WhereItIsWritten(string subscription)
    {
        using var solution = App(subscription);
        var built = solution.Build();

        built.Failed.Should().BeTrue("the add accessor is kept on the server, and no twin holds it");
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(")
            .And.Contain("error EQ2010").And.Contain("'App.Model.Signal.Fired'").And.Contain("'add' accessor");
    }

    [Fact]
    public void ASubscriptionThroughTheAccessorThatCrosses_Builds()
    {
        using var solution = App("s?.Fired -= () => { }");
        var built = solution.Build();

        built.Failed.Should().BeFalse($"the remove accessor crosses:\n{built.Errors}");
    }
}
