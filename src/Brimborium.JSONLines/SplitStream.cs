#pragma warning disable IDE0057 // Use range operator

namespace Brimborium.JSONLines;

/// <summary>
/// Split a stream into multiple streams by newline.
/// </summary>
public sealed class SplitStream : IDisposable {
    // the size of bytes to read from the stream at once
    private readonly int _ChunkSize;

    // the size of the internal buffer - it's 4 times the chunk size
    private readonly int _BufferSize;

    private Stream? _Stream;
    private bool _LeaveOpen;
    private readonly State _State;
    private InnerStream? _ActiveInnerStream;

    /// <summary>
    /// create a new instance.
    /// </summary>
    /// <param name="stream">The stream to read and split into multiple streams.</param>
    /// <param name="leaveOpen">leave the stream open after disposing this instance.</param>
    /// <param name="chunkSize">The size of bytes to read from the stream at once.</param>
    public SplitStream(Stream stream, bool leaveOpen = true, int chunkSize = 0) {
        if (chunkSize <= 0) { this._ChunkSize = 1024 * 16; } else { this._ChunkSize = chunkSize; }
        this._BufferSize = this._ChunkSize * 4;

        this._Stream = stream;
        this._LeaveOpen = leaveOpen;
        State state = new(this._ChunkSize, this._BufferSize);
        this._State = state;
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (this._LeaveOpen) {
            this._Stream = null;
        } else {
            using (var stream = this._Stream) {
                this._Stream = null;
            }
        }
    }

    /// <summary>
    /// Get the next stream or null if there is no more data.
    /// </summary>
    /// <returns>the next stream or null if there is no more data.</returns>
    /// <exception cref="InvalidOperationException">if the previous stream is not closed yet.</exception>
    public Stream? GetStream() {
        if (this._Stream is { } stream) {
            if (this._ActiveInnerStream is { }) {
                throw new InvalidOperationException("The previous stream is not closed yet.");
            }
            if (this.Prefetch()) {
                var inner = new InnerStream(this, this._State, stream);
                this._ActiveInnerStream = inner;
                return inner;
            } else {
                return null;
            }
        } else {
            return null;
        }
    }

    /// <summary>
    /// Get the next stream or null if there is no more data.
    /// </summary>
    /// <returns>the next stream or null if there is no more data.</returns>
    /// <exception cref="InvalidOperationException">if the previous stream is not closed yet.</exception>
    public async ValueTask<Stream?> GetStreamAsync(CancellationToken cancellationToken) {
        if (this._Stream is { } stream) {
            if (this._ActiveInnerStream is { }) {
                throw new InvalidOperationException("The previous stream is not closed yet.");
            }
            if (await this.PrefetchAsync(cancellationToken)) {
                var inner = new InnerStream(this, this._State, stream);
                this._ActiveInnerStream = inner;
                return inner;
            } else {
                return null;
            }
        } else {
            return null;
        }
    }

    /// <summary>
    /// Load the first line into the buffer. 
    /// or use the last loaded conent also respecting the newline
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    internal bool Prefetch() {
        if (this._Stream is not { } stream) { return false; }

        if (0 <= this._State.LengthNewLine) {
            this._State.SkipWhitespace();
        }

        if (this._State.BufferLength == 0) {
            this._State.Read(stream, this._State.ChunkSize);
            if (this._State.EndOfStream) { return false; }
        }

        while (0 <= this._State.BufferLength) {
            this._State.SkipWhitespace();

            if (0 < this._State.BufferLength) {
                this._State.FindNextBufferEOFPosition();
                return true;
            } else {
                this._State.Read(stream, this._State.ChunkSize);
                if (this._State.EndOfStream) { return false; }
            }
        }

        {
            this._State.Read(stream, this._State.ChunkSize);
            return (0 < this._State.BufferLength);
        }
    }

    internal async ValueTask<bool> PrefetchAsync(CancellationToken cancellationToken) {
        if (this._Stream is not { } stream) { return false; }

        if (0 <= this._State.LengthNewLine) {
            this._State.SkipWhitespace();
        }

        if (this._State.BufferLength == 0) {
            this._State.Read(stream, this._State.ChunkSize);
            if (this._State.EndOfStream) { return false; }
        }

        while (0 <= this._State.BufferLength) {
            this._State.SkipWhitespace();

            if (0 < this._State.BufferLength) {
                this._State.FindNextBufferEOFPosition();
                return true;
            } else {
                await this._State.ReadAsync(stream, this._State.ChunkSize, cancellationToken);
                if (this._State.EndOfStream) { return false; }
            }
        }

        {
            await this._State.ReadAsync(stream, this._State.ChunkSize, cancellationToken);
            return (0 < this._State.BufferLength);
        }
    }


    internal sealed class State {
        public readonly int ChunkSize;
        public readonly int BufferSize;
        public readonly byte[] Buffer;
        public int BufferStart;
        public int BufferLength;
        public bool EndOfStream;
        public int LengthNewLine;

        public State(int chunkSize, int bufferSize) {
            this.ChunkSize = chunkSize;
            this.BufferSize = bufferSize;
            this.Buffer = new byte[bufferSize];
            this.BufferStart = 0;
            this.BufferLength = 0;
            this.LengthNewLine = -1;
        }

        public bool IsEnabledLengthNewLine => (0 <= this.LengthNewLine);

        private static byte[] ArrWhiteSpace = new byte[] { 9, 10, 13, 32 };

        public bool SkipWhitespace() {
            if (0 < this.BufferLength) {
                var diff = this.BufferLength - this.Buffer.AsSpan(this.BufferStart, this.BufferLength).TrimStart(ArrWhiteSpace).Length;
                if (0 < diff) {
                    this.LengthNewLine = -1;
                    this.AdvanceBuffer(diff);
                    return true;
                }
            }
            return false;
        }

        public void AdvanceBuffer(int diff) {
            if (this.BufferLength == diff) {
                this.BufferLength = 0;
                this.BufferStart = 0;
                if (this.IsEnabledLengthNewLine) {
                    this.LengthNewLine -= diff;
                }
            } else if (this.BufferLength < diff) {
                throw new ArgumentOutOfRangeException();
            } else {
                this.BufferLength -= diff;
                this.BufferStart += diff;
                if (this.IsEnabledLengthNewLine) {
                    this.LengthNewLine -= diff;
                }
            }
        }

        public void Read(Stream stream, int count) {
            if (this.IsEnabledLengthNewLine) {
                throw new Exception("0 <= this.BufferLengthEOF");
            }
            if (this.EndOfStream) { return; }

            // target position within the buffer
            int bufferEnd = this.BufferStart + this.BufferLength;

            // if the tail space is low copy it to the front
            // rare / panic
            if (this.ChunkSize * 3 < bufferEnd) {
                Array.Copy(this.Buffer, bufferEnd, this.Buffer, 0, this.BufferLength);
                this.BufferStart = 0;
                bufferEnd = this.BufferLength;
            }

            if (this.BufferSize < bufferEnd + count) {
                count = this.BufferSize - bufferEnd;
            }

            int read = stream.Read(this.Buffer, bufferEnd, count);
            this.PostRead(read);

        }

        public async ValueTask ReadAsync(Stream stream, int count, CancellationToken cancellationToken) {
            if (this.IsEnabledLengthNewLine) {
                throw new Exception("0 <= this.BufferLengthEOF");
            }
            if (this.EndOfStream) { return; }

            // target position within the buffer
            int bufferEnd = this.BufferStart + this.BufferLength;

            // if the tail space is low copy it to the front
            // rare / panic
            if (this.ChunkSize * 3 < bufferEnd) {
                Array.Copy(this.Buffer, bufferEnd, this.Buffer, 0, this.BufferLength);
                this.BufferStart = 0;
                bufferEnd = this.BufferLength;
            }

            if (this.BufferSize < bufferEnd + count) {
                count = this.BufferSize - bufferEnd;
            }

            int read = await stream.ReadAsync(this.Buffer.AsMemory().Slice(bufferEnd, count), cancellationToken);
            this.PostRead(read);
        }

        private void PostRead(int read) {
            if (read == 0) {
                this.EndOfStream = true;
            } else {
                this.BufferLength += read;
                this.FindNextBufferEOFPosition();
            }
        }

        public bool FindNextBufferEOFPosition() {
            int diff = this.Buffer.AsSpan(this.BufferStart, this.BufferLength).IndexOfAny(ArrNewLines);
            if (0 <= diff) {
                this.LengthNewLine = diff;
                return true;
            } else {
                return false;
            }
        }

        public static byte[] ArrNewLines = new byte[] { 10, 13 };

        public int Copy(Span<byte> buffer) {
            int result = buffer.Length;
            if (this.IsEnabledLengthNewLine) {
                if (this.LengthNewLine < result) {
                    result = this.LengthNewLine;
                }
            }

            if (this.BufferLength < result) {
                result = this.BufferLength;
            }

            if (result == 0) {
                return 0;
            }
            this.Buffer.AsSpan(this.BufferStart, result).CopyTo(buffer);

            this.AdvanceBuffer(result);

            return result;
        }
    }

    internal class InnerStream : Stream {
        private readonly SplitStream _SplitStream;
        private readonly State _State;
        private readonly Stream _Stream;
        private bool _EndOfSplit;

        public InnerStream(SplitStream splitStream, State state, Stream stream) {
            this._SplitStream = splitStream;
            this._State = state;
            this._Stream = stream;
            this._EndOfSplit = this._State.IsEnabledLengthNewLine;
        }

        public override void Close() {
            this._SplitStream._ActiveInnerStream = null;
            base.Close();
        }

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
            if ((0 == this._State.BufferLength) && (this._EndOfSplit)) { return 0; }

            if (0 == this._State.LengthNewLine) {
                if (this._State.SkipWhitespace()) {
                    this._State.LengthNewLine = -1;
                }
                return 0;
            } else if (0 < this._State.LengthNewLine) {
                return this._State.Copy(buffer);
            }

            if (this._State.EndOfStream) {
                if (this._State.BufferLength == 0) {
                    return 0;
                } else {
                    return this._State.Copy(buffer);
                }
            }

            if (this._State.ChunkSize < buffer.Length) { buffer = buffer.Slice(0, this._State.ChunkSize); }

            {
                if (buffer.Length < this._State.BufferLength) {
                } else if (this._State.BufferLength < this._State.ChunkSize) {
                    if (0 < this._State.BufferLength
                        && this._State.ChunkSize * 3 < this._State.BufferStart + this._State.BufferLength) {
                        buffer = buffer.Slice(0, this._State.BufferLength);
                    } else {
                        this._State.Read(this._Stream, buffer.Length);
                        if (0 <= this._State.LengthNewLine) {
                            if (this._State.LengthNewLine < buffer.Length) {
                                buffer = buffer.Slice(0, this._State.LengthNewLine);
                            }
                            if (!this._EndOfSplit) {
                                this._EndOfSplit = true;
                            }
                        }
                        if (!this._EndOfSplit && this._State.EndOfStream) {
                            this._EndOfSplit = true;
                        }
                    }
                }
            }

            return this._State.Copy(buffer);
        }

        public override int Read(byte[] buffer, int offset, int count) {
            if (buffer.Length < (offset + count)) {
                return this.Read(buffer.AsSpan(offset));
            } else {
                return this.Read(buffer.AsSpan(offset, count));
            }
        }

        public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {

            if ((0 == this._State.BufferLength) && (this._EndOfSplit)) { return 0; }

            if (0 == this._State.LengthNewLine) {
                if (this._State.SkipWhitespace()) {
                    this._State.LengthNewLine = -1;
                }
                return 0;
            } else if (0 < this._State.LengthNewLine) {
                return this._State.Copy(buffer.Span);
            }

            if (this._State.EndOfStream) {
                if (this._State.BufferLength == 0) {
                    return 0;
                } else {
                    return this._State.Copy(buffer.Span);
                }
            }

            if (this._State.ChunkSize < buffer.Length) { buffer = buffer.Slice(0, this._State.ChunkSize); }

            {
                if (buffer.Length < this._State.BufferLength) {
                } else if (this._State.BufferLength < this._State.ChunkSize) {
                    if (0 < this._State.BufferLength
                        && this._State.ChunkSize * 3 < this._State.BufferStart + this._State.BufferLength) {
                        buffer = buffer.Slice(0, this._State.BufferLength);
                    } else {
                        await this._State.ReadAsync(this._Stream, buffer.Length, cancellationToken);
                        if (0 <= this._State.LengthNewLine) {
                            if (this._State.LengthNewLine < buffer.Length) {
                                buffer = buffer.Slice(0, this._State.LengthNewLine);
                            }
                            if (!this._EndOfSplit) {
                                this._EndOfSplit = true;
                            }
                        }
                        if (!this._EndOfSplit && this._State.EndOfStream) {
                            this._EndOfSplit = true;
                        }
                    }
                }
            }

            return this._State.Copy(buffer.Span);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            if (buffer.Length < (offset + count)) {
                return await this.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            } else {
                return await this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            }
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}