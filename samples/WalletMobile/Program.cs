using eQuantic.UI.Primitives;
using eQuantic.Wallet;

// eQUANTIC WALLET — six screens of a real banking app, and the SAME C# on a phone and on a desktop.
// The two heads below are the whole difference between the platforms: on iOS the framework owns the
// app's lifecycle, on macOS a window stands in at phone geometry (390×844) for fast iteration.
// `--screen <name>` picks one of the six, `--self-test` presents 120 frames and exits 0, and
// `--render-png <path>` renders one frame through the NORMATIVE Reference backend.

#if IOS

eQuantic.UI.Native.Shell.iOS.PhotonApp.Run(args, () => new WalletApp(), PhotonTheme.Instance);
return 0;

#else

using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Native.Shell.MacOS;

const int Width = 390;
const int Height = 844;

var screen = Array.IndexOf(args, "--screen") is var s and >= 0 && args.Length > s + 1
    && Enum.TryParse<WalletScreen>(args[s + 1], ignoreCase: true, out var parsed)
        ? parsed
        : WalletScreen.Home;

if (Array.IndexOf(args, "--render-png") is var pngIndex and >= 0)
{
    var path = args.Length > pngIndex + 1 ? args[pngIndex + 1] : "wallet.png";
    var text = new CoreTextService();
    var host = new eQuantic.UI.Native.Components.PhotonHost(
        new WalletApp(screen), PhotonTheme.Instance, ThemeMode.Light, Width, Height, text)
    {
        RenderScale = 2f,
        TextRasterizer = text,
        IconRasterizer = new CoreGraphicsIconRasterizer(),
        ImageLoader = new CoreGraphicsImageLoader(),
        SafeAreaInsets = WalletApp.PhoneInsets,
    };

    var builder = new eQuantic.UI.Native.Engine.DisplayListBuilder();
    host.RenderFrame(builder);
    using var backend = new eQuantic.UI.Native.Engine.Reference.ReferenceBackend();
    using var surface = backend.CreateSurface(Width * 2, Height * 2);
    backend.Render(builder.Build(), surface);
    var rgba = new byte[Width * 2 * Height * 2 * 4];
    surface.ReadPixelsSrgb(rgba);
    File.WriteAllBytes(path, eQuantic.UI.Native.Engine.PngCodec.Encode(Width * 2, Height * 2, rgba));
    Console.WriteLine($"[wallet] wrote {path}");
    return 0;
}

var selfTest = args.Contains("--self-test");
var window = new PhotonWindow($"eQuantic Wallet — {WalletData.TitleOf(screen)}", Width, Height);
window.Run(new WalletApp(screen), PhotonTheme.Instance, maxFrames: selfTest ? 120 : 0);
Console.WriteLine($"[wallet] frames presented: {window.FramesPresented}");
return 0;

#endif
