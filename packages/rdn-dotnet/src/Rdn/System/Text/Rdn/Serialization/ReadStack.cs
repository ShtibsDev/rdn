// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn
{
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal struct ReadStack
    {
        /// <summary>
        /// Exposes the stack frame that is currently active.
        /// </summary>
        public ReadStackFrame Current;

        /// <summary>
        /// Gets the parent stack frame, if it exists.
        /// </summary>
        public readonly ref ReadStackFrame Parent
        {
            get
            {
                Debug.Assert(_count > 1);
                Debug.Assert(_stack is not null);
                return ref _stack[_count - 2];
            }
        }

        public readonly RdnPropertyInfo? ParentProperty
            => Current.HasParentObject ? Parent.RdnPropertyInfo : null;

        /// <summary>
        /// Buffer containing all frames in the stack. For performance it is only populated for serialization depths > 1.
        /// </summary>
        private ReadStackFrame[] _stack;

        /// <summary>
        /// Tracks the current depth of the stack.
        /// </summary>
        private int _count;

        /// <summary>
        /// If not zero, indicates that the stack is part of a re-entrant continuation of given depth.
        /// </summary>
        private int _continuationCount;

        /// <summary>
        /// Indicates that the state still contains suspended frames waiting re-entry.
        /// </summary>
        public readonly bool IsContinuation => _continuationCount != 0;

        /// <summary>
        /// Whether we need to read ahead in the inner read loop.
        /// </summary>
        public bool SupportContinuation;

        /// <summary>
        /// Ensures that the stack buffer has sufficient capacity to hold an additional frame.
        /// </summary>
        private void EnsurePushCapacity()
        {
            if (_stack is null)
            {
                _stack = new ReadStackFrame[4];
            }
            else if (_count - 1 == _stack.Length)
            {
                Array.Resize(ref _stack, 2 * _stack.Length);
            }
        }

        internal void Initialize(RdnTypeInfo rdnTypeInfo, bool supportContinuation = false)
        {
            Current.RdnTypeInfo = rdnTypeInfo;
            Current.RdnPropertyInfo = rdnTypeInfo.PropertyInfoForTypeInfo;
            Current.NumberHandling = Current.RdnPropertyInfo.EffectiveNumberHandling;
            Current.CanContainMetadata = false;
            SupportContinuation = supportContinuation;
        }

        public void Push()
        {
            if (_continuationCount == 0)
            {
                if (_count == 0)
                {
                    // Performance optimization: reuse the first stack frame on the first push operation.
                    // NB need to be careful when making writes to Current _before_ the first `Push`
                    // operation is performed.
                    _count = 1;
                }
                else
                {
                    RdnTypeInfo rdnTypeInfo = Current.RdnPropertyInfo?.RdnTypeInfo ?? Current.CtorArgumentState!.RdnParameterInfo!.RdnTypeInfo;
                    RdnNumberHandling? numberHandling = Current.NumberHandling;

                    EnsurePushCapacity();
                    _stack[_count - 1] = Current;
                    Current = default;
                    _count++;

                    Current.RdnTypeInfo = rdnTypeInfo;
                    Current.RdnPropertyInfo = rdnTypeInfo.PropertyInfoForTypeInfo;
                    // Allow number handling on property to win over handling on type.
                    Current.NumberHandling = numberHandling ?? Current.RdnPropertyInfo.EffectiveNumberHandling;
                    Current.CanContainMetadata = false;
                }
            }
            else
            {
                // We are re-entering a continuation, adjust indices accordingly.

                if (_count++ > 0)
                {
                    _stack[_count - 2] = Current;
                    Current = _stack[_count - 1];
                }

                // check if we are done
                if (_continuationCount == _count)
                {
                    _continuationCount = 0;
                }
            }

            SetConstructorArgumentState();
#if DEBUG
            // Ensure the method is always exercised in debug builds.
            _ = RdnPath();
#endif
        }

        public void Pop(bool success)
        {
            Debug.Assert(_count > 0);
            Debug.Assert(RdnPath() is not null);

            if (!success)
            {
                // Check if we need to initialize the continuation.
                if (_continuationCount == 0)
                {
                    if (_count == 1)
                    {
                        // No need to copy any frames here.
                        _continuationCount = 1;
                        _count = 0;
                        return;
                    }

                    // Need to push the Current frame to the stack,
                    // ensure that we have sufficient capacity.
                    EnsurePushCapacity();
                    _continuationCount = _count--;
                }
                else if (--_count == 0)
                {
                    // reached the root, no need to copy frames.
                    return;
                }

                _stack[_count] = Current;
                Current = _stack[_count - 1];
            }
            else
            {
                Debug.Assert(_continuationCount == 0);

                if (--_count > 0)
                {
                    Current = _stack[_count - 1];
                }
            }
        }

        // Return a RDNPath using simple dot-notation when possible. When special characters are present, bracket-notation is used:
        // $.x.y[0].z
        // $['PropertyName.With.Special.Chars']
        public string RdnPath()
        {
            StringBuilder sb = new StringBuilder("$");

            (int frameCount, bool includeCurrentFrame) = _continuationCount switch
            {
                0 => (_count - 1, true), // Not a continuation, report previous frames and Current.
                1 => (0, true), // Continuation of depth 1, just report Current frame.
                int c => (c, false) // Continuation of depth > 1, report the entire stack.
            };

            for (int i = 0; i < frameCount; i++)
            {
                AppendStackFrame(sb, ref _stack[i]);
            }

            if (includeCurrentFrame)
            {
                AppendStackFrame(sb, ref Current);
            }

            return sb.ToString();

            static void AppendStackFrame(StringBuilder sb, ref ReadStackFrame frame)
            {
                // Append the property name.
                string? propertyName = GetPropertyName(ref frame);
                AppendPropertyName(sb, propertyName);

                if (frame.RdnTypeInfo != null && frame.IsProcessingEnumerable())
                {
                    if (frame.ReturnValue is not IEnumerable enumerable)
                    {
                        return;
                    }

                    // For continuation scenarios only, before or after all elements are read, the exception is not within the array.
                    if (frame.ObjectState == StackFrameObjectState.None ||
                        frame.ObjectState == StackFrameObjectState.CreatedObject ||
                        frame.ObjectState == StackFrameObjectState.ReadElements)
                    {
                        sb.Append('[');
                        sb.Append(GetCount(enumerable));
                        sb.Append(']');
                    }
                }
            }

            static int GetCount(IEnumerable enumerable)
            {
                if (enumerable is ICollection collection)
                {
                    return collection.Count;
                }

                int count = 0;
                IEnumerator enumerator = enumerable.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    count++;
                }

                return count;
            }

            static void AppendPropertyName(StringBuilder sb, string? propertyName)
            {
                if (propertyName != null)
                {
                    if (propertyName.AsSpan().ContainsSpecialCharacters())
                    {
                        sb.Append(@"['");
                        sb.AppendEscapedPropertyName(propertyName);
                        sb.Append(@"']");
                    }
                    else
                    {
                        sb.Append('.');
                        sb.Append(propertyName);
                    }
                }
            }

            static string? GetPropertyName(ref ReadStackFrame frame)
            {
                string? propertyName = null;

                // Attempt to get the RDN property name from the frame.
                byte[]? utf8PropertyName = frame.RdnPropertyName;
                if (utf8PropertyName == null)
                {
                    if (frame.RdnPropertyNameAsString != null)
                    {
                        // Attempt to get the RDN property name set manually for dictionary
                        // keys and KeyValuePair property names.
                        propertyName = frame.RdnPropertyNameAsString;
                    }
                    else
                    {
                        // Attempt to get the RDN property name from the RdnPropertyInfo or RdnParameterInfo.
                        utf8PropertyName = frame.RdnPropertyInfo?.NameAsUtf8Bytes ??
                            frame.CtorArgumentState?.RdnParameterInfo?.RdnNameAsUtf8Bytes;
                    }
                }

                if (utf8PropertyName != null)
                {
                    propertyName = Encoding.UTF8.GetString(utf8PropertyName);
                }

                return propertyName;
            }
        }

        // Traverses the stack for the outermost object being deserialized using constructor parameters
        // Only called when calculating exception information.
        public RdnTypeInfo GetTopRdnTypeInfoWithParameterizedConstructor()
        {
            Debug.Assert(!IsContinuation);

            for (int i = 0; i < _count - 1; i++)
            {
                if (_stack[i].RdnTypeInfo.UsesParameterizedConstructor)
                {
                    return _stack[i].RdnTypeInfo;
                }
            }

            Debug.Assert(Current.RdnTypeInfo.UsesParameterizedConstructor);
            return Current.RdnTypeInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetConstructorArgumentState()
        {
            if (Current.RdnTypeInfo.UsesParameterizedConstructor)
            {
                Current.CtorArgumentState ??= new();
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Path = {RdnPath()}, Current = ConverterStrategy.{Current.RdnTypeInfo?.Converter.ConverterStrategy}, {Current.RdnTypeInfo?.Type.Name}";
    }
}
