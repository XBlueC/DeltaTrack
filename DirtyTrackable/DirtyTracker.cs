using System.Collections;

namespace DirtyTrackable;

public class DirtyTracker(IDirtyTrackable owner)
{
    private readonly IDirtyTrackable _owner = owner;

    private readonly HashSet<string> _dirtyFields = new();
    private readonly Dictionary<IDirtyTrackable, int> _childReferenceCount = new();

    #region IDirtyTrackable Implementation

    public bool IsDirty() => _dirtyFields.Count > 0 || HasDirtyChildren();

    public IReadOnlyCollection<string> GetDirtyFields() => _dirtyFields.ToList().AsReadOnly();

    public void MarkFieldDirty(string field)
    {
        _dirtyFields.Add(field);
    }

    public void MarkClean(bool recursive = false)
    {
        _dirtyFields.Clear();

        if (recursive)
        {
            MarkChildrenClean();
        }
    }

    public void SubscribeChild(IDirtyTrackable child, Action onChange)
    {
        if (child == null) return;

        if (_childReferenceCount.TryGetValue(child, out var count))
        {
            _childReferenceCount[child] = count + 1;
        }
        else
        {
            _childReferenceCount[child] = 1;
            child.DirtyStateChanged += onChange;
        }
    }

    public void UnsubscribeChild(IDirtyTrackable child, Action onChange)
    {
        if (child == null) return;

        if (_childReferenceCount.TryGetValue(child, out var count))
        {
            if (count <= 1)
            {
                _childReferenceCount.Remove(child);
                child.DirtyStateChanged -= onChange;
            }
            else
            {
                _childReferenceCount[child] = count - 1;
            }
        }
    }

    #endregion

    #region Protected Methods

    protected bool HasDirtyChildren()
    {
        return _childReferenceCount.Keys.Any(child => child.IsDirty());
    }

    protected void MarkChildrenClean()
    {
        foreach (var child in _childReferenceCount.Keys)
        {
            child.MarkClean(recursive: true);
        }
    }

    #endregion

    #region Collection Helper Methods

    protected internal void HandleItemAdded(object item, Action onChange, string indexPath = null)
    {
        if (item is IDirtyTrackable trackable)
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

    protected internal void HandleItemRemoved(object item, Action onChange, string indexPath = null)
    {
        if (item is IDirtyTrackable trackable)
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

    protected internal void InitializeExistingItems(IEnumerable items, Action onChange)
    {
        foreach (var item in items)
        {
            HandleItemAdded(item, onChange);
        }
    }

    #endregion

    public void Subscribe(object item, Action onChange)
    {
        if (item == null) return;

        switch (item)
        {
            case IDictionary dictionary:
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value is IDirtyTrackable trackable)
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
                    if (element is IDirtyTrackable trackable)
                    {
                        SubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case IDirtyTrackable trackable:
            {
                trackable.DirtyStateChanged += onChange;
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
                    if (entry.Value is IDirtyTrackable trackable)
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
                    if (element is IDirtyTrackable trackable)
                    {
                        UnsubscribeChild(trackable, onChange);
                    }
                }

                break;
            }
            case IDirtyTrackable trackable:
            {
                trackable.DirtyStateChanged -= onChange;
                break;
            }
        }
    }
}