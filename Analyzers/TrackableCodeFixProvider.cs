using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DeltaTrack;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TrackableCodeFixProvider)), Shared]
public class TrackableCodeFixProvider : CodeFixProvider
{
    private const string TrackableAttributeRuleId = "TRACK001";
    private const string TrackableFieldRuleId = "TRACK002";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(TrackableAttributeRuleId, TrackableFieldRuleId);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        switch (diagnostic.Id)
        {
            case TrackableAttributeRuleId:
                await RegisterTrackableAttributeFixAsync(context, diagnostic);
                break;
            case TrackableFieldRuleId:
                await RegisterTrackableFieldFixAsync(context, diagnostic);
                break;
        }
    }

    private static async Task RegisterTrackableAttributeFixAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var classDecl = node as ClassDeclarationSyntax;
        if (classDecl == null) return;

        context.RegisterCodeFix(
            new CustomCodeAction(
                "Make class partial",
                c => MakeClassPartialAsync(context.Document, classDecl, c),
                diagnostic),
            diagnostic);
    }

    private static async Task RegisterTrackableFieldFixAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // 诊断位置是 VariableDeclaratorSyntax，需要向上找到 FieldDeclarationSyntax
        var fieldDecl = node as FieldDeclarationSyntax;
        if (fieldDecl == null)
        {
            fieldDecl = node?.Parent as FieldDeclarationSyntax;
            if (fieldDecl == null)
            {
                fieldDecl = node?.Parent?.Parent as FieldDeclarationSyntax;
            }
        }

        if (fieldDecl == null) return;

        var classDecl = fieldDecl.Parent as ClassDeclarationSyntax;
        if (classDecl == null) return;

        var isPartialClass = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        var isPrivate = fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));

        if (!isPartialClass)
        {
            context.RegisterCodeFix(
                new CustomCodeAction(
                    "Make class partial",
                    c => MakeClassPartialAsync(context.Document, classDecl, c),
                    diagnostic),
                diagnostic);
        }

        if (!isPrivate)
        {
            context.RegisterCodeFix(
                new CustomCodeAction(
                    "Make field private",
                    c => MakeFieldPrivateAsync(context.Document, fieldDecl, c),
                    diagnostic),
                diagnostic);
        }

        if (!isPartialClass && !isPrivate)
        {
            context.RegisterCodeFix(
                new CustomCodeAction(
                    "Make class partial and field private",
                    c => MakeClassPartialAndFieldPrivateAsync(context.Document, classDecl, fieldDecl, c),
                    diagnostic),
                diagnostic);
        }
    }

    private static async Task<Document> MakeClassPartialAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
    {
        var modifiers = classDecl.Modifiers;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            return document;
        }

        // 找到访问修饰符之后的位置插入 partial
        var insertIndex = 0;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.PublicKeyword) ||
                modifiers[i].IsKind(SyntaxKind.InternalKeyword) ||
                modifiers[i].IsKind(SyntaxKind.ProtectedKeyword) ||
                modifiers[i].IsKind(SyntaxKind.PrivateKeyword))
            {
                insertIndex = i + 1;
            }
        }

        var newModifiers = modifiers.Insert(insertIndex, SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var newClassDecl = classDecl.WithModifiers(newModifiers);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var newRoot = root.ReplaceNode(classDecl, newClassDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> MakeFieldPrivateAsync(Document document, FieldDeclarationSyntax fieldDecl, CancellationToken cancellationToken)
    {
        var newModifiers = SyntaxFactory.TokenList();
        newModifiers = newModifiers.Add(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

        foreach (var m in fieldDecl.Modifiers)
        {
            if (!m.IsKind(SyntaxKind.PublicKeyword) &&
                !m.IsKind(SyntaxKind.ProtectedKeyword) &&
                !m.IsKind(SyntaxKind.InternalKeyword))
            {
                newModifiers = newModifiers.Add(m);
            }
        }

        var newFieldDecl = fieldDecl.WithModifiers(newModifiers);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var newRoot = root.ReplaceNode(fieldDecl, newFieldDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> MakeClassPartialAndFieldPrivateAsync(Document document, ClassDeclarationSyntax classDecl, FieldDeclarationSyntax fieldDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var classModifiers = classDecl.Modifiers;
        SyntaxTokenList newClassModifiers;
        if (classModifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            newClassModifiers = classModifiers;
        }
        else
        {
            // 找到访问修饰符之后的位置插入 partial
            var insertIndex = 0;
            for (var i = 0; i < classModifiers.Count; i++)
            {
                if (classModifiers[i].IsKind(SyntaxKind.PublicKeyword) ||
                    classModifiers[i].IsKind(SyntaxKind.InternalKeyword) ||
                    classModifiers[i].IsKind(SyntaxKind.ProtectedKeyword) ||
                    classModifiers[i].IsKind(SyntaxKind.PrivateKeyword))
                {
                    insertIndex = i + 1;
                }
            }

            newClassModifiers = classModifiers.Insert(insertIndex, SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        }

        var newFieldModifiers = SyntaxFactory.TokenList();
        newFieldModifiers = newFieldModifiers.Add(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

        foreach (var m in fieldDecl.Modifiers)
        {
            if (!m.IsKind(SyntaxKind.PublicKeyword) &&
                !m.IsKind(SyntaxKind.ProtectedKeyword) &&
                !m.IsKind(SyntaxKind.InternalKeyword))
            {
                newFieldModifiers = newFieldModifiers.Add(m);
            }
        }

        var newFieldDecl = fieldDecl.WithModifiers(newFieldModifiers);

        // 先在类内部替换字段，再替换整个类（同时添加 partial）
        var newClassDecl = classDecl.ReplaceNode(fieldDecl, newFieldDecl);
        newClassDecl = newClassDecl.WithModifiers(newClassModifiers);

        var newRoot = root.ReplaceNode(classDecl, newClassDecl);

        return document.WithSyntaxRoot(newRoot);
    }

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    private sealed class CustomCodeAction : Microsoft.CodeAnalysis.CodeActions.CodeAction
    {
        private readonly Func<CancellationToken, Task<Document>> _createChangedDocument;
        private readonly string _title;
        private readonly string _equivalenceKey;

        public CustomCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, Diagnostic diagnostic)
        {
            _title = title;
            _createChangedDocument = createChangedDocument;
            _equivalenceKey = $"{diagnostic.Id}_{title}";
        }

        public override string Title => _title;
        public override string EquivalenceKey => _equivalenceKey;

        protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            return _createChangedDocument(cancellationToken);
        }
    }
}