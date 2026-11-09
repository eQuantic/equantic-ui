using System.Globalization;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Testing;

/// <summary>
/// Lays a component out the way Photon's shells do, with no window and no GPU, so an app's tests can
/// read where each node lands and how its text breaks: Flutter's <c>WidgetTester.getRect</c>, for any
/// .NET test framework.
/// <code>
/// using var tester = new PhotonTester(width: 390, height: 844, density: Density.Compact);
/// tester.Pump(new ProfileCard());
/// var name = tester.Find("Ana Beatriz Nogueira");
/// Assert.Single(name.Text!.Lines);
/// </code>
/// <para>
/// A FRAME is drawn by <see cref="PhotonHost"/>, the frame driver every native shell wraps: a stateful
/// instance is kept by its position from one frame to the next, a frame builds only what a
/// <c>SetState</c> marked, and the overlay layers lay out in their own pass. What a test measures is
/// what a shell lays out.
/// </para>
/// <para>
/// THE SAME ANSWER ON EVERY OS. Text is measured by the engine's deterministic measurer and drawn as
/// the native goldens draw it, a bar per line, the way Flutter's tests render every glyph in a test font
/// of boxes: a sentence wraps at the same word on a Mac and on the Linux machine CI runs.
/// </para>
/// <para>
/// A node is found by the NAME a screen reader announces, as <c>ComponentTester</c> finds a control on
/// the web: the same name finds it on both targets.
/// </para>
/// </summary>
public sealed class PhotonTester : IDisposable
{
    private PhotonHost? _host;
    private RealizeResult? _frame;
    private DisplayList? _paint;
    private MeasuredNode? _root;
    private IReadOnlyList<MeasuredNode> _layers = [];
    private Dictionary<int, MeasuredNode> _byPosition = [];
    private bool _disposed;

    /// <summary>A tester for one component and the frames it draws.</summary>
    /// <param name="theme">The theme every component builds and paints with; the SDK's when null.</param>
    /// <param name="culture">The culture a component formats with and reads its own labels in; the
    /// invariant one when null, so a test reads the same on every machine.</param>
    /// <param name="width">The viewport's width in dp, which decides the size class and which arm of
    /// an adaptive node shows; a desktop window's by default.</param>
    /// <param name="height">The viewport's height in dp.</param>
    /// <param name="density">The density the shell asks for: Compact for a window driven by a mouse,
    /// Comfortable for a touch screen.</param>
    /// <param name="mode">The theme's mode the frame paints in.</param>
    /// <param name="typeScale">The system's text size, as a multiple of the theme's.</param>
    public PhotonTester(IAppTheme? theme = null, CultureInfo? culture = null, float width = 1280f,
        float height = 800f, Density density = Density.Comfortable, ThemeMode mode = ThemeMode.Light,
        float typeScale = 1f)
    {
        if (!(width >= 1)) throw new ArgumentOutOfRangeException(nameof(width), width, "A viewport is at least one dp wide.");
        if (!(height >= 1)) throw new ArgumentOutOfRangeException(nameof(height), height, "A viewport is at least one dp tall.");
        if (!(typeScale > 0)) throw new ArgumentOutOfRangeException(nameof(typeScale), typeScale, "Text is drawn at some size.");

        Theme = theme ?? PhotonTheme.Instance;
        Culture = culture ?? CultureInfo.InvariantCulture;
        Width = width;
        Height = height;
        Density = density;
        Mode = mode;
        TypeScale = typeScale;
    }

    /// <summary>The theme every component builds and paints with.</summary>
    public IAppTheme Theme { get; }

    /// <summary>The culture every build and every handler runs in.</summary>
    public CultureInfo Culture { get; }

    /// <summary>The viewport's width in dp.</summary>
    public float Width { get; }

    /// <summary>The viewport's height in dp.</summary>
    public float Height { get; }

    /// <summary>The density the controls are laid out at.</summary>
    public Density Density { get; }

    /// <summary>The theme's mode the frame paints in.</summary>
    public ThemeMode Mode { get; }

    /// <summary>The system's text size, as a multiple of the theme's.</summary>
    public float TypeScale { get; }

    /// <summary>The page's layout in the last frame, from the root down.</summary>
    public MeasuredNode Root => _root ?? throw NothingDrawn();

    /// <summary>The layout of each overlay layer the last frame showed (a dialog, a menu, a sheet), in
    /// paint order, topmost last.</summary>
    public IReadOnlyList<MeasuredNode> Layers => _frame is null ? throw NothingDrawn() : _layers;

    /// <summary>Whether a <c>SetState</c> asked for a frame that has not been drawn yet.</summary>
    public bool HasScheduledFrame => _host?.NeedsRender ?? false;

    /// <summary>Mounts <paramref name="root"/> and draws its first frame. A second call starts over:
    /// the tree drawn before is taken down, and every <c>OnUnmount</c> in it runs.</summary>
    public void Pump(VisualNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ThrowIfDisposed();
        Unmount();
        _host = new PhotonHost(root, Theme, Mode, Width, Height, ApproximateTextMeasurer.Instance, TypeScale)
        {
            Density = Density,
            // As every production shell runs it: each frame's layout nodes are handed to the next.
            // The measurements a test holds are copies (MeasuredNode), so recycling rewrites nothing
            // a test can see.
            RecycleFrames = true,
        };
        Draw();
    }

    /// <summary>Draws the next frame: builds what a <c>SetState</c> marked since the last one.</summary>
    public void Pump()
    {
        ThrowIfDisposed();
        if (_host is null) throw new InvalidOperationException("Nothing is mounted yet: pump a component first, with Pump(root).");
        Draw();
    }

    /// <summary>The node a screen reader announces as <paramref name="name"/>, as the last frame laid it
    /// out: a button by its label, a text by what it says.</summary>
    /// <exception cref="InvalidOperationException">No node is announced so, or more than one is; the
    /// message lists what the frame announces.</exception>
    public MeasuredNode Find(string name) => Locate(name, null);

    /// <summary>The node announced as <paramref name="name"/> in <paramref name="role"/>, for a name that
    /// two nodes of different roles share.</summary>
    /// <exception cref="InvalidOperationException">No node is announced so, or more than one is.</exception>
    public MeasuredNode Find(SemanticRole role, string name) => Locate(name, role);

    /// <summary>
    /// Paints the last frame on the CPU, through the reference backend every GPU backend is held to,
    /// and answers its pixels: sRGB, four bytes a pixel (red, green, blue, alpha), row after row, from
    /// the top-left. The viewport's size rounds up to whole pixels at one pixel a dp.
    /// </summary>
    public byte[] ReadPixelsSrgb()
    {
        ThrowIfDisposed();
        var paint = _paint ?? throw NothingDrawn();
        var width = (int)MathF.Ceiling(Width);
        var height = (int)MathF.Ceiling(Height);
        using var backend = new ReferenceBackend();
        using var surface = new ReferenceSurface(width, height);
        backend.Render(paint, surface);
        var pixels = new byte[width * height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }

    /// <summary>Takes the tree down, so every <c>OnUnmount</c> in it runs.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unmount();
    }

    private void Draw()
    {
        using var culture = new CultureScope(Culture);
        var builder = new DisplayListBuilder();
        var frame = _host!.RenderFrame(builder);
        _paint = builder.Build();
        _frame = frame;

        var byPosition = new Dictionary<int, MeasuredNode>();
        _root = MeasuredNode.Copy(frame.Root, byPosition);
        var layers = new MeasuredNode[frame.OverlayRoots.Count];
        for (var i = 0; i < layers.Length; i++) layers[i] = MeasuredNode.Copy(frame.OverlayRoots[i], byPosition);
        _layers = layers;
        _byPosition = byPosition;
    }

    private MeasuredNode Locate(string name, SemanticRole? role)
    {
        ArgumentNullException.ThrowIfNull(name);
        ThrowIfDisposed();
        if (_host is null || _frame is null) throw NothingDrawn();

        var announced = _host.Semantics();
        var matches = announced.Where(node => node.Label == name && (role is null || node.Role == role)).ToList();
        if (matches.Count == 1) return Measured(matches[0]);

        // The failure lists what IS announced: the test's author learns the name the reader hears,
        // which is the one to write.
        var heard = announced.Count == 0
            ? " Nothing is announced."
            : " It announces:\n  " + string.Join("\n  ", announced.Select(node => $"{node.Role} \"{node.Label}\""));
        var what = role is null ? "node" : role.ToString();
        throw new InvalidOperationException((matches.Count == 0
            ? $"No {what} in the frame is announced as \"{name}\"."
            : $"{matches.Count} of the frame's nodes are announced as \"{name}\"; name the role to tell them apart.") + heard);
    }

    /// <summary>The measured node <paramref name="announced"/> was read from: the one its position holds.</summary>
    private MeasuredNode Measured(SemanticNode announced) =>
        _byPosition.TryGetValue(announced.Id, out var measured)
            ? measured
            : throw new InvalidOperationException(
                $"{announced.Role} \"{announced.Label}\" was announced from a position the frame laid nothing out at.");

    private void Unmount()
    {
        if (_host is null) return;
        using var culture = new CultureScope(Culture);
        _host.Unmount();
        _host = null;
        _frame = null;
        _paint = null;
        _root = null;
        _layers = [];
        _byPosition = [];
    }

    private static InvalidOperationException NothingDrawn() =>
        new("Nothing is drawn yet: pump a component first, with Pump(root).");

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
