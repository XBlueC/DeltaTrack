# DeltaTrack

**精准的对象变更检测库** - 自动追踪对象属性变化，零侵入式实现脏数据监控

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

## 🎯 核心作用

DeltaTrack 解决了对象状态变更检测的痛点：
- **自动监控**：只需添加 `[Trackable]` 和 `[TrackableField]` 特性
- **智能感知**：自动捕获属性赋值、集合增删改等所有变更操作
- **层级追踪**：支持嵌套对象和复杂集合的深度变更检测
- **实时反馈**：提供变更字段列表和状态变化事件

## 🔥 核心亮点

### 1. 极简使用
```csharp
[Trackable]
public partial class Order
{
    [TrackableField] private string _customerName = "";
    [TrackableField] private List<string> _items = new();
}

var order = new Order();
order.CustomerName = "张三";
order.Items.Add("商品A");

// 检查变更状态
Console.WriteLine(order.HasChanges()); // True
Console.WriteLine(string.Join(", ", order.GetChangedProperties())); // CustomerName, Items
```

### 2. 全场景覆盖
- ✅ 基础类型属性变更
- ✅ 集合元素增删改（List、Dictionary、Set）
- ✅ 嵌套对象属性变化
- ✅ 复杂对象图的递归追踪

### 3. 高性能设计
- 基于 Roslyn Source Generator 编译时生成代码
- 运行时零反射开销
- 内存友好的事件订阅机制
- 智能引用计数避免重复订阅

### 4. 完善的生态支持
- 与 JSON 序列化无缝集成
- 提供递归清理和批量操作 API
- 完整的单元测试覆盖
- 开发期诊断分析器支持

## 🚀 快速上手

### 1. 定义可追踪对象
```csharp
[Trackable]
public partial class Order
{
    [TrackableField] private string _customerName = "";
    [TrackableField] private decimal _amount;
    [TrackableField] private List<string> _products = new();
    [TrackableField] private Address? _address;
}

[Trackable]
public partial class Address
{
    [TrackableField] private string _city = "";
    [TrackableField] private string _detail = "";
}
```

### 2. 实时监控变更
```csharp
var order = new Order();

// 属性变更检测
order.CustomerName = "李四";
Console.WriteLine(order.HasChanges()); // True

// 集合操作检测
order.Products.Add("iPhone");
order.Products.Add("MacBook");
Console.WriteLine(order.GetChangedProperties().Contains("Products")); // True

// 嵌套对象检测
order.Address = new Address { City = "上海" };
order.Address.Detail = "南京路123号";
Console.WriteLine(order.GetChangedProperties().Contains("Address")); // True
```

### 3. 状态管理
```csharp
// 获取变更跟踪器
var changeTracker = order.GetChangeTracker();

// 批量清理变更状态
changeTracker.MarkClean();

// 递归清理（包括嵌套对象）
changeTracker.MarkClean(recursive: true);

// 监听变更事件
changeTracker.OnChanged += () => {
    Console.WriteLine($"检测到变更: {string.Join(", ", changeTracker.GetChangedProperties())}");
};

// 手动标记字段为脏
changeTracker.MarkChanged("CustomField");
```

## 📊 应用场景

- **数据同步**：检测实体变更，只同步修改过的字段
- **表单验证**：实时监控用户输入变化
- **缓存失效**：基于对象变更自动刷新缓存
- **审计日志**：记录业务对象的关键变更历史
- **性能优化**：避免不必要的数据库更新操作
- **UI绑定**：自动通知界面更新
- **分布式系统**：精确的数据变更传播

## 🛠️ 技术特点

- **编译时生成**：基于 Roslyn Source Generator，提升性能
- **零运行时依赖**：不引入额外的第三方库
- **类型安全**：强类型 API 设计，编译期检查
- **内存高效**：智能的事件订阅和取消订阅机制
- **引用计数**：防止循环引用导致的内存泄漏

## 🤝 贡献

我们欢迎各种形式的贡献！

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件