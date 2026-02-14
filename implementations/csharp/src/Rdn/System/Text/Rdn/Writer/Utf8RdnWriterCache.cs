// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;

namespace Rdn
{
    /// <summary>
    /// Defines a thread-local cache for RdnSerializer to store reusable Utf8RdnWriter/IBufferWriter instances.
    /// </summary>
    internal static class Utf8RdnWriterCache
    {
        [ThreadStatic]
        private static ThreadLocalState? t_threadLocalState;

        public static Utf8RdnWriter RentWriterAndBuffer(RdnSerializerOptions options, out PooledByteBufferWriter bufferWriter) =>
            RentWriterAndBuffer(options.GetWriterOptions(), options.DefaultBufferSize, out bufferWriter);

        public static Utf8RdnWriter RentWriterAndBuffer(RdnWriterOptions options, int defaultBufferSize, out PooledByteBufferWriter bufferWriter)
        {
            ThreadLocalState state = t_threadLocalState ??= new();
            Utf8RdnWriter writer;

            if (state.RentedWriters++ == 0)
            {
                // First RdnSerializer call in the stack -- initialize & return the cached instances.
                bufferWriter = state.BufferWriter;
                writer = state.Writer;

                bufferWriter.InitializeEmptyInstance(defaultBufferSize);
                writer.Reset(bufferWriter, options);
            }
            else
            {
                // We're in a recursive RdnSerializer call -- return fresh instances.
                bufferWriter = new PooledByteBufferWriter(defaultBufferSize);
                writer = new Utf8RdnWriter(bufferWriter, options);
            }

            return writer;
        }

        public static Utf8RdnWriter RentWriter(RdnSerializerOptions options, IBufferWriter<byte> bufferWriter)
        {
            ThreadLocalState state = t_threadLocalState ??= new();
            Utf8RdnWriter writer;

            if (state.RentedWriters++ == 0)
            {
                // First RdnSerializer call in the stack -- initialize & return the cached instance.
                writer = state.Writer;
                writer.Reset(bufferWriter, options.GetWriterOptions());
            }
            else
            {
                // We're in a recursive RdnSerializer call -- return a fresh instance.
                writer = new Utf8RdnWriter(bufferWriter, options.GetWriterOptions());
            }

            return writer;
        }

        public static void ReturnWriterAndBuffer(Utf8RdnWriter writer, PooledByteBufferWriter bufferWriter)
        {
            Debug.Assert(t_threadLocalState != null);
            ThreadLocalState state = t_threadLocalState;

            writer.ResetAllStateForCacheReuse();
            bufferWriter.ClearAndReturnBuffers();

            int rentedWriters = --state.RentedWriters;
            Debug.Assert((rentedWriters == 0) == (ReferenceEquals(state.BufferWriter, bufferWriter) && ReferenceEquals(state.Writer, writer)));
        }

        public static void ReturnWriter(Utf8RdnWriter writer)
        {
            Debug.Assert(t_threadLocalState != null);
            ThreadLocalState state = t_threadLocalState;

            writer.ResetAllStateForCacheReuse();

            int rentedWriters = --state.RentedWriters;
            Debug.Assert((rentedWriters == 0) == ReferenceEquals(state.Writer, writer));
        }

        private sealed class ThreadLocalState
        {
            public readonly PooledByteBufferWriter BufferWriter;
            public readonly Utf8RdnWriter Writer;
            public int RentedWriters;

            public ThreadLocalState()
            {
                BufferWriter = PooledByteBufferWriter.CreateEmptyInstanceForCaching();
                Writer = Utf8RdnWriter.CreateEmptyInstanceForCaching();
            }
        }
    }
}
