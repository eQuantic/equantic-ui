using System.ComponentModel;
using Microsoft.Build.Framework;

namespace eQuantic.UI.Sdk.Tasks;

/// <summary>
/// Answers <see cref="Program"/>: the program <see cref="Archive"/> holds, unpacked by
/// <see cref="ProgramArchive"/> into a folder named by the zip's content, under the user's cache.
/// Nothing is written beside the zip, so a package cache may be read-only (#364), and a zip that
/// changes is unpacked again rather than shadowed by the program an older one left (#932).
/// </summary>
public sealed class UnpackProgram : Microsoft.Build.Utilities.Task
{
    /// <summary>The zip, whose one program is named like it without <c>.zip</c>.</summary>
    [Required]
    public string Archive { get; set; } = "";

    /// <summary>The cache to unpack under: <see cref="ProgramArchive.DefaultCache"/> when it is not set.</summary>
    public string Cache { get; set; } = "";

    /// <summary>The program, ready to run.</summary>
    [Output]
    public string Program { get; private set; } = "";

    /// <inheritdoc />
    public override bool Execute()
    {
        var cache = Cache;
        try
        {
            if (cache.Length == 0) cache = ProgramArchive.DefaultCache();
            var (program, unpacked) = ProgramArchive.Unpack(Archive, cache);
            Program = program;
            if (unpacked) Log.LogMessage(MessageImportance.High, "Unpacked {0} into {1}", Archive, Path.GetDirectoryName(program));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or Win32Exception)
        {
            Log.LogError("{0} could not be unpacked into {1}: {2}", Archive, cache.Length == 0 ? "the user's cache" : cache, e.Message);
            return false;
        }
    }
}
