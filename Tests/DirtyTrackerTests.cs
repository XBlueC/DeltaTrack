using DirtyTrackable;
using Xunit;

namespace Tests;

public class DirtyTrackerTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithOwner()
    {
        // Arrange
        var owner = new MockDirtyTrackable();

        // Act
        var tracker = new DirtyTracker(owner);

        // Assert
        Assert.NotNull(tracker);
    }

    [Fact]
    public void Subscribe_ShouldHandleNullItem()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => tracker.Subscribe(null, () => { }));
        Assert.Null(exception);
    }

    [Fact]
    public void Subscribe_ShouldHandleIDirtyTrackable()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var item = new MockDirtyTrackable();
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        // Act
        tracker.Subscribe(item, onChange);
        item.MarkFieldDirty("SubscribedItem");

        // Assert
        Assert.True(onChangeCalled);
    }

    [Fact]
    public void Subscribe_ShouldHandleICollection()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var items = new List<MockDirtyTrackable> 
        { 
            new MockDirtyTrackable(), 
            new MockDirtyTrackable() 
        };
        var onChangeCalled = 0;
        Action onChange = () => onChangeCalled++;

        // Act
        tracker.Subscribe(items, onChange);

        // Trigger changes on all items
        foreach (var item in items)
        {
            item.MarkFieldDirty("CollectionItem");
        }

        // Assert
        Assert.Equal(items.Count, onChangeCalled);
    }

    [Fact]
    public void Subscribe_ShouldHandleIDictionary()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var dict = new Dictionary<string, MockDirtyTrackable>
        {
            ["key1"] = new MockDirtyTrackable(),
            ["key2"] = new MockDirtyTrackable()
        };
        var onChangeCalled = 0;
        Action onChange = () => onChangeCalled++;

        // Act
        tracker.Subscribe(dict, onChange);

        // Trigger changes on all values
        foreach (var item in dict.Values)
        {
            item.MarkFieldDirty("DictionaryItem");
        }

        // Assert
        Assert.Equal(dict.Count, onChangeCalled);
    }

    [Fact]
    public void Subscribe_ShouldIgnoreNonDirtyTrackableItems()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var items = new List<object> { "string", 42, new object() };
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        // Act
        tracker.Subscribe(items, onChange);

        // Assert - Should not throw and onChange should not be called
        Assert.False(onChangeCalled);
    }

    [Fact]
    public void Unsubscribe_ShouldHandleIDirtyTrackable()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var item = new MockDirtyTrackable();
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        tracker.Subscribe(item, onChange);

        // Act
        tracker.Unsubscribe(item, onChange);
        item.MarkFieldDirty("UnsubscribedItem");

        // Assert
        Assert.False(onChangeCalled);
    }

    [Fact]
    public void Unsubscribe_ShouldHandleICollection()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var items = new List<MockDirtyTrackable> { new MockDirtyTrackable() };
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        tracker.Subscribe(items, onChange);

        // Act
        tracker.Unsubscribe(items, onChange);
        items[0].MarkFieldDirty("UnsubscribedCollectionItem");

        // Assert
        Assert.False(onChangeCalled);
    }

    [Fact]
    public void Unsubscribe_ShouldHandleIDictionary()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var dict = new Dictionary<string, MockDirtyTrackable>
        {
            ["key"] = new MockDirtyTrackable()
        };
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        tracker.Subscribe(dict, onChange);

        // Act
        tracker.Unsubscribe(dict, onChange);
        dict["key"].MarkFieldDirty("UnsubscribedDictionaryItem");

        // Assert
        Assert.False(onChangeCalled);
    }

    [Fact]
    public void Subscribe_Unsubscribe_ShouldWorkWithNestedCollections()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var nestedList = new List<List<MockDirtyTrackable>>
        {
            new List<MockDirtyTrackable> { new MockDirtyTrackable() },
            new List<MockDirtyTrackable> { new MockDirtyTrackable(), new MockDirtyTrackable() }
        };
        var totalItems = nestedList.Sum(list => list.Count);
        var onChangeCalled = 0;
        Action onChange = () => onChangeCalled++;

        // Act - Subscribe
        tracker.Subscribe(nestedList, onChange);

        // Trigger changes on all nested items
        foreach (var list in nestedList)
        {
            foreach (var item in list)
            {
                item.MarkFieldDirty("NestedItem");
            }
        }

        // Assert - All nested items should trigger onChange
        Assert.Equal(totalItems, onChangeCalled);

        // Reset counter
        onChangeCalled = 0;

        // Act - Unsubscribe
        tracker.Unsubscribe(nestedList, onChange);

        // Trigger changes again
        foreach (var list in nestedList)
        {
            foreach (var item in list)
            {
                item.MarkFieldDirty("NestedItem2");
            }
        }

        // Assert - No more onChange calls
        Assert.Equal(0, onChangeCalled);
    }

    [Fact]
    public void Subscribe_ShouldHandleMixedCollectionTypes()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var mixedItems = new List<object>
        {
            new MockDirtyTrackable(), // This should be subscribed
            "string",                 // This should be ignored
            42,                       // This should be ignored
            new MockDirtyTrackable()  // This should be subscribed
        };
        var onChangeCalled = 0;
        Action onChange = () => onChangeCalled++;

        // Act
        tracker.Subscribe(mixedItems, onChange);

        // Trigger changes only on DirtyTrackable items
        foreach (var item in mixedItems.OfType<MockDirtyTrackable>())
        {
            item.MarkFieldDirty("MixedItem");
        }

        // Assert
        Assert.Equal(2, onChangeCalled); // Only 2 DirtyTrackable items
    }

    [Fact]
    public void Subscribe_ShouldHandleEmptyCollections()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var emptyList = new List<MockDirtyTrackable>();
        var emptyDict = new Dictionary<string, MockDirtyTrackable>();
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        // Act & Assert - Should not throw
        var exception1 = Record.Exception(() => tracker.Subscribe(emptyList, onChange));
        var exception2 = Record.Exception(() => tracker.Subscribe(emptyDict, onChange));

        Assert.Null(exception1);
        Assert.Null(exception2);
        Assert.False(onChangeCalled);
    }

    [Fact]
    public void Unsubscribe_ShouldHandleEmptyCollections()
    {
        // Arrange
        var owner = new MockDirtyTrackable();
        var tracker = new DirtyTracker(owner);
        var emptyList = new List<MockDirtyTrackable>();
        var emptyDict = new Dictionary<string, MockDirtyTrackable>();
        var onChangeCalled = false;
        Action onChange = () => onChangeCalled = true;

        // Act & Assert - Should not throw
        var exception1 = Record.Exception(() => tracker.Unsubscribe(emptyList, onChange));
        var exception2 = Record.Exception(() => tracker.Unsubscribe(emptyDict, onChange));

        Assert.Null(exception1);
        Assert.Null(exception2);
        Assert.False(onChangeCalled);
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