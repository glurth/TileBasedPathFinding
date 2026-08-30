using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static public class Permutations
{
    static int Factorial(int n)
    {
        int fact = 1;
        for (int i = n; i > 0; i--)
        {
            fact *= i;
        }
        return fact;
    }
    static int Factorial(int end,int start)
    {
        int fact = 1;
        for (int i = end; i > start; i--)
        {
            fact *= i;
        }
        return fact;
    }
    static public int NumberOfPermutations(int countOfSelectionOptions, int numberOfSelections)
    {
        int r = countOfSelectionOptions - numberOfSelections;
        if (r < 0) throw new System.Exception("Unable to process NumberOfPermutations function: `countOfSelectionOptions` must be greater than `numberOfSelections`, but it is not.");
        return Factorial(countOfSelectionOptions,r);
    }

    static public IEnumerable<List<T>> PermutationsOf<T>(this List<T> list)
    {
        int count = list.Count;
        int[] indices = new int[count];// this array of indices will be manipulated below

        // The permutation is generated using indices 
        // Note: the contents of the indices array, starts in ASCENDING order.
        for (int i = 0; i < count; i++)
            indices[i] = i;

        while (true)
        {
            // Build the current permutation from the index ordering.
            List<T> result = new List<T>(count);

            for (int i = 0; i < count; i++)
                result.Add(list[indices[i]]);

            yield return result;

            // Find the rightmost pair that is still increasing.
            // Everything to its right is already in descending order.
            int pivotIndex = count - 2;// start with second to last element

            //decrement pivotIndex until we reach an element in the indices array, that is greater than the next index.
            while (pivotIndex >= 0 && indices[pivotIndex] > indices[pivotIndex + 1])
                pivotIndex--;

            // No increasing pair means the indices are completely descending,
            // which is the last possible permutation.
            if (pivotIndex < 0)
                yield break;

            // Find the smallest value to the right that is larger than the pivot.
            int swapIndex = count - 1;//start with the last element

            while (indices[swapIndex] < indices[pivotIndex])
                swapIndex--;

            //modify the indices array with the next permutation

            // Increase the pivot by swapping it with that value.
            int temp = indices[pivotIndex];
            indices[pivotIndex] = indices[swapIndex];
            indices[swapIndex] = temp;

            // The remaining values were descending. Reverse them to get the
            // lowest possible ordering following the new pivot.
            System.Array.Reverse(indices, pivotIndex + 1, count - pivotIndex - 1);
        }
    }
    static public long OrderingPermutationCount(int x)
    {
        return NumberOfPermutations(x, x);
    }

    static public int[] PermutationFromIndex(int x, long index)
    {
        long count = OrderingPermutationCount(x);

        if (index < 0 || index >= count)
            throw new System.ArgumentOutOfRangeException(nameof(index));

        List<int> available = new List<int>(x);
        for (int i = 0; i < x; i++)
        {
            available.Add(i);
        }

        int[] result = new int[x];

        for (int pos = 0; pos < x; pos++)
        {
            long fact = 1;
            for (int i = 2; i < x - pos; i++)
            {
                fact *= i;
            }

            int choice = (int)(index / fact);
            index %= fact;

            result[pos] = available[choice];
            available.RemoveAt(choice);
        }

        return result;
    }
}
