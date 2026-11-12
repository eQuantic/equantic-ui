namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// Ends the app's hot reload with its host's services as well as when the app stops (#1375). It was
/// disposed only on <c>ApplicationStopping</c>, and disposing a host does not stop it, so a host disposed
/// without being stopped, as an integration test disposes one, kept watching its content root and kept
/// the compiler it had started, whose working directory is that root (#1366). The container disposes this
/// singleton when the host's services are disposed, whichever comes first, and disposing twice does
/// nothing more.
/// </summary>
internal sealed class HotReloadLifetime : IDisposable
{
    private HotReloadService? _service;

    /// <summary>Owns <paramref name="service"/> from now on, for the host's whole life.</summary>
    internal void Hold(HotReloadService service) => Interlocked.Exchange(ref _service, service)?.Dispose();

    public void Dispose() => Interlocked.Exchange(ref _service, null)?.Dispose();
}
