using FluentAssertions;

namespace Tests;

/// <summary>
/// 嵌套对象跟踪功能测试
/// </summary>
public class NestedObjectTrackingTests
{
    /// <summary>
    /// 测试简单嵌套对象的属性跟踪
    /// </summary>
    [Fact]
    public void NestedModel_Should_Track_Child_Object_Changes()
    {
        // Arrange
        var model = new NestedModel();
        
        // Act
        model.Child.Name = "Child Name";
        model.Child.Age = 10;
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeTrue();
        model.GetChangeTracker().GetChangedFields().Should().Contain("Child");
        model.Child.GetChangeTracker().IsChanged().Should().BeTrue();
        model.Child.GetChangeTracker().GetChangedFields().Should().Contain("Name", "Age");
    }

    /// <summary>
    /// 测试嵌套对象列表的跟踪
    /// </summary>
    [Fact]
    public void NestedModel_Should_Track_Children_List_Changes()
    {
        // Arrange
        var model = new NestedModel();
        var child1 = new SimpleModel();
        var child2 = new SimpleModel();
        
        // Act
        model.Children.Add(child1);
        model.Children.Add(child2);
        child1.Name = "First Child";
        child2.Age = 5;
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeTrue();
        model.GetChangeTracker().GetChangedFields().Should().Contain("Children");
        model.Children.Should().HaveCount(2);
        
        // 验证子对象的脏状态
        child1.GetChangeTracker().IsChanged().Should().BeTrue();
        child1.GetChangeTracker().GetChangedFields().Should().Contain("Name");
        child2.GetChangeTracker().IsChanged().Should().BeTrue();
        child2.GetChangeTracker().GetChangedFields().Should().Contain("Age");
    }

    /// <summary>
    /// 测试命名子对象字典的跟踪
    /// </summary>
    [Fact]
    public void NestedModel_Should_Track_Named_Children_Changes()
    {
        // Arrange
        var model = new NestedModel();
        var child1 = new SimpleModel();
        var child2 = new SimpleModel();
        
        // Act
        model.NamedChildren["first"] = child1;
        model.NamedChildren["second"] = child2;
        child1.Name = "First Named Child";
        child2.IsActive = true;
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeTrue();
        model.GetChangeTracker().GetChangedFields().Should().Contain("NamedChildren");
        model.NamedChildren.Should().ContainKeys("first", "second");
        
        // 验证子对象的脏状态
        child1.GetChangeTracker().IsChanged().Should().BeTrue();
        child1.GetChangeTracker().GetChangedFields().Should().Contain("Name");
        child2.GetChangeTracker().IsChanged().Should().BeTrue();
        child2.GetChangeTracker().GetChangedFields().Should().Contain("IsActive");
    }

    /// <summary>
    /// 测试复杂嵌套场景的跟踪
    /// </summary>
    [Fact]
    public void ComplexModel_Should_Handle_Deep_Nesting()
    {
        // Arrange
        var model = new ComplexModel();
        var section = new NestedModel();
        var child = new SimpleModel();
        
        // Act
        model.Title = "Main Title";
        model.Sections["main"] = section;
        section.Children.Add(child);
        child.Name = "Deep Nested Child";
        child.Age = 15;
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeTrue();
        model.GetChangeTracker().GetChangedFields().Should().Contain("Title", "Sections");
        
        section.GetChangeTracker().IsChanged().Should().BeTrue();
        section.GetChangeTracker().GetChangedFields().Should().Contain("Children");
        
        child.GetChangeTracker().IsChanged().Should().BeTrue();
        child.GetChangeTracker().GetChangedFields().Should().Contain("Name", "Age");
    }

    /// <summary>
    /// 测试嵌套对象替换的跟踪
    /// </summary>
    [Fact]
    public void NestedModel_Should_Track_Object_Replacement()
    {
        // Arrange
        var model = new NestedModel();
        var oldChild = new SimpleModel();
        var newChild = new SimpleModel();
        
        model.Child = oldChild;
        oldChild.Name = "Old Child";
        
        // Act
        model.Child = newChild;
        newChild.Age = 20;
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeTrue();
        model.GetChangeTracker().GetChangedFields().Should().Contain("Child");
        
        // 验证新旧对象的状态
        oldChild.GetChangeTracker().IsChanged().Should().BeTrue(); // 仍然保持脏状态
        newChild.GetChangeTracker().IsChanged().Should().BeTrue();
        newChild.GetChangeTracker().GetChangedFields().Should().Contain("Age");
    }

    /// <summary>
    /// 测试嵌套集合中对象的跟踪
    /// </summary>
    [Fact]
    public void Nested_Collections_Should_Track_Nested_Objects()
    {
        // Arrange
        var model = new NestedModel();
        var child = new SimpleModel();
        
        // Act
        model.Children.Add(child);
        child.Name = "Nested Collection Child";
        
        // 然后从集合中移除
        model.Children.RemoveAt(0);
        child.Age = 25; // 即使移除了也应该还能跟踪
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeTrue();
        model.GetChangeTracker().GetChangedFields().Should().Contain("Children");
        child.GetChangeTracker().IsChanged().Should().BeTrue();
        child.GetChangeTracker().GetChangedFields().Should().Contain("Name", "Age");
    }

    /// <summary>
    /// 测试递归清理嵌套对象
    /// </summary>
    [Fact]
    public void NestedModel_Should_Clean_Recursively()
    {
        // Arrange
        var model = new NestedModel();
        var child = new SimpleModel();
        model.Child = child;
        model.Children.Add(child);
        
        child.Name = "Test Child";
        child.Age = 30;
        
        // Act
        model.GetChangeTracker().MarkClean(recursive: true);
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeFalse();
        model.GetChangeTracker().GetChangedFields().Should().BeEmpty();
        child.GetChangeTracker().IsChanged().Should().BeFalse();
        child.GetChangeTracker().GetChangedFields().Should().BeEmpty();
    }

    /// <summary>
    /// 测试部分清理嵌套对象（非递归）
    /// </summary>
    [Fact]
    public void NestedModel_Should_Clean_Without_Recursive()
    {
        // Arrange
        var model = new NestedModel();
        var child = new SimpleModel();
        model.Child = child;
        child.Name = "Test Child";
        
        // Act
        model.GetChangeTracker().MarkClean(recursive: false); // 只清理自身
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeFalse();
        model.GetChangeTracker().GetChangedFields().Should().BeEmpty();
        // 子对象仍应该是脏的
        child.GetChangeTracker().IsChanged().Should().BeTrue();
        child.GetChangeTracker().GetChangedFields().Should().Contain("Name");
    }

    /// <summary>
    /// 测试深层嵌套对象的独立性
    /// </summary>
    [Fact]
    public void Deep_Nested_Objects_Should_Be_Independent()
    {
        // Arrange
        var topLevel = new ComplexModel();
        var section = new NestedModel();
        var child = new SimpleModel();
        
        topLevel.Sections["test"] = section;
        section.Children.Add(child);
        
        // Act - 分别修改不同层级
        topLevel.Title = "Top Level";
        section.Child.Name = "Section Child";
        child.Age = 25;
        
        // Assert - 各层级独立跟踪
        topLevel.GetChangeTracker().IsChanged().Should().BeTrue();
        topLevel.GetChangeTracker().GetChangedFields().Should().Contain("Title", "Sections");
        
        section.GetChangeTracker().IsChanged().Should().BeTrue();
        section.GetChangeTracker().GetChangedFields().Should().Contain("Child", "Children");
        
        child.GetChangeTracker().IsChanged().Should().BeTrue();
        child.GetChangeTracker().GetChangedFields().Should().Contain("Age");
    }

    /// <summary>
    /// 测试嵌套对象初始化状态
    /// </summary>
    [Fact]
    public void Nested_Objects_Should_Start_Clean()
    {
        // Arrange & Act
        var model = new NestedModel();
        var child = new SimpleModel();
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeFalse();
        model.GetChangeTracker().GetChangedFields().Should().BeEmpty();
        child.GetChangeTracker().IsChanged().Should().BeFalse();
        child.GetChangeTracker().GetChangedFields().Should().BeEmpty();
    }

    /// <summary>
    /// 测试嵌套对象与集合的混合使用
    /// </summary>
    [Fact]
    public void Mixed_Nested_And_Collection_Usage_Should_Work()
    {
        // Arrange
        var model = new ComplexModel();
        var contact = new SimpleModel();
        var section = new NestedModel();
        
        // Act
        model.PrimaryContact = contact;
        contact.Name = "Primary Contact";
        
        model.Sections["main"] = section;
        section.Child = contact; // 同一个对象在不同位置使用
        
        model.Categories.Add("Category1");
        
        // Assert
        model.GetChangeTracker().IsChanged().Should().BeTrue();
        model.GetChangeTracker().GetChangedFields().Should().Contain("PrimaryContact", "Sections", "Categories");
        
        contact.GetChangeTracker().IsChanged().Should().BeTrue();
        contact.GetChangeTracker().GetChangedFields().Should().Contain("Name");
        
        section.GetChangeTracker().IsChanged().Should().BeTrue();
        section.GetChangeTracker().GetChangedFields().Should().Contain("Child");
    }
}