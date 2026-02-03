using DirtyTrackable;

namespace Tests;

[Trackable]
public partial class Order
{
    [TrackableField] private Dictionary<string, Person> _associates = new();
    [TrackableField] private string _customerName = "";
    [TrackableField] private List<string> _items = new();
    [TrackableField] private Dictionary<string, int> _metadata = new();
    [TrackableField] private List<Person> _people = new();
    [TrackableField] private Address? _shippingAddress; // 嵌套 Trackable
    [TrackableField] private HashSet<string> _tags = new();
    [TrackableField] private decimal _totalAmount;
}

[Trackable]
public partial class Address
{
    [TrackableField] private string _city = "";
    [TrackableField] private string _street = "";
}

[Trackable]
public partial class Person
{
    [TrackableField] private string _name = "";
}