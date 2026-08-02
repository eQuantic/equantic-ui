using System.Reflection;
using System.Runtime.Loader;
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
//   eqicon --source <file[;file]> --out <dir> [--size 1024] [--name AppIcon]

var options = ParseArgs(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: eqicon --source <file[;file]> --out <dir> [--size 1024] [--name AppIcon]");
    return 1;
}

var (sources, outDir, size, name) = options.Value;

// Nothing to do when the icon has not changed since the catalog was written — the SDK calls this
// on every build, and rasterizing a megapixel to produce identical bytes is a build nobody wants.
var catalog = Path.Combine(outDir, $"{name}.appiconset", $"{name}.png");
if (File.Exists(catalog) && sources.Where(File.Exists)
        .All(source => File.GetLastWriteTimeUtc(source) <= File.GetLastWriteTimeUtc(catalog)))
{
    return 0;
}

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

    WriteCatalog(outDir, name, () => File.WriteAllBytes(
        Path.Combine(outDir, $"{name}.appiconset", $"{name}.png"),
        PngCodec.Encode(width, height, pixels)));

    Console.WriteLine($"eqicon: wrote {Path.Combine(outDir, $"{name}.appiconset")} from "
        + Path.GetFileName(artwork) + (transparent ? " (flattened onto white — iOS icons cannot be transparent)" : ""));
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

var set = Path.Combine(outDir, $"{name}.appiconset");
WriteCatalog(outDir, name, () =>
    File.WriteAllBytes(Path.Combine(set, $"{name}.png"), PngCodec.Encode((int)size, (int)size, rgba)));

Console.WriteLine($"eqicon: wrote {set} from {iconType.FullName}");
return 0;

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

static (string[] Sources, string Out, float Size, string Name)? ParseArgs(string[] args)
{
    string? Value(string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && args.Length > index + 1 ? args[index + 1].Trim() : null;
    }

    var source = Value("--source");
    var output = Value("--out");
    if (source is null || output is null) return null;

    return (source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        output,
        float.TryParse(Value("--size"), out var size) ? size : 1024f,
        Value("--name") ?? "AppIcon");
}
