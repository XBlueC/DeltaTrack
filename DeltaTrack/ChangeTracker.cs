using System.Collections;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace DeltaTrack;

public class ChangeTracker : IDisposable
{
    public static IReadOnlyDictionary<string, object> EmptyDelta { get; } =
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(0));

    private readonly Dictionary<(ITrackable Child, Action Handler), ChildSubscription> _childSubscriptions =
        new(SubscriptionComparer.Instance);

    private bool _disposed;
    private bool _cleaning;
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
        if (_cleaning) throw new InvalidOperationException("A cycle was detected during recursive tracking cleanup.");
        _cleaning = true;
        try
        {
            OnClean?.Invoke(recursive);
            for (var i = 0; i < DirtyFlagsArray.Length; i++)
                DirtyFlagsArray[i] = 0;
        }
        finally
        {
            _cleaning = false;
        }
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

        var key = (child, onChange);
        if (_childSubscriptions.TryGetValue(key, out var existing))
        {
            existing.Count++;
        }
        else
        {
            var subscription = new ChildSubscription(child, onChange);
            child.OnChanged += subscription.Callback;
            _childSubscriptions.Add(key, subscription);
        }
    }

    private void UnsubscribeChild(ITrackable child, Action onChange)
    {
        if (child == null || _disposed) return;

        var key = (child, onChange);
        if (_childSubscriptions.TryGetValue(key, out var existing))
        {
            if (existing.Count <= 1)
            {
                _childSubscriptions.Remove(key);
                child.OnChanged -= existing.Callback;
            }
            else
            {
                existing.Count--;
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
            HandleEnumerable(enumerable, onChange, true, new HashSet<object>(ElementIdentityComparer<object>.Instance));
        }
    }

    public void HandleItemAdded<T>(T item, Action onChange)
    {
        if (CanContainTrackable<T>()) HandleItemAdded((object)item, onChange);
    }

    public void HandleItemRemoved<T>(T item, Action onChange)
    {
        if (CanContainTrackable<T>()) HandleItemRemoved((object)item, onChange);
    }

    private static bool CanContainTrackable<T>() =>
        !typeof(T).IsValueType || typeof(ITrackable).IsAssignableFrom(typeof(T)) ||
        typeof(IEnumerable).IsAssignableFrom(typeof(T));

    public void HandleItemRemoved(object item, Action onChange)
    {
        if (item is ITrackable trackable)
        {
            UnsubscribeChild(trackable, onChange);
        }
        else if (item is IEnumerable enumerable && item is not string)
        {
            HandleEnumerable(enumerable, onChange, false,
                new HashSet<object>(ElementIdentityComparer<object>.Instance));
        }
    }

    private void HandleEnumerable(IEnumerable items, Action onChange, bool added, HashSet<object> path)
    {
        if (!path.Add(items)) throw new InvalidOperationException("A cycle was detected in a tracked collection.");
        try
        {
            foreach (var item in items is IDictionary dictionary ? dictionary.Values : items)
            {
                if (item is ITrackable child)
                {
                    if (added) SubscribeChild(child, onChange);
                    else UnsubscribeChild(child, onChange);
                }
                else if (item is IEnumerable nested && item is not string)
                    HandleEnumerable(nested, onChange, added, path);
            }
        }
        finally
        {
            path.Remove(items);
        }
    }

    public static void MarkItemsClean(IEnumerable items)
    {
        if (items != null) MarkItemsClean(items, new HashSet<object>(ElementIdentityComparer<object>.Instance));
    }

    private static void MarkItemsClean(IEnumerable items, HashSet<object> path)
    {
        if (!path.Add(items))
            throw new InvalidOperationException("A cycle was detected during collection tracking cleanup.");
        try
        {
            foreach (var item in items is IDictionary dictionary ? dictionary.Values : items)
            {
                if (item is ITrackable child) child.MarkClean(true);
                else if (item is IEnumerable nested && item is not string) MarkItemsClean(nested, path);
            }
        }
        finally
        {
            path.Remove(items);
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
        if (item is ITrackable trackable)
            SubscribeChild(trackable, onChange);
    }

    public void Unsubscribe(object item, Action onChange)
    {
        if (item is ITrackable trackable)
            UnsubscribeChild(trackable, onChange);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var subscription in _childSubscriptions.Values)
        {
            subscription.Child.OnChanged -= subscription.Callback;
        }

        _childSubscriptions.Clear();
        for (var i = 0; i < DirtyFlagsArray.Length; i++)
            DirtyFlagsArray[i] = 0;

        OnChanged = null;
        OnClean = null;
        OnChangedDetailed = null;
    }

    private sealed class ChildSubscription
    {
        private readonly Action _handler;
        private bool _notifying;
        public ITrackable Child { get; }
        public Action Callback { get; }
        public int Count { get; set; } = 1;

        public ChildSubscription(ITrackable child, Action handler)
        {
            Child = child;
            _handler = handler;
            Callback = Notify;
        }

        private void Notify()
        {
            if (_notifying)
                throw new InvalidOperationException("A cycle was detected while propagating a tracked child change.");
            _notifying = true;
            try
            {
                _handler?.Invoke();
            }
            finally
            {
                _notifying = false;
            }
        }
    }

    private sealed class SubscriptionComparer : IEqualityComparer<(ITrackable Child, Action Handler)>
    {
        public static readonly SubscriptionComparer Instance = new();

        public bool Equals((ITrackable Child, Action Handler) x, (ITrackable Child, Action Handler) y) =>
            ReferenceEquals(x.Child, y.Child) && Equals(x.Handler, y.Handler);

        public int GetHashCode((ITrackable Child, Action Handler) key) =>
            unchecked(RuntimeHelpers.GetHashCode(key.Child) * 397 ^ (key.Handler?.GetHashCode() ?? 0));
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