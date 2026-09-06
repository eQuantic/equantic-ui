using System.Reflection;
using System.Runtime.Loader;
using eQuantic.UI.Build;
using eQuantic.UI.Codegen;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// eqicon — the app's icon, into the catalog its platform installs from.
//
// The SOURCE can be either: a `.cs` declaring an IAppIcon, which is rendered here by the same
// engine that draws the app, or a `.png` a designer produced. A real brand mark comes from a
// designer and pretending otherwise would be dogma; what the framework owes an app author is that
// neither path makes them hand-write an asset catalog, a Contents.json or a manifest key.
//
// The C# path compiles the icon file ON ITS OWN, against the vocabulary and nothing else — the
// app's own assembly may target a device this build machine cannot load, and an icon has no
// business reaching into the app anyway.
//
//   eqicon --source <file[;file]> [--out <catalog-dir>] [--web <dir> --app <name>]
//          [--android <res-dir>] [--macos <file.icns>] [--size 1024] [--name AppIcon]
//
// `--out` writes the platform's asset catalog; `--web` writes what a browser asks for — the
// manifest's install icons, the one iOS Safari pins, the one in the tab. Same source, both ways:
// an app states its icon ONCE and it appears everywhere it belongs.

// `capabilities` — the Apple manifest keys for what the app declared. Read off the compiled
// assembly, where the generator put them; written where the SDK hands them to Apple's packaging.
if (args.Length > 0 && args[0] == "entitlements") return Entitlements(args);

if (args.Length > 0 && args[0] == "capabilities")
{
    string? Arg(string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && args.Length > index + 1 ? args[index + 1].Trim() : null;
    }

    var appAssembly = Arg("--assembly");
    var plist = Arg("--plist");
    if (appAssembly is null || plist is null)
    {
        Console.Error.WriteLine("Usage: eqicon capabilities --assembly <app.dll> --plist <out.plist>");
        return 1;
    }

    if (!File.Exists(appAssembly)) return 0;      // nothing built yet; the build will come back
    var wrote = CapabilityManifest.Write(appAssembly, plist);
    Console.WriteLine(wrote
        ? $"eqicon: wrote {plist}"
        : "eqicon: no device capabilities declared");
    return 0;
}

// The RELEASE half of the same idea: what the app needs the SYSTEM to permit, read from
// `[assembly: PhotonEntitlement(…)]` and written where codesign can be handed it.
static int Entitlements(string[] args)
{
    string? Arg(string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && args.Length > index + 1 ? args[index + 1].Trim() : null;
    }

    var appAssembly = Arg("--assembly");
    var plist = Arg("--plist");
    if (appAssembly is null || plist is null)
    {
        Console.Error.WriteLine("Usage: eqicon entitlements --assembly <app.dll> --plist <out.entitlements>");
        return 1;
    }

    if (!File.Exists(appAssembly)) return 0;      // nothing built yet; the build will come back
    var required = (Arg("--also") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries
        | StringSplitOptions.TrimEntries);
    Console.WriteLine(EntitlementsManifest.Write(appAssembly, plist, required)
        ? $"eqicon: wrote {plist}"
        : "eqicon: no entitlements declared");
    return 0;
}

// `vectors` — the app's `Assets/**/*.svg`, as one C# class of drawing data. A browser cannot read
// a file and a phone should not carry its artwork twice, so the reading happens HERE and what the
// app compiles against is a typed member per file.
if (args.Length > 0 && args[0] == "vectors")
{
    string? Arg(string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && args.Length > index + 1 ? args[index + 1].Trim() : null;
    }

    var assets = Arg("--assets");
    var output = Arg("--out");
    var ns = Arg("--namespace");
    if (assets is null || output is null || ns is null)
    {
        Console.Error.WriteLine("Usage: eqicon vectors --assets <dir> --out <file.g.cs> --namespace <ns>");
        return 1;
    }

    var count = VectorCatalog.Write(assets, output, ns);
    if (count > 0) Console.WriteLine($"eqicon: {count} drawing(s) → {output}");
    return 0;
}

// `bundle` — the macOS .app around a built head. A separate verb because it is a different job
// from deriving artwork, and the SDK calls it with what the project already knows.
if (args.Length > 0 && args[0] == "bundle")
{
    string? Arg(string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && args.Length > index + 1 ? args[index + 1].Trim() : null;
    }

    var bundle = Arg("--app");
    var executable = Arg("--exec");
    if (bundle is null || executable is null)
    {
        Console.Error.WriteLine("Usage: eqicon bundle --app <Name.app> --exec <executable> "
            + "[--name <display>] [--id <bundle id>] [--version <x.y.z>] [--icns <file>] "
            + "[--payload <dir> [--exclude <dir>]]");
        return 1;
    }

    var declaredVersion = Arg("--version") ?? "1.0.0";
    MacAppBundle.Write(bundle, executable,
        Arg("--name") ?? executable,
        Arg("--id") ?? $"com.equantic.{executable.ToLowerInvariant()}",
        declaredVersion,
        Arg("--icns"),
        Arg("--capabilities"));

    // Said once, where it is actionable: Apple's version is one to three integers, and a .NET
    // prerelease version is not that. Trimming it silently would mean the app reports a version
    // nobody chose; the alternative — passing it through — is a bundle notarization refuses.
    if (MacAppBundle.AppleVersion(declaredVersion) is var apple && apple != declaredVersion)
    {
        Console.WriteLine($"eqicon: the bundle version is {apple} — Apple accepts one to three "
            + $"integers, so the rest of \"{declaredVersion}\" cannot travel in the plist.");
    }

    // The payload — what the app needs to RUN — when the caller says where it is. Optional so the
    // verb still writes a bundle SHELL for anything that fills it another way; the SDK always
    // passes it, because deciding what belongs inside is the half that cannot live in a glob.
    if (Arg("--payload") is { } payload)
    {
        var excluded = args
            .Select((value, index) => (value, index))
            .Where(entry => entry.value == "--exclude" && args.Length > entry.index + 1)
            .Select(entry => args[entry.index + 1])
            .ToArray();
        var copied = MacAppPayload.Populate(payload, bundle, excluded);
        Console.WriteLine($"eqicon: wrote {bundle} ({copied} file(s))");
    }
    else
    {
        Console.WriteLine($"eqicon: wrote {bundle}");
    }

    return 0;
}

var options = ParseArgs(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: eqicon --source <file[;file]> [--out <dir>] [--web <dir> --app <name>] "
        + "[--android <res-dir>] [--macos <file.icns>] [--windows <file.ico>] [--size 1024] [--name AppIcon]");
    return 1;
}

var (sources, outDir, webDir, androidDir, macIcns, windowsIco, appName, size, name) = options.Value;

// Nothing to do when every output is already newer than the icon — the SDK calls this on each
// build, and rasterizing a megapixel to produce identical bytes is a build nobody wants. EVERY
// output, not one of them: a check that watches a single file calls a half-written set finished.
if (UpToDate(sources, Outputs(outDir, webDir, androidDir, macIcns, windowsIco, name))) return 0;

// A PNG needs no rendering: the artwork already exists and the platform's ceremony is what we owe.
if (sources.FirstOrDefault(s => s.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) is { } artwork)
{
    if (!File.Exists(artwork))
    {
        Console.Error.WriteLine($"eqicon: {artwork} does not exist.");
        return 1;
    }

    var (width, height) = PngSize(artwork);
    if (width != (int)size || height != (int)size)
    {
        Console.Error.WriteLine(
            $"eqicon: {Path.GetFileName(artwork)} is {width}x{height}; the catalog declares {size}x{size}. "
            + "Give the icon at that size — every smaller one is derived from it.");
        return 1;
    }

    // An iOS icon must be OPAQUE — one with an alpha channel installs as a blank tile, silently,
    // which is a whole afternoon to work out. Designers export with transparency all the time, so
    // the framework flattens it rather than sending the file back.
    var (_, _, pixels) = PngCodec.Decode(File.ReadAllBytes(artwork));
    var transparent = false;
    for (var i = 3; i < pixels.Length; i += 4)
    {
        if (pixels[i] == 255) continue;
        transparent = true;
        var alpha = pixels[i] / 255f;
        for (var channel = i - 3; channel < i; channel++)
            pixels[channel] = (byte)(pixels[channel] * alpha + 255 * (1 - alpha));
        pixels[i] = 255;
    }

    Emit(outDir, webDir, androidDir, macIcns, windowsIco, appName, name, width, pixels);
    Console.WriteLine($"eqicon: wrote {Where(outDir, webDir, androidDir, macIcns, windowsIco)} from " + Path.GetFileName(artwork)
        + (transparent ? " (flattened onto white — iOS icons cannot be transparent)" : ""));
    return 0;
}

var trees = sources
    .Where(File.Exists)
    .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
    .ToArray();
if (trees.Length == 0)
{
    Console.Error.WriteLine($"eqicon: no icon source found ({string.Join(";", sources)}).");
    return 1;
}

// The icon may only speak the VOCABULARY — that is the contract on IAppIcon — and the vocabulary
// ships beside this tool. Resolving from here rather than from the app's own reference list keeps
// the icon buildable before the app's project references have been built at all.
var referenceDirs = new[]
{
    AppContext.BaseDirectory,
    Path.GetDirectoryName(typeof(object).Assembly.Location)!,
};
var references = referenceDirs
    .Where(Directory.Exists)
    .SelectMany(dir => Directory.EnumerateFiles(dir, "*.dll"))
    .GroupBy(Path.GetFileName)
    .Select(group => (MetadataReference)MetadataReference.CreateFromFile(group.First()))
    .ToArray();

var compilation = CSharpCompilation.Create("eQuantic.AppIcon", trees, references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

using var image = new MemoryStream();
var emitted = compilation.Emit(image);
if (!emitted.Success)
{
    foreach (var diagnostic in emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
        Console.Error.WriteLine($"eqicon: {diagnostic}");
    return 1;
}

image.Position = 0;
// The icon assembly is loaded into THIS process, so its vocabulary types have to be the ones
// already here — a second copy would make `is IAppIcon` false against an identical interface.
var assembly = AssemblyLoadContext.Default.LoadFromStream(image);
var iconType = assembly.GetTypes().FirstOrDefault(t =>
    !t.IsAbstract && t.IsClass && typeof(IAppIcon).IsAssignableFrom(t));
if (iconType is null)
{
    Console.Error.WriteLine("eqicon: no IAppIcon implementation in the icon source.");
    return 1;
}

var icon = (IAppIcon)Activator.CreateInstance(iconType)!;

// Real glyphs where the platform can give them. An icon with a letter in it is the common case, and
// a placeholder box would ship silently.
ITextMeasurer? measurer = null;
if (OperatingSystem.IsMacOS())
{
    try { measurer = new eQuantic.UI.Native.Shell.Apple.CoreTextService(); }
    catch (DllNotFoundException) { }
    catch (EntryPointNotFoundException) { }
}
else if (OperatingSystem.IsWindows())
{
    try { measurer = new eQuantic.UI.Native.Shell.Windows.Graphics.DirectWriteTextService(); }
    catch (DllNotFoundException) { }
    catch (InvalidOperationException) { }
}

// The icon is DESIGNED in dp, like every other tree, and rasterized at whatever density the
// platform asks for — the same relationship a phone screen has with its own scale factor. Without
// that the author would be picking type sizes against a 1024-unit canvas, which is nobody's idea
// of designing an icon.
const float canvas = 64f;
var theme = PhotonTheme.Instance;
var root = icon.Build(new ComponentContext(theme));
var host = new PhotonHost(root, theme, ThemeMode.Light, canvas, canvas, measurer)
{
    TextRasterizer = measurer as ITextRasterizer,
    RenderScale = size / canvas,
    ReducedMotion = true,
};

var builder = new DisplayListBuilder();
host.RenderFrame(builder);
using var backend = new ReferenceBackend();
using var surface = backend.CreateSurface((int)size, (int)size);
backend.Render(builder.Build(), surface);

var rgba = new byte[(int)size * (int)size * 4];
surface.ReadPixelsSrgb(rgba);
// A home screen composites the icon against whatever it is showing, so alpha is not ours to keep.
for (var i = 3; i < rgba.Length; i += 4) rgba[i] = 255;

Emit(outDir, webDir, androidDir, macIcns, windowsIco, appName, name, (int)size, rgba);
Console.WriteLine($"eqicon: wrote {Where(outDir, webDir, androidDir, macIcns, windowsIco)} from {iconType.FullName}");
return 0;

/// <summary>Everything this invocation is responsible for producing.</summary>
static IEnumerable<string> Outputs(string? outDir, string? webDir, string? androidDir, string? macIcns,
    string? windowsIco, string name)
{
    if (outDir is not null) yield return Path.Combine(outDir, $"{name}.appiconset", $"{name}.png");
    if (outDir is not null) yield return Path.Combine(outDir, $"{name}.appiconset", "Contents.json");
    if (outDir is not null) yield return Path.Combine(outDir, "..", $"{name}.plist");
    if (androidDir is not null) yield return Path.Combine(androidDir, "mipmap-xxxhdpi", "ic_launcher.png");
    if (macIcns is not null) yield return macIcns;
    if (windowsIco is not null) yield return windowsIco;
    if (webDir is null) yield break;
    yield return Path.Combine(webDir, "icon-512.png");
    yield return Path.Combine(webDir, "icon-192.png");
    yield return Path.Combine(webDir, "apple-touch-icon.png");
    yield return Path.Combine(webDir, "favicon-32.png");
    yield return Path.Combine(webDir, "site.webmanifest");
}

static bool UpToDate(string[] sources, IEnumerable<string> outputs)
{
    var newest = sources.Where(File.Exists).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty().Max();
    return outputs.All(output => File.Exists(output) && File.GetLastWriteTimeUtc(output) >= newest);
}

/// <summary>Writes whichever outputs this invocation asked for — a catalog, a web set, or both.</summary>
static void Emit(string? outDir, string? webDir, string? androidDir, string? macIcns, string? windowsIco,
    string appName, string name, int size, byte[] rgba)
{
    if (outDir is not null)
        WriteCatalog(outDir, name, () =>
            File.WriteAllBytes(Path.Combine(outDir, $"{name}.appiconset", $"{name}.png"),
                PngCodec.Encode(size, size, rgba)));

    if (webDir is not null) WebIcons.Write(webDir, appName, size, rgba);
    if (androidDir is not null) AndroidIcons.Write(androidDir, size, rgba);
    if (macIcns is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(macIcns)!);
        MacIcons.Write(macIcns, size, rgba);
    }
    // The executable's own icon on Windows — what the taskbar, the Start menu and Explorer show.
    if (windowsIco is not null) WindowsIcons.Write(windowsIco, size, rgba);
}

static string Where(string? outDir, string? webDir, string? androidDir, string? macIcns, string? windowsIco) =>
    string.Join(" and ", new[] { outDir, webDir, androidDir, macIcns, windowsIco }.Where(d => d is not null));

/// <summary>The catalog around the artwork: the manifest key, the Contents.json, the file itself.</summary>
static void WriteCatalog(string outDir, string name, Action writeArtwork)
{
    var set = Path.Combine(outDir, $"{name}.appiconset");
    Directory.CreateDirectory(set);
    writeArtwork();

    File.WriteAllText(Path.Combine(set, "Contents.json"), JsonWriter.Document(json => json
        .Array("images", images => images.Element(image => image
            .String("filename", $"{name}.png")
            .String("idiom", "universal")
            .String("platform", "ios")
            .String("size", "1024x1024")))
        .Object("info", info => info
            .String("author", "eQuantic.UI")
            .Number("version", 1))));

    // Apple reads the icon's LOCATION from the app manifest rather than from a build property, so
    // the tool that writes the catalog writes that too.
    File.WriteAllText(Path.Combine(outDir, "..", $"{name}.plist"), PropertyListWriter.Document(plist =>
        plist.String("XSAppIconAssets", $"obj/eQuantic/Assets.xcassets/{name}.appiconset")));
}

/// <summary>Width and height straight out of the PNG header — no decoder needed to check a size.</summary>
static (int Width, int Height) PngSize(string path)
{
    var header = new byte[24];
    using var stream = File.OpenRead(path);
    if (stream.Read(header) < 24) return (0, 0);
    return (BitConverter.ToInt32(header.AsSpan(16, 4).ToArray().Reverse().ToArray()),
        BitConverter.ToInt32(header.AsSpan(20, 4).ToArray().Reverse().ToArray()));
}

static (string[] Sources, string? Out, string? Web, string? Android, string? Mac, string? Windows, string App, float Size, string Name)? ParseArgs(string[] args)
{
    string? Value(string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && args.Length > index + 1 ? args[index + 1].Trim() : null;
    }

    var source = Value("--source");
    var output = Value("--out");
    var web = Value("--web");
    var android = Value("--android");
    var mac = Value("--macos");
    var windows = Value("--windows");
    if (source is null || (output is null && web is null && android is null && mac is null && windows is null)) return null;

    return (source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        output, web, android, mac, windows, Value("--app") ?? "App",
        float.TryParse(Value("--size"), out var size) ? size : 1024f,
        Value("--name") ?? "AppIcon");
}
