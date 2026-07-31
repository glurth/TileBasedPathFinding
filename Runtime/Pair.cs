using System;
using System.Collections.Generic;

/// <summary>
/// Represent a link between two objects.  contains a reference to, or instance of (depending on type T), both objects in the pair.
/// </summary>
/// <typeparam name="T"></typeparam>
public readonly struct Pair<T> : IEquatable<Pair<T>>
{
    public T Item1 { get; }
    public T Item2 { get; }

    public Pair(T item1, T item2)
    {
        Item1 = item1;
        Item2 = item2;
    }

    public void Deconstruct(out T item1, out T item2)
    {
        item1 = Item1;
        item2 = Item2;
    }

    public bool Equals(Pair<T> other)
    {
        return EqualityComparer<T>.Default.Equals(Item1, other.Item1)
            && EqualityComparer<T>.Default.Equals(Item2, other.Item2);
    }

    public override bool Equals(object obj)
    {
        if (obj is Pair<T> other)
            return Equals(other);

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Item1, Item2);
    }

    public override string ToString()
    {
        return $"({Item1}, {Item2})";
    }

    public static bool operator ==(Pair<T> left, Pair<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Pair<T> left, Pair<T> right)
    {
        return !left.Equals(right);
    }

    public static implicit operator Pair<T>((T, T) tuple)
    {
        return new Pair<T>(tuple.Item1, tuple.Item2);
    }

    public static implicit operator (T, T)(Pair<T> pair)
    {
        return (pair.Item1, pair.Item2);
    }
}