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
    /// It is safe to perform multiple concurrent read operations on a <see cref="JsonSet"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("JsonSet[{List.Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class JsonSet : JsonNode, ICollection<JsonNode?>
    {
        private JsonElement? _jsonElement;
        private List<JsonNode?>? _list;

        internal override JsonElement? UnderlyingElement => _jsonElement;

        /// <summary>
        ///   Initializes a new instance of the <see cref="JsonSet"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public JsonSet(JsonNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="JsonSet"/> class that contains items from the specified params array.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        /// <param name="items">The items to add to the new <see cref="JsonSet"/>.</param>
        public JsonSet(JsonNodeOptions options, params JsonNode?[] items) : base(options)
        {
            InitializeFromArray(items);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="JsonSet"/> class that contains items from the specified array.
        /// </summary>
        /// <param name="items">The items to add to the new <see cref="JsonSet"/>.</param>
        public JsonSet(params JsonNode?[] items) : base()
        {
            InitializeFromArray(items);
        }

        private protected override JsonValueKind GetValueKindCore() => JsonValueKind.Set;

        internal override JsonNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out List<JsonNode?>? list, out JsonElement? jsonElement);

            if (list is null)
            {
                return jsonElement.HasValue
                    ? new JsonSet(jsonElement.Value.Clone(), Options)
                    : new JsonSet(Options);
            }

            var jsonSet = new JsonSet(Options) { _list = new List<JsonNode?>(list.Count) };

            for (int i = 0; i < list.Count; i++)
            {
                jsonSet.Add(list[i]?.DeepCloneCore());
            }

            return jsonSet;
        }

        internal override bool DeepEqualsCore(JsonNode node)
        {
            switch (node)
            {
                case JsonObject:
                case JsonArray:
                case JsonMap:
                    return false;
                case JsonValue value:
                    return value.DeepEqualsCore(this);
                case JsonSet set:
                    List<JsonNode?> currentList = List;
                    List<JsonNode?> otherList = set.List;

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
        ///   Initializes a new instance of the <see cref="JsonSet"/> class that contains items from the specified <see cref="JsonElement"/>.
        /// </summary>
        public static JsonSet? Create(JsonElement element, JsonNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Set => new JsonSet(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(JsonValueKind.Set))),
            };
        }

        internal JsonSet(JsonElement element, JsonNodeOptions? options = null) : base(options)
        {
            Debug.Assert(element.ValueKind == JsonValueKind.Set);
            _jsonElement = element;
        }

        /// <summary>
        ///   Gets the number of elements contained in the <see cref="JsonSet"/>.
        /// </summary>
        public int Count => List.Count;

        /// <summary>
        ///   Returns <see langword="false"/>.
        /// </summary>
        bool ICollection<JsonNode?>.IsReadOnly => false;

        /// <summary>
        ///   Adds a <see cref="JsonNode"/> to the <see cref="JsonSet"/>.
        /// </summary>
        public void Add(JsonNode? item)
        {
            item?.AssignParent(this);
            List.Add(item);
        }

        /// <summary>
        ///   Adds an object to the <see cref="JsonSet"/>.
        /// </summary>
        [RequiresUnreferencedCode(JsonValue.CreateUnreferencedCodeMessage)]
        [RequiresDynamicCode(JsonValue.CreateDynamicCodeMessage)]
        public void Add<T>(T? value)
        {
            JsonNode? nodeToAdd = ConvertFromValue(value, Options);
            Add(nodeToAdd);
        }

        /// <summary>
        ///   Removes all elements from the <see cref="JsonSet"/>.
        /// </summary>
        public void Clear()
        {
            List<JsonNode?>? list = _list;

            if (list is null)
            {
                _jsonElement = null;
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
        ///   Determines whether an element is in the <see cref="JsonSet"/>.
        /// </summary>
        public bool Contains(JsonNode? item) => List.Contains(item);

        /// <summary>
        ///   Copies the elements of the <see cref="JsonSet"/> to an Array.
        /// </summary>
        void ICollection<JsonNode?>.CopyTo(JsonNode?[] array, int arrayIndex) => List.CopyTo(array, arrayIndex);

        /// <summary>
        ///   Removes the first occurrence of a specific <see cref="JsonNode"/> from the <see cref="JsonSet"/>.
        /// </summary>
        public bool Remove(JsonNode? item)
        {
            if (List.Remove(item))
            {
                DetachParent(item);
                return true;
            }

            return false;
        }

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="JsonSet"/>.
        /// </summary>
        public IEnumerator<JsonNode?> GetEnumerator() => List.GetEnumerator();

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="JsonSet"/>.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)List).GetEnumerator();

        /// <summary>
        /// Gets or creates the underlying list containing the element nodes of the set.
        /// </summary>
        private List<JsonNode?> List => _list ?? InitializeList();

        private protected override JsonNode? GetItem(int index)
        {
            return List[index];
        }

        private protected override void SetItem(int index, JsonNode? value)
        {
            value?.AssignParent(this);
            DetachParent(List[index]);
            List[index] = value;
        }

        internal override void GetPath(ref ValueStringBuilder path, JsonNode? child)
        {
            Parent?.GetPath(ref path, this);

            if (child != null)
            {
                int index = List.IndexOf(child);
                Debug.Assert(index >= 0);

                path.Append('[');
#if NET
                Span<char> chars = stackalloc char[JsonConstants.MaximumFormatUInt32Length];
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
        public override void WriteTo(Utf8JsonWriter writer, JsonSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            GetUnderlyingRepresentation(out List<JsonNode?>? list, out JsonElement? jsonElement);

            if (list is null && jsonElement.HasValue)
            {
                jsonElement.Value.WriteTo(writer);
            }
            else
            {
                writer.WriteStartSet(forceTypeName: List.Count == 0);

                foreach (JsonNode? element in List)
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

        private void InitializeFromArray(JsonNode?[] items)
        {
            var list = new List<JsonNode?>(items);

            for (int i = 0; i < list.Count; i++)
            {
                list[i]?.AssignParent(this);
            }

            _list = list;
        }

        private List<JsonNode?> InitializeList()
        {
            GetUnderlyingRepresentation(out List<JsonNode?>? list, out JsonElement? jsonElement);

            if (list is null)
            {
                if (jsonElement.HasValue)
                {
                    JsonElement jElement = jsonElement.Value;
                    Debug.Assert(jElement.ValueKind == JsonValueKind.Set);

                    list = new List<JsonNode?>(jElement.GetArrayLength());

                    foreach (JsonElement element in jElement.EnumerateSet())
                    {
                        JsonNode? node = JsonNodeConverter.Create(element, Options);
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
                _jsonElement = null;
            }

            return list;
        }

        private void GetUnderlyingRepresentation(out List<JsonNode?>? list, out JsonElement? jsonElement)
        {
            jsonElement = _jsonElement;
            Interlocked.MemoryBarrier();
            list = _list;
        }

        private static void DetachParent(JsonNode? item)
        {
            if (item != null) item.Parent = null;
        }

        [ExcludeFromCodeCoverage]
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly JsonSet _node;

            public DebugView(JsonSet node)
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
                public JsonNode? Value;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public string Display
                {
                    get
                    {
                        if (Value == null) return "null";
                        if (Value is JsonValue) return Value.ToJsonString();
                        if (Value is JsonObject jsonObject) return $"JsonObject[{jsonObject.Count}]";
                        if (Value is JsonSet jsonSet) return $"JsonSet[{jsonSet.Count}]";
                        if (Value is JsonMap jsonMap) return $"JsonMap[{jsonMap.Count}]";
                        JsonArray jsonArray = (JsonArray)Value;
                        return $"JsonArray[{jsonArray.Count}]";
                    }
                }
            }
        }
    }
}
