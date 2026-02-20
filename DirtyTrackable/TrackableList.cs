using System.Collections.ObjectModel;

namespace DirtyTrackable;

public class TrackableList<T> : Collection<T>, IDirtyTrackable where T : notnull
{
    private readonly Action _onChanged;
    private readonly BaseDirtyTracker _tracker;

    public TrackableList(Action onChanged) : base(new List<T>())
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _tracker = new DirtyTracker(this);
    }

    public TrackableList(Action onChanged, IEnumerable<T> initialItems) : base(new List<T>(initialItems))
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _tracker = new DirtyTracker(this);
        _tracker.InitializeExistingItems(initialItems, _onChanged);
    }

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        _tracker.HandleItemAdded(item, _onChanged, index.ToString());
        _onChanged();
    }

    protected override void SetItem(int index, T item)
    {
        var oldItem = this[index];
        base.SetItem(index, item);
        _tracker.HandleItemRemoved(oldItem, _onChanged, index.ToString());
        _tracker.HandleItemAdded(item, _onChanged, index.ToString());
        _onChanged();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        _tracker.HandleItemRemoved(item, _onChanged, index.ToString());
        _onChanged();
    }

    protected override void ClearItems()
    {
        for (int i = 0; i < Count; i++)
        {
            _tracker.HandleItemRemoved(this[i], _onChanged, i.ToString());
        }

        base.ClearItems();
        _onChanged();
    }

    #region IDirtyTrackable Implementation

    public bool IsDirty() => _tracker.IsDirty();

    public IReadOnlyCollection<string> GetDirtyFields() => _tracker.GetDirtyFields();

    public void MarkFieldDirty(string field) => _tracker.MarkFieldDirty(field);

    public void MarkClean(bool recursive = false) => _tracker.MarkClean(recursive);

    public event Action DirtyStateChanged;

    #endregion
}