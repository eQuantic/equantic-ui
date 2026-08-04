namespace eQuantic.UI.Primitives;

/// <summary>
/// An image a capability handed over — from the photo library, from the camera. ENCODED bytes as the
/// device produced them (a JPEG, a PNG, a HEIC), not pixels: decoding is the image loader's job on
/// every target already, and a capability that decoded eagerly would cost megabytes to hand back a
/// thumbnail nobody asked for.
/// </summary>
/// <param name="Bytes">The file's own bytes.</param>
/// <param name="MimeType">What they are — <c>image/jpeg</c>, <c>image/png</c>, <c>image/heic</c>.</param>
/// <param name="Width">Pixel width, when the device reported one; zero when it did not.</param>
/// <param name="Height">Pixel height, when the device reported one; zero when it did not.</param>
/// <param name="Name">The file's name where there is one — a picker has it, a camera does not.</param>
public sealed record ImageData(
    byte[] Bytes,
    string MimeType,
    int Width = 0,
    int Height = 0,
    string? Name = null)
{
    /// <summary>How big the file is. Useful before deciding to upload it.</summary>
    public int ByteCount => Bytes.Length;
}
