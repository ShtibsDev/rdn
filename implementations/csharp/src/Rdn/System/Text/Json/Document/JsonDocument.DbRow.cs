// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class JsonDocument
    {
        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct DbRow
        {
            internal const int Size = 12;

            // Sign bit is currently unassigned
            private readonly int _location;

            // Sign bit is used for "HasComplexChildren" (StartArray)
            private readonly int _sizeOrLengthUnion;

            // Top 5 bits are JsonTokenType (supports values 0-31)
            // remaining 27 bits are the number of rows to skip to get to the next value
            // This isn't limiting on the number of rows, since Span.MaxLength / sizeof(DbRow) can't
            // exceed that range.
            private readonly int _numberOfRowsAndTypeUnion;

            /// <summary>
            /// Index into the payload
            /// </summary>
            internal int Location => _location;

            /// <summary>
            /// length of text in JSON payload (or number of elements if its a JSON array)
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
                _numberOfRowsAndTypeUnion & 0x07FFFFFF; // Number of rows that the current JSON element occupies within the database

            internal JsonTokenType TokenType => (JsonTokenType)(unchecked((uint)_numberOfRowsAndTypeUnion) >> 27);

            internal const int UnknownSize = -1;

            internal DbRow(JsonTokenType jsonTokenType, int location, int sizeOrLength)
            {
                Debug.Assert(jsonTokenType > JsonTokenType.None && (jsonTokenType <= JsonTokenType.Null || jsonTokenType == JsonTokenType.RdnDateTime || jsonTokenType == JsonTokenType.RdnTimeOnly || jsonTokenType == JsonTokenType.RdnDuration || jsonTokenType == JsonTokenType.RdnRegExp || jsonTokenType == JsonTokenType.StartSet || jsonTokenType == JsonTokenType.EndSet || jsonTokenType == JsonTokenType.StartMap || jsonTokenType == JsonTokenType.EndMap));
                Debug.Assert((byte)jsonTokenType < 1 << 5);
                Debug.Assert(location >= 0);
                Debug.Assert(sizeOrLength >= UnknownSize);
                Debug.Assert(Unsafe.SizeOf<DbRow>() == Size);

                _location = location;
                _sizeOrLengthUnion = sizeOrLength;
                _numberOfRowsAndTypeUnion = (int)jsonTokenType << 27;
            }

            internal bool IsSimpleValue => TokenType >= JsonTokenType.PropertyName && TokenType <= JsonTokenType.RdnRegExp;
        }
    }
}
