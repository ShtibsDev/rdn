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
    /// It is safe to perform multiple concurrent read operations on a <see cref="JsonMap"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("JsonMap[{Entries.Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class JsonMap : JsonNode, ICollection<KeyValuePair<JsonNode?, JsonNode?>>
    {
        private JsonElement? _jsonElement;
        private List<KeyValuePair<JsonNode?, JsonNode?>>? _entries;

        internal override JsonElement? UnderlyingElement => _jsonElement;

        /// <summary>
        ///   Initializes a new instance of the <see cref="JsonMap"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public JsonMap(JsonNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="JsonMap"/> class that contains entries from the specified params array.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="entries">The entries to add to the new <see cref="JsonMap"/>.</param>
        public JsonMap(JsonNodeOptions options, params KeyValuePair<JsonNode?, JsonNode?>[] entries) : base(options)
        {
            InitializeFromArray(entries);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="JsonMap"/> class that contains entries from the specified array.
        /// </summary>
        /// <param name="entries">The entries to add to the new <see cref="JsonMap"/>.</param>
        public JsonMap(params KeyValuePair<JsonNode?, JsonNode?>[] entries) : base()
        {
            InitializeFromArray(entries);
        }

        private protected override JsonValueKind GetValueKindCore() => JsonValueKind.Map;

        internal override JsonNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out List<KeyValuePair<JsonNode?, JsonNode?>>? entries, out JsonElement? jsonElement);

            if (entries is null)
            {
                return jsonElement.HasValue
                    ? new JsonMap(jsonElement.Value.Clone(), Options)
                    : new JsonMap(Options);
            }

            var jsonMap = new JsonMap(Options) { _entries = new List<KeyValuePair<JsonNode?, JsonNode?>>(entries.Count) };

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                jsonMap.Add(entry.Key?.DeepCloneCore(), entry.Value?.DeepCloneCore());
            }

            return jsonMap;
        }

        internal override bool DeepEqualsCore(JsonNode node)
        {
            switch (node)
            {
                case JsonObject:
                case JsonArray:
                case JsonSet:
                    return false;
                case JsonValue value:
                    return value.DeepEqualsCore(this);
                case JsonMap map:
                    List<KeyValuePair<JsonNode?, JsonNode?>> currentEntries = Entries;
                    List<KeyValuePair<JsonNode?, JsonNode?>> otherEntries = map.Entries;

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
        ///   Initializes a new instance of the <see cref="JsonMap"/> class that contains entries from the specified <see cref="JsonElement"/>.
        /// </summary>
        public static JsonMap? Create(JsonElement element, JsonNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Map => new JsonMap(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(JsonValueKind.Map))),
            };
        }

        internal JsonMap(JsonElement element, JsonNodeOptions? options = null) : base(options)
        {
            Debug.Assert(element.ValueKind == JsonValueKind.Map);
            _jsonElement = element;
        }

        /// <summary>
        ///   Gets the number of key-value pairs contained in the <see cref="JsonMap"/>.
        /// </summary>
        public int Count => Entries.Count;

        /// <summary>
        ///   Returns <see langword="false"/>.
        /// </summary>
        bool ICollection<KeyValuePair<JsonNode?, JsonNode?>>.IsReadOnly => false;

        /// <summary>
        ///   Adds a key-value pair to the <see cref="JsonMap"/>.
        /// </summary>
        public void Add(JsonNode? key, JsonNode? value)
        {
            key?.AssignParent(this);
            value?.AssignParent(this);
            Entries.Add(new KeyValuePair<JsonNode?, JsonNode?>(key, value));
        }

        /// <summary>
        ///   Adds a key-value pair to the <see cref="JsonMap"/>.
        /// </summary>
        public void Add(KeyValuePair<JsonNode?, JsonNode?> entry)
        {
            Add(entry.Key, entry.Value);
        }

        /// <summary>
        ///   Removes all entries from the <see cref="JsonMap"/>.
        /// </summary>
        public void Clear()
        {
            List<KeyValuePair<JsonNode?, JsonNode?>>? entries = _entries;

            if (entries is null)
            {
                _jsonElement = null;
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
        ///   Determines whether a key-value pair is in the <see cref="JsonMap"/>.
        /// </summary>
        public bool Contains(KeyValuePair<JsonNode?, JsonNode?> item) => Entries.Contains(item);

        /// <summary>
        ///   Copies the entries of the <see cref="JsonMap"/> to an Array.
        /// </summary>
        void ICollection<KeyValuePair<JsonNode?, JsonNode?>>.CopyTo(KeyValuePair<JsonNode?, JsonNode?>[] array, int arrayIndex) => Entries.CopyTo(array, arrayIndex);

        /// <summary>
        ///   Removes the first occurrence of a specific entry from the <see cref="JsonMap"/>.
        /// </summary>
        public bool Remove(KeyValuePair<JsonNode?, JsonNode?> item)
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
        ///   Returns an enumerator that iterates through the <see cref="JsonMap"/>.
        /// </summary>
        public IEnumerator<KeyValuePair<JsonNode?, JsonNode?>> GetEnumerator() => Entries.GetEnumerator();

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="JsonMap"/>.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Entries).GetEnumerator();

        /// <summary>
        /// Gets or creates the underlying list containing the entries of the map.
        /// </summary>
        private List<KeyValuePair<JsonNode?, JsonNode?>> Entries => _entries ?? InitializeEntries();

        private protected override JsonNode? GetItem(int index)
        {
            // For flat access (used by path navigation), treat entries as interleaved key, value
            var entries = Entries;
            int entryIndex = index / 2;
            return (index % 2 == 0) ? entries[entryIndex].Key : entries[entryIndex].Value;
        }

        private protected override void SetItem(int index, JsonNode? value)
        {
            value?.AssignParent(this);
            var entries = Entries;
            int entryIndex = index / 2;
            if (index % 2 == 0)
            {
                DetachParent(entries[entryIndex].Key);
                entries[entryIndex] = new KeyValuePair<JsonNode?, JsonNode?>(value, entries[entryIndex].Value);
            }
            else
            {
                DetachParent(entries[entryIndex].Value);
                entries[entryIndex] = new KeyValuePair<JsonNode?, JsonNode?>(entries[entryIndex].Key, value);
            }
        }

        internal override void GetPath(ref ValueStringBuilder path, JsonNode? child)
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
                        Span<char> chars = stackalloc char[JsonConstants.MaximumFormatUInt32Length];
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
                        Span<char> chars = stackalloc char[JsonConstants.MaximumFormatUInt32Length];
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
        public override void WriteTo(Utf8JsonWriter writer, JsonSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            GetUnderlyingRepresentation(out List<KeyValuePair<JsonNode?, JsonNode?>>? entries, out JsonElement? jsonElement);

            if (entries is null && jsonElement.HasValue)
            {
                jsonElement.Value.WriteTo(writer);
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

        private void InitializeFromArray(KeyValuePair<JsonNode?, JsonNode?>[] entries)
        {
            var list = new List<KeyValuePair<JsonNode?, JsonNode?>>(entries);

            for (int i = 0; i < list.Count; i++)
            {
                list[i].Key?.AssignParent(this);
                list[i].Value?.AssignParent(this);
            }

            _entries = list;
        }

        private List<KeyValuePair<JsonNode?, JsonNode?>> InitializeEntries()
        {
            GetUnderlyingRepresentation(out List<KeyValuePair<JsonNode?, JsonNode?>>? entries, out JsonElement? jsonElement);

            if (entries is null)
            {
                if (jsonElement.HasValue)
                {
                    JsonElement jElement = jsonElement.Value;
                    Debug.Assert(jElement.ValueKind == JsonValueKind.Map);

                    int itemCount = jElement.GetArrayLength();
                    entries = new List<KeyValuePair<JsonNode?, JsonNode?>>(itemCount / 2);

                    var enumerator = jElement.EnumerateMap();
                    while (enumerator.MoveNext())
                    {
                        JsonNode? key = JsonNodeConverter.Create(enumerator.Current, Options);
                        key?.AssignParent(this);

                        if (!enumerator.MoveNext())
                        {
                            Debug.Fail("Map should have even number of items (key-value pairs)");
                            break;
                        }

                        JsonNode? value = JsonNodeConverter.Create(enumerator.Current, Options);
                        value?.AssignParent(this);

                        entries.Add(new KeyValuePair<JsonNode?, JsonNode?>(key, value));
                    }
                }
                else
                {
                    entries = new();
                }

                _entries = entries;
                Interlocked.MemoryBarrier();
                _jsonElement = null;
            }

            return entries;
        }

        private void GetUnderlyingRepresentation(out List<KeyValuePair<JsonNode?, JsonNode?>>? entries, out JsonElement? jsonElement)
        {
            jsonElement = _jsonElement;
            Interlocked.MemoryBarrier();
            entries = _entries;
        }

        private static void DetachParent(JsonNode? item)
        {
            if (item != null) item.Parent = null;
        }

        [ExcludeFromCodeCoverage]
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly JsonMap _node;

            public DebugView(JsonMap node)
            {
                _node = node;
            }

            public string Json => _node.ToJsonString();
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
                public JsonNode? Key;

                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public JsonNode? Value;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public readonly string Display
                {
                    get
                    {
                        string keyStr = Key == null ? "null" : Key is JsonValue ? Key.ToJsonString() : "...";
                        string valStr = Value == null ? "null" : Value is JsonValue ? Value.ToJsonString() : "...";
                        return $"{keyStr} => {valStr}";
                    }
                }
            }
        }
    }
}
