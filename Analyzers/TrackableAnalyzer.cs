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

    private static readonly DiagnosticDescriptor TrackIgnoreOutsideTrackableRule = new(
        "TRACK003",
        "TrackIgnore is only effective on fields of [Trackable] classes",
        "The 'TrackIgnore' attribute has no effect here; it is only meaningful on private fields of classes marked with [Trackable]",
        "Usage",
        DiagnosticSeverity.Warning,
        true,
        "TrackIgnore opts a private field out of the auto-tracking triggered by [Trackable]. On classes without [Trackable], private fields are not auto-tracked, so this attribute is a no-op."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(TrackableAttributeRule, TrackableFieldRule, TrackIgnoreOutsideTrackableRule);

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

            if (!hasAttribute) return;

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

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        if (context.Symbol is not IFieldSymbol fieldSymbol)
            return;

        var attributes = fieldSymbol.GetAttributes();
        var containingType = fieldSymbol.ContainingType;
        if (containingType == null)
            return;

        var hasTrackableFieldAttribute = attributes
            .Any(attr => attr.AttributeClass?.Name == "TrackableFieldAttribute");

        if (hasTrackableFieldAttribute)
        {
            var isPartialClass = containingType.DeclaringSyntaxReferences
                .Any(refs =>
                    refs.GetSyntax() is ClassDeclarationSyntax classSyntax
                    && classSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

            var isPrivate = fieldSymbol.DeclaredAccessibility == Accessibility.Private;

            if (!isPartialClass || !isPrivate)
            {
                var location = fieldSymbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(TrackableFieldRule, location));
            }
        }

        var hasTrackIgnoreAttribute = attributes
            .Any(attr => attr.AttributeClass?.Name == "TrackIgnoreAttribute");

        if (hasTrackIgnoreAttribute)
        {
            var classHasTrackable = containingType.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "TrackableAttribute");

            if (!classHasTrackable)
            {
                var location = fieldSymbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(TrackIgnoreOutsideTrackableRule, location));
            }
        }
    }
}