using DeltaTrack;
using Xunit;

namespace Tests;

/// <summary>
/// P0：OnChangedDetailed 事件携带的 ChangeInfo 元数据。
/// 重点验证 Source / DirtyFlags（增量值）/ Slot 三个字段在常见场景下的行为。
/// </summary>
public class OnChangedDetailedTests
{
    [Fact]
    public void SimpleField_Mutation_Source_Is_Self()
    {
        var m = new SimpleModel();
        ChangeInfo? captured = null;
        m.OnChangedDetailed += info => captured = info;

        m.Name = "Alice";

        Assert.NotNull(captured);
        Assert.Same(m, captured!.Value.Source);
    }

    [Fact]
    public void DirtyFlags_Carries_Field_Bit()
    {
        var m = new SimpleModel();
        ChangeInfo? captured = null;
        m.OnChangedDetailed += info => captured = info;

        m.Name = "Alice"; // 第 0 位

        Assert.Equal((long)SimpleModel.DirtyFlag.Name, captured!.Value.DirtyFlags);
    }

    [Fact]
    public void DirtyFlags_Is_Incremental_Not_Cumulative()
    {
        // 每一次 MarkChanged 投递的 DirtyFlags 只包含本次点亮的位，不会累积之前的位
        var m = new SimpleModel();
        var captures = new List<long>();
        m.OnChangedDetailed += info => captures.Add(info.DirtyFlags);

        m.Name = "Alice";
        m.Age = 30;

        Assert.Equal(2, captures.Count);
        Assert.Equal((long)SimpleModel.DirtyFlag.Name, captures[0]);
        Assert.Equal((long)SimpleModel.DirtyFlag.Age, captures[1]); // 不含 Name 位
    }

    [Fact]
    public void Slot_Is_Zero_For_Small_Class()
    {
        var m = new SimpleModel();
        ChangeInfo? captured = null;
        m.OnChangedDetailed += info => captured = info;

        m.Name = "x";

        Assert.Equal(0, captured!.Value.Slot);
    }

    [Fact]
    public void Multiple_Changes_Trigger_Multiple_Events()
    {
        var m = new SimpleModel();
        var count = 0;
        m.OnChangedDetailed += _ => count++;

        m.Name = "a";
        m.Age = 1;
        m.IsActive = true;

        Assert.Equal(3, count);
    }

    [Fact]
    public void Same_Field_Mutation_Twice_Triggers_Twice()
    {
        // 即使脏位已经点亮，第二次赋值（值不同）仍然触发事件
        var m = new SimpleModel();
        var count = 0;
        m.OnChangedDetailed += _ => count++;

        m.Name = "a";
        m.Name = "b";

        Assert.Equal(2, count);
    }

    [Fact]
    public void Nested_Bubble_Source_Is_Parent_Not_Child()
    {
        // 当前实现：子对象变更经简单 OnChanged 冒泡到父对象，父对象再调用自己的 MarkChanged，
        // 父对象 OnChangedDetailed 收到的 Source 永远是父对象自己，DirtyFlags 指向父对象的子字段位。
        // 子对象自身的 OnChangedDetailed 不会被父对象的订阅者收到。
        var parent = new NestedModel();
        ChangeInfo? captured = null;
        parent.OnChangedDetailed += info => captured = info;

        parent.Child.Name = "x";

        Assert.NotNull(captured);
        Assert.Same(parent, captured!.Value.Source);
        Assert.Equal((long)NestedModel.DirtyFlag.Child, captured.Value.DirtyFlags);
    }

    [Fact]
    public void Detailed_Subscriber_Independent_Of_OnChanged()
    {
        // OnChanged 与 OnChangedDetailed 都会触发，互不干扰
        var m = new SimpleModel();
        var simple = 0;
        var detailed = 0;
        m.OnChanged += () => simple++;
        m.OnChangedDetailed += _ => detailed++;

        m.Name = "x";

        Assert.Equal(1, simple);
        Assert.Equal(1, detailed);
    }
}