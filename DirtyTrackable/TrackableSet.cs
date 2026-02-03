using System.Collections;

namespace DirtyTrackable;

public class TrackableSet<T> : ISet<T>
{
    private readonly ISet<T> _inner;
    private readonly Action _onChanged;

    public TrackableSet(Action onChanged)
        : this(onChanged, new HashSet<T>())
    {
    }

    public TrackableSet(Action onChanged, ISet<T> inner)
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        foreach (var item in _inner)
            if (item is IDirtyTrackable trackable)
                trackable.DirtyStateChanged += _onChanged;
    }

    public bool Add(T item)
    {
        var added = _inner.Add(item);
        if (added)
        {
            if (item is IDirtyTrackable trackable) trackable.DirtyStateChanged += _onChanged;

            _onChanged();
        }

        return added;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _inner.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        var removed = _inner.Remove(item);
        if (removed)
        {
            if (item is IDirtyTrackable trackable) trackable.DirtyStateChanged -= _onChanged;

            _onChanged();
        }

        return removed;
    }

    void ICollection<T>.Add(T item)
    {
        Add(item);
    }

    public void Clear()
    {
        if (_inner.Count > 0)
        {
            foreach (var item in _inner)
                if (item is IDirtyTrackable trackable)
                    trackable.DirtyStateChanged -= _onChanged;

            _inner.Clear();
            _onChanged();
        }
    }

    public bool Contains(T item)
    {
        return _inner.Contains(item);
    }

    public int Count => _inner.Count;
    public bool IsReadOnly => false;

    public IEnumerator<T> GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void UnionWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var changed = false;
        foreach (var item in other)
            if (_inner.Add(item))
            {
                if (item is IDirtyTrackable trackable) trackable.DirtyStateChanged += _onChanged;

                changed = true;
            }

        if (changed) _onChanged();
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        var toRemove = new List<T>();

        foreach (var item in _inner)
            if (!otherSet.Contains(item))
                toRemove.Add(item);

        if (toRemove.Count > 0)
        {
            foreach (var item in toRemove)
            {
                _inner.Remove(item);
                if (item is IDirtyTrackable trackable) trackable.DirtyStateChanged -= _onChanged;
            }

            _onChanged();
        }
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        var toRemove = new List<T>();

        foreach (var item in _inner)
            if (otherSet.Contains(item))
                toRemove.Add(item);

        if (toRemove.Count > 0)
        {
            foreach (var item in toRemove)
            {
                _inner.Remove(item);
                if (item is IDirtyTrackable trackable) trackable.DirtyStateChanged -= _onChanged;
            }

            _onChanged();
        }
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        var currentSnapshot = new HashSet<T>(_inner);

        var toRemove = currentSnapshot.Intersect(otherSet).ToList();
        var toAdd = otherSet.Except(currentSnapshot).ToList();

        var changed = false;

        foreach (var item in toRemove)
        {
            _inner.Remove(item);
            if (item is IDirtyTrackable trackable) trackable.DirtyStateChanged -= _onChanged;

            changed = true;
        }

        foreach (var item in toAdd)
        {
            _inner.Add(item);
            if (item is IDirtyTrackable trackable) trackable.DirtyStateChanged += _onChanged;

            changed = true;
        }

        if (changed) _onChanged();
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        return _inner.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        return _inner.IsSupersetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        return _inner.IsProperSupersetOf(other);
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        return _inner.IsProperSubsetOf(other);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        return _inner.Overlaps(other);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        return _inner.SetEquals(other);
    }
}