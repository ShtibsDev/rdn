// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Rdn.Encodings.Web;

namespace Rdn
{
    /// <summary>
    /// Provides the ability for the user to define custom behavior when writing RDN
    /// using the <see cref="Utf8RdnWriter"/>. By default, the RDN is written without
    /// any indentation or extra white space. Also, the <see cref="Utf8RdnWriter"/> will
    /// throw an exception if the user attempts to write structurally invalid RDN.
    /// </summary>
    public struct RdnWriterOptions
    {
        private static readonly string s_alternateNewLine = Environment.NewLine.Length == 2 ? RdnConstants.NewLineLineFeed : RdnConstants.NewLineCarriageReturnLineFeed;

        internal const int DefaultMaxDepth = 1000;

        private int _maxDepth;
        private int _optionsMask;

        /// <summary>
        /// The encoder to use when escaping strings, or <see langword="null" /> to use the default encoder.
        /// </summary>
        public JavaScriptEncoder? Encoder { get; set; }

        /// <summary>
        /// Defines whether the <see cref="Utf8RdnWriter"/> should pretty print the RDN which includes:
        /// indenting nested RDN tokens, adding new lines, and adding white space between property names and values.
        /// By default, the RDN is written without any extra white space.
        /// </summary>
        public bool Indented
        {
            get
            {
                return (_optionsMask & IndentBit) != 0;
            }
            set
            {
                if (value)
                    _optionsMask |= IndentBit;
                else
                    _optionsMask &= ~IndentBit;
            }
        }

        /// <summary>
        /// Defines the indentation character used by <see cref="Utf8RdnWriter"/> when <see cref="Indented"/> is enabled. Defaults to the space character.
        /// </summary>
        /// <remarks>Allowed characters are space and horizontal tab.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains an invalid character.</exception>
        public char IndentCharacter
        {
            readonly get => (_optionsMask & IndentCharacterBit) != 0 ? RdnConstants.TabIndentCharacter : RdnConstants.DefaultIndentCharacter;
            set
            {
                RdnWriterHelper.ValidateIndentCharacter(value);
                if (value is not RdnConstants.DefaultIndentCharacter)
                    _optionsMask |= IndentCharacterBit;
                else
                    _optionsMask &= ~IndentCharacterBit;
            }
        }

        /// <summary>
        /// Defines the indentation size used by <see cref="Utf8RdnWriter"/> when <see cref="Indented"/> is enabled. Defaults to two.
        /// </summary>
        /// <remarks>Allowed values are integers between 0 and 127, included.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is out of the allowed range.</exception>
        public int IndentSize
        {
            readonly get => EncodeIndentSize((_optionsMask & IndentSizeMask) >> OptionsBitCount);
            set
            {
                RdnWriterHelper.ValidateIndentSize(value);
                _optionsMask = (_optionsMask & ~IndentSizeMask) | (EncodeIndentSize(value) << OptionsBitCount);
            }
        }

        // Encoding is applied by swapping 0 with the default value to ensure default(RdnWriterOptions) instances are well-defined.
        // As this operation is symmetrical, it can also be used to decode.
        private static int EncodeIndentSize(int value) => value switch
        {
            0 => RdnConstants.DefaultIndentSize,
            RdnConstants.DefaultIndentSize => 0,
            _ => value
        };

        /// <summary>
        /// Gets or sets the maximum depth allowed when writing RDN, with the default (i.e. 0) indicating a max depth of 1000.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the max depth is set to a negative value.
        /// </exception>
        /// <remarks>
        /// Reading past this depth will throw a <exception cref="RdnException"/>.
        /// </remarks>
        public int MaxDepth
        {
            readonly get => _maxDepth;
            set
            {
                if (value < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(nameof(value));
                }

                _maxDepth = value;
            }
        }

        /// <summary>
        /// Defines whether the <see cref="Utf8RdnWriter"/> should skip structural validation and allow
        /// the user to write invalid RDN, when set to true. If set to false, any attempts to write invalid RDN will result in
        /// a <exception cref="InvalidOperationException"/> to be thrown.
        /// </summary>
        /// <remarks>
        /// If the RDN being written is known to be correct,
        /// then skipping validation (by setting it to true) could improve performance.
        /// An example of invalid RDN where the writer will throw (when SkipValidation
        /// is set to false) is when you write a value within a RDN object
        /// without a property name.
        /// </remarks>
        public bool SkipValidation
        {
            get
            {
                return (_optionsMask & SkipValidationBit) != 0;
            }
            set
            {
                if (value)
                    _optionsMask |= SkipValidationBit;
                else
                    _optionsMask &= ~SkipValidationBit;
            }
        }

        /// <summary>
        /// Gets or sets the new line string to use when <see cref="Indented"/> is <see langword="true"/>.
        /// The default is the value of <see cref="Environment.NewLine"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the new line string is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the new line string is not <c>\n</c> or <c>\r\n</c>.
        /// </exception>
        public string NewLine
        {
            get => (_optionsMask & NewLineBit) != 0 ? s_alternateNewLine : Environment.NewLine;
            set
            {
                RdnWriterHelper.ValidateNewLine(value);
                if (value != Environment.NewLine)
                    _optionsMask |= NewLineBit;
                else
                    _optionsMask &= ~NewLineBit;
            }
        }

        /// <summary>
        /// When <see langword="true"/>, the writer always emits the <c>Map{</c> prefix
        /// for non-empty Maps. When <see langword="false"/> (the default), non-empty Maps
        /// are written with just <c>{</c> since the <c>=&gt;</c> syntax is unambiguous.
        /// Empty Maps always emit the prefix (<c>Map{}</c>) to disambiguate from <c>{}</c>.
        /// </summary>
        public bool AlwaysWriteMapTypeName
        {
            get => (_optionsMask & AlwaysWriteMapTypeNameBit) != 0;
            set
            {
                if (value)
                    _optionsMask |= AlwaysWriteMapTypeNameBit;
                else
                    _optionsMask &= ~AlwaysWriteMapTypeNameBit;
            }
        }

        /// <summary>
        /// When <see langword="true"/>, the writer always emits the <c>Set{</c> prefix
        /// for non-empty Sets. When <see langword="false"/> (the default), non-empty Sets
        /// are written with just <c>{</c> since the content syntax (bare values) is unambiguous.
        /// Empty Sets always emit the prefix (<c>Set{}</c>) to disambiguate from <c>{}</c>.
        /// </summary>
        public bool AlwaysWriteSetTypeName
        {
            get => (_optionsMask & AlwaysWriteSetTypeNameBit) != 0;
            set
            {
                if (value)
                    _optionsMask |= AlwaysWriteSetTypeNameBit;
                else
                    _optionsMask &= ~AlwaysWriteSetTypeNameBit;
            }
        }

        /// <summary>
        /// Convenience property that gets or sets both <see cref="AlwaysWriteMapTypeName"/> and
        /// <see cref="AlwaysWriteSetTypeName"/> together. The getter returns <see langword="true"/>
        /// only when both are set.
        /// </summary>
        public bool AlwaysWriteCollectionTypeNames
        {
            get => AlwaysWriteMapTypeName && AlwaysWriteSetTypeName;
            set
            {
                AlwaysWriteMapTypeName = value;
                AlwaysWriteSetTypeName = value;
            }
        }

        internal bool IndentedOrNotSkipValidation => (_optionsMask & (IndentBit | SkipValidationBit)) != SkipValidationBit;  // Equivalent to: Indented || !SkipValidation;

        private const int OptionsBitCount = 6;
        private const int IndentBit = 1;
        private const int SkipValidationBit = 2;
        private const int NewLineBit = 4;
        private const int IndentCharacterBit = 8;
        private const int AlwaysWriteMapTypeNameBit = 16;
        private const int AlwaysWriteSetTypeNameBit = 32;
        private const int IndentSizeMask = RdnConstants.MaximumIndentSize << OptionsBitCount;
    }
}
