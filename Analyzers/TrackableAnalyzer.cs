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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(TrackableAttributeRule, TrackableFieldRule);

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
            }
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