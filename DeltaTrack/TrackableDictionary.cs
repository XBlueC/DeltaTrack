using System.Collections;

namespace DeltaTrack;

public class TrackableDictionary<TKey, TValue> : TrackableCollectionBase, IDictionary<TKey, TValue>
    where TValue : notnull where TKey : notnull
{
    private readonly IDictionary<TKey, TValue> _inner;

    public TrackableDictionary(Action onChanged) : this(onChanged, new Dictionary<TKey, TValue>())
    {
    }

    public TrackableDictionary(Action onChanged, IDictionary<TKey, TValue> inner) : base(onChanged)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        foreach (var kvp in _inner)
            NotifyAdded(kvp.Value);
    }

    public TValue this[TKey key]
    {
        get => _inner[key];
        set
        {
            var hasOld = _inner.TryGetValue(key, out var oldValue);
            _inner[key] = value;

            if (hasOld) NotifyRemoved(oldValue);
            NotifyAdded(value);
            RaiseChanged();
        }
    }

    public void Add(TKey key, TValue value)
    {
        _inner.Add(key, value);
        NotifyAdded(value);
        RaiseChanged();
    }

    public bool Remove(TKey key)
    {
        if (!_inner.TryGetValue(key, out var value) || !_inner.Remove(key)) return false;

        NotifyRemoved(value);
        RaiseChanged();
        return true;
    }

    public void Clear()
    {
        if (_inner.Count == 0) return;

        var snapshot = new List<TValue>(_inner.Count);
        foreach (var kvp in _inner)
            snapshot.Add(kvp.Value);

        _inner.Clear();
        foreach (var value in snapshot)
            NotifyRemoved(value);

        RaiseChanged();
    }

    public bool ContainsKey(TKey key) => _inner.ContainsKey(key);

    public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);

    public ICollection<TKey> Keys => _inner.Keys;
    public ICollection<TValue> Values => _inner.Values;
    public int Count => _inner.Count;
    public bool IsReadOnly => _inner.IsReadOnly;

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public bool Contains(KeyValuePair<TKey, TValue> item) => _inner.Contains(item);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        => _inner.CopyTo(array, arrayIndex);

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (Equals(_inner[item.Key], item.Value)) return Remove(item.Key);
        return false;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
}