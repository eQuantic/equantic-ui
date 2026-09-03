using eQuantic.UI.Primitives;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// The builder a Photon app is configured through — the same shape as
/// <c>WebApplicationBuilder</c>, deliberately. A .NET developer already knows that services go in
/// <see cref="Services"/>, that <see cref="Configuration"/> has read appsettings.json and the
/// command line, and that <c>Build()</c> hands back something to <c>Run()</c>. Learning a second
/// shape for the same idea is a cost the framework should not be charging.
/// </summary>
public sealed class PhotonApplicationBuilder
{
    private readonly HostApplicationBuilder _host;

    internal PhotonApplicationBuilder(string[] args)
    {
        Args = args;
        // The real host builder does the work: appsettings.json, appsettings.{Environment}.json,
        // user secrets in Development, environment variables and the command line, in that order.
        _host = Host.CreateApplicationBuilder(args);
        _host.Services.AddOptions<PhotonOptions>()
            .Bind(_host.Configuration.GetSection(PhotonOptions.SectionName));
    }

    /// <summary>The arguments the process was started with — UIKit wants them back.</summary>
    public string[] Args { get; }

    /// <summary>Where an app registers what its components will ask for.</summary>
    public IServiceCollection Services => _host.Services;

    /// <summary>appsettings.json, the environment and the command line, already merged.</summary>
    public IConfigurationManager Configuration => _host.Configuration;

    /// <summary>Development, Staging, Production — and the content root beneath them.</summary>
    public IHostEnvironment Environment => _host.Environment;

    public ILoggingBuilder Logging => _host.Logging;

    /// <summary>
    /// The app's own settings, in code. Whatever is set here wins over configuration, because a
    /// value written in the program is a decision and a value in a file is a default.
    /// </summary>
    public PhotonApplicationBuilder Configure(Action<PhotonOptions> configure)
    {
        Services.PostConfigure(configure);
        return this;
    }

    /// <summary>The theme the whole tree resolves against — the one setting no app can skip.</summary>
    public PhotonApplicationBuilder UseTheme(IAppTheme theme) => Configure(options => options.Theme = theme);

    /// <summary>
    /// What this app asks of the device — the camera, the photo library, the sensors. Stated here
    /// rather than in any platform file: the generator reads these calls and writes the Info.plist
    /// keys and the Android permissions from them.
    /// </summary>
    public PhotonCapabilitiesBuilder Capabilities { get; } = new();

    /// <summary>
    /// What this app needs the SYSTEM to permit when it ships signed — the release-time counterpart
    /// of <see cref="Capabilities"/>, and stated the same way: in C#, on the builder, never in a
    /// plist. Only your own code's needs; what the .NET runtime needs under the hardened runtime
    /// the SDK declares by itself.
    /// </summary>
    public PhotonEntitlementsBuilder Entitlements { get; } = new();

    /// <summary>
    /// What the system reads ABOUT this app before it runs — its copyright line, its category, the
    /// oldest macOS it starts on, whether it takes a Dock tile, the URLs it answers to. The same
    /// journey as the other two: a call here, an assembly declaration from the generator, and the
    /// SDK writes the Info.plist.
    /// </summary>
    public PhotonBundleBuilder Bundle { get; } = new();

    public PhotonApplication Build()
    {
        // LAST, so anything the app registered itself already sits in the collection and the
        // shell's TryAdd steps aside. A fake photo library in a test wins over the real one.
        // The declarations reach run time too, so an app can explain itself before it asks.
        _host.Services.Configure<PhotonOptions>(options => options.Declared = Capabilities.Declared);
        // The light/dark hand, present on every head unless the app brought its own. The runner
        // attaches it to the window's host after Build — services exist before any window does.
        _host.Services.TryAddSingleton<IThemeController, PhotonThemeController>();
        // TIME, the same way: a component that advances itself asks for IClock and gets .NET's own
        // timer here. Nothing about it is Photon's — the engine's loop already presents when state
        // changed, so a tick has nothing to schedule with the renderer.
        _host.Services.TryAddSingleton<IClock, PhotonClock>();
        // The culture hand, same shape: the runner resolves the PLATFORM's locale and applies it
        // here before the first frame; an app that wants to force a culture applies over it.
        //
        // TWO registrations, one instance. The shells resolve the CONCRETE type (they hand it real
        // CultureInfo objects, which only this side can materialize), while components ask for the
        // INTERFACE — the write-once door, which speaks BCP-47 names because that is the currency
        // the browser also has. Registering only the concrete type is what a CultureSwitcher in a
        // window resolved to null against: the switch drew, and switched nothing.
        _host.Services.TryAddSingleton<PhotonCultureController>();
        _host.Services.TryAddSingleton<eQuantic.UI.Primitives.ICultureController>(
            services => services.GetRequiredService<PhotonCultureController>());
        PhotonApplication.RegisterCapabilities(_host.Services);
        return new PhotonApplication(_host.Build(), Args);
    }
}
