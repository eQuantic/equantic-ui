using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// A window's frame on Metal: the last pass draws into the drawable's texture and presents it when
/// it submits, without waiting, so the CAMetalLayer's drawable pool paces the CPU (#1952). The passes
/// before it (the layers, a backdrop's content and its blur) draw into the renderer's own targets,
/// exactly as offscreen.
/// </summary>
internal sealed class MetalDrawableFrame : IRhiFrameTarget
{
    private readonly MetalDevice _device;
    private readonly IntPtr _texture;
    private readonly ulong _pixelFormat;
    private readonly IntPtr _drawable;

    public MetalDrawableFrame(MetalDevice device, IntPtr texture, ulong pixelFormat, IntPtr drawable)
    {
        _device = device;
        _texture = texture;
        _pixelFormat = pixelFormat;
        _drawable = drawable;
        // The texture knows its size: the layer may have been resized since the shell last asked it.
        Width = (int)ObjC.Send(texture, ObjC.sel_registerName("width"));
        Height = (int)ObjC.Send(texture, ObjC.sel_registerName("height"));
    }

    public int Width { get; }

    public int Height { get; }

    public IRhiCommandList Begin(Color clearColor) =>
        _device.BeginDrawable(_texture, _pixelFormat, _drawable, clearColor);

    public void Submit(IRhiCommandList commands) => commands.Submit(waitUntilCompleted: false);
}
