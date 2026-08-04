using System.Runtime.InteropServices;
using eQuantic.UI.Native.Shell.Apple;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A block built by hand, checked against an API that calls it SYNCHRONOUSLY and immediately —
/// because guessing at the layout against one that answers minutes later, or never, is hopeless.
/// `enumerateObjectsUsingBlock:` calls back three times before it returns, so the block is either
/// right or the test says so.
/// <para>
/// The synchronous API proves the CALL and nothing else, which is a trap worth naming: it borrows
/// the block and hands it straight back. An asynchronous framework OWNS it — copies on the way in,
/// releases when done — and that is where the first version died, taking the app with it after a
/// successful authentication. So the lifetime is tested separately, and by hand.
/// </para>
/// </summary>
public class ObjCBlockTests
{
    private delegate void Enumerate(IntPtr block, IntPtr item, nuint index, IntPtr stop);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_Block_copy")]
    private static extern IntPtr BlockCopy(IntPtr block);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_Block_release")]
    private static extern void BlockRelease(IntPtr block);

    [Fact]
    public void ABlock_IsActuallyCalled_ByTheRuntime()
    {
        var array = Send(objc_getClass("NSMutableArray"), Sel("array"));
        foreach (var value in new[] { "a", "b", "c" })
            SendVoid(array, Sel("addObject:"), NSString(value));

        var seen = new List<nuint>();
        Enumerate callback = (_, _, index, _) => seen.Add(index);
        using var block = new ObjCBlock(callback);

        SendVoid(array, Sel("enumerateObjectsUsingBlock:"), block.Handle);

        GC.KeepAlive(callback);
        seen.Should().Equal([(nuint)0, 1, 2], "the runtime called back once per item");
    }

    [Fact]
    public void ItReceivesTheARGUMENTS_NotJustTheCall()
    {
        // A block whose layout is right but whose function pointer is read at the wrong offset can
        // still be "called" — with rubbish. Reading a real argument proves the frame.
        var array = Send(objc_getClass("NSMutableArray"), Sel("array"));
        SendVoid(array, Sel("addObject:"), NSString("hello"));

        string? received = null;
        Enumerate callback = (_, item, _, _) => received = FromNSString(item);
        using var block = new ObjCBlock(callback);

        SendVoid(array, Sel("enumerateObjectsUsingBlock:"), block.Handle);

        GC.KeepAlive(callback);
        received.Should().Be("hello");
    }

    [Fact]
    public void ItSurvivesTheCOPY_AND_RELEASE_AnAsyncFrameworkDoes()
    {
        // What LocalAuthentication does with a reply block, in the open: retain it, call it long
        // after the caller has moved on, let it go. A block claiming to live on the stack asks the
        // runtime to copy it to the heap and free it — with memory this class allocated and still
        // frees itself, which is a segfault in `_Block_release` arriving on a background thread,
        // minutes later, with nothing in the trace pointing back at the block that caused it.
        var array = Send(objc_getClass("NSMutableArray"), Sel("array"));
        SendVoid(array, Sel("addObject:"), NSString("kept"));

        string? received = null;
        Enumerate callback = (_, item, _, _) => received = FromNSString(item);
        using var block = new ObjCBlock(callback);

        var retained = BlockCopy(block.Handle);
        retained.Should().Be(block.Handle, "a global block is copied by handing back the same pointer");

        SendVoid(array, Sel("enumerateObjectsUsingBlock:"), retained);
        received.Should().Be("kept", "the copy is as callable as the original");

        BlockRelease(retained);   // <- the crash, if the block lies about where it lives
        GC.KeepAlive(callback);

        // And still ours afterwards: the framework let go, the block did not evaporate.
        received = null;
        SendVoid(array, Sel("enumerateObjectsUsingBlock:"), block.Handle);
        received.Should().Be("kept");
    }
}
