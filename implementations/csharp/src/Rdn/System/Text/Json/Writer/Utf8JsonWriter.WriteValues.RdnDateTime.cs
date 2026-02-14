// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Rdn
{
    public sealed partial class Utf8JsonWriter
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
            _tokenType = JsonTokenType.RdnDateTime;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            output[BytesPending++] = JsonConstants.AtSign;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            if (_tokenType != JsonTokenType.PropertyName)
            {
                if (_tokenType != JsonTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = JsonConstants.AtSign;
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
            _tokenType = JsonTokenType.RdnDateTime;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            output[BytesPending++] = JsonConstants.AtSign;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            if (_tokenType != JsonTokenType.PropertyName)
            {
                if (_tokenType != JsonTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = JsonConstants.AtSign;
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
            _tokenType = JsonTokenType.RdnDateTime;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            output[BytesPending++] = JsonConstants.AtSign;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            if (_tokenType != JsonTokenType.PropertyName)
            {
                if (_tokenType != JsonTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = JsonConstants.AtSign;
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
            _tokenType = JsonTokenType.RdnTimeOnly;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            output[BytesPending++] = JsonConstants.AtSign;
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
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            if (_tokenType != JsonTokenType.PropertyName)
            {
                if (_tokenType != JsonTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = JsonConstants.AtSign;
            int written = RdnWriterHelper.FormatRdnTimeOnly(output.Slice(BytesPending), value);
            BytesPending += written;
        }

        /// <summary>
        /// Writes an RdnDuration as an RDN @-prefixed literal: @P... (no quotes).
        /// </summary>
        public void WriteRdnDurationValue(RdnDuration value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            string iso = value.Iso ?? "P0D";
            byte[] isoBytes = System.Text.Encoding.UTF8.GetBytes(iso);

            if (_options.Indented)
            {
                WriteRdnDurationValueIndented(isoBytes);
            }
            else
            {
                WriteRdnDurationValueMinimized(isoBytes);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = JsonTokenType.RdnDuration;
        }

        private void WriteRdnDurationValueMinimized(byte[] isoBytes)
        {
            int maxRequired = 1 + isoBytes.Length + 1; // @ + value + optional separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            output[BytesPending++] = JsonConstants.AtSign;
            isoBytes.CopyTo(output.Slice(BytesPending));
            BytesPending += isoBytes.Length;
        }

        private void WriteRdnDurationValueIndented(byte[] isoBytes)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + 1 + isoBytes.Length + 1 + _newLineLength;

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = JsonConstants.ListSeparator;
            }

            if (_tokenType != JsonTokenType.PropertyName)
            {
                if (_tokenType != JsonTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = JsonConstants.AtSign;
            isoBytes.CopyTo(output.Slice(BytesPending));
            BytesPending += isoBytes.Length;
        }

        /// <summary>
        /// Writes a TimeSpan as an RDN @-prefixed ISO 8601 duration literal (e.g. @P1DT2H3M4S).
        /// </summary>
        public void WriteRdnTimeSpanValue(TimeSpan value)
        {
            string iso = RdnWriterHelper.FormatTimeSpanAsIsoDuration(value);
            WriteRdnDurationValue(new RdnDuration(iso));
        }
    }
}
