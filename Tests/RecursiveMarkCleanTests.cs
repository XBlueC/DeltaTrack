using System.Reflection;
using Xunit;

namespace Tests;

public class RecursiveMarkCleanTests
{
    [Fact]
    public void MarkClean_WithoutRecursive_OnlyClearsTopLevel()
    {
        // Arrange
        var order = new Order();
        var person = new Person { Name = "Alice" };
        order.Associates["manager"] = person;
        order.CustomerName = "Bob";
        
        // 确保所有对象都是脏的
        Assert.True(order.IsDirty());
        Assert.True(person.IsDirty());

        // Act
        order.MarkClean(); // 不使用递归参数，默认为 false

        // Assert
        Assert.False(order.IsDirty()); // 顶层应该被清理
        Assert.True(person.IsDirty()); // 子对象仍然脏
        Assert.Contains("manager", order.Associates.Keys);
        Assert.Equal("Alice", order.Associates["manager"].Name);
    }

    [Fact]
    public void MarkClean_WithRecursive_True_ClearsAllLevels()
    {
        // Arrange
        var order = new Order();
        var person = new Person { Name = "Alice" };
        order.Associates["manager"] = person;
        order.CustomerName = "Bob";
        
        // 确保所有对象都是脏的
        Assert.True(order.IsDirty());
        Assert.True(person.IsDirty());

        // Act
        order.MarkClean(recursive: true);

        // Assert
        Assert.False(order.IsDirty()); // 顶层被清理
        Assert.False(person.IsDirty()); // 子对象也被清理
        Assert.Empty(order.GetDirtyFields());
        Assert.Empty(person.GetDirtyFields());
    }

    [Fact]
    public void MarkClean_Recursive_ListItemsAlsoCleaned()
    {
        // Arrange
        var order = new Order();
        var person1 = new Person { Name = "Alice" };
        var person2 = new Person { Name = "Bob" };
        
        order.People.Add(person1);
        order.People.Add(person2);
        order.CustomerName = "Test";
        
        person1.Name = "Alice Modified";
        person2.Name = "Bob Modified";
        
        Assert.True(order.IsDirty());
        Assert.True(person1.IsDirty());
        Assert.True(person2.IsDirty());

        // Act
        order.MarkClean(recursive: true);

        // Assert
        Assert.False(order.IsDirty());
        Assert.False(person1.IsDirty());
        Assert.False(person2.IsDirty());
    }

    [Fact]
    public void MarkClean_Recursive_NestedObjectsCleaned()
    {
        // Arrange
        var order = new Order();
        var address = new Address { City = "Shanghai", Street = "Nanjing Road" };
        order.ShippingAddress = address;
        order.CustomerName = "Customer";
        
        address.City = "Beijing"; // 修改嵌套对象
        
        Assert.True(order.IsDirty());
        Assert.True(address.IsDirty());

        // Act
        order.MarkClean(recursive: true);

        // Assert
        Assert.False(order.IsDirty());
        Assert.False(address.IsDirty());
    }

    [Fact]
    public void MarkClean_Recursive_ComplexHierarchy()
    {
        // Arrange
        var order = new Order();
        var manager = new Person { Name = "Manager" };
        var subordinate = new Person { Name = "Subordinate" };
        
        // 创建复杂层次结构
        order.Associates["manager"] = manager;
        order.Associates["subordinate"] = subordinate;
        manager.Name = "Updated Manager";
        subordinate.Name = "Updated Subordinate";
        
        Assert.True(order.IsDirty());
        Assert.True(manager.IsDirty());
        Assert.True(subordinate.IsDirty());

        // Act
        order.MarkClean(recursive: true);

        // Assert
        Assert.False(order.IsDirty());
        Assert.False(manager.IsDirty());
        Assert.False(subordinate.IsDirty());
    }

    [Fact]
    public void MarkClean_Recursive_NullNestedObject_HandledGracefully()
    {
        // Arrange
        var order = new Order();
        order.ShippingAddress = null; // 显式设置为 null
        order.CustomerName = "Test";
        
        Assert.True(order.IsDirty());

        // Act
        order.MarkClean(recursive: true);

        // Assert
        Assert.False(order.IsDirty());
        Assert.Null(order.ShippingAddress);
    }

    [Fact]
    public void MarkClean_Recursive_EmptyCollections_HandledGracefully()
    {
        // Arrange
        var order = new Order();
        order.CustomerName = "Test";
        // Items 和 Tags 都是空的
        
        Assert.True(order.IsDirty());

        // Act
        order.MarkClean(recursive: true);

        // Assert
        Assert.False(order.IsDirty());
        Assert.Empty(order.Items);
        Assert.Empty(order.Tags);
    }

    [Fact]
    public void MarkClean_Recursive_MixedDirtyStates()
    {
        // Arrange
        var order = new Order();
        var cleanPerson = new Person { Name = "Clean" };
        var dirtyPerson = new Person { Name = "WillBeDirty" };
        
        order.People.Add(cleanPerson);
        order.People.Add(dirtyPerson);
        order.CustomerName = "Test";
        
        // 只修改其中一个子对象
        dirtyPerson.Name = "Actually Dirty";
        
        // 检查初始状态
        System.Console.WriteLine($"Initial - Order dirty: {order.IsDirty()}");
        System.Console.WriteLine($"Initial - Clean person dirty: {cleanPerson.IsDirty()}");
        System.Console.WriteLine($"Initial - Dirty person dirty: {dirtyPerson.IsDirty()}");
        
        Assert.True(order.IsDirty());
        // 注意：添加到 TrackableList 后，即使是未修改的对象也会被标记为脏
        Assert.True(cleanPerson.IsDirty()); 
        Assert.True(dirtyPerson.IsDirty());

        // Act
        order.MarkClean(recursive: true);
        
        // 检查清理后状态
        System.Console.WriteLine($"After clean - Order dirty: {order.IsDirty()}");
        System.Console.WriteLine($"After clean - Clean person dirty: {cleanPerson.IsDirty()}");
        System.Console.WriteLine($"After clean - Dirty person dirty: {dirtyPerson.IsDirty()}");

        // Assert
        Assert.False(order.IsDirty());
        // 递归清理会清理所有子对象，无论它们是否原本就是脏的
        Assert.False(cleanPerson.IsDirty()); 
        Assert.False(dirtyPerson.IsDirty());
    }
}