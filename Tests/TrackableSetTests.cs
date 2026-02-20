using System.ComponentModel;
using DirtyTrackable;
using Xunit;

namespace Tests;

public class TrackableSetTests
{
    [Fact]
    public void Constructor_Default_ShouldCreateEmptySet()
    {
        // Arrange & Act
        var set = new TrackableSet<string>(() => { });

        // Assert
        Assert.NotNull(set);
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_WithInnerSet_ShouldInitializeCorrectly()
    {
        // Arrange
        var innerSet = new HashSet<string> { "item1", "item2", "item3" };

        // Act
        var set = new TrackableSet<string>(() => { }, innerSet);

        // Assert
        Assert.Equal(3, set.Count);
        Assert.Contains("item1", set);
        Assert.Contains("item2", set);
        Assert.Contains("item3", set);
    }

    [Fact]
    public void Constructor_NullOnChanged_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TrackableSet<string>(null));
    }

    [Fact]
    public void Constructor_NullInnerSet_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TrackableSet<string>(() => { }, null));
    }

    [Fact]
    public void Add_NewItem_ShouldAddAndTriggerChange()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        var result = set.Add("newItem");

        // Assert
        Assert.True(result);
        Assert.True(changed);
        Assert.Single(set);
        Assert.Contains("newItem", set);
    }

    [Fact]
    public void Add_ExistingItem_ShouldReturnFalseAndNotTriggerChange()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("existingItem");
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        var result = set.Add("existingItem");

        // Assert
        Assert.False(result);
        Assert.False(changed);
        Assert.Single(set);
    }

    [Fact]
    public void Add_InterfaceMethod_ShouldWorkSameAsAdd()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        ((ICollection<string>)set).Add("item");

        // Assert
        Assert.True(changed);
        Assert.Single(set);
        Assert.Contains("item", set);
    }

    [Fact]
    public void Remove_ExistingItem_ShouldRemoveAndTriggerChange()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item");
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        var result = set.Remove("item");

        // Assert
        Assert.True(result);
        Assert.True(changed);
        Assert.Empty(set);
    }

    [Fact]
    public void Remove_NonExistentItem_ShouldReturnFalseAndNotTriggerChange()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("existingItem");
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        var result = set.Remove("nonexistent");

        // Assert
        Assert.False(result);
        Assert.False(changed);
        Assert.Single(set);
    }

    [Fact]
    public void Clear_ShouldRemoveAllItemsAndTriggerChange()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        set.Add("item3");
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        set.Clear();

        // Assert
        Assert.True(changed);
        Assert.Empty(set);
    }

    [Fact]
    public void Clear_EmptySet_ShouldNotTriggerChange()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        set.Clear();

        // Assert
        Assert.False(changed);
        Assert.Empty(set);
    }

    [Fact]
    public void Contains_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");

        // Act & Assert
        Assert.True(set.Contains("item1"));
        Assert.False(set.Contains("item2"));
    }

    [Fact]
    public void Count_Property_ShouldReturnCorrectCount()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });

        // Act & Assert
        Assert.Equal(0, set.Count);
        set.Add("item1");
        Assert.Equal(1, set.Count);
        set.Add("item2");
        Assert.Equal(2, set.Count);
        set.Remove("item1");
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void IsReadOnly_Property_ShouldReturnFalse()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });

        // Act & Assert
        Assert.False(set.IsReadOnly);
    }

    [Fact]
    public void CopyTo_ShouldCopyItemsToArray()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        var array = new string[3];

        // Act
        set.CopyTo(array, 0);

        // Assert
        Assert.Contains("item1", array);
        Assert.Contains("item2", array);
        Assert.Null(array[2]);
    }

    [Fact]
    public void GetEnumerator_ShouldEnumerateAllItems()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        var items = new[] { "item1", "item2", "item3" };
        foreach (var item in items)
        {
            set.Add(item);
        }

        // Act
        var enumeratedItems = set.OrderBy(x => x).ToList();

        // Assert
        Assert.Equal(items.OrderBy(x => x), enumeratedItems);
    }

    [Fact]
    public void UnionWith_ShouldAddAllItemsFromOtherCollection()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        var other = new[] { "item2", "item3", "item1" }; // item1 already exists
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        set.UnionWith(other);

        // Assert
        Assert.True(changed);
        Assert.Equal(3, set.Count);
        Assert.Contains("item1", set);
        Assert.Contains("item2", set);
        Assert.Contains("item3", set);
    }

    [Fact]
    public void UnionWith_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => set.UnionWith(null));
    }

    [Fact]
    public void IntersectWith_ShouldKeepOnlyCommonItems()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        set.Add("item3");
        var other = new[] { "item2", "item3", "item4" };
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        set.IntersectWith(other);

        // Assert
        Assert.True(changed);
        Assert.Equal(2, set.Count);
        Assert.Contains("item2", set);
        Assert.Contains("item3", set);
        Assert.DoesNotContain("item1", set);
        Assert.DoesNotContain("item4", set);
    }

    [Fact]
    public void IntersectWith_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null));
    }

    [Fact]
    public void ExceptWith_ShouldRemoveItemsPresentInOtherCollection()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        set.Add("item3");
        var other = new[] { "item2", "item4" };
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        set.ExceptWith(other);

        // Assert
        Assert.True(changed);
        Assert.Equal(2, set.Count);
        Assert.Contains("item1", set);
        Assert.Contains("item3", set);
        Assert.DoesNotContain("item2", set);
    }

    [Fact]
    public void ExceptWith_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => set.ExceptWith(null));
    }

    [Fact]
    public void SymmetricExceptWith_ShouldToggleMembership()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1"); // In set only
        set.Add("item2"); // In both
        var other = new[] { "item2", "item3" }; // item2 in both, item3 in other only
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        set.SymmetricExceptWith(other);

        // Assert
        Assert.True(changed);
        Assert.Equal(2, set.Count);
        Assert.Contains("item1", set); // Kept (in set only)
        Assert.Contains("item3", set); // Added (in other only)
        Assert.DoesNotContain("item2", set); // Removed (in both)
    }

    [Fact]
    public void SymmetricExceptWith_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null));
    }

    [Fact]
    public void IsSubsetOf_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        var superset = new[] { "item1", "item2", "item3" };
        var notSuperset = new[] { "item1", "item3" };

        // Act & Assert
        Assert.True(set.IsSubsetOf(superset));
        Assert.False(set.IsSubsetOf(notSuperset));
    }

    [Fact]
    public void IsSupersetOf_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        set.Add("item3");
        var subset = new[] { "item1", "item2" };
        var notSubset = new[] { "item1", "item4" };

        // Act & Assert
        Assert.True(set.IsSupersetOf(subset));
        Assert.False(set.IsSupersetOf(notSubset));
    }

    [Fact]
    public void IsProperSupersetOf_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        var properSubset = new[] { "item1" };
        var equalSet = new[] { "item1", "item2" };
        var notSubset = new[] { "item1", "item3" };

        // Act & Assert
        Assert.True(set.IsProperSupersetOf(properSubset));
        Assert.False(set.IsProperSupersetOf(equalSet)); // Not proper (equal)
        Assert.False(set.IsProperSupersetOf(notSubset));
    }

    [Fact]
    public void IsProperSubsetOf_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        var properSuperset = new[] { "item1", "item2" };
        var equalSet = new[] { "item1" };
        var notSuperset = new[] { "item2", "item3" };

        // Act & Assert
        Assert.True(set.IsProperSubsetOf(properSuperset));
        Assert.False(set.IsProperSubsetOf(equalSet)); // Not proper (equal)
        Assert.False(set.IsProperSubsetOf(notSuperset));
    }

    [Fact]
    public void Overlaps_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        var overlapping = new[] { "item2", "item3" };
        var nonOverlapping = new[] { "item3", "item4" };

        // Act & Assert
        Assert.True(set.Overlaps(overlapping));
        Assert.False(set.Overlaps(nonOverlapping));
    }

    [Fact]
    public void SetEquals_ShouldReturnCorrectResult()
    {
        // Arrange
        var set = new TrackableSet<string>(() => { });
        set.Add("item1");
        set.Add("item2");
        var equalSet = new[] { "item1", "item2" };
        var differentSet = new[] { "item1", "item3" };
        var superset = new[] { "item1", "item2", "item3" };

        // Act & Assert
        Assert.True(set.SetEquals(equalSet));
        Assert.False(set.SetEquals(differentSet));
        Assert.False(set.SetEquals(superset));
    }

    [Fact]
    public void DirtyTracking_ShouldWorkWithIDirtyTrackableItems()
    {
        // Arrange
        var set = new TrackableSet<MockDirtyTrackable>(() => { });
        var item = new MockDirtyTrackable();
        var parentChanged = false;
        set.DirtyStateChanged += () => parentChanged = true;

        // Act - Add item
        set.Add(item);
        var isDirtyAfterAdd = set.IsDirty();

        // Change item
        item.MarkFieldDirty("ChildField");
        var isDirtyAfterChildChange = set.IsDirty();

        // Clean item
        item.MarkClean();
        var isDirtyAfterChildClean = set.IsDirty();

        // Assert
        Assert.False(isDirtyAfterAdd); // Adding item doesn't make set dirty
        Assert.True(isDirtyAfterChildChange); // Child becoming dirty makes set dirty
        Assert.False(isDirtyAfterChildClean); // Cleaning child makes set clean
        Assert.True(parentChanged); // Parent should be notified of child changes
    }

    [Fact]
    public void MarkClean_ShouldCleanAllDirtyItems()
    {
        // Arrange
        var set = new TrackableSet<MockDirtyTrackable>(() => { });
        var item1 = new MockDirtyTrackable();
        var item2 = new MockDirtyTrackable();
        
        set.Add(item1);
        set.Add(item2);
        
        item1.MarkFieldDirty("Field1");
        item2.MarkFieldDirty("Field2");

        // Act
        set.MarkClean(recursive: true);

        // Assert
        Assert.False(item1.IsDirty());
        Assert.False(item2.IsDirty());
        Assert.False(set.IsDirty());
    }

    [Fact]
    public void GetDirtyFields_ShouldIncludeChildDirtyFields()
    {
        // Arrange
        var set = new TrackableSet<MockDirtyTrackable>(() => { });
        var item = new MockDirtyTrackable();
        set.Add(item);

        // Act
        item.MarkFieldDirty("ChildField");
        var dirtyFields = set.GetDirtyFields();

        // Assert
        Assert.NotEmpty(dirtyFields);
    }

    [Fact]
    public void InitialItems_ShouldBeTrackedForChanges()
    {
        // Arrange
        var innerSet = new HashSet<MockDirtyTrackable>
        {
            new MockDirtyTrackable(),
            new MockDirtyTrackable()
        };
        var set = new TrackableSet<MockDirtyTrackable>(() => { }, innerSet);
        var changed = false;
        set.DirtyStateChanged += () => changed = true;

        // Act
        innerSet.First().MarkFieldDirty("InitialItemField");

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