namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// An Objective-C autorelease pool for the span of a <c>using</c>: what the calls inside it autorelease is
/// released when it is disposed. AppKit's own loop and UIKit's run loop drain one per event, and a thread
/// of .NET's pool has none, so a loop a shell runs itself and a call made on a pool thread bring their
/// own. Without one, what they were handed lived as long as the process: the macOS window left a frame's
/// drawable, command buffer and render pass descriptor behind every frame (#1506).
/// </summary>
public readonly struct AutoreleasePool : IDisposable
{
    private readonly IntPtr _pool;

    private AutoreleasePool(IntPtr pool) => _pool = pool;

    /// <summary>Opens a pool on the calling thread; dispose it on the same thread.</summary>
    public static AutoreleasePool Push() => new(ObjC.objc_autoreleasePoolPush());

    /// <summary>Drains the pool, releasing what was autoreleased since it was pushed.</summary>
    public void Dispose()
    {
        if (_pool != IntPtr.Zero) ObjC.objc_autoreleasePoolPop(_pool);
    }
}
