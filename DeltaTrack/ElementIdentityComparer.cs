using System.Runtime.CompilerServices;

namespace DeltaTrack;

internal sealed class ElementIdentityComparer<T> : IEqualityComparer<T>
{
    public static readonly ElementIdentityComparer<T> Instance = new();

    public bool Equals(T x, T y) => typeof(T).IsValueType
        ? EqualityComparer<T>.Default.Equals(x, y)
        : ReferenceEquals(x, y);

    public int GetHashCode(T value) => typeof(T).IsValueType
        ? EqualityComparer<T>.Default.GetHashCode(value)
        : value is null
            ? 0
            : RuntimeHelpers.GetHashCode(value);
}