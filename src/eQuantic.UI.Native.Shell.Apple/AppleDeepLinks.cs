using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// The URLs an Apple platform hands an app that is already running, delivered as an APPLE EVENT of
/// all things — the mechanism is from 1993 and it is still the only one macOS has.
/// <para>
/// The handler is installed EARLY and once, before the app finishes launching, because a cold
/// launch delivers its URL within milliseconds of the process starting: an app that installs the
/// handler when its first screen mounts has already missed it. So the URL is BUFFERED here and
/// answered by <see cref="Launch"/> on demand, rather than delivered to whoever happens to be
/// listening at the time.
/// </para>
/// <para>
/// Registered through the runtime like every other Apple delegate in this assembly
/// (<c>objc_allocateClassPair</c> + <c>class_addMethod</c>), because an AppleEvent handler is a
/// target and a SELECTOR — there is no block-taking form of it, which is why this could not simply
/// use <see cref="ObjCBlock"/>.
/// </para>
/// </summary>
public static class AppleDeepLinks
{
    // 'GURL' and '----', as the four-character codes AppleEvents are addressed by. Spelled from
    // their characters rather than as magic numbers, because that is what they ARE and because a
    // typo in a hex literal here produces a handler that is simply never called.
    private static readonly uint InternetEventClass = FourCharCode("GURL");
    private static readonly uint GetUrlEventId = FourCharCode("GURL");
    private static readonly uint KeyDirectObject = FourCharCode("----");

    private delegate void HandleUrlEvent(IntPtr self, IntPtr cmd, IntPtr eventDescriptor, IntPtr reply);

    private static readonly object Gate = new();
    private static DeepLinkRelay? _relay;
    private static HandleUrlEvent? _onUrl;          // kept alive for the runtime's sake
    private static IntPtr _handlerClass;
    private static IntPtr _handler;

    /// <summary>
    /// Installs the handler and returns the relay behind it. Called by the shell BEFORE the run
    /// loop starts — the whole point is to be listening before the launch event arrives, and
    /// nothing an app writes can be early enough. Idempotent: the second caller gets the first
    /// caller's relay, which is what makes the container's registration safe.
    /// </summary>
    public static DeepLinkRelay Install()
    {
        lock (Gate)
        {
            if (_relay is not null) return _relay;
            _relay = new DeepLinkRelay();

            _handlerClass = objc_allocateClassPair(objc_getClass("NSObject"), "EQDeepLinkHandler", 0);
            _onUrl = static (_, _, descriptor, _) => Deliver(descriptor);
            class_addMethod(_handlerClass, sel_registerName("handleURLEvent:withReplyEvent:"),
                Marshal.GetFunctionPointerForDelegate(_onUrl), "v@:@@");
            objc_registerClassPair(_handlerClass);

            _handler = Send(Send(_handlerClass, Sel("alloc")), Sel("init"));

            var manager = Send(objc_getClass("NSAppleEventManager"), Sel("sharedAppleEventManager"));
            SendVoid(manager, Sel("setEventHandler:andSelector:forEventClass:andEventID:"),
                _handler, sel_registerName("handleURLEvent:withReplyEvent:"),
                InternetEventClass, GetUrlEventId);

            return _relay;
        }
    }

    /// <summary>The one line of interop: an AppleEvent's direct object, as text. Everything that
    /// happens to it afterwards is <see cref="DeepLinkRelay"/>'s, where it can be tested.</summary>
    private static void Deliver(IntPtr descriptor)
    {
        if (descriptor == IntPtr.Zero) return;
        var direct = Send(descriptor, Sel("paramDescriptorForKeyword:"), KeyDirectObject);
        if (direct == IntPtr.Zero) return;

        DeepLinkRelay? relay;
        lock (Gate) relay = _relay;
        relay?.Offer(FromNSString(Send(direct, Sel("stringValue"))));
    }

    /// <summary>Four characters, most significant first — the layout an OSType has had since
    /// before any of this was a framework.</summary>
    internal static uint FourCharCode(string code) =>
        ((uint)code[0] << 24) | ((uint)code[1] << 16) | ((uint)code[2] << 8) | code[3];
}
