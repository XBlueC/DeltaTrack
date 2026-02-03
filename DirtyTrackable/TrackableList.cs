using System.Collections.ObjectModel;

namespace DirtyTrackable;

public class TrackableList<T> : Collection<T> where T : notnull
{
    private readonly Action _onChanged;
    private readonly Dictionary<T, int> _referenceCount = new();

    public TrackableList(Action onChanged) : base(new List<T>())
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
    }

    public TrackableList(Action onChanged, IEnumerable<T> initialItems) : base(new List<T>(initialItems))
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        foreach (var item in initialItems) TrackItem(item, true);
    }

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        TrackItem(item, true);
        _onChanged();
    }

    protected override void SetItem(int index, T item)
    {
        var oldItem = this[index];
        base.SetItem(index, item);
        TrackItem(oldItem, false);
        TrackItem(item, true);
        _onChanged();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        TrackItem(item, false);
        _onChanged();
    }

    protected override void ClearItems()
    {
        foreach (var item in this) TrackItem(item, false);

        base.ClearItems();
        _onChanged();
    }

    private void TrackItem(T item, bool added)
    {
        if (item is not IDirtyTrackable trackable)
            return;

        if (added)
        {
            if (_referenceCount.TryGetValue(item, out var count))
            {
                _referenceCount[item] = count + 1;
            }
            else
            {
                _referenceCount[item] = 1;
                trackable.DirtyStateChanged += _onChanged;
            }
        }
        else
        {
            if (_referenceCount.TryGetValue(item, out var count))
            {
                if (count <= 1)
                {
                    _referenceCount.Remove(item);
                    trackable.DirtyStateChanged -= _onChanged;
                }
                else
                {
                    _referenceCount[item] = count - 1;
                }
            }
        }
    }
}