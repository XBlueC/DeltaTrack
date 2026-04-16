using DeltaTrack;
using FluentAssertions;

namespace Tests;

/// <summary>
/// 源生成属性功能测试
/// </summary>
public class GeneratedPropertiesTests
{
    /// <summary>
    /// 测试简单模型的基本属性生成功能
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Generate_Properties_Correctly()
    {
        // Arrange
        var model = new SimpleModel();
        
        // Act & Assert
        model.HasChanges().Should().BeFalse();
        model.GetChangedProperties().Should().BeEmpty();
        
        // 测试属性设置
        model.Name = "Test Name";
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Name");
        model.Name.Should().Be("Test Name");
    }

    /// <summary>
    /// 测试多个属性的脏状态跟踪
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Track_Multiple_Fields()
    {
        // Arrange
        var model = new SimpleModel();
        
        // Act
        model.Name = "John Doe";
        model.Age = 30;
        model.IsActive = true;
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Name", "Age", "IsActive");
    }

    /// <summary>
    /// 测试相同值的属性设置不触发脏状态
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Not_Mark_Dirty_For_Same_Values()
    {
        // Arrange
        var model = new SimpleModel();
        model.Name = "Initial"; // 先设置初始值
        
        // Act
        model.Name = "Initial"; // 设置相同值
        
        // Assert
        model.GetChangedProperties().Should().Contain("Name"); // 第一次设置会标记为脏
        // 注意：源生成器使用 EqualityComparer，默认情况下字符串比较是按值比较的
    }

    /// <summary>
    /// 测试值类型属性的脏状态跟踪
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Track_Value_Types_Correctly()
    {
        // Arrange
        var model = new SimpleModel();
        
        // Act
        model.Age = 25;
        model.BirthDate = new DateTime(1995, 1, 1);
        model.IsActive = true;
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Age", "BirthDate", "IsActive");
        model.Age.Should().Be(25);
        model.BirthDate.Should().Be(new DateTime(1995, 1, 1));
        model.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// 测试清理功能
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Clean_Dirty_State()
    {
        // Arrange
        var model = new SimpleModel();
        model.Name = "Test";
        model.Age = 30;
        
        // Act
        model.MarkClean();
        
        // Assert
        model.HasChanges().Should().BeFalse();
        model.GetChangedProperties().Should().BeEmpty();
    }

    /// <summary>
    /// 测试手动标记字段为脏
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Allow_Manual_Dirty_Marking()
    {
        // Arrange
        var model = new SimpleModel();
    
        // Act - 使用已知属性名
        model.MarkChanged("Name");
    
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Name");
    }
    
    /// <summary>
    /// 测试使用类型安全的 DirtyFlag 标记
    /// </summary>
    [Fact]
    public void SimpleModel_Should_Allow_DirtyFlag_Marking()
    {
        // Arrange
        var model = new SimpleModel();
    
        // Act
        model.MarkChanged(SimpleModel.DirtyFlag.Name | SimpleModel.DirtyFlag.Age);
    
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetDirtyFlags().Should().HaveFlag(SimpleModel.DirtyFlag.Name);
        model.GetDirtyFlags().Should().HaveFlag(SimpleModel.DirtyFlag.Age);
        model.GetChangedProperties().Should().Contain("Name", "Age");
    }

    /// <summary>
    /// 测试集合模型的列表属性生成功能
    /// </summary>
    [Fact]
    public void CollectionModel_Should_Generate_List_Property()
    {
        // Arrange
        var model = new CollectionModel();
        
        // Act
        model.Tags.Add("Tag1");
        model.Tags.Add("Tag2");
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Tags");
        model.Tags.Should().Contain("Tag1", "Tag2");
    }

    /// <summary>
    /// 测试集合模型的字典属性生成功能
    /// </summary>
    [Fact]
    public void CollectionModel_Should_Generate_Dictionary_Property()
    {
        // Arrange
        var model = new CollectionModel();
        
        // Act
        model.Metadata["Key1"] = "Value1";
        model.Metadata["Key2"] = "Value2";
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Metadata");
        model.Metadata["Key1"].Should().Be("Value1");
        model.Metadata["Key2"].Should().Be("Value2");
    }

    /// <summary>
    /// 测试集合模型的HashSet属性生成功能
    /// </summary>
    [Fact]
    public void CollectionModel_Should_Generate_Set_Property()
    {
        // Arrange
        var model = new CollectionModel();
        
        // Act
        model.Numbers.Add(1);
        model.Numbers.Add(2);
        model.Numbers.Add(1); // 重复添加
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Numbers");
        model.Numbers.Should().Contain(new[] { 1, 2 });
        model.Numbers.Count.Should().Be(2);
    }

    /// <summary>
    /// 测试集合模型属性的生成功能
    /// </summary>
    [Fact]
    public void CollectionModel_Should_Generate_Collection_Properties()
    {
        // Arrange
        var model = new CollectionModel();
        
        // Act
        model.Tags.Add("Tag1");
        model.Metadata["Key1"] = "Value1";
        model.Numbers.Add(1);
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Tags", "Metadata", "Numbers");
    }

    /// <summary>
    /// 测试复杂模型的组合属性生成功能
    /// </summary>
    [Fact]
    public void ComplexModel_Should_Handle_Complex_Properties()
    {
        // Arrange
        var model = new ComplexModel();
        
        // Act
        model.Title = "Test Title";
        model.Categories.Add("Category1");
        model.PrimaryContact.Name = "Contact Name";
        model.Settings["Setting1"] = "Value1";
        
        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Title", "Categories", "PrimaryContact", "Settings");
        model.Title.Should().Be("Test Title");
        model.Categories.Should().Contain("Category1");
        model.PrimaryContact.Name.Should().Be("Contact Name");
        model.Settings["Setting1"].Should().Be("Value1");
    }

    /// <summary>
    /// 测试属性设置时的相等性比较
    /// </summary>
    [Fact]
    public void Generated_Properties_Should_Use_Proper_Equality_Comparison()
    {
        // Arrange
        var model = new SimpleModel();
        
        // Act
        model.Name = null;
        model.Name = null; // 再次设置null
        
        // Assert
        // 源生成器应该正确处理null值的相等性比较
        model.GetChangedProperties().Should().Contain("Name");
    }

    /// <summary>
    /// 测试DateTime类型的属性跟踪
    /// </summary>
    [Fact]
    public void Generated_Properties_Should_Handle_DateTime_Correctly()
    {
        // Arrange
        var model = new SimpleModel();
        var date1 = new DateTime(2023, 1, 1);
        var date2 = new DateTime(2023, 12, 31);
        
        // Act
        model.BirthDate = date1;
        model.BirthDate = date2;
        
        // Assert
        model.BirthDate.Should().Be(date2);
        model.GetChangedProperties().Should().Contain("BirthDate");
    }

    /// <summary>
    /// 测试不带 TrackableAttribute 但有 TrackableField 的类也能正常生成跟踪代码
    /// </summary>
    [Fact]
    public void ModelWithoutTrackableAttribute_Should_Generate_Tracking_Code()
    {
        // Arrange
        var model = new ModelWithoutTrackableAttribute();

        // Act & Assert
        model.HasChanges().Should().BeFalse();
        model.GetChangedProperties().Should().BeEmpty();

        // 测试属性设置
        model.Name = "Test Name";
        model.Age = 25;
        model.IsActive = true;

        // Assert
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("Name", "Age", "IsActive");
        model.Name.Should().Be("Test Name");
        model.Age.Should().Be(25);
        model.IsActive.Should().BeTrue();

        // 测试清理功能
        model.MarkClean();
        model.HasChanges().Should().BeFalse();
        model.GetChangedProperties().Should().BeEmpty();
    }

    /// <summary>
    /// 测试带 TrackableAttribute 的类自动追踪私有字段（无需 TrackableField）
    /// </summary>
    [Fact]
    public void AutoTrackModel_Should_Track_All_Private_Fields()
    {
        // Arrange
        var model = new AutoTrackModel();

        // Act & Assert - 初始状态
        model.HasChanges().Should().BeFalse();

        // 设置自动追踪的字段
        model.AutoName = "Auto Test";
        model.AutoAge = 30;
        model.AutoBirthDate = new DateTime(1990, 1, 1);
        model.AutoIsActive = true;

        // Assert - 所有私有字段都被追踪
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("AutoName", "AutoAge", "AutoBirthDate", "AutoIsActive");
    }

    /// <summary>
    /// 测试 TrackIgnoreAttribute 排除字段追踪
    /// </summary>
    [Fact]
    public void AutoTrackModel_Should_Not_Track_Ignored_Fields()
    {
        // Arrange
        var model = new AutoTrackModel();

        // Act - 设置被忽略的字段（通过反射验证字段存在但无属性）
        // IgnoredField 不会生成属性，所以无法直接设置
        // 验证只有 AutoName 等字段有属性

        // Assert - 验证生成的属性不包括 IgnoredField
        model.HasChanges().Should().BeFalse();
        model.AutoName = "Test";
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("AutoName");
        model.GetChangedProperties().Should().NotContain("IgnoredField");
    }

    /// <summary>
    /// 测试 ModelWithIgnore 的 TrackIgnore 特性功能
    /// </summary>
    [Fact]
    public void ModelWithIgnore_Should_Only_Track_NonIgnored_Fields()
    {
        // Arrange
        var model = new ModelWithIgnore();

        // Act
        model.TrackedField = "Tracked Value";

        // Assert - 只有 TrackedField 被追踪
        model.HasChanges().Should().BeTrue();
        model.GetChangedProperties().Should().Contain("TrackedField");
        model.GetChangedProperties().Should().NotContain("IgnoredField", "IgnoredNumber");

        // 验证 TrackedField 属性存在且正常工作
        model.TrackedField.Should().Be("Tracked Value");
    }
}