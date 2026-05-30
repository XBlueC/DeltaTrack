using DeltaTrack;

// [TrackIgnore] 标记的字段会被源生成器跳过，运行时既不读也不写——
// 因此 CS0169（从未使用）/ CS0414（赋值但未使用）在测试模型里是预期行为。
#pragma warning disable CS0169, CS0414

namespace Tests;

/// <summary>
/// 测试用的基础模型类 - 包含各种可跟踪的字段类型
/// </summary>
[Trackable]
public partial class SimpleModel
{
    [TrackableField] private string _name = "";
    [TrackableField] private int _age;
    [TrackableField] private DateTime _birthDate;
    [TrackableField] private bool _isActive;
}

/// <summary>
/// 测试不带 TrackableAttribute 但有 TrackableField 的类
/// </summary>
public partial class ModelWithoutTrackableAttribute
{
    [TrackableField] private string _name = "";
    [TrackableField] private int _age;
    [TrackableField] private DateTime _birthDate;
    [TrackableField] private bool _isActive;
}

/// <summary>
/// 测试带 TrackableAttribute 自动追踪私有字段（无需 TrackableField）
/// </summary>
[Trackable]
public partial class AutoTrackModel
{
    private string _autoName = "";
    private int _autoAge;
    private DateTime _autoBirthDate;
    [TrackIgnore] private string _ignoredField = "";
    private bool _autoIsActive;
}

/// <summary>
/// 测试 TrackIgnoreAttribute 排除字段追踪
/// </summary>
[Trackable]
public partial class ModelWithIgnore
{
    private string _trackedField = "";
    [TrackIgnore] private string _ignoredField = "";
    [TrackIgnore] private int _ignoredNumber;
}

/// <summary>
/// 测试用的集合模型类 - 包含各种可跟踪集合
/// </summary>
[Trackable]
public partial class CollectionModel
{
    [TrackableField] private List<string> _tags = new();
    [TrackableField] private Dictionary<string, string> _metadata = new();
    [TrackableField] private HashSet<int> _numbers = new();
    [TrackableField] private List<string> _trackableItems = new();
}

/// <summary>
/// 测试用的嵌套对象模型类
/// </summary>
[Trackable]
public partial class NestedModel
{
    [TrackableField] private SimpleModel _child = new();
    [TrackableField] private List<SimpleModel> _children = new();
    [TrackableField] private Dictionary<string, SimpleModel> _namedChildren = new();
}

/// <summary>
/// 测试用的复杂模型类 - 组合多种场景
/// </summary>
[Trackable]
public partial class ComplexModel
{
    [TrackableField] private string _title = "";
    [TrackableField] private List<string> _categories = new();
    [TrackableField] private SimpleModel _primaryContact = new();
    [TrackableField] private Dictionary<string, NestedModel> _sections = new();
    [TrackableField] private Dictionary<string, string> _settings = new();
}

/// <summary>
/// 测试用的三层嵌套模型 - 用于测试深层递归清理
/// </summary>
[Trackable]
public partial class ThreeLevelModel
{
    [TrackableField] private string _label = "";
    [TrackableField] private NestedModel _nested = new();
}

/// <summary>
/// 测试用的 HashSet 包含可追踪对象的模型
/// </summary>
[Trackable]
public partial class SetOfTrackableModel
{
    [TrackableField] private string _name = "";
    [TrackableField] private HashSet<SimpleModel> _items = new();
}

/// <summary>
/// 测试用的多种可追踪集合混合模型
/// </summary>
[Trackable]
public partial class MixedTrackableCollectionsModel
{
    [TrackableField] private List<SimpleModel> _trackableList = new();
    [TrackableField] private Dictionary<string, SimpleModel> _trackableDict = new();
    [TrackableField] private SimpleModel _directChild = new();
}

/// <summary>
/// 65 字段模型 —— 验证 long[] 多 slot 分级路径（>64 字段时不再生成 DirtyFlag enum，
/// 改走 FieldIndex 常量类 + 多 slot MarkChanged）
/// </summary>
[Trackable]
public partial class WideModel
{
    [TrackableField] private int _f00;
    [TrackableField] private int _f01;
    [TrackableField] private int _f02;
    [TrackableField] private int _f03;
    [TrackableField] private int _f04;
    [TrackableField] private int _f05;
    [TrackableField] private int _f06;
    [TrackableField] private int _f07;
    [TrackableField] private int _f08;
    [TrackableField] private int _f09;
    [TrackableField] private int _f10;
    [TrackableField] private int _f11;
    [TrackableField] private int _f12;
    [TrackableField] private int _f13;
    [TrackableField] private int _f14;
    [TrackableField] private int _f15;
    [TrackableField] private int _f16;
    [TrackableField] private int _f17;
    [TrackableField] private int _f18;
    [TrackableField] private int _f19;
    [TrackableField] private int _f20;
    [TrackableField] private int _f21;
    [TrackableField] private int _f22;
    [TrackableField] private int _f23;
    [TrackableField] private int _f24;
    [TrackableField] private int _f25;
    [TrackableField] private int _f26;
    [TrackableField] private int _f27;
    [TrackableField] private int _f28;
    [TrackableField] private int _f29;
    [TrackableField] private int _f30;
    [TrackableField] private int _f31;
    [TrackableField] private int _f32;
    [TrackableField] private int _f33;
    [TrackableField] private int _f34;
    [TrackableField] private int _f35;
    [TrackableField] private int _f36;
    [TrackableField] private int _f37;
    [TrackableField] private int _f38;
    [TrackableField] private int _f39;
    [TrackableField] private int _f40;
    [TrackableField] private int _f41;
    [TrackableField] private int _f42;
    [TrackableField] private int _f43;
    [TrackableField] private int _f44;
    [TrackableField] private int _f45;
    [TrackableField] private int _f46;
    [TrackableField] private int _f47;
    [TrackableField] private int _f48;
    [TrackableField] private int _f49;
    [TrackableField] private int _f50;
    [TrackableField] private int _f51;
    [TrackableField] private int _f52;
    [TrackableField] private int _f53;
    [TrackableField] private int _f54;
    [TrackableField] private int _f55;
    [TrackableField] private int _f56;
    [TrackableField] private int _f57;
    [TrackableField] private int _f58;
    [TrackableField] private int _f59;
    [TrackableField] private int _f60;
    [TrackableField] private int _f61;
    [TrackableField] private int _f62;

    [TrackableField] private int _f63;

    // 第 65 个字段：跨入 slot 1
    [TrackableField] private int _f64;
}