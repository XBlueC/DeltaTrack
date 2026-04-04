namespace DeltaTrack;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class AttachAttributeAttribute(Type attributeType, params object[] constructorArguments) : Attribute
{
    public Type AttributeType { get; } = attributeType ?? throw new ArgumentNullException(nameof(attributeType));
    public object[] ConstructorArguments { get; } = constructorArguments ?? [];
}