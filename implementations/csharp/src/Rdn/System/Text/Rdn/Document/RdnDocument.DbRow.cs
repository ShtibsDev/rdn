// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class RdnDocument
    {
        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct DbRow
        {
            internal const int Size = 12;

            // Sign bit is currently unassigned
            private readonly int _location;

            // Sign bit is used for "HasComplexChildren" (StartArray)
            private readonly int _sizeOrLengthUnion;

            // Top 5 bits are RdnTokenType (supports values 0-31)
            // remaining 27 bits are the number of rows to skip to get to the next value
            // This isn't limiting on the number of rows, since Span.MaxLength / sizeof(DbRow) can't
            // exceed that range.
            private readonly int _numberOfRowsAndTypeUnion;

            /// <summary>
            /// Index into the payload
            /// </summary>
            internal int Location => _location;

            /// <summary>
            /// length of text in RDN payload (or number of elements if its a RDN array)
            /// </summary>
            internal int SizeOrLength => _sizeOrLengthUnion & int.MaxValue;

            internal bool IsUnknownSize => _sizeOrLengthUnion == UnknownSize;

            /// <summary>
            /// String/PropertyName: Unescaping is required.
            /// Array: At least one element is an object/array.
            /// Otherwise; false
            /// </summary>
            internal bool HasComplexChildren => _sizeOrLengthUnion < 0;

            internal int NumberOfRows =>
                _numberOfRowsAndTypeUnion & 0x07FFFFFF; // Number of rows that the current RDN element occupies within the database

            internal RdnTokenType TokenType => (RdnTokenType)(unchecked((uint)_numberOfRowsAndTypeUnion) >> 27);

            internal const int UnknownSize = -1;

            internal DbRow(RdnTokenType rdnTokenType, int location, int sizeOrLength)
            {
                Debug.Assert(rdnTokenType > RdnTokenType.None && (rdnTokenType <= RdnTokenType.Null || rdnTokenType == RdnTokenType.RdnDateTime || rdnTokenType == RdnTokenType.RdnTimeOnly || rdnTokenType == RdnTokenType.RdnDuration || rdnTokenType == RdnTokenType.RdnRegExp || rdnTokenType == RdnTokenType.StartSet || rdnTokenType == RdnTokenType.EndSet || rdnTokenType == RdnTokenType.StartMap || rdnTokenType == RdnTokenType.EndMap));
                Debug.Assert((byte)rdnTokenType < 1 << 5);
                Debug.Assert(location >= 0);
                Debug.Assert(sizeOrLength >= UnknownSize);
                Debug.Assert(Unsafe.SizeOf<DbRow>() == Size);

                _location = location;
                _sizeOrLengthUnion = sizeOrLength;
                _numberOfRowsAndTypeUnion = (int)rdnTokenType << 27;
            }

            internal bool IsSimpleValue => TokenType >= RdnTokenType.PropertyName && TokenType <= RdnTokenType.RdnRegExp;
        }
    }
}
