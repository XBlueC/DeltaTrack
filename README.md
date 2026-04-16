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
- **Zero GC Pressure**: Uses bitflag (`long`) instead of `HashSet<string>` for dirty marking, ideal for high-frequency scenarios like game servers

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

// Type-safe dirty flag check
order.GetDirtyFlags();                           // Order.DirtyFlag.CustomerName

// Clear change records
order.MarkClean();
```

### Type-Safe Dirty Flag API

The generator creates a `[Flags] enum DirtyFlag : long` for each trackable class, enabling type-safe bit operations:

```csharp
// Mark specific fields as changed using type-safe flags
order.MarkChanged(Order.DirtyFlag.CustomerName | Order.DirtyFlag.Amount);

// Check specific dirty flags
var flags = order.GetDirtyFlags();
if (flags.HasFlag(Order.DirtyFlag.CustomerName))
{
    // Handle customer name change
}

// String-based marking is also supported
order.MarkChanged("CustomerName");
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

Or subscribe to the event directly:

```csharp
order.OnChanged += () => Console.WriteLine("Changed!");
order.OnChanged -= handler;  // Manual unsubscribe
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

- `ITrackable` interface implementation (`HasChanges()`, `GetChangedProperties()`, `MarkClean()`, `event OnChanged`)
- `[Flags] enum DirtyFlag : long` with one flag per tracked field
- Property getter/setter for each private field
- Automatic `OnXxxChanged()` call in setter using bitflag operations
- `GetDirtyFlags()`, `MarkChanged(DirtyFlag)`, `MarkChanged(string)` helper methods

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
list.Add(item);            // Triggers change
list.Insert(0, item);      // Triggers change
list[0] = newItem;         // Triggers change (SetItem)
list.RemoveAt(0);          // Triggers change
list.Remove(item);         // Triggers change
list.Clear();              // Triggers change
```

If elements are `ITrackable`, automatically subscribes to their change events.

### TrackableDictionary\<TKey, TValue\>

Implements `IDictionary<TKey, TValue>`, tracks all dictionary operations:

```csharp
dict["key"] = value;       // Triggers change (Add or Set)
dict.Add(key, value);      // Triggers change
dict.Remove(key);          // Triggers change
dict.Clear();              // Triggers change

// Query operations don't trigger change
dict.ContainsKey(key);
dict.TryGetValue(key, out var value);
```

### TrackableSet\<T\>

Implements `ISet<T>`, tracks all set operations:

```csharp
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
var addr = new Address();
order.Addresses.Add(addr);

addr.City = "Beijing";        // Triggers collection's onChange (change propagates up)

order.Addresses.Remove(addr); // Automatically unsubscribes from addr
```

## API Reference

### ITrackable Interface

The sole public contract for consumers. All generated trackable classes implement this interface:

```csharp
public interface ITrackable
{
    bool HasChanges();                              // Whether there are changes
    IReadOnlyList<string> GetChangedProperties();   // List of changed properties
    void MarkClean(bool recursive = false);         // Clear change records
    event Action OnChanged;                         // Triggered when changed
}
```

### Generated Per-Class API

In addition to `ITrackable`, the generator produces these members on each trackable class:

```csharp
// Type-safe dirty flag enum
[Flags]
public enum DirtyFlag : long
{
    Name = 1L << 0,
    Age  = 1L << 1,
    // ... one flag per tracked field
}

// Get current dirty flags
DirtyFlag GetDirtyFlags();

// Mark fields as changed using type-safe flags
void MarkChanged(DirtyFlag flags);

// Mark field as changed by name
void MarkChanged(string propertyName);
```

### Extension Methods

```csharp
// Subscribe to change events, returns IDisposable
IDisposable SubscribeToChanges(Action handler)
```

Example:

```csharp
// Direct interface usage
order.HasChanges();
order.GetChangedProperties();
order.MarkClean(recursive: true);
order.OnChanged += () => Console.WriteLine("Changed!");

// Using subscription (recommended, auto manages lifecycle)
using var sub = order.SubscribeToChanges(() => Console.WriteLine("Changed!"));
```

## Analyzer Diagnostics

DeltaTrack includes compile-time analyzers to catch issues early:

| Code    | Severity | Description                                        |
|---------|----------|----------------------------------------------------|
| TRACK001 | Error   | Trackable class must be declared as `partial`       |
| TRACK002 | Error   | `[TrackableField]` must be on a private field       |
| TRACK003 | Error   | Trackable class exceeds 64 tracked fields (bitflag limit) |

## Use Cases

| Scenario            | Usage                                                          |
|---------------------|----------------------------------------------------------------|
| Data Sync           | Only sync fields returned by `GetChangedProperties()`          |
| Form Validation     | Real-time monitoring of user input changes, trigger validation |
| Cache Invalidation  | Auto refresh cache when objects change                         |
| Audit Logging       | Record changed fields from `GetChangedProperties()`            |
| Database Updates    | Only update fields with changes, reduce IO                     |
| UI Binding          | `SubscribeToChanges()` to notify UI refresh                    |
| Distributed Systems | Precisely propagate changes to other nodes                     |

## Technical Features

- **Compile-time Generation** - Based on Roslyn Source Generator, no runtime overhead
- **Zero Intrusion** - Only add attributes, no business code modification
- **Zero Reflection** - Generated code calls directly, excellent performance
- **Zero GC Dirty Tracking** - Uses `long` bitflag instead of `HashSet<string>`, no allocations on change
- **Type-Safe Flags** - Generated `[Flags] enum DirtyFlag : long` per class for compile-time checked operations
- **Smart Reference Counting** - Correctly manages subscriptions when same object referenced multiple places, prevents memory leaks
- **Nested Tracking** - Auto-tracks nested objects and trackable elements in collections
- **Compile-time Diagnostics** - Analyzer catches common mistakes (non-partial class, >64 fields, etc.)

## License

MIT License - XBlueC
