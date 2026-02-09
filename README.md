# DeltaTrack

DeltaTrack 是一个轻量级的 C# 库，用于实现对象的脏数据跟踪功能。它通过源代码生成器自动为标记的类生成脏数据跟踪代码，使开发者能够轻松检测对象的属性变化，而无需手动实现繁琐的变更检测逻辑。

## 功能特性

- ✅ 自动生成脏数据跟踪代码
- ✅ 支持嵌套对象的脏数据跟踪
- ✅ 支持集合类型的脏数据跟踪（List、Dictionary、HashSet）
- ✅ 提供简洁的 API 来检查对象状态
- ✅ 支持脏数据状态变化事件通知
- ✅ 轻量级设计，无外部依赖

## 快速开始

### 基本用法

#### 1. 标记需要跟踪的类和字段

```csharp
using DirtyTrackable;

[Trackable]
public partial class Order
{
    [TrackableField] private string _customerName = "";
    [TrackableField] private decimal _totalAmount;
    [TrackableField] private List<string> _items = new();
    [TrackableField] private Address? _shippingAddress;
}

[Trackable]
public partial class Address
{
    [TrackableField] private string _city = "";
    [TrackableField] private string _street = "";
}
```

#### 2. 使用生成的属性和方法

```csharp
// 创建对象
var order = new Order();

// 修改属性
order.CustomerName = "John Doe";
order.TotalAmount = 100.50m;
order.Items.Add("Item 1");

// 检查脏数据状态
Console.WriteLine(order.IsDirty()); // True
Console.WriteLine(string.Join(", ", order.GetDirtyFields()));

// 标记为干净
order.MarkClean();
Console.WriteLine(order.IsDirty()); // False

// 监听脏数据状态变化
order.DirtyStateChanged += () => Console.WriteLine("Order state changed!");
order.CustomerName = "Jane Smith";
// 输出: Order state changed!
```

## 贡献

欢迎提交 Issue 和 Pull Request 来改进 DeltaTrack！

## 许可证

DeltaTrack 使用 MIT 许可证。
