// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Converters;
using Rdn.Serialization.Metadata;

namespace Rdn.Nodes
{
    /// <summary>
    ///   The base class that represents a single node within a mutable RDN document.
    /// </summary>
    /// <seealso cref="RdnSerializerOptions.UnknownTypeHandling"/> to specify that a type
    /// declared as an <see cref="object"/> should be deserialized as a <see cref="RdnNode"/>.
    public abstract partial class RdnNode
    {
        // Default options instance used when calling built-in RdnNode converters.
        private protected static readonly RdnSerializerOptions s_defaultOptions = new();

        private RdnNode? _parent;
        private RdnNodeOptions? _options;

        /// <summary>
        /// The underlying RdnElement if the node is backed by a RdnElement.
        /// </summary>
        internal virtual RdnElement? UnderlyingElement => null;

        /// <summary>
        ///   Options to control the behavior.
        /// </summary>
        public RdnNodeOptions? Options
        {
            get
            {
                if (!_options.HasValue && Parent != null)
                {
                    // Remember the parent options; if node is re-parented later we still want to keep the
                    // original options since they may have affected the way the node was created as is the case
                    // with RdnObject's case-insensitive dictionary.
                    _options = Parent.Options;
                }

                return _options;
            }
        }

        internal RdnNode(RdnNodeOptions? options = null)
        {
            _options = options;
        }

        /// <summary>
        ///   Casts to the derived <see cref="RdnArray"/> type.
        /// </summary>
        /// <returns>
        ///   A <see cref="RdnArray"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The node is not a <see cref="RdnArray"/>.
        /// </exception>
        public RdnArray AsArray()
        {
            RdnArray? jArray = this as RdnArray;

            if (jArray is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(RdnArray));
            }

            return jArray;
        }

        /// <summary>
        ///   Casts to the derived <see cref="RdnObject"/> type.
        /// </summary>
        /// <returns>
        ///   A <see cref="RdnObject"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The node is not a <see cref="RdnObject"/>.
        /// </exception>
        public RdnObject AsObject()
        {
            RdnObject? jObject = this as RdnObject;

            if (jObject is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(RdnObject));
            }

            return jObject;
        }

        /// <summary>
        ///   Casts to the derived <see cref="RdnValue"/> type.
        /// </summary>
        /// <returns>
        ///   A <see cref="RdnValue"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   The node is not a <see cref="RdnValue"/>.
        /// </exception>
        public RdnValue AsValue()
        {
            RdnValue? jValue = this as RdnValue;

            if (jValue is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(RdnValue));
            }

            return jValue;
        }

        /// <summary>
        ///   Gets the parent <see cref="RdnNode"/>.
        ///   If there is no parent, <see langword="null"/> is returned.
        ///   A parent can either be a <see cref="RdnObject"/> or a <see cref="RdnArray"/>.
        /// </summary>
        public RdnNode? Parent
        {
            get
            {
                return _parent;
            }
            internal set
            {
                _parent = value;
            }
        }

        /// <summary>
        ///   Gets the RDN path.
        /// </summary>
        /// <returns>The RDN Path value.</returns>
        public string GetPath()
        {
            if (Parent == null)
            {
                return "$";
            }

            var path = new ValueStringBuilder(stackalloc char[RdnConstants.StackallocCharThreshold]);
            path.Append('$');
            GetPath(ref path, null);
            return path.ToString();
        }

        internal abstract void GetPath(ref ValueStringBuilder path, RdnNode? child);

        /// <summary>
        ///   Gets the root <see cref="RdnNode"/>.
        /// </summary>
        /// <remarks>
        ///   The current node is returned if it is a root.
        /// </remarks>
        public RdnNode Root
        {
            get
            {
                RdnNode? parent = Parent;
                if (parent == null)
                {
                    return this;
                }

                while (parent.Parent != null)
                {
                    parent = parent.Parent;
                }

                return parent;
            }
        }

        /// <summary>
        ///   Gets the value for the current <see cref="RdnValue"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to obtain from the <see cref="RdnValue"/>.</typeparam>
        /// <returns>A value converted from the <see cref="RdnValue"/> instance.</returns>
        /// <remarks>
        ///   {T} can be the type or base type of the underlying value.
        ///   If the underlying value is a <see cref="RdnElement"/> then {T} can also be the type of any primitive
        ///   value supported by current <see cref="RdnElement"/>.
        ///   Specifying the <see cref="object"/> type for {T} will always succeed and return the underlying value as <see cref="object"/>.<br />
        ///   The underlying value of a <see cref="RdnValue"/> after deserialization is an instance of <see cref="RdnElement"/>,
        ///   otherwise it's the value specified when the <see cref="RdnValue"/> was created.
        /// </remarks>
        /// <seealso cref="Rdn.Nodes.RdnValue.TryGetValue"></seealso>
        /// <exception cref="FormatException">
        ///   The current <see cref="RdnNode"/> cannot be represented as a {T}.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   The current <see cref="RdnNode"/> is not a <see cref="RdnValue"/> or
        ///   is not compatible with {T}.
        /// </exception>
        public virtual T GetValue<T>() =>
            throw new InvalidOperationException(SR.Format(SR.NodeWrongType, nameof(RdnValue)));

        /// <summary>
        ///   Gets or sets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> is less than 0 or <paramref name="index"/> is greater than the number of properties.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   The current <see cref="RdnNode"/> is not a <see cref="RdnArray"/> or <see cref="RdnObject"/>.
        /// </exception>
        public RdnNode? this[int index]
        {
            get => GetItem(index);
            set => SetItem(index, value);
        }

        private protected virtual RdnNode? GetItem(int index)
        {
            ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(RdnArray), nameof(RdnObject));
            return null;
        }

        private protected virtual void SetItem(int index, RdnNode? node) =>
            ThrowHelper.ThrowInvalidOperationException_NodeWrongType(nameof(RdnArray), nameof(RdnObject));

        /// <summary>
        ///   Gets or sets the element with the specified property name.
        ///   If the property is not found, <see langword="null"/> is returned.
        /// </summary>
        /// <param name="propertyName">The name of the property to return.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///   The current <see cref="RdnNode"/> is not a <see cref="RdnObject"/>.
        /// </exception>
        public RdnNode? this[string propertyName]
        {
            get
            {
                return AsObject().GetItem(propertyName);
            }
            set
            {
                AsObject().SetItem(propertyName, value);
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="RdnNode"/>. All children nodes are recursively cloned.
        /// </summary>
        /// <returns>A new cloned instance of the current node.</returns>
        public RdnNode DeepClone() => DeepCloneCore();

        internal abstract RdnNode DeepCloneCore();

        /// <summary>
        /// Returns <see cref="RdnValueKind"/> of current instance.
        /// </summary>
        public RdnValueKind GetValueKind() => GetValueKindCore();

        private protected abstract RdnValueKind GetValueKindCore();

        /// <summary>
        /// Returns property name of the current node from the parent object.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The current parent is not a <see cref="RdnObject"/>.
        /// </exception>
        public string GetPropertyName()
        {
            RdnObject? parentObject = _parent as RdnObject;

            if (parentObject is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeParentWrongType(nameof(RdnObject));
            }

            return parentObject.GetPropertyName(this);
        }

        /// <summary>
        /// Returns index of the current node from the parent <see cref="RdnArray" />.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The current parent is not a <see cref="RdnArray"/>.
        /// </exception>
        public int GetElementIndex()
        {
            RdnArray? parentArray = _parent as RdnArray;

            if (parentArray is null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeParentWrongType(nameof(RdnArray));
            }

            return parentArray.GetElementIndex(this);
        }

        /// <summary>
        /// Compares the values of two nodes, including the values of all descendant nodes.
        /// </summary>
        /// <param name="node1">The <see cref="RdnNode"/> to compare.</param>
        /// <param name="node2">The <see cref="RdnNode"/> to compare.</param>
        /// <returns><c>true</c> if the tokens are equal; otherwise <c>false</c>.</returns>
        public static bool DeepEquals(RdnNode? node1, RdnNode? node2)
        {
            if (node1 is null)
            {
                return node2 is null;
            }
            else if (node2 is null)
            {
                return false;
            }

            return node1.DeepEqualsCore(node2);
        }

        internal abstract bool DeepEqualsCore(RdnNode node);

        /// <summary>
        /// Replaces this node with a new value.
        /// </summary>
        /// <typeparam name="T">The type of value to be replaced.</typeparam>
        /// <param name="value">Value that replaces this node.</param>
        [RequiresUnreferencedCode(RdnValue.CreateUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnValue.CreateDynamicCodeMessage)]
        public void ReplaceWith<T>(T value)
        {
            RdnNode? node;
            switch (_parent)
            {
                case RdnObject rdnObject:
                    node = ConvertFromValue(value);
                    rdnObject.SetItem(GetPropertyName(), node);
                    return;
                case RdnArray rdnArray:
                    node = ConvertFromValue(value);
                    rdnArray.SetItem(GetElementIndex(), node);
                    return;
            }
        }

        internal void AssignParent(RdnNode parent)
        {
            if (Parent != null)
            {
                ThrowHelper.ThrowInvalidOperationException_NodeAlreadyHasParent();
            }

            RdnNode? p = parent;
            while (p != null)
            {
                if (p == this)
                {
                    ThrowHelper.ThrowInvalidOperationException_NodeCycleDetected();
                }

                p = p.Parent;
            }

            Parent = parent;
        }

        /// <summary>
        /// Adaptation of the equivalent RdnValue.Create factory method extended
        /// to support arbitrary <see cref="RdnElement"/> and <see cref="RdnNode"/> values.
        /// TODO consider making public cf. https://github.com/dotnet/runtime/issues/70427
        /// </summary>
        [RequiresUnreferencedCode(RdnSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnSerializer.SerializationRequiresDynamicCodeMessage)]
        internal static RdnNode? ConvertFromValue<T>(T? value, RdnNodeOptions? options = null)
        {
            if (value is null)
            {
                return null;
            }

            if (value is RdnNode node)
            {
                return node;
            }

            if (value is RdnElement element)
            {
                return RdnNodeConverter.Create(element, options);
            }

            var rdnTypeInfo = RdnSerializerOptions.Default.GetTypeInfo<T>();
            return RdnValue.CreateFromTypeInfo(value, rdnTypeInfo, options);
        }
    }
}
