// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rdn.Serialization.Converters;
using System.Threading;

namespace Rdn.Nodes
{
    /// <summary>
    ///   Represents a mutable RDN Set (an unordered collection of values).
    /// </summary>
    /// <remarks>
    /// It is safe to perform multiple concurrent read operations on a <see cref="RdnSet"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("RdnSet[{List.Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class RdnSet : RdnNode, ICollection<RdnNode?>
    {
        private RdnElement? _rdnElement;
        private List<RdnNode?>? _list;

        internal override RdnElement? UnderlyingElement => _rdnElement;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnSet"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public RdnSet(RdnNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnSet"/> class that contains items from the specified params array.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="items">The items to add to the new <see cref="RdnSet"/>.</param>
        public RdnSet(RdnNodeOptions options, params RdnNode?[] items) : base(options)
        {
            InitializeFromArray(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnSet"/> class that contains items from the specified array.
        /// </summary>
        /// <param name="items">The items to add to the new <see cref="RdnSet"/>.</param>
        public RdnSet(params RdnNode?[] items) : base()
        {
            InitializeFromArray(items);
        }

        private protected override RdnValueKind GetValueKindCore() => RdnValueKind.Set;

        internal override RdnNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out List<RdnNode?>? list, out RdnElement? rdnElement);

            if (list is null)
            {
                return rdnElement.HasValue
                    ? new RdnSet(rdnElement.Value.Clone(), Options)
                    : new RdnSet(Options);
            }

            var rdnSet = new RdnSet(Options) { _list = new List<RdnNode?>(list.Count) };

            for (int i = 0; i < list.Count; i++)
            {
                rdnSet.Add(list[i]?.DeepCloneCore());
            }

            return rdnSet;
        }

        internal override bool DeepEqualsCore(RdnNode node)
        {
            switch (node)
            {
                case RdnObject:
                case RdnArray:
                case RdnMap:
                    return false;
                case RdnValue value:
                    return value.DeepEqualsCore(this);
                case RdnSet set:
                    List<RdnNode?> currentList = List;
                    List<RdnNode?> otherList = set.List;

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

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnSet"/> class that contains items from the specified <see cref="RdnElement"/>.
        /// </summary>
        public static RdnSet? Create(RdnElement element, RdnNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                RdnValueKind.Null => null,
                RdnValueKind.Set => new RdnSet(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(RdnValueKind.Set))),
            };
        }

        internal RdnSet(RdnElement element, RdnNodeOptions? options = null) : base(options)
        {
            Debug.Assert(element.ValueKind == RdnValueKind.Set);
            _rdnElement = element;
        }

        /// <summary>
        ///   Gets the number of elements contained in the <see cref="RdnSet"/>.
        /// </summary>
        public int Count => List.Count;

        /// <summary>
        ///   Returns <see langword="false"/>.
        /// </summary>
        bool ICollection<RdnNode?>.IsReadOnly => false;

        /// <summary>
        ///   Adds a <see cref="RdnNode"/> to the <see cref="RdnSet"/>.
        /// </summary>
        public void Add(RdnNode? item)
        {
            item?.AssignParent(this);
            List.Add(item);
        }

        /// <summary>
        ///   Adds an object to the <see cref="RdnSet"/>.
        /// </summary>
        [RequiresUnreferencedCode(RdnValue.CreateUnreferencedCodeMessage)]
        [RequiresDynamicCode(RdnValue.CreateDynamicCodeMessage)]
        public void Add<T>(T? value)
        {
            RdnNode? nodeToAdd = ConvertFromValue(value, Options);
            Add(nodeToAdd);
        }

        /// <summary>
        ///   Removes all elements from the <see cref="RdnSet"/>.
        /// </summary>
        public void Clear()
        {
            List<RdnNode?>? list = _list;

            if (list is null)
            {
                _rdnElement = null;
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    DetachParent(list[i]);
                }

                list.Clear();
            }
        }

        /// <summary>
        ///   Determines whether an element is in the <see cref="RdnSet"/>.
        /// </summary>
        public bool Contains(RdnNode? item) => List.Contains(item);

        /// <summary>
        ///   Copies the elements of the <see cref="RdnSet"/> to an Array.
        /// </summary>
        void ICollection<RdnNode?>.CopyTo(RdnNode?[] array, int arrayIndex) => List.CopyTo(array, arrayIndex);

        /// <summary>
        ///   Removes the first occurrence of a specific <see cref="RdnNode"/> from the <see cref="RdnSet"/>.
        /// </summary>
        public bool Remove(RdnNode? item)
        {
            if (List.Remove(item))
            {
                DetachParent(item);
                return true;
            }

            return false;
        }

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="RdnSet"/>.
        /// </summary>
        public IEnumerator<RdnNode?> GetEnumerator() => List.GetEnumerator();

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="RdnSet"/>.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)List).GetEnumerator();

        /// <summary>
        /// Gets or creates the underlying list containing the element nodes of the set.
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
                writer.WriteStartSet(forceTypeName: List.Count == 0);

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

                writer.WriteEndSet();
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

        private List<RdnNode?> InitializeList()
        {
            GetUnderlyingRepresentation(out List<RdnNode?>? list, out RdnElement? rdnElement);

            if (list is null)
            {
                if (rdnElement.HasValue)
                {
                    RdnElement jElement = rdnElement.Value;
                    Debug.Assert(jElement.ValueKind == RdnValueKind.Set);

                    list = new List<RdnNode?>(jElement.GetArrayLength());

                    foreach (RdnElement element in jElement.EnumerateSet())
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

                _list = list;
                Interlocked.MemoryBarrier();
                _rdnElement = null;
            }

            return list;
        }

        private void GetUnderlyingRepresentation(out List<RdnNode?>? list, out RdnElement? rdnElement)
        {
            rdnElement = _rdnElement;
            Interlocked.MemoryBarrier();
            list = _list;
        }

        private static void DetachParent(RdnNode? item)
        {
            if (item != null) item.Parent = null;
        }

        [ExcludeFromCodeCoverage]
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly RdnSet _node;

            public DebugView(RdnSet node)
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
                        if (Value == null) return "null";
                        if (Value is RdnValue) return Value.ToRdnString();
                        if (Value is RdnObject rdnObject) return $"RdnObject[{rdnObject.Count}]";
                        if (Value is RdnSet rdnSet) return $"RdnSet[{rdnSet.Count}]";
                        if (Value is RdnMap rdnMap) return $"RdnMap[{rdnMap.Count}]";
                        RdnArray rdnArray = (RdnArray)Value;
                        return $"RdnArray[{rdnArray.Count}]";
                    }
                }
            }
        }
    }
}
