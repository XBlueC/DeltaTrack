using System.Collections;

namespace DirtyTrackable;

public class TrackableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IGetSource<IDictionary<TKey, TValue>>
    where TValue : notnull where TKey : notnull
{
    private readonly IDictionary<TKey, TValue> _inner;
    private readonly Action _onChanged;
    private readonly Dictionary<TValue, int> _valueRefCount = new();

    public TrackableDictionary(Action onChanged) : this(onChanged, new Dictionary<TKey, TValue>())
    {
    }

    public TrackableDictionary(Action onChanged, IDictionary<TKey, TValue> inner)
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        foreach (var value in _inner.Values) TrackValue(value, true);
    }

    public TValue this[TKey key]
    {
        get => _inner[key];
        set
        {
            var hasOld = _inner.TryGetValue(key, out var oldValue);
            _inner[key] = value;
            if (hasOld) TrackValue(oldValue, false);

            TrackValue(value, true);
            _onChanged?.Invoke();
        }
    }

    public void Add(TKey key, TValue value)
    {
        _inner.Add(key, value);
        TrackValue(value, true);
        _onChanged?.Invoke();
    }

    public bool Remove(TKey key)
    {
        if (_inner.TryGetValue(key, out var value) && _inner.Remove(key))
        {
            TrackValue(value, false);
            _onChanged?.Invoke();
            return true;
        }

        return false;
    }

    public void Clear()
    {
        if (_inner.Count > 0)
        {
            foreach (var value in _inner.Values) TrackValue(value, false);

            _valueRefCount.Clear();
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

    public IDictionary<TKey, TValue> GetSource()
    {
        return _inner;
    }

    private void TrackValue(TValue value, bool added)
    {
        if (value is not IDirtyTrackable trackable)
            return;

        if (added)
        {
            if (_valueRefCount.TryGetValue(value, out var count))
            {
                _valueRefCount[value] = count + 1;
            }
            else
            {
                _valueRefCount[value] = 1;
                trackable.DirtyStateChanged += _onChanged;
            }
        }
        else
        {
            if (_valueRefCount.TryGetValue(value, out var count))
            {
                if (count <= 1)
                {
                    _valueRefCount.Remove(value);
                    trackable.DirtyStateChanged -= _onChanged;
                }
                else
                {
                    _valueRefCount[value] = count - 1;
                }
            }
        }
    }
}

public interface IGetSource<out T>
{
    T GetSource();
}