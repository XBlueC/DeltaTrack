using System.Collections;

namespace DeltaTrack;

public class TrackableSet<T> : TrackableCollectionBase, ISet<T>
{
    private readonly ISet<T> _inner;

    public TrackableSet(Action onChanged) : this(onChanged, new HashSet<T>())
    {
    }

    public TrackableSet(Action onChanged, ISet<T> inner) : base(onChanged)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        InitializeExistingItems(_inner);
    }

    public bool Add(T item)
    {
        if (!_inner.Add(item)) return false;
        NotifyAdded(item);
        RaiseChanged();
        return true;
    }

    public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

    public bool Remove(T item)
    {
        if (!_inner.Remove(item)) return false;
        NotifyRemoved(item);
        RaiseChanged();
        return true;
    }

    void ICollection<T>.Add(T item) => Add(item);

    public void Clear()
    {
        if (_inner.Count == 0) return;

        var snapshot = new List<T>(_inner);
        _inner.Clear();
        foreach (var item in snapshot)
            NotifyRemoved(item);
        RaiseChanged();
    }

    public bool Contains(T item) => _inner.Contains(item);

    public int Count => _inner.Count;
    public bool IsReadOnly => false;

    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void UnionWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var changed = false;
        foreach (var item in other)
        {
            if (!_inner.Add(item)) continue;
            NotifyAdded(item);
            changed = true;
        }

        if (changed) RaiseChanged();
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        var toRemove = new List<T>();
        foreach (var item in _inner)
        {
            if (!otherSet.Contains(item)) toRemove.Add(item);
        }

        if (toRemove.Count == 0) return;

        foreach (var item in toRemove)
        {
            _inner.Remove(item);
            NotifyRemoved(item);
        }

        RaiseChanged();
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        var toRemove = new List<T>();
        foreach (var item in _inner)
        {
            if (otherSet.Contains(item)) toRemove.Add(item);
        }

        if (toRemove.Count == 0) return;

        foreach (var item in toRemove)
        {
            _inner.Remove(item);
            NotifyRemoved(item);
        }

        RaiseChanged();
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        var currentSnapshot = new HashSet<T>(_inner);

        var toRemove = currentSnapshot.Intersect(otherSet).ToList();
        var toAdd = otherSet.Except(currentSnapshot).ToList();

        if (toRemove.Count == 0 && toAdd.Count == 0) return;

        foreach (var item in toRemove)
        {
            _inner.Remove(item);
            NotifyRemoved(item);
        }

        foreach (var item in toAdd)
        {
            _inner.Add(item);
            NotifyAdded(item);
        }

        RaiseChanged();
    }

    public bool IsSubsetOf(IEnumerable<T> other) => _inner.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _inner.IsSupersetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _inner.IsProperSupersetOf(other);
    public bool IsProperSubsetOf(IEnumerable<T> other) => _inner.IsProperSubsetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _inner.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _inner.SetEquals(other);
}