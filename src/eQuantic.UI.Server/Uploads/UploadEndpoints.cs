using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using tusdotnet;

namespace eQuantic.UI.Server;

/// <summary>
/// The endpoint the pages upload files to, in resumable parts over the tus protocol, beside the Server
/// Actions' own. <c>MapUI</c> maps it when the app called <c>UseUploads</c>.
/// </summary>
internal static class UploadEndpoints
{
    /// <summary>Where the uploads are received; the runtime's client sends to the same path.</summary>
    public const string Path = "/api/_equantic/uploads";

    /// <summary>
    /// Maps the endpoint, open to an anonymous request: who may upload is decided per policy, and a
    /// visitor's upload is kept for the browser that sent it. An app's fallback authorization policy
    /// would otherwise refuse every visitor, a policy that accepts their files included.
    /// </summary>
    public static IEndpointRouteBuilder MapUploads(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTus(Path, context =>
            Task.FromResult(context.RequestServices.GetRequiredService<UploadProtocol>().Configuration)).AllowAnonymous();
        return endpoints;
    }
}
