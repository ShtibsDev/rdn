// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

namespace Rdn.Nodes
{
    public partial class RdnObject : IList<KeyValuePair<string, RdnNode?>>
    {
        /// <summary>Gets the property the specified index.</summary>
        /// <param name="index">The zero-based index of the pair to get.</param>
        /// <returns>The property at the specified index as a key/value pair.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than or equal to <see cref="Count"/>.</exception>
        public KeyValuePair<string, RdnNode?> GetAt(int index) => Dictionary.GetAt(index);

        /// <summary>Sets a new property at the specified index.</summary>
        /// <param name="index">The zero-based index of the property to set.</param>
        /// <param name="propertyName">The property name to store at the specified index.</param>
        /// <param name="value">The RDN value to store at the specified index.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than or equal to <see cref="Count"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> is already specified in a different index.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="value"/> already has a parent.</exception>
        public void SetAt(int index, string propertyName, RdnNode? value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            OrderedDictionary<string, RdnNode?> dictionary = Dictionary;
            KeyValuePair<string, RdnNode?> existing = dictionary.GetAt(index);
            dictionary.SetAt(index, propertyName, value);
            DetachParent(existing.Value);
            value?.AssignParent(this);
        }

        /// <summary>Sets a new property value at the specified index.</summary>
        /// <param name="index">The zero-based index of the property to set.</param>
        /// <param name="value">The RDN value to store at the specified index.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than or equal to <see cref="Count"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="value"/> already has a parent.</exception>
        public void SetAt(int index, RdnNode? value)
        {
            OrderedDictionary<string, RdnNode?> dictionary = Dictionary;
            KeyValuePair<string, RdnNode?> existing = dictionary.GetAt(index);
            dictionary.SetAt(index, value);
            DetachParent(existing.Value);
            value?.AssignParent(this);
        }

        /// <summary>Determines the index of a specific property name in the object.</summary>
        /// <param name="propertyName">The property name to locate.</param>
        /// <returns>The index of <paramref name="propertyName"/> if found; otherwise, -1.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
        public int IndexOf(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return Dictionary.IndexOf(propertyName);
        }

        /// <summary>Inserts a property into the object at the specified index.</summary>
        /// <param name="index">The zero-based index at which the property should be inserted.</param>
        /// <param name="propertyName">The property name to insert.</param>
        /// <param name="value">The RDN value to insert.</param>
        /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is null.</exception>
        /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="RdnObject"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than <see cref="Count"/>.</exception>
        public void Insert(int index, string propertyName, RdnNode? value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            Dictionary.Insert(index, propertyName, value);
            value?.AssignParent(this);
        }

        /// <summary>Removes the property at the specified index.</summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than or equal to <see cref="Count"/>.</exception>
        public void RemoveAt(int index)
        {
            KeyValuePair<string, RdnNode?> existing = Dictionary.GetAt(index);
            Dictionary.RemoveAt(index);
            DetachParent(existing.Value);
        }

        /// <inheritdoc />
        KeyValuePair<string, RdnNode?> IList<KeyValuePair<string, RdnNode?>>.this[int index]
        {
            get => GetAt(index);
            set => SetAt(index, value.Key, value.Value);
        }

        /// <inheritdoc />
        int IList<KeyValuePair<string, RdnNode?>>.IndexOf(KeyValuePair<string, RdnNode?> item) => ((IList<KeyValuePair<string, RdnNode?>>)Dictionary).IndexOf(item);

        /// <inheritdoc />
        void IList<KeyValuePair<string, RdnNode?>>.Insert(int index, KeyValuePair<string, RdnNode?> item) => Insert(index, item.Key, item.Value);

        /// <inheritdoc />
        void IList<KeyValuePair<string, RdnNode?>>.RemoveAt(int index) => RemoveAt(index);
    }
}
