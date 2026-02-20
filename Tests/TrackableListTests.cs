using DirtyTrackable;
using Xunit;

namespace Tests;

public class TrackableListTests
{
    [Fact]
    public void Constructor_Default_ShouldCreateEmptyList()
    {
        // Arrange & Act
        var list = new TrackableList<string>(() => { });

        // Assert
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public void Constructor_WithInitialItems_ShouldInitializeCorrectly()
    {
        // Arrange
        var initialItems = new[] { "item1", "item2", "item3" };

        // Act
        var list = new TrackableList<string>(() => { }, initialItems);

        // Assert
        Assert.Equal(initialItems.Length, list.Count);
        Assert.Equal(initialItems, list);
    }

    [Fact]
    public void Constructor_NullOnChanged_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TrackableList<string>(null));
    }

    [Fact]
    public void Add_ShouldAddItemAndTriggerChange()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        list.Add("newItem");

        // Assert
        Assert.True(changed);
        Assert.Contains("newItem", list);
        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void Insert_ShouldInsertItemAtCorrectPosition()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");
        list.Add("item3");
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        list.Insert(1, "item2");

        // Assert
        Assert.True(changed);
        Assert.Equal(3, list.Count);
        Assert.Equal("item2", list[1]);
        Assert.Equal(new[] { "item1", "item2", "item3" }, list);
    }

    [Fact]
    public void Remove_ShouldRemoveItemAndTriggerChange()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");
        list.Add("item2");
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        var result = list.Remove("item1");

        // Assert
        Assert.True(result);
        Assert.True(changed);
        Assert.Single(list);
        Assert.DoesNotContain("item1", list);
        Assert.Contains("item2", list);
    }

    [Fact]
    public void Remove_NonExistentItem_ShouldReturnFalse()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        var result = list.Remove("nonexistent");

        // Assert
        Assert.False(result);
        Assert.False(changed);
        Assert.Single(list);
    }

    [Fact]
    public void RemoveAt_ShouldRemoveItemAtCorrectIndex()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");
        list.Add("item2");
        list.Add("item3");
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        list.RemoveAt(1);

        // Assert
        Assert.True(changed);
        Assert.Equal(2, list.Count);
        Assert.Equal("item1", list[0]);
        Assert.Equal("item3", list[1]);
    }

    [Fact]
    public void Indexer_Set_ShouldReplaceItemAndTriggerChange()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("oldItem");
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        list[0] = "newItem";

        // Assert
        Assert.True(changed);
        Assert.Single(list);
        Assert.Equal("newItem", list[0]);
    }

    [Fact]
    public void Clear_ShouldRemoveAllItemsAndTriggerChange()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");
        list.Add("item2");
        list.Add("item3");
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        list.Clear();

        // Assert
        Assert.True(changed);
        Assert.Empty(list);
    }

    [Fact]
    public void Clear_EmptyList_ShouldNotTriggerChange()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        list.Clear();

        // Assert
        Assert.False(changed);
        Assert.Empty(list);
    }

    [Fact]
    public void Contains_ShouldReturnCorrectResult()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");

        // Act & Assert
        Assert.True(list.Contains("item1"));
        Assert.False(list.Contains("item2"));
    }

    [Fact]
    public void IndexOf_ShouldReturnCorrectIndex()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");
        list.Add("item2");
        list.Add("item3");

        // Act & Assert
        Assert.Equal(0, list.IndexOf("item1"));
        Assert.Equal(1, list.IndexOf("item2"));
        Assert.Equal(2, list.IndexOf("item3"));
        Assert.Equal(-1, list.IndexOf("nonexistent"));
    }

    [Fact]
    public void CopyTo_ShouldCopyItemsToArray()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        list.Add("item1");
        list.Add("item2");
        var array = new string[3];

        // Act
        list.CopyTo(array, 0);

        // Assert
        Assert.Equal("item1", array[0]);
        Assert.Equal("item2", array[1]);
        Assert.Null(array[2]);
    }



    [Fact]
    public void GetEnumerator_ShouldEnumerateAllItems()
    {
        // Arrange
        var list = new TrackableList<string>(() => { });
        var items = new[] { "item1", "item2", "item3" };
        foreach (var item in items)
        {
            list.Add(item);
        }

        // Act
        var enumeratedItems = list.ToList();

        // Assert
        Assert.Equal(items, enumeratedItems);
    }

    [Fact]
    public void DirtyTracking_ShouldWorkWithIDirtyTrackableItems()
    {
        // Arrange
        var list = new TrackableList<MockDirtyTrackable>(() => { });
        var item = new MockDirtyTrackable();
        var parentChanged = false;
        list.DirtyStateChanged += () => parentChanged = true;

        // Act - Add item
        list.Add(item);
        var isDirtyAfterAdd = list.IsDirty();

        // Change item
        item.MarkFieldDirty("ChildField");
        var isDirtyAfterChildChange = list.IsDirty();

        // Clean item
        item.MarkClean();
        var isDirtyAfterChildClean = list.IsDirty();

        // Assert
        Assert.False(isDirtyAfterAdd); // Adding item doesn't make list dirty
        Assert.True(isDirtyAfterChildChange); // Child becoming dirty makes list dirty
        Assert.False(isDirtyAfterChildClean); // Cleaning child makes list clean
        Assert.True(parentChanged); // Parent should be notified of child changes
    }

    [Fact]
    public void MarkClean_ShouldCleanAllDirtyItems()
    {
        // Arrange
        var list = new TrackableList<MockDirtyTrackable>(() => { });
        var item1 = new MockDirtyTrackable();
        var item2 = new MockDirtyTrackable();
        
        list.Add(item1);
        list.Add(item2);
        
        item1.MarkFieldDirty("Field1");
        item2.MarkFieldDirty("Field2");

        // Act
        list.MarkClean(recursive: true);

        // Assert
        Assert.False(item1.IsDirty());
        Assert.False(item2.IsDirty());
        Assert.False(list.IsDirty());
    }

    [Fact]
    public void GetDirtyFields_ShouldIncludeChildDirtyFields()
    {
        // Arrange
        var list = new TrackableList<MockDirtyTrackable>(() => { });
        var item = new MockDirtyTrackable();
        list.Add(item);

        // Act
        item.MarkFieldDirty("ChildField");
        var dirtyFields = list.GetDirtyFields();

        // Assert
        Assert.NotEmpty(dirtyFields);
    }

    [Fact]
    public void InitialItems_ShouldBeTrackedForChanges()
    {
        // Arrange
        var initialItems = new[] { new MockDirtyTrackable(), new MockDirtyTrackable() };
        var list = new TrackableList<MockDirtyTrackable>(() => { }, initialItems);
        var changed = false;
        list.DirtyStateChanged += () => changed = true;

        // Act
        initialItems[0].MarkFieldDirty("InitialItemField");

        // Assert
        Assert.True(changed);
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