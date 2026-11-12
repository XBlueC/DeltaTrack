using System.Collections;

namespace DeltaTrack;

public class TrackableSet<T> : TrackableCollectionBase, ISet<T>
{
    private readonly ISet<T> _inner;
    private static readonly bool IsUntrackedValue = typeof(T).IsValueType &&
        !typeof(ITrackable).IsAssignableFrom(typeof(T)) && !typeof(IEnumerable).IsAssignableFrom(typeof(T));

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
        if (IsUntrackedValue)
        {
            if (!_inner.Remove(item)) return false;
            RaiseChanged();
            return true;
        }
        if (_inner is HashSet<T> hashSet)
        {
            if (!hashSet.Contains(item)) return false;
            var actual = item;
            if (!typeof(T).IsValueType)
                foreach (var candidate in hashSet)
                    if (hashSet.Comparer.Equals(candidate, item))
                    {
                        actual = candidate;
                        break;
                    }

            if (!hashSet.Remove(item)) return false;
            NotifyRemoved(actual);
            RaiseChanged();
            return true;
        }

        if (!_inner.Contains(item)) return false;
        var before = new HashSet<T>(_inner, ElementIdentityComparer<T>.Instance);
        if (!_inner.Remove(item)) return false;
        ReconcileSubscriptions(before);
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
    public bool IsReadOnly => _inner.IsReadOnly;

    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void UnionWith(IEnumerable<T> other) => ApplySetOperation(other, SetOperation.Union);
    public void IntersectWith(IEnumerable<T> other) => ApplySetOperation(other, SetOperation.Intersect);
    public void ExceptWith(IEnumerable<T> other) => ApplySetOperation(other, SetOperation.Except);
    public void SymmetricExceptWith(IEnumerable<T> other) => ApplySetOperation(other, SetOperation.SymmetricExcept);

    private void ApplySetOperation(IEnumerable<T> other, SetOperation operation)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        var operand = other is TrackableSet<T> wrapper ? wrapper._inner : other;
        if (IsUntrackedValue)
        {
            var original = new List<T>(_inner);
            try { MutateInner(operand, operation); }
            finally { if (!_inner.SetEquals(original)) RaiseChanged(); }
            return;
        }
        var before = new HashSet<T>(_inner, ElementIdentityComparer<T>.Instance);
        try
        {
            MutateInner(operand, operation);
        }
        finally
        {
            ReconcileSubscriptions(before);
        }
    }

    private void MutateInner(IEnumerable<T> other, SetOperation operation)
    {
        switch (operation)
        {
            case SetOperation.Union: _inner.UnionWith(other); break;
            case SetOperation.Intersect: _inner.IntersectWith(other); break;
            case SetOperation.Except: _inner.ExceptWith(other); break;
            case SetOperation.SymmetricExcept: _inner.SymmetricExceptWith(other); break;
        }
    }

    private void ReconcileSubscriptions(HashSet<T> before)
    {
        var changed = false;
        foreach (var item in _inner)
        {
            if (before.Remove(item)) continue;
            NotifyAdded(item);
            changed = true;
        }

        foreach (var item in before)
        {
            NotifyRemoved(item);
            changed = true;
        }

        if (changed) RaiseChanged();
    }

    private enum SetOperation
    {
        Union,
        Intersect,
        Except,
        SymmetricExcept
    }

    public bool IsSubsetOf(IEnumerable<T> other) => _inner.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _inner.IsSupersetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _inner.IsProperSupersetOf(other);
    public bool IsProperSubsetOf(IEnumerable<T> other) => _inner.IsProperSubsetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _inner.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _inner.SetEquals(other);

    public static ISet<T> CopyItems(ISet<T> source)
    {
        while (source is TrackableSet<T> wrapper) source = wrapper._inner;
        return source is SortedSet<T> sorted
            ? new SortedSet<T>(sorted, sorted.Comparer)
            : CopyHashSet(source);
    }

    public static HashSet<T> CopyHashSet(ISet<T> source)
    {
        while (source is TrackableSet<T> wrapper) source = wrapper._inner;
        return source is HashSet<T> hashSet
            ? new HashSet<T>(source, hashSet.Comparer)
            : throw new NotSupportedException(
                "The set comparer cannot be preserved. Assign a HashSet<T> with the required comparer, or use an ISet<T> field for a SortedSet<T>.");
    }

    public static SortedSet<T> CopySortedSet(ISet<T> source)
    {
        while (source is TrackableSet<T> wrapper) source = wrapper._inner;
        return source is SortedSet<T> sorted
            ? new SortedSet<T>(source, sorted.Comparer)
            : throw new NotSupportedException(
                "The set comparer cannot be preserved. Assign a SortedSet<T> with the required comparer.");
    }
}
