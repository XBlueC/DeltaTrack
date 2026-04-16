using DeltaTrack;
using FluentAssertions;

namespace Tests;

/// <summary>
/// 递归清理功能的边界场景测试
/// 重点测试 AppendHelperMethods 生成的 MarkPropClean 方法
/// </summary>
public class RecursiveCleanupTests
{
    // ==================== 三层深度嵌套递归清理 ====================

    /// <summary>
    /// 三层嵌套: ThreeLevelModel -> NestedModel -> SimpleModel
    /// 从顶层递归清理应该清除所有层级
    /// </summary>
    [Fact]
    public void ThreeLevel_Recursive_Clean_Should_Clean_All_Levels()
    {
        // Arrange
        var top = new ThreeLevelModel();
        top.Label = "Top";
        top.Nested.Child.Name = "GrandChild";
        top.Nested.Child.Age = 10;

        // Verify dirty
        top.HasChanges().Should().BeTrue();
        top.Nested.HasChanges().Should().BeTrue();
        top.Nested.Child.HasChanges().Should().BeTrue();

        // Act
        top.MarkClean(recursive: true);

        // Assert
        top.HasChanges().Should().BeFalse("顶层应该被清理");
        top.Nested.HasChanges().Should().BeFalse("中间层应该被清理");
        top.Nested.Child.HasChanges().Should().BeFalse("最底层应该被清理");
    }

    /// <summary>
    /// 三层嵌套: 中间层列表中的子对象也应该被递归清理
    /// ThreeLevelModel -> NestedModel.Children[i] -> SimpleModel
    /// </summary>
    [Fact]
    public void ThreeLevel_With_List_Recursive_Clean_Should_Clean_List_Items()
    {
        // Arrange
        var top = new ThreeLevelModel();
        var child1 = new SimpleModel { Name = "Child1", Age = 1 };
        var child2 = new SimpleModel { Name = "Child2", Age = 2 };
        top.Nested.Children.Add(child1);
        top.Nested.Children.Add(child2);

        // Act
        top.MarkClean(recursive: true);

        // Assert
        top.HasChanges().Should().BeFalse();
        top.Nested.HasChanges().Should().BeFalse();
        child1.HasChanges().Should().BeFalse("列表中的子对象应该被递归清理");
        child2.HasChanges().Should().BeFalse("列表中的子对象应该被递归清理");
    }

    /// <summary>
    /// 三层嵌套: 中间层字典中的子对象也应该被递归清理
    /// ThreeLevelModel -> NestedModel.NamedChildren["key"] -> SimpleModel
    /// </summary>
    [Fact]
    public void ThreeLevel_With_Dict_Recursive_Clean_Should_Clean_Dict_Values()
    {
        // Arrange
        var top = new ThreeLevelModel();
        var child1 = new SimpleModel { Name = "DictChild1" };
        var child2 = new SimpleModel { Name = "DictChild2" };
        top.Nested.NamedChildren["a"] = child1;
        top.Nested.NamedChildren["b"] = child2;

        // Act
        top.MarkClean(recursive: true);

        // Assert
        top.HasChanges().Should().BeFalse();
        top.Nested.HasChanges().Should().BeFalse();
        child1.HasChanges().Should().BeFalse("字典中的子对象应该被递归清理");
        child2.HasChanges().Should().BeFalse("字典中的子对象应该被递归清理");
    }

    // ==================== HasChanges 守卫问题 ====================

    /// <summary>
    /// BUG场景: 中间层被非递归清理后，顶层递归清理应该仍然能清理底层
    /// 
    /// 步骤:
    /// 1. GrandChild 变脏 -> Child 变脏 -> Parent 变脏
    /// 2. Child.MarkClean(false) -> Child 干净，但 GrandChild 仍脏
    /// 3. Parent.MarkClean(true) -> 应该清理 GrandChild
    /// 
    /// 生成的代码检查 HasChanges() 后才递归，中间层已清理会导致跳过
    /// </summary>
    [Fact]
    public void Recursive_Clean_Should_Not_Skip_When_Intermediate_Already_Clean()
    {
        // Arrange
        var top = new ThreeLevelModel();
        top.Nested.Child.Name = "GrandChild Dirty";

        // 中间层非递归清理
        top.Nested.MarkClean(recursive: false);

        // 验证中间状态
        top.Nested.HasChanges().Should().BeFalse("中间层已清理");
        top.Nested.Child.HasChanges().Should().BeTrue("底层仍然脏");

        // Act - 顶层递归清理
        top.MarkClean(recursive: true);

        // Assert
        top.Nested.Child.HasChanges().Should().BeFalse(
            "即使中间层已干净，顶层递归清理也应该清理底层孙子节点");
    }

    /// <summary>
    /// 类似场景: 直接trackable子对象被非递归清理后，再对父对象递归清理
    /// </summary>
    [Fact]
    public void Recursive_Clean_Should_Reach_Grandchild_Even_If_Child_Was_Cleaned()
    {
        // Arrange
        var model = new ComplexModel();
        var section = new NestedModel();
        var grandChild = new SimpleModel();

        model.Sections["main"] = section;
        section.Child = grandChild;
        grandChild.Name = "Deep Child";

        // 清理中间层（section），但不递归
        section.MarkClean(recursive: false);

        section.HasChanges().Should().BeFalse();
        grandChild.HasChanges().Should().BeTrue();

        // Act
        model.MarkClean(recursive: true);

        // Assert
        grandChild.HasChanges().Should().BeFalse(
            "从顶层递归清理应该穿透已清理的中间层到达底层");
    }

    // ==================== HashSet<Trackable> 递归清理 ====================

    /// <summary>
    /// BUG场景: HashSet<SimpleModel> 中的 trackable 对象应该被递归清理
    /// 
    /// IsCollectionOfTrackable 只检查 ImplementsIListInterface，
    /// 而 HashSet 实现了 ISet 接口，会被 ImplementsIListInterface 排除。
    /// 导致 HashSet<Trackable> 的递归清理代码不会生成。
    /// </summary>
    [Fact]
    public void HashSet_Of_Trackable_Items_Should_Be_Recursively_Cleaned()
    {
        // Arrange
        var model = new SetOfTrackableModel();
        var item1 = new SimpleModel { Name = "Item1", Age = 1 };
        var item2 = new SimpleModel { Name = "Item2", Age = 2 };
        model.Items.Add(item1);
        model.Items.Add(item2);

        // Verify dirty
        item1.HasChanges().Should().BeTrue();
        item2.HasChanges().Should().BeTrue();

        // Act
        model.MarkClean(recursive: true);

        // Assert
        model.HasChanges().Should().BeFalse();
        item1.HasChanges().Should().BeFalse(
            "HashSet 中的 Trackable 对象应该被递归清理");
        item2.HasChanges().Should().BeFalse(
            "HashSet 中的 Trackable 对象应该被递归清理");
    }

    /// <summary>
    /// HashSet<Trackable> 非递归清理不应该影响子对象
    /// </summary>
    [Fact]
    public void HashSet_Of_Trackable_NonRecursive_Clean_Should_Not_Touch_Items()
    {
        // Arrange
        var model = new SetOfTrackableModel();
        var item = new SimpleModel { Name = "Item1" };
        model.Items.Add(item);

        // Act
        model.MarkClean(recursive: false);

        // Assert
        model.HasChanges().Should().BeFalse();
        item.HasChanges().Should().BeTrue("非递归清理不应该影响集合中的子对象");
    }

    // ==================== 混合 Trackable 集合递归清理 ====================

    /// <summary>
    /// List<Trackable> + Dictionary<string, Trackable> + 直接Trackable字段混合场景
    /// </summary>
    [Fact]
    public void Mixed_Trackable_Collections_Should_All_Be_Recursively_Cleaned()
    {
        // Arrange
        var model = new MixedTrackableCollectionsModel();
        var listItem = new SimpleModel { Name = "ListItem" };
        var dictItem = new SimpleModel { Name = "DictItem" };
        model.DirectChild.Name = "DirectChild";
        model.TrackableList.Add(listItem);
        model.TrackableDict["key"] = dictItem;

        // Act
        model.MarkClean(recursive: true);

        // Assert
        model.HasChanges().Should().BeFalse();
        model.DirectChild.HasChanges().Should().BeFalse("直接Trackable字段应该被递归清理");
        listItem.HasChanges().Should().BeFalse("List中的Trackable应该被递归清理");
        dictItem.HasChanges().Should().BeFalse("Dictionary中的Trackable应该被递归清理");
    }

    /// <summary>
    /// 混合场景中部分子对象脏、部分干净
    /// </summary>
    [Fact]
    public void Mixed_Dirty_And_Clean_Items_Should_Be_Handled_Correctly()
    {
        // Arrange
        var model = new MixedTrackableCollectionsModel();
        var cleanItem = new SimpleModel { Name = "WillBeClean" };
        var dirtyItem = new SimpleModel { Name = "WillStayDirtyThenClean" };
        model.TrackableList.Add(cleanItem);
        model.TrackableList.Add(dirtyItem);

        // 先清理一个item
        cleanItem.MarkClean();

        // Act
        model.MarkClean(recursive: true);

        // Assert
        cleanItem.HasChanges().Should().BeFalse();
        dirtyItem.HasChanges().Should().BeFalse("即使是刚加入的脏对象也应该被清理");
    }

    // ==================== Null 安全性 ====================

    /// <summary>
    /// Trackable 字段为 null 时递归清理不应该抛异常
    /// </summary>
    [Fact]
    public void Recursive_Clean_With_Null_Trackable_Child_Should_Not_Throw()
    {
        // Arrange
        var model = new NestedModel();
        model.Child = null;

        // Act
        var exception = Record.Exception(() => model.MarkClean(recursive: true));

        // Assert
        exception.Should().BeNull("null 子对象不应该导致递归清理异常");
    }

    /// <summary>
    /// 集合属性包含 null 元素时递归清理不应该抛异常
    /// </summary>
    [Fact]
    public void Recursive_Clean_With_Null_Items_In_Collections_Should_Not_Throw()
    {
        // Arrange
        var model = new MixedTrackableCollectionsModel();
        model.TrackableDict["null_value"] = null;

        // Act
        var exception = Record.Exception(() => model.MarkClean(recursive: true));

        // Assert
        exception.Should().BeNull("集合中的 null 元素不应该导致递归清理异常");
    }

    // ==================== 空集合 ====================

    /// <summary>
    /// 空的 Trackable 集合递归清理
    /// </summary>
    [Fact]
    public void Empty_Trackable_Collections_Recursive_Clean_Should_Work()
    {
        // Arrange
        var model = new MixedTrackableCollectionsModel();
        model.DirectChild.Name = "Only direct child dirty";
        // TrackableList 和 TrackableDict 为空

        // Act
        model.MarkClean(recursive: true);

        // Assert
        model.HasChanges().Should().BeFalse();
        model.DirectChild.HasChanges().Should().BeFalse();
    }

    // ==================== 替换子对象后的递归清理 ====================

    /// <summary>
    /// 替换 trackable 子对象后，旧对象不受影响，新对象应被递归清理
    /// </summary>
    [Fact]
    public void Recursive_Clean_After_Replacing_Child_Should_Only_Affect_New_Child()
    {
        // Arrange
        var model = new NestedModel();
        var oldChild = new SimpleModel { Name = "Old" };
        var newChild = new SimpleModel { Name = "New" };

        model.Child = oldChild;
        model.Child = newChild;

        // Act
        model.MarkClean(recursive: true);

        // Assert
        model.HasChanges().Should().BeFalse();
        newChild.HasChanges().Should().BeFalse("新子对象应该被递归清理");
        // 旧子对象自身的脏状态不受父对象清理的影响（已取消订阅）
        oldChild.HasChanges().Should().BeTrue("旧子对象已脱离，不应受递归清理影响");
    }

    // ==================== 多次清理 ====================

    /// <summary>
    /// 递归清理后再次变脏，再次递归清理应该正常工作
    /// </summary>
    [Fact]
    public void Multiple_Recursive_Clean_Cycles_Should_Work()
    {
        // Arrange
        var model = new MixedTrackableCollectionsModel();
        var item = new SimpleModel();
        model.TrackableList.Add(item);

        // 第一轮: 变脏 + 清理
        item.Name = "First";
        model.DirectChild.Age = 1;
        model.MarkClean(recursive: true);

        model.HasChanges().Should().BeFalse();
        item.HasChanges().Should().BeFalse();

        // 第二轮: 再次变脏 + 清理
        item.Name = "Second";
        model.DirectChild.Age = 2;
        model.MarkClean(recursive: true);

        // Assert
        model.HasChanges().Should().BeFalse();
        item.HasChanges().Should().BeFalse("多轮清理应该正常工作");
        model.DirectChild.HasChanges().Should().BeFalse();
    }

    // ==================== ComplexModel 深层组合 ====================

    /// <summary>
    /// ComplexModel 完整深层递归:
    /// ComplexModel.Sections["key"].Children[i].Name = dirty
    /// ComplexModel.Sections["key"].NamedChildren["key2"].Age = dirty
    /// ComplexModel.PrimaryContact.Name = dirty
    /// 一次递归清理全部清除
    /// </summary>
    [Fact]
    public void ComplexModel_Deep_Recursive_Clean_All_Paths()
    {
        // Arrange
        var model = new ComplexModel();
        var section = new NestedModel();
        var listChild = new SimpleModel { Name = "ListChild" };
        var dictChild = new SimpleModel { Age = 99 };
        var contact = new SimpleModel { Name = "Contact" };

        model.Title = "Title";
        model.PrimaryContact = contact;
        model.Sections["main"] = section;
        section.Children.Add(listChild);
        section.NamedChildren["x"] = dictChild;

        // Verify everything is dirty
        model.HasChanges().Should().BeTrue();
        contact.HasChanges().Should().BeTrue();
        section.HasChanges().Should().BeTrue();
        listChild.HasChanges().Should().BeTrue();
        dictChild.HasChanges().Should().BeTrue();

        // Act
        model.MarkClean(recursive: true);

        // Assert
        model.HasChanges().Should().BeFalse();
        contact.HasChanges().Should().BeFalse("PrimaryContact 应该被清理");
        section.HasChanges().Should().BeFalse("Sections 中的 NestedModel 应该被清理");
        listChild.HasChanges().Should().BeFalse("Sections[].Children[] 中的对象应该被清理");
        dictChild.HasChanges().Should().BeFalse("Sections[].NamedChildren[] 中的对象应该被清理");
    }

    /// <summary>
    /// 同一个 Trackable 对象出现在多个位置时的递归清理
    /// </summary>
    [Fact]
    public void Shared_Trackable_Object_In_Multiple_Locations_Should_Be_Cleaned()
    {
        // Arrange
        var model = new MixedTrackableCollectionsModel();
        var shared = new SimpleModel { Name = "Shared" };
        model.DirectChild = shared;
        model.TrackableList.Add(shared);
        model.TrackableDict["shared"] = shared;

        // Act
        model.MarkClean(recursive: true);

        // Assert
        shared.HasChanges().Should().BeFalse("共享的对象应该被清理（至少一个路径会清理它）");
    }
}
