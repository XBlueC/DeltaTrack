using DeltaTrack;
using FluentAssertions;

namespace Tests;

/// <summary>
/// 事件和清理功能测试
/// </summary>
public class EventsAndCleanupTests
{
    /// <summary>
    /// 测试脏状态变化事件触发
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Fire_DirtyState_Changed_Event()
    {
        // Arrange
        var model = new SimpleModel();
        var eventCount = 0;
        model.GetChangeTracker().OnChanged += () => eventCount++;

        // Act
        model.Name = "Test Name";
        model.Age = 30;
        model.Name = "Another Name"; // 第二次修改同一字段

        // Assert
        eventCount.Should().Be(3);
    }

    /// <summary>
    /// 测试多次订阅同一事件
    /// </summary>
    [Fact]
    public void Model_Should_Handle_Multiple_Event_Subscriptions()
    {
        // Arrange
        var model = new SimpleModel();
        var eventCount1 = 0;
        var eventCount2 = 0;

        model.GetChangeTracker().OnChanged += () => eventCount1++;
        model.GetChangeTracker().OnChanged += () => eventCount2++;

        // Act
        model.Name = "Test";

        // Assert
        eventCount1.Should().Be(1);
        eventCount2.Should().Be(1);
    }

    /// <summary>
    /// 测试事件订阅和取消订阅
    /// </summary>
    [Fact]
    public void Model_Should_Handle_Event_Unsubscription()
    {
        // Arrange
        var model = new SimpleModel();
        var eventCount = 0;
        Action handler = () => eventCount++;

        model.GetChangeTracker().OnChanged += handler;
        model.Name = "Test1"; // 触发事件

        // Act
        model.GetChangeTracker().OnChanged -= handler;
        model.Name = "Test2"; // 不应该触发事件

        // Assert
        eventCount.Should().Be(1);
    }

    /// <summary>
    /// 测试集合操作触发事件
    /// </summary>
    [Fact]
    public void Collection_Operations_Should_Fire_Events()
    {
        // Arrange
        var model = new CollectionModel();
        var eventCount = 0;
        model.GetChangeTracker().OnChanged += () => eventCount++;

        // Act
        model.Tags.Add("Tag1");
        model.Tags.Add("Tag2");
        model.Tags.Remove("Tag1");
        model.Metadata["Key"] = "Value";

        // Assert
        eventCount.Should().Be(4);
    }

    /// <summary>
    /// 测试嵌套对象事件传播
    /// </summary>
    [Fact]
    public void Nested_Objects_Should_Propagate_Events()
    {
        // Arrange
        var parent = new NestedModel();
        var child = new SimpleModel();
        var parentEventCount = 0;
        var childEventCount = 0;

        parent.GetChangeTracker().OnChanged += () => parentEventCount++;
        child.GetChangeTracker().OnChanged += () => childEventCount++;

        parent.Child = child;

        // Act
        child.Name = "Child Name";

        // Assert
        childEventCount.Should().Be(1);
        parentEventCount.Should().Be(2); // 父对象应该收到2个事件：Child属性变更 + 子对象脏状态传播
    }

    /// <summary>
    /// 测试清理后重新变脏的事件
    /// </summary>
    [Fact]
    public void Model_Should_Fire_Events_After_Cleanup()
    {
        // Arrange
        var model = new SimpleModel();
        var eventCount = 0;
        model.GetChangeTracker().OnChanged += () => eventCount++;

        model.Name = "Initial";
        model.MarkClean();

        // Act
        model.Name = "New Value";

        // Assert
        eventCount.Should().Be(2); // 初始设置 + 清理后的重新设置
    }

    /// <summary>
    /// 测试复杂清理场景
    /// </summary>
    [Fact]
    public void Complex_Model_Should_Clean_Completely()
    {
        // Arrange
        var model = new ComplexModel();
        var contact = new SimpleModel();
        var section = new NestedModel();
        var child = new SimpleModel();

        model.PrimaryContact = contact;
        model.Sections["main"] = section;
        section.Children.Add(child);

        // 脏化所有对象
        model.Title = "Title";
        contact.Name = "Contact";
        section.Child.Name = "Section Child";
        child.Age = 25;

        // Act
        model.MarkClean(recursive: true);

        // Assert
        model.HasChanges().Should().BeFalse();
        contact.HasChanges().Should().BeFalse();
        section.HasChanges().Should().BeFalse();
        child.HasChanges().Should().BeFalse();

        model.GetChangedProperties().Should().BeEmpty();
        contact.GetChangedProperties().Should().BeEmpty();
        section.GetChangedProperties().Should().BeEmpty();
        child.GetChangedProperties().Should().BeEmpty();
    }

    /// <summary>
    /// 测试部分清理场景
    /// </summary>
    [Fact]
    public void Complex_Model_Should_Clean_Selectively()
    {
        // Arrange
        var model = new ComplexModel();
        var contact = new SimpleModel();
        model.PrimaryContact = contact;

        model.Title = "Title";
        contact.Name = "Contact";

        // Act - 只清理顶层
        model.MarkClean(recursive: false);

        // Assert
        model.HasChanges().Should().BeFalse();
        contact.HasChanges().Should().BeTrue(); // 子对象仍脏
        contact.GetChangedProperties().Should().Contain("Name");
    }

    /// <summary>
    /// 测试空集合的清理行为
    /// </summary>
    [Fact]
    public void Empty_Collections_Should_Clean_Properly()
    {
        // Arrange
        var model = new CollectionModel();
        // 不对集合进行任何操作，保持空状态

        // Act
        model.MarkClean();

        // Assert
        model.HasChanges().Should().BeFalse();
        model.GetChangedProperties().Should().BeEmpty();
    }

    /// <summary>
    /// 测试null值处理
    /// </summary>
    [Fact]
    public void Model_Should_Handle_Null_Values_Gracefully()
    {
        // Arrange
        var model = new NestedModel();

        // Act & Assert - 不应该抛出异常
        model.Child = null;
        var exception = Record.Exception(() => model.HasChanges());
        exception.Should().BeNull();
    }

    /// <summary>
    /// 测试重复字段标记
    /// </summary>
    [Fact]
    public void Model_Should_Handle_Duplicate_Field_Marking()
    {
        // Arrange
        var model = new SimpleModel();

        // Act
        model.MarkChanged("TestField");
        model.MarkChanged("TestField"); // 重复标记
        model.MarkChanged("AnotherField");

        // Assert
        model.GetChangedProperties().Should().HaveCount(2);
        model.GetChangedProperties().Should().Contain("TestField", "AnotherField");
    }

    /// <summary>
    /// 测试事件处理中的异常不会影响其他订阅者
    /// </summary>
    [Fact]
    public void Event_Exceptions_Should_Not_Stop_Other_Handlers()
    {
        // Arrange
        var model = new SimpleModel();
        var normalHandlerCalled = false;
        var exceptionHandlerCalled = false;

        model.GetChangeTracker().OnChanged += () => normalHandlerCalled = true;
        model.GetChangeTracker().OnChanged += () => throw new InvalidOperationException("Test Exception");
        model.GetChangeTracker().OnChanged += () => exceptionHandlerCalled = true;

        // Act & Assert
        var exception = Record.Exception(() => model.Name = "Test");

        // 异常应该被抛出
        exception.Should().NotBeNull();
        exception.Should().BeOfType<InvalidOperationException>();

        // 正常的处理器应该被调用
        normalHandlerCalled.Should().BeTrue();
        // 异常后的处理器不应该被调用
        exceptionHandlerCalled.Should().BeFalse();
    }

    /// <summary>
    /// 测试清理后的事件触发
    /// </summary>
    [Fact]
    public void Model_Should_Fire_Events_After_Being_Cleaned()
    {
        // Arrange
        var model = new SimpleModel();
        var eventCount = 0;
        model.GetChangeTracker().OnChanged += () => eventCount++;

        model.Name = "Test";
        model.MarkClean();

        // Act
        model.Age = 30; // 清理后再次变脏

        // Assert
        eventCount.Should().Be(2); // 第一次设置 + 清理后重新设置
        model.HasChanges().Should().BeTrue();
    }
}