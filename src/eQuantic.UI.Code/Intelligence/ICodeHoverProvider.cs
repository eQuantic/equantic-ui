namespace eQuantic.UI.Code;

/// <summary>Where hover text comes from — the same shape as completion, and for the same reason.</summary>
public interface ICodeHoverProvider
{
    Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position,
        CancellationToken cancellation);
}
