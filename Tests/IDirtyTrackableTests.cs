using DirtyTrackable;
using Xunit;

namespace Tests;

public class IDirtyTrackableTests
{
    [Fact]
    public void IsDirty_ShouldReturnFalse_WhenNoChangesMade()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();

        // Act & Assert
        Assert.False(mockTrackable.IsDirty());
    }

    [Fact]
    public void IsDirty_ShouldReturnTrue_WhenFieldMarkedDirty()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();

        // Act
        mockTrackable.MarkFieldDirty("TestField");

        // Assert
        Assert.True(mockTrackable.IsDirty());
    }

    [Fact]
    public void GetDirtyFields_ShouldReturnEmpty_WhenNoDirtyFields()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();

        // Act
        var dirtyFields = mockTrackable.GetDirtyFields();

        // Assert
        Assert.NotNull(dirtyFields);
        Assert.Empty(dirtyFields);
    }

    [Fact]
    public void GetDirtyFields_ShouldReturnDirtyFieldNames()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();
        mockTrackable.MarkFieldDirty("Field1");
        mockTrackable.MarkFieldDirty("Field2");

        // Act
        var dirtyFields = mockTrackable.GetDirtyFields();

        // Assert
        Assert.Contains("Field1", dirtyFields);
        Assert.Contains("Field2", dirtyFields);
        Assert.Equal(2, dirtyFields.Count);
    }

    [Fact]
    public void MarkFieldDirty_ShouldAddFieldToDirtyList()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();

        // Act
        mockTrackable.MarkFieldDirty("TestField");

        // Assert
        var dirtyFields = mockTrackable.GetDirtyFields();
        Assert.Contains("TestField", dirtyFields);
    }

    [Fact]
    public void MarkFieldDirty_ShouldNotDuplicateFields()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();

        // Act
        mockTrackable.MarkFieldDirty("SameField");
        mockTrackable.MarkFieldDirty("SameField");

        // Assert
        var dirtyFields = mockTrackable.GetDirtyFields();
        Assert.Single(dirtyFields);
        Assert.Contains("SameField", dirtyFields);
    }

    [Fact]
    public void MarkClean_ShouldClearAllDirtyFields()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();
        mockTrackable.MarkFieldDirty("Field1");
        mockTrackable.MarkFieldDirty("Field2");

        // Act
        mockTrackable.MarkClean();

        // Assert
        Assert.False(mockTrackable.IsDirty());
        Assert.Empty(mockTrackable.GetDirtyFields());
    }

    [Fact]
    public void DirtyStateChanged_EventShouldBeRaised_WhenFieldBecomesDirty()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();
        var eventRaised = false;
        mockTrackable.DirtyStateChanged += () => eventRaised = true;

        // Act
        mockTrackable.MarkFieldDirty("TestField");

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void DirtyStateChanged_EventShouldBeRaised_WhenMarkingClean()
    {
        // Arrange
        var mockTrackable = new MockDirtyTrackable();
        mockTrackable.MarkFieldDirty("TestField"); // Make it dirty first
        
        var eventRaised = false;
        mockTrackable.DirtyStateChanged += () => eventRaised = true;

        // Act
        mockTrackable.MarkClean();

        // Assert
        Assert.True(eventRaised);
    }

    // Mock implementation for testing the interface
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