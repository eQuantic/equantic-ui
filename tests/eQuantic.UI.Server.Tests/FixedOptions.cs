using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server.Tests;

/// <summary>An options monitor that holds one value and never changes, for a type built by hand.</summary>
internal sealed class FixedOptions<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
