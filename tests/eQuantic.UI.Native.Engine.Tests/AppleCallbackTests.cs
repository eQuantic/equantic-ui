using System.Runtime.InteropServices;
using eQuantic.UI.Native.Shell.Apple;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the Apple shells hand native code to call back (#795). An iPhone build is compiled ahead of
/// time, with no JIT to make a native-to-managed wrapper on demand, so a callback there must be a
/// static method the compiler saw as an entry point. A delegate turned into a function pointer works
/// on the Mac and on the simulator, both of which have a JIT, and fails at its first call on a device.
/// </summary>
public partial class AppleCallbackTests
{
    /// <summary>
    /// The instrument that FAILS, rather than a reviewer who remembers: no code in the shared Apple
    /// shell or the iOS shell turns a delegate into a function pointer. What remains is
    /// <c>(IntPtr)(delegate* unmanaged&lt;…&gt;)&amp;Entry</c>, which the compiler accepts only for a
    /// method marked <see cref="UnmanagedCallersOnlyAttribute"/>.
    /// <para>
    /// The source is read as it is, comments included: stripping them with a pattern also strips
    /// whatever a string such as <c>"Frameworks/*"</c> opens, and a guard that can hide code is worse
    /// than one a comment can trip.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("eQuantic.UI.Native.Shell.Apple")]
    [InlineData("eQuantic.UI.Native.Shell.iOS")]
    public void NoDelegateBecomesAFunctionPointer(string project)
    {
        var offenders = Code(project)
            .Where(file => file.Code.Contains("GetFunctionPointerForDelegate", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();

        offenders.Should().BeEmpty(
            "a device build has no JIT to make a wrapper for a delegate; hand native code a static " +
            "[UnmanagedCallersOnly] entry point instead");
    }

    /// <summary>
    /// The other way a delegate becomes a function pointer: a P/Invoke that takes one, which the
    /// marshaller turns into a pointer at the call without the word appearing anywhere. Read from the
    /// compiled shared shell, the one of the two this suite can load.
    /// </summary>
    [Fact]
    public void NoPInvokeTakesADelegate()
    {
        var methods = typeof(ObjCBlock).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            .Where(method => method.Attributes.HasFlag(System.Reflection.MethodAttributes.PinvokeImpl))
            .ToArray();
        methods.Should().NotBeEmpty("the shared shell is all P/Invoke; finding none means this read nothing");

        var offenders = methods
            .Where(method => method.GetParameters().Any(parameter => typeof(Delegate).IsAssignableFrom(parameter.ParameterType)))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();
        offenders.Should().BeEmpty("a delegate passed to native code is a function pointer a device build never prepared");
    }

    /// <summary>
    /// A method registered with the Objective-C runtime, called the way the system calls it: an
    /// AppleEvent sent to the handler class through <c>objc_msgSend</c>, which dispatches to the
    /// entry point the class was given, and the URL arrives at the relay.
    /// </summary>
    [MacFact]
    public void AnAppleEvent_ReachesTheRelay_ThroughTheRegisteredEntryPoint()
    {
        var relay = AppleDeepLinks.Install();
        var url = $"eqtest://open/{Guid.NewGuid():N}";
        Uri? heard = null;
        using var subscription = relay.Subscribe(received => heard = received);

        var descriptors = objc_getClass("NSAppleEventDescriptor");
        var gurl = FourCharCode("GURL");
        var urlEvent = AppleEvent(descriptors, Sel("appleEventWithEventClass:eventID:targetDescriptor:returnID:transactionID:"),
            gurl, gurl, Send(descriptors, Sel("nullDescriptor")), -1 /* kAutoGenerateReturnID */, 0 /* kAnyTransactionID */);
        SetParam(urlEvent, Sel("setParamDescriptor:forKeyword:"),
            Send(descriptors, Sel("descriptorWithString:"), NSString(url)), FourCharCode("----"));

        var handler = Send(Send(objc_getClass("EQDeepLinkHandler"), Sel("alloc")), Sel("init"));
        SendVoid(handler, Sel("handleURLEvent:withReplyEvent:"), urlEvent, IntPtr.Zero);

        heard.Should().Be(new Uri(url), "the runtime dispatched to the registered entry point");
    }

    private static uint FourCharCode(string code) =>
        ((uint)code[0] << 24) | ((uint)code[1] << 16) | ((uint)code[2] << 8) | code[3];

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static partial IntPtr AppleEvent(IntPtr receiver, IntPtr selector,
        uint eventClass, uint eventId, IntPtr target, short returnId, int transactionId);

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static partial void SetParam(IntPtr receiver, IntPtr selector, IntPtr descriptor, uint keyword);

    /// <summary>Every <c>.cs</c> of a project under <c>src/</c>, by path.</summary>
    private static IEnumerable<(string Path, string Code)> Code(string project)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", project)))
            here = here.Parent;
        here.Should().NotBeNull("the guard reads {0}'s source, so it has to find the tree", project);

        var folder = Path.Combine(here!.FullName, "src", project);
        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();
        files.Should().HaveCountGreaterThan(3, "a guard that read no file would pass having checked nothing");

        return files.Select(path => (Path.GetRelativePath(folder, path), File.ReadAllText(path)));
    }
}
