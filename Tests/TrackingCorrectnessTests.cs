using DeltaTrack;

namespace Tests;

public class TrackingCorrectnessTests
{
    [Theory]
    [InlineData("query")]
    [InlineData("subscribe")]
    [InlineData("clean")]
    public void ExistingCollectionElementsAreTrackedBeforeReadingProperties(string initialize)
    {
        var child = new SimpleModel();
        var parent = new InitializedCollections(child);
        switch (initialize)
        {
            case "query": Assert.False(parent.HasChanges()); break;
            case "subscribe": parent.OnChanged += () => { }; break;
            case "clean": parent.MarkClean(true); break;
        }

        child.Age++;
        Assert.Equal(new[] { "Items", "Named", "Unique" }, parent.GetChangedProperties());
        parent.MarkClean(true);
        Assert.False(child.HasChanges());
        child.Age++;
        Assert.Equal(3, parent.GetChangedProperties().Count);
    }

    [Fact]
    public void InterfaceAndObjectFieldsTrackRuntimeChildren()
    {
        var child = new SimpleModel();
        var parent = new RuntimeChildren(child);
        parent.HasChanges();
        child.Age++;
        Assert.Equal(new[] { "Contract", "Untyped" }, parent.GetChangedProperties());
        parent.MarkClean(true);
        Assert.False(child.HasChanges());
        parent.Contract = null;
        parent.Untyped = null;
        parent.MarkClean();
        child.Age++;
        Assert.False(parent.HasChanges());
    }

    [Fact]
    public void UntypedCollectionElementsAreRecursivelyCleaned()
    {
        var child = new SimpleModel();
        var parent = new RuntimeChildren(child);
        parent.Items.Add(child);
        parent.Contracts.Add(child);
        parent.Named["child"] = child;
        child.Age++;
        parent.MarkClean(true);
        Assert.False(child.HasChanges());
        child.Age++;
        Assert.Contains("Items", parent.GetChangedProperties());
        Assert.Contains("Contracts", parent.GetChangedProperties());
        Assert.Contains("Named", parent.GetChangedProperties());
    }

    [Fact]
    public void EqualInstancesHaveIndependentSubscriptions()
    {
        var first = new EqualTrackable();
        var second = new EqualTrackable();
        var calls = 0;
        using var list = new TrackableList<EqualTrackable>(() => calls++);
        list.Add(first);
        list.Add(second);
        calls = 0;
        second.Value++;
        Assert.Equal(1, calls);
        list.Remove(second);
        calls = 0;
        second.Value++;
        Assert.Equal(1, calls); // Remove uses collection equality and therefore removed first.
        first.Value++;
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ReferenceCountingIsPerChildAndCallbackAndDisposeDetachesDirectChildren()
    {
        var child = new EqualTrackable();
        var tracker = new ChangeTracker(1);
        var a = 0;
        var b = 0;
        Action first = () => a++;
        Action second = () => b++;
        tracker.Subscribe(child, first);
        tracker.Subscribe(child, first);
        tracker.Subscribe(child, second);
        child.Value++;
        Assert.Equal((1, 1), (a, b));
        tracker.Unsubscribe(child, first);
        child.Value++;
        Assert.Equal((2, 2), (a, b));
        tracker.Unsubscribe(child, first);
        child.Value++;
        Assert.Equal((2, 3), (a, b));
        tracker.Dispose();
        child.Value++;
        Assert.Equal((2, 3), (a, b));
    }

    [Fact]
    public void SharedChildMarksBothFieldsWithoutBeingTreatedAsCycle()
    {
        var child = new SimpleModel();
        var parent = new RuntimeChildren(child);
        parent.MarkClean();
        child.Age++;
        Assert.Equal(new[] { "Contract", "Untyped" }, parent.GetChangedProperties());
    }

    [Fact]
    public void SelfReferenceThrowsAndTrackingRecoversAfterRemovingTheEdge()
    {
        var node = new CorrectnessNode();
        var error = Assert.Throws<InvalidOperationException>(() => node.Next = node);
        Assert.Contains("cycle", error.Message);
        Assert.Throws<InvalidOperationException>(() => node.MarkClean(true));
        node.Next = null;
        node.MarkClean(true);
        node.Value++;
        Assert.True(node.HasChanges());
    }

    [Fact]
    public void TwoNodeCycleThrowsDuringPropagationAndCleanup()
    {
        var first = new CorrectnessNode();
        var second = new CorrectnessNode();
        first.Next = second;
        Assert.Throws<InvalidOperationException>(() => second.Next = first);
        Assert.Throws<InvalidOperationException>(() => first.MarkClean(true));
        second.Next = null;
        first.MarkClean(true);
        Assert.False(first.HasChanges());
        second.Value++;
        Assert.True(first.HasChanges());
    }

    [Fact]
    public void CyclicEnumerableFailsWithoutUnboundedRecursion()
    {
        var items = new List<object>();
        items.Add(items);
        var tracker = new ChangeTracker(1);
        Assert.Throws<InvalidOperationException>(() => tracker.HandleItemAdded(items, () => { }));
        Assert.Throws<InvalidOperationException>(() => ChangeTracker.MarkItemsClean(items));
    }

    [Theory]
    [InlineData("union", false)]
    [InlineData("intersect", false)]
    [InlineData("except", false)]
    [InlineData("symmetric", false)]
    [InlineData("union", true)]
    [InlineData("intersect", true)]
    [InlineData("except", true)]
    [InlineData("symmetric", true)]
    public void SetOperationsUseTheUnderlyingComparer(string operation, bool sorted)
    {
        ISet<string> NewSet() => sorted
            ? new SortedSet<string>(new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase);

        var expected = NewSet();
        var callbacks = 0;
        using var actual = new TrackableSet<string>(() => callbacks++, NewSet());
        var other = new HashSet<string>(new[] { "A", "c" }, StringComparer.Ordinal);
        Apply(expected, other, operation);
        Apply(actual, other, operation);
        Assert.True(expected.SetEquals(actual));
        Assert.Equal(expected.SetEquals(new[] { "a", "b" }) ? 0 : 1, callbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetRemovalUnsubscribesTheStoredInstance(bool sorted)
    {
        var child = new SimpleModel { Age = 7 };
        ISet<SimpleModel> inner = sorted
            ? new SortedSet<SimpleModel>(Comparer<SimpleModel>.Create((a, b) => a.Age.CompareTo(b.Age))) { child }
            : new HashSet<SimpleModel>(new AgeComparer()) { child };
        var callbacks = 0;
        using var set = new TrackableSet<SimpleModel>(() => callbacks++, inner);
        Assert.True(set.Remove(new SimpleModel { Age = 7 }));
        callbacks = 0;
        child.Name = "removed";
        Assert.Equal(0, callbacks);
        Assert.Empty(set);
    }

    [Fact]
    public void BulkOperationsReconcileSubscriptionsEvenWhenEnumerationThrows()
    {
        var child = new SimpleModel();
        var callbacks = 0;
        using var set = new TrackableSet<SimpleModel>(() => callbacks++);

        IEnumerable<SimpleModel> Failing()
        {
            yield return child;
            throw new InvalidOperationException();
        }

        Assert.Throws<InvalidOperationException>(() => set.UnionWith(Failing()));
        Assert.Contains(child, set);
        Assert.Equal(1, callbacks);
        child.Age++;
        Assert.Equal(2, callbacks);
    }

    [Fact]
    public void SetSelfOperationsAreSupported()
    {
        using var set = new TrackableSet<int>(() => { });
        set.Add(1);
        set.UnionWith(set);
        set.IntersectWith(set);
        Assert.Single(set);
        set.ExceptWith(set);
        Assert.Empty(set);
        set.Add(2);
        set.SymmetricExceptWith(set);
        Assert.Empty(set);
    }

    [Fact]
    public void CollectionAssignmentsPreserveComparersIncludingWrappedSources()
    {
        var source = new ComparerCollections();
        source.Named = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["key"] = 1 };
        source.Unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "key" };
        source.Ordered = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "key" };
        source.Sorted = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["key"] = 1 };
        source.Indexed = new SortedList<string, int>(StringComparer.OrdinalIgnoreCase) { ["key"] = 1 };
        var copy = new ComparerCollections
        {
            Named = source.Named, Unique = source.Unique, Ordered = source.Ordered,
            Sorted = source.Sorted, Indexed = source.Indexed,
            ContractSet = source.Ordered, ContractDictionary = source.Indexed
        };
        Assert.True(copy.Named.ContainsKey("KEY"));
        Assert.True(copy.Unique.Contains("KEY"));
        Assert.True(copy.Ordered.Contains("KEY"));
        Assert.True(copy.Sorted.ContainsKey("KEY"));
        Assert.True(copy.Indexed.ContainsKey("KEY"));
        Assert.True(copy.ContractSet.Contains("KEY"));
        Assert.True(copy.ContractDictionary.ContainsKey("KEY"));
        source.Named.Clear();
        Assert.Single(copy.Named);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValueTypeSetNotificationsUseTheSetComparer(bool sorted)
    {
        ISet<CollapsingValue> inner = sorted
            ? new SortedSet<CollapsingValue>(Comparer<CollapsingValue>.Create((a, b) => a.Id.CompareTo(b.Id)))
            : new HashSet<CollapsingValue>(new ValueIdComparer());
        inner.Add(new CollapsingValue(1));
        inner.Add(new CollapsingValue(2));
        var calls = 0;
        using var set = new TrackableSet<CollapsingValue>(() => calls++, inner);
        set.UnionWith(Array.Empty<CollapsingValue>());
        Assert.Equal(0, calls);
        Assert.True(set.Remove(new CollapsingValue(1)));
        Assert.Equal(1, calls);
        set.SymmetricExceptWith(new[] { new CollapsingValue(2) });
        Assert.Equal(2, calls);
        Assert.Empty(set);
    }

    [Fact]
    public void ReferenceComparedSetKeepsValueEqualChildrenDistinct()
    {
        var first = new EqualTrackable();
        var second = new EqualTrackable();
        var calls = 0;
        using var set = new TrackableSet<EqualTrackable>(() => calls++,
            new HashSet<EqualTrackable>(ReferenceEqualityComparer.Instance) { first, second });
        set.IntersectWith(new[] { second });
        Assert.Equal(1, calls);
        calls = 0;
        first.Value++;
        Assert.Equal(0, calls);
        second.Value++;
        Assert.Equal(1, calls);
    }

    private readonly struct CollapsingValue(int id)
    {
        public int Id { get; } = id;
        public override bool Equals(object obj) => obj is CollapsingValue;
        public override int GetHashCode() => 0;
    }

    private sealed class ValueIdComparer : IEqualityComparer<CollapsingValue>
    {
        public bool Equals(CollapsingValue a, CollapsingValue b) => a.Id == b.Id;
        public int GetHashCode(CollapsingValue value) => value.Id;
    }

    [Fact]
    public void UnsupportedComparerCopyThrowsBeforeChangingTheModel()
    {
        var parent = new ComparerCollections();
        var old = parent.Named;
        parent.MarkClean();
        var opaque = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        Assert.Throws<NotSupportedException>(() => parent.Named = opaque);
        Assert.Same(old, parent.Named);
        Assert.False(parent.HasChanges());
    }

    private static void Apply<T>(ISet<T> set, IEnumerable<T> other, string operation)
    {
        switch (operation)
        {
            case "union": set.UnionWith(other); break;
            case "intersect": set.IntersectWith(other); break;
            case "except": set.ExceptWith(other); break;
            case "symmetric": set.SymmetricExceptWith(other); break;
        }
    }

    private sealed class AgeComparer : IEqualityComparer<SimpleModel>
    {
        public bool Equals(SimpleModel a, SimpleModel b) => a?.Age == b?.Age;
        public int GetHashCode(SimpleModel value) => value.Age;
    }
}

[Trackable]
public partial class InitializedCollections
{
    private List<SimpleModel> _items;
    private Dictionary<string, SimpleModel> _named;
    private HashSet<SimpleModel> _unique;

    public InitializedCollections(SimpleModel child)
    {
        _items = new() { child };
        _named = new() { ["child"] = child };
        _unique = new() { child };
    }
}

[Trackable]
public partial class RuntimeChildren
{
    private ITrackable _contract;
    private object _untyped;
    private List<object> _items = new();
    private IList<ITrackable> _contracts = new List<ITrackable>();
    private IDictionary<string, object> _named = new Dictionary<string, object>();

    public RuntimeChildren(SimpleModel child)
    {
        _contract = child;
        _untyped = child;
    }
}

[Trackable]
public partial class EqualTrackable
{
    private int _value;
    public override bool Equals(object obj) => obj is EqualTrackable;
    public override int GetHashCode() => 0;
}

[Trackable]
public partial class CorrectnessNode
{
    private CorrectnessNode _next;
    private int _value;
}

[Trackable]
public partial class ComparerCollections
{
    private Dictionary<string, int> _named = new();
    private HashSet<string> _unique = new();
    private SortedSet<string> _ordered = new();
    private SortedDictionary<string, int> _sorted = new();
    private SortedList<string, int> _indexed = new();
    private ISet<string> _contractSet;
    private IDictionary<string, int> _contractDictionary;
}
