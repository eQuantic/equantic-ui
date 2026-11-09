using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The kinds of file the console takes, declared once: the page reads it to say what it accepts and to
/// refuse what it does not, and the server holds every upload to it (<c>UseUploads</c> in Program.cs).
/// </summary>
public static class SampleUploads
{
    /// <summary>3D models and their notes, up to 64 MB: large enough to travel in several parts.</summary>
    public static readonly UploadPolicy Models = new("models",
        filters: [new FileFilter("3D models", "stl", "obj", "3mf"), new FileFilter("Notes", "txt")],
        maxSize: 64 * 1024 * 1024);
}
