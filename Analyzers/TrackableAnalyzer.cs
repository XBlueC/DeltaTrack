using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DeltaTrack;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TrackableAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor TrackableAttributeRule = new(
        "TRACK001",
        "Trackable attribute can only be used on partial classes",
        "The 'Trackable' attribute can only be applied to partial classes, not on regular classes",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "This attribute is intended to be used only on partial classes."
    );

    private static readonly DiagnosticDescriptor TrackableFieldRule = new(
        "TRACK002",
        "TrackableField can only be used on private fields in partial classes",
        "The 'TrackableField' attribute can only be applied to private fields in partial classes",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "This attribute is intended to be used only on private fields within partial classes."
    );

    private static readonly DiagnosticDescriptor TooManyFieldsRule = new(
        "TRACK003",
        "Too many trackable fields",
        "Class '{0}' has {1} trackable fields, which exceeds the maximum of 64. DirtyFlag uses a 64-bit bitmask and cannot represent more than 64 fields",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "DeltaTrack uses a long (64-bit) bitmask for dirty flags. Reduce the number of tracked fields to 64 or fewer."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(TrackableAttributeRule, TrackableFieldRule, TooManyFieldsRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is INamedTypeSymbol namedTypeSymbol &&
            namedTypeSymbol.TypeKind == TypeKind.Class)
        {
            var hasAttribute = namedTypeSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "TrackableAttribute");

            if (hasAttribute)
            {
                var isPartial = namedTypeSymbol.DeclaringSyntaxReferences
                    .Any(refs =>
                        refs.GetSyntax() is ClassDeclarationSyntax classSyntax
                        && classSyntax.Modifiers.Any(m =>
                            m.IsKind(SyntaxKind.PartialKeyword)));

                if (!isPartial)
                {
                    var location = namedTypeSymbol.Locations.FirstOrDefault() ?? Location.None;
                    var diagnostic = Diagnostic.Create(TrackableAttributeRule, location);
                    context.ReportDiagnostic(diagnostic);
                }

                // Check trackable field count <= 64
                CheckTrackableFieldCount(context, namedTypeSymbol, true);
            }
            else
            {
                // Class without [Trackable] but may have [TrackableField] fields
                CheckTrackableFieldCount(context, namedTypeSymbol, false);
            }
        }
    }

    private static void CheckTrackableFieldCount(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol, bool hasTrackableAttribute)
    {
        var count = 0;
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IFieldSymbol field)
                continue;

            var hasTrackableFieldAttr = field.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "TrackableFieldAttribute");

            var hasTrackIgnoreAttr = field.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "TrackIgnoreAttribute");

            if (hasTrackIgnoreAttr)
                continue;

            if (hasTrackableFieldAttr || (hasTrackableAttribute && field.DeclaredAccessibility == Accessibility.Private))
                count++;
        }

        if (count > 64)
        {
            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            var diagnostic = Diagnostic.Create(TooManyFieldsRule, location, typeSymbol.Name, count);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        if (context.Symbol is not IFieldSymbol fieldSymbol)
            return;

        var hasTrackableFieldAttribute = fieldSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "TrackableFieldAttribute");

        if (!hasTrackableFieldAttribute)
            return;

        var containingType = fieldSymbol.ContainingType;
        if (containingType == null)
            return;

        var isPartialClass = containingType.DeclaringSyntaxReferences
            .Any(refs =>
                refs.GetSyntax() is ClassDeclarationSyntax classSyntax
                && classSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        var isPrivate = fieldSymbol.DeclaredAccessibility == Accessibility.Private;

        if (!isPartialClass || !isPrivate)
        {
            var location = fieldSymbol.Locations.FirstOrDefault() ?? Location.None;
            var diagnostic = Diagnostic.Create(TrackableFieldRule, location);
            context.ReportDiagnostic(diagnostic);
        }
    }
}