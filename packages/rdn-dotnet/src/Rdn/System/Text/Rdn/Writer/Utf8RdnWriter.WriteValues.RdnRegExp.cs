// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        /// <summary>
        /// Writes a regex as an RDN literal: /source/flags (no quotes).
        /// </summary>
        public void WriteRdnRegExpValue(string source, string flags)
        {
            ArgumentNullException.ThrowIfNull(source);
            flags ??= string.Empty;

            WriteRdnRegExpValue(source.AsSpan(), flags.AsSpan());
        }

        /// <summary>
        /// Writes a regex as an RDN literal: /source/flags (no quotes).
        /// </summary>
        public void WriteRdnRegExpValue(ReadOnlySpan<char> source, ReadOnlySpan<char> flags)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnRegExpValueIndented(source, flags);
            }
            else
            {
                WriteRdnRegExpValueMinimized(source, flags);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnRegExp;
        }

        private void WriteRdnRegExpValueMinimized(ReadOnlySpan<char> source, ReadOnlySpan<char> flags)
        {
            // / (1) + source (transcoded) + / (1) + flags (transcoded) + optional separator (1)
            int maxRequired = 3 + source.Length * RdnConstants.MaxExpansionFactorWhileTranscoding + flags.Length * RdnConstants.MaxExpansionFactorWhileTranscoding;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.Slash;
            TranscodeAndWrite(source, output);
            output[BytesPending++] = RdnConstants.Slash;
            TranscodeAndWrite(flags, output);
        }

        private void WriteRdnRegExpValueIndented(ReadOnlySpan<char> source, ReadOnlySpan<char> flags)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 3 + source.Length * RdnConstants.MaxExpansionFactorWhileTranscoding + flags.Length * RdnConstants.MaxExpansionFactorWhileTranscoding + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            if (_tokenType != RdnTokenType.PropertyName)
            {
                if (_tokenType != RdnTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = RdnConstants.Slash;
            TranscodeAndWrite(source, output);
            output[BytesPending++] = RdnConstants.Slash;
            TranscodeAndWrite(flags, output);
        }

        /// <summary>
        /// Writes a regex as an RDN literal using UTF-8 spans: /source/flags (no quotes).
        /// </summary>
        public void WriteRdnRegExpValue(ReadOnlySpan<byte> utf8Source, ReadOnlySpan<byte> utf8Flags)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnRegExpValueUtf8Indented(utf8Source, utf8Flags);
            }
            else
            {
                WriteRdnRegExpValueUtf8Minimized(utf8Source, utf8Flags);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnRegExp;
        }

        private void WriteRdnRegExpValueUtf8Minimized(ReadOnlySpan<byte> utf8Source, ReadOnlySpan<byte> utf8Flags)
        {
            // / (1) + source + / (1) + flags + optional separator (1)
            int maxRequired = 3 + utf8Source.Length + utf8Flags.Length;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.Slash;
            utf8Source.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Source.Length;
            output[BytesPending++] = RdnConstants.Slash;
            utf8Flags.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Flags.Length;
        }

        private void WriteRdnRegExpValueUtf8Indented(ReadOnlySpan<byte> utf8Source, ReadOnlySpan<byte> utf8Flags)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 3 + utf8Source.Length + utf8Flags.Length + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            if (_tokenType != RdnTokenType.PropertyName)
            {
                if (_tokenType != RdnTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = RdnConstants.Slash;
            utf8Source.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Source.Length;
            output[BytesPending++] = RdnConstants.Slash;
            utf8Flags.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Flags.Length;
        }
    }
}
