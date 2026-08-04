using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>Touch ID on a Mac — the shared AppleBiometry, which is the same framework the
/// iPhone's Face ID answers through.</summary>
internal sealed class AppleBiometrics : IBiometrics
{
    public bool IsAvailable => AppleBiometry.IsAvailable();

    public async ValueTask<BiometricResult> AuthenticateAsync(string reason,
        CancellationToken cancellationToken = default) =>
        await AppleBiometry.AuthenticateAsync(reason, cancellationToken);
}
