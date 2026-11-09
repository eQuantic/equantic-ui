using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// The two headers that come with the Content-Security-Policy (#1267), on every response the app
/// sends: <c>X-Content-Type-Options: nosniff</c>, so a script or a stylesheet is read only as the type
/// it was sent as, and <c>Referrer-Policy: strict-origin-when-cross-origin</c>, so a link to another
/// site carries the origin and never the path and query.
/// <para>
/// A startup filter, as <see cref="PoweredByHeader"/> is: registered by
/// <see cref="UIOptions.UseContentSecurityPolicy"/>, run first, and never clobbering a value something
/// upstream already wrote.
/// </para>
/// </summary>
internal sealed class SecurityHeaders : IStartupFilter
{
    internal const string NoSniff = "nosniff";
    internal const string Referrer = "strict-origin-when-cross-origin";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, pipeline) =>
        {
            context.Response.OnStarting(static state =>
            {
                var headers = ((HttpContext)state).Response.Headers;
                if (!headers.ContainsKey("X-Content-Type-Options")) headers["X-Content-Type-Options"] = NoSniff;
                if (!headers.ContainsKey("Referrer-Policy")) headers["Referrer-Policy"] = Referrer;
                return Task.CompletedTask;
            }, context);
            return pipeline(context);
        });
        next(app);
    };
}
