using System.Collections;
using System.Globalization;

namespace DeltaTrack;

public class ChangeTracker : IDisposable
{
    private readonly Dictionary<ITrackable, (int Count, Action OnChange)> _childSubscriptions = new();
    private bool _disposed;
    private int _suspendCount;

    public ChangeTracker(int slotCount)
    {
        if (slotCount < 1) slotCount = 1;
        DirtyFlagsArray = new long[slotCount];
    }

    public bool HasChanges()
    {
        for (var i = 0; i < DirtyFlagsArray.Length; i++)
        {
            if (DirtyFlagsArray[i] != 0) return true;
        }

        return false;
    }

    public long[] DirtyFlagsArray { get; }

    public int SlotCount => DirtyFlagsArray.Length;

    public bool IsSuspended => _suspendCount > 0;

    public void MarkChanged(int slot, long flag) => MarkChanged(slot, flag, null);


    public void MarkChanged(int slot, long flag, ITrackable source)
    {
        if (_suspendCount > 0) return;
        if ((uint)slot >= (uint)DirtyFlagsArray.Length) return;
        DirtyFlagsArray[slot] |= flag;
        OnChanged?.Invoke();
        if (source != null)
            OnChangedDetailed?.Invoke(new ChangeInfo(slot, flag, source));
    }

    public void MarkClean(bool recursive = false)
    {
        OnClean?.Invoke(recursive);
        for (var i = 0; i < DirtyFlagsArray.Length; i++)
            DirtyFlagsArray[i] = 0;
    }

    public IDisposable SuspendTracking() => new SuspendScope(this);

    private sealed class SuspendScope : IDisposable
    {
        private ChangeTracker _tracker;

        public SuspendScope(ChangeTracker tracker)
        {
            _tracker = tracker;
            tracker._suspendCount++;
        }

        public void Dispose()
        {
            var t = Interlocked.Exchange(ref _tracker, null);
            if (t != null && t._suspendCount > 0)
                t._suspendCount--;
        }
    }

    public event Action OnChanged;
    public event Action<bool> OnClean;
    public event Action<ChangeInfo> OnChangedDetailed;

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
            child.OnChanged += onChange;
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
                child.OnChanged -= existing.OnChange;
            }
            else
            {
                _childSubscriptions[child] = (existing.Count - 1, existing.OnChange);
            }
        }
    }

    public void HandleItemAdded(object item, Action onChange)
    {
        if (item is ITrackable trackable)
        {
            SubscribeChild(trackable, onChange);
        }
        else if (item is IEnumerable enumerable && item is not string)
        {
            foreach (var element in enumerable)
            {
                HandleItemAdded(element, onChange);
            }
        }
    }

    public void HandleItemRemoved(object item, Action onChange)
    {
        if (item is ITrackable trackable)
        {
            UnsubscribeChild(trackable, onChange);
        }
        else if (item is IEnumerable enumerable && item is not string)
        {
            foreach (var element in enumerable)
            {
                HandleItemRemoved(element, onChange);
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
        if (item is ITrackable trackable)
            trackable.OnChanged += onChange;
    }

    public void Unsubscribe(object item, Action onChange)
    {
        if (item == null) return;
        if (item is ITrackable trackable)
            trackable.OnChanged -= onChange;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _childSubscriptions)
        {
            entry.Key.OnChanged -= entry.Value.OnChange;
        }

        _childSubscriptions.Clear();
        for (var i = 0; i < DirtyFlagsArray.Length; i++)
            DirtyFlagsArray[i] = 0;

        OnChanged = null;
        OnClean = null;
        OnChangedDetailed = null;
    }

    public static T DeltaCast<T>(object value)
    {
        if (value == null) return default;
        if (value is T t) return t;

        var targetType = typeof(T);
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying.IsEnum)
        {
            if (value is string s) return (T)Enum.Parse(underlying, s, ignoreCase: true);
            return (T)Enum.ToObject(underlying, value);
        }

        if (value is IConvertible)
        {
            return (T)Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }

        return (T)value;
    }
}