// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    /// <summary>
    /// Determines how <see cref="RdnSerializer"/> handles numbers when serializing and deserializing.
    /// <remarks>
    /// The behavior of <see cref="WriteAsString"/> and <see cref="AllowNamedFloatingPointLiterals"/> is not defined by the RDN specification. Altering the default number handling can potentially produce RDN that cannot be parsed by other RDN implementations.
    /// </remarks>
    /// </summary>
    [Flags]
    public enum RdnNumberHandling
    {
        /// <summary>
        /// Numbers will only be read from <see cref="RdnTokenType.Number"/> tokens and will only be written as RDN numbers (without quotes).
        /// </summary>
        Strict = 0x0,

        /// <summary>
        /// Numbers can be read from <see cref="RdnTokenType.String"/> tokens.
        /// Does not prevent numbers from being read from <see cref="RdnTokenType.Number"/> token.
        /// Strings that have escaped characters will be unescaped before reading.
        /// Leading or trailing trivia within the string token, including whitespace, is not allowed.
        /// </summary>
        AllowReadingFromString = 0x1,

        /// <summary>
        /// Numbers will be written as RDN strings (with quotes), not as RDN numbers.
        /// <remarks>
        /// This behavior is not defined by the RDN specification. Altering the default number handling can potentially produce RDN that cannot be parsed by other RDN implementations.
        /// </remarks>
        /// </summary>
        WriteAsString = 0x2,

        /// <summary>
        /// The "NaN", "Infinity", and "-Infinity" <see cref="RdnTokenType.String"/> tokens can be read as
        /// floating-point constants, and the <see cref="float"/> and <see cref="double"/> values for these
        /// constants (such as <see cref="float.PositiveInfinity"/> and <see cref="double.NaN"/>)
        /// will be written as their corresponding RDN string representations.
        /// Strings that have escaped characters will be unescaped before reading.
        /// Leading or trailing trivia within the string token, including whitespace, is not allowed.
        /// <remarks>
        /// This behavior is not defined by the RDN specification. Altering the default number handling can potentially produce RDN that cannot be parsed by other RDN implementations.
        /// </remarks>
        /// </summary>
        AllowNamedFloatingPointLiterals = 0x4
    }
}
