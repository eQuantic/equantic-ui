using System.Text.Json;
using System.Text.Json.Serialization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// An <see cref="UploadedFile"/> inside an action's argument, refused. Only an action's own parameter is
/// bound from the server's record of the upload (<see cref="UploadBinding"/>): one in a DTO, a list or a
/// dictionary was read as the page wrote it, its id, name, size and policy the caller's to choose (#1291).
/// A null one stays null, so a DTO whose file is optional still crosses.
/// </summary>
internal sealed class UploadedFileArgumentConverter : JsonConverter<UploadedFile>
{
    public override bool CanConvert(Type typeToConvert) => typeof(UploadedFile).IsAssignableFrom(typeToConvert);

    public override UploadedFile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new JsonException(
            "An uploaded file is bound only as an action's own parameter: take it as a parameter of its own, not inside another value.");

    public override void Write(Utf8JsonWriter writer, UploadedFile value, JsonSerializerOptions options) =>
        throw new NotSupportedException("The converter reads an action's arguments only.");
}
