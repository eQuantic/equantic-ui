using System.Reflection;
using System.Runtime.Loader;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

// eqphoton: ONE Photon frame of ONE page, in a process of its own.
//
// This process loads and RUNS the user's compiled page. That is why it exists at all: a Build() that
// loops forever, allocates without end, or throws something exotic must take down a disposable child,
// never the design host that owns the preview. The parent enforces the timeout and reads one line of
// JSON from stdout; everything else this process has to say goes to stderr.
//
// It renders through the REFERENCE rasterizer — the engine's normative pixel source, the one the
// golden suite and the GPU parity gates are measured against. TreeGpuParityTests is the standing
// proof that Metal draws the same display list to the same pixels, so a Reference frame is an honest
// picture of what the device shows, produced on machines that have no GPU at all.

string? assemblyPath = null;
string? refsPath = null;
string? typeName = null;
string? outPath = null;
var width = 390f;
var height = 844f;
var density = Density.Comfortable;
var mode = ThemeMode.Light;

for (var i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--assembly": assemblyPath = args[++i]; break;
        case "--refs": refsPath = args[++i]; break;
        case "--type": typeName = args[++i]; break;
        case "--out": outPath = args[++i]; break;
        case "--width": width = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--height": height = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--density": density = args[++i] == "compact" ? Density.Compact : Density.Comfortable; break;
        case "--mode": mode = args[++i] == "dark" ? ThemeMode.Dark : ThemeMode.Light; break;
    }
}

if (assemblyPath is null || typeName is null || outPath is null)
{
    Console.Error.WriteLine("eqphoton: --assembly, --type and --out are required.");
    return 2;
}

// The user's own dependencies (their NuGets, their project references) resolve from the SAME list
// MSBuild resolved for their build. eQuantic.UI.* never gets here: those are already loaded — they
// shipped WITH this process — and the default context prefers a loaded assembly, which is what keeps
// the user's VisualNode and this process's VisualNode the same type.
var references = refsPath is not null && File.Exists(refsPath)
    ? File.ReadAllLines(refsPath)
        .Where(line => line.Length > 0)
        .ToDictionary(Path.GetFileNameWithoutExtension, line => line, StringComparer.OrdinalIgnoreCase)
    : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (name.Name is null || !references.TryGetValue(name.Name, out var found) || found is null) return null;

    // The list MSBuild resolves for COMPILING is ref assemblies where they exist — metadata-only
    // skeletons that load fine and throw BadImageFormatException the moment anything in them runs.
    // The implementation sits beside them: bin/ for a project reference's obj/…/ref/, lib/ for a
    // NuGet package's ref/. Only when the mapped file exists — a path guessed wrong falls back to
    // the listed one, whose failure at least names the right assembly.
    var separator = Path.DirectorySeparatorChar;
    var implementation = found
        .Replace($"{separator}obj{separator}", $"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
        .Replace($"{separator}ref{separator}", $"{separator}", StringComparison.OrdinalIgnoreCase);
    if (implementation != found && File.Exists(implementation)) return context.LoadFromAssemblyPath(implementation);

    var packaged = found.Replace($"{separator}ref{separator}", $"{separator}lib{separator}", StringComparison.OrdinalIgnoreCase);
    if (packaged != found && File.Exists(packaged)) return context.LoadFromAssemblyPath(packaged);

    return context.LoadFromAssemblyPath(found);
};

// From a STREAM, not from the path: the file is a temp the parent deletes the moment this process
// exits, and loading by path would keep it locked on Windows.
Assembly assembly;
await using (var stream = File.OpenRead(assemblyPath))
{
    assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
}

var pageType = assembly.GetType(typeName, throwOnError: false);
if (pageType is null)
{
    Console.Error.WriteLine($"eqphoton: '{typeName}' is not a type in the compiled assembly.");
    return 3;
}

// A page is constructed the way the DI container would construct it, minus the container: the
// parameterless constructor when there is one, and otherwise the one whose parameters can all be
// satisfied — optional values by their defaults, everything else by null. A page that dereferences an
// injected service inside Build() then fails INSIDE Build, and that failure is this process's whole
// reason to exist: it comes back as a sentence, not as a hung preview.
object? instance;
try
{
    instance = Construct(pageType, assembly, depth: 0);
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"eqphoton: {typeName} could not be constructed — "
        + $"{(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}");
    return 3;
}

if (instance is not VisualNode root)
{
    Console.Error.WriteLine(instance is null
        ? $"eqphoton: {typeName} has no constructor the preview can satisfy."
        : $"eqphoton: {typeName} is not a component (it does not derive from VisualNode).");
    return 3;
}

// Text the way eqicon gets it: CoreText on a Mac, and the measurer-less path elsewhere — a frame
// with placeholder text metrics is still a frame, and the honest note travels with the result.
ITextMeasurer? measurer = null;
if (OperatingSystem.IsMacOS())
{
    try { measurer = new eQuantic.UI.Native.Shell.Apple.CoreTextService(); }
    catch (DllNotFoundException) { }
    catch (EntryPointNotFoundException) { }
}

// A DEVELOPER is watching, by definition: the boundary names the component and quotes the throw
// instead of the production panel's guarded sentence, and every contained failure also lands on
// stderr — where the design host turns it into words the panel can show.
eQuantic.UI.Primitives.ComponentBoundary.Diagnostics = true;
eQuantic.UI.Primitives.ComponentBoundary.Report = (component, error) =>
    Console.Error.WriteLine($"eqphoton: {component.GetType().Name} threw {error.GetType().Name}: {error.Message}");

var host = new PhotonHost(root, PhotonTheme.Instance, mode, width, height, measurer)
{
    TextRasterizer = measurer as ITextRasterizer,
    Density = density,
    // A STILL is being made: parked animations, not a frame captured mid-flight.
    ReducedMotion = true,
};

if (OperatingSystem.IsMacOS())
{
    host.ImageLoader = new eQuantic.UI.Native.Shell.Apple.CoreGraphicsImageLoader();
    host.IconRasterizer = new eQuantic.UI.Native.Shell.Apple.CoreGraphicsIconRasterizer();
}

// Two frames before the one that counts: OnMount runs on the first, and anything it changed
// (a lazy section, a computed field) is in the tree by the second — the same settle the macOS
// screenshot runner gives a page.
var builder = new DisplayListBuilder();
try
{
    for (var frame = 0; frame < 2; frame++)
    {
        builder = new DisplayListBuilder();
        host.RenderFrame(builder, frame * 16f);
    }
}
catch (Exception ex)
{
    // The page's own code threw — usually a null service a constructor argument could not supply.
    // One sentence with the page's exception, which is what the panel can actually show.
    Console.Error.WriteLine($"eqphoton: {typeName}.Build threw {ex.GetType().Name}: {ex.Message}");
    return 4;
}

using var backend = new ReferenceBackend();
using var surface = backend.CreateSurface((int)width, (int)height);
backend.Render(builder.Build(), surface);

var rgba = new byte[(int)width * (int)height * 4];
surface.ReadPixelsSrgb(rgba);
File.WriteAllBytes(outPath, PngCodec.Encode((int)width, (int)height, rgba));

// The one line the parent reads.
static object? Construct(Type type, Assembly userAssembly, int depth)
{
    // The way the DI container would, minus the container. A page's constructor asks for interfaces;
    // the app registered implementations for them at startup, and the preview has no startup — so:
    // the parameterless constructor when there is one, otherwise the constructor with the fewest
    // unsatisfied parameters, each satisfied in the order a person would try:
    //   1. its default value;
    //   2. an implementation from the USER'S OWN assembly (a sample's WalletLedger carries the very
    //      demo data its screens are designed around — the best possible frame);
    //   3. for any other interface, a benign stub whose every member answers default — enough for
    //      configuration["key"] to answer null and let the page take its own fallback;
    //   4. default/null, and Build() itself says what it missed.
    if (depth > 3) return null;

    var constructor = type.GetConstructors()
        .OrderBy(candidate => candidate.GetParameters().Count(parameter => !parameter.HasDefaultValue))
        .FirstOrDefault();
    if (constructor is null) return Activator.CreateInstance(type);

    var arguments = constructor.GetParameters().Select(parameter =>
    {
        if (parameter.HasDefaultValue) return parameter.DefaultValue;
        var wanted = parameter.ParameterType;

        if (wanted.IsInterface)
        {
            var implementation = userAssembly.GetTypes().FirstOrDefault(candidate =>
                candidate is { IsClass: true, IsAbstract: false } && wanted.IsAssignableFrom(candidate));
            if (implementation is not null)
            {
                try { return Construct(implementation, userAssembly, depth + 1); }
                catch { /* fall through to the stub */ }
            }
            return Stub(wanted);
        }

        return wanted.IsValueType ? Activator.CreateInstance(wanted) : null;
    }).ToArray();

    return constructor.Invoke(arguments);
}

/// <summary>An implementation of nothing: every member answers default. It exists so a page whose
/// constructor merely READS a service can still be framed.</summary>
static object? Stub(Type contract)
{
    try
    {
        return DispatchProxy.Create(contract, typeof(NullProxy));
    }
    catch
    {
        // A contract a proxy cannot implement (generic, non-public) stays null, and Build() says
        // what it missed.
        return null;
    }
}

Console.WriteLine($"{{\"ok\":true,\"width\":{(int)width},\"height\":{(int)height},\"text\":{(measurer is null ? "false" : "true")}}}");
return 0;

/// <summary>The stub's behaviour: default for everything, which is null for a configuration key and
/// zero for a count — the answers an app's own fallbacks are written against.</summary>
public class NullProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod is not null && targetMethod.ReturnType != typeof(void) && targetMethod.ReturnType.IsValueType
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
}
