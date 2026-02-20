using DirtyTrackable;
using Xunit;

namespace Tests;

public class TrackableDictionaryTests
{
    [Fact]
    public void Constructor_Default_ShouldCreateEmptyDictionary()
    {
        // Arrange & Act
        var dict = new TrackableDictionary<string, string>(() => { });

        // Assert
        Assert.NotNull(dict);
        Assert.Empty(dict);
    }

    [Fact]
    public void Constructor_WithInnerDictionary_ShouldInitializeCorrectly()
    {
        // Arrange
        var innerDict = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        var dict = new TrackableDictionary<string, string>(() => { }, innerDict);

        // Assert
        Assert.Equal(2, dict.Count);
        Assert.Equal("value1", dict["key1"]);
        Assert.Equal("value2", dict["key2"]);
    }

    [Fact]
    public void Constructor_NullOnChanged_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TrackableDictionary<string, string>(null));
    }

    [Fact]
    public void Constructor_NullInnerDictionary_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TrackableDictionary<string, string>(() => { }, null));
    }

    [Fact]
    public void Indexer_Get_ShouldReturnValue()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict.Add("key", "value");

        // Act
        var value = dict["key"];

        // Assert
        Assert.Equal("value", value);
    }

    [Fact]
    public void Indexer_Set_NewKey_ShouldAddNewItemAndTriggerChange()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        dict["newKey"] = "newValue";

        // Assert
        Assert.True(changed);
        Assert.Single(dict);
        Assert.Equal("newValue", dict["newKey"]);
    }

    [Fact]
    public void Indexer_Set_ExistingKey_ShouldReplaceValueAndTriggerChange()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "oldValue";
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        dict["key"] = "newValue";

        // Assert
        Assert.True(changed);
        Assert.Single(dict);
        Assert.Equal("newValue", dict["key"]);
    }

    [Fact]
    public void Add_KeyValuePair_ShouldAddItemAndTriggerChange()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        dict.Add(new KeyValuePair<string, string>("key", "value"));

        // Assert
        Assert.True(changed);
        Assert.Single(dict);
        Assert.Equal("value", dict["key"]);
    }

    [Fact]
    public void Add_KeyValue_ShouldAddItemAndTriggerChange()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        dict.Add("key", "value");

        // Assert
        Assert.True(changed);
        Assert.Single(dict);
        Assert.Equal("value", dict["key"]);
    }

    [Fact]
    public void Remove_Key_ShouldRemoveItemAndTriggerChange()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "value";
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        var result = dict.Remove("key");

        // Assert
        Assert.True(result);
        Assert.True(changed);
        Assert.Empty(dict);
    }

    [Fact]
    public void Remove_KeyValuePair_ShouldRemoveMatchingItem()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "value";
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        var result = dict.Remove(new KeyValuePair<string, string>("key", "value"));

        // Assert
        Assert.True(result);
        Assert.True(changed);
        Assert.Empty(dict);
    }

    [Fact]
    public void Remove_KeyValuePair_NonMatchingValue_ShouldReturnFalse()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "value";
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        var result = dict.Remove(new KeyValuePair<string, string>("key", "wrongValue"));

        // Assert
        Assert.False(result);
        Assert.False(changed);
        Assert.Single(dict);
    }

    [Fact]
    public void Remove_NonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "value";
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        var result = dict.Remove("nonexistent");

        // Assert
        Assert.False(result);
        Assert.False(changed);
        Assert.Single(dict);
    }

    [Fact]
    public void Clear_ShouldRemoveAllItemsAndTriggerChange()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key1"] = "value1";
        dict["key2"] = "value2";
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        dict.Clear();

        // Assert
        Assert.True(changed);
        Assert.Empty(dict);
    }

    [Fact]
    public void Clear_EmptyDictionary_ShouldNotTriggerChange()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        dict.Clear();

        // Assert
        Assert.False(changed);
        Assert.Empty(dict);
    }

    [Fact]
    public void ContainsKey_ShouldReturnCorrectResult()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "value";

        // Act & Assert
        Assert.True(dict.ContainsKey("key"));
        Assert.False(dict.ContainsKey("nonexistent"));
    }

    [Fact]
    public void Contains_KeyValuePair_ShouldReturnCorrectResult()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "value";

        // Act & Assert
        Assert.True(dict.Contains(new KeyValuePair<string, string>("key", "value")));
        Assert.False(dict.Contains(new KeyValuePair<string, string>("key", "wrongValue")));
        Assert.False(dict.Contains(new KeyValuePair<string, string>("nonexistent", "value")));
    }

    [Fact]
    public void TryGetValue_ShouldReturnCorrectResult()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key"] = "value";

        // Act
        var found = dict.TryGetValue("key", out var value);
        var notFound = dict.TryGetValue("nonexistent", out var nonValue);

        // Assert
        Assert.True(found);
        Assert.Equal("value", value);
        Assert.False(notFound);
        Assert.Null(nonValue);
    }

    [Fact]
    public void Keys_Property_ShouldReturnAllKeys()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key1"] = "value1";
        dict["key2"] = "value2";

        // Act
        var keys = dict.Keys;

        // Assert
        Assert.Contains("key1", keys);
        Assert.Contains("key2", keys);
        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public void Values_Property_ShouldReturnAllValues()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key1"] = "value1";
        dict["key2"] = "value2";

        // Act
        var values = dict.Values;

        // Assert
        Assert.Contains("value1", values);
        Assert.Contains("value2", values);
        Assert.Equal(2, values.Count);
    }

    [Fact]
    public void Count_Property_ShouldReturnCorrectCount()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });

        // Act & Assert
        Assert.Equal(0, dict.Count);
        dict["key"] = "value";
        Assert.Equal(1, dict.Count);
        dict["key2"] = "value2";
        Assert.Equal(2, dict.Count);
    }



    [Fact]
    public void CopyTo_ShouldCopyKeyValuePairsToArray()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key1"] = "value1";
        dict["key2"] = "value2";
        var array = new KeyValuePair<string, string>[3];

        // Act
        dict.CopyTo(array, 0);

        // Assert
        Assert.Contains(array, kvp => kvp.Key == "key1" && kvp.Value == "value1");
        Assert.Contains(array, kvp => kvp.Key == "key2" && kvp.Value == "value2");
    }

    [Fact]
    public void GetEnumerator_ShouldEnumerateAllKeyValuePairs()
    {
        // Arrange
        var dict = new TrackableDictionary<string, string>(() => { });
        dict["key1"] = "value1";
        dict["key2"] = "value2";

        // Act
        var pairs = dict.ToList();

        // Assert
        Assert.Equal(2, pairs.Count);
        Assert.Contains(pairs, kvp => kvp.Key == "key1" && kvp.Value == "value1");
        Assert.Contains(pairs, kvp => kvp.Key == "key2" && kvp.Value == "value2");
    }

    [Fact]
    public void DirtyTracking_ShouldWorkWithIDirtyTrackableValues()
    {
        // Arrange
        var dict = new TrackableDictionary<string, MockDirtyTrackable>(() => { });
        var value = new MockDirtyTrackable();
        var parentChanged = false;
        dict.DirtyStateChanged += () => parentChanged = true;

        // Act - Add item
        dict["key"] = value;
        var isDirtyAfterAdd = dict.IsDirty();

        // Change value
        value.MarkFieldDirty("ChildField");
        var isDirtyAfterChildChange = dict.IsDirty();

        // Clean value
        value.MarkClean();
        var isDirtyAfterChildClean = dict.IsDirty();

        // Assert
        Assert.False(isDirtyAfterAdd); // Adding item doesn't make dict dirty
        Assert.True(isDirtyAfterChildChange); // Child becoming dirty makes dict dirty
        Assert.False(isDirtyAfterChildClean); // Cleaning child makes dict clean
        Assert.True(parentChanged); // Parent should be notified of child changes
    }

    [Fact]
    public void MarkClean_ShouldCleanAllDirtyValues()
    {
        // Arrange
        var dict = new TrackableDictionary<string, MockDirtyTrackable>(() => { });
        var value1 = new MockDirtyTrackable();
        var value2 = new MockDirtyTrackable();
        
        dict["key1"] = value1;
        dict["key2"] = value2;
        
        value1.MarkFieldDirty("Field1");
        value2.MarkFieldDirty("Field2");

        // Act
        dict.MarkClean(recursive: true);

        // Assert
        Assert.False(value1.IsDirty());
        Assert.False(value2.IsDirty());
        Assert.False(dict.IsDirty());
    }

    [Fact]
    public void GetDirtyFields_ShouldIncludeChildDirtyFields()
    {
        // Arrange
        var dict = new TrackableDictionary<string, MockDirtyTrackable>(() => { });
        var value = new MockDirtyTrackable();
        dict["key"] = value;

        // Act
        value.MarkFieldDirty("ChildField");
        var dirtyFields = dict.GetDirtyFields();

        // Assert
        Assert.NotEmpty(dirtyFields);
    }

    [Fact]
    public void InitialItems_ShouldBeTrackedForChanges()
    {
        // Arrange
        var innerDict = new Dictionary<string, MockDirtyTrackable>
        {
            ["key1"] = new MockDirtyTrackable(),
            ["key2"] = new MockDirtyTrackable()
        };
        var dict = new TrackableDictionary<string, MockDirtyTrackable>(() => { }, innerDict);
        var changed = false;
        dict.DirtyStateChanged += () => changed = true;

        // Act
        innerDict["key1"].MarkFieldDirty("InitialItemField");

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void ReplaceValue_ShouldHandleOldValueUnsubscription()
    {
        // Arrange
        var dict = new TrackableDictionary<string, MockDirtyTrackable>(() => { });
        var oldValue = new MockDirtyTrackable();
        var newValue = new MockDirtyTrackable();
        var changeCount = 0;
        dict.DirtyStateChanged += () => changeCount++;

        dict["key"] = oldValue;
        oldValue.MarkFieldDirty("OldField"); // Should trigger change
        var changesAfterOldDirty = changeCount;

        // Act
        dict["key"] = newValue; // Replace value

        // Old value changes should not trigger anymore
        oldValue.MarkClean();
        oldValue.MarkFieldDirty("OldField2");
        var changesAfterOldReplacement = changeCount;

        // New value changes should trigger
        newValue.MarkFieldDirty("NewField");
        var changesAfterNewDirty = changeCount;

        // Assert
        Assert.Equal(2, changesAfterOldDirty); // Initial add + old dirty
        Assert.Equal(2, changesAfterOldReplacement); // No additional changes
        Assert.Equal(3, changesAfterNewDirty); // One more for new dirty
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