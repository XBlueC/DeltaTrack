using DirtyTrackable;
using Xunit;

namespace Tests;

public class SourceGeneratorTests
{
    [Fact]
    public void GeneratedClass_ShouldImplementIDirtyTrackable()
    {
        // Arrange
        var person = new Person();

        // Act & Assert
        Assert.IsAssignableFrom<IDirtyTrackable>(person);
    }

    [Fact]
    public void GeneratedProperties_ShouldHaveCorrectGettersAndSetters()
    {
        // Arrange
        var person = new Person();
        var originalName = person.Name;
        var newName = "John Doe";

        // Act
        person.Name = newName;

        // Assert
        Assert.Equal(newName, person.Name);
        Assert.NotEqual(originalName, person.Name);
    }

    [Fact]
    public void SimplePropertyChanges_ShouldMarkFieldDirty()
    {
        // Arrange
        var person = new Person();
        var dirtyStateChanged = false;
        ((IDirtyTrackable)person).DirtyStateChanged += () => dirtyStateChanged = true;

        // Act
        person.Name = "John Doe";

        // Assert
        Assert.True(((IDirtyTrackable)person).IsDirty());
        Assert.Contains("Name", ((IDirtyTrackable)person).GetDirtyFields());
        Assert.True(dirtyStateChanged);
    }

    [Fact]
    public void SimplePropertyChanges_SameValue_ShouldNotMarkDirty()
    {
        // Arrange
        var person = new Person();
        person.Name = "John";
        ((IDirtyTrackable)person).MarkClean(); // Clean first

        // Act
        person.Name = "John"; // Same value

        // Assert
        Assert.False(((IDirtyTrackable)person).IsDirty());
    }

    [Fact]
    public void TrackableProperty_ChildChanges_ShouldNotifyParent()
    {
        // Arrange
        var company = new Company();
        var employee = new Employee();
        var dirtyStateChanged = false;
        ((IDirtyTrackable)company).DirtyStateChanged += () => dirtyStateChanged = true;

        // Act
        company.Employee = employee;
        employee.Name = "John Doe";

        // Assert
        Assert.True(dirtyStateChanged);
        Assert.True(((IDirtyTrackable)company).IsDirty());
    }

    [Fact]
    public void TrackableProperty_ChildClean_ShouldCleanParent()
    {
        // Arrange
        var company = new Company();
        var employee = new Employee();
        company.Employee = employee;
        employee.Name = "John Doe";

        // Act
        ((IDirtyTrackable)company).MarkClean(recursive: true);

        // Assert
        Assert.False(((IDirtyTrackable)employee).IsDirty());
        Assert.False(((IDirtyTrackable)company).IsDirty());
    }

    [Fact]
    public void TrackableListProperty_AddItem_ShouldTrackChanges()
    {
        // Arrange
        var department = new Department();
        var employee = new Employee();
        var dirtyStateChanged = false;
        ((IDirtyTrackable)department).DirtyStateChanged += () => dirtyStateChanged = true;

        // Act
        department.Employees.Add(employee);
        employee.Name = "John Doe";

        // Assert
        Assert.True(dirtyStateChanged);
        Assert.True(((IDirtyTrackable)department).IsDirty());
    }

    [Fact]
    public void TrackableListProperty_ChildClean_ShouldCleanAll()
    {
        // Arrange
        var department = new Department();
        var employee1 = new Employee();
        var employee2 = new Employee();
        
        department.Employees.Add(employee1);
        department.Employees.Add(employee2);
        
        employee1.Name = "John";
        employee2.Name = "Jane";

        // Act
        ((IDirtyTrackable)department).MarkClean(recursive: true);

        // Assert
        Assert.False(((IDirtyTrackable)employee1).IsDirty());
        Assert.False(((IDirtyTrackable)employee2).IsDirty());
        Assert.False(((IDirtyTrackable)department).IsDirty());
    }

    [Fact]
    public void TrackableDictionaryProperty_AddItem_ShouldTrackChanges()
    {
        // Arrange
        var organization = new Organization();
        var employee = new Employee();
        var dirtyStateChanged = false;
        ((IDirtyTrackable)organization).DirtyStateChanged += () => dirtyStateChanged = true;

        // Act
        organization.Employees["emp1"] = employee;
        employee.Name = "John Doe";

        // Assert
        Assert.True(dirtyStateChanged);
        Assert.True(((IDirtyTrackable)organization).IsDirty());
    }

    [Fact]
    public void TrackableDictionaryProperty_ChildClean_ShouldCleanAll()
    {
        // Arrange
        var organization = new Organization();
        var employee1 = new Employee();
        var employee2 = new Employee();
        
        organization.Employees["emp1"] = employee1;
        organization.Employees["emp2"] = employee2;
        
        employee1.Name = "John";
        employee2.Name = "Jane";

        // Act
        ((IDirtyTrackable)organization).MarkClean(recursive: true);

        // Assert
        Assert.False(((IDirtyTrackable)employee1).IsDirty());
        Assert.False(((IDirtyTrackable)employee2).IsDirty());
        Assert.False(((IDirtyTrackable)organization).IsDirty());
    }

    [Fact]
    public void TrackableSetProperty_AddItem_ShouldTrackChanges()
    {
        // Arrange
        var team = new Team();
        var employee = new Employee();
        var dirtyStateChanged = false;
        ((IDirtyTrackable)team).DirtyStateChanged += () => dirtyStateChanged = true;

        // Act
        team.Members.Add(employee);
        employee.Name = "John Doe";

        // Assert
        Assert.True(dirtyStateChanged);
        Assert.True(((IDirtyTrackable)team).IsDirty());
    }

    [Fact]
    public void MultipleProperties_DirtyFields_ShouldTrackSeparately()
    {
        // Arrange
        var person = new Person();

        // Act
        person.Name = "John";
        person.Age = 30;

        // Assert
        var dirtyFields = ((IDirtyTrackable)person).GetDirtyFields();
        Assert.Contains("Name", dirtyFields);
        Assert.Contains("Age", dirtyFields);
        Assert.Equal(2, dirtyFields.Count);
    }

    [Fact]
    public void MarkClean_NonRecursive_ShouldOnlyCleanSelf()
    {
        // Arrange
        var company = new Company();
        var employee = new Employee();
        company.Employee = employee;
        employee.Name = "John Doe";

        // Act
        ((IDirtyTrackable)company).MarkClean(recursive: false);

        // Assert
        Assert.True(((IDirtyTrackable)employee).IsDirty()); // Child still dirty
        Assert.False(((IDirtyTrackable)company).IsDirty()); // Parent clean
    }

    [Fact]
    public void PropertySetter_ShouldHandleNullValues()
    {
        // Arrange
        var company = new Company();
        var employee = new Employee();
        company.Employee = employee;

        // Act
        company.Employee = null;

        // Assert - Should not throw
        var exception = Record.Exception(() => {
            var temp = company.Employee;
        });
        Assert.Null(exception);
    }

    [Fact]
    public void GeneratedConstructor_ShouldInitializeTracker()
    {
        // Arrange & Act
        var person = new Person();

        // Assert
        Assert.NotNull(person);
        Assert.False(((IDirtyTrackable)person).IsDirty());
    }

    [Fact]
    public void AttachAttribute_ShouldAddCustomAttributesToGeneratedProperties()
    {
        // Arrange
        var decoratedClass = new DecoratedClass();

        // Act
        var propertyInfo = decoratedClass.GetType().GetProperty("DecoratedProperty");
        var attributes = propertyInfo?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false);

        // Assert
        Assert.NotNull(attributes);
        Assert.Single(attributes);
    }
}

// Test classes that will be processed by the source generator
[Trackable]
public partial class Person
{
    [TrackableField]
    private string _name = "Default Name";
    
    [TrackableField]
    private int _age;
    
    [TrackableField]
    private DateTime _birthDate;
}

[Trackable]
public partial class Employee
{
    [TrackableField]
    private string _name = "Employee Name";
    
    [TrackableField]
    private decimal _salary;
}

[Trackable]
public partial class Company
{
    [TrackableField]
    private Employee _employee;
    
    [TrackableField]
    private string _companyName;
}

[Trackable]
public partial class Department
{
    [TrackableField]
    private List<Employee> _employees = new();
    
    [TrackableField]
    private string _departmentName;
}

[Trackable]
public partial class Organization
{
    [TrackableField]
    private Dictionary<string, Employee> _employees = new();
    
    [TrackableField]
    private string _orgName;
}

[Trackable]
public partial class Team
{
    [TrackableField]
    private HashSet<Employee> _members = new();
    
    [TrackableField]
    private string _teamName;
}

[Trackable]
public partial class DecoratedClass
{
    [TrackableField]
    [AttachAttribute(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute))]
    private string _decoratedProperty = "";
}