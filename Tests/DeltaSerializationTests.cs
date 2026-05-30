using DeltaTrack;
using Xunit;

namespace Tests;

/// <summary>
/// P0：GetDelta / ApplyDelta 增量序列化与回灌。
/// 涵盖空字典、null、未知 key、类型不匹配、数值强转（JSON 反序列化场景）、集合字段往返、
/// 仅返回当前脏字段而非全部字段、ApplyDelta 正确触发 OnChanged。
/// </summary>
public class DeltaSerializationTests
{
    [Fact]
    public void GetDelta_Empty_When_No_Changes()
    {
        var m = new SimpleModel();
        var delta = m.GetDelta();
        Assert.Empty(delta);
    }

    [Fact]
    public void GetDelta_Returns_Only_Dirty_Field()
    {
        var m = new SimpleModel();
        m.Name = "Alice";

        var delta = m.GetDelta();

        Assert.Single(delta);
        Assert.Equal("Alice", delta["Name"]);
    }

    [Fact]
    public void GetDelta_After_MarkClean_Excludes_Previously_Changed()
    {
        var m = new SimpleModel();
        m.Name = "Alice";
        m.Age = 30;
        m.MarkClean();
        m.Name = "Bob";

        var delta = m.GetDelta();

        Assert.Single(delta);
        Assert.Equal("Bob", delta["Name"]);
    }

    [Fact]
    public void ApplyDelta_Roundtrip_Simple_Fields()
    {
        var src = new SimpleModel();
        src.Name = "Alice";
        src.Age = 30;
        src.IsActive = true;
        var delta = src.GetDelta();

        var dst = new SimpleModel();
        dst.ApplyDelta(delta);

        Assert.Equal("Alice", dst.Name);
        Assert.Equal(30, dst.Age);
        Assert.True(dst.IsActive);
    }

    [Fact]
    public void ApplyDelta_Null_Is_NoOp()
    {
        var m = new SimpleModel();
        m.ApplyDelta(null!); // 应静默返回，不抛
        Assert.False(m.HasChanges());
    }

    [Fact]
    public void ApplyDelta_Empty_Is_NoOp()
    {
        var m = new SimpleModel();
        m.ApplyDelta(new Dictionary<string, object>());
        Assert.False(m.HasChanges());
    }

    [Fact]
    public void ApplyDelta_Unknown_Key_Is_Ignored()
    {
        var m = new SimpleModel();
        var delta = new Dictionary<string, object>
        {
            ["NonExistent"] = "x",
            ["Name"] = "Alice"
        };

        m.ApplyDelta(delta); // 不应抛
        Assert.Equal("Alice", m.Name);
    }

    [Fact]
    public void ApplyDelta_Numeric_Coercion_Long_To_Int()
    {
        // JSON 反序列化常把整数解析成 long；DeltaCast 走 Convert.ChangeType 完成强转
        var m = new SimpleModel();
        var delta = new Dictionary<string, object> { ["Age"] = (long)42 };

        m.ApplyDelta(delta);

        Assert.Equal(42, m.Age);
    }

    [Fact]
    public void ApplyDelta_Type_Mismatch_Throws()
    {
        // 无法从字符串 "abc" 转成 int，Convert.ChangeType 抛 FormatException
        var m = new SimpleModel();
        var delta = new Dictionary<string, object> { ["Age"] = "abc" };

        Assert.ThrowsAny<Exception>(() => m.ApplyDelta(delta));
    }

    [Fact]
    public void ApplyDelta_Triggers_OnChanged_Per_Field()
    {
        var m = new SimpleModel();
        var count = 0;
        m.OnChanged += () => count++;

        m.ApplyDelta(new Dictionary<string, object>
        {
            ["Name"] = "x",
            ["Age"] = 1
        });

        Assert.Equal(2, count);
        Assert.True(m.HasChanges());
    }

    [Fact]
    public void GetDelta_With_Collection_Field_Returns_Wrapper()
    {
        var m = new CollectionModel();
        m.Tags.Add("a");

        var delta = m.GetDelta();

        Assert.Contains("Tags", delta.Keys);
        Assert.NotNull(delta["Tags"]);
    }

    [Fact]
    public void ApplyDelta_With_Collection_Field_Roundtrip()
    {
        var src = new CollectionModel();
        src.Tags.Add("a");
        src.Tags.Add("b");
        var delta = src.GetDelta();

        var dst = new CollectionModel();
        dst.ApplyDelta(delta);

        Assert.Contains("a", dst.Tags);
        Assert.Contains("b", dst.Tags);
        Assert.Equal(2, dst.Tags.Count);
    }

    [Fact]
    public void ApplyDelta_With_Dictionary_Field()
    {
        var src = new CollectionModel();
        src.Metadata["k"] = "v";
        var delta = src.GetDelta();

        var dst = new CollectionModel();
        dst.ApplyDelta(delta);

        Assert.Equal("v", dst.Metadata["k"]);
    }

    [Fact]
    public void Suspend_Plus_ApplyDelta_Yields_Clean_State()
    {
        // 反序列化经典模式：先 SuspendTracking，再 ApplyDelta，结束后 HasChanges 为 false
        var m = new SimpleModel();
        using (m.SuspendTracking())
        {
            m.ApplyDelta(new Dictionary<string, object>
            {
                ["Name"] = "Alice",
                ["Age"] = 30
            });
        }

        Assert.Equal("Alice", m.Name);
        Assert.Equal(30, m.Age);
        Assert.False(m.HasChanges());
    }
}