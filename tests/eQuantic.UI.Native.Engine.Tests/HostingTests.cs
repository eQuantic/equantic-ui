using System.Reflection;
using eQuantic.UI.Material;
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

    [Fact]
    public void TheThemeReachesTheAppThroughUseThemeAlone()
    {
        PhotonApplication.CreateBuilder().Build().Theme.Should().BeSameAs(PhotonTheme.Instance,
            "an app that chooses no theme draws with Photon's own");

        var material = MaterialTheme.FromSeed(Color.FromRgb(0x67, 0x50, 0xA4));
        var builder = PhotonApplication.CreateBuilder(["--Photon:Theme", "Material"]);
        builder.UseTheme(material);

        builder.Build().Theme.Should().BeSameAs(material, "a theme is a C# object, and configuration names none");
    }

    /// <summary>
    /// The options the configuration binder is handed reach no theme (#1398). Its generator walks every
    /// type the bound options reach, the theme's slots included, and a record member a slot holds with a
    /// non-positional <c>init</c> (a <see cref="TypeStyle"/>) made it write <c>Fluid = Fluid;</c>, which
    /// does not compile: so a slot could carry no type style. Walked here as the generator walks it, so a
    /// theme put back on the options fails by name.
    /// </summary>
    [Fact]
    public void TheBoundOptions_ReachNoTheme()
    {
        var reached = new HashSet<Type>();
        var pending = new Queue<Type>([typeof(PhotonOptions)]);
        while (pending.TryDequeue(out var type))
        {
            if (!reached.Add(type)) continue;
            var next = type.IsGenericType ? type.GetGenericArguments()
                : type.IsArray ? [type.GetElementType()!]
                : type.Namespace?.StartsWith("eQuantic", StringComparison.Ordinal) == true
                    ? type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.PropertyType).ToArray()
                    : [];
            foreach (var reachedType in next) pending.Enqueue(Nullable.GetUnderlyingType(reachedType) ?? reachedType);
        }

        reached.Where(type => typeof(IAppTheme).IsAssignableFrom(type) || type == typeof(TypeStyle))
            .Should().BeEmpty("the theme is chosen with UseTheme, and the binder walks nothing it holds");
    }

    /// <summary>
    /// The Windows shell says which operating system it exists for. That one fact is what lets a
    /// Mac that carries it beside its own shell — this very test project, or a publish for more
    /// than one desktop — find ONE runner instead of refusing to start with two. The SDK writes the
    /// attribute only for platform-specific target frameworks, so the shell states it in source;
    /// this pins that it did.
    /// </summary>
    [Fact]
    public void TheWindowsShellDeclaresItsOperatingSystem() =>
        typeof(eQuantic.UI.Native.Shell.Windows.WindowsPhotonRunner).Assembly
            .GetCustomAttributes<System.Runtime.Versioning.SupportedOSPlatformAttribute>()
            .Should().Contain(platform => platform.PlatformName.StartsWith("windows", StringComparison.Ordinal),
                "the host treats a shell declared for another OS as not there");

    /// <summary>The Mac's shell says so too — by name, because this project reaches it only through
    /// the sample and only on a Mac, where the sample's SDK picked it.</summary>
    [MacFact]
    public void TheMacShellDeclaresItsOperatingSystem() =>
        Assembly.Load(new AssemblyName("eQuantic.UI.Native.Shell.MacOS"))
            .GetCustomAttributes<System.Runtime.Versioning.SupportedOSPlatformAttribute>()
            .Should().Contain(platform => platform.PlatformName.StartsWith("macos", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// AppKit is a main-thread framework, and xUnit runs nowhere near the main thread — so this
    /// is the message a developer gets for calling Run() from a worker (a test, a Task, a
    /// background service). It used to be an uncatchable ObjC exception that took the whole
    /// process with it, which told nobody anything.
    /// </summary>
    [MacFact]
    public void RunningOffTheMainThreadSaysSo_InsteadOfKillingTheProcess()
    {
        var app = PhotonApplication.CreateBuilder().Build();

        var run = () => app.UseRoot(() => new Text("x")).Run();
        run.Should().Throw<InvalidOperationException>().WithMessage("*MAIN thread*");
    }
}
