# DeltaTrack

Precise object change detection library - Automatically tracks property changes with zero intrusion.

[![NuGet](https://img.shields.io/nuget/v/DeltaTrack.svg)](https://www.nuget.org/packages/DeltaTrack/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0-blue.svg)](https://dotnet.microsoft.com/)
[![Build Status](https://github.com/XBlueC/DeltaTrack/actions/workflows/ci.yml/badge.svg)](https://github.com/XBlueC/DeltaTrack/actions)

## Overview

DeltaTrack solves the pain points of object state change detection:
- **Automatic Tracking**: Just add `[Trackable]` or `[TrackableField]` attributes
- **Smart Detection**: Automatically captures all changes including property assignments, collection add/remove/modify
- **Hierarchical Tracking**: Supports deep change detection for nested objects and complex collections
- **Real-time Feedback**: Provides changed field list and change events

## Installation

```bash
dotnet add package DeltaTrack
```

## Quick Start

### Define Trackable Objects

**Method 1: Using `[Trackable]` Attribute**

After marking the class, all private fields are automatically tracked (class must be `partial`):

```csharp
[Trackable]
public partial class Order
{
    private string _customerName = "";    // Auto-tracked
    private decimal _amount;              // Auto-tracked
    private Address? _address;            // Auto-tracked (nested object)
}

[Trackable]
public partial class Address
{
    private string _city = "";            // Auto-tracked
    private string _detail = "";          // Auto-tracked
}
```

**Method 2: Using `[TrackableField]` Attribute Only**

No need for `[Trackable]`, just add `[TrackableField]` to private fields (class must be `partial`):

```csharp
public partial class Order
{
    [TrackableField] private string _name;      // Tracked
    private int _count;                         // Not tracked
}
```

The Analyzer automatically generates the `ITrackable` implementation - no manual code needed.

### Check Change Status

```csharp
var order = new Order();
order.CustomerName = "John";

// Check if there are changes
order.HasChanges();                              // True

// Get list of changed properties
order.GetChangedProperties();                    // ["CustomerName"]

// Clear change records
order.MarkClean();
```

### Nested Object Tracking

Nested trackable objects are automatically tracked, and changes propagate upward:

```csharp
order.Address = new Address { City = "Shanghai" };
order.Address.Detail = "Nanjing Road 123";

order.HasChanges();                              // True (Address changes propagate to Order)

// Recursively clean all nested objects
order.MarkClean(recursive: true);
```

### Subscribe to Change Events

Using extension method (recommended):

```csharp
using var subscription = order.SubscribeToChanges(() =>
{
    Console.WriteLine("Object changed!");
});
// subscription.Dispose() automatically unsubscribes
```

Or access ChangeTracker directly:

```csharp
var tracker = order.GetChangeTracker();
tracker.OnChanged += () => Console.WriteLine("Changed!");
tracker.OnClean += (recursive) => Console.WriteLine($"Cleaned (recursive: {recursive})");
```

## Attributes

### `[Trackable]`

Marks a class as trackable, Analyzer automatically generates `ITrackable` implementation. **All private fields are auto-tracked**:

```csharp
[Trackable]
public partial class MyClass
{
    private string _name;           // Auto-tracked
    private int _count;             // Auto-tracked
    private List<int> _items;       // Auto-tracked
}
```

Generated code includes:
- `GetChangeTracker()` method returning `IChangeTracker`
- Property getter/setter for each private field
- Automatic `MarkChanged()` call in setter

### `[TrackableField]`

Can be used independently without `[Trackable]`. Marks private field as trackable (class must be `partial`):

```csharp
public partial class MyClass
{
    [TrackableField] private string _name;     // Tracked
    private int _internalState;                // Not tracked
}
```

Can also be used with `[Trackable]` for explicit tracking:

```csharp
[Trackable]
public partial class MyClass
{
    [TrackableField] private string _name;     // Explicit (already auto-tracked)
}
```

### `[TrackIgnore]`

Ignore specific private fields in `[Trackable]` class:

```csharp
[Trackable]
public partial class MyClass
{
    private string _name;                      // Auto-tracked

    [TrackIgnore]
    private string _cachedValue;               // Ignored, not tracked
}
```

### `[AttachAttribute]`

Add extra attributes to generated properties, supports constructor parameters:

```csharp
using System.Text.Json.Serialization;

[Trackable]
public partial class MyClass
{
    [AttachAttribute(typeof(JsonPropertyNameAttribute), "customer_name")]
    private string _customerName;              // Generate property with attribute

    [AttachAttribute(typeof(RequiredAttribute))]
    private string _email;
}
```

Generated properties:

```csharp
[JsonPropertyName("customer_name")]
public string CustomerName { get; set; }

[Required]
public string Email { get; set; }
```

Multiple `[AttachAttribute]` supported:

```csharp
[AttachAttribute(typeof(JsonPropertyNameAttribute), "name")]
[AttachAttribute(typeof(MaxLengthAttribute), 100)]
private string _name;
```

## Trackable Collections

DeltaTrack provides three trackable collections that automatically monitor element add/remove/modify operations.

### TrackableList\<T\>

Based on `Collection<T>`, tracks all list operations:

```csharp
var list = new TrackableList<Product>(() => tracker.MarkChanged("Products"));

list.Add(item);            // Triggers change
list.Insert(0, item);      // Triggers change
list[0] = newItem;         // Triggers change (SetItem)
list.RemoveAt(0);          // Triggers change
list.Remove(item);         // Triggers change
list.Clear();              // Triggers change
```

Initialize with existing elements:

```csharp
var initialItems = new List<Product> { p1, p2 };
var list = new TrackableList<Product>(onChange, initialItems);
```

If elements are `ITrackable`, automatically subscribes to their change events.

### TrackableDictionary\<TKey, TValue\>

Implements `IDictionary<TKey, TValue>`, tracks all dictionary operations:

```csharp
var dict = new TrackableDictionary<string, Product>(() => onChange());

dict["key"] = value;       // Triggers change (Add or Set)
dict.Add(key, value);      // Triggers change
dict.Remove(key);          // Triggers change
dict.Clear();              // Triggers change

// Query operations don't trigger change
dict.ContainsKey(key);
dict.TryGetValue(key, out var value);
```

Initialize with existing elements:

```csharp
var existing = new Dictionary<string, Product> { ["k1"] = p1 };
var dict = new TrackableDictionary<string, Product>(onChange, existing);
```

### TrackableSet\<T\>

Implements `ISet<T>`, tracks all set operations:

```csharp
var set = new TrackableSet<string>(() => onChange());

set.Add(item);             // Triggers change (only when actually added)
set.Remove(item);          // Triggers change (only when actually removed)
set.Clear();               // Triggers change

// Bulk operations
set.UnionWith(other);      // Triggers change (if new items added)
set.IntersectWith(other);  // Triggers change (if items removed)
set.ExceptWith(other);     // Triggers change (if items removed)
set.SymmetricExceptWith(other); // Triggers change (if any changes)

// Query operations don't trigger change
set.Contains(item);
set.SetEquals(other);
set.IsSubsetOf(other);
```

### Collections and Nested Objects

`ITrackable` elements in collections are automatically tracked:

```csharp
var list = new TrackableList<Address>(() => tracker.MarkChanged("Addresses"));
var addr = new Address();
list.Add(addr);

addr.City = "Beijing";        // Triggers collection's onChange (change propagates up)

list.Remove(addr);         // Automatically unsubscribes from addr
```

## API Reference

### IChangeTracker Interface

```csharp
public interface IChangeTracker
{
    bool HasChanges();                              // Whether there are changes
    IReadOnlyCollection<string> GetChangedProperties(); // List of changed properties
    void MarkChanged(string property);              // Manually mark as changed
    void MarkClean(bool recursive = false);         // Clear change records

    event Action OnChanged;                         // Triggered when changed
    event Action<bool> OnClean;                     // Triggered when cleaned
}
```

### ITrackable Interface

```csharp
public interface ITrackable
{
    IChangeTracker GetChangeTracker();              // Get change tracker
}
```

### ITrackable Extension Methods

```csharp
// Check changes
bool HasChanges()

// Get changed properties list
IReadOnlyCollection<string> GetChangedProperties()

// Clear change records
void MarkClean(bool recursive = false)

// Manually mark as changed
void MarkChanged(string property)

// Subscribe to change events, returns IDisposable
IDisposable SubscribeToChanges(Action handler)
```

Example:

```csharp
// Using extension methods
order.HasChanges();
order.GetChangedProperties();
order.MarkChanged("CustomField");
order.MarkClean(recursive: true);

// Using subscription (recommended, auto manages lifecycle)
using var sub = order.SubscribeToChanges(() => Console.WriteLine("Changed!"));
```

### ChangeTracker Internal Mechanism

`ChangeTracker` implements intelligent nested object management:

- **Reference Counting**: When same nested object is referenced in multiple places, subscribes only once, counting prevents duplicate subscriptions
- **Automatic Propagation**: Nested object's `OnChanged` event triggers parent object's change
- **Recursive Cleanup**: `MarkClean(true)` recursively cleans all subscribed nested objects

```csharp
// Internal API (usually no need to call directly)
tracker.HandleItemAdded(item, onChange);           // Handle item addition
tracker.HandleItemRemoved(item, onChange);         // Handle item removal
tracker.InitializeExistingItems(items, onChange);  // Initialize existing items
tracker.Subscribe(item, onChange);                 // Subscribe to object changes
tracker.Unsubscribe(item, onChange);               // Unsubscribe
```

## Use Cases

| Scenario | Usage |
|----------|-------|
| Data Sync | Only sync fields returned by `GetChangedProperties()` |
| Form Validation | Real-time monitoring of user input changes, trigger validation |
| Cache Invalidation | Auto refresh cache when objects change |
| Audit Logging | Record changed fields from `GetChangedProperties()` |
| Database Updates | Only update fields with changes, reduce IO |
| UI Binding | `SubscribeToChanges()` to notify UI refresh |
| Distributed Systems | Precisely propagate changes to other nodes |

## Technical Features

- **Compile-time Generation** - Based on Roslyn Source Generator, no runtime overhead
- **Zero Intrusion** - Only add attributes, no business code modification
- **Zero Reflection** - Generated code calls directly, excellent performance
- **Smart Reference Counting** - Correctly manages subscriptions when same object referenced multiple places, prevents memory leaks
- **Nested Tracking** - Auto-tracks nested objects and trackable elements in collections
- **Type Safe** - Strongly typed API, compile-time checking

## License

MIT License - XBlueC
