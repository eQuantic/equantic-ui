namespace eQuantic.UI.Code;

/// <summary>
/// One row of a view. A <see cref="CodeRowKind.Line"/> row shows <see cref="Line"/>. A filler
/// stands before <see cref="Line"/>, and shows <see cref="SourceLine"/> of another document, or
/// <see cref="Label"/>, or nothing. A placeholder stands for the <see cref="Count"/> lines hidden
/// from <see cref="Line"/> on.
/// </summary>
public readonly record struct CodeRow(CodeRowKind Kind, int Line, int Count = 1, int SourceLine = -1, string? Label = null);
