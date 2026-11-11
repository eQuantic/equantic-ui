using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>
/// An app that takes uploads, on a test server: the real upload endpoint, the real Server Actions
/// middleware and the real cleaner, over a folder the test names, so a second app started over the same
/// folder is the same server after a restart.
/// </summary>
internal sealed class UploadsApp : IAsyncDisposable
{
    /// <summary>3D models, up to a mebibyte.</summary>
    public static readonly UploadPolicy Models =
        new("models", [new FileFilter("3D models", "stl", "obj", "3mf")], maxSize: 1024 * 1024);

    /// <summary>Any file, of at most ten bytes.</summary>
    public static readonly UploadPolicy Small = new("small", [], maxSize: 10);

    /// <summary>Any file, from a signed-in person only.</summary>
    public static readonly UploadPolicy Members = new("members", [], maxSize: 1024, requireSignedIn: true);

    private readonly WebApplication _app;

    private UploadsApp(WebApplication app, string folder, FakeTimeProvider time)
    {
        _app = app;
        Folder = folder;
        Time = time;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    /// <summary>The folder the uploads are kept in.</summary>
    public string Folder { get; }

    /// <summary>The app's clock, which the test moves.</summary>
    public FakeTimeProvider Time { get; }

    public ReceivedLog Log => Services.GetRequiredService<ReceivedLog>();

    public TusBrowser Browser(string? user = null, string? origin = null) => new(Client, user, origin);

    public static async Task<UploadsApp> StartAsync(string folder, FakeTimeProvider? time = null,
        IReadOnlyDictionary<string, string?>? configuration = null, string? webRoot = null, string? contentRoot = null,
        string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            EnvironmentName = environment,
            // An empty one leaves the web root to ASP.NET Core's default, which is unset while wwwroot does not exist.
            WebRootPath = webRoot == "" ? null : webRoot ?? Directory.CreateTempSubdirectory("eq-uploads-web-").FullName,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{UploadsOptions.SectionName}:{nameof(UploadsOptions.Folder)}"] = folder,
        });
        if (configuration is not null) builder.Configuration.AddInMemoryCollection(configuration);
        var clock = time ?? new FakeTimeProvider(DateTimeOffset.UtcNow);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<ReceivedLog>();
        builder.Services.AddAuthentication(HeaderUser.Name)
            .AddScheme<AuthenticationSchemeOptions, HeaderUser>(HeaderUser.Name, null);
        builder.Services.AddUI(options => options
            .ScanAssembly(typeof(UploadsApp).Assembly)
            .UseUploads(uploads => uploads.Accept(Models).Accept(Small).Accept(Members)));
        var app = builder.Build();
        app.UseServerActions();
        app.MapUI();
        await app.StartAsync();
        return new UploadsApp(app, folder, clock);
    }

    /// <summary>The files the folder holds, by name.</summary>
    public IReadOnlyList<string> FilesInFolder() =>
        Directory.EnumerateFiles(Folder).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
