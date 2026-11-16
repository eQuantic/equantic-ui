namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// A file of eqc's output, written only when its text changed (#1279). A save changes one screen,
/// and every module, map and catalogue was written again on every compile: their times moved with
/// nothing in them changed, which bun and every reader that goes by a file's time took for a change.
/// What decides that a file belongs to a compile is what the compile produced, never its time: the
/// prunes read the names the compile produced (the catalogues) or what the bundle wrote (bun's output).
/// </summary>
internal static class OutputFile
{
    /// <summary>Writes <paramref name="text"/> at <paramref name="path"/> unless the file holds it
    /// already, and answers whether it wrote.</summary>
    public static bool Write(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return false;
        File.WriteAllText(path, text);
        return true;
    }
}
