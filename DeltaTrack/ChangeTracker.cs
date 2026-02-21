using System.Collections;

namespace DeltaTrack;

public class ChangeTracker : IChangeTracker
{
    private readonly HashSet<string> _dirtyFields = new();
    private readonly Dictionary<ITrackable, int> _childReferenceCount = new();

    public bool HasChanges() => _dirtyFields.Count > 0;

    public IReadOnlyCollection<string> GetChangedFields() => _dirtyFields.ToList().AsReadOnly();

    public void MarkChanged(string field)
    {
        _dirtyFields.Add(field);
        OnChanged?.Invoke();
    }

    public void MarkClean(bool recursive = false)
    {
        OnClean?.Invoke(recursive);
        _dirtyFields.Clear();

        if (recursive)
        {
            MarkChildrenClean();
        }
    }

    public event Action OnChanged;
    public event Action<bool> OnClean;

    private void SubscribeChild(ITrackable child, Action onChange)
    {
        if (child == null) return;

        if (_childReferenceCount.TryGetValue(child, out var count))
        {
            _childReferenceCount[child] = count + 1;
        }
        else
        {
            _childReferenceCount[child] = 1;
            Subscribe(child, onChange);
        }
    }

    private void UnsubscribeChild(ITrackable child, Action onChange)
    {
        if (child == null) return;

        if (_childReferenceCount.TryGetValue(child, out var count))
        {
            if (count <= 1)
            {
                _childReferenceCount.Remove(child);
                Unsubscribe(child, onChange);
            }
            else
            {
                _childReferenceCount[child] = count - 1;
            }
        }
    }

    private bool HasDirtyChildren()
    {
        return _childReferenceCount.Keys.Any(child => child.GetChangeTracker().HasChanges());
    }

    private void MarkChildrenClean()
    {
        foreach (var child in _childReferenceCount.Keys)
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
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value is ITrackable trackable)
                    {
                        SubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case ICollection collection:
            {
                foreach (var element in collection)
                {
                    if (element is ITrackable trackable)
                    {
                        SubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case ITrackable trackable:
            {
                trackable.GetChangeTracker().OnChanged += onChange;
                break;
            }
        }
    }

    public void Unsubscribe(object item, Action onChange)
    {
        switch (item)
        {
            case IDictionary dictionary:
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value is ITrackable trackable)
                    {
                        UnsubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case ICollection collection:
            {
                foreach (var element in collection)
                {
                    if (element is ITrackable trackable)
                    {
                        UnsubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case ITrackable trackable:
            {
                trackable.GetChangeTracker().OnChanged -= onChange;
                break;
            }
        }
    }
}