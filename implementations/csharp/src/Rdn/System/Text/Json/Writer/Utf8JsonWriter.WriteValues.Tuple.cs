// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Rdn
{
    public sealed partial class Utf8JsonWriter
    {
        /// <summary>
        /// Writes the beginning of an RDN Tuple: <c>(</c>.
        /// </summary>
        public void WriteStartTuple()
        {
            if (CurrentDepth >= _options.MaxDepth)
            {
                ThrowInvalidOperationException_DepthTooLarge();
            }

            if (_options.IndentedOrNotSkipValidation)
            {
                WriteStartTupleSlow();
            }
            else
            {
                WriteStartMinimized(JsonConstants.OpenParen);
            }

            _currentDepth &= JsonConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = JsonTokenType.StartArray;
        }

        private void WriteStartTupleSlow()
        {
            Debug.Assert(_options.Indented || !_options.SkipValidation);

            if (_options.Indented)
            {
                if (!_options.SkipValidation)
                {
                    ValidateStart();
                    UpdateBitStackOnStartTuple();
                }
                WriteStartIndented(JsonConstants.OpenParen);
            }
            else
            {
                Debug.Assert(!_options.SkipValidation);
                ValidateStart();
                UpdateBitStackOnStartTuple();
                WriteStartMinimized(JsonConstants.OpenParen);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBitStackOnStartTuple()
        {
            _bitStack.PushFalse();
            int depth = _bitStack.CurrentDepth;
            if (depth < 64)
            {
                _tupleDepthMask |= (1L << depth);
            }
            _enclosingContainer = EnclosingContainerType.Tuple;
        }

        /// <summary>
        /// Writes the end of an RDN Tuple: <c>)</c>.
        /// </summary>
        public void WriteEndTuple()
        {
            WriteEnd(JsonConstants.CloseParen);
            _tokenType = JsonTokenType.EndArray;
        }
    }
}
