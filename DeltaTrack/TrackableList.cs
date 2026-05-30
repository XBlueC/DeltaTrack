using System.Collections;

namespace DeltaTrack;

public class TrackableList<T> : TrackableCollectionBase, IList<T> where T : notnull
{
    private readonly IList<T> _inner;

    public TrackableList(Action onChanged) : this(onChanged, new List<T>())
    {
    }

    public TrackableList(Action onChanged, IList<T> initialItems) : base(onChanged)
    {
        _inner = initialItems ?? throw new ArgumentNullException(nameof(initialItems));
        InitializeExistingItems(_inner);
    }

    public T this[int index]
    {
        get => _inner[index];
        set
        {
            var old = _inner[index];
            _inner[index] = value;
            NotifyRemoved(old);
            NotifyAdded(value);
            RaiseChanged();
        }
    }

    public int Count => _inner.Count;
    public bool IsReadOnly => _inner.IsReadOnly;

    public void Add(T item)
    {
        _inner.Add(item);
        NotifyAdded(item);
        RaiseChanged();
    }

    public void Insert(int index, T item)
    {
        _inner.Insert(index, item);
        NotifyAdded(item);
        RaiseChanged();
    }

    public bool Remove(T item)
    {
        var idx = _inner.IndexOf(item);
        if (idx < 0) return false;

        var removed = _inner[idx];
        _inner.RemoveAt(idx);
        NotifyRemoved(removed);
        RaiseChanged();
        return true;
    }

    public void RemoveAt(int index)
    {
        var item = _inner[index];
        _inner.RemoveAt(index);
        NotifyRemoved(item);
        RaiseChanged();
    }

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

    public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);

    public int IndexOf(T item) => _inner.IndexOf(item);

    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
}