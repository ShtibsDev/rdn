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
    ///   Represents a mutable RDN Map (an ordered collection of key-value pairs where keys can be any RDN value type).
    /// </summary>
    /// <remarks>
    /// It is safe to perform multiple concurrent read operations on a <see cref="RdnMap"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("RdnMap[{Entries.Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class RdnMap : RdnNode, ICollection<KeyValuePair<RdnNode?, RdnNode?>>
    {
        private RdnElement? _rdnElement;
        private List<KeyValuePair<RdnNode?, RdnNode?>>? _entries;

        internal override RdnElement? UnderlyingElement => _rdnElement;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnMap"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public RdnMap(RdnNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnMap"/> class that contains entries from the specified params array.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="entries">The entries to add to the new <see cref="RdnMap"/>.</param>
        public RdnMap(RdnNodeOptions options, params KeyValuePair<RdnNode?, RdnNode?>[] entries) : base(options)
        {
            InitializeFromArray(entries);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnMap"/> class that contains entries from the specified array.
        /// </summary>
        /// <param name="entries">The entries to add to the new <see cref="RdnMap"/>.</param>
        public RdnMap(params KeyValuePair<RdnNode?, RdnNode?>[] entries) : base()
        {
            InitializeFromArray(entries);
        }

        private protected override RdnValueKind GetValueKindCore() => RdnValueKind.Map;

        internal override RdnNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out List<KeyValuePair<RdnNode?, RdnNode?>>? entries, out RdnElement? rdnElement);

            if (entries is null)
            {
                return rdnElement.HasValue
                    ? new RdnMap(rdnElement.Value.Clone(), Options)
                    : new RdnMap(Options);
            }

            var rdnMap = new RdnMap(Options) { _entries = new List<KeyValuePair<RdnNode?, RdnNode?>>(entries.Count) };

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                rdnMap.Add(entry.Key?.DeepCloneCore(), entry.Value?.DeepCloneCore());
            }

            return rdnMap;
        }

        internal override bool DeepEqualsCore(RdnNode node)
        {
            switch (node)
            {
                case RdnObject:
                case RdnArray:
                case RdnSet:
                    return false;
                case RdnValue value:
                    return value.DeepEqualsCore(this);
                case RdnMap map:
                    List<KeyValuePair<RdnNode?, RdnNode?>> currentEntries = Entries;
                    List<KeyValuePair<RdnNode?, RdnNode?>> otherEntries = map.Entries;

                    if (currentEntries.Count != otherEntries.Count)
                    {
                        return false;
                    }

                    for (int i = 0; i < currentEntries.Count; i++)
                    {
                        if (!DeepEquals(currentEntries[i].Key, otherEntries[i].Key) ||
                            !DeepEquals(currentEntries[i].Value, otherEntries[i].Value))
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
        ///   Initializes a new instance of the <see cref="RdnMap"/> class that contains entries from the specified <see cref="RdnElement"/>.
        /// </summary>
        public static RdnMap? Create(RdnElement element, RdnNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                RdnValueKind.Null => null,
                RdnValueKind.Map => new RdnMap(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(RdnValueKind.Map))),
            };
        }

        internal RdnMap(RdnElement element, RdnNodeOptions? options = null) : base(options)
        {
            Debug.Assert(element.ValueKind == RdnValueKind.Map);
            _rdnElement = element;
        }

        /// <summary>
        ///   Gets the number of key-value pairs contained in the <see cref="RdnMap"/>.
        /// </summary>
        public int Count => Entries.Count;

        /// <summary>
        ///   Returns <see langword="false"/>.
        /// </summary>
        bool ICollection<KeyValuePair<RdnNode?, RdnNode?>>.IsReadOnly => false;

        /// <summary>
        ///   Adds a key-value pair to the <see cref="RdnMap"/>.
        /// </summary>
        public void Add(RdnNode? key, RdnNode? value)
        {
            key?.AssignParent(this);
            value?.AssignParent(this);
            Entries.Add(new KeyValuePair<RdnNode?, RdnNode?>(key, value));
        }

        /// <summary>
        ///   Adds a key-value pair to the <see cref="RdnMap"/>.
        /// </summary>
        public void Add(KeyValuePair<RdnNode?, RdnNode?> entry)
        {
            Add(entry.Key, entry.Value);
        }

        /// <summary>
        ///   Removes all entries from the <see cref="RdnMap"/>.
        /// </summary>
        public void Clear()
        {
            List<KeyValuePair<RdnNode?, RdnNode?>>? entries = _entries;

            if (entries is null)
            {
                _rdnElement = null;
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    DetachParent(entries[i].Key);
                    DetachParent(entries[i].Value);
                }

                entries.Clear();
            }
        }

        /// <summary>
        ///   Determines whether a key-value pair is in the <see cref="RdnMap"/>.
        /// </summary>
        public bool Contains(KeyValuePair<RdnNode?, RdnNode?> item) => Entries.Contains(item);

        /// <summary>
        ///   Copies the entries of the <see cref="RdnMap"/> to an Array.
        /// </summary>
        void ICollection<KeyValuePair<RdnNode?, RdnNode?>>.CopyTo(KeyValuePair<RdnNode?, RdnNode?>[] array, int arrayIndex) => Entries.CopyTo(array, arrayIndex);

        /// <summary>
        ///   Removes the first occurrence of a specific entry from the <see cref="RdnMap"/>.
        /// </summary>
        public bool Remove(KeyValuePair<RdnNode?, RdnNode?> item)
        {
            if (Entries.Remove(item))
            {
                DetachParent(item.Key);
                DetachParent(item.Value);
                return true;
            }

            return false;
        }

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="RdnMap"/>.
        /// </summary>
        public IEnumerator<KeyValuePair<RdnNode?, RdnNode?>> GetEnumerator() => Entries.GetEnumerator();

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="RdnMap"/>.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Entries).GetEnumerator();

        /// <summary>
        /// Gets or creates the underlying list containing the entries of the map.
        /// </summary>
        private List<KeyValuePair<RdnNode?, RdnNode?>> Entries => _entries ?? InitializeEntries();

        private protected override RdnNode? GetItem(int index)
        {
            // For flat access (used by path navigation), treat entries as interleaved key, value
            var entries = Entries;
            int entryIndex = index / 2;
            return (index % 2 == 0) ? entries[entryIndex].Key : entries[entryIndex].Value;
        }

        private protected override void SetItem(int index, RdnNode? value)
        {
            value?.AssignParent(this);
            var entries = Entries;
            int entryIndex = index / 2;
            if (index % 2 == 0)
            {
                DetachParent(entries[entryIndex].Key);
                entries[entryIndex] = new KeyValuePair<RdnNode?, RdnNode?>(value, entries[entryIndex].Value);
            }
            else
            {
                DetachParent(entries[entryIndex].Value);
                entries[entryIndex] = new KeyValuePair<RdnNode?, RdnNode?>(entries[entryIndex].Key, value);
            }
        }

        internal override void GetPath(ref ValueStringBuilder path, RdnNode? child)
        {
            Parent?.GetPath(ref path, this);

            if (child != null)
            {
                // Find the child in entries (could be a key or value)
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (ReferenceEquals(Entries[i].Key, child))
                    {
                        int flatIndex = i * 2;
                        path.Append('[');
#if NET
                        Span<char> chars = stackalloc char[RdnConstants.MaximumFormatUInt32Length];
                        bool formatted = ((uint)flatIndex).TryFormat(chars, out int charsWritten);
                        Debug.Assert(formatted);
                        path.Append(chars.Slice(0, charsWritten));
#else
                        path.Append(flatIndex.ToString());
#endif
                        path.Append(']');
                        return;
                    }

                    if (ReferenceEquals(Entries[i].Value, child))
                    {
                        int flatIndex = i * 2 + 1;
                        path.Append('[');
#if NET
                        Span<char> chars = stackalloc char[RdnConstants.MaximumFormatUInt32Length];
                        bool formatted = ((uint)flatIndex).TryFormat(chars, out int charsWritten);
                        Debug.Assert(formatted);
                        path.Append(chars.Slice(0, charsWritten));
#else
                        path.Append(flatIndex.ToString());
#endif
                        path.Append(']');
                        return;
                    }
                }

                Debug.Fail("Child not found in map entries");
            }
        }

        /// <inheritdoc/>
        public override void WriteTo(Utf8RdnWriter writer, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            GetUnderlyingRepresentation(out List<KeyValuePair<RdnNode?, RdnNode?>>? entries, out RdnElement? rdnElement);

            if (entries is null && rdnElement.HasValue)
            {
                rdnElement.Value.WriteTo(writer);
            }
            else
            {
                writer.WriteStartMap(forceTypeName: Entries.Count == 0);

                foreach (var entry in Entries)
                {
                    if (entry.Key is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        entry.Key.WriteTo(writer, options);
                    }

                    writer.WriteMapArrow();

                    if (entry.Value is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        entry.Value.WriteTo(writer, options);
                    }
                }

                writer.WriteEndMap();
            }
        }

        private void InitializeFromArray(KeyValuePair<RdnNode?, RdnNode?>[] entries)
        {
            var list = new List<KeyValuePair<RdnNode?, RdnNode?>>(entries);

            for (int i = 0; i < list.Count; i++)
            {
                list[i].Key?.AssignParent(this);
                list[i].Value?.AssignParent(this);
            }

            _entries = list;
        }

        private List<KeyValuePair<RdnNode?, RdnNode?>> InitializeEntries()
        {
            GetUnderlyingRepresentation(out List<KeyValuePair<RdnNode?, RdnNode?>>? entries, out RdnElement? rdnElement);

            if (entries is null)
            {
                if (rdnElement.HasValue)
                {
                    RdnElement jElement = rdnElement.Value;
                    Debug.Assert(jElement.ValueKind == RdnValueKind.Map);

                    int itemCount = jElement.GetArrayLength();
                    entries = new List<KeyValuePair<RdnNode?, RdnNode?>>(itemCount / 2);

                    var enumerator = jElement.EnumerateMap();
                    while (enumerator.MoveNext())
                    {
                        RdnNode? key = RdnNodeConverter.Create(enumerator.Current, Options);
                        key?.AssignParent(this);

                        if (!enumerator.MoveNext())
                        {
                            Debug.Fail("Map should have even number of items (key-value pairs)");
                            break;
                        }

                        RdnNode? value = RdnNodeConverter.Create(enumerator.Current, Options);
                        value?.AssignParent(this);

                        entries.Add(new KeyValuePair<RdnNode?, RdnNode?>(key, value));
                    }
                }
                else
                {
                    entries = new();
                }

                _entries = entries;
                Interlocked.MemoryBarrier();
                _rdnElement = null;
            }

            return entries;
        }

        private void GetUnderlyingRepresentation(out List<KeyValuePair<RdnNode?, RdnNode?>>? entries, out RdnElement? rdnElement)
        {
            rdnElement = _rdnElement;
            Interlocked.MemoryBarrier();
            entries = _entries;
        }

        private static void DetachParent(RdnNode? item)
        {
            if (item != null) item.Parent = null;
        }

        [ExcludeFromCodeCoverage]
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly RdnMap _node;

            public DebugView(RdnMap node)
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
                    DebugViewItem[] properties = new DebugViewItem[_node.Entries.Count];

                    for (int i = 0; i < _node.Entries.Count; i++)
                    {
                        properties[i].Key = _node.Entries[i].Key;
                        properties[i].Value = _node.Entries[i].Value;
                    }

                    return properties;
                }
            }

            [DebuggerDisplay("{Display,nq}")]
            private struct DebugViewItem
            {
                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public RdnNode? Key;

                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public RdnNode? Value;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public readonly string Display
                {
                    get
                    {
                        string keyStr = Key == null ? "null" : Key is RdnValue ? Key.ToRdnString() : "...";
                        string valStr = Value == null ? "null" : Value is RdnValue ? Value.ToRdnString() : "...";
                        return $"{keyStr} => {valStr}";
                    }
                }
            }
        }
    }
}
