using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// A shell's chance to register what the DEVICE can do — the camera, the photo library, the sensors
/// — as ordinary services, before anything is built. An app then takes them through a constructor
/// like any other dependency, and never learns which platform satisfied them.
/// <para>
/// Discovered exactly like <see cref="IPhotonRunner"/>, through
/// <see cref="PhotonCapabilitiesAttribute"/>, and for the same reason: an app names no shell, so
/// the shell has to name itself.
/// </para>
/// </summary>
public interface IPhotonCapabilities
{
    /// <summary>
    /// Registers this platform's realizations. Use <c>TryAdd*</c>: an app that registered its own —
    /// a fake in a test, a wrapper that logs — has already decided, and the shell does not overrule
    /// it.
    /// </summary>
    void Register(IServiceCollection services);
}

/// <summary>A shell assembly's declaration of what it can offer the device.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PhotonCapabilitiesAttribute(
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    Type providerType) : Attribute
{
    /// <summary>The provider, constructed by the host — annotated for the trimmer for the same
    /// reason <see cref="PhotonRunnerAttribute.RunnerType"/> is.</summary>
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type ProviderType { get; } = providerType;
}
