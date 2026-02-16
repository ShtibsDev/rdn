// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class RdnDocument
    {
        // Ensure this stays on the stack by making it a ref struct.
        private ref struct PropertyNameSet : IDisposable
        {
            // Data structure to track property names in an object while deserializing
            // into a RdnDocument and validate that there are no duplicates. A small
            // array is used when the number of properties is small and no properties
            // are escaped. Otherwise a hash set is used.

            private HashSet<ReadOnlyMemory<byte>>? _hashSet;

            private const int ArraySetThreshold = 16;
            private int _arraySetCount;
            private bool _useArraySet = true;

#if NET
            [InlineArray(ArraySetThreshold)]
            private struct InlineRangeArray16
            {
                private (int Start, int Length) _element0;
            }

            private InlineRangeArray16 _arraySet;
#else
            private readonly (int Start, int Length)[] _arraySet = new (int Start, int Length)[ArraySetThreshold];
#endif

            public PropertyNameSet()
            {
            }

            internal void SetCapacity(int capacity)
            {
                if (capacity <= ArraySetThreshold)
                {
                    _useArraySet = true;
                }
                else
                {
                    _useArraySet = false;
                    if (_hashSet is null)
                    {
                        _hashSet = new HashSet<ReadOnlyMemory<byte>>(
#if NET
                            capacity,
#endif
                            PropertyNameComparer.Instance);
                    }
                    else
                    {
#if NET
                        _hashSet.EnsureCapacity(capacity);
#endif
                    }
                }
            }

            internal void AddPropertyName(RdnProperty property, RdnDocument document)
            {
                DbRow dbRow = document._parsedData.Get(property.Value.MetadataDbIndex - DbRow.Size);
                Debug.Assert(dbRow.TokenType is RdnTokenType.PropertyName);

                ReadOnlyMemory<byte> utf8Rdn = document._utf8Rdn;
                ReadOnlyMemory<byte> propertyName = utf8Rdn.Slice(dbRow.Location, dbRow.SizeOrLength);

                if (dbRow.HasComplexChildren)
                {
                    SwitchToHashSet(utf8Rdn);
                    propertyName = RdnReaderHelper.GetUnescaped(propertyName.Span);
                }

                if (_useArraySet)
                {
                    for (int i = 0; i < _arraySetCount; i++)
                    {
                        (int Start, int Length) range = _arraySet[i];
                        ReadOnlySpan<byte> previousPropertyName = utf8Rdn.Span.Slice(range.Start, range.Length);

                        if (previousPropertyName.SequenceEqual(propertyName.Span))
                        {
                            ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(propertyName.Span);
                        }
                    }

                    _arraySet[_arraySetCount] = (dbRow.Location, dbRow.SizeOrLength);
                    _arraySetCount++;
                }
                else
                {
                    Debug.Assert(_hashSet is not null);

                    if (!_hashSet.Add(propertyName))
                    {
                        ThrowHelper.ThrowRdnException_DuplicatePropertyNotAllowed(propertyName.Span);
                    }
                }
            }

            private void SwitchToHashSet(ReadOnlyMemory<byte> utf8Rdn)
            {
                if (_useArraySet)
                {
                    _hashSet ??= new HashSet<ReadOnlyMemory<byte>>(
#if NET
                        ArraySetThreshold,
#endif
                        PropertyNameComparer.Instance);

                    for (int i = 0; i < _arraySetCount; i++)
                    {
                        (int Start, int Length) range = _arraySet[i];
                        ReadOnlyMemory<byte> propertyName = utf8Rdn.Slice(range.Start, range.Length);
                        bool success = _hashSet.Add(propertyName);
                        Debug.Assert(success, $"Property name {propertyName} should not already exist in the set.");
                    }

                    _useArraySet = false;
                    _arraySetCount = 0;
                }
            }

            internal void Reset()
            {
                _hashSet?.Clear();
                _arraySetCount = 0;
            }

            public readonly void Dispose()
            {
            }

            private sealed class PropertyNameComparer : IEqualityComparer<ReadOnlyMemory<byte>>
            {
                internal static readonly PropertyNameComparer Instance = new();

                public bool Equals(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
                    left.Length == right.Length && left.Span.SequenceEqual(right.Span);

                public int GetHashCode(ReadOnlyMemory<byte> name)
                {
                    // Marvin is the currently used hash algorithm for string comparisons.
                    // The seed is unique to this process so an item's hash code can't easily be
                    // discovered by an adversary trying to perform a denial of service attack.
                    return Marvin.ComputeHash32(name.Span, Marvin.DefaultSeed);
                }
            }
        }
    }
}
