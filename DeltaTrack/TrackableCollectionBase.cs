using System.Collections;

namespace DeltaTrack;

public abstract class TrackableCollectionBase(Action onChanged) : IDisposable
{
    protected readonly ChangeTracker Tracker = new(1);

    protected Action OnChangedCallback = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

    protected void NotifyAdded(object item) => Tracker.HandleItemAdded(item, OnChangedCallback);

    protected void NotifyRemoved(object item) => Tracker.HandleItemRemoved(item, OnChangedCallback);

    protected void NotifyAdded<T>(T item) => Tracker.HandleItemAdded(item, OnChangedCallback);

    protected void NotifyRemoved<T>(T item) => Tracker.HandleItemRemoved(item, OnChangedCallback);

    protected void RaiseChanged() => OnChangedCallback?.Invoke();

    protected void InitializeExistingItems(IEnumerable items) =>
        Tracker.InitializeExistingItems(items, OnChangedCallback);

    protected void InitializeExistingItems<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
            NotifyAdded(item);
    }

    public virtual void Dispose()
    {
        Tracker.Dispose();
        OnChangedCallback = null;
    }
}