using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// A request body read no further than its cap: past it, the read throws the
/// <see cref="BadHttpRequestException"/> Kestrel throws for its own limit, with 413. The Server Action
/// endpoint set Kestrel's limit only where the feature that carries it was still writable, and a
/// middleware that had started reading the body first left it read-only: the cap was skipped, and no
/// line said so (#1291). It is read through this one in every case now.
/// </summary>
internal sealed class BoundedReadStream(Stream inner, long cap) : Stream
{
    private long _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _read; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => Counted(inner.Read(buffer, offset, count));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Counted(await inner.ReadAsync(buffer, cancellationToken));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int Counted(int read)
    {
        _read += read;
        if (_read > cap)
            throw new BadHttpRequestException($"The request body is larger than {cap} bytes.", StatusCodes.Status413PayloadTooLarge);
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
