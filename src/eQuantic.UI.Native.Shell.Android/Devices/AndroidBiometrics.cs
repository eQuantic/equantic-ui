using Android.Hardware.Biometrics;
using Android.OS;
using AndroidX = global::Android;
using eQuantic.UI.Primitives;
using Java.Lang;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Android's fingerprint and face readers, through the FRAMEWORK's own BiometricPrompt rather than
/// the AndroidX one — same prompt, same behaviour, and no dependency an app has to carry for a
/// capability it may never use.
/// <para>
/// DeviceCredential is allowed alongside biometrics: refusing the lock screen would lock out anyone
/// whose finger is wet, and the PIN is not a weaker proof — it is the thing the fingerprint stands
/// in for.
/// </para>
/// </summary>
internal sealed class AndroidBiometrics : IBiometrics
{
    public bool IsAvailable
    {
        get
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Q) return false;
            if (PhotonActivity.Current?.GetSystemService(global::Android.Content.Context.BiometricService)
                is not BiometricManager manager) return false;
            // 0 is BIOMETRIC_SUCCESS. The named constant is deprecated in favour of the overload
            // that takes authenticators, which is not bound here — and the value is the contract.
            return manager.CanAuthenticate() == 0;
        }
    }

    public ValueTask<BiometricResult> AuthenticateAsync(string reason,
        CancellationToken cancellationToken = default)
    {
        if (PhotonActivity.Current is not { } activity || Build.VERSION.SdkInt < BuildVersionCodes.Q)
            return new ValueTask<BiometricResult>(BiometricResult.Unavailable);

        var answer = new TaskCompletionSource<BiometricResult>();
        var prompt = new BiometricPrompt.Builder(activity)
            .SetTitle("Confirm it's you")
            .SetDescription(reason)
            // Without a way out the builder throws: a prompt the user cannot leave is not allowed.
            // The device's own credential is that way out, and it weakens nothing — it is the thing
            // the fingerprint stands in for. (0x00FF | 0x8000: BIOMETRIC_WEAK | DEVICE_CREDENTIAL,
            // stated as values because the named flags are not bound.)
            .SetAllowedAuthenticators(0x000000FF | 0x00008000)
            .Build();

        // Android's own cancellation object. The token cancels the WAIT and this cancels the
        // PROMPT — both are needed, and only the second actually takes the sheet off the screen.
        var signal = new CancellationSignal();
        cancellationToken.Register(() =>
        {
            signal.Cancel();
            answer.TrySetCanceled(cancellationToken);
        });

        prompt.Authenticate(signal, activity.MainExecutor!, new Callback(answer));
        return new ValueTask<BiometricResult>(answer.Task);
    }

    private sealed class Callback(TaskCompletionSource<BiometricResult> answer)
        : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result) =>
            answer.TrySetResult(BiometricResult.Succeeded);

        /// <summary>A wrong finger. The system stays up and lets them try again, so this is not the
        /// end of anything — reporting it as a failure here would end it early.</summary>
        public override void OnAuthenticationFailed() { }

        public override void OnAuthenticationError(BiometricErrorCode errorCode, ICharSequence? errString) =>
            answer.TrySetResult(errorCode switch
            {
                BiometricErrorCode.UserCanceled or BiometricErrorCode.Canceled => BiometricResult.Cancelled,
                // The "Cancel" button on the sheet — the user leaving, by the door provided.
                (BiometricErrorCode)13 => BiometricResult.Cancelled,
                BiometricErrorCode.NoBiometrics => BiometricResult.NotEnrolled,
                BiometricErrorCode.HwNotPresent or BiometricErrorCode.HwUnavailable =>
                    BiometricResult.Unavailable,
                _ => BiometricResult.Failed,
            });
    }
}
