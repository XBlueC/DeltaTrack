using System.Collections.ObjectModel;

namespace DeltaTrack;

public class TrackableList<T> : Collection<T> where T : notnull
{
    private readonly ChangeTracker _tracker;
    private readonly Action _onChanged;

    public TrackableList(Action onChanged) : this(onChanged, new List<T>())
    {
    }

    public TrackableList(Action onChanged, IList<T> initialItems) : base(initialItems)
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _tracker = new ChangeTracker();
        _tracker.InitializeExistingItems(initialItems, _onChanged);
    }

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        _tracker.HandleItemAdded(item, _onChanged);
        _onChanged();
    }

    protected override void SetItem(int index, T item)
    {
        var oldItem = this[index];
        base.SetItem(index, item);
        _tracker.HandleItemRemoved(oldItem, _onChanged);
        _tracker.HandleItemAdded(item, _onChanged);
        _onChanged();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        _tracker.HandleItemRemoved(item, _onChanged);
        _onChanged();
    }

    protected override void ClearItems()
    {
        if (Count == 0)
            return;
        for (int i = 0; i < Count; i++)
        {
            _tracker.HandleItemRemoved(this[i], _onChanged);
        }

        base.ClearItems();
        _onChanged();
    }
}