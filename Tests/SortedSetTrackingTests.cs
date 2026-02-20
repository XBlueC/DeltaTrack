namespace Tests;

public class SortedSetTrackingTests
{
    [Fact]
    public void SortedSet_Add_MarksDirty()
    {
        var order = new Order();
        order.MarkClean();

        order.SortedScores.Add(85);
        order.SortedScores.Add(92);

        Assert.True(order.IsDirty());
        Assert.Contains("SortedScores", order.GetDirtyFields());
    }

    [Fact]
    public void SortedSet_Remove_MarksDirty()
    {
        var order = new Order();
        order.SortedScores.Add(85);
        order.SortedScores.Add(92);
        order.MarkClean();

        var removed = order.SortedScores.Remove(85);

        Assert.True(removed);
        Assert.True(order.IsDirty());
        Assert.Contains("SortedScores", order.GetDirtyFields());
    }

    [Fact]
    public void SortedSet_Clear_MarksDirty()
    {
        var order = new Order();
        order.SortedScores.Add(85);
        order.SortedScores.Add(92);
        order.MarkClean();

        order.SortedScores.Clear();

        Assert.True(order.IsDirty());
        Assert.Contains("SortedScores", order.GetDirtyFields());
    }

    [Fact]
    public void SortedSet_Properties_WorkCorrectly()
    {
        var order = new Order();
        order.SortedScores.Add(85);
        order.SortedScores.Add(92);
        order.SortedScores.Add(78);

        // 验证排序功能 - 通过枚举验证排序正确性
        var enumerated = order.SortedScores.ToList();
        Assert.Equal(new[] { 78, 85, 92 }, enumerated);
                
        // 验证集合操作
        Assert.True(order.SortedScores.Contains(78));
        Assert.True(order.SortedScores.Contains(85));
        Assert.True(order.SortedScores.Contains(92));
        Assert.False(order.SortedScores.Contains(100));
    }

    [Fact]
    public void SortedSet_SetOperations_MarkDirty()
    {
        var order = new Order();
        order.SortedScores.Add(1);
        order.SortedScores.Add(2);
        order.MarkClean();

        order.SortedScores.UnionWith(new[] { 3, 4 });
        Assert.True(order.IsDirty());

        order.MarkClean();
        order.SortedScores.IntersectWith(new[] { 2, 3, 4 });
        Assert.True(order.IsDirty());
    }
}