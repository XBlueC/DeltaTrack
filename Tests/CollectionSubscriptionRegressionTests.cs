using DeltaTrack;
using Xunit;

namespace Tests;

/// <summary>
/// P1：v1.1.0 修复——ChangeTracker.Subscribe 不再对 IDictionary/ICollection 走 foreach 分支。
/// 修复前：集合字段 setter 会让父级订阅同一元素两次（包装器 InitializeExistingItems 一次 + Subscribe 集合分支一次），
/// 导致单次元素属性变更让父级 OnChanged 触发两次。这里给三种集合各留一条防回归。
/// </summary>
public class CollectionSubscriptionRegressionTests
{
    [Fact]
    public void List_Element_Mutation_Triggers_Parent_Once()
    {
        var m = new MixedTrackableCollectionsModel();
        var child = new SimpleModel();
        m.TrackableList.Add(child);
        m.MarkClean(true);

        var count = 0;
        m.OnChanged += () => count++;

        child.Name = "x";

        Assert.Equal(1, count);
    }

    [Fact]
    public void Dictionary_Value_Mutation_Triggers_Parent_Once()
    {
        var m = new MixedTrackableCollectionsModel();
        var child = new SimpleModel();
        m.TrackableDict["k"] = child;
        m.MarkClean(true);

        var count = 0;
        m.OnChanged += () => count++;

        child.Name = "x";

        Assert.Equal(1, count);
    }

    [Fact]
    public void Set_Element_Mutation_Triggers_Parent_Once()
    {
        var m = new SetOfTrackableModel();
        var child = new SimpleModel();
        m.Items.Add(child);
        m.MarkClean(true);

        var count = 0;
        m.OnChanged += () => count++;

        child.Name = "x";

        Assert.Equal(1, count);
    }

    [Fact]
    public void TrackableList_Initialized_With_Existing_Items_Auto_Subscribes()
    {
        // Part A 的 TrackableCollectionBase.InitializeExistingItems 必须自动订阅构造时传入的元素
        var inner = new List<SimpleModel> { new SimpleModel() };
        var changed = false;
        var list = new TrackableList<SimpleModel>(() => changed = true, inner);

        inner[0].Name = "x";

        Assert.True(changed);
        // 引用仍然是同一对象（包装器不复制）
        Assert.Same(inner[0], list[0]);
    }
}