using DeltaTrack;
using FluentAssertions;

namespace Tests;

/// <summary>
/// TrackableList 功能测试
/// </summary>
public class TrackableListTests
{
    /// <summary>
    /// 测试 TrackableList 基本添加功能
    /// </summary>
    [Fact]
    public void TrackableList_Should_Track_Additions()
    {
        // Arrange
        var changeCount = 0;
        var list = new TrackableList<string>(() => changeCount++);
        
        // Act
        list.Add("Item1");
        list.Add("Item2");
        
        // Assert
        list.Count.Should().Be(2);
        changeCount.Should().Be(2);
        list.Should().Contain("Item1", "Item2");
    }

    /// <summary>
    /// 测试 TrackableList 插入功能
    /// </summary>
    [Fact]
    public void TrackableList_Should_Track_Insertions()
    {
        // Arrange
        var changeCount = 0;
        var list = new TrackableList<string>(() => changeCount++);
        list.Add("Item1");
        list.Add("Item3");
        
        // Act
        list.Insert(1, "Item2");
        
        // Assert
        changeCount.Should().Be(3);
        list[1].Should().Be("Item2");
    }

    /// <summary>
    /// 测试 TrackableList 设置功能
    /// </summary>
    [Fact]
    public void TrackableList_Should_Track_Replacements()
    {
        // Arrange
        var changeCount = 0;
        var list = new TrackableList<string>(() => changeCount++);
        list.Add("Item1");
        list.Add("Item2");
        
        // Act
        list[0] = "NewItem1";
        
        // Assert
        changeCount.Should().Be(3);
        list[0].Should().Be("NewItem1");
    }

    /// <summary>
    /// 测试 TrackableList 移除功能
    /// </summary>
    [Fact]
    public void TrackableList_Should_Track_Removals()
    {
        // Arrange
        var changeCount = 0;
        var list = new TrackableList<string>(() => changeCount++);
        list.Add("Item1");
        list.Add("Item2");
        
        // Act
        list.RemoveAt(0);
        
        // Assert
        changeCount.Should().Be(3);
        list.Count.Should().Be(1);
        list[0].Should().Be("Item2");
    }

    /// <summary>
    /// 测试 TrackableList 清空功能
    /// </summary>
    [Fact]
    public void TrackableList_Should_Track_Clear()
    {
        // Arrange
        var changeCount = 0;
        var list = new TrackableList<string>(() => changeCount++);
        list.Add("Item1");
        list.Add("Item2");
        
        // Act
        list.Clear();
        
        // Assert
        changeCount.Should().Be(3);
        list.Count.Should().Be(0);
    }

    /// <summary>
    /// 测试 TrackableList 初始化构造函数
    /// </summary>
    [Fact]
    public void TrackableList_Should_Handle_Initial_Items()
    {
        // Arrange
        var initialItems = new List<string> { "Item1", "Item2", "Item3" };
        var changeCount = 0;
        
        // Act
        var list = new TrackableList<string>(() => changeCount++, initialItems);
        
        // Assert
        list.Count.Should().Be(3);
        changeCount.Should().Be(0); // 初始化不触发变更
        list.Should().ContainInOrder("Item1", "Item2", "Item3");
    }
}

/// <summary>
/// TrackableDictionary 功能测试
/// </summary>
public class TrackableDictionaryTests
{
    /// <summary>
    /// 测试 TrackableDictionary 基本添加功能
    /// </summary>
    [Fact]
    public void TrackableDictionary_Should_Track_Additions()
    {
        // Arrange
        var changeCount = 0;
        var dict = new TrackableDictionary<string, string>(() => changeCount++);
        
        // Act
        dict.Add("Key1", "Value1");
        dict["Key2"] = "Value2";
        
        // Assert
        dict.Count.Should().Be(2);
        changeCount.Should().Be(2);
        dict["Key1"].Should().Be("Value1");
        dict["Key2"].Should().Be("Value2");
    }

    /// <summary>
    /// 测试 TrackableDictionary 更新功能
    /// </summary>
    [Fact]
    public void TrackableDictionary_Should_Track_Updates()
    {
        // Arrange
        var changeCount = 0;
        var dict = new TrackableDictionary<string, string>(() => changeCount++);
        dict.Add("Key1", "Value1");
        
        // Act
        dict["Key1"] = "NewValue1";
        
        // Assert
        changeCount.Should().Be(2);
        dict["Key1"].Should().Be("NewValue1");
    }

    /// <summary>
    /// 测试 TrackableDictionary 删除功能
    /// </summary>
    [Fact]
    public void TrackableDictionary_Should_Track_Removals()
    {
        // Arrange
        var changeCount = 0;
        var dict = new TrackableDictionary<string, string>(() => changeCount++);
        dict.Add("Key1", "Value1");
        dict.Add("Key2", "Value2");
        
        // Act
        var removed = dict.Remove("Key1");
        
        // Assert
        removed.Should().BeTrue();
        changeCount.Should().Be(3);
        dict.Count.Should().Be(1);
        dict.ContainsKey("Key1").Should().BeFalse();
    }

    /// <summary>
    /// 测试 TrackableDictionary 清空功能
    /// </summary>
    [Fact]
    public void TrackableDictionary_Should_Track_Clear()
    {
        // Arrange
        var changeCount = 0;
        var dict = new TrackableDictionary<string, string>(() => changeCount++);
        dict.Add("Key1", "Value1");
        dict.Add("Key2", "Value2");
        
        // Act
        dict.Clear();
        
        // Assert
        changeCount.Should().Be(3);
        dict.Count.Should().Be(0);
    }

    /// <summary>
    /// 测试 TrackableDictionary 初始化构造函数
    /// </summary>
    [Fact]
    public void TrackableDictionary_Should_Handle_Initial_Data()
    {
        // Arrange
        var initialData = new Dictionary<string, string>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };
        var changeCount = 0;
        
        // Act
        var dict = new TrackableDictionary<string, string>(() => changeCount++, initialData);
        
        // Assert
        dict.Count.Should().Be(2);
        changeCount.Should().Be(0); // 初始化不触发变更
        dict["Key1"].Should().Be("Value1");
        dict["Key2"].Should().Be("Value2");
    }
}

/// <summary>
/// TrackableSet 功能测试
/// </summary>
public class TrackableSetTests
{
    /// <summary>
    /// 测试 TrackableSet 基本添加功能
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Track_Additions()
    {
        // Arrange
        var changeCount = 0;
        var set = new TrackableSet<int>(() => changeCount++);
        
        // Act
        set.Add(1);
        set.Add(2);
        set.Add(1); // 重复添加
        
        // Assert
        set.Count.Should().Be(2);
        changeCount.Should().Be(2); // 重复添加不触发变更
        set.Should().Contain(new[] { 1, 2 });
    }

    /// <summary>
    /// 测试 TrackableSet 删除功能
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Track_Removals()
    {
        // Arrange
        var changeCount = 0;
        var set = new TrackableSet<int>(() => changeCount++);
        set.Add(1);
        set.Add(2);
        
        // Act
        var removed = set.Remove(1);
        var notRemoved = set.Remove(3); // 不存在的元素
        
        // Assert
        removed.Should().BeTrue();
        notRemoved.Should().BeFalse();
        changeCount.Should().Be(3);
        set.Count.Should().Be(1);
        set.Should().Contain(2);
    }

    /// <summary>
    /// 测试 TrackableSet 清空功能
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Track_Clear()
    {
        // Arrange
        var changeCount = 0;
        var set = new TrackableSet<int>(() => changeCount++);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        
        // Act
        set.Clear();
        
        // Assert
        changeCount.Should().Be(4);
        set.Count.Should().Be(0);
    }

    /// <summary>
    /// 测试 TrackableSet 并集操作
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Track_UnionWith()
    {
        // Arrange
        var changeCount = 0;
        var set = new TrackableSet<int>(() => changeCount++);
        set.Add(1);
        set.Add(2);
        var other = new HashSet<int> { 2, 3, 4 };
        
        // Act
        set.UnionWith(other);
        
        // Assert
        changeCount.Should().Be(3); // 添加了3和4
        set.Count.Should().Be(4);
        set.Should().Contain(new[] { 1, 2, 3, 4 });
    }

    /// <summary>
    /// 测试 TrackableSet 交集操作
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Track_IntersectWith()
    {
        // Arrange
        var changeCount = 0;
        var set = new TrackableSet<int>(() => changeCount++);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        var other = new HashSet<int> { 2, 3, 4 };
        
        // Act
        set.IntersectWith(other);
        
        // Assert
        changeCount.Should().Be(4);
        set.Count.Should().Be(2);
        set.Should().Contain(new[] { 2, 3 });
    }

    /// <summary>
    /// 测试 TrackableSet 差集操作
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Track_ExceptWith()
    {
        // Arrange
        var changeCount = 0;
        var set = new TrackableSet<int>(() => changeCount++);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        var other = new HashSet<int> { 2, 4 };
        
        // Act
        set.ExceptWith(other);
        
        // Assert
        changeCount.Should().Be(4);
        set.Count.Should().Be(2);
        set.Should().Contain(new[] { 1, 3 });
    }

    /// <summary>
    /// 测试 TrackableSet 对称差集操作
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Track_SymmetricExceptWith()
    {
        // Arrange
        var changeCount = 0;
        var set = new TrackableSet<int>(() => changeCount++);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        var other = new HashSet<int> { 2, 3, 4, 5 };
        
        // Act
        set.SymmetricExceptWith(other);
        
        // Assert
        changeCount.Should().Be(4);
        set.Count.Should().Be(3);
        set.Should().Contain(new[] { 1, 4, 5 });
    }

    /// <summary>
    /// 测试 TrackableSet 初始化构造函数
    /// </summary>
    [Fact]
    public void TrackableSet_Should_Handle_Initial_Items()
    {
        // Arrange
        var initialItems = new HashSet<int> { 1, 2, 3 };
        var changeCount = 0;
        
        // Act
        var set = new TrackableSet<int>(() => changeCount++, initialItems);
        
        // Assert
        set.Count.Should().Be(3);
        changeCount.Should().Be(0); // 初始化不触发变更
        set.Should().Contain(new[] { 1, 2, 3 });
    }
}