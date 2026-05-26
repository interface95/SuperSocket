using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SuperSocket.Command.SourceGeneration.Model;

internal readonly struct EquatableArray<T> : IReadOnlyList<T>, IEquatable<EquatableArray<T>>
{
    private readonly T[]? _items;

    public EquatableArray(T[] items)
    {
        _items = items;
    }

    public EquatableArray(IEnumerable<T> items)
    {
        _items = items.ToArray();
    }

    public int Count => Items.Length;

    public T this[int index] => Items[index];

    private T[] Items => _items ?? Array.Empty<T>();

    public bool Equals(EquatableArray<T> other)
    {
        return Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = 17;

            foreach (T item in Items)
            {
                hashCode = (hashCode * 31) + EqualityComparer<T>.Default.GetHashCode(item!);
            }

            return hashCode;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)Items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static implicit operator EquatableArray<T>(T[] items)
    {
        return new EquatableArray<T>(items);
    }
}
