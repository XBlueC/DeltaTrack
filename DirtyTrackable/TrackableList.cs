using System.Collections.ObjectModel;

namespace DirtyTrackable;

public class TrackableList<T> : Collection<T>, IDirtyTrackable where T : notnull
{
    private readonly DirtyTracker _tracker;

    public TrackableList(Action onChanged) : base(new List<T>())
    {
        DirtyStateChanged += onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _tracker = new DirtyTracker(this);
    }

    public TrackableList(Action onChanged, IList<T> initialItems) : base(initialItems)
    {
        DirtyStateChanged += onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _tracker = new DirtyTracker(this);
        _tracker.InitializeExistingItems(initialItems, DirtyStateChangedHandler);
    }

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        _tracker.HandleItemAdded(item, DirtyStateChangedHandler, index.ToString());
        DirtyStateChangedHandler();
    }

    protected override void SetItem(int index, T item)
    {
        var oldItem = this[index];
        base.SetItem(index, item);
        _tracker.HandleItemRemoved(oldItem, DirtyStateChangedHandler, index.ToString());
        _tracker.HandleItemAdded(item, DirtyStateChangedHandler, index.ToString());
        DirtyStateChangedHandler();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        _tracker.HandleItemRemoved(item, DirtyStateChangedHandler, index.ToString());
        DirtyStateChangedHandler();
    }

    protected override void ClearItems()
    {
        if (Count <= 0)
            return;
        for (int i = 0; i < Count; i++)
        {
            _tracker.HandleItemRemoved(this[i], DirtyStateChangedHandler, i.ToString());
        }

        base.ClearItems();
        DirtyStateChangedHandler();
    }

    #region IDirtyTrackable Implementation

    public bool IsDirty() => _tracker.IsDirty();

    public IReadOnlyCollection<string> GetDirtyFields() => _tracker.GetDirtyFields();

    public void MarkFieldDirty(string field) => _tracker.MarkFieldDirty(field);

    public void MarkClean(bool recursive = false) => _tracker.MarkClean(recursive);

    public event Action DirtyStateChanged;

    private void DirtyStateChangedHandler()
    {
        DirtyStateChanged?.Invoke();
    }

    #endregion
}