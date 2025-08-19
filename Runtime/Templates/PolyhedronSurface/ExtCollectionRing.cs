using System.Collections.Generic;

namespace Eye.Maps.Templates
{
    /// <summary>
    /// Provides ring-indexing extension methods for arrays and lists,
    /// allowing safe wraparound access to elements by index.
    /// </summary>
    public static class ExtCollectionRing
    {
        /// <summary>
        /// Safely retrieves an element from a list using ring/wraparound indexing.
        /// Index values outside the list bounds wrap around (modulo count).
        /// </summary>
        /// <typeparam name="T">The element type of the list.</typeparam>
        /// <param name="list">The list to access.</param>
        /// <param name="index">
        /// The index to access. Can be negative or greater than the list count.
        /// The effective index is computed as (index % Count + Count) % Count.
        /// </param>
        /// <returns>
        /// The element at the wrapped index
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown if the list is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the list is empty.</exception>
        public static T RingIndex<T>(this List<T> list, int index)
        {
            if (list == null) throw new System.ArgumentNullException(nameof(list));
            return list[index.RingIndex(list.Count)];
        }

        /// <summary>
        /// Safely retrieves an element from an array using ring/wraparound indexing.
        /// Index values outside the array bounds wrap around (modulo length).
        /// </summary>
        /// <typeparam name="T">The element type of the array.</typeparam>
        /// <param name="array">The array to access.</param>
        /// <param name="index">
        /// The index to access. Can be negative or greater than the array length.
        /// The effective index is computed as (index % Length + Length) % Length.
        /// </param>
        /// <returns>
        /// The element at the wrapped index
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown if the array is null.</exception>
        ///  /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the array is empty.</exception>
        public static T RingIndex<T>(this T[] array, int index)
        {
            if (array == null) throw new System.ArgumentNullException(nameof(array));
            return array[index.RingIndex(array.Length)];
        }

        public static int RingIndex(this int index, int ringSize)
        {
            if (ringSize <= 0) throw new System.ArgumentOutOfRangeException(nameof(ringSize) + " must be greater than zero.");
            index %= ringSize;
            if (index < 0) index += ringSize;
            return index;

        }
    }
}