using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PartialClassOnlyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "PARTIAL001",
        "Attribute can only be used on partial classes",
        "The '{0}' attribute can only be applied to partial classes, not on regular classes",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "This attribute is intended to be used only on partial classes."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
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
                    var diagnostic = Diagnostic.Create(Rule, location, "TrackableAttribute");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}