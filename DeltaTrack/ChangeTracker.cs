using System.Collections;

namespace DeltaTrack;

public class ChangeTracker : IDisposable
{
    private long _dirtyFlags;
    private readonly Dictionary<ITrackable, (int Count, Action OnChange)> _childSubscriptions = new();
    private bool _disposed;

    public bool HasChanges() => _dirtyFlags != 0;

    public long DirtyFlags => _dirtyFlags;

    public void MarkChanged(long flag)
    {
        _dirtyFlags |= flag;
        OnChanged?.Invoke();
    }

    public void MarkClean(bool recursive = false)
    {
        OnClean?.Invoke(recursive);
        _dirtyFlags = 0;
    }

    public event Action OnChanged;
    public event Action<bool> OnClean;

    private void SubscribeChild(ITrackable child, Action onChange)
    {
        if (child == null || _disposed) return;

        if (_childSubscriptions.TryGetValue(child, out var existing))
        {
            _childSubscriptions[child] = (existing.Count + 1, existing.OnChange);
        }
        else
        {
            _childSubscriptions[child] = (1, onChange);
            child.OnChanged += onChange;
        }
    }

    private void UnsubscribeChild(ITrackable child, Action onChange)
    {
        if (child == null || _disposed) return;

        if (_childSubscriptions.TryGetValue(child, out var existing))
        {
            if (existing.Count <= 1)
            {
                _childSubscriptions.Remove(child);
                child.OnChanged -= existing.OnChange;
            }
            else
            {
                _childSubscriptions[child] = (existing.Count - 1, existing.OnChange);
            }
        }
    }

    public void HandleItemAdded(object item, Action onChange)
    {
        if (item is ITrackable trackable)
        {
            SubscribeChild(trackable, onChange);
        }
        else if (item is IEnumerable enumerable && item is not string)
        {
            foreach (var element in enumerable)
            {
                HandleItemAdded(element, onChange);
            }
        }
    }

    public void HandleItemRemoved(object item, Action onChange)
    {
        if (item is ITrackable trackable)
        {
            UnsubscribeChild(trackable, onChange);
        }
        else if (item is IEnumerable enumerable && item is not string)
        {
            foreach (var element in enumerable)
            {
                HandleItemRemoved(element, onChange);
            }
        }
    }

    public void InitializeExistingItems(IEnumerable items, Action onChange)
    {
        foreach (var item in items)
        {
            HandleItemAdded(item, onChange);
        }
    }

    public void Subscribe(object item, Action onChange)
    {
        if (item == null) return;

        switch (item)
        {
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                    SubscribeItem(entry.Value, onChange);
                break;
            case ICollection collection:
                foreach (var element in collection)
                    SubscribeItem(element, onChange);
                break;
            case ITrackable trackable:
                trackable.OnChanged += onChange;
                break;
        }
    }

    public void Unsubscribe(object item, Action onChange)
    {
        if (item == null) return;

        switch (item)
        {
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                    UnsubscribeItem(entry.Value, onChange);
                break;
            case ICollection collection:
                foreach (var element in collection)
                    UnsubscribeItem(element, onChange);
                break;
            case ITrackable trackable:
                trackable.OnChanged -= onChange;
                break;
        }
    }

    private void SubscribeItem(object item, Action onChange)
    {
        if (item is ITrackable trackable)
            SubscribeChild(trackable, onChange);
    }

    private void UnsubscribeItem(object item, Action onChange)
    {
        if (item is ITrackable trackable)
            UnsubscribeChild(trackable, onChange);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _childSubscriptions)
        {
            entry.Key.OnChanged -= entry.Value.OnChange;
        }

        _childSubscriptions.Clear();
        _dirtyFlags = 0;

        OnChanged = null;
        OnClean = null;
    }
}