using DeltaTrack;

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
        Assert.Equal(2, list.Count);
        Assert.Equal(2, changeCount);
        Assert.Contains("Item1", list);
        Assert.Contains("Item2", list);
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
        Assert.Equal(3, changeCount);
        Assert.Equal("Item2", list[1]);
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
        Assert.Equal(3, changeCount);
        Assert.Equal("NewItem1", list[0]);
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
        Assert.Equal(3, changeCount);
        Assert.Single(list);
        Assert.Equal("Item2", list[0]);
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
        Assert.Equal(3, changeCount);
        Assert.Empty(list);
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
        Assert.Equal(3, list.Count);
        Assert.Equal(0, changeCount); // 初始化不触发变更
        Assert.Equal("Item1", list[0]);
        Assert.Equal("Item2", list[1]);
        Assert.Equal("Item3", list[2]);
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
        Assert.Equal(2, dict.Count);
        Assert.Equal(2, changeCount);
        Assert.Equal("Value1", dict["Key1"]);
        Assert.Equal("Value2", dict["Key2"]);
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
        Assert.Equal(2, changeCount);
        Assert.Equal("NewValue1", dict["Key1"]);
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
        Assert.True(removed);
        Assert.Equal(3, changeCount);
        Assert.Single(dict);
        Assert.False(dict.ContainsKey("Key1"));
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
        Assert.Equal(3, changeCount);
        Assert.Empty(dict);
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
        Assert.Equal(2, dict.Count);
        Assert.Equal(0, changeCount); // 初始化不触发变更
        Assert.Equal("Value1", dict["Key1"]);
        Assert.Equal("Value2", dict["Key2"]);
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
        Assert.Equal(2, set.Count);
        Assert.Equal(2, changeCount); // 重复添加不触发变更
        Assert.Contains(1, set);
        Assert.Contains(2, set);
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
        Assert.True(removed);
        Assert.False(notRemoved);
        Assert.Equal(3, changeCount);
        Assert.Single(set);
        Assert.Contains(2, set);
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
        Assert.Equal(4, changeCount);
        Assert.Empty(set);
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
        Assert.Equal(3, changeCount); // 添加了3和4
        Assert.Equal(4, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(2, set);
        Assert.Contains(3, set);
        Assert.Contains(4, set);
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
        Assert.Equal(4, changeCount);
        Assert.Equal(2, set.Count);
        Assert.Contains(2, set);
        Assert.Contains(3, set);
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
        Assert.Equal(4, changeCount);
        Assert.Equal(2, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(3, set);
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
        Assert.Equal(4, changeCount);
        Assert.Equal(3, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(4, set);
        Assert.Contains(5, set);
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
        Assert.Equal(3, set.Count);
        Assert.Equal(0, changeCount); // 初始化不触发变更
        Assert.Contains(1, set);
        Assert.Contains(2, set);
        Assert.Contains(3, set);
    }
}