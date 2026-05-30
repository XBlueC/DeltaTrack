using DeltaTrack;
using Xunit;

namespace Tests;

/// <summary>
/// P2：直接对 ChangeTracker 做白盒测试，覆盖生成代码不会经过的边界。
/// 包括多 slot 构造、越界 slot 静默丢弃、DirtyFlagsArray 视图，
/// 以及 ApplyDelta 内部使用的 DeltaCast 数值强转/null/引用类型透传。
/// </summary>
public class ChangeTrackerLowLevelTests
{
    // ---- 构造与 slot ----

    [Fact]
    public void Default_Constructor_Has_Single_Slot()
    {
        var t = new ChangeTracker(1);
        Assert.Equal(1, t.SlotCount);
        Assert.Single(t.DirtyFlagsArray);
    }

    [Fact]
    public void Custom_SlotCount_Allocates_Array()
    {
        var t = new ChangeTracker(3);
        Assert.Equal(3, t.SlotCount);
        Assert.Equal(3, t.DirtyFlagsArray.Length);
    }

    [Fact]
    public void Zero_Or_Negative_SlotCount_Coerced_To_One()
    {
        Assert.Equal(1, new ChangeTracker(0).SlotCount);
        Assert.Equal(1, new ChangeTracker(-5).SlotCount);
    }

    [Fact]
    public void MarkChanged_OutOfRange_Slot_Is_Silent()
    {
        var t = new ChangeTracker(2);
        t.MarkChanged(99, 1L);
        t.MarkChanged(-1, 1L);

        Assert.False(t.HasChanges());
    }

    [Fact]
    public void DirtyFlagsArray_Reflects_All_Slots()
    {
        var t = new ChangeTracker(2);
        t.MarkChanged(0, 1L << 5);
        t.MarkChanged(1, 1L << 10);

        Assert.Equal(1L << 5, t.DirtyFlagsArray[0]);
        Assert.Equal(1L << 10, t.DirtyFlagsArray[1]);
        Assert.True(t.HasChanges());
    }

    [Fact]
    public void MarkClean_Clears_All_Slots()
    {
        var t = new ChangeTracker(2);
        t.MarkChanged(0, 1L);
        t.MarkChanged(1, 1L);

        t.MarkClean();

        Assert.False(t.HasChanges());
        Assert.Equal(0L, t.DirtyFlagsArray[0]);
        Assert.Equal(0L, t.DirtyFlagsArray[1]);
    }

    // ---- OnChangedDetailed 直接路径 ----

    [Fact]
    public void OnChangedDetailed_Carries_Slot_And_Source()
    {
        var t = new ChangeTracker(2);
        var src = new SimpleModel();
        ChangeInfo? captured = null;
        t.OnChangedDetailed += info => captured = info;

        t.MarkChanged(1, 1L << 7, src);

        Assert.NotNull(captured);
        Assert.Equal(1, captured!.Value.Slot);
        Assert.Equal(1L << 7, captured.Value.DirtyFlags);
        Assert.Same(src, captured.Value.Source);
    }

    [Fact]
    public void OnChangedDetailed_Null_Source_Skips_Detailed_Event()
    {
        // MarkChanged(slot, flag) 重载内部以 source=null 调用，不应触发 OnChangedDetailed
        var t = new ChangeTracker(1);
        var detailed = 0;
        var simple = 0;
        t.OnChangedDetailed += _ => detailed++;
        t.OnChanged += () => simple++;

        t.MarkChanged(0, 1L); // 无 source

        Assert.Equal(1, simple);
        Assert.Equal(0, detailed);
    }

    // ---- DeltaCast<T> ----

    [Fact]
    public void DeltaCast_Same_Type_Direct()
    {
        Assert.Equal("hello", ChangeTracker.DeltaCast<string>("hello"));
    }

    [Fact]
    public void DeltaCast_Null_Returns_Default_For_Reference()
    {
        Assert.Null(ChangeTracker.DeltaCast<string>(null!));
    }

    [Fact]
    public void DeltaCast_Null_Returns_Default_For_Value()
    {
        Assert.Equal(0, ChangeTracker.DeltaCast<int>(null!));
        Assert.False(ChangeTracker.DeltaCast<bool>(null!));
    }

    [Fact]
    public void DeltaCast_Long_To_Int()
    {
        Assert.Equal(42, ChangeTracker.DeltaCast<int>((long)42));
    }

    [Fact]
    public void DeltaCast_Int_To_Long()
    {
        Assert.Equal(42L, ChangeTracker.DeltaCast<long>(42));
    }

    [Fact]
    public void DeltaCast_Double_To_Int()
    {
        Assert.Equal(3, ChangeTracker.DeltaCast<int>(3.0));
    }

    [Fact]
    public void DeltaCast_Reference_Type_Direct()
    {
        var sm = new SimpleModel();
        Assert.Same(sm, ChangeTracker.DeltaCast<SimpleModel>(sm));
    }

    [Fact]
    public void DeltaCast_Incompatible_Reference_Throws()
    {
        // 非数值/非字符串/非可空目标，类型不匹配直接 InvalidCastException
        Assert.Throws<InvalidCastException>(() => ChangeTracker.DeltaCast<SimpleModel>(new NestedModel()));
    }

    // ---- DeltaCast 深度覆盖：enum / Nullable ----

    private enum CastEnum
    {
        A = 0,
        B = 1,
        C = 2
    }

    [Fact]
    public void DeltaCast_Enum_From_Long()
    {
        // System.Text.Json 默认读整数为 long
        Assert.Equal(CastEnum.B, ChangeTracker.DeltaCast<CastEnum>(1L));
    }

    [Fact]
    public void DeltaCast_Enum_From_Int()
    {
        Assert.Equal(CastEnum.C, ChangeTracker.DeltaCast<CastEnum>(2));
    }

    [Fact]
    public void DeltaCast_Enum_From_String()
    {
        // Enum.Parse 走不区分大小写
        Assert.Equal(CastEnum.B, ChangeTracker.DeltaCast<CastEnum>("b"));
    }

    [Fact]
    public void DeltaCast_Nullable_Int_From_Long()
    {
        int? actual = ChangeTracker.DeltaCast<int?>(42L);
        Assert.Equal(42, actual);
    }

    [Fact]
    public void DeltaCast_Nullable_Enum_From_Long()
    {
        CastEnum? actual = ChangeTracker.DeltaCast<CastEnum?>(2L);
        Assert.Equal(CastEnum.C, actual);
    }

    [Fact]
    public void DeltaCast_Nullable_DateTime_From_DateTime()
    {
        var dt = new DateTime(2025, 1, 15, 10, 30, 0);
        DateTime? actual = ChangeTracker.DeltaCast<DateTime?>(dt);
        Assert.Equal(dt, actual);
    }

    [Fact]
    public void DeltaCast_Nullable_Decimal_From_Double()
    {
        decimal? actual = ChangeTracker.DeltaCast<decimal?>(3.14);
        Assert.Equal(3.14m, actual);
    }

    [Fact]
    public void DeltaCast_Nullable_Null_Returns_Null()
    {
        Assert.Null(ChangeTracker.DeltaCast<int?>(null!));
    }

    // ---- DeltaCast 补充覆盖：IConvertible 跨类别、Flags enum、快路径变体 ----

    [Flags]
    private enum CastFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
        Exec = 4
    }

    [Fact]
    public void DeltaCast_Bool_From_Int()
    {
        // Convert.ChangeType: 0 = false, 非0 = true
        Assert.True(ChangeTracker.DeltaCast<bool>(1));
        Assert.False(ChangeTracker.DeltaCast<bool>(0));
    }

    [Fact]
    public void DeltaCast_Bool_From_String()
    {
        Assert.True(ChangeTracker.DeltaCast<bool>("true"));
        Assert.True(ChangeTracker.DeltaCast<bool>("True"));
        Assert.False(ChangeTracker.DeltaCast<bool>("false"));
    }

    [Fact]
    public void DeltaCast_String_From_Int()
    {
        Assert.Equal("42", ChangeTracker.DeltaCast<string>(42));
    }

    [Fact]
    public void DeltaCast_Int_From_String()
    {
        // 使用 InvariantCulture 避免区域影响
        Assert.Equal(42, ChangeTracker.DeltaCast<int>("42"));
    }

    [Fact]
    public void DeltaCast_DateTime_From_String()
    {
        // ISO 8601 不受区域影响
        var actual = ChangeTracker.DeltaCast<DateTime>("2025-01-15T10:30:00");
        Assert.Equal(new DateTime(2025, 1, 15, 10, 30, 0), actual);
    }

    [Fact]
    public void DeltaCast_Decimal_From_Long()
    {
        Assert.Equal(42m, ChangeTracker.DeltaCast<decimal>(42L));
    }

    [Fact]
    public void DeltaCast_Long_To_Int_Overflow_Throws()
    {
        // long.MaxValue 超出 int 范围，Convert.ChangeType 招 OverflowException
        Assert.Throws<OverflowException>(() => ChangeTracker.DeltaCast<int>(long.MaxValue));
    }

    [Fact]
    public void DeltaCast_Enum_From_Invalid_String_Throws()
    {
        // 不存在的名称 → ArgumentException
        Assert.Throws<ArgumentException>(() => ChangeTracker.DeltaCast<CastEnum>("NotAMember"));
    }

    [Fact]
    public void DeltaCast_Flags_Enum_From_Long()
    {
        // 位组合 5 = Read | Exec
        var actual = ChangeTracker.DeltaCast<CastFlags>(5L);
        Assert.Equal(CastFlags.Read | CastFlags.Exec, actual);
    }

    [Fact]
    public void DeltaCast_Nullable_Bool_From_Int()
    {
        bool? actual = ChangeTracker.DeltaCast<bool?>(1);
        Assert.True(actual);
    }

    [Fact]
    public void DeltaCast_Derived_To_Base_Direct()
    {
        // 派生类型 赋值给 基类目标，应走 `value is T t` 快路径
        var derived = new NestedModel();
        var actual = ChangeTracker.DeltaCast<object>(derived);
        Assert.Same(derived, actual);
    }

    [Fact]
    public void DeltaCast_Interface_Target_Direct()
    {
        // 接口目标，实现类型的 value 走快路径
        var list = new System.Collections.Generic.List<int> { 1, 2, 3 };
        var actual = ChangeTracker.DeltaCast<System.Collections.Generic.IList<int>>(list);
        Assert.Same(list, actual);
    }

    // ---- DeltaCast 补充：Trackable 模型、库内集合、数组、泛型不匹配 ----

    [Fact]
    public void DeltaCast_Trackable_To_ITrackable_Interface()
    {
        // [Trackable] 生成的类实现了 ITrackable，可直接分配给接口目标（快路径）
        var sm = new SimpleModel();
        var actual = ChangeTracker.DeltaCast<ITrackable>(sm);
        Assert.Same(sm, actual);
    }

    [Fact]
    public void DeltaCast_TrackableList_To_IList()
    {
        // 生成器实际生成代码会把 TrackableList<T> 分配给 IList<T> 字段——这是最高频的集合路径
        var trackable = new TrackableList<int>(() => { }, new System.Collections.Generic.List<int> { 1, 2, 3 });
        var actual = ChangeTracker.DeltaCast<System.Collections.Generic.IList<int>>(trackable);
        Assert.Same(trackable, actual);
        Assert.Equal(3, actual.Count);
    }

    [Fact]
    public void DeltaCast_TrackableDictionary_To_IDictionary()
    {
        var inner = new System.Collections.Generic.Dictionary<string, int> { ["a"] = 1 };
        var trackable = new TrackableDictionary<string, int>(() => { }, inner);
        var actual = ChangeTracker.DeltaCast<System.Collections.Generic.IDictionary<string, int>>(trackable);
        Assert.Same(trackable, actual);
        Assert.Equal(1, actual["a"]);
    }

    [Fact]
    public void DeltaCast_Array_To_IList()
    {
        // int[] 隐式实现了 IList<int>，走 `value is T t` 快路径
        int[] arr = { 1, 2, 3 };
        var actual = ChangeTracker.DeltaCast<System.Collections.Generic.IList<int>>(arr);
        Assert.Same(arr, actual);
    }

    [Fact]
    public void DeltaCast_Generic_Mismatch_Throws()
    {
        // List<int> 不实现 IList<string>，也不是 IConvertible → 走到底 (T)value 招 InvalidCastException
        Assert.Throws<InvalidCastException>(() =>
            ChangeTracker.DeltaCast<System.Collections.Generic.IList<string>>(
                new System.Collections.Generic.List<int> { 1, 2, 3 }));
    }

    // ---- DeltaCast 补充：自定义 struct 与非 IConvertible 值类型 ----

    private struct CastPoint
    {
        public int X;
        public int Y;

        public CastPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    private struct CastSize
    {
#pragma warning disable CS0649 // 字段仅作为 DeltaCast 的目标类型参数，无需赋值
        public int W;
        public int H;
#pragma warning restore CS0649
    }

    [Fact]
    public void DeltaCast_Custom_Struct_Same_Type()
    {
        // 自定义 struct 同类型走 `value is T t` 快路径（包含拆箱）
        var p = new CastPoint(3, 5);
        var actual = ChangeTracker.DeltaCast<CastPoint>(p);
        Assert.Equal(3, actual.X);
        Assert.Equal(5, actual.Y);
    }

    [Fact]
    public void DeltaCast_Custom_Struct_Incompatible_Throws()
    {
        // 两个不相关的 struct，走到底 (T)value 招 InvalidCastException
        Assert.Throws<InvalidCastException>(() =>
            ChangeTracker.DeltaCast<CastSize>(new CastPoint(1, 2)));
    }

    [Fact]
    public void DeltaCast_Nullable_Custom_Struct()
    {
        // Nullable<自定义struct>：解包后 underlying 非 enum 且非 IConvertible，走 fallback (T)value
        // CLR 会把 (object)point 拆箱重装为 Nullable<CastPoint>
        var p = new CastPoint(7, 11);
        CastPoint? actual = ChangeTracker.DeltaCast<CastPoint?>(p);
        Assert.True(actual.HasValue);
        Assert.Equal(7, actual.Value.X);
        Assert.Equal(11, actual.Value.Y);
    }

    [Fact]
    public void DeltaCast_Guid_Same_Type()
    {
        // Guid 不实现 IConvertible、也不是 enum，但同类型会命中 `value is T t` 快路径
        var g = Guid.NewGuid();
        var actual = ChangeTracker.DeltaCast<Guid>(g);
        Assert.Equal(g, actual);
    }

    [Fact]
    public void DeltaCast_TimeSpan_Same_Type()
    {
        var ts = TimeSpan.FromMinutes(15);
        var actual = ChangeTracker.DeltaCast<TimeSpan>(ts);
        Assert.Equal(ts, actual);
    }

    [Fact]
    public void DeltaCast_String_To_Guid_Throws()
    {
        // 已知限制：Guid 不在 IConvertible 协议内，从字符串转换会走 fallback (T)value 失败
        // 实际使用中序列化器（STJ/Newtonsoft）会直接还原为 Guid 实例，命中快路径
        Assert.Throws<InvalidCastException>(() =>
            ChangeTracker.DeltaCast<Guid>("00000000-0000-0000-0000-000000000001"));
    }

    // ---- Dispose ----

    [Fact]
    public void Dispose_Clears_All_Slots_And_Events()
    {
        var t = new ChangeTracker(2);
        t.MarkChanged(0, 1L);
        t.MarkChanged(1, 1L);

        t.Dispose();

        Assert.False(t.HasChanges());
    }

    [Fact]
    public void Dispose_Twice_Is_Safe()
    {
        var t = new ChangeTracker(1);
        t.Dispose();
        t.Dispose();
    }
}