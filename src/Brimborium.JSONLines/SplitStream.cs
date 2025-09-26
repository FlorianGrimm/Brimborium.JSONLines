using System.Reflection.PortableExecutable;

namespace Brimborium.JSONLines;

public sealed class SplitStream : IDisposable {
    private readonly int _ChunkSize;
    private readonly int _BufferSize;

    private Stream? _Stream;
    private bool _DisposeStream;
    private readonly State _State;
    private InnerStream? _ActiveInnerStream;

    public SplitStream(Stream stream, bool disposeStream, int chunkSize = 0, int bufferSize = 0) {
        if (chunkSize <= 0) { this._ChunkSize = 1024*16; } else { this._ChunkSize = chunkSize; }
        if (bufferSize <= this._ChunkSize * 4) { this._BufferSize = this._ChunkSize * 4; } else { this._BufferSize = bufferSize; }
        this._Stream = stream;
        this._DisposeStream = disposeStream;
        State state = new(this._ChunkSize, this._BufferSize);
        this._State = state;
    }

    public void Dispose() {
        if (this._DisposeStream) {
            using (var stream = this._Stream) {
                this._Stream = null;
            }
        } else {
            this._Stream = null;
            this._DisposeStream = false;
        }
    }

    public Stream? GetStream() {
        if (this._Stream is { } stream) {
            if (this._ActiveInnerStream is { }) {
                throw new InvalidOperationException("The previous stream is not closed yet.");
            }
            if (this.Prefetch(stream)) {
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

    internal bool Prefetch(Stream stream) {
        if (0 <= this._State.BufferEOFPosition) {
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


    internal sealed class State {
        public readonly int ChunkSize;
        public readonly int BufferSize;
        public readonly byte[] Buffer;
        public int BufferStart;
        public int BufferLength;
        public bool EndOfStream;
        public int BufferEOFPosition;

        public State(int chunkSize, int bufferSize) {
            this.ChunkSize = chunkSize;
            this.BufferSize = bufferSize;
            this.Buffer = new byte[bufferSize];
            this.BufferStart = 0;
            this.BufferLength = 0;
            this.BufferEOFPosition = -1;
        }

        public bool IsEnabledBufferEOFPosition => (0 <= this.BufferEOFPosition);

        private static byte[] _WhiteSpace = new byte[] { 9, 10, 13, 32 };

        public bool SkipWhitespace() {
            if (0 < this.BufferLength) {
                var diff = this.BufferLength - this.Buffer.AsSpan(this.BufferStart, this.BufferLength).TrimStart(_WhiteSpace).Length;
                if (0 < diff) {
                    this.BufferEOFPosition = -1;
                    this.AdvanceBuffer(diff);
                    return true;
                }
            }
            return false;
        }

        internal void AdvanceBuffer(int diff) {
            if (this.BufferLength == diff) {
                this.BufferLength = 0;
                this.BufferStart = 0;
                if (this.IsEnabledBufferEOFPosition) {
                    this.BufferEOFPosition -= diff;
                }
            } else if (this.BufferLength < diff) {
                throw new ArgumentOutOfRangeException();
            } else {
                this.BufferLength -= diff;
                this.BufferStart += diff;
                if (this.IsEnabledBufferEOFPosition) {
                    this.BufferEOFPosition -= diff;
                }
            }
        }

        public void Read(Stream stream, int count) {
            if (this.IsEnabledBufferEOFPosition) {
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
            if (read == 0) {
                this.EndOfStream = true;
                return;
            }
            {
                this.BufferLength += read;
                this.FindNextBufferEOFPosition();
            }
        }

        public async ValueTask ReadAsync(Stream stream, int count) {
            if (this.IsEnabledBufferEOFPosition) {
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

            int read = await stream.ReadAsync(this.Buffer, bufferEnd, count);
            if (read == 0) {
                this.EndOfStream = true;
                return;
            }
            {
                this.BufferLength += read;
                this.FindNextBufferEOFPosition();
            }
        }

        public bool FindNextBufferEOFPosition() {
            int diff = this.Buffer.AsSpan(this.BufferStart, this.BufferLength).IndexOfAny(_NewLines);
            if (0 <= diff) {
                this.BufferEOFPosition = diff;
                return true;
            } else {
                return false;
            }
        }

        internal static byte[] _NewLines = new byte[] { 10, 13 };

        public int Copy(Span<byte> buffer) {
            int result = buffer.Length;
            if (this.IsEnabledBufferEOFPosition) {
                if (this.BufferEOFPosition < result) {
                    result = this.BufferEOFPosition;
                }
            }

            if (this.BufferLength < result) {
                result = this.BufferLength;
            }

            if (result == 0) {
                return 0;
            }
            this.Buffer.AsSpan(this.BufferStart, result).CopyTo(buffer);
            //Array.Copy(this.Buffer, this.BufferStart, buffer, offset, result);

            this.AdvanceBuffer(result);

            return result;
        }
    }

    internal class InnerStream : Stream {
        private readonly SplitStream _SplitStream;
        private readonly State _State;
        private readonly Stream _Stream;
        private long _Position;
        private bool _EndOfSplit;

        public InnerStream(SplitStream splitStream, State state, Stream stream) {
            this._SplitStream = splitStream;
            this._State = state;
            this._Stream = stream;
            this._EndOfSplit = this._State.IsEnabledBufferEOFPosition;
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
            get => this._Position;
            set { throw new NotSupportedException(); }
        }

        public override void Flush() { throw new NotSupportedException(); }

        public override int Read(Span<byte> buffer) {
            if ((0 == this._State.BufferLength) && (this._EndOfSplit)) { return 0; }

            if (0 == this._State.BufferEOFPosition) {
                if (this._State.SkipWhitespace()) {
                    this._State.BufferEOFPosition = -1;
                }
                return 0;
            } else if (0 < this._State.BufferEOFPosition) {
                return this._State.Copy(buffer);
            }

            if (this._State.EndOfStream) {
                if (this._State.BufferLength == 0) {
                    return 0;
                } else {
                    return this._State.Copy(buffer);
                }
            }

            if (this._State.ChunkSize < buffer.Length) { buffer=buffer.Slice(0, this._State.ChunkSize); }

            {
                if (buffer.Length < this._State.BufferLength) {
                } else if (this._State.BufferLength < this._State.ChunkSize) {
                    if (0 < this._State.BufferLength
                        && this._State.ChunkSize * 3 < this._State.BufferStart + this._State.BufferLength) {
                        buffer = buffer.Slice(0, this._State.BufferLength);
                    } else {
                        this._State.Read(this._Stream, buffer.Length);
                        if (0 <= this._State.BufferEOFPosition) {
                            if (this._State.BufferEOFPosition < buffer.Length) {
                                buffer = buffer.Slice(0, this._State.BufferEOFPosition);
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
            return this.Read(buffer.AsSpan(offset, count));
        }

        public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {

            if ((0 == this._State.BufferLength) && (this._EndOfSplit)) { return 0; }

            if (0 == this._State.BufferEOFPosition) {
                if (this._State.SkipWhitespace()) {
                    this._State.BufferEOFPosition = -1;
                }
                return 0;
            } else if (0 < this._State.BufferEOFPosition) {
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
                        this._State.Read(this._Stream, buffer.Length);
                        if (0 <= this._State.BufferEOFPosition) {
                            if (this._State.BufferEOFPosition < buffer.Length) {
                                buffer = buffer.Slice(0, this._State.BufferEOFPosition);
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
            return await this.ReadAsync( buffer.AsMemory(offset, count), cancellationToken);
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