// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;

namespace Rdn
{
    public sealed partial class Utf8RdnWriter
    {
        /// <summary>
        /// Writes a DateTime as an RDN @-prefixed literal: @YYYY-MM-DDTHH:mm:ss.sssZ (25 bytes, no quotes).
        /// </summary>
        public void WriteRdnDateTimeValue(DateTime value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnDateTimeValueIndented(value);
            }
            else
            {
                WriteRdnDateTimeValueMinimized(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnDateTime;
        }

        private void WriteRdnDateTimeValueMinimized(DateTime value)
        {
            // @ (1) + YYYY-MM-DDTHH:mm:ss.sssZ (24) + optionally 1 list separator = 26
            int maxRequired = 26;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnDateTime(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        private void WriteRdnDateTimeValueIndented(DateTime value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 26 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnDateTime(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        /// <summary>
        /// Writes a DateOnly as an RDN @-prefixed literal: @YYYY-MM-DD (11 bytes, no quotes).
        /// </summary>
        public void WriteRdnDateOnlyValue(DateOnly value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnDateOnlyValueIndented(value);
            }
            else
            {
                WriteRdnDateOnlyValueMinimized(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnDateTime;
        }

        private void WriteRdnDateOnlyValueMinimized(DateOnly value)
        {
            // @ (1) + YYYY-MM-DD (10) + optionally 1 list separator = 12
            int maxRequired = 12;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnDateOnly(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        private void WriteRdnDateOnlyValueIndented(DateOnly value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 12 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnDateOnly(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        /// <summary>
        /// Writes a DateTimeOffset as an RDN @-prefixed literal: @YYYY-MM-DDTHH:mm:ss.sssZ (25 bytes, no quotes).
        /// </summary>
        public void WriteRdnDateTimeOffsetValue(DateTimeOffset value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnDateTimeOffsetValueIndented(value);
            }
            else
            {
                WriteRdnDateTimeOffsetValueMinimized(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnDateTime;
        }

        private void WriteRdnDateTimeOffsetValueMinimized(DateTimeOffset value)
        {
            int maxRequired = 26;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnDateTimeOffset(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        private void WriteRdnDateTimeOffsetValueIndented(DateTimeOffset value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 26 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnDateTimeOffset(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        /// <summary>
        /// Writes a TimeOnly as an RDN @-prefixed literal: @HH:MM:SS[.mmm] (no quotes).
        /// </summary>
        public void WriteRdnTimeOnlyValue(TimeOnly value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnTimeOnlyValueIndented(value);
            }
            else
            {
                WriteRdnTimeOnlyValueMinimized(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnTimeOnly;
        }

        private void WriteRdnTimeOnlyValueMinimized(TimeOnly value)
        {
            // @ (1) + HH:MM:SS.mmm (12) + optionally 1 list separator = 14
            int maxRequired = 14;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnTimeOnly(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        private void WriteRdnTimeOnlyValueIndented(TimeOnly value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 14 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnTimeOnly(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        /// <summary>
        /// Writes an RdnDuration as an RDN @-prefixed literal: @P... (no quotes).
        /// Zero-allocation: ISO duration strings are pure ASCII, so each char is written directly as a byte.
        /// </summary>
        public void WriteRdnDurationValue(RdnDuration value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            string iso = value.Iso ?? "P0D";

            if (_options.Indented)
            {
                WriteRdnDurationValueIndented(iso);
            }
            else
            {
                WriteRdnDurationValueMinimized(iso);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnDuration;
        }

        private void WriteRdnDurationValueMinimized(string iso)
        {
            int maxRequired = 1 + iso.Length + 1; // @ + value + optional separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.AtSign;
            // ISO duration strings are pure ASCII (P, D, T, H, M, S, '.', '-', 0-9),
            // so each char maps 1:1 to a UTF-8 byte — no Encoding.UTF8.GetBytes allocation needed.
            for (int i = 0; i < iso.Length; i++)
            {
                output[BytesPending++] = (byte)iso[i];
            }
        }

        private void WriteRdnDurationValueIndented(string iso)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 1 + iso.Length + 1 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.AtSign;
            // ISO duration strings are pure ASCII — write each char directly as a byte.
            for (int i = 0; i < iso.Length; i++)
            {
                output[BytesPending++] = (byte)iso[i];
            }
        }

        /// <summary>
        /// Writes a TimeSpan as an RDN @-prefixed ISO 8601 duration literal (e.g. @P1DT2H3M4S).
        /// Zero-allocation: formats directly into the output buffer.
        /// </summary>
        public void WriteRdnTimeSpanValue(TimeSpan value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_options.Indented)
            {
                WriteRdnTimeSpanValueIndented(value);
            }
            else
            {
                WriteRdnTimeSpanValueMinimized(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnDuration;
        }

        private void WriteRdnTimeSpanValueMinimized(TimeSpan value)
        {
            // @ (1) + max duration ~26 + optionally 1 list separator = 28
            int maxRequired = 28;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatTimeSpanAsIsoDuration(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        private void WriteRdnTimeSpanValueIndented(TimeSpan value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 28 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.AtSign;
            int written = RdnWriterHelper.FormatTimeSpanAsIsoDuration(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        /// <summary>
        /// Writes a DateTime as an RDN @-prefixed Unix timestamp in milliseconds (e.g. @1705312200000).
        /// </summary>
        public void WriteRdnUnixTimestampValue(DateTime value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            long millis = (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;

            if (_options.Indented)
            {
                WriteRdnUnixTimestampValueIndented(millis);
            }
            else
            {
                WriteRdnUnixTimestampValueMinimized(millis);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnDateTime;
        }

        /// <summary>
        /// Writes a DateTimeOffset as an RDN @-prefixed Unix timestamp in milliseconds (e.g. @1705312200000).
        /// </summary>
        public void WriteRdnUnixTimestampOffsetValue(DateTimeOffset value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            long millis = value.ToUnixTimeMilliseconds();

            if (_options.Indented)
            {
                WriteRdnUnixTimestampValueIndented(millis);
            }
            else
            {
                WriteRdnUnixTimestampValueMinimized(millis);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = RdnTokenType.RdnDateTime;
        }

        private void WriteRdnUnixTimestampValueMinimized(long millis)
        {
            // @ (1) + up to 20 digits for long + optionally 1 list separator = 22
            int maxRequired = 22;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = RdnConstants.ListSeparator;
            }

            output[BytesPending++] = RdnConstants.AtSign;
            bool result = Utf8Formatter.TryFormat(millis, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteRdnUnixTimestampValueIndented(long millis)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 22 + _newLineLength;

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

            output[BytesPending++] = RdnConstants.AtSign;
            bool result = Utf8Formatter.TryFormat(millis, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }
    }
}
