namespace eQuantic.UI.Code;

/// <summary>
/// Where folds come from. The default implementation folds by INDENTATION, which works for every
/// language including the ones with no braces — an app with a parser hands back better ones.
/// </summary>
public interface ICodeFoldProvider
{
    IReadOnlyList<CodeFold> FoldsFor(CodeDocument document);
}
