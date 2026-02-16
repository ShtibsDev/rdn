// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Converters;
using System.Threading;

namespace Rdn.Nodes
{
    /// <summary>
    ///   Represents a mutable RDN array.
    /// </summary>
    /// <remarks>
    /// It is safe to perform multiple concurrent read operations on a <see cref="RdnArray"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("RdnArray[{List.Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed partial class RdnArray : RdnNode
    {
        private RdnElement? _rdnElement;
        private List<RdnNode?>? _list;

        internal override RdnElement? UnderlyingElement => _rdnElement;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnArray"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public RdnArray(RdnNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnArray"/> class that contains items from the specified params array.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="items">The items to add to the new <see cref="RdnArray"/>.</param>
        public RdnArray(RdnNodeOptions options, params RdnNode?[] items) : base(options)
        {
            InitializeFromArray(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnArray"/> class that contains items from the specified params span.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="items">The items to add to the new <see cref="RdnArray"/>.</param>
        public RdnArray(RdnNodeOptions options, params ReadOnlySpan<RdnNode?> items) : base(options)
        {
            InitializeFromSpan(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnArray"/> class that contains items from the specified array.
        /// </summary>
        /// <param name="items">The items to add to the new <see cref="RdnArray"/>.</param>
        public RdnArray(params RdnNode?[] items) : base()
        {
            InitializeFromArray(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnArray"/> class that contains items from the specified span.
        /// </summary>
        /// <param name="items">The items to add to the new <see cref="RdnArray"/>.</param>
        public RdnArray(params ReadOnlySpan<RdnNode?> items) : base()
        {
            InitializeFromSpan(items);
        }

        private protected override RdnValueKind GetValueKindCore() => RdnValueKind.Array;

        internal override RdnNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out List<RdnNode?>? list, out RdnElement? rdnElement);

            if (list is null)
            {
                return rdnElement.HasValue
                    ? new RdnArray(rdnElement.Value.Clone(), Options)
                    : new RdnArray(Options);
            }

            var rdnArray = new RdnArray(Options)
            {
                _list = new List<RdnNode?>(list.Count)
            };

            for (int i = 0; i < list.Count; i++)
            {
                rdnArray.Add(list[i]?.DeepCloneCore());
            }

            return rdnArray;
        }

        internal override bool DeepEqualsCore(RdnNode node)
        {
            switch (node)
            {
                case RdnObject:
                case RdnSet:
                case RdnMap:
                    return false;
                case RdnValue value:
                    // RdnValue instances have special comparison semantics, dispatch to their implementation.
                    return value.DeepEqualsCore(this);
                case RdnArray array:
                    List<RdnNode?> currentList = List;
                    List<RdnNode?> otherList = array.List;

                    if (currentList.Count != otherList.Count)
                    {
                        return false;
                    }

                    for (int i = 0; i < currentList.Count; i++)
                    {
                        if (!DeepEquals(currentList[i], otherList[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                default:
                    Debug.Fail("Impossible case");
                    return false;
            }
        }

        internal int GetElementIndex(RdnNode? node)
        {
            return List.IndexOf(node);
        }

        /// <summary>
        /// Returns an enumerable that wraps calls to <see cref="RdnNode.GetValue{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to obtain from the <see cref="RdnValue"/>.</typeparam>
        /// <returns>An enumerable iterating over values of the array.</returns>
        public IEnumerable<T> GetValues<T>()
        {
            foreach (RdnNode? item in List)
            {
                yield return item is null ? (T)(object?)null! : item.GetValue<T>();
            }
        }

        private void InitializeFromArray(RdnNode?[] items)
        {
            var list = new List<RdnNode?>(items);

            for (int i = 0; i < list.Count; i++)
            {
                list[i]?.AssignParent(this);
            }

            _list = list;
        }

        private void InitializeFromSpan(ReadOnlySpan<RdnNode?> items)
        {
            List<RdnNode?> list = new(items.Length);

#if NET
            list.AddRange(items);
#else
            foreach (RdnNode? item in items)
            {
                list.Add(item);
            }
#endif

            for (int i = 0; i < list.Count; i++)
            {
                list[i]?.AssignParent(this);
            }

            _list = list;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnArray"/> class that contains items from the specified <see cref="RdnElement"/>.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="RdnArray"/> class that contains items from the specified <see cref="RdnElement"/>.
        /// </returns>
        /// <param name="element">The <see cref="RdnElement"/>.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <exception cref="InvalidOperationException">
        ///   The <paramref name="element"/> is not a <see cref="RdnValueKind.Array"/>.
        /// </exception>
        public static RdnArray? Create(RdnElement element, RdnNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                RdnValueKind.Null => null,
                RdnValueKind.Array => new RdnArray(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(RdnValueKind.Array))),
            };
        }

        internal RdnArray(RdnElement element, RdnNodeOptions? options = null) : base(options)
        {
            Debug.Assert(element.ValueKind == RdnValueKind.Array);
            _rdnElement = element;
        }

        /// <summary>
        ///   Adds an object to the end of the <see cref="RdnArray"/>.
        /// </summary>
        /// <typeparam name="T">The type of object to be added.</typeparam>
        /// <param name="value">
        ///   The object to be added to the end of the <see cref="RdnArray"/>.
        /// </param>
        [RequiresUnreferencedCode(RdnValue.CreateUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnValue.CreateDynamicCodeMessage)]
        public void Add<T>(T? value)
        {
            RdnNode? nodeToAdd = ConvertFromValue(value, Options);
            Add(nodeToAdd);
        }

        /// <summary>
        /// Gets or creates the underlying list containing the element nodes of the array.
        /// </summary>
        private List<RdnNode?> List => _list ?? InitializeList();

        private protected override RdnNode? GetItem(int index)
        {
            return List[index];
        }

        private protected override void SetItem(int index, RdnNode? value)
        {
            value?.AssignParent(this);
            DetachParent(List[index]);
            List[index] = value;
        }

        internal override void GetPath(ref ValueStringBuilder path, RdnNode? child)
        {
            Parent?.GetPath(ref path, this);

            if (child != null)
            {
                int index = List.IndexOf(child);
                Debug.Assert(index >= 0);

                path.Append('[');
#if NET
                Span<char> chars = stackalloc char[RdnConstants.MaximumFormatUInt32Length];
                bool formatted = ((uint)index).TryFormat(chars, out int charsWritten);
                Debug.Assert(formatted);
                path.Append(chars.Slice(0, charsWritten));
#else
                path.Append(index.ToString());
#endif
                path.Append(']');
            }
        }

        /// <inheritdoc/>
        public override void WriteTo(Utf8RdnWriter writer, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            GetUnderlyingRepresentation(out List<RdnNode?>? list, out RdnElement? rdnElement);

            if (list is null && rdnElement.HasValue)
            {
                rdnElement.Value.WriteTo(writer);
            }
            else
            {
                writer.WriteStartArray();

                foreach (RdnNode? element in List)
                {
                    if (element is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        element.WriteTo(writer, options);
                    }
                }

                writer.WriteEndArray();
            }
        }

        private List<RdnNode?> InitializeList()
        {
            GetUnderlyingRepresentation(out List<RdnNode?>? list, out RdnElement? rdnElement);

            if (list is null)
            {
                if (rdnElement.HasValue)
                {
                    RdnElement jElement = rdnElement.Value;
                    Debug.Assert(jElement.ValueKind == RdnValueKind.Array);

                    list = new List<RdnNode?>(jElement.GetArrayLength());

                    foreach (RdnElement element in jElement.EnumerateArray())
                    {
                        RdnNode? node = RdnNodeConverter.Create(element, Options);
                        node?.AssignParent(this);
                        list.Add(node);
                    }
                }
                else
                {
                    list = new();
                }

                // Ensure _rdnElement is written to after _list
                _list = list;
                Interlocked.MemoryBarrier();
                _rdnElement = null;
            }

            return list;
        }

        /// <summary>
        /// Provides a coherent view of the underlying representation of the current node.
        /// The rdnElement value should be consumed if and only if the list value is null.
        /// </summary>
        private void GetUnderlyingRepresentation(out List<RdnNode?>? list, out RdnElement? rdnElement)
        {
            // Because RdnElement cannot be read atomically there might be torn reads,
            // however the order of read/write operations guarantees that that's only
            // possible if the value of _list is non-null.
            rdnElement = _rdnElement;
            Interlocked.MemoryBarrier();
            list = _list;
        }

        [ExcludeFromCodeCoverage] // Justification = "Design-time"
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly RdnArray _node;

            public DebugView(RdnArray node)
            {
                _node = node;
            }

            public string Rdn => _node.ToRdnString();
            public string Path => _node.GetPath();

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            private DebugViewItem[] Items
            {
                get
                {
                    DebugViewItem[] properties = new DebugViewItem[_node.List.Count];

                    for (int i = 0; i < _node.List.Count; i++)
                    {
                        properties[i].Value = _node.List[i];
                    }

                    return properties;
                }
            }

            [DebuggerDisplay("{Display,nq}")]
            private struct DebugViewItem
            {
                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public RdnNode? Value;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public string Display
                {
                    get
                    {
                        if (Value == null)
                        {
                            return $"null";
                        }

                        if (Value is RdnValue)
                        {
                            return Value.ToRdnString();
                        }

                        if (Value is RdnObject rdnObject)
                        {
                            return $"RdnObject[{rdnObject.Count}]";
                        }

                        if (Value is RdnSet rdnSet)
                        {
                            return $"RdnSet[{rdnSet.Count}]";
                        }

                        if (Value is RdnMap rdnMap)
                        {
                            return $"RdnMap[{rdnMap.Count}]";
                        }

                        RdnArray rdnArray = (RdnArray)Value;
                        return $"RdnArray[{rdnArray.List.Count}]";
                    }
                }
            }
        }
    }
}
