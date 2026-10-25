namespace eQuantic.UI.Primitives;

/// <summary>
/// One file of a <see cref="FontFamily"/>: an OpenType font embedded in an assembly, the weights it
/// sets and whether it is the italic cut.
/// <para>
/// OpenType only, <c>.ttf</c> or <c>.otf</c>, because it is the one format every target's text
/// engine reads as it is: CoreText, DirectWrite, Android's typefaces and every browser. The file
/// travels inside the assembly, so the server that serves it and the native shell that registers it
/// read the same bytes from the same place, and nothing is copied into an app's bundle.
/// </para>
/// <para>
/// An app never writes one: the SDK reads the files under its <c>Assets</c> folder and writes a
/// catalog of them (<see cref="FontCatalogAttribute"/>). A library that ships a theme with its own
/// faces embeds the files and declares them with this.
/// </para>
/// </summary>
/// <param name="Assembly">The simple name of the assembly the file is embedded in.</param>
/// <param name="Resource">The file's manifest resource name in that assembly, ending in <c>.ttf</c> or <c>.otf</c>.</param>
/// <param name="MinWeight">The lightest weight it sets, from 1 to 1000: a static face's only weight.</param>
/// <param name="MaxWeight">The heaviest. A variable font's weight axis runs from <paramref name="MinWeight"/> to here.</param>
/// <param name="Italic">Whether it is the italic cut of its family.</param>
public sealed record FontFile(string Assembly, string Resource, int MinWeight, int MaxWeight, bool Italic = false)
{
    /// <inheritdoc cref="FontFile" path="/param[@name='Assembly']"/>
    public string Assembly { get; init; } = Assembly is { Length: > 0 }
        ? Assembly
        : throw new ArgumentException("A face names the assembly it is embedded in.", nameof(Assembly));

    /// <inheritdoc cref="FontFile" path="/param[@name='Resource']"/>
    public string Resource { get; init; } = Resource is { Length: > 0 }
        && (Resource.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || Resource.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
        ? Resource
        : throw new ArgumentException("A face is an OpenType file, a .ttf or an .otf.", nameof(Resource));

    /// <inheritdoc cref="FontFile" path="/param[@name='MinWeight']"/>
    public int MinWeight { get; init; } = MinWeight is >= 1 and <= 1000
        ? MinWeight
        : throw new ArgumentOutOfRangeException(nameof(MinWeight), "A face's weights run from 1 to 1000.");

    /// <inheritdoc cref="FontFile" path="/param[@name='MaxWeight']"/>
    public int MaxWeight { get; init; } = MaxWeight is >= 1 and <= 1000 && MaxWeight >= MinWeight
        ? MaxWeight
        : throw new ArgumentOutOfRangeException(nameof(MaxWeight), "A face's weights run from 1 to 1000, the lightest first.");

    /// <summary>
    /// The file's bytes, read from the assembly it is embedded in, or null when the assembly or the
    /// resource is not there. HOST ONLY: the server serves them and a native shell registers them with
    /// its text engine, and a page never reads a file.
    /// </summary>
    [ServerOnly]
    public byte[]? Read()
    {
        System.Reflection.Assembly assembly;
        try
        {
            assembly = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(Assembly));
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return null;
        }
        using var stream = assembly.GetManifestResourceStream(Resource);
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
