// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rdn.Nodes
{
    /// <summary>
    ///   Represents a mutable RDN object.
    /// </summary>
    /// <remarks>
    /// It's safe to perform multiple concurrent read operations on a <see cref="RdnObject"/>,
    /// but issues can occur if the collection is modified while it's being read.
    /// </remarks>
    [DebuggerDisplay("RdnObject[{Count}]")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed partial class RdnObject : RdnNode
    {
        private RdnElement? _rdnElement;

        internal override RdnElement? UnderlyingElement => _rdnElement;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnObject"/> class that is empty.
        /// </summary>
        /// <param name="options">Options to control the behavior.</param>
        public RdnObject(RdnNodeOptions? options = null) : base(options) { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnObject"/> class that contains the specified <paramref name="properties"/>.
        /// </summary>
        /// <param name="properties">The properties to be added.</param>
        /// <param name="options">Options to control the behavior.</param>
        public RdnObject(IEnumerable<KeyValuePair<string, RdnNode?>> properties, RdnNodeOptions? options = null) : this(options)
        {
            int capacity = properties is ICollection<KeyValuePair<string, RdnNode?>> propertiesCollection ? propertiesCollection.Count : 0;
            OrderedDictionary<string, RdnNode?> dictionary = CreateDictionary(options, capacity);

            foreach (KeyValuePair<string, RdnNode?> node in properties)
            {
                dictionary.Add(node.Key, node.Value);
                node.Value?.AssignParent(this);
            }

            _dictionary = dictionary;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RdnObject"/> class that contains properties from the specified <see cref="RdnElement"/>.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="RdnObject"/> class that contains properties from the specified <see cref="RdnElement"/>.
        /// </returns>
        /// <param name="element">The <see cref="RdnElement"/>.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>A <see cref="RdnObject"/>.</returns>
        public static RdnObject? Create(RdnElement element, RdnNodeOptions? options = null)
        {
            return element.ValueKind switch
            {
                RdnValueKind.Null => null,
                RdnValueKind.Object => new RdnObject(element, options),
                _ => throw new InvalidOperationException(SR.Format(SR.NodeElementWrongType, nameof(RdnValueKind.Object)))
            };
        }

        internal RdnObject(RdnElement element, RdnNodeOptions? options = null) : this(options)
        {
            Debug.Assert(element.ValueKind == RdnValueKind.Object);
            _rdnElement = element;
        }

        /// <summary>
        /// Gets or creates the underlying dictionary containing the properties of the object.
        /// </summary>
        private OrderedDictionary<string, RdnNode?> Dictionary => _dictionary ?? InitializeDictionary();

        private protected override RdnNode? GetItem(int index) => GetAt(index).Value;
        private protected override void SetItem(int index, RdnNode? value) => SetAt(index, value);

        internal override RdnNode DeepCloneCore()
        {
            GetUnderlyingRepresentation(out OrderedDictionary<string, RdnNode?>? dictionary, out RdnElement? rdnElement);

            if (dictionary is null)
            {
                return rdnElement.HasValue
                    ? new RdnObject(rdnElement.Value.Clone(), Options)
                    : new RdnObject(Options);
            }

            var jObject = new RdnObject(Options)
            {
                _dictionary = CreateDictionary(Options, Count)
            };

            foreach (KeyValuePair<string, RdnNode?> item in dictionary)
            {
                jObject.Add(item.Key, item.Value?.DeepCloneCore());
            }

            return jObject;
        }

        internal string GetPropertyName(RdnNode? node)
        {
            KeyValuePair<string, RdnNode?>? item = FindValue(node);
            return item.HasValue ? item.Value.Key : string.Empty;
        }

        /// <summary>
        ///   Returns the value of a property with the specified name.
        /// </summary>
        /// <param name="propertyName">The name of the property to return.</param>
        /// <param name="rdnNode">The RDN value of the property with the specified name.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <returns>
        ///   <see langword="true"/> if a property with the specified name was found; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetPropertyValue(string propertyName, out RdnNode? rdnNode) => TryGetPropertyValue(propertyName, out rdnNode, out _);

        /// <summary>
        ///   Gets the value associated with the specified property name.
        /// </summary>
        /// <param name="propertyName">The property name of the value to get.</param>
        /// <param name="rdnNode">
        ///   When this method returns, it contains the value associated with the specified property name, if the property name is found;
        ///   otherwise <see langword="null"/>.
        /// </param>
        /// <param name="index">The index of <paramref name="propertyName"/> if found; otherwise, -1.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="RdnObject"/> contains an element with the specified property name; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetPropertyValue(string propertyName, out RdnNode? rdnNode, out int index)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

#if NET9_0
            index = Dictionary.IndexOf(propertyName);
            if (index < 0)
            {
                rdnNode = null;
                return false;
            }

            rdnNode = Dictionary.GetAt(index).Value;
            return true;
#else
            return Dictionary.TryGetValue(propertyName, out rdnNode, out index);
#endif
        }

        /// <inheritdoc/>
        public override void WriteTo(Utf8RdnWriter writer, RdnSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(writer);

            GetUnderlyingRepresentation(out OrderedDictionary<string, RdnNode?>? dictionary, out RdnElement? rdnElement);

            if (dictionary is null && rdnElement.HasValue)
            {
                // Write the element without converting to nodes.
                rdnElement.Value.WriteTo(writer);
            }
            else
            {
                writer.WriteStartObject();
                WriteContentsTo(writer, options);
                writer.WriteEndObject();
            }
        }

        /// <summary>
        /// Writes the properties of this RdnObject to the writer without the surrounding braces.
        /// This is used for extension data serialization where the properties should be flattened
        /// into the parent object.
        /// </summary>
        internal void WriteContentsTo(Utf8RdnWriter writer, RdnSerializerOptions? options)
        {
            GetUnderlyingRepresentation(out OrderedDictionary<string, RdnNode?>? dictionary, out RdnElement? rdnElement);

            if (dictionary is null && rdnElement.HasValue)
            {
                // Write properties from the underlying RdnElement without converting to nodes.
                foreach (RdnProperty property in rdnElement.Value.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
            }
            else
            {
                foreach (KeyValuePair<string, RdnNode?> entry in Dictionary)
                {
                    writer.WritePropertyName(entry.Key);

                    if (entry.Value is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        entry.Value.WriteTo(writer, options);
                    }
                }
            }
        }

        private protected override RdnValueKind GetValueKindCore() => RdnValueKind.Object;

        internal override bool DeepEqualsCore(RdnNode node)
        {
            switch (node)
            {
                case RdnArray:
                case RdnMap:
                    return false;
                case RdnValue value:
                    // RdnValue instances have special comparison semantics, dispatch to their implementation.
                    return value.DeepEqualsCore(this);
                case RdnObject rdnObject:
                    OrderedDictionary<string, RdnNode?> currentDict = Dictionary;
                    OrderedDictionary<string, RdnNode?> otherDict = rdnObject.Dictionary;

                    if (currentDict.Count != otherDict.Count)
                    {
                        return false;
                    }

                    foreach (KeyValuePair<string, RdnNode?> item in currentDict)
                    {
                        if (!otherDict.TryGetValue(item.Key, out RdnNode? rdnNode) || !DeepEquals(item.Value, rdnNode))
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

        internal RdnNode? GetItem(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (TryGetPropertyValue(propertyName, out RdnNode? value))
            {
                return value;
            }

            // Return null for missing properties.
            return null;
        }

        internal override void GetPath(ref ValueStringBuilder path, RdnNode? child)
        {
            Parent?.GetPath(ref path, this);

            if (child != null)
            {
                string propertyName = FindValue(child)!.Value.Key;
                if (propertyName.AsSpan().ContainsSpecialCharacters())
                {
                    path.Append("['");
                    path.AppendEscapedPropertyName(propertyName);
                    path.Append("']");
                }
                else
                {
                    path.Append('.');
                    path.Append(propertyName);
                }
            }
        }

        internal void SetItem(string propertyName, RdnNode? value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            OrderedDictionary<string, RdnNode?> dict = Dictionary;

            if (
#if NET9_0
                !dict.TryAdd(propertyName, value)
#else
                !dict.TryAdd(propertyName, value, out int index)
#endif
                )
            {
#if NET9_0
                int index = dict.IndexOf(propertyName);
#endif
                Debug.Assert(index >= 0);
                RdnNode? replacedValue = dict.GetAt(index).Value;

                if (ReferenceEquals(value, replacedValue))
                {
                    return;
                }

                DetachParent(replacedValue);
                dict.SetAt(index, value);
            }

            value?.AssignParent(this);
        }

        private void DetachParent(RdnNode? item)
        {
            Debug.Assert(_dictionary != null, "Cannot have detachable nodes without a materialized dictionary.");

            if (item != null) item.Parent = null;
        }

        private KeyValuePair<string, RdnNode?>? FindValue(RdnNode? value)
        {
            foreach (KeyValuePair<string, RdnNode?> item in Dictionary)
            {
                if (ReferenceEquals(item.Value, value))
                {
                    return item;
                }
            }

            return null;
        }

        [ExcludeFromCodeCoverage] // Justification = "Design-time"
        private sealed class DebugView
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly RdnObject _node;

            public DebugView(RdnObject node)
            {
                _node = node;
            }

            public string Rdn => _node.ToRdnString();
            public string Path => _node.GetPath();

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            private DebugViewProperty[] Items
            {
                get
                {
                    DebugViewProperty[] properties = new DebugViewProperty[_node.Count];

                    int i = 0;
                    foreach (KeyValuePair<string, RdnNode?> item in _node)
                    {
                        properties[i].PropertyName = item.Key;
                        properties[i].Value = item.Value;
                        i++;
                    }

                    return properties;
                }
            }

            [DebuggerDisplay("{Display,nq}")]
            private struct DebugViewProperty
            {
                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public RdnNode? Value;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public string PropertyName;

                [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                public string Display
                {
                    get
                    {
                        if (Value == null)
                        {
                            return $"{PropertyName} = null";
                        }

                        if (Value is RdnValue)
                        {
                            return $"{PropertyName} = {Value.ToRdnString()}";
                        }

                        if (Value is RdnObject rdnObject)
                        {
                            return $"{PropertyName} = RdnObject[{rdnObject.Count}]";
                        }

                        if (Value is RdnSet rdnSet)
                        {
                            return $"{PropertyName} = RdnSet[{rdnSet.Count}]";
                        }

                        if (Value is RdnMap rdnMap)
                        {
                            return $"{PropertyName} = RdnMap[{rdnMap.Count}]";
                        }

                        RdnArray rdnArray = (RdnArray)Value;
                        return $"{PropertyName} = RdnArray[{rdnArray.Count}]";
                    }
                }

            }
        }
    }
}
