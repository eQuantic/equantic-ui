using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>Face ID and Touch ID — the same shared LocalAuthentication the Mac uses. The only
/// difference between the platforms is which sensor answers, which is precisely the kind of
/// difference an app should never have to know about.</summary>
internal sealed class IosBiometrics : IBiometrics
{
    public bool IsAvailable => AppleBiometry.IsAvailable();

    public async ValueTask<BiometricResult> AuthenticateAsync(string reason,
        CancellationToken cancellationToken = default) =>
        await AppleBiometry.AuthenticateAsync(reason, cancellationToken);
}
