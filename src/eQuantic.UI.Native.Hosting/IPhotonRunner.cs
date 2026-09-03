namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// What actually opens a window, or hands control to UIKit. One per platform shell, and an app
/// never names it: the SDK references exactly one shell for the framework being built, and the
/// shell declares itself with <see cref="PhotonRunnerAttribute"/>.
/// </summary>
public interface IPhotonRunner
{
    void Run(PhotonApplication app);
}

/// <summary>
/// A shell assembly's declaration that it can run an app. The host looks for exactly one across the
/// entry assembly's references, which is how <c>app.Run()</c> stays the same line on every device.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PhotonRunnerAttribute(
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    Type runnerType) : Attribute
{
    /// <summary>
    /// The runner, constructed by the host. Annotated so the TRIMMER keeps the constructor: an app
    /// never names its shell in code, which is the whole point of the design and also exactly the
    /// evidence the trimmer uses to decide nothing needs it. Without this the published app dies at
    /// launch, having built and signed cleanly.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type RunnerType { get; } = runnerType;
}
