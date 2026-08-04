using eQuantic.UI.Primitives;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    public PhotonApplication Build()
    {
        // LAST, so anything the app registered itself already sits in the collection and the
        // shell's TryAdd steps aside. A fake photo library in a test wins over the real one.
        PhotonApplication.RegisterCapabilities(_host.Services);
        return new PhotonApplication(_host.Build(), Args);
    }
}
