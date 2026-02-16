// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Rdn.Serialization
{
    [StructLayout(LayoutKind.Auto)]
    internal struct PipeReadBufferState : IReadBufferState<PipeReadBufferState, PipeReader>
    {
        private readonly PipeReader _utf8Rdn;

        private ReadOnlySequence<byte> _sequence = ReadOnlySequence<byte>.Empty;
        private bool _isFinalBlock;
        private bool _isFirstBlock = true;
        private int _unsuccessfulReadBytes;

        public PipeReadBufferState(PipeReader utf8Rdn)
        {
            _utf8Rdn = utf8Rdn;
        }

        public readonly bool IsFinalBlock => _isFinalBlock;

#if DEBUG
        public readonly ReadOnlySequence<byte> Bytes => _sequence;
#endif

        public void Advance(long bytesConsumed)
        {
            _unsuccessfulReadBytes = 0;
            if (bytesConsumed == 0)
            {
                long leftOver = _sequence.Length;
                // Cap at int.MaxValue as PipeReader.ReadAtLeastAsync uses an int as the minimum size argument.
                _unsuccessfulReadBytes = (int)Math.Min(int.MaxValue, leftOver * 2);
            }

            _utf8Rdn.AdvanceTo(_sequence.Slice(bytesConsumed).Start, _sequence.End);
            _sequence = ReadOnlySequence<byte>.Empty;
        }

        /// <summary>
        /// Read from the PipeReader until either our buffer limit is filled or we hit EOF.
        /// Calling ReadCore is relatively expensive, so we minimize the number of times
        /// we need to call it.
        /// </summary>
        public async ValueTask<PipeReadBufferState> ReadAsync(PipeReader utf8Rdn, CancellationToken cancellationToken, bool fillBuffer = true)
        {
            Debug.Assert(_sequence.Equals(ReadOnlySequence<byte>.Empty), "ReadAsync should only be called when the buffer is empty.");

            // Since mutable structs don't work well with async state machines,
            // make all updates on a copy which is returned once complete.
            PipeReadBufferState bufferState = this;

            int minBufferSize = _unsuccessfulReadBytes > 0 ? _unsuccessfulReadBytes : 0;
            ReadResult readResult = await _utf8Rdn.ReadAtLeastAsync(minBufferSize, cancellationToken).ConfigureAwait(false);

            bufferState._sequence = readResult.Buffer;
            bufferState._isFinalBlock = readResult.IsCompleted;
            bufferState.ProcessReadBytes();

            if (readResult.IsCanceled)
            {
                ThrowHelper.ThrowOperationCanceledException_PipeReadCanceled();
            }

            return bufferState;
        }

        public void Read(PipeReader utf8Rdn) => throw new NotImplementedException();

        public void GetReader(RdnReaderState rdnReaderState, out Utf8RdnReader reader)
        {
            if (_sequence.IsSingleSegment)
            {
                reader = new Utf8RdnReader(
#if NET
                    _sequence.FirstSpan,
#else
                    _sequence.First.Span,
#endif
                    IsFinalBlock, rdnReaderState);
            }
            else
            {
                reader = new Utf8RdnReader(_sequence, IsFinalBlock, rdnReaderState);
            }
        }

        private void ProcessReadBytes()
        {
            if (_isFirstBlock)
            {
                _isFirstBlock = false;

                // Handle the UTF-8 BOM if present
                if (_sequence.Length > 0)
                {
                    if (_sequence.First.Length >= RdnConstants.Utf8Bom.Length)
                    {
                        if (_sequence.First.Span.StartsWith(RdnConstants.Utf8Bom))
                        {
                            _sequence = _sequence.Slice((byte)RdnConstants.Utf8Bom.Length);
                        }
                    }
                    else
                    {
                        // BOM spans multiple segments
                        SequencePosition pos = _sequence.Start;
                        int matched = 0;
                        while (matched < RdnConstants.Utf8Bom.Length && _sequence.TryGet(ref pos, out ReadOnlyMemory<byte> mem, advance: true))
                        {
                            ReadOnlySpan<byte> span = mem.Span;
                            for (int i = 0; i < span.Length && matched < RdnConstants.Utf8Bom.Length; i++, matched++)
                            {
                                if (span[i] != RdnConstants.Utf8Bom[matched])
                                {
                                    matched = 0;
                                    break;
                                }
                            }
                        }

                        if (matched == RdnConstants.Utf8Bom.Length)
                        {
                            _sequence = _sequence.Slice(RdnConstants.Utf8Bom.Length);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_sequence.Equals(ReadOnlySequence<byte>.Empty))
            {
                return;
            }

            // If we have a sequence, that likely means an Exception was thrown during deserialization.
            // We should make sure to call AdvanceTo so that future reads on the PipeReader can be done without throwing.
            // We'll advance to the start of the sequence as we don't know how many bytes were consumed.
            _utf8Rdn.AdvanceTo(_sequence.Start);
            _sequence = ReadOnlySequence<byte>.Empty;
        }
    }
}
