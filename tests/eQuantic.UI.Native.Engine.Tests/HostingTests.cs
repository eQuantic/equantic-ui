using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The host a Photon app is built through — the same shape as WebApplicationBuilder, because a .NET
/// developer already knows it. What is worth testing is that the familiar parts really are the
/// familiar parts: services resolve, configuration reaches a component, and settings written in the
/// program beat settings written in a file.
/// </summary>
public class HostingTests
{
    private interface IGreeting { string Text { get; } }

    private sealed class Greeting : IGreeting { public string Text => "Good morning"; }

    private sealed class Screen(IGreeting greeting, IConfiguration configuration) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text($"{greeting.Text}, {configuration["who"]}");
    }

    [Fact]
    public void AComponentTakesItsDependenciesThroughItsConstructor()
    {
        // The point of the whole exercise: a screen is an ordinary class the container builds.
        var builder = PhotonApplication.CreateBuilder(["--who", "Ana"]);
        builder.Services.AddSingleton<IGreeting, Greeting>();
        var app = builder.Build();

        // UseRoot names the screen; Run would hand it to a shell. What this asserts is the tree
        // the shell would have been handed.
        var root = (Screen)app.UseRoot<Screen>().Root();
        var text = (Text)root.Build(new ComponentContext(PhotonTheme.Instance));

        text.Content.Should().Be("Good morning, Ana");
    }

    [Fact]
    public void TheCommandLineReachesConfiguration_LikeEveryOtherDotNetProgram()
    {
        var app = PhotonApplication.CreateBuilder(["--screen", "cards"]).Build();
        app.Configuration["screen"].Should().Be("cards");
    }

    [Fact]
    public void OptionsBindFromConfiguration_AndTheProgramStillWins()
    {
        // A value in a file is a default; a value written in the program is a decision.
        var builder = PhotonApplication.CreateBuilder(["--Photon:MaxFrames", "120", "--Photon:Title", "From config"]);
        builder.Configure(photon => photon.Title = "From code");
        var app = builder.Build();

        app.Options.MaxFrames.Should().Be(120, "nothing in the program overrode it");
        app.Options.Title.Should().Be("From code");
    }

    /// <summary>
    /// AppKit is a main-thread framework, and xUnit runs nowhere near the main thread — so this
    /// is the message a developer gets for calling Run() from a worker (a test, a Task, a
    /// background service). It used to be an uncatchable ObjC exception that took the whole
    /// process with it, which told nobody anything.
    /// </summary>
    [Fact]
    public void RunningOffTheMainThreadSaysSo_InsteadOfKillingTheProcess()
    {
        var app = PhotonApplication.CreateBuilder().Build();

        var run = () => app.UseRoot(() => new Text("x")).Run();
        run.Should().Throw<InvalidOperationException>().WithMessage("*MAIN thread*");
    }
}
