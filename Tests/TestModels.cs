using DeltaTrack;

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