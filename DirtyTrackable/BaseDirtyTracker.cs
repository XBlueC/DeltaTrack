using System.Collections;

namespace DirtyTrackable;

public abstract class BaseDirtyTracker
{
    private readonly HashSet<string> _dirtyFields = new();
    private readonly Dictionary<IDirtyTrackable, int> _childReferenceCount = new();

    #region IDirtyTrackable Implementation

    public virtual bool IsDirty() => _dirtyFields.Count > 0 || HasDirtyChildren();

    public virtual IReadOnlyCollection<string> GetDirtyFields() => _dirtyFields.ToList().AsReadOnly();

    public virtual void MarkFieldDirty(string field)
    {
        _dirtyFields.Add(field);
    }

    public virtual void MarkClean(bool recursive = false)
    {
        _dirtyFields.Clear();

        if (recursive)
        {
            MarkChildrenClean();
        }
    }

    public virtual void SubscribeChild(IDirtyTrackable child, Action onChange)
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

    public virtual void UnsubscribeChild(IDirtyTrackable child, Action onChange)
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

    protected virtual bool HasDirtyChildren()
    {
        return _childReferenceCount.Keys.Any(child => child.IsDirty());
    }

    protected virtual void MarkChildrenClean()
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
            // if (!string.IsNullOrEmpty(indexPath))
            // {
            //     _pathTracker.MarkPathDirty(indexPath);
            // }
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
            // if (!string.IsNullOrEmpty(indexPath))
            // {
            //     _pathTracker.MarkPathDirty(indexPath);
            // }
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
}