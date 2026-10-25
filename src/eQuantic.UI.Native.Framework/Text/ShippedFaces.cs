using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The files a shell has handed its text engine (#1055), so each one is registered once: the first
/// time text asks for a family that ships files. The family reaches the text service inside the
/// style, so nothing has to list the families up front, and one a page names at a call site loads as
/// the theme's do.
/// <para>
/// Each shell brings its engine's own registration (CoreText, DirectWrite, Android's typefaces) and
/// this brings the bytes, read from the assembly each file is embedded in.
/// </para>
/// </summary>
/// <param name="register">The engine's registration of one file, answering whether it took it.</param>
public sealed class ShippedFaces(Func<FontFile, byte[], bool> register)
{
    private readonly Dictionary<FontFile, bool> _registered = new();
    private readonly Lock _gate = new();

    /// <summary>
    /// Registers each file of <paramref name="family"/> not registered yet. Answers whether every one
    /// of them is: a file that cannot be read, or that the engine refuses, leaves the family to fall
    /// back, and the text service reports it as it reports any family it could not resolve. A family
    /// with no files answers false, since there is nothing of it to register.
    /// </summary>
    public bool Register(FontFamily? family)
    {
        if (family is not { Files.Count: > 0 }) return false;
        lock (_gate)
        {
            var all = true;
            foreach (var file in family.Files)
            {
                if (!_registered.TryGetValue(file, out var done))
                    _registered[file] = done = file.Read() is { } bytes && register(file, bytes);
                all &= done;
            }
            return all;
        }
    }

    /// <summary>Whether <paramref name="file"/> is registered with the engine.</summary>
    public bool IsRegistered(FontFile file)
    {
        lock (_gate) return _registered.GetValueOrDefault(file);
    }
}
