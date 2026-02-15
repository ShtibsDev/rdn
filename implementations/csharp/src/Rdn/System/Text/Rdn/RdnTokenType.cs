// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    /// <summary>
    /// This enum defines the various RDN tokens that make up a RDN text and is used by
    /// the <see cref="Utf8RdnReader"/> when moving from one token to the next.
    /// The <see cref="Utf8RdnReader"/> starts at 'None' by default. The 'Comment' enum value
    /// is only ever reached in a specific <see cref="Utf8RdnReader"/> mode and is not
    /// reachable by default.
    /// </summary>
    public enum RdnTokenType : byte
    {
        // Do not re-number.
        // We rely on the underlying values to quickly check things like RdnReaderHelper.IsTokenTypePrimitive and Utf8RdnWriter.CanWriteValue

        /// <summary>
        ///   Indicates that there is no value (as distinct from <see cref="Null"/>).
        /// </summary>
        /// <remarks>
        ///   This is the default token type if no data has been read by the <see cref="Utf8RdnReader"/>.
        /// </remarks>
        None = 0,

        /// <summary>
        ///   Indicates that the token type is the start of a RDN object.
        /// </summary>
        StartObject = 1,

        /// <summary>
        ///   Indicates that the token type is the end of a RDN object.
        /// </summary>
        EndObject = 2,

        /// <summary>
        ///   Indicates that the token type is the start of a RDN array.
        /// </summary>
        StartArray = 3,

        /// <summary>
        ///   Indicates that the token type is the end of a RDN array.
        /// </summary>
        EndArray = 4,

        /// <summary>
        ///   Indicates that the token type is a RDN property name.
        /// </summary>
        PropertyName = 5,

        /// <summary>
        ///   Indicates that the token type is the comment string.
        /// </summary>
        Comment = 6,

        /// <summary>
        ///   Indicates that the token type is a RDN string.
        /// </summary>
        String = 7,

        /// <summary>
        ///   Indicates that the token type is a RDN number.
        /// </summary>
        Number = 8,

        /// <summary>
        ///   Indicates that the token type is the RDN literal <c>true</c>.
        /// </summary>
        True = 9,

        /// <summary>
        ///   Indicates that the token type is the RDN literal <c>false</c>.
        /// </summary>
        False = 10,

        /// <summary>
        ///   Indicates that the token type is the RDN literal <c>null</c>.
        /// </summary>
        Null = 11,

        /// <summary>
        ///   Indicates that the token type is an RDN DateTime literal (e.g. <c>@2024-01-15T10:30:00.123Z</c>).
        /// </summary>
        RdnDateTime = 12,

        /// <summary>
        ///   Indicates that the token type is an RDN TimeOnly literal (e.g. <c>@14:30:00</c>).
        /// </summary>
        RdnTimeOnly = 13,

        /// <summary>
        ///   Indicates that the token type is an RDN Duration literal (e.g. <c>@P1Y2M3DT4H5M6S</c>).
        /// </summary>
        RdnDuration = 14,

        /// <summary>
        ///   Indicates that the token type is an RDN RegExp literal (e.g. <c>/^[a-z]+$/gi</c>).
        /// </summary>
        RdnRegExp = 15,

        /// <summary>
        ///   Indicates that the token type is the start of an RDN Set.
        /// </summary>
        StartSet = 16,

        /// <summary>
        ///   Indicates that the token type is the end of an RDN Set.
        /// </summary>
        EndSet = 17,

        /// <summary>
        ///   Indicates that the token type is the start of an RDN Map.
        /// </summary>
        StartMap = 18,

        /// <summary>
        ///   Indicates that the token type is the end of an RDN Map.
        /// </summary>
        EndMap = 19,

        /// <summary>
        ///   Indicates that the token type is an RDN binary literal (e.g. <c>b"SGVsbG8="</c> or <c>x"48656C6C6F"</c>).
        /// </summary>
        RdnBinary = 20,
    }
}
