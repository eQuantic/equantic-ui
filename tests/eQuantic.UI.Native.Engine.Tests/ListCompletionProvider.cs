using eQuantic.UI.Code;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Offers what it was made with, whatever was typed: the list is the view's to test.</summary>
internal sealed class ListCompletionProvider(params CodeCompletionItem[] items) : ICodeCompletionProvider
{
    public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
        CodeCompletionContext context, CancellationToken cancellation) =>
        Task.FromResult(new CodeCompletionList(items));
}
