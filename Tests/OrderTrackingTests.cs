using System.Reflection;
using System.Text.Json;

namespace Tests;

public class OrderTrackingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true, // 因为数据存在私有字段 _items / _tags
        PropertyNamingPolicy = null, // 保持字段原名
        WriteIndented = false
    };

    [Fact]
    public void StringProperty_SetDifferentValue_MarksDirty()
    {
        var order = new Order();
        order.MarkClean();

        order.CustomerName = "Alice";

        Assert.True(order.IsDirty());
        Assert.Contains("CustomerName", order.GetDirtyFields());
    }

    [Fact]
    public void StringProperty_SetSameValue_DoesNotMarkDirty()
    {
        var order = new Order { CustomerName = "Bob" };
        order.MarkClean();

        order.CustomerName = "Bob"; // same

        Assert.False(order.IsDirty());
    }

    [Fact]
    public void DecimalProperty_ChangeMarksDirty()
    {
        var order = new Order();
        order.MarkClean();

        order.TotalAmount = 99.99m;

        Assert.True(order.IsDirty());
        Assert.Contains("TotalAmount", order.GetDirtyFields());
    }

    // —————————— List<T> 测试 ——————————

    [Fact]
    public void List_Add_MarksDirty()
    {
        var order = new Order();
        order.MarkClean();

        order.Items.Add("Laptop");

        Assert.True(order.IsDirty());
        Assert.Contains("Items", order.GetDirtyFields());
    }

    [Fact]
    public void List_Remove_MarksDirty()
    {
        var order = new Order();
        order.Items.Add("Temp");
        order.MarkClean();

        var removed = order.Items.Remove("Temp");

        Assert.True(removed);
        Assert.True(order.IsDirty());
        Assert.Contains("Items", order.GetDirtyFields());
    }

    [Fact]
    public void List_Clear_MarksDirty()
    {
        var order = new Order();
        order.Items.Add("A");
        order.MarkClean();

        order.Items.Clear();

        Assert.True(order.IsDirty());
        Assert.Contains("Items", order.GetDirtyFields());
    }

    [Fact]
    public void List_IndexSet_MarksDirty()
    {
        var order = new Order();
        order.Items.Add("Old");
        order.MarkClean();

        order.Items[0] = "New";

        Assert.True(order.IsDirty());
        Assert.Contains("Items", order.GetDirtyFields());
    }

    // —————————— Dictionary<TKey, TValue> 测试 ——————————

    [Fact]
    public void Dictionary_AddOrUpdate_MarksDirty()
    {
        var order = new Order();
        order.MarkClean();

        order.Metadata["source"] = 1;

        Assert.True(order.IsDirty());
        Assert.Contains("Metadata", order.GetDirtyFields());
    }

    [Fact]
    public void Dictionary_Remove_MarksDirty()
    {
        var order = new Order();
        order.Metadata["temp"] = 1;
        order.MarkClean();

        var removed = order.Metadata.Remove("temp");

        Assert.True(removed);
        Assert.True(order.IsDirty());
        Assert.Contains("Metadata", order.GetDirtyFields());
    }

    [Fact]
    public void Dictionary_Clear_MarksDirty()
    {
        var order = new Order();
        order.Metadata["key"] = 42;
        order.MarkClean();

        order.Metadata.Clear();

        Assert.True(order.IsDirty());
        Assert.Contains("Metadata", order.GetDirtyFields());
    }

    // —————————— HashSet<T> 测试 ——————————

    [Fact]
    public void HashSet_Add_MarksDirty()
    {
        var order = new Order();
        order.MarkClean();

        order.Tags.Add("urgent");

        Assert.True(order.IsDirty());
        Assert.Contains("Tags", order.GetDirtyFields());
    }

    [Fact]
    public void HashSet_Remove_MarksDirty()
    {
        var order = new Order();
        order.Tags.Add("temp");
        order.MarkClean();

        var removed = order.Tags.Remove("temp");

        Assert.True(removed);
        Assert.True(order.IsDirty());
        Assert.Contains("Tags", order.GetDirtyFields());
    }

    // —————————— 嵌套 Trackable 对象 ——————————

    [Fact]
    public void NestedObject_SetProperty_MarksParentDirty()
    {
        var order = new Order();
        order.ShippingAddress = new Address();
        order.MarkClean();

        order.ShippingAddress.City = "Shanghai";

        Assert.True(order.IsDirty());
        Assert.Contains("ShippingAddress", order.GetDirtyFields());
    }

    [Fact]
    public void NestedObject_Reassign_MarksDirty()
    {
        var order = new Order();
        order.MarkClean();

        order.ShippingAddress = new Address { Street = "Nanjing Rd" };

        Assert.True(order.IsDirty());
        Assert.Contains("ShippingAddress", order.GetDirtyFields());
    }

    [Fact]
    public void NestedObject_SetToNull_MarksDirty()
    {
        var order = new Order();
        order.ShippingAddress = new Address();
        order.MarkClean();

        order.ShippingAddress = null;

        Assert.True(order.IsDirty());
        Assert.Contains("ShippingAddress", order.GetDirtyFields());
    }

    // —————————— 状态管理 ——————————

    [Fact]
    public void MarkClean_ResetsAllDirtyFlags()
    {
        var order = new Order();
        order.CustomerName = "A";
        order.Items.Add("item");
        Assert.True(order.IsDirty());

        order.MarkClean();

        Assert.False(order.IsDirty());
        Assert.Empty(order.GetDirtyFields());
    }

    [Fact]
    public void MultipleFields_AllReported()
    {
        var order = new Order();
        order.MarkClean();

        order.CustomerName = "C";
        order.TotalAmount = 100;
        order.Items.Add("X");

        var dirtyFields = order.GetDirtyFields().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "CustomerName", "Items", "TotalAmount" }, dirtyFields);
    }

    // —————————— 事件通知 ——————————

    [Fact]
    public void DirtyStateChanged_EventFiredOnFirstChange()
    {
        var order = new Order();
        var eventFired = false;
        order.DirtyStateChanged += () => eventFired = true;

        order.CustomerName = "Trigger";

        Assert.True(eventFired);
    }

    [Fact]
    public void DirtyStateChanged_FiredOnEveryFieldChange()
    {
        var order = new Order();
        var count = 0;
        order.DirtyStateChanged += () => count++;

        order.CustomerName = "A"; // 1
        order.TotalAmount = 1m; // 2

        Assert.Equal(2, count); // 每次 MarkFieldDirty 都触发
    }

    // —————————— 边界 & 安全性 ——————————

    [Fact]
    public void AccessCollectionAfterDeserialization_DoesNotCrash()
    {
        // 模拟反序列化后 _items = null
        var order = new Order();
        typeof(Order)
            .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(order, null);

        // 首次访问应安全初始化
        var items = order.Items; // 不应抛异常
        Assert.NotNull(items);

        // 后续操作应正常标记 dirty
        items.Add("safe");
        Assert.True(order.IsDirty());
    }

    [Fact]
    public void SetCollectionToNull_ThenAccess_CreatesNewInstance()
    {
        var order = new Order();
        order.MarkClean();

        // 模拟设为 null（如从 API 接收）
        typeof(Order)
            .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(order, null);

        // 访问属性应返回新包装器
        var items = order.Items;
        Assert.Empty(items);

        // 修改应标记 dirty
        items.Add("restored");
        Assert.True(order.IsDirty());
    }

    // —————————————————— TrackableList 序列化测试 ——————————————————

    [Fact]
    public void TrackableList_AfterJsonDeserialization_ModifyTriggersDirty()
    {
        // 1. 创建并填充订单
        var order = new Order();
        order.Items.Add("Laptop");
        order.MarkClean();

        // 2. 序列化 + 反序列化（模拟从 API/DB 恢复）
        var json = JsonSerializer.Serialize(order, JsonOptions);
        var deserializedOrder = JsonSerializer.Deserialize<Order>(json, JsonOptions)!;

        // 3. 验证反序列化后数据存在
        Assert.Single(deserializedOrder.Items);
        Assert.Equal("Laptop", deserializedOrder.Items[0]);

        // 4. 修改反序列化后的集合 → 应标记 dirty
        deserializedOrder.MarkClean();
        deserializedOrder.Items.Add("Mouse");

        Assert.True(deserializedOrder.IsDirty());
        Assert.Contains("Items", deserializedOrder.GetDirtyFields());
    }

    [Fact]
    public void TrackableList_DeserializedWithNullField_AccessInitializesSafely()
    {
        // 模拟反序列化时 _items 字段为 null（某些 JSON 库会这样）
        var order = new Order();
        // 手动设为 null，模拟反序列化结果
        typeof(Order).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(order, null);

        // 首次访问不应崩溃，且返回空列表
        var items = order.Items; // 触发 lazy init

        Assert.NotNull(items);
        Assert.Empty(items);

        // 后续修改应正常工作
        order.MarkClean();
        items.Add("Phone");
        Assert.True(order.IsDirty());
    }

    // —————————————————— TrackableSet 序列化测试 ——————————————————

    [Fact]
    public void TrackableSet_AfterJsonDeserialization_ModifyTriggersDirty()
    {
        var order = new Order();
        order.Tags.Add("urgent");
        order.MarkClean();

        var json = JsonSerializer.Serialize(order, JsonOptions);
        var deserializedOrder = JsonSerializer.Deserialize<Order>(json, JsonOptions)!;

        Assert.Contains("urgent", deserializedOrder.Tags);

        deserializedOrder.MarkClean();
        deserializedOrder.Tags.Add("shipped");

        Assert.True(deserializedOrder.IsDirty());
        Assert.Contains("Tags", deserializedOrder.GetDirtyFields());
    }

    [Fact]
    public void TrackableSet_DeserializedWithNullField_AccessInitializesSafely()
    {
        var order = new Order();
        typeof(Order).GetField("_tags", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(order, null);

        var tags = order.Tags; // lazy init

        Assert.NotNull(tags);
        Assert.Empty(tags);

        order.MarkClean();
        tags.Add("test");
        Assert.True(order.IsDirty());
    }

    // —————————————————— 嵌套对象序列化联动测试 ——————————————————

    [Fact]
    public void TrackableList_WithNestedTrackableObject_AfterDeserialization_ChildChangeTriggersParentDirty()
    {
        var order = new Order();
        var person = new Person { Name = "Alice" };
        order.People.Add(person);
        order.MarkClean();

        // 序列化 + 反序列化
        var json = JsonSerializer.Serialize(order, JsonOptions);
        var deserializedOrder = JsonSerializer.Deserialize<Order>(json, JsonOptions)!;

        // 获取反序列化后的 person
        var deserializedPerson = deserializedOrder.People[0];

        // 修改子对象 → 应触发父级 dirty
        deserializedOrder.MarkClean();
        deserializedPerson.Name = "Bob";

        Assert.True(deserializedOrder.IsDirty());
        Assert.Contains("People", deserializedOrder.GetDirtyFields());
    }


    [Fact]
    public void TrackableDictionary_AfterJsonDeserialization_ModifyTriggersDirty()
    {
        var order = new Order();
        order.Metadata["status"] = 1;
        order.MarkClean();

        var json = JsonSerializer.Serialize(order, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Order>(json, JsonOptions)!;

        Assert.Equal(1, deserialized.Metadata["status"]);

        deserialized.MarkClean();
        deserialized.Metadata["priority"] = 2;

        Assert.True(deserialized.IsDirty());
        Assert.Contains("Metadata", deserialized.GetDirtyFields());
    }

    [Fact]
    public void TrackableDictionary_DeserializedWithNullField_AccessInitializesSafely()
    {
        var order = new Order();
        typeof(Order).GetField("_metadata", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(order, null);

        var dict = order.Metadata; // lazy init

        Assert.NotNull(dict);
        Assert.Empty(dict);

        order.MarkClean();
        dict["key"] = 1;
        Assert.True(order.IsDirty());
    }

    [Fact]
    public void TrackableDictionary_NestedTrackableValue_ChildChangeTriggersParentDirty()
    {
        var order = new Order();
        var person = new Person { Name = "Alice" };
        order.Associates["manager"] = person;
        order.MarkClean();

        var json = JsonSerializer.Serialize(order, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Order>(json, JsonOptions)!;

        var manager = deserialized.Associates["manager"];
        deserialized.MarkClean();

        manager.Name = "Bob";

        Assert.True(deserialized.IsDirty());
        Assert.Contains("Associates", deserialized.GetDirtyFields());
    }
}