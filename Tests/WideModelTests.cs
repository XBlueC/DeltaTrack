using DeltaTrack;
using Xunit;

namespace Tests;

/// <summary>
/// 验证字段数 > 64 时的 long[] 多 slot 分级路径：
/// - HasChanges / GetChangedProperties / GetDelta / ApplyDelta 都能跨 slot 工作
/// - OnChangedDetailed 携带正确的 Slot 与 DirtyFlags
/// - MarkClean 同时清空所有 slot
/// - FieldIndex 常量类正确生成（编译期可见即视为通过）
/// </summary>
public class WideModelTests
{
    [Fact]
    public void Slot0_Field_Marks_HasChanges_And_Reports_PropertyName()
    {
        var m = new WideModel();
        Assert.False(m.HasChanges());

        m.F00 = 1;

        Assert.True(m.HasChanges());
        var changed = m.GetChangedProperties();
        Assert.Contains("F00", changed);
        Assert.Single(changed);
    }

    [Fact]
    public void Slot1_Field_Marks_HasChanges_And_Reports_PropertyName()
    {
        var m = new WideModel();
        m.F64 = 42;

        Assert.True(m.HasChanges());
        var changed = m.GetChangedProperties();
        Assert.Contains("F64", changed);
        Assert.Single(changed);
    }

    [Fact]
    public void Both_Slots_Dirty_Reports_All_Changed_Properties()
    {
        var m = new WideModel();
        m.F00 = 1;
        m.F32 = 2;
        m.F63 = 3; // slot 0 最后一位
        m.F64 = 4; // slot 1 第一位

        var changed = m.GetChangedProperties();
        Assert.Equal(4, changed.Count);
        Assert.Contains("F00", changed);
        Assert.Contains("F32", changed);
        Assert.Contains("F63", changed);
        Assert.Contains("F64", changed);
    }

    [Fact]
    public void GetDelta_Includes_Fields_Across_Slots()
    {
        var m = new WideModel();
        m.F10 = 100;
        m.F64 = 200;

        var delta = m.GetDelta();
        Assert.Equal(2, delta.Count);
        Assert.Equal(100, (int)delta["F10"]);
        Assert.Equal(200, (int)delta["F64"]);
    }

    [Fact]
    public void ApplyDelta_Restores_Fields_Across_Slots()
    {
        var m1 = new WideModel();
        m1.F05 = 5;
        m1.F64 = 64;
        var delta = m1.GetDelta();

        var m2 = new WideModel();
        m2.ApplyDelta(delta);

        Assert.Equal(5, m2.F05);
        Assert.Equal(64, m2.F64);
    }

    [Fact]
    public void OnChangedDetailed_Carries_Correct_Slot_And_Flag()
    {
        var m = new WideModel();
        ChangeInfo? captured = null;
        m.OnChangedDetailed += info => captured = info;

        m.F64 = 7;

        Assert.NotNull(captured);
        Assert.Equal(1, captured!.Value.Slot);
        Assert.Equal(1L << 0, captured.Value.DirtyFlags);
        Assert.Same(m, captured.Value.Source);
    }

    [Fact]
    public void OnChangedDetailed_Slot0_Field_Has_Slot_Zero()
    {
        var m = new WideModel();
        ChangeInfo? captured = null;
        m.OnChangedDetailed += info => captured = info;

        m.F03 = 9;

        Assert.NotNull(captured);
        Assert.Equal(0, captured!.Value.Slot);
        Assert.Equal(1L << 3, captured.Value.DirtyFlags);
    }

    [Fact]
    public void MarkClean_Clears_All_Slots()
    {
        var m = new WideModel();
        m.F00 = 1;
        m.F64 = 2;
        Assert.True(m.HasChanges());

        m.MarkClean();

        Assert.False(m.HasChanges());
        Assert.Empty(m.GetChangedProperties());
        Assert.Empty(m.GetDelta());
    }

    [Fact]
    public void MarkChanged_String_Targets_Slot1_Field()
    {
        var m = new WideModel();
        m.MarkChanged("F64");

        var changed = m.GetChangedProperties();
        Assert.Single(changed);
        Assert.Equal("F64", changed[0]);
    }

    [Fact]
    public void SuspendTracking_Skips_Marks_Across_Slots()
    {
        var m = new WideModel();
        var changedCount = 0;
        m.OnChanged += () => changedCount++;

        using (m.SuspendTracking())
        {
            m.F00 = 1;
            m.F64 = 2;
        }

        Assert.False(m.HasChanges());
        Assert.Equal(0, changedCount);
        Assert.Equal(1, m.F00);
        Assert.Equal(2, m.F64);
    }

    [Fact]
    public void FieldIndex_Constants_Are_Generated()
    {
        // 编译期可见即说明常量类已生成；同时校验值的连续性
        Assert.Equal(0, WideModel.FieldIndex.F00);
        Assert.Equal(63, WideModel.FieldIndex.F63);
        Assert.Equal(64, WideModel.FieldIndex.F64);
    }
}