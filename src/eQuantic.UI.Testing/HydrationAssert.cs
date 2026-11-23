using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Testing;

/// <summary>
/// A served page, hydrated in a test (#1775): the app's server draws it, the page's own scripts run against
/// that markup in a headless DOM, and the test fails where the browser would draw it otherwise.
/// <para>
/// A real site's pages passed every test on the server's HTML and broke only once they hydrated: a label the
/// browser drew blank, an icon size the browser refused, a string a static field kept in the first request's
/// language, a path the browser's router gave to another page than the server drew. Each was found by
/// opening the page in a browser. <see cref="PageAsync{TEntryPoint}"/> finds them in <c>dotnet test</c>.
/// </para>
/// <para>
/// The page runs in happy-dom, under the embedded bun the app was built with, which the package's build
/// targets find beside the test assembly: a test project references the app and this package, and installs
/// nothing else.
/// </para>
/// </summary>
public static class HydrationAssert
{
    /// <summary>The file the package's build targets write beside the test assembly, naming the bun.</summary>
    internal const string BunFile = "eQuantic.UI.Testing.bun";

    private const string HarnessResource = "eQuantic.UI.Testing.hydration-harness.js";

    /// <summary>
    /// Serves the app on a port of this machine, draws <paramref name="path"/> as a browser would, and throws a
    /// <see cref="HydrationException"/> naming what went wrong when the page throws, when the browser drew
    /// it differently from the server, or when the browser's router chose another page for it.
    /// <para>
    /// The factory is served by Kestrel, since a page's scripts are fetched over a socket: hand it to this
    /// method before anything starts it in memory (a client it created), or call <c>UseKestrel()</c> on it
    /// first. One factory serves every page a test asks about.
    /// </para>
    /// </summary>
    /// <typeparam name="TEntryPoint">The app's entry point.</typeparam>
    /// <param name="factory">The app under test.</param>
    /// <param name="path">The page's path, as a browser asks for it (<c>/catalogue</c>).</param>
    /// <param name="options">How long to wait and how wide the window is; the defaults otherwise.</param>
    /// <param name="cancellationToken">Stops waiting for the page.</param>
    /// <returns>The report of a page that hydrated as the server drew it.</returns>
    /// <exception cref="HydrationException">The page did not hydrate as the server drew it.</exception>
    public static Task<HydrationReport> PageAsync<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory, string path,
        HydrationOptions? options = null, CancellationToken cancellationToken = default)
        where TEntryPoint : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(path);
        return JudgedAsync(new Uri(ServedAddress(factory), path), options, cancellationToken);
    }

    private static async Task<HydrationReport> JudgedAsync(Uri address, HydrationOptions? options, CancellationToken cancellationToken)
    {
        var report = await DrawAsync(address, options ?? new HydrationOptions(), cancellationToken).ConfigureAwait(false);
        return report.Passed ? report : throw new HydrationException(report);
    }

    /// <summary>The address the factory's app answers at, served by Kestrel on a free port of this machine.</summary>
    private static Uri ServedAddress<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory) where TEntryPoint : class
    {
        try
        {
            factory.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        }
        catch (InvalidOperationException)
        {
            // Already started, or already told to use Kestrel: what it serves on is read below.
        }

        factory.StartServer();
        var server = factory.Services.GetRequiredService<IServer>();
        if (server is TestServer)
            throw new InvalidOperationException(
                "The factory was started in memory before HydrationAssert saw it, and a page's scripts are fetched over a "
                + "socket. Hand the factory to HydrationAssert before anything creates a client of it, or call "
                + "factory.UseKestrel() first.");
        var listening = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("The factory's server listens on no address.");
        return new Uri(listening.TrimEnd('/') + "/");
    }

    private static async Task<HydrationReport> DrawAsync(Uri address, HydrationOptions options, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Bun())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(Harness());
        start.ArgumentList.Add(address.AbsoluteUri);
        start.ArgumentList.Add(((long)options.Timeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(options.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"The bun at '{start.FileName}' did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // The harness waits for the page as long as it was told, and then a little to read it.
        deadline.CancelAfter(options.Timeout + TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Drawing {address} did not finish in {options.Timeout + TimeSpan.FromSeconds(30)}.");
        }

        var printed = await output.ConfigureAwait(false);
        if (process.ExitCode != 0 || printed.Length == 0)
            throw new InvalidOperationException(
                $"Drawing {address} failed (exit {process.ExitCode}): {await errors.ConfigureAwait(false)}");
        try
        {
            return Read(printed);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Drawing {address} printed no report: {printed[..Math.Min(printed.Length, 2000)]}", exception);
        }
    }

    /// <summary>The report of what the harness printed.</summary>
    internal static HydrationReport Read(string printed)
    {
        using var document = JsonDocument.Parse(printed);
        var root = document.RootElement;
        var said = root.GetProperty("console").EnumerateArray()
            .Select(entry => (Level: entry.GetProperty("level").GetString(), Text: entry.GetProperty("text").GetString() ?? ""))
            .ToList();
        var report = new HydrationReport
        {
            Address = root.GetProperty("address").GetString() ?? "",
            Booted = root.GetProperty("record").ValueKind == JsonValueKind.Object,
            Errors = said.Where(entry => entry.Level == "error").Select(entry => entry.Text).ToList(),
            MissingStrings = said.Where(entry => entry.Level == "warn" && entry.Text.Contains("missing string", StringComparison.Ordinal))
                .Select(entry => entry.Text).ToList(),
        };
        if (!report.Booted) return report;

        var record = root.GetProperty("record");
        return report with
        {
            Page = record.GetProperty("page").GetString(),
            RoutedPage = record.TryGetProperty("routed", out var routed) ? routed.GetString() : null,
            Hydrated = record.GetProperty("hydrated").GetBoolean(),
            DrewOver = Strings(record, "drewOver"),
            Warnings = Strings(record, "warnings"),
            Differences = record.GetProperty("differences").EnumerateArray()
                .Select(difference => new HydrationDifference(
                    difference.GetProperty("kind").GetString() ?? "",
                    difference.GetProperty("where").GetString() ?? "",
                    difference.TryGetProperty("name", out var name) ? name.GetString() : null,
                    difference.GetProperty("server").GetString(),
                    difference.GetProperty("client").GetString()))
                .ToList(),
        };
    }

    private static List<string> Strings(JsonElement record, string name) =>
        record.TryGetProperty(name, out var values)
            ? values.EnumerateArray().Select(value => value.GetString() ?? "").ToList()
            : [];

    /// <summary>The embedded bun the app was built with, as the package's build targets wrote it down.</summary>
    private static string Bun()
    {
        var file = Path.Combine(AppContext.BaseDirectory, BunFile);
        var bun = File.Exists(file) ? File.ReadLines(file).FirstOrDefault()?.Trim() : null;
        if (string.IsNullOrEmpty(bun) || !File.Exists(bun))
            throw new InvalidOperationException(
                $"No embedded bun is named in '{file}'. The test project's build writes it from the eQuantic.UI app it "
                + "references: reference the app's project and the eQuantic.UI.Testing package, and build.");
        return bun;
    }

    /// <summary>The harness the assembly carries, written once where it can run: a folder named by its hash, so
    /// two versions of the package never run each other's.</summary>
    private static string Harness()
    {
        using var stream = typeof(HydrationAssert).Assembly.GetManifestResourceStream(HarnessResource)
            ?? throw new InvalidOperationException($"The assembly carries no '{HarnessResource}'.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var folder = Path.Combine(Path.GetTempPath(), "eQuantic.UI.Testing", Convert.ToHexString(SHA256.HashData(bytes))[..16]);
        var harness = Path.Combine(folder, "hydration-harness.js");
        if (File.Exists(harness)) return harness;

        Directory.CreateDirectory(folder);
        var written = Path.Combine(folder, $"{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(written, bytes);
        try
        {
            File.Move(written, harness);
        }
        catch (IOException) when (File.Exists(harness))
        {
            // Another test wrote the same bytes first.
            File.Delete(written);
        }

        return harness;
    }
}
