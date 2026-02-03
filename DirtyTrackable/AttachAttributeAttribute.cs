namespace DirtyTrackable;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class AttachAttributeAttribute : Attribute
{
    public AttachAttributeAttribute(Type attributeType, params object[] constructorArguments)
    {
        AttributeType = attributeType ?? throw new ArgumentNullException(nameof(attributeType));
        ConstructorArguments = constructorArguments ?? Array.Empty<object>();
    }

    public Type AttributeType { get; }
    public object[] ConstructorArguments { get; }
}