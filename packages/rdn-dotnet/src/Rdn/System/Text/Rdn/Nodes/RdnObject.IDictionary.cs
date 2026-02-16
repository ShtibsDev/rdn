// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Rdn.Serialization.Converters;
using System.Threading;

namespace Rdn.Nodes
{
    public partial class RdnObject : IDictionary<string, RdnNode?>
    {
        private OrderedDictionary<string, RdnNode?>? _dictionary;

        /// <summary>
        ///   Adds an element with the provided property name and value to the <see cref="RdnObject"/>.
        /// </summary>
        /// <param name="propertyName">The property name of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/>is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   An element with the same property name already exists in the <see cref="RdnObject"/>.
        /// </exception>
        public void Add(string propertyName, RdnNode? value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            Dictionary.Add(propertyName, value);
            value?.AssignParent(this);
        }

        /// <summary>
        ///   Adds an element with the provided name and value to the <see cref="RdnObject"/>, if a property named <paramref name="propertyName"/> doesn't already exist.
        /// </summary>
        /// <param name="propertyName">The property name of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
        /// <returns>
        ///   <see langword="true"/> if the property didn't exist and the element was added; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAdd(string propertyName, RdnNode? value) => TryAdd(propertyName, value, out _);

        /// <summary>
        ///   Adds an element with the provided name and value to the <see cref="RdnObject"/>, if a property named <paramref name="propertyName"/> doesn't already exist.
        /// </summary>
        /// <param name="propertyName">The property name of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <param name="index">The index of the added or existing <paramref name="propertyName"/>. This is always a valid index into the <see cref="RdnObject"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
        /// <returns>
        ///   <see langword="true"/> if the property didn't exist and the element was added; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAdd(string propertyName, RdnNode? value, out int index)
        {
            if (propertyName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(propertyName));
            }
#if NET9_0
            bool success = Dictionary.TryAdd(propertyName, value);
            index = success ? Dictionary.Count - 1 : Dictionary.IndexOf(propertyName);
#else
            bool success = Dictionary.TryAdd(propertyName, value, out index);
#endif
            if (success)
            {
                value?.AssignParent(this);
            }
            return success;
        }

        /// <summary>
        ///   Adds the specified property to the <see cref="RdnObject"/>.
        /// </summary>
        /// <param name="property">
        ///   The KeyValuePair structure representing the property name and value to add to the <see cref="RdnObject"/>.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   An element with the same property name already exists in the <see cref="RdnObject"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   The property name of <paramref name="property"/> is <see langword="null"/>.
        /// </exception>
        public void Add(KeyValuePair<string, RdnNode?> property) => Add(property.Key, property.Value);

        /// <summary>
        ///   Removes all elements from the <see cref="RdnObject"/>.
        /// </summary>
        public void Clear()
        {
            OrderedDictionary<string, RdnNode?>? dictionary = _dictionary;

            if (dictionary is null)
            {
                _rdnElement = null;
                return;
            }

            foreach (RdnNode? node in dictionary.Values)
            {
                DetachParent(node);
            }

            dictionary.Clear();
        }

        /// <summary>
        ///   Determines whether the <see cref="RdnObject"/> contains an element with the specified property name.
        /// </summary>
        /// <param name="propertyName">The property name to locate in the <see cref="RdnObject"/>.</param>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="RdnObject"/> contains an element with the specified property name; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        public bool ContainsKey(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return Dictionary.ContainsKey(propertyName);
        }

        /// <summary>
        ///   Gets the number of elements contained in <see cref="RdnObject"/>.
        /// </summary>
        public int Count => Dictionary.Count;

        /// <summary>
        ///   Removes the element with the specified property name from the <see cref="RdnObject"/>.
        /// </summary>
        /// <param name="propertyName">The property name of the element to remove.</param>
        /// <returns>
        ///   <see langword="true"/> if the element is successfully removed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        public bool Remove(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            bool success = Dictionary.Remove(propertyName, out RdnNode? removedNode);
            if (success)
            {
                DetachParent(removedNode);
            }

            return success;
        }

        /// <summary>
        ///   Determines whether the <see cref="RdnObject"/> contains a specific property name and <see cref="RdnNode"/> reference.
        /// </summary>
        /// <param name="item">The element to locate in the <see cref="RdnObject"/>.</param>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="RdnObject"/> contains an element with the property name; otherwise, <see langword="false"/>.
        /// </returns>
        bool ICollection<KeyValuePair<string, RdnNode?>>.Contains(KeyValuePair<string, RdnNode?> item) =>
            ((IDictionary<string, RdnNode?>)Dictionary).Contains(item);

        /// <summary>
        ///   Copies the elements of the <see cref="RdnObject"/> to an array of type KeyValuePair starting at the specified array index.
        /// </summary>
        /// <param name="array">
        ///   The one-dimensional Array that is the destination of the elements copied from <see cref="RdnObject"/>.
        /// </param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="array"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="index"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   The number of elements in the source ICollection is greater than the available space from <paramref name="index"/>
        ///   to the end of the destination <paramref name="array"/>.
        /// </exception>
        void ICollection<KeyValuePair<string, RdnNode?>>.CopyTo(KeyValuePair<string, RdnNode?>[] array, int index) =>
            ((IDictionary<string, RdnNode?>)Dictionary).CopyTo(array, index);

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="RdnObject"/>.
        /// </summary>
        /// <returns>
        ///   An enumerator that iterates through the <see cref="RdnObject"/>.
        /// </returns>
        public IEnumerator<KeyValuePair<string, RdnNode?>> GetEnumerator() => Dictionary.GetEnumerator();

        /// <summary>
        ///   Removes a key and value from the <see cref="RdnObject"/>.
        /// </summary>
        /// <param name="item">
        ///   The KeyValuePair structure representing the property name and value to remove from the <see cref="RdnObject"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> if the element is successfully removed; otherwise, <see langword="false"/>.
        /// </returns>
        bool ICollection<KeyValuePair<string, RdnNode?>>.Remove(KeyValuePair<string, RdnNode?> item) => Remove(item.Key);

        /// <summary>
        ///   Gets a collection containing the property names in the <see cref="RdnObject"/>.
        /// </summary>
        ICollection<string> IDictionary<string, RdnNode?>.Keys => Dictionary.Keys;

        /// <summary>
        ///   Gets a collection containing the property values in the <see cref="RdnObject"/>.
        /// </summary>
        ICollection<RdnNode?> IDictionary<string, RdnNode?>.Values => Dictionary.Values;

        /// <summary>
        ///   Gets the value associated with the specified property name.
        /// </summary>
        /// <param name="propertyName">The property name of the value to get.</param>
        /// <param name="rdnNode">
        ///   When this method returns, contains the value associated with the specified property name, if the property name is found;
        ///   otherwise, <see langword="null"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> if the <see cref="RdnObject"/> contains an element with the specified property name; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        bool IDictionary<string, RdnNode?>.TryGetValue(string propertyName, out RdnNode? rdnNode)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return Dictionary.TryGetValue(propertyName, out rdnNode);
        }

        /// <summary>
        ///   Returns <see langword="false"/>.
        /// </summary>
        bool ICollection<KeyValuePair<string, RdnNode?>>.IsReadOnly => false;

        /// <summary>
        ///   Returns an enumerator that iterates through the <see cref="RdnObject"/>.
        /// </summary>
        /// <returns>
        ///   An enumerator that iterates through the <see cref="RdnObject"/>.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => Dictionary.GetEnumerator();

        private OrderedDictionary<string, RdnNode?> InitializeDictionary()
        {
            GetUnderlyingRepresentation(out OrderedDictionary<string, RdnNode?>? dictionary, out RdnElement? rdnElement);

            if (dictionary is null)
            {
                OrderedDictionary<string, RdnNode?> newDictionary = CreateDictionary(Options);

                if (rdnElement.HasValue)
                {
                    foreach (RdnProperty jElementProperty in rdnElement.Value.EnumerateObject())
                    {
                        RdnNode? node = RdnNodeConverter.Create(jElementProperty.Value, Options);
                        if (node != null) node.Parent = this;

                        newDictionary.Add(jElementProperty.Name, node);
                    }
                }

                // Ensure only one dictionary instance is published using CompareExchange
                OrderedDictionary<string, RdnNode?>? exchangedDictionary = Interlocked.CompareExchange(ref _dictionary, newDictionary, null);
                if (exchangedDictionary is null)
                {
                    // We won the race and published our dictionary
                    // Ensure _rdnElement is written to after _dictionary
                    _rdnElement = null;
                    dictionary = newDictionary;
                }
                else
                {
                    // Another thread won the race, use their dictionary
                    dictionary = exchangedDictionary;
                }
            }

            return dictionary;
        }

        private static OrderedDictionary<string, RdnNode?> CreateDictionary(RdnNodeOptions? options, int capacity = 0)
        {
            StringComparer comparer = options?.PropertyNameCaseInsensitive ?? false
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

            return new(capacity, comparer);
        }

        /// <summary>
        /// Provides a coherent view of the underlying representation of the current node.
        /// The rdnElement value should be consumed if and only if dictionary value is null.
        /// </summary>
        private void GetUnderlyingRepresentation(out OrderedDictionary<string, RdnNode?>? dictionary, out RdnElement? rdnElement)
        {
            // Because RdnElement cannot be read atomically there might be torn reads,
            // however the order of read/write operations guarantees that that's only
            // possible if the value of _dictionary is non-null.
            rdnElement = _rdnElement;
            Interlocked.MemoryBarrier();
            dictionary = _dictionary;
        }
    }
}
