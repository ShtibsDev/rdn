// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Rdn
{
    /// <summary>
    /// Provides the ability for the user to define custom behavior when parsing RDN to create a <see cref="RdnDocument"/>.
    /// </summary>
    public struct RdnDocumentOptions
    {
        internal const int DefaultMaxDepth = 64;

        private int _maxDepth;
        private RdnCommentHandling _commentHandling;

        /// <summary>
        /// Defines how the <see cref="Utf8RdnReader"/> should handle comments when reading through the RDN.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the comment handling enum is set to a value that is not supported (or not within the <see cref="RdnCommentHandling"/> enum range).
        /// </exception>
        /// <remarks>
        /// By default <exception cref="RdnException"/> is thrown if a comment is encountered.
        /// </remarks>
        public RdnCommentHandling CommentHandling
        {
            readonly get => _commentHandling;
            set
            {
                Debug.Assert(value >= 0);
                if (value > RdnCommentHandling.Skip)
                    throw new ArgumentOutOfRangeException(nameof(value), SR.RdnDocumentDoesNotSupportComments);

                _commentHandling = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum depth allowed when reading RDN, with the default (i.e. 0) indicating a max depth of 64.
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
        /// Defines whether an extra comma at the end of a list of RDN values in an object or array
        /// is allowed (and ignored) within the RDN payload being read.
        /// </summary>
        /// <remarks>
        /// By default, it's set to false, and <exception cref="RdnException"/> is thrown if a trailing comma is encountered.
        /// </remarks>
        public bool AllowTrailingCommas { get; set; }

        /// <summary>
        /// Defines whether duplicate property names are allowed when deserializing RDN objects.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default, it's set to true. If set to false, <see cref="RdnException"/> is thrown
        /// when a duplicate property name is encountered during deserialization.
        /// </para>
        /// </remarks>
        private bool _disallowDuplicateProperties;
        public bool AllowDuplicateProperties
        {
            // These are negated because the declaring type is a struct and we want the value to be true
            // for the default struct value.
            get => !_disallowDuplicateProperties;
            set => _disallowDuplicateProperties = !value;
        }

        internal RdnReaderOptions GetReaderOptions()
        {
            return new RdnReaderOptions
            {
                AllowTrailingCommas = AllowTrailingCommas,
                CommentHandling = CommentHandling,
                MaxDepth = MaxDepth
            };
        }
    }
}
