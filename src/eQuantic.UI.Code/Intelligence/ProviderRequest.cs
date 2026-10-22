namespace eQuantic.UI.Code;

/// <summary>
/// How an editor's sessions end a request they made of a provider. Cancelling runs at once, on this
/// thread, every callback the provider registered on its token, and gathers what they threw into one
/// <c>AggregateException</c> after running them all: that is the provider's failure, reported like any
/// other, and never the keystroke's, the move's or the edit's that ended the request.
/// </summary>
internal static class ProviderRequest
{
    internal static void Cancel(CancellationTokenSource? source, Action<Exception>? failed)
    {
        if (source is null) return;
        try
        {
            source.Cancel();
        }
        catch (Exception error)
        {
            failed?.Invoke(error);
        }
    }
}
