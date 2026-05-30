using DeltaTrack;
using Xunit;

namespace Tests;

/// <summary>
/// P0：SuspendTracking 作用域。
/// 验证嵌套引用计数、Dispose 幂等性、嵌套 ITrackable 冒泡静默、IsSuspended 状态。
/// </summary>
public class SuspendTrackingTests
{
    [Fact]
    public void Single_Suspend_Skips_Dirty_Marking()
    {
        var m = new SimpleModel();
        var changedCount = 0;
        m.OnChanged += () => changedCount++;

        using (m.SuspendTracking())
        {
            m.Name = "Alice";
            m.Age = 30;
        }

        Assert.False(m.HasChanges());
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void Suspend_Still_Writes_Field_Value()
    {
        // 挂起只压制脏标记，字段赋值依然生效（这是反序列化场景的核心契约）
        var m = new SimpleModel();
        using (m.SuspendTracking())
        {
            m.Name = "Alice";
            m.Age = 42;
        }

        Assert.Equal("Alice", m.Name);
        Assert.Equal(42, m.Age);
    }

    [Fact]
    public void Tracking_Resumes_After_Dispose()
    {
        var m = new SimpleModel();
        using (m.SuspendTracking())
        {
            m.Name = "x";
        }

        m.Age = 1;

        Assert.True(m.HasChanges());
        Assert.Contains("Age", m.GetChangedProperties());
        Assert.DoesNotContain("Name", m.GetChangedProperties());
    }

    [Fact]
    public void Nested_Suspend_Is_Refcounted()
    {
        // 内层 dispose 后仍处于挂起；外层 dispose 才真正恢复
        var m = new SimpleModel();
        using (m.SuspendTracking())
        {
            using (m.SuspendTracking())
            {
                m.Name = "a";
            }

            m.Age = 1; // 依然挂起
        }

        Assert.False(m.HasChanges());

        m.IsActive = true; // 全部退出后才追踪
        Assert.True(m.HasChanges());
    }

    [Fact]
    public void Disposing_Same_Scope_Twice_Is_Safe()
    {
        // SuspendScope 内部用 Interlocked.Exchange 防重复 Dispose；
        // 错配的 dispose 不应该把 _suspendCount 减成负数
        var m = new SimpleModel();
        var scope = m.SuspendTracking();
        scope.Dispose();
        scope.Dispose(); // 第二次必须无副作用

        m.Name = "x";
        Assert.True(m.HasChanges());
    }

    [Fact]
    public void Suspend_Silences_Nested_Trackable_Bubble()
    {
        // 父对象处于挂起，修改子对象虽然子对象自己会变脏，但冒泡到父对象时被丢弃
        var p = new NestedModel();
        var bubbled = 0;
        p.OnChanged += () => bubbled++;

        using (p.SuspendTracking())
        {
            p.Child.Name = "x";
        }

        Assert.False(p.HasChanges());
        Assert.Equal(0, bubbled);
    }

    [Fact]
    public void IsSuspended_Reflects_Tracker_State()
    {
        var t = new ChangeTracker(1);
        Assert.False(t.IsSuspended);

        var s = t.SuspendTracking();
        Assert.True(t.IsSuspended);

        s.Dispose();
        Assert.False(t.IsSuspended);
    }

    [Fact]
    public void Suspend_Skips_OnChangedDetailed_Too()
    {
        var m = new SimpleModel();
        var detailed = 0;
        m.OnChangedDetailed += _ => detailed++;

        using (m.SuspendTracking())
        {
            m.Name = "a";
            m.Age = 1;
        }

        Assert.Equal(0, detailed);
    }
}