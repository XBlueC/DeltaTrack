using System.Collections;

namespace DeltaTrack;

public class TrackableDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    where TValue : notnull where TKey : notnull
{
    private readonly IDictionary<TKey, TValue> _inner;
    private readonly Action _onChanged;
    private readonly ChangeTracker _tracker;

    public TrackableDictionary(Action onChanged) : this(onChanged, new Dictionary<TKey, TValue>())
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _tracker = new ChangeTracker();
    }

    public TrackableDictionary(Action onChanged, IDictionary<TKey, TValue> inner)
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _tracker = new ChangeTracker();

        foreach (var kvp in _inner)
        {
            _tracker.HandleItemAdded(kvp.Value, _onChanged, kvp.Key?.ToString());
        }
    }

    public TValue this[TKey key]
    {
        get => _inner[key];
        set
        {
            var hasOld = _inner.TryGetValue(key, out var oldValue);
            _inner[key] = value;

            if (hasOld)
            {
                _tracker.HandleItemRemoved(oldValue, _onChanged, key?.ToString());
            }

            _tracker.HandleItemAdded(value, _onChanged, key?.ToString());
            _onChanged?.Invoke();
        }
    }

    public void Add(TKey key, TValue value)
    {
        _inner.Add(key, value);
        _tracker.HandleItemAdded(value, _onChanged, key?.ToString());
        _onChanged?.Invoke();
    }

    public bool Remove(TKey key)
    {
        if (_inner.TryGetValue(key, out var value) && _inner.Remove(key))
        {
            _tracker.HandleItemRemoved(value, _onChanged, key?.ToString());
            _onChanged?.Invoke();
            return true;
        }

        return false;
    }

    public void Clear()
    {
        if (_inner.Count > 0)
        {
            foreach (var kvp in _inner)
            {
                _tracker.HandleItemRemoved(kvp.Value, _onChanged, kvp.Key?.ToString());
            }

            _inner.Clear();
            _onChanged?.Invoke();
        }
    }

    public bool ContainsKey(TKey key)
    {
        return _inner.ContainsKey(key);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        return _inner.TryGetValue(key, out value);
    }

    public ICollection<TKey> Keys => _inner.Keys;
    public ICollection<TValue> Values => _inner.Values;
    public int Count => _inner.Count;
    public bool IsReadOnly => _inner.IsReadOnly;

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return _inner.Contains(item);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        _inner.CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (Equals(_inner[item.Key], item.Value)) return Remove(item.Key);

        return false;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _inner.GetEnumerator();
    }
}