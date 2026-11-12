using Microsoft.CodeAnalysis;

namespace DeltaTrack;

internal static class TrackingModelFacts
{
    public static bool HasAttribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);

    public static INamedTypeSymbol FindTrackableBase(INamedTypeSymbol type)
    {
        for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
        {
            if (HasAttribute(parent, "DeltaTrack.TrackableAttribute") ||
                parent.AllInterfaces.Any(i => i.ToDisplayString() == "DeltaTrack.ITrackable") ||
                parent.GetMembers().OfType<IFieldSymbol>()
                    .Any(f => HasAttribute(f, "DeltaTrack.TrackableFieldAttribute")))
                return parent;
        }

        return null;
    }
}