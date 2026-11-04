using System.Text.Json;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// How a Server Action's <see cref="UploadedFile"/> parameter is bound from the page's argument. The
/// page sends the upload as it knows it, and only its id is read: the name, the type, the size and the
/// policy are the server's record, never what the page wrote. The file is bound only for the request's
/// owner (<see cref="UploadOwner"/>) and only once its last byte has arrived; otherwise the action does
/// not run, and the page is told why.
/// </summary>
internal static class UploadBinding
{
    /// <summary>The file <paramref name="argument"/> names, or null for a null argument.</summary>
    /// <exception cref="UploadNotBoundException">The argument names no upload this request may receive.</exception>
    public static async Task<UploadedFile?> BindAsync(HttpContext context, JsonElement argument)
    {
        if (argument.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (Id(argument) is not { } id)
            throw new UploadNotBoundException(StatusCodes.Status400BadRequest, "The file names no upload.");

        var store = context.RequestServices.GetService<UploadStore>()
                    ?? throw new UploadNotBoundException(StatusCodes.Status500InternalServerError,
                        "The app receives no uploads: call UseUploads on AddUI.");
        var record = await store.FindAsync(id);
        var now = context.RequestServices.GetService<TimeProvider>()?.GetUtcNow() ?? DateTimeOffset.UtcNow;
        if (record is null || record.Expires <= now)
            throw new UploadNotBoundException(StatusCodes.Status404NotFound,
                "There is no upload with this id: it was never made, or it expired.");
        if (!UploadOwner.Owns(context, record.Owner))
            throw new UploadNotBoundException(StatusCodes.Status403Forbidden, "This upload belongs to someone else.");
        if (!await store.IsCompleteAsync(record, context.RequestAborted))
            throw new UploadNotBoundException(StatusCodes.Status409Conflict, "The upload is not finished yet.");
        return new ReceivedUploadedFile(record, store);
    }

    /// <summary>The upload's id in the page's value, read without regard to the case of its name.</summary>
    private static string? Id(JsonElement argument) =>
        argument.ValueKind == JsonValueKind.Object
            ? argument.EnumerateObject()
                .Where(property => string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null)
                .FirstOrDefault(value => !string.IsNullOrEmpty(value))
            : null;
}
