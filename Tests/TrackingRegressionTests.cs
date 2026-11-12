using DeltaTrack;

namespace Tests;

public class TrackingRegressionTests
{
    [Theory]
    [InlineData("list")]
    [InlineData("dictionary")]
    [InlineData("set")]
    public void ReplacingCollectionReleasesOldSubscriptions(string kind)
    {
        var (parent, replace) = CreateCollectionOwner(kind);
        var child = new SimpleModel();
        for (var i = 0; i < 100; i++) replace(child);
        parent.MarkClean();
        var count = 0;
        parent.OnChanged += () => count++;

        child.Age++;
        Assert.Equal(1, count);

        replace(null);
        parent.MarkClean();
        count = 0;
        child.Age++;
        Assert.False(parent.HasChanges());
        Assert.Equal(0, count);
    }

    [Fact]
    public void ObsoleteWrapperNoLongerNotifiesOwner()
    {
        var model = new NestedModel();
        var old = model.Children;
        model.Children = new List<SimpleModel>();
        model.MarkClean();

        old.Add(new SimpleModel());
        Assert.False(model.HasChanges());
    }

    [Fact]
    public void AssigningOwnWrapperDoesNotCopyOrMarkDirty()
    {
        var model = new NestedModel();
        var items = model.Children;
        items.Add(new SimpleModel());
        model.MarkClean();

        model.Children = items;

        Assert.Same(items, model.Children);
        Assert.False(model.HasChanges());
    }

    [Fact]
    public void FailedCollectionCopyPreservesExistingSubscriptions()
    {
        var model = new NestedModel();
        var child = new SimpleModel();
        model.Children.Add(child);
        var old = model.Children;

        Assert.Throws<InvalidOperationException>(() => model.Children = new ThrowingList());
        Assert.Same(old, model.Children);
        model.MarkClean();
        child.Age++;
        Assert.True(model.HasChanges());
    }

    [Fact]
    public void InterfaceCollectionsTrackElementsAndMutations()
    {
        var model = new InterfaceCollectionModel();
        var child = new SimpleModel();
        model.Items.Add(child);
        model.NamedItems.Add("child", child);
        model.UniqueItems.Add(child);
        model.MarkClean(true);

        child.Age++;

        Assert.Equal(new[] { "Items", "NamedItems", "UniqueItems" }, model.GetChangedProperties());
        model.Items = new List<SimpleModel> { new() };
        model.NamedItems = new Dictionary<string, SimpleModel> { ["new"] = new() };
        model.UniqueItems = new HashSet<SimpleModel> { new() };
        model.MarkClean(true);
        model.Items[0].Age++;
        model.NamedItems["new"].Age++;
        model.UniqueItems.Single().Age++;
        Assert.Equal(3, model.GetChangedProperties().Count);
    }

    [Fact]
    public void EmptyDeltaIsSharedAndCannotBeModified()
    {
        var model = new SimpleModel();
        var delta = model.GetDelta();
        Assert.Same(delta, model.GetDelta());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object>)delta).Add("Age", 1));
        Assert.Empty(new SimpleModel().GetDelta());
    }

    [Fact]
    public void WarmTrackingPathsDoNotAllocate()
    {
        var scalar = new SimpleModel();
        var clean = new SimpleModel();
        var owner = new NestedModel();
        var child = owner.Child;
        var list = new TrackableList<int>(() => { });
        list.Add(0);

        AssertNoAllocations(() => scalar.Age++);
        AssertNoAllocations(() => _ = clean.GetDelta());
        AssertNoAllocations(() => list[0]++);
        AssertNoAllocations(() => owner.Child = child);
        Assert.False(owner.HasChanges());
    }

    [Fact]
    public void NestedSubscriptionSurvivesThrowingChangeHandler()
    {
        var parent = new NestedModel();
        Action throwing = () => throw new InvalidOperationException();
        parent.OnChanged += throwing;
        var child = new SimpleModel();
        Assert.Throws<InvalidOperationException>(() => parent.Child = child);
        parent.OnChanged -= throwing;
        parent.MarkClean();

        child.Age++;

        Assert.True(parent.HasChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitMarkChangedEmitsDetailedEvent(bool useFlags)
    {
        var model = new SimpleModel();
        ChangeInfo? info = null;
        model.OnChangedDetailed += change => info = change;
        if (useFlags) model.MarkChanged(SimpleModel.DirtyFlag.Age);
        else model.MarkChanged("Age");
        Assert.True(info.HasValue);
        Assert.Same(model, info.Value.Source);
    }

    private static void AssertNoAllocations(Action action)
    {
        for (var i = 0; i < 10_000; i++) action();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) action();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static (ITrackable Parent, Action<SimpleModel> Replace) CreateCollectionOwner(string kind)
    {
        var model = new InterfaceCollectionModel();
        return (model, kind switch
        {
            "list" => child => model.Items = child == null ? null : new List<SimpleModel> { child },
            "dictionary" => child =>
                model.NamedItems = child == null ? null : new Dictionary<string, SimpleModel> { ["key"] = child },
            "set" => child => model.UniqueItems = child == null ? null : new HashSet<SimpleModel> { child },
            _ => throw new ArgumentException(nameof(kind))
        });
    }

    private sealed class ThrowingList : List<SimpleModel>, IList<SimpleModel>
    {
        int ICollection<SimpleModel>.Count => 1;

        void ICollection<SimpleModel>.CopyTo(SimpleModel[] array, int arrayIndex) =>
            throw new InvalidOperationException();

        IEnumerator<SimpleModel> IEnumerable<SimpleModel>.GetEnumerator() => throw new InvalidOperationException();
    }
}

[Trackable]
public partial class InterfaceCollectionModel
{
    private IList<SimpleModel> _items;
    private IDictionary<string, SimpleModel> _namedItems;
    private ISet<SimpleModel> _uniqueItems;
}