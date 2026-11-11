using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace eQuantic.UI.Server;

/// <summary>
/// Puts ASP.NET's response compression in front of the app's pipeline. <c>AddUI</c> registered the Brotli
/// and Gzip providers and nothing applied them, so an app's <c>runtime.js</c>, near a megabyte, and its
/// modules went out uncompressed, whatever the browser accepted (#1002). The developer's Program.cs asks for
/// none of this, so the SDK says it, through the mechanism .NET has for a library to add to a pipeline it
/// does not own. An app that calls <c>UseResponseCompression</c> as well changes nothing: a response that
/// already says its encoding is left alone.
/// </summary>
internal sealed class ResponseCompressionFirst : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseResponseCompression();
        next(app);
    };
}
