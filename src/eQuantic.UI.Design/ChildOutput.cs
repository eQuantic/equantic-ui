using System.Diagnostics;

namespace eQuantic.UI.Design;

/// <summary>
/// Reads a child to its end under one deadline, both streams from the start. The design host waited for the
/// renderer to exit before it read either stream, so a renderer that wrote more than a pipe holds, 64 KB on
/// macOS and Linux, blocked on the full pipe, never exited, and was reported as a page whose Build() does not
/// return (#997). A stream ends when the last process holding it closes it, so the deadline holds the end of
/// both streams as well as the exit.
/// </summary>
internal static class ChildOutput
{
    /// <summary>Waits for <paramref name="child"/>, which started with both streams redirected, and what it
    /// wrote. <c>Ended</c> is false when the deadline came first, with what it wrote until then.</summary>
    internal static (bool Ended, string Output, string Error) Read(Process child, TimeSpan deadline)
    {
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        var ended = Task.WhenAll(output, error, child.WaitForExitAsync()).Wait(deadline);
        return (ended, Finished(output), Finished(error));
    }

    private static string Finished(Task<string> stream) => stream.IsCompletedSuccessfully ? stream.Result : "";
}
