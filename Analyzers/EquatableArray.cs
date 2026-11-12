using System.Collections;
using System.Collections.Immutable;

namespace DeltaTrack;

internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
{
    private readonly ImmutableArray<T> _items = items.IsDefault ? ImmutableArray<T>.Empty : items;

    public int Length => _items.IsDefault ? 0 : _items.Length;
    public T this[int index] => _items[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length) return false;
        for (var i = 0; i < Length; i++)
            if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
                return false;
        return true;
    }

    public override bool Equals(object obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this)
            hash = unchecked(hash * 31 + (item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(item)));
        return hash;
    }

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)(_items.IsDefault ? ImmutableArray<T>.Empty : _items)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public static implicit operator EquatableArray<T>(ImmutableArray<T> items) => new(items);
}