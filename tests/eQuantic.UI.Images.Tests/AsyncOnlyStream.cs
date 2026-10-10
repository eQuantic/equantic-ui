namespace eQuantic.UI.Images.Tests;

/// <summary>
/// A stream that reads only asynchronously and cannot seek, as ASP.NET Core's request body does with
/// <c>AllowSynchronousIO</c> off, its default, and that counts the bytes it hands out.
/// </summary>
internal sealed class AsyncOnlyStream(byte[] bytes) : Stream
{
    private readonly MemoryStream _inner = new(bytes);

    /// <summary>How many bytes the reader took.</summary>
    public long BytesRead { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    // Kestrel's own refusal, word for word.
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException(
            "Synchronous operations are disallowed. Call ReadAsync or set AllowSynchronousIO to true instead.");

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        BytesRead += read;
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }
}
