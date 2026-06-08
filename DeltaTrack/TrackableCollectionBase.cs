using System.Collections;

namespace DeltaTrack;

public abstract class TrackableCollectionBase(Action onChanged) : IDisposable
{
    protected readonly ChangeTracker Tracker = new(1);

    protected readonly Action OnChangedCallback = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

    protected void NotifyAdded(object item) => Tracker.HandleItemAdded(item, OnChangedCallback);

    protected void NotifyRemoved(object item) => Tracker.HandleItemRemoved(item, OnChangedCallback);

    protected void RaiseChanged() => OnChangedCallback();

    protected void InitializeExistingItems(IEnumerable items) => Tracker.InitializeExistingItems(items, OnChangedCallback);

    public virtual void Dispose() => Tracker.Dispose();
}