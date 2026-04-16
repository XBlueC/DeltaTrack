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
        Assert.True(model.HasChanges());
        Assert.Contains("Child", model.GetChangedProperties());
        Assert.True(model.Child.HasChanges());
        Assert.Contains("Name", model.Child.GetChangedProperties());
        Assert.Contains("Age", model.Child.GetChangedProperties());
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
        Assert.True(model.HasChanges());
        Assert.Contains("Children", model.GetChangedProperties());
        Assert.Equal(2, model.Children.Count);
        
        // 验证子对象的脏状态
        Assert.True(child1.HasChanges());
        Assert.Contains("Name", child1.GetChangedProperties());
        Assert.True(child2.HasChanges());
        Assert.Contains("Age", child2.GetChangedProperties());
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
        Assert.True(model.HasChanges());
        Assert.Contains("NamedChildren", model.GetChangedProperties());
        Assert.True(model.NamedChildren.ContainsKey("first"));
        Assert.True(model.NamedChildren.ContainsKey("second"));
        
        // 验证子对象的脏状态
        Assert.True(child1.HasChanges());
        Assert.Contains("Name", child1.GetChangedProperties());
        Assert.True(child2.HasChanges());
        Assert.Contains("IsActive", child2.GetChangedProperties());
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
        Assert.True(model.HasChanges());
        Assert.Contains("Title", model.GetChangedProperties());
        Assert.Contains("Sections", model.GetChangedProperties());
        
        Assert.True(section.HasChanges());
        Assert.Contains("Children", section.GetChangedProperties());
        
        Assert.True(child.HasChanges());
        Assert.Contains("Name", child.GetChangedProperties());
        Assert.Contains("Age", child.GetChangedProperties());
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
        Assert.True(model.HasChanges());
        Assert.Contains("Child", model.GetChangedProperties());
        
        // 验证新旧对象的状态
        Assert.True(oldChild.HasChanges()); // 仍然保持脏状态
        Assert.True(newChild.HasChanges());
        Assert.Contains("Age", newChild.GetChangedProperties());
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
        Assert.True(model.HasChanges());
        Assert.Contains("Children", model.GetChangedProperties());
        Assert.True(child.HasChanges());
        Assert.Contains("Name", child.GetChangedProperties());
        Assert.Contains("Age", child.GetChangedProperties());
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
        model.MarkClean(recursive: true);
        
        // Assert
        Assert.False(model.HasChanges());
        Assert.Empty(model.GetChangedProperties());
        Assert.False(child.HasChanges());
        Assert.Empty(child.GetChangedProperties());
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
        model.MarkClean(recursive: false); // 只清理自身
        
        // Assert
        Assert.False(model.HasChanges());
        Assert.Empty(model.GetChangedProperties());
        // 子对象仍应该是脏的
        Assert.True(child.HasChanges());
        Assert.Contains("Name", child.GetChangedProperties());
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
        Assert.True(topLevel.HasChanges());
        Assert.Contains("Title", topLevel.GetChangedProperties());
        Assert.Contains("Sections", topLevel.GetChangedProperties());
        
        Assert.True(section.HasChanges());
        Assert.Contains("Child", section.GetChangedProperties());
        Assert.Contains("Children", section.GetChangedProperties());
        
        Assert.True(child.HasChanges());
        Assert.Contains("Age", child.GetChangedProperties());
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
        Assert.False(model.HasChanges());
        Assert.Empty(model.GetChangedProperties());
        Assert.False(child.HasChanges());
        Assert.Empty(child.GetChangedProperties());
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
        Assert.True(model.HasChanges());
        Assert.Contains("PrimaryContact", model.GetChangedProperties());
        Assert.Contains("Sections", model.GetChangedProperties());
        Assert.Contains("Categories", model.GetChangedProperties());
        
        Assert.True(contact.HasChanges());
        Assert.Contains("Name", contact.GetChangedProperties());
        
        Assert.True(section.HasChanges());
        Assert.Contains("Child", section.GetChangedProperties());
    }
}
