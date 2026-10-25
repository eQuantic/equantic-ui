using System.Security.Cryptography;
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

    /// <summary>
    /// The file of <paramref name="family"/> that sets text at <paramref name="weight"/>, upright or
    /// <paramref name="italic"/>, chosen as a browser chooses among a family's faces (CSS Fonts §5.2), so a
    /// family draws from the same file on every target. The files of the slant asked for, or every file
    /// when the family ships none of that slant; among them, one whose weights hold the weight asked for;
    /// failing that, for a weight below 400 the nearest lighter file first and then the nearest heavier,
    /// for one above 500 the nearest heavier first and then the nearest lighter, and for 400 to 500 the
    /// heavier files up to 500, then the lighter, then the heavier past 500. Null for a family with no files.
    /// <para>
    /// The shell opens THAT file, rather than asking its engine for the family at a weight: the engine
    /// reads a face's weight from its file, and Google's static Thin and ExtraLight of Inter carry the
    /// same weight class there, 250, while CoreText's match by weight picked a neighbouring file for Thin,
    /// Light and Black as measured (#1082).
    /// </para>
    /// </summary>
    public static FontFile? FileFor(FontFamily family, FontWeight weight, bool italic)
    {
        var asked = (int)weight;
        var slant = family.Files.Where(file => file.Italic == italic).ToList();
        var files = slant.Count > 0 ? slant : family.Files.ToList();
        if (files.FirstOrDefault(file => file.MinWeight <= asked && asked <= file.MaxWeight) is { } holding) return holding;

        var lighter = files.Where(file => file.MaxWeight < asked).OrderByDescending(file => file.MaxWeight);
        var heavier = files.Where(file => file.MinWeight > asked).OrderBy(file => file.MinWeight);
        var order = asked switch
        {
            < 400 => lighter.Concat(heavier),
            > 500 => heavier.Concat(lighter),
            _ => heavier.Where(file => file.MinWeight <= 500).Concat(lighter).Concat(heavier.Where(file => file.MinWeight > 500)),
        };
        return order.FirstOrDefault();
    }

    /// <summary>Whether <paramref name="file"/> is registered with the engine.</summary>
    public bool IsRegistered(FontFile file)
    {
        lock (_gate) return _registered.GetValueOrDefault(file);
    }

    /// <summary>
    /// Where a text engine that opens a face only from a FILE, as Android's does, opens
    /// <paramref name="file"/>: its <paramref name="bytes"/> written once under <paramref name="folder"/>,
    /// named by their hash, and published only WHOLE, written beside that name and then moved onto it,
    /// so a write a full disk cut short is never what a later launch opens. Null when the cache cannot
    /// take it, which is a face that did not register: it threw where text was measured, and the
    /// family failed the text instead of falling back (Copilot's third review of #1062).
    /// </summary>
    public static string? CacheFile(string folder, FontFile file, byte[] bytes)
    {
        var path = Path.Combine(folder, Convert.ToHexStringLower(SHA256.HashData(bytes))[..16] + Path.GetExtension(file.Resource));
        // A file under the face's name is the face when it is as long as the face. One cut short, by a
        // build that wrote in place or by the storage under it, is written again rather than trusted.
        if (new FileInfo(path) is { Exists: true, Length: var length } && length == bytes.LongLength) return path;
        var partial = path + "." + Path.GetRandomFileName();
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(partial, bytes);
            File.Move(partial, path, overwrite: true);
            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(partial);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The cache is failing already, and what is left is named apart from the face.
            }
            return null;
        }
    }
}
