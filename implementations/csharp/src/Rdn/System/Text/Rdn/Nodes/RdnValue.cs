// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization;
using Rdn.Serialization.Metadata;

namespace Rdn.Nodes
{
    /// <summary>
    /// Represents a mutable RDN value.
    /// </summary>
    public abstract partial class RdnValue : RdnNode
    {
        internal const string CreateUnreferencedCodeMessage = "Creating RdnValue instances with non-primitive types is not compatible with trimming. It can result in non-primitive types being serialized, which may have their members trimmed.";
        internal const string CreateDynamicCodeMessage = "Creating RdnValue instances with non-primitive types requires generating code at runtime.";

        private protected RdnValue(RdnNodeOptions? options) : base(options) { }

        /// <summary>
        ///   Tries to obtain the current RDN value and returns a value that indicates whether the operation succeeded.
        /// </summary>
        /// <remarks>
        ///   {T} can be the type or base type of the underlying value.
        ///   If the underlying value is a <see cref="RdnElement"/> then {T} can also be the type of any primitive
        ///   value supported by current <see cref="RdnElement"/>.
        ///   Specifying the <see cref="object"/> type for {T} will always succeed and return the underlying value as <see cref="object"/>.<br />
        ///   The underlying value of a <see cref="RdnValue"/> after deserialization is an instance of <see cref="RdnElement"/>,
        ///   otherwise it's the value specified when the <see cref="RdnValue"/> was created.
        /// </remarks>
        /// <seealso cref="RdnNode.GetValue{T}"></seealso>
        /// <typeparam name="T">The type of value to obtain.</typeparam>
        /// <param name="value">When this method returns, contains the parsed value.</param>
        /// <returns><see langword="true"/> if the value can be successfully obtained; otherwise, <see langword="false"/>.</returns>
        public abstract bool TryGetValue<T>([NotNullWhen(true)] out T? value);

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </returns>
        /// <typeparam name="T">The type of value to create.</typeparam>
        /// <param name="value">The value to create.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        [RequiresUnreferencedCode(CreateUnreferencedCodeMessage + " Use the overload that takes a RdnTypeInfo, or make sure all of the required types are preserved.")]
        [RequiresDynamicCode(CreateDynamicCodeMessage)]
        public static RdnValue? Create<T>(T? value, RdnNodeOptions? options = null)
        {
            if (value is null)
            {
                return null;
            }

            if (value is RdnNode)
            {
                ThrowHelper.ThrowArgumentException_NodeValueNotAllowed(nameof(value));
            }

            if (value is RdnElement element)
            {
                return CreateFromElement(ref element, options);
            }

            var rdnTypeInfo = RdnSerializerOptions.Default.GetTypeInfo<T>();
            return CreateFromTypeInfo(value, rdnTypeInfo, options);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="RdnValue"/> class that contains the specified value.
        /// </returns>
        /// <typeparam name="T">The type of value to create.</typeparam>
        /// <param name="value">The value to create.</param>
        /// <param name="rdnTypeInfo">The <see cref="RdnTypeInfo"/> that will be used to serialize the value.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="RdnValue"/> class that contains the specified value.</returns>
        public static RdnValue? Create<T>(T? value, RdnTypeInfo<T> rdnTypeInfo, RdnNodeOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(rdnTypeInfo);

            if (value is null)
            {
                return null;
            }

            if (value is RdnNode)
            {
                ThrowHelper.ThrowArgumentException_NodeValueNotAllowed(nameof(value));
            }

            rdnTypeInfo.EnsureConfigured();

            if (value is RdnElement element && rdnTypeInfo.EffectiveConverter.IsInternalConverter)
            {
                return CreateFromElement(ref element, options);
            }

            return CreateFromTypeInfo(value, rdnTypeInfo, options);
        }

        internal override bool DeepEqualsCore(RdnNode otherNode)
        {
            if (GetValueKind() != otherNode.GetValueKind())
            {
                return false;
            }

            // Fall back to slow path that converts the nodes to RdnElement.
            RdnElement thisElement = ToRdnElement(this, out RdnDocument? thisDocument);
            RdnElement otherElement = ToRdnElement(otherNode, out RdnDocument? otherDocument);
            try
            {
                return RdnElement.DeepEquals(thisElement, otherElement);
            }
            finally
            {
                thisDocument?.Dispose();
                otherDocument?.Dispose();
            }

            static RdnElement ToRdnElement(RdnNode node, out RdnDocument? backingDocument)
            {
                if (node.UnderlyingElement is { } element)
                {
                    backingDocument = null;
                    return element;
                }

                Utf8RdnWriter writer = Utf8RdnWriterCache.RentWriterAndBuffer(
                    options: default,
                    RdnSerializerOptions.BufferSizeDefault,
                    out PooledByteBufferWriter output);

                try
                {
                    node.WriteTo(writer);
                    writer.Flush();
                    Utf8RdnReader reader = new(output.WrittenSpan);
                    backingDocument = RdnDocument.ParseValue(ref reader);
                    return backingDocument.RootElement;
                }
                finally
                {
                    Utf8RdnWriterCache.ReturnWriterAndBuffer(writer, output);
                }
            }
        }

        internal sealed override void GetPath(ref ValueStringBuilder path, RdnNode? child)
        {
            Debug.Assert(child == null);

            Parent?.GetPath(ref path, this);
        }

        internal static RdnValue CreateFromTypeInfo<T>(T value, RdnTypeInfo<T> rdnTypeInfo, RdnNodeOptions? options = null)
        {
            Debug.Assert(rdnTypeInfo.IsConfigured);
            Debug.Assert(value != null);

            if (RdnValue<T>.TypeIsSupportedPrimitive &&
                rdnTypeInfo is { EffectiveConverter.IsInternalConverter: true } &&
                (rdnTypeInfo.EffectiveNumberHandling & RdnNumberHandling.WriteAsString) is 0)
            {
                // If the type is using the built-in converter for a known primitive,
                // switch to the more efficient RdnValuePrimitive<T> implementation.
                return new RdnValuePrimitive<T>(value, rdnTypeInfo.EffectiveConverter, options);
            }

            return new RdnValueCustomized<T>(value, rdnTypeInfo, options);
        }

        internal static RdnValue? CreateFromElement(ref readonly RdnElement element, RdnNodeOptions? options = null)
        {
            switch (element.ValueKind)
            {
                case RdnValueKind.Null:
                    return null;

                case RdnValueKind.Object or RdnValueKind.Array:
                    // Force usage of RdnArray and RdnObject instead of supporting those in an RdnValue.
                    ThrowHelper.ThrowInvalidOperationException_NodeElementCannotBeObjectOrArray();
                    return null;

                default:
                    return new RdnValueOfElement(element, options);
            }
        }
    }
}
