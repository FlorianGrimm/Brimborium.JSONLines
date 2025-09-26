#pragma warning disable IDE0063 // Use simple 'using' statement
#pragma warning disable IDE0057 // Use range operator

namespace Brimborium.JSONLines;

/// <summary>
/// Split a stream into multiple streams by newline.
/// </summary>
public sealed class SplitStream : Stream {
    private static byte[] ArrWhiteSpace = new byte[] { 9, 10, 13, 32 };
    private static byte[] ArrNewLines = new byte[] { 10, 13 };

    // the size of bytes to read from the stream at once
    private readonly int _ChunkSize;

    // the size of the private buffer - it's 4 times the chunk size
    private readonly int _BufferSize;

    // the private buffer - will be cleared when the stream is disposed
    private readonly byte[] _Buffer;

    // the giveninner stream
    private Stream? _InnerStream;

    // leave the stream open after disposing this instance
    private bool _LeaveOpen;

    // the position in the buffer where the next read should start
    private int _BufferStart;

    // the number of bytes in the buffer
    private int _BufferLength;

    // the length of the line in the buffer or -1 if there is no newline in the buffer.
    private int _LengthNewLine;

    // true if the end of the line is read from the inner stream - but there could be content in the buffer
    private bool _EndOfSplit;

    // true if the end of the stream is reached - but there could be content in the buffer
    private bool _EndOfStream;

    // true if the next line is ready to read; false MoveNextStream has not been called yet.
    private bool _ReadyToRead;

    /// <summary>
    /// create a new instance.
    /// </summary>
    /// <param name="stream">The stream to read and split into multiple streams.</param>
    /// <param name="leaveOpen">leave the stream open after disposing this instance.</param>
    /// <param name="chunkSize">The size of bytes to read from the stream at once.</param>
    public SplitStream(Stream stream, bool leaveOpen = true, int chunkSize = 0) {
        if (chunkSize <= 0) { this._ChunkSize = 1024 * 16; } else { this._ChunkSize = chunkSize; }
        this._BufferSize = this._ChunkSize * 4;
        this._Buffer = new byte[this._BufferSize];
        this._InnerStream = stream;
        this._LeaveOpen = leaveOpen;
        this._BufferStart = 0;
        this._BufferLength = 0;
        this._LengthNewLine = -1;
        this._EndOfSplit = false;
        this._EndOfStream = false;
        this._ReadyToRead = false;
    }

    /// <inheritdoc/>
    public override void Close() {
        if (this._LeaveOpen) {
            this._InnerStream = null;
        } else {
            using (var stream = this._InnerStream) {
                this._InnerStream = null;
            }
        }
        Array.Clear(this._Buffer);
        base.Close();
    }

    /// <summary>
    /// Check if there is a next stream.
    /// </summary>
    /// <returns>true if there is a next stream.</returns>
    public bool MoveNextStream() {
        if (this._InnerStream is { } stream) {
            this._EndOfSplit = false;
            this._ReadyToRead = this.Prefetch();
            return _ReadyToRead;
        } else {
            return false;
        }
    }

    /// <summary>
    /// Check if there is a next stream.
    /// </summary>
    /// <returns>true if there is a next stream.</returns>
    public async ValueTask<bool> MoveNextStreamAsync(CancellationToken cancellationToken) {
        if (this._InnerStream is { } stream) {
            this._EndOfSplit = false;
            this._ReadyToRead = (await this.PrefetchAsync(cancellationToken).ConfigureAwait(false));
            return this._ReadyToRead;
        } else {
            return false;
        }
    }

    /// <summary>
    /// Load the first line into the buffer. 
    /// or use the last loaded conent also respecting the newline
    /// </summary>
    private bool Prefetch() {
        if (this._InnerStream is null) { return false; }

        if (0 <= this._LengthNewLine) {
            this.SkipWhitespace();
        }

        if (this._BufferLength == 0) {
            this.InnerRead(this._ChunkSize);
            if (this._EndOfStream) { return false; }
        }

        while (0 <= this._BufferLength) {
            this.SkipWhitespace();

            if (0 < this._BufferLength) {
                // SkipWhitespace has eaten all heading whitespace, 
                // then there is content (0 < this._BufferLength) 
                // and there could be a newline in the buffer.
                this.FindNextBufferEOFPosition();
                return true;
            } else {
                this.InnerRead(this._ChunkSize);
                if (this._EndOfStream) { return false; }
            }
        }

        {
            this.InnerRead(this._ChunkSize);
            return (0 < this._BufferLength);
        }
    }

    /// <summary>
    /// Load the first line into the buffer. 
    /// or use the last loaded conent also respecting the newline
    /// </summary>
    private async ValueTask<bool> PrefetchAsync(CancellationToken cancellationToken) {
        if (this._InnerStream is null) { return false; }

        if (0 <= this._LengthNewLine) {
            this.SkipWhitespace();
        }

        if (this._BufferLength == 0) {
            await this.InnerReadAsync(this._ChunkSize, cancellationToken);
            if (this._EndOfStream) { return false; }
        }

        while (0 <= this._BufferLength) {
            this.SkipWhitespace();

            if (0 < this._BufferLength) {
                // SkipWhitespace has eaten all heading whitespace, 
                // then there is content (0 < this._BufferLength) 
                // and there could be a newline in the buffer.
                this.FindNextBufferEOFPosition();
                return true;
            } else {
                await this.InnerReadAsync(this._ChunkSize, cancellationToken);
                if (this._EndOfStream) { return false; }
            }
        }

        {
            await this.InnerReadAsync(this._ChunkSize, cancellationToken);
            return (0 < this._BufferLength);
        }
    }

    // Stream
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => -1;

    public override long Position {
        get => -1;
        set { throw new NotSupportedException(); }
    }

    public override void Flush() { throw new NotSupportedException(); }


    public override int Read(Span<byte> buffer) {
        if (!this._ReadyToRead) { return 0; }
        if ((0 == this._BufferLength) && (this._EndOfSplit)) { return 0; }

        if (0 == this._LengthNewLine) {
            if (this.SkipWhitespace()) {
                this._LengthNewLine = -1;
            }
            return 0;
        } else if (0 < this._LengthNewLine) {
            return this.Copy(buffer);
        }

        if (this._EndOfStream) {
            if (this._BufferLength == 0) {
                return 0;
            } else {
                return this.Copy(buffer);
            }
        }

        if (this._ChunkSize < buffer.Length) { buffer = buffer.Slice(0, this._ChunkSize); }

        {
            if (buffer.Length < this._BufferLength) {
            } else if (this._BufferLength < this._ChunkSize) {
                if (0 < this._BufferLength
                    && this._ChunkSize * 3 < this._BufferStart + this._BufferLength) {
                    buffer = buffer.Slice(0, this._BufferLength);
                } else {
                    this.InnerRead(buffer.Length);
                    if (0 <= this._LengthNewLine) {
                        if (this._LengthNewLine < buffer.Length) {
                            buffer = buffer.Slice(0, this._LengthNewLine);
                        }
                        if (!this._EndOfSplit) {
                            this._EndOfSplit = true;
                        }
                    }
                    if (!this._EndOfSplit && this._EndOfStream) {
                        this._EndOfSplit = true;
                    }
                }
            }
        }

        return this.Copy(buffer);
    }

    public override int Read(byte[] buffer, int offset, int count) {
        if (!this._ReadyToRead) { return 0; }
        if (buffer.Length < (offset + count)) {
            return this.Read(buffer.AsSpan(offset));
        } else {
            return this.Read(buffer.AsSpan(offset, count));
        }
    }


    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        if (!this._ReadyToRead) { return 0; }
        if ((0 == this._BufferLength) && (this._EndOfSplit)) { return 0; }

        if (0 == this._LengthNewLine) {
            if (this.SkipWhitespace()) {
                this._LengthNewLine = -1;
            }
            return 0;
        } else if (0 < this._LengthNewLine) {
            return this.Copy(buffer.Span);
        }

        if (this._EndOfStream) {
            if (this._BufferLength == 0) {
                return 0;
            } else {
                return this.Copy(buffer.Span);
            }
        }

        if (this._ChunkSize < buffer.Length) { buffer = buffer.Slice(0, this._ChunkSize); }

        {
            if (buffer.Length < this._BufferLength) {
            } else if (this._BufferLength < this._ChunkSize) {
                if (0 < this._BufferLength
                    && this._ChunkSize * 3 < this._BufferStart + this._BufferLength) {
                    buffer = buffer.Slice(0, this._BufferLength);
                } else {
                    await this.InnerReadAsync(buffer.Length, cancellationToken);
                    if (0 <= this._LengthNewLine) {
                        if (this._LengthNewLine < buffer.Length) {
                            buffer = buffer.Slice(0, this._LengthNewLine);
                        }
                        if (!this._EndOfSplit) {
                            this._EndOfSplit = true;
                        }
                    }
                    if (!this._EndOfSplit && this._EndOfStream) {
                        this._EndOfSplit = true;
                    }
                }
            }
        }

        return this.Copy(buffer.Span);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        if (!this._ReadyToRead) { return 0; }

        if (buffer.Length < (offset + count)) {
            return await this.ReadAsync(buffer.AsMemory(offset), cancellationToken);
        } else {
            return await this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        }
    }

    // Stream

    public override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }

    // Helpers

    private bool IsEnabledLengthNewLine => (0 <= this._LengthNewLine);

    // TrimStart at _BufferStart
    private bool SkipWhitespace() {
        if (0 < this._BufferLength) {
            var diff = this._BufferLength - this._Buffer.AsSpan(this._BufferStart, this._BufferLength).TrimStart(ArrWhiteSpace).Length;
            if (0 < diff) {
                this._LengthNewLine = -1;
                this.AdvanceBuffer(diff);
                return true;
            }
        }
        return false;
    }

    // move the buffer start to the right
    private void AdvanceBuffer(int diff) {
        if (this._BufferLength == diff) {
            this._BufferLength = 0;
            this._BufferStart = 0;
            if (this.IsEnabledLengthNewLine) {
                this._LengthNewLine -= diff;
            }
        } else if (this._BufferLength < diff) {
            throw new ArgumentOutOfRangeException();
        } else {
            this._BufferLength -= diff;
            this._BufferStart += diff;
            if (this.IsEnabledLengthNewLine) {
                this._LengthNewLine -= diff;
            }
        }
    }

    private void InnerRead(int count) {
        if (this._InnerStream is not { } stream) { return; }

        if (this.IsEnabledLengthNewLine) {
            throw new Exception("0 <= this._BufferLengthEOF");
        }
        if (this._EndOfStream) { return; }

        // target position within the buffer
        int bufferEnd = this._BufferStart + this._BufferLength;

        // if the tail space is low eat the buffer
        if (0 < this._BufferLength && this._ChunkSize * 3 < bufferEnd) {
            return;
        }

        if (this._BufferSize < bufferEnd + count) {
            count = this._BufferSize - bufferEnd;
        }

        int read = stream.Read(this._Buffer, bufferEnd, count);
        this.PostRead(read);

    }

    private async ValueTask InnerReadAsync(int count, CancellationToken cancellationToken) {
        if (this._InnerStream is not { } stream) { return; }

        if (this.IsEnabledLengthNewLine) {
            throw new Exception("0 <= this._BufferLengthEOF");
        }
        if (this._EndOfStream) { return; }

        // target position within the buffer
        int bufferEnd = this._BufferStart + this._BufferLength;

        // if the tail space is low eat the buffer
        if (0 < this._BufferLength && this._ChunkSize * 3 < bufferEnd) {
            return;
        }

        if (this._BufferSize < bufferEnd + count) {
            count = this._BufferSize - bufferEnd;
        }

        int read = await stream.ReadAsync(this._Buffer.AsMemory().Slice(bufferEnd, count), cancellationToken);
        this.PostRead(read);
    }

    private void PostRead(int read) {
        if (read == 0) {
            this._EndOfStream = true;
        } else {
            this._BufferLength += read;
            this.FindNextBufferEOFPosition();
        }
    }

    private bool FindNextBufferEOFPosition() {
        int diff = this._Buffer.AsSpan(this._BufferStart, this._BufferLength).IndexOfAny(ArrNewLines);
        if (0 <= diff) {
            this._LengthNewLine = diff;
            return true;
        } else {
            return false;
        }
    }

    private int Copy(Span<byte> buffer) {
        int result = buffer.Length;
        if (this.IsEnabledLengthNewLine) {
            if (this._LengthNewLine < result) {
                result = this._LengthNewLine;
            }
        }

        if (this._BufferLength < result) {
            result = this._BufferLength;
        }

        if (result == 0) {
            return 0;
        }
        this._Buffer.AsSpan(this._BufferStart, result).CopyTo(buffer);

        this.AdvanceBuffer(result);

        if (this._EndOfSplit && (0 == this._BufferLength || 0 == this._LengthNewLine)) {
            this._ReadyToRead = false;
        }

        return result;
    }
}