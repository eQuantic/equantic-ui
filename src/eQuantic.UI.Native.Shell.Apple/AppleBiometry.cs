using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// Touch ID, Face ID, and the Mac's own. SHARED by the Apple shells, because LocalAuthentication is
/// the same framework on both and the only difference is which sensor answers.
/// <para>
/// The policy is "biometrics OR the device passcode": refusing the passcode would mean an app that
/// locks out anyone whose finger is wet, and the passcode is not a weaker proof — it is the thing
/// the biometrics stand in for.
/// </para>
/// </summary>
// Named for what it IS rather than for the framework behind it: `LocalAuthentication` is also the
// namespace the iOS bindings put their own types in, and a class that shadows a namespace makes
// every reference to it ambiguous in exactly the shell that needs it most.
public static class AppleBiometry
{
    private const string Framework =
        "/System/Library/Frameworks/LocalAuthentication.framework/LocalAuthentication";

    /// <summary>Biometrics, falling back to the device's own passcode.</summary>
    private const long PolicyBiometricsOrPasscode = 2; // LAPolicyDeviceOwnerAuthentication

    // The error codes worth telling apart. The rest are all "it did not work".
    private const long ErrorUserCancel = -2;
    private const long ErrorUserFallback = -3;
    private const long ErrorSystemCancel = -4;
    private const long ErrorBiometryNotEnrolled = -7;
    private const long ErrorBiometryNotAvailable = -6;

    /// <summary>How long the system's own sheet gets before the caller is told it did not work.</summary>
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(60);

    private delegate void ReplyBlock(IntPtr block, [MarshalAs(UnmanagedType.I1)] bool success, IntPtr error);

    /// <summary>Whether a reader exists AND has something enrolled — the two are one question to a
    /// caller deciding whether to offer the button at all.</summary>
    public static bool IsAvailable()
    {
        Load();
        var context = NewContext();
        if (context == IntPtr.Zero) return false;
        return SendBool(context, Sel("canEvaluatePolicy:error:"), PolicyBiometricsOrPasscode, IntPtr.Zero);
    }

    /// <summary>
    /// Asks, and answers how it went. The reason is shown by the system in its own sheet, verbatim.
    /// </summary>
    public static Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken cancellationToken)
    {
        Load();
        var context = NewContext();
        if (context == IntPtr.Zero) return Task.FromResult(BiometricResult.Unavailable);
        if (!SendBool(context, Sel("canEvaluatePolicy:error:"), PolicyBiometricsOrPasscode, IntPtr.Zero))
            return Task.FromResult(BiometricResult.Unavailable);

        var answer = new TaskCompletionSource<BiometricResult>();

        // The block and its delegate are held until the reply arrives: a block whose delegate was
        // collected is a crash in another thread, minutes later, pointing nowhere useful.
        ReplyBlock reply = null!;
        ObjCBlock? block = null;
        reply = (_, success, error) =>
        {
            answer.TrySetResult(success ? BiometricResult.Succeeded : Interpret(error));
            block?.Dispose();
        };
        block = new ObjCBlock(reply);

        // Cancelling stops the WAIT; the system sheet is the system's, and nothing here dismisses it.
        cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken));

        // A reply that never comes is a button that never answers — the failure mode this whole
        // codebase spent a day removing. Whatever the reason (an unsigned bundle, a sheet the
        // system declined to show), the caller gets an answer it can act on rather than a promise
        // that stays pending for ever.
        _ = Task.Delay(ReplyTimeout, cancellationToken)
            .ContinueWith(_ => answer.TrySetResult(BiometricResult.Failed),
                TaskContinuationOptions.OnlyOnRanToCompletion);

        SendVoid(context, Sel("evaluatePolicy:localizedReason:reply:"),
            PolicyBiometricsOrPasscode, NSString(reason), block.Handle);

        // Held to the end of the call so neither is collected while the framework holds them.
        GC.KeepAlive(reply);
        return answer.Task;
    }

    /// <summary>Which of the four "did not succeed" situations this was.</summary>
    private static BiometricResult Interpret(IntPtr error)
    {
        if (error == IntPtr.Zero) return BiometricResult.Failed;
        return SendLong(error, Sel("code")) switch
        {
            ErrorUserCancel or ErrorSystemCancel => BiometricResult.Cancelled,
            ErrorUserFallback => BiometricResult.FallbackRequested,
            ErrorBiometryNotEnrolled => BiometricResult.NotEnrolled,
            ErrorBiometryNotAvailable => BiometricResult.Unavailable,
            _ => BiometricResult.Failed,
        };
    }

    private static IntPtr NewContext()
    {
        var type = objc_getClass("LAContext");
        return type == IntPtr.Zero ? IntPtr.Zero : Send(Send(type, Sel("alloc")), Sel("init"));
    }

    private static void Load() => dlopen(Framework, 2 /* RTLD_NOW */);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlopen(string path, int mode);
}
