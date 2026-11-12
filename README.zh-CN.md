# DeltaTrack

[English](README.md) | [简体中文](README.zh-CN.md)

通过 Roslyn 源生成器在编译时生成属性和变更追踪代码，记录对象中哪些字段发生了变化，支持嵌套对象、集合、变更事件和增量数据导出。

[![NuGet](https://img.shields.io/nuget/v/DeltaTrack.svg)](https://www.nuget.org/packages/DeltaTrack/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0-blue.svg)](https://dotnet.microsoft.com/)
[![Build Status](https://github.com/XBlueC/DeltaTrack/actions/workflows/ci.yml/badge.svg)](https://github.com/XBlueC/DeltaTrack/actions)

## 安装

```bash
dotnet add package DeltaTrack
```

运行库面向 .NET Standard 2.0，NuGet 包包含源生成器和分析器。

## 快速开始

在 `partial` 类上添加 `[Trackable]`，生成器会为可追踪的私有字段生成公开属性，并实现 `ITrackable`。

```csharp
[Trackable]
public partial class Order
{
    private string _customerName = "";
    private decimal _amount;
    private Address? _address;
    private List<Address> _addresses = new List<Address>();
}

[Trackable]
public partial class Address
{
    private string _city = "";
    private string _detail = "";
}
```

通过生成的属性修改数据：

```csharp
var order = new Order();
order.CustomerName = "张三";
order.Amount = 99.5m;

order.HasChanges();            // true
order.GetChangedProperties();  // ["CustomerName", "Amount"]
order.GetDirtyFlags();         // CustomerName | Amount

order.MarkClean();             // 清除当前对象的变更标记
order.HasChanges();            // false
```

普通标量属性使用 `EqualityComparer<T>.Default` 判断值是否变化。重复赋相同值不会产生变更；追踪过的嵌套对象按引用身份判断，重复赋同一实例也不会产生变更。

**请通过生成的属性和集合包装器修改数据。** 直接修改 `_amount` 等原始字段，或者绕过包装器修改原始集合，不会自动记录变更。

## 选择追踪字段

### 追踪整个类

`[Trackable]` 自动追踪类中的私有实例字段，包括其他 `partial` 声明中的字段。静态字段、常量和 `readonly` 字段不参与追踪。

```csharp
[Trackable]
public partial class Customer
{
    private string _name = "";

    [TrackIgnore]
    private string _cachedDisplayName = "";
}
```

`[TrackIgnore]` 用于排除字段。

### 只追踪指定字段

不标记整个类时，可以单独使用 `[TrackableField]`。字段必须是私有字段，类必须是 `partial`。

```csharp
public partial class Player
{
    [TrackableField]
    private int _score;

    private int _internalCounter; // 不生成追踪属性
}
```

支持泛型类和嵌套类；嵌套类的外层类型也必须声明为 `partial`。特性别名和完整的 `TrackableFieldAttribute` 名称均可使用。

### 给生成的属性附加特性

```csharp
[Trackable]
public partial class Contact
{
    [AttachAttribute(typeof(JsonPropertyNameAttribute), "customer_name")]
    [AttachAttribute(typeof(MaxLengthAttribute), 100)]
    private string _customerName = "";
}
```

生成的 `CustomerName` 属性会带有 `[JsonPropertyName("customer_name")]` 和 `[MaxLength(100)]`。同一字段可以使用多个
`[AttachAttribute]`，支持特性构造函数参数。

## 嵌套对象与集合

### 嵌套对象

```csharp
order.Address = new Address { City = "上海" };
order.MarkClean(recursive: true);

order.Address.Detail = "南京路 123 号";
order.HasChanges();           // true
order.GetChangedProperties(); // ["Address"]

order.MarkClean();                // 只清理 order，子对象仍可处于脏状态
order.MarkClean(recursive: true); // 同时递归清理子对象和集合中的可追踪元素
```

子对象变更会把父对象对应的引用字段标记为已变更。`ITrackable` 接口、`object` 等引用类型字段或集合元素，会在运行时检查其中的可追踪对象。

同一子对象可以被多个字段或集合位置引用，订阅按对象引用身份管理，不受子对象重写 `Equals()` 的影响。替换引用、移除元素时会相应解除订阅。

### 集合操作

支持列表、字典和集合，以及声明为 `IList<T>`、`IDictionary<TKey, TValue>`、`ISet<T>` 的字段。生成的集合属性暴露对应接口，通过包装器记录操作：

| 包装器                              | 主要追踪操作                                 |
|-------------------------------------|----------------------------------------------|
| `TrackableList<T>`                  | 添加、插入、删除、清空、索引赋值             |
| `TrackableDictionary<TKey, TValue>` | 添加、删除、清空、索引赋值                   |
| `TrackableSet<T>`                   | 添加、删除、清空、并集、交集、差集、对称差集 |

```csharp
var address = new Address();
order.Addresses.Add(address); // 标记 Addresses 已变更
order.MarkClean(recursive: true);

address.City = "北京";        // 元素变更向上传播
order.GetChangedProperties(); // ["Addresses"]

order.Addresses.Remove(address); // 移除该位置的订阅
```

查询操作不会标记变更。集合中的已有可追踪元素会在首次使用所属对象的追踪 API 或订阅事件时建立订阅，无须先读取集合属性。

整体赋值集合属性时，会复制内容，再释放旧包装器的订阅。保留下来的旧包装器仍可作为集合使用，但不再通知原所属对象。把当前包装器赋回同一属性不会产生变更。

### 比较器与集合赋值

复制赋值会保留 `Dictionary`、`HashSet`、`SortedDictionary`、`SortedList`、`SortedSet` 及其追踪包装器的比较器。需要同时接受哈希集合和有序集合时，优先把字段声明为
`IDictionary` 或 `ISet`。

具体集合类型之间不兼容的复制，或无法获取比较器的自定义集合复制，会在修改属性前抛出 `NotSupportedException`
。此时请显式构造标准集合，并提供所需比较器。自定义集合仍可在构造函数中初始化为后备字段，再通过包装器使用。

## 变更事件

推荐使用返回 `IDisposable` 的订阅扩展，方便管理生命周期：

```csharp
using var subscription = order.SubscribeToChanges(() =>
{
    Console.WriteLine("订单发生变化");
});
// subscription.Dispose() 解除本次订阅
```

也可以直接订阅和解除事件：

```csharp
Action handler = () => Console.WriteLine("订单发生变化");
order.OnChanged += handler;
order.OnChanged -= handler;
```

需要具体字段信息时，使用 `OnChangedDetailed`：

```csharp
order.OnChangedDetailed += info =>
{
    Console.WriteLine($"来源: {info.Source.GetType().Name}");
    Console.WriteLine($"槽位: {info.Slot}, 本次变更位: {info.DirtyFlags}");
};
```

`DirtyFlags` 表示本次通知的位标记，不是对象累计的全部脏标记。`Source` 是发出当前事件的对象；子对象变更传播到父对象时，父对象事件的
`Source` 就是父对象，标记的是父对象对应的引用字段。

事件在调用线程上同步触发，不进行批量合并。一个字段已经处于脏状态后，再次发生变化仍可触发事件。处理器异常会向调用方传播，已完成的赋值或集合操作不会回滚。

## 暂停追踪

初始化、加载或回放数据时，可以暂时抑制当前对象的脏标记和变更事件：

```csharp
var received = new Order();
using (received.SuspendTracking())
{
    received.CustomerName = "李四";
    received.Amount = 100;
}

received.HasChanges(); // false
```

暂停作用域可以嵌套，最外层作用域退出后恢复追踪。暂停期间的变更不会在退出时补发，也不会清除进入作用域前已有的脏标记。

暂停仅作用于当前对象，不会递归暂停子对象。父对象暂停时修改子对象，子对象仍可能变脏；如需清理整个对象图，请显式调用
`MarkClean(recursive: true)`。

## Delta 导出与应用

`GetDelta()` 导出当前已变更属性的名称和值，`ApplyDelta()` 通过生成的属性写回数据：

```csharp
order.MarkClean(recursive: true);
order.CustomerName = "王五";
order.Amount = 99.5m;

IReadOnlyDictionary<string, object> delta = order.GetDelta();
// { "CustomerName": "王五", "Amount": 99.5m }

var target = new Order();
using (target.SuspendTracking())
{
    target.ApplyDelta(delta);
}

order.MarkClean(); // 由调用方决定何时确认并清理变更
```

使用时需要注意：

- 导出不会自动清理脏标记。干净对象返回共享的只读空字典；非空导出会分配字典，值类型可能装箱。
- Delta 是浅层数据，嵌套对象和集合按引用返回，不是独立快照，也不是集合操作日志。需要深层差异、旧值、撤销或跨进程同步协议时，应在上层实现。
- `ApplyDelta()` 忽略未知属性名。已知属性通过 `ChangeTracker.DeltaCast<T>` 转换，支持直接类型匹配、枚举、可空值类型以及基于
  `InvariantCulture` 的 `IConvertible` 转换。
- 类型或数值转换失败会抛出异常；应用不具备事务性，前面已应用的属性不会回滚。未暂停时，应用过程也会正常标记变更和触发事件。
- Delta 的值是有类型的 CLR 对象。默认 `System.Text.Json` 将 `Dictionary<string, object>` 反序列化为含 `JsonElement`
  的字典，不能直接当成已完成类型转换的数据传给 `ApplyDelta()`；需要先按目标类型转换或自定义序列化适配。

## API 速查

所有生成的可追踪类都实现以下接口：

```csharp
public interface ITrackable
{
    bool HasChanges();
    IReadOnlyList<string> GetChangedProperties();
    void MarkClean(bool recursive = false);
    event Action OnChanged;
    event Action<ChangeInfo> OnChangedDetailed;
    IDisposable SuspendTracking();
    IReadOnlyDictionary<string, object> GetDelta();
    void ApplyDelta(IReadOnlyDictionary<string, object> delta);
}
```

生成器还会生成 `FieldIndex` 字段索引常量和 `MarkChanged(string propertyName)`。追踪字段不超过 64 个时，另外生成
`[Flags] enum DirtyFlag : long`、`GetDirtyFlags()` 和 `MarkChanged(DirtyFlag)`：

```csharp
order.MarkChanged(Order.DirtyFlag.CustomerName | Order.DirtyFlag.Amount);
order.MarkChanged("Address");

var flags = order.GetDirtyFlags();
bool amountChanged = (flags & Order.DirtyFlag.Amount) != 0;
```

手动标记只记录变更并通知订阅者，不修改属性值。

| 追踪字段数量 | 脏标记存储                   | 类型化访问方式                                        |
|--------------|------------------------------|-------------------------------------------------------|
| ≤ 64         | 单个 `long` 槽位             | `DirtyFlag`、`GetDirtyFlags()`，以及 `FieldIndex`     |
| > 64         | 每 64 个字段一个 `long` 槽位 | `FieldIndex`；不生成 `DirtyFlag` 和 `GetDirtyFlags()` |

字段超过 64 个时，其他追踪 API 仍可使用。`ChangeInfo.Slot` 指出本次通知对应哪个槽位，`DirtyFlags` 表示该槽位中的变更位。

## 编译诊断与功能边界

| 诊断码     | 级别 | 含义                                                 |
|------------|------|------------------------------------------------------|
| `TRACK001` | 错误 | 使用 `[Trackable]` 的类必须是 `partial`              |
| `TRACK002` | 错误 | `[TrackableField]` 必须用于 `partial` 类中的私有字段 |
| `TRACK003` | 警告 | `[TrackIgnore]` 只对 `[Trackable]` 类的字段有效      |
| `TRACK004` | 错误 | `[Trackable]` 要求非静态类                           |
| `TRACK005` | 错误 | 请求生成追踪代码的类型，不能继承已有追踪能力的基类   |

**继承：** 未添加追踪特性的派生类可以使用基类的追踪成员，但新增字段不会因此自动纳入追踪。不要在已有追踪基类的派生类上再次使用
`[Trackable]` 或 `[TrackableField]`；需要分别追踪时使用组合。

**对象图：** 支持共享子对象和重复引用，要求追踪关系无环。子对象事件传播或递归清理发生循环重入时，会抛出带有循环提示的
`InvalidOperationException`。异常不回滚先前修改，继续使用前应解除循环引用。

**追踪含义：** 记录的是自上次清理后被标记过的属性，不保存原始值。先把属性改成新值再改回原值，不会自动清除脏标记。

## 性能与线程模型

属性和追踪调用在编译时生成，常规属性变更不依赖运行时反射扫描。脏标记使用位运算，避免为每次变更维护字符串集合。

初始化完成后，标量属性修改、干净对象的 `GetDelta()` 和基础类型集合元素通知等常用路径可以不分配内存。初始化、订阅变化、集合扩容、非空
Delta 导出仍可能分配；集合批量运算也可能创建临时快照。

DeltaTrack 按单线程访问设计，不在内部加锁。同一个对象及其可达的追踪对象图，应由同一线程访问，或由调用方统一同步，包括查询操作。

- 游戏服务器：每个实体由固定逻辑线程或 Actor 管理。
- UI：在 UI 线程修改，后台任务通过调度器切回所属线程。
- Web：使用请求范围内的实例；共享实例需要外部同步。

事件同步执行且允许重入。在处理器中再次修改对象可能继续触发事件，应由业务层控制重入行为。

## 许可证

[MIT License](LICENSE) — XBlueC
