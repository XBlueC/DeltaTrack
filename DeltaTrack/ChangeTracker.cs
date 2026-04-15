using System.Collections;

namespace DeltaTrack;

public class ChangeTracker : IChangeTracker
{
    private readonly HashSet<string> _changedProperties = new();
    private readonly Dictionary<ITrackable, (int Count, Action OnChange)> _childSubscriptions = new();
    private bool _disposed;

    public ChangeTracker(Action onInit = null) => onInit?.Invoke();

    public bool HasChanges() => _changedProperties.Count > 0;

    public IReadOnlyCollection<string> GetChangedProperties() => _changedProperties;

    public void MarkChanged(string property)
    {
        _changedProperties.Add(property);
        OnChanged?.Invoke();
    }

    public void MarkClean(bool recursive = false)
    {
        OnClean?.Invoke(recursive);
        _changedProperties.Clear();

        if (recursive)
        {
            MarkChildrenClean();
        }
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
            child.GetChangeTracker().OnChanged += onChange;
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
                child.GetChangeTracker().OnChanged -= existing.OnChange;
            }
            else
            {
                _childSubscriptions[child] = (existing.Count - 1, existing.OnChange);
            }
        }
    }

    private bool HasDirtyChildren()
    {
        foreach (var child in _childSubscriptions.Keys)
        {
            if (child.GetChangeTracker().HasChanges())
                return true;
        }
        return false;
    }

    private void MarkChildrenClean()
    {
        foreach (var child in _childSubscriptions.Keys)
        {
            child.GetChangeTracker().MarkClean(recursive: true);
        }
    }

    public void HandleItemAdded(object item, Action onChange, string indexPath = null)
    {
        if (item is ITrackable trackable)
        {
            SubscribeChild(trackable, onChange);
        }
        else if (item is IEnumerable enumerable && item is not string)
        {
            int index = 0;
            foreach (var element in enumerable)
            {
                HandleItemAdded(element, onChange, index.ToString());
                index++;
            }
        }
    }

    public void HandleItemRemoved(object item, Action onChange, string indexPath = null)
    {
        if (item is ITrackable trackable)
        {
            UnsubscribeChild(trackable, onChange);
        }
        else if (item is IEnumerable enumerable && item is not string)
        {
            var index = 0;
            foreach (var element in enumerable)
            {
                HandleItemRemoved(element, onChange, $"{indexPath}.{index}");
                index++;
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
                trackable.GetChangeTracker().OnChanged += onChange;
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
                trackable.GetChangeTracker().OnChanged -= onChange;
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
            entry.Key.GetChangeTracker().OnChanged -= entry.Value.OnChange;
        }

        _childSubscriptions.Clear();
        _changedProperties.Clear();

        OnChanged = null;
        OnClean = null;
    }
}