# DeltaTrack

精准的对象变更检测库 - 自动追踪属性变化，零侵入式脏数据监控。

[![NuGet](https://img.shields.io/nuget/v/DeltaTrack.svg)](https://www.nuget.org/packages/DeltaTrack/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

## 核心作用

DeltaTrack 解决了对象状态变更检测的痛点：
- **自动监控**：只需添加 `[Trackable]` 和 `[TrackableField]` 特性
- **智能感知**：自动捕获属性赋值、集合增删改等所有变更操作
- **层级追踪**：支持嵌套对象和复杂集合的深度变更检测
- **实时反馈**：提供变更字段列表和状态变化事件

## 安装

```bash
dotnet add package DeltaTrack
```

## 快速开始

### 定义可追踪对象

**方式一：使用 `[Trackable]` 特性**

标记类后，所有私有字段自动被追踪，无需额外标注（类必须是 `partial`）：

```csharp
[Trackable]
public partial class Order
{
    private string _customerName = "";    // 自动追踪
    private decimal _amount;              // 自动追踪
    private Address? _address;            // 自动追踪（嵌套对象）
}

[Trackable]
public partial class Address
{
    private string _city = "";            // 自动追踪
    private string _detail = "";          // 自动追踪
}
```

**方式二：单独使用 `[TrackableField]`**

不需要 `[Trackable]`，只需给私有字段加 `[TrackableField]`（类必须是 `partial`）：

```csharp
public partial class Order
{
    [TrackableField] private string _name;      // 追踪
    private int _count;                         // 不追踪
}
```

Analyzer 会自动生成 `ITrackable` 接口实现，无需手动编写任何代码。

### 检查变更状态

```csharp
var order = new Order();
order.CustomerName = "张三";

// 检查是否有变更
order.HasChanges();                              // True

// 获取变更的属性列表
order.GetChangedProperties();                    // ["CustomerName"]

// 清除变更记录
order.MarkClean();
```

### 嵌套对象追踪

嵌套的可追踪对象会自动被追踪，变更会向上传播：

```csharp
order.Address = new Address { City = "上海" };
order.Address.Detail = "南京路123号";

order.HasChanges();                              // True（Address 的变更传播到 Order）

// 递归清理所有嵌套对象
order.MarkClean(recursive: true);
```

### 订阅变更事件

使用扩展方法订阅（推荐）：

```csharp
using var subscription = order.SubscribeToChanges(() =>
{
    Console.WriteLine("对象已变更！");
});
// subscription.Dispose() 时自动取消订阅
```

或直接访问 ChangeTracker：

```csharp
var tracker = order.GetChangeTracker();
tracker.OnChanged += () => Console.WriteLine("变更！");
tracker.OnClean += (recursive) => Console.WriteLine($"已清理 (递归: {recursive})");
```

## 特性详解

### `[Trackable]`

标记类为可追踪对象，Analyzer 自动生成 `ITrackable` 实现。**所有私有字段自动被追踪**：

```csharp
[Trackable]
public partial class MyClass
{
    private string _name;           // 自动追踪
    private int _count;             // 自动追踪
    private List<int> _items;       // 自动追踪
}
```

生成的代码包含：
- `GetChangeTracker()` 方法返回 `IChangeTracker`
- 每个私有字段的属性 getter/setter
- setter 中自动调用 `MarkChanged()`

### `[TrackableField]`

可独立使用，无需 `[Trackable]`。标记私有字段为可追踪（类必须是 `partial`）：

```csharp
public partial class MyClass
{
    [TrackableField] private string _name;     // 追踪
    private int _internalState;                // 不追踪
}
```

也可与 `[Trackable]` 配合，显式指定追踪（语义上更明确）：

```csharp
[Trackable]
public partial class MyClass
{
    [TrackableField] private string _name;     // 显式标记（已被自动追踪）
}
```

### `[TrackIgnore]`

在 `[Trackable]` 类中忽略指定私有字段：

```csharp
[Trackable]
public partial class MyClass
{
    private string _name;                      // 自动追踪

    [TrackIgnore]
    private string _cachedValue;               // 忽略，不追踪
}
```

### `[AttachAttribute]`

为生成的属性附加额外特性，支持构造函数参数：

```csharp
using System.Text.Json.Serialization;

[Trackable]
public partial class MyClass
{
    [AttachAttribute(typeof(JsonPropertyNameAttribute), "customer_name")]
    private string _customerName;              // 生成带特性的属性

    [AttachAttribute(typeof(RequiredAttribute))]
    private string _email;
}
```

生成的属性：

```csharp
[JsonPropertyName("customer_name")]
public string CustomerName { get; set; }

[Required]
public string Email { get; set; }
```

支持多个 `[AttachAttribute]`：

```csharp
[AttachAttribute(typeof(JsonPropertyNameAttribute), "name")]
[AttachAttribute(typeof(MaxLengthAttribute), 100)]
private string _name;
```

## 可追踪集合

DeltaTrack 提供三种可追踪集合，自动监控元素的增删改操作。

### TrackableList\<T\>

基于 `Collection<T>` 实现，追踪所有列表操作：

```csharp
var list = new TrackableList<Product>(() => tracker.MarkChanged("Products"));

list.Add(item);            // 触发变更
list.Insert(0, item);      // 触发变更
list[0] = newItem;         // 触发变更（SetItem）
list.RemoveAt(0);          // 触发变更
list.Remove(item);         // 触发变更
list.Clear();              // 触发变更
```

初始化时加载已有元素：

```csharp
var initialItems = new List<Product> { p1, p2 };
var list = new TrackableList<Product>(onChange, initialItems);
```

如果元素是 `ITrackable`，会自动订阅其变更事件。

### TrackableDictionary\<TKey, TValue\>

实现 `IDictionary<TKey, TValue>`，追踪所有字典操作：

```csharp
var dict = new TrackableDictionary<string, Product>(() => onChange());

dict["key"] = value;       // 触发变更（Add 或 Set）
dict.Add(key, value);      // 触发变更
dict.Remove(key);          // 触发变更
dict.Clear();              // 触发变更

// 查询操作不触发变更
dict.ContainsKey(key);
dict.TryGetValue(key, out var value);
```

初始化时加载已有元素：

```csharp
var existing = new Dictionary<string, Product> { ["k1"] = p1 };
var dict = new TrackableDictionary<string, Product>(onChange, existing);
```

### TrackableSet\<T\>

实现 `ISet<T>`，追踪所有集合操作：

```csharp
var set = new TrackableSet<string>(() => onChange());

set.Add(item);             // 触发变更（仅当真正添加时）
set.Remove(item);          // 触发变更（仅当真正移除时）
set.Clear();               // 触发变更

// 批量操作
set.UnionWith(other);      // 触发变更（如果有新增）
set.IntersectWith(other);  // 触发变更（如果有移除）
set.ExceptWith(other);     // 触发变更（如果有移除）
set.SymmetricExceptWith(other); // 触发变更（如果有变化）

// 查询操作不触发变更
set.Contains(item);
set.SetEquals(other);
set.IsSubsetOf(other);
```

### 集合与嵌套对象

集合中的 `ITrackable` 元素会自动被追踪：

```csharp
var list = new TrackableList<Address>(() => tracker.MarkChanged("Addresses"));
var addr = new Address();
list.Add(addr);

addr.City = "北京";        // 触发集合的 onChange（变更向上传播）

list.Remove(addr);         // 自动取消对 addr 的订阅
```

## API 参考

### IChangeTracker 接口

```csharp
public interface IChangeTracker
{
    bool HasChanges();                              // 是否有变更
    IReadOnlyCollection<string> GetChangedProperties(); // 变更的属性列表
    void MarkChanged(string property);              // 手动标记变更
    void MarkClean(bool recursive = false);         // 清除变更记录

    event Action OnChanged;                         // 变更时触发
    event Action<bool> OnClean;                     // 清理时触发
}
```

### ITrackable 接口

```csharp
public interface ITrackable
{
    IChangeTracker GetChangeTracker();              // 获取变更追踪器
}
```

### ITrackable 扩展方法

```csharp
// 检查变更
bool HasChanges()

// 获取变更属性列表
IReadOnlyCollection<string> GetChangedProperties()

// 清除变更记录
void MarkClean(bool recursive = false)

// 手动标记变更
void MarkChanged(string property)

// 订阅变更事件，返回 IDisposable
IDisposable SubscribeToChanges(Action handler)
```

示例：

```csharp
// 使用扩展方法
order.HasChanges();
order.GetChangedProperties();
order.MarkChanged("CustomField");
order.MarkClean(recursive: true);

// 使用订阅（推荐，自动管理生命周期）
using var sub = order.SubscribeToChanges(() => Console.WriteLine("Changed!"));
```

### ChangeTracker 内部机制

`ChangeTracker` 内部实现了智能的嵌套对象管理：

- **引用计数**：同一嵌套对象在多处引用时，只订阅一次，计数管理避免重复订阅
- **自动传播**：嵌套对象的 `OnChanged` 事件会触发父对象的变更
- **递归清理**：`MarkClean(true)` 会递归清理所有已订阅的嵌套对象

```csharp
// 内部 API（通常不需要直接调用）
tracker.HandleItemAdded(item, onChange);           // 处理元素添加
tracker.HandleItemRemoved(item, onChange);         // 处理元素移除
tracker.InitializeExistingItems(items, onChange);  // 初始化已有元素
tracker.Subscribe(item, onChange);                 // 订阅对象变更
tracker.Unsubscribe(item, onChange);               // 取消订阅
```

## 应用场景

| 场景 | 用法 |
|------|------|
| 数据同步 | 只同步 `GetChangedProperties()` 返回的字段 |
| 表单验证 | 实时监控用户输入变化，触发验证 |
| 缓存失效 | 对象变更时自动刷新缓存 |
| 审计日志 | 记录 `GetChangedProperties()` 中的变更字段 |
| 数据库更新 | 只更新有变更的字段，减少 IO |
| UI 绑定 | `SubscribeToChanges()` 通知界面刷新 |
| 分布式系统 | 精确传播变更到其他节点 |

## 技术特点

- **编译时生成** - 基于 Roslyn Source Generator，无运行时开销
- **零侵入** - 只需添加特性，不修改业务代码
- **零反射** - 生成的代码直接调用，性能优异
- **智能引用计数** - 同一对象多处引用时正确管理订阅，防止内存泄漏
- **嵌套追踪** - 自动追踪嵌套对象和集合中的可追踪元素
- **类型安全** - 强类型 API，编译期检查

## 许可证

MIT License - XBlueC