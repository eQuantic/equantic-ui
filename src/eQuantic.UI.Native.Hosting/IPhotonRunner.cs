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
public sealed class PhotonRunnerAttribute(Type runnerType) : Attribute
{
    public Type RunnerType { get; } = runnerType;
}
