using System.Reflection;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// A built Photon app. <c>CreateBuilder</c>, configure, <c>Build</c>, <c>Run</c> — the shape of
/// every modern .NET program, so the only new thing an author has to learn here is the component
/// they are drawing.
/// <code>
/// var builder = PhotonApplication.CreateBuilder(args);
/// builder.Services.AddSingleton&lt;IAccounts, Accounts&gt;();
/// var app = builder.Build();
/// app.Run&lt;WalletApp&gt;();
/// </code>
/// </summary>
public sealed class PhotonApplication
{
    private readonly IHost _host;

    internal PhotonApplication(IHost host, string[] args)
    {
        _host = host;
        Args = args;
        Options = host.Services.GetRequiredService<IOptions<PhotonOptions>>().Value;
    }

    public static PhotonApplicationBuilder CreateBuilder(string[]? args = null) => new(args ?? []);

    /// <summary>
    /// This app's program, registered by a generated module initializer — which runs when the app's
    /// assembly loads, and is therefore the only moment Android offers: the system launches an
    /// Activity there and no <c>Main</c> ever runs.
    /// </summary>
    public static Func<string[], PhotonApplication>? Program { get; set; }

    /// <summary>
    /// The app, from the program that describes it. Nothing is looked up at run time: the generator
    /// saw the compilation and wrote a direct call to this app's <c>CreateApp</c>.
    /// </summary>
    public static PhotonApplication Create(string[]? args = null) =>
        Program?.Invoke(args ?? [])
        ?? throw new InvalidOperationException(
            "No program. A Photon app describes itself with a public static "
            + "PhotonApplication CreateApp(string[] args) — by convention on a public partial class "
            + "Program, whose other half the SDK generates.");

    public IServiceProvider Services => _host.Services;

    public IConfiguration Configuration => _host.Services.GetRequiredService<IConfiguration>();

    public PhotonOptions Options { get; }

    public string[] Args { get; }

    /// <summary>
    /// The tree, built fresh. It is a FACTORY rather than an instance because a shell may need to
    /// build it after the platform is up — a tree constructed at process start would measure text
    /// through a font stack that is not loaded yet.
    /// </summary>
    public Func<VisualNode> Root { get; private set; } = () =>
        throw new InvalidOperationException("No root component. Call app.Run<TComponent>().");

    /// <summary>
    /// The screen this app opens on, resolved from the container — so a component takes its
    /// dependencies through its constructor like anything else in .NET.
    /// </summary>
    public PhotonApplication UseRoot<TRoot>() where TRoot : VisualNode
    {
        Root = () => ActivatorUtilities.CreateInstance<TRoot>(Services);
        return this;
    }

    /// <summary>The screen, built by the caller rather than by the container.</summary>
    public PhotonApplication UseRoot(Func<VisualNode> root)
    {
        Root = root;
        return this;
    }

    /// <summary>Sets the screen and hands control to the platform — the usual last line.</summary>
    public void Run<TRoot>() where TRoot : VisualNode => UseRoot<TRoot>().Run();

    public void Run(Func<VisualNode> root) => UseRoot(root).Run();

    /// <summary>
    /// Hands control to the platform. Which platform is not a question the app answers: the SDK
    /// referenced exactly one shell for the framework being built, and that shell says so.
    /// </summary>
    public void Run()
    {
        // Where a component in the MIDDLE of the tree finds a capability. Armed for the process,
        // not per render: a native app has one container and one surface, and the tree is rebuilt
        // on it from the first frame to the last.
        eQuantic.UI.Primitives.CapabilityScope.Current = Services.GetService;
        FindRunner().Run(this);
    }

    /// <summary>
    /// Lets every shell that shipped register what the device can do, before the container is built.
    /// A shell that offers nothing is not an error — a head with no camera simply has none to offer,
    /// and the app finds the capability reporting itself unavailable rather than missing.
    /// </summary>
    internal static void RegisterCapabilities(IServiceCollection services)
    {
        foreach (var declaration in ShellAssemblies()
            .SelectMany(assembly => assembly.GetCustomAttributes<PhotonCapabilitiesAttribute>())
            .Select(attribute => attribute.ProviderType)
            .Distinct())
        {
            ((IPhotonCapabilities)Activator.CreateInstance(declaration)!).Register(services);
        }
    }

    private static IPhotonRunner FindRunner()
    {
        var declarations = ShellAssemblies()
            .SelectMany(assembly => assembly.GetCustomAttributes<PhotonRunnerAttribute>())
            .Select(attribute => attribute.RunnerType)
            .Distinct()
            .ToArray();

        return declarations.Length switch
        {
            1 => (IPhotonRunner)Activator.CreateInstance(declarations[0])!,
            0 => throw new InvalidOperationException(
                "No platform shell is referenced. The SDK adds one per target framework — check that "
                + "this project builds through eQuantic.UI.Sdk.Native."),
            _ => throw new InvalidOperationException(
                $"More than one platform shell is referenced ({string.Join(", ", declarations.Select(t => t.Name))}). "
                + "An app runs on the device its target framework names, and only that one."),
        };
    }

    /// <summary>
    /// Every shell that shipped with this app. NOT the entry assembly's reference list: an app never
    /// names its shell in code, so the compiler drops the reference and the list comes back empty.
    /// What actually ships is what is in the folder beside the program — which on a phone is the
    /// app bundle, and on a desktop the output directory.
    /// </summary>
    private static IEnumerable<Assembly> ShellAssemblies()
    {
        const string prefix = "eQuantic.UI.Native.Shell.";
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith(prefix, StringComparison.Ordinal) == true)
            .ToList();

        var seen = loaded.Select(assembly => assembly.GetName().Name).ToHashSet(StringComparer.Ordinal);
        foreach (var assembly in loaded) yield return assembly;

        if (!Directory.Exists(AppContext.BaseDirectory)) yield break;
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, prefix + "*.dll"))
        {
            if (!seen.Add(Path.GetFileNameWithoutExtension(path))) continue;
            Assembly shell;
            try { shell = Assembly.LoadFrom(path); }
            catch (BadImageFormatException) { continue; }
            catch (FileLoadException) { continue; }
            yield return shell;
        }
    }
}
