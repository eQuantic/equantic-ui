using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// Stamps <c>x-powered-by: eQuantic.UI</c> on every response the app sends.
/// <para>
/// An <see cref="IStartupFilter"/> rather than a middleware the app must remember to call:
/// <c>AddUI</c> registers it, ASP.NET runs it FIRST, and every response — SSR pages, Server
/// Actions, the static bundles — carries the stamp with zero lines in any Program.cs. Header set
/// via <c>OnStarting</c>, the only moment that is both early enough to beat the send and late
/// enough to survive middlewares that clear response state.
/// </para>
/// <para>
/// Just the NAME, no version: the stamp is provenance, and a version string in a response header
/// is a gift to vulnerability scanners with nothing in it for anyone else. Apps that must pass a
/// hardening checklist that flags ANY x-powered-by turn it off with
/// <see cref="UIOptions.DisablePoweredByHeader"/> — the option exists precisely so the default can
/// stay proud.
/// </para>
/// </summary>
internal sealed class PoweredByHeader : IStartupFilter
{
    internal const string Value = "eQuantic.UI";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, pipeline) =>
        {
            context.Response.OnStarting(static state =>
            {
                var response = ((HttpContext)state).Response;
                // Never clobber: if something upstream (a proxy header rewrite, an app's own
                // branding) already answered, the framework does not argue.
                if (!response.Headers.ContainsKey("x-powered-by"))
                    response.Headers["x-powered-by"] = Value;
                return Task.CompletedTask;
            }, context);
            return pipeline(context);
        });
        next(app);
    };
}
