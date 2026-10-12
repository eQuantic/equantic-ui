namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// An ask a framework answers ONCE, through a block: the answer's task, and the block that delivers
/// it. The block's entry point finds this through <see cref="ObjCBlock.Context{T}"/> and calls
/// <see cref="Reply"/>, which settles the task and lets the context go while the framework still
/// holds the block (see <see cref="ObjCBlock.ReleaseContext"/>).
/// <para>
/// Continuations run asynchronously: settling the task from inside a native callback would otherwise
/// run whatever awaited it on the framework's queue, inside that callback.
/// </para>
/// </summary>
internal sealed class PendingReply<T>
{
    private readonly TaskCompletionSource<T> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="invoke">The block's static <c>[UnmanagedCallersOnly]</c> entry point.</param>
    public PendingReply(IntPtr invoke) => Block = new ObjCBlock(invoke, this);

    /// <summary>What gets passed where the framework expects the completion block.</summary>
    public ObjCBlock Block { get; }

    public Task<T> Task => _answer.Task;

    /// <summary>The framework's answer, from the block's entry point.</summary>
    public void Reply(T value)
    {
        _answer.TrySetResult(value);
        Block.ReleaseContext();
    }

    /// <summary>An answer that does not wait for the framework: a timeout, for one.</summary>
    public void Settle(T value) => _answer.TrySetResult(value);

    public void Cancel(CancellationToken cancellationToken) => _answer.TrySetCanceled(cancellationToken);
}
