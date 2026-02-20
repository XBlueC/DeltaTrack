using DirtyTrackable;
using Xunit;

namespace Tests;

public class BaseDirtyTrackerTests
{
    [Fact]
    public void IsDirty_ShouldReturnFalse_WhenNoDirtyFieldsOrChildren()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());

        // Act & Assert
        Assert.False(tracker.IsDirty());
    }

    [Fact]
    public void IsDirty_ShouldReturnTrue_WhenHasDirtyFields()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());
        tracker.MarkFieldDirty("TestField");

        // Act & Assert
        Assert.True(tracker.IsDirty());
    }

    [Fact]
    public void GetDirtyFields_ShouldReturnCorrectFields()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());
        tracker.MarkFieldDirty("Field1");
        tracker.MarkFieldDirty("Field2");

        // Act
        var dirtyFields = tracker.GetDirtyFields();

        // Assert
        Assert.Contains("Field1", dirtyFields);
        Assert.Contains("Field2", dirtyFields);
        Assert.Equal(2, dirtyFields.Count);
    }

    [Fact]
    public void MarkFieldDirty_ShouldAddField()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());

        // Act
        tracker.MarkFieldDirty("TestField");

        // Assert
        var dirtyFields = tracker.GetDirtyFields();
        Assert.Contains("TestField", dirtyFields);
    }

    [Fact]
    public void MarkClean_ShouldClearDirtyFields()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());
        tracker.MarkFieldDirty("Field1");
        tracker.MarkFieldDirty("Field2");

        // Act
        tracker.MarkClean();

        // Assert
        Assert.False(tracker.IsDirty());
        Assert.Empty(tracker.GetDirtyFields());
    }

    [Fact]
    public void MarkClean_Recursive_ShouldCleanChildren()
    {
        // Arrange
        var parent = new MockDirtyTrackable();
        var tracker = new TestDirtyTracker(parent);
        var child = new MockDirtyTrackable();
        
        tracker.SubscribeChild(child, () => { });
        child.MarkFieldDirty("ChildField");

        // Act
        tracker.MarkClean(recursive: true);

        // Assert
        Assert.False(child.IsDirty());
        Assert.False(tracker.IsDirty());
    }

    [Fact]
    public void HandleItemAdded_ShouldSubscribeToDirtyTrackableItems()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());
        var item = new MockDirtyTrackable();
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        // Act
        tracker.HandleItemAddedPublic(item, onChange);
        item.MarkFieldDirty("NewItemField");

        // Assert
        Assert.True(onChangeCalled);
    }

    [Fact]
    public void HandleItemRemoved_ShouldUnsubscribeFromDirtyTrackableItems()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());
        var item = new MockDirtyTrackable();
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        tracker.HandleItemAddedPublic(item, onChange);

        // Act
        tracker.HandleItemRemovedPublic(item, onChange);
        item.MarkFieldDirty("RemovedItemField");

        // Assert
        Assert.False(onChangeCalled);
    }

    [Fact]
    public void InitializeExistingItems_ShouldHandleAllItems()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());
        var items = new[] { new MockDirtyTrackable(), new MockDirtyTrackable() };
        var onChangeCalled = 0;
        Action onChange = () => onChangeCalled++;

        // Act
        tracker.InitializeExistingItemsPublic(items, onChange);

        // Trigger changes on all items
        foreach (var item in items)
        {
            item.MarkFieldDirty("InitializedItemField");
        }

        // Assert
        Assert.Equal(items.Length, onChangeCalled);
    }

    [Fact]
    public void SubscribeChild_ShouldHandleNullChild()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => tracker.SubscribeChild(null, () => { }));
        Assert.Null(exception);
    }

    [Fact]
    public void UnsubscribeChild_ShouldHandleNullChild()
    {
        // Arrange
        var tracker = new TestDirtyTracker(new MockDirtyTrackable());

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => tracker.UnsubscribeChild(null, () => { }));
        Assert.Null(exception);
    }

    [Fact]
    public void ChildReferenceCount_ShouldWorkCorrectly()
    {
        // Arrange
        var parent = new MockDirtyTrackable();
        var tracker = new TestDirtyTracker(parent);
        var child = new MockDirtyTrackable();
        var onChange = new Action(() => { });

        // Act - Subscribe multiple times
        tracker.SubscribeChild(child, onChange);
        tracker.SubscribeChild(child, onChange);
        tracker.SubscribeChild(child, onChange);

        child.MarkFieldDirty("RefCountTest");

        // Unsubscribe twice
        tracker.UnsubscribeChild(child, onChange);
        tracker.UnsubscribeChild(child, onChange);

        // Unsubscribe last time
        tracker.UnsubscribeChild(child, onChange);
    }

    // Test implementation that exposes protected members
    private class TestDirtyTracker : DirtyTracker
    {
        private readonly IDirtyTrackable _owner;

        public TestDirtyTracker(IDirtyTrackable owner) : base(owner)
        {
            _owner = owner;
        }

        public new bool IsDirty() => base.IsDirty();
        public new IReadOnlyCollection<string> GetDirtyFields() => base.GetDirtyFields();
        public new void MarkFieldDirty(string field) => base.MarkFieldDirty(field);
        public new void MarkClean(bool recursive = false) => base.MarkClean(recursive);
        public new void SubscribeChild(IDirtyTrackable child, Action onChange) => base.SubscribeChild(child, onChange);
        public new void UnsubscribeChild(IDirtyTrackable child, Action onChange) => base.UnsubscribeChild(child, onChange);
        public new void MarkChildrenClean() => base.MarkChildrenClean();

        // Public wrappers for protected methods
        public void HandleItemAddedPublic(object item, Action onChange, string indexPath = null) =>
            HandleItemAdded(item, onChange, indexPath);

        public void HandleItemRemovedPublic(object item, Action onChange, string indexPath = null) =>
            HandleItemRemoved(item, onChange, indexPath);

        public void InitializeExistingItemsPublic(IEnumerable<object> items, Action onChange) =>
            InitializeExistingItems(items, onChange);
    }

    // Mock implementation for testing
    private class MockDirtyTrackable : IDirtyTrackable
    {
        private readonly HashSet<string> _dirtyFields = new();
        public event Action DirtyStateChanged = () => { };

        public bool IsDirty() => _dirtyFields.Count > 0;

        public IReadOnlyCollection<string> GetDirtyFields() => _dirtyFields.ToList().AsReadOnly();

        public void MarkFieldDirty(string field)
        {
            _dirtyFields.Add(field);
            DirtyStateChanged?.Invoke();
        }

        public void MarkClean(bool recursive = false)
        {
            _dirtyFields.Clear();
            DirtyStateChanged?.Invoke();
        }
    }
}