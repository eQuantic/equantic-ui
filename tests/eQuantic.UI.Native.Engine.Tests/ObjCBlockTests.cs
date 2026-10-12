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
/// <para>
/// The block calls a static <see cref="UnmanagedCallersOnlyAttribute"/> entry point, which finds what
/// it answers for in the block's own storage (#795). Each test reads its result back from that
/// context, so a block that reached the right function with the wrong context fails here too.
/// </para>
/// </summary>
public unsafe class ObjCBlockTests
{
    /// <summary>What an entry point writes to: the context a block carries.</summary>
    private sealed class Seen
    {
        public List<nuint> Indexes { get; } = [];
        public string? Item { get; set; }
    }

    [UnmanagedCallersOnly]
    private static void Enumerated(IntPtr block, IntPtr item, nuint index, IntPtr stop)
    {
        var seen = ObjCBlock.Context<Seen>(block);
        seen.Indexes.Add(index);
        seen.Item = FromNSString(item);
    }

    private static IntPtr Entry => (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, nuint, IntPtr, void>)&Enumerated;

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_Block_copy")]
    private static extern IntPtr BlockCopy(IntPtr block);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_Block_release")]
    private static extern void BlockRelease(IntPtr block);

    private static IntPtr ArrayOf(params string[] values)
    {
        var array = Send(objc_getClass("NSMutableArray"), Sel("array"));
        foreach (var value in values) SendVoid(array, Sel("addObject:"), NSString(value));
        return array;
    }

    [MacFact]
    public void ABlock_IsActuallyCalled_ByTheRuntime()
    {
        var seen = new Seen();
        using var block = new ObjCBlock(Entry, seen);

        SendVoid(ArrayOf("a", "b", "c"), Sel("enumerateObjectsUsingBlock:"), block.Handle);

        seen.Indexes.Should().Equal([(nuint)0, 1, 2], "the runtime called back once per item");
    }

    [MacFact]
    public void ItReceivesTheARGUMENTS_NotJustTheCall()
    {
        // A block whose layout is right but whose function pointer is read at the wrong offset can
        // still be "called" — with rubbish. Reading a real argument proves the frame.
        var seen = new Seen();
        using var block = new ObjCBlock(Entry, seen);

        SendVoid(ArrayOf("hello"), Sel("enumerateObjectsUsingBlock:"), block.Handle);

        seen.Item.Should().Be("hello");
    }

    [MacFact]
    public void EachBlockAnswersForItsOwnContext()
    {
        // One static entry point serves every block; what tells them apart is the context each
        // carries. Two blocks, two arrays: each context hears its own array and nothing of the other.
        var first = new Seen();
        var second = new Seen();
        using var one = new ObjCBlock(Entry, first);
        using var two = new ObjCBlock(Entry, second);

        SendVoid(ArrayOf("first"), Sel("enumerateObjectsUsingBlock:"), one.Handle);
        SendVoid(ArrayOf("second", "again"), Sel("enumerateObjectsUsingBlock:"), two.Handle);

        first.Item.Should().Be("first");
        first.Indexes.Should().Equal([(nuint)0]);
        second.Item.Should().Be("again");
        second.Indexes.Should().Equal([(nuint)0, 1]);
    }

    [MacFact]
    public void ItSurvivesTheCOPY_AND_RELEASE_AnAsyncFrameworkDoes()
    {
        // What LocalAuthentication does with a reply block, in the open: retain it, call it long
        // after the caller has moved on, let it go. A block claiming to live on the stack asks the
        // runtime to copy it to the heap and free it — with memory this class allocated and still
        // frees itself, which is a segfault in `_Block_release` arriving on a background thread,
        // minutes later, with nothing in the trace pointing back at the block that caused it.
        var array = ArrayOf("kept");
        var seen = new Seen();
        using var block = new ObjCBlock(Entry, seen);

        var retained = BlockCopy(block.Handle);
        retained.Should().Be(block.Handle, "a global block is copied by handing back the same pointer");

        SendVoid(array, Sel("enumerateObjectsUsingBlock:"), retained);
        seen.Item.Should().Be("kept", "the copy is as callable as the original");

        BlockRelease(retained);   // <- the crash, if the block lies about where it lives

        // And still ours afterwards: the framework let go, the block and its context did not evaporate.
        seen.Item = null;
        SendVoid(array, Sel("enumerateObjectsUsingBlock:"), block.Handle);
        seen.Item.Should().Be("kept");
    }

    [Fact]
    public void ABlockNeedsAnEntryPointAndAContext()
    {
        var withoutEntry = () => new ObjCBlock(IntPtr.Zero, new Seen());
        withoutEntry.Should().Throw<ArgumentException>();

        var withoutContext = () => new ObjCBlock(Entry, null!);
        withoutContext.Should().Throw<ArgumentNullException>();
    }
}
