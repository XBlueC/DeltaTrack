using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace DeltaTrack;

[Generator]
public class TrackableGenerator : IIncrementalGenerator
{
    private const string TrackableAttributeFullName = "DeltaTrack.TrackableAttribute";
    private const string TrackableFieldAttributeFullName = "DeltaTrack.TrackableFieldAttribute";
    private const string AttachAttributeAttributeFullName = "DeltaTrack.AttachAttributeAttribute";
    private const string TrackIgnoreAttributeFullName = "DeltaTrack.TrackIgnoreAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .ForAttributeWithMetadataName(TrackableAttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax declaration &&
                                    declaration.Modifiers.Any(PartialKeyword),
                static (ctx, token) => GetClassInfo((INamedTypeSymbol)ctx.TargetSymbol, token))
            .Where(static c => c.ShouldGenerate);

        var fields = context.SyntaxProvider
            .ForAttributeWithMetadataName(TrackableFieldAttributeFullName,
                static (_, _) => true,
                static (ctx, token) => GetFieldClassInfo(ctx, token))
            .Where(static c => c.ShouldGenerate);

        context.RegisterSourceOutput(classes.WithTrackingName("TrackableClasses"), GenerateCode);
        context.RegisterSourceOutput(fields.WithTrackingName("TrackableFields"), GenerateCode);
    }

    private static ClassInfo GetFieldClassInfo(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        if (context.TargetSymbol is not IFieldSymbol field || HasTrackableAttribute(field.ContainingType))
            return new ClassInfo();

        // A type can have several annotated fields, including fields in other partial declarations.
        var first = field.ContainingType.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(f => HasAttribute(f, TrackableFieldAttributeFullName));
        return SymbolEqualityComparer.Default.Equals(first, field)
            ? GetClassInfo(field.ContainingType, token)
            : new ClassInfo();
    }

    private static ClassInfo GetClassInfo(INamedTypeSymbol typeSymbol, CancellationToken token)
    {
        if (typeSymbol.IsStatic || typeSymbol.TypeKind != TypeKind.Class ||
            TrackingModelFacts.FindTrackableBase(typeSymbol) != null ||
            !typeSymbol.DeclaringSyntaxReferences.Any(r =>
                r.GetSyntax(token) is ClassDeclarationSyntax c && c.Modifiers.Any(PartialKeyword)))
            return new ClassInfo();

        var autoTrack = HasTrackableAttribute(typeSymbol);
        var fields = typeSymbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared && !f.IsStatic && !f.IsConst && !f.IsReadOnly &&
                        f.DeclaredAccessibility == Accessibility.Private && f.Type.TypeKind != TypeKind.Error &&
                        (autoTrack || HasAttribute(f, TrackableFieldAttributeFullName)) &&
                        !HasAttribute(f, TrackIgnoreAttributeFullName))
            .ToArray();

        var containers = new Stack<INamedTypeSymbol>();
        for (var parent = typeSymbol.ContainingType; parent != null; parent = parent.ContainingType)
        {
            if (!parent.DeclaringSyntaxReferences.Any(r =>
                    r.GetSyntax(token) is TypeDeclarationSyntax c && c.Modifiers.Any(PartialKeyword)))
                return new ClassInfo();
            containers.Push(parent);
        }

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new ClassInfo
        {
            ShouldGenerate = fields.Length > 0,
            Namespace = ns,
            Declaration = GetTypeDeclaration(typeSymbol),
            ContainingTypes = containers.Select(GetTypeDeclaration).ToImmutableArray(),
            HintName = (string.IsNullOrEmpty(ns) ? "" : ns + ".") +
                       string.Join("+", containers.Select(t => t.MetadataName).Append(typeSymbol.MetadataName)) +
                       ".Track.g.cs",
            Fields = fields.Select(f =>
            {
                token.ThrowIfCancellationRequested();
                var wrapper = TryGetCollectionWrapperType(f.Type);
                return new FieldInfo
                {
                    Name = EscapeIdentifier(f.Name),
                    Type = f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    IsCollection = wrapper != null,
                    IsTrackable = IsTrackableType(f.Type) || wrapper == null && MayHoldTrackable(f.Type),
                    WrapperTypeName = wrapper?.WrapperTypeName ?? string.Empty,
                    CollectionInterfaceName = wrapper?.InterfaceName ?? string.Empty,
                    StorageTypeName = wrapper?.StorageTypeName ?? string.Empty,
                    PopulateCollection = wrapper?.PopulateCollection ?? false,
                    CopyExpression = wrapper?.CopyExpression ?? string.Empty,
                    NeedsElementTracking = wrapper != null && ElementMayContainTrackable(wrapper.ElementType),
                    IsDictionary = wrapper?.Kind == "IDictionary",
                    AdditionalAttributes = ExtractAdditionalAttributes(f)
                };
            }).ToImmutableArray()
        };
    }

    private static string GetTypeDeclaration(INamedTypeSymbol type)
    {
        var kind = type.TypeKind == TypeKind.Struct ? "struct" :
            type.TypeKind == TypeKind.Interface ? "interface" : "class";
        if (type.IsRecord) kind = "record " + kind;
        var parameters = type.TypeParameters.Length == 0
            ? ""
            : "<" + string.Join(", ", type.TypeParameters.Select(p => EscapeIdentifier(p.Name))) + ">";
        return (type.IsStatic ? "static " : "") + "partial " + kind + " " + EscapeIdentifier(type.Name) + parameters;
    }

    private static string EscapeIdentifier(string name) => SyntaxFacts.GetKeywordKind(name) != None ? "@" + name : name;

    private static bool HasAttribute(ISymbol symbol, string fullName) => symbol.GetAttributes()
        .Any(a => a.AttributeClass?.ToDisplayString() == fullName);

    private static bool IsTrackableType(ITypeSymbol type) => type != null &&
                                                             (type.ToDisplayString() == "DeltaTrack.ITrackable" ||
                                                              (HasTrackableAttribute(type) ||
                                                               type.AllInterfaces.Any(i =>
                                                                   i.ToDisplayString() == "DeltaTrack.ITrackable") ||
                                                               type is INamedTypeSymbol
                                                               {
                                                                   TypeKind: TypeKind.Class
                                                               } named && named.GetMembers().OfType<IFieldSymbol>()
                                                                   .Any(f => HasAttribute(f,
                                                                       TrackableFieldAttributeFullName))));

    private static bool MayHoldTrackable(ITypeSymbol type) => type.IsReferenceType &&
                                                              (type.TypeKind == TypeKind.Interface ||
                                                               type is INamedTypeSymbol { IsSealed: false });

    private static bool ElementMayContainTrackable(ITypeSymbol type) => type.SpecialType != SpecialType.System_String &&
                                                                        (IsTrackableType(type) ||
                                                                         MayHoldTrackable(type) ||
                                                                         type is ITypeParameterSymbol ||
                                                                         type.AllInterfaces.Any(i =>
                                                                             i.ToDisplayString() ==
                                                                             "System.Collections.IEnumerable"));

    private static ImmutableArray<string> ExtractAdditionalAttributes(IFieldSymbol fieldSymbol)
    {
        if (fieldSymbol == null) return ImmutableArray<string>.Empty;

        var result = ImmutableArray.CreateBuilder<string>();
        var fullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

        foreach (var attr in fieldSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != AttachAttributeAttributeFullName)
                continue;

            if (attr.ConstructorArguments.Length < 1)
                continue;

            var attrType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
            if (attrType == null)
                continue;

            var attrFullName = attrType.ToDisplayString(fullyQualifiedFormat);
            var args = ImmutableArray.CreateBuilder<AttributeArgument>();

            for (var i = 1; i < attr.ConstructorArguments.Length; i++)
            {
                var typedConst = attr.ConstructorArguments[i];
                if (typedConst.Kind == TypedConstantKind.Array)
                {
                    foreach (var element in typedConst.Values)
                        AddAttributeArgument(args, element, fullyQualifiedFormat);
                }
                else
                {
                    AddAttributeArgument(args, typedConst, fullyQualifiedFormat);
                }
            }

            result.Add(attrFullName + "(" + string.Join(", ", args.Select(FormatAttributeArgument)) + ")");
        }

        return result.ToImmutable();
    }

    private static void AddAttributeArgument(ImmutableArray<AttributeArgument>.Builder args, TypedConstant typedConst,
        SymbolDisplayFormat format)
    {
        var isEnum = typedConst.Type?.TypeKind == TypeKind.Enum;
        string enumTypeName = null;
        string enumMemberName = null;

        if (isEnum && typedConst.Type is INamedTypeSymbol enumType && typedConst.Value != null)
        {
            enumTypeName = enumType.ToDisplayString(format);
            foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.ConstantValue?.Equals(typedConst.Value) == true)
                {
                    enumMemberName = member.Name;
                    break;
                }
            }
        }

        args.Add(new()
        {
            Value = typedConst.Value,
            IsEnum = isEnum,
            EnumTypeName = enumTypeName,
            EnumMemberName = enumMemberName
        });
    }

    private static CollectionInfo TryGetCollectionWrapperType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
            return null;

        var contract = FindCollectionInterface(namedType, "IDictionary") ??
                       FindCollectionInterface(namedType, "ISet") ?? FindCollectionInterface(namedType, "IList");
        if (contract == null) return null;

        var concreteName = contract.Name == "IDictionary" ? "Dictionary" : contract.Name == "ISet" ? "HashSet" : "List";
        var wrapperName = contract.Name == "IDictionary" ? "TrackableDictionary" :
            contract.Name == "ISet" ? "TrackableSet" : "TrackableList";
        var arguments = string.Join(", ",
            contract.TypeArguments.Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        var isInterface = namedType.TypeKind == TypeKind.Interface;
        if (isInterface && !SymbolEqualityComparer.Default.Equals(namedType, contract)) return null;
        if (!isInterface && (namedType.IsAbstract || !namedType.InstanceConstructors.Any(c =>
                c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public))) return null;

        var wrapperTypeName = $"global::DeltaTrack.{wrapperName}<{arguments}>";
        var copyMethod = isInterface ? "CopyItems" : "Copy" + namedType.Name;
        var hasCopyMethod = contract.Name == "IDictionary" &&
                            (isInterface || namedType.Name is "Dictionary" or "SortedDictionary" or "SortedList") ||
                            contract.Name == "ISet" && (isInterface || namedType.Name is "HashSet" or "SortedSet");
        hasCopyMethod &= isInterface || namedType.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";
        var unsupportedCopy = contract.Name != "IList" && !hasCopyMethod;
        return new CollectionInfo
        {
            Kind = contract.Name,
            ElementType = contract.TypeArguments[contract.TypeArguments.Length - 1],
            InterfaceName = contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            WrapperTypeName = wrapperTypeName,
            CopyExpression = hasCopyMethod
                ? $"{wrapperTypeName}.{copyMethod}(value)"
                : unsupportedCopy
                    ? "throw new global::System.NotSupportedException(\"Cannot copy a custom collection without a supported comparer. Use a standard dictionary/set or initialize the backing field in the constructor.\")"
                    : string.Empty,
            StorageTypeName = isInterface
                ? $"global::System.Collections.Generic.{concreteName}<{arguments}>"
                : namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            PopulateCollection = !isInterface && (namedType.Name != concreteName ||
                                                  namedType.ContainingNamespace.ToDisplayString() !=
                                                  "System.Collections.Generic")
        };
    }

    private static bool HasTrackableAttribute(ISymbol symbol) =>
        HasAttribute(symbol, TrackableAttributeFullName);

    private static INamedTypeSymbol FindCollectionInterface(INamedTypeSymbol type, string name)
    {
        bool Matches(INamedTypeSymbol candidate) => candidate.TypeKind == TypeKind.Interface &&
                                                    candidate.Name == name &&
                                                    candidate.ContainingNamespace.ToDisplayString() ==
                                                    "System.Collections.Generic";

        return Matches(type) ? type : type.AllInterfaces.FirstOrDefault(Matches);
    }

    private static void GenerateCode(SourceProductionContext context, ClassInfo classInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");

        if (!string.IsNullOrEmpty(classInfo.Namespace))
        {
            sb.AppendLine($"namespace {classInfo.Namespace}");
            sb.AppendLine("{");
        }

        foreach (var container in classInfo.ContainingTypes)
        {
            sb.AppendLine(container);
            sb.AppendLine("{");
        }

        sb.AppendLine($"    {classInfo.Declaration} : global::DeltaTrack.ITrackable");
        sb.AppendLine("    {");

        AppendDirtyFlagEnum(sb, classInfo);
        AppendFieldIndexConstants(sb, classInfo);
        AppendDirtyFlagNames(sb, classInfo);
        AppendTrackerProperty(sb, classInfo);
        AppendProperties(sb, classInfo);
        AppendOnChangeMethods(sb, classInfo);
        AppendHelperMethods(sb, classInfo);

        sb.AppendLine("    }");

        foreach (var _ in classInfo.ContainingTypes)
            sb.AppendLine("}");

        if (!string.IsNullOrEmpty(classInfo.Namespace))
        {
            sb.AppendLine("}");
        }

        context.AddSource(classInfo.HintName, sb.ToString());
    }

    private static void AppendDirtyFlagEnum(StringBuilder sb, ClassInfo classInfo)
    {
        // 字段数 > 64 时 [Flags] enum : long 已无法表达全部位，改由 FieldIndex 常量类承担
        if (classInfo.Fields.Length > 64) return;

        sb.AppendLine("        [global::System.Flags]");
        sb.AppendLine("        public enum DirtyFlag : long");
        sb.AppendLine("        {");
        sb.AppendLine("            None = 0,");
        for (var i = 0; i < classInfo.Fields.Length; i++)
        {
            var propName = ToPropertyName(classInfo.Fields[i].Name);
            sb.AppendLine($"            {propName} = 1L << {i},");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void AppendFieldIndexConstants(StringBuilder sb, ClassInfo classInfo)
    {
        sb.AppendLine("        public static class FieldIndex");
        sb.AppendLine("        {");
        for (var i = 0; i < classInfo.Fields.Length; i++)
        {
            var propName = ToPropertyName(classInfo.Fields[i].Name);
            sb.AppendLine($"            public const int {propName} = {i};");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void AppendDirtyFlagNames(StringBuilder sb, ClassInfo classInfo)
    {
        var names = classInfo.Fields.Select(f => $"\"{ToPropertyName(f.Name)}\"");
        sb.AppendLine(
            $"        private static readonly string[] _dirtyFlagNames = new string[] {{ {string.Join(", ", names)} }};");
        sb.AppendLine();
    }

    private static void AppendTrackerProperty(StringBuilder sb, ClassInfo classInfo)
    {
        var trackableFields = classInfo.Fields.Where(f => f.IsTrackable).ToList();
        var slotCount = (classInfo.Fields.Length + 63) / 64;
        if (slotCount < 1) slotCount = 1;

        sb.AppendLine("        private global::DeltaTrack.ChangeTracker _tracker;");
        sb.AppendLine();
        sb.AppendLine("        private global::DeltaTrack.ChangeTracker GetTracker()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_tracker != null) return _tracker;");
        sb.AppendLine($"            var tracker = new global::DeltaTrack.ChangeTracker({slotCount});");
        sb.AppendLine("            _tracker = tracker;");
        sb.AppendLine("            tracker.OnClean += MarkPropClean;");
        foreach (var field in trackableFields)
        {
            var propName = ToPropertyName(field.Name);
            sb.AppendLine($"            tracker.Subscribe({field.Name}, On{propName}Changed);");
        }

        foreach (var field in classInfo.Fields.Where(f => f.HasWrapper && !f.IsTrackable && f.NeedsElementTracking))
        {
            var propName = ToPropertyName(field.Name);
            sb.AppendLine($"            if ({field.Name} != null && _wrapper{propName} == null)");
            sb.AppendLine(
                $"                _wrapper{propName} = new {field.WrapperTypeName}(On{propName}Changed, {field.Name});");
        }

        sb.AppendLine("            return tracker;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public bool HasChanges() => GetTracker().HasChanges();");
        sb.AppendLine();
        sb.AppendLine("        public void MarkClean(bool recursive = false) => GetTracker().MarkClean(recursive);");
        sb.AppendLine();
        sb.AppendLine("        public event global::System.Action OnChanged");
        sb.AppendLine("        {");
        sb.AppendLine("            add => GetTracker().OnChanged += value;");
        sb.AppendLine("            remove => GetTracker().OnChanged -= value;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public event global::System.Action<global::DeltaTrack.ChangeInfo> OnChangedDetailed");
        sb.AppendLine("        {");
        sb.AppendLine("            add => GetTracker().OnChangedDetailed += value;");
        sb.AppendLine("            remove => GetTracker().OnChangedDetailed -= value;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public global::System.IDisposable SuspendTracking() => GetTracker().SuspendTracking();");
        sb.AppendLine();
    }

    private static void AppendProperties(StringBuilder sb, ClassInfo classInfo)
    {
        foreach (var field in classInfo.Fields)
        {
            if (field.IsTrackable)
            {
                GenerateTrackingProperty(sb, field);
            }
            else if (field.IsCollection)
            {
                GenerateTrackingCollectionsProperty(sb, field);
            }
            else
            {
                GenerateSimpleProperty(sb, field);
            }

            sb.AppendLine();
        }
    }

    private static void GenerateSimpleProperty(StringBuilder sb, FieldInfo field)
    {
        var fieldName = field.Name;
        var propName = ToPropertyName(fieldName);
        var fieldType = field.Type;

        AppendAddedAttributes(sb, field);
        sb.AppendLine($@"        public {fieldType} {propName}");
        sb.AppendLine(@"        {");
        sb.AppendLine($@"            get => {fieldName};");
        sb.AppendLine(@"            set");
        sb.AppendLine(@"            {");
        sb.AppendLine(
            $@"                if (!global::System.Collections.Generic.EqualityComparer<{fieldType}>.Default.Equals({fieldName}, value))");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    GetTracker();");
        sb.AppendLine($@"                    {fieldName} = value;");
        sb.AppendLine($@"                    On{propName}Changed();");
        sb.AppendLine(@"                }");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"        }");
    }

    private static void GenerateTrackingProperty(StringBuilder sb, FieldInfo field)
    {
        var fieldName = field.Name;
        var propName = ToPropertyName(fieldName);
        var fieldType = field.Type;

        AppendAddedAttributes(sb, field);
        sb.AppendLine($@"        public {fieldType} {propName}");
        sb.AppendLine(@"        {");
        sb.AppendLine($@"            get {{ GetTracker(); return {fieldName}; }}");
        sb.AppendLine(@"            set");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                if (global::System.Object.ReferenceEquals({fieldName}, value)) return;");
        sb.AppendLine($@"                GetTracker();");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _tracker.Unsubscribe({fieldName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        sb.AppendLine($@"                {fieldName} = value;");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _tracker.Subscribe({fieldName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        sb.AppendLine($@"                On{propName}Changed();");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"        }");
    }

    private static void GenerateTrackingCollectionsProperty(StringBuilder sb, FieldInfo field)
    {
        var fieldName = field.Name;
        var propName = ToPropertyName(fieldName);
        var wrapperFieldName = $"_wrapper{propName}";

        if (!field.HasWrapper)
        {
            GenerateTrackingProperty(sb, field);
            return;
        }

        var wrapperType = field.WrapperTypeName;
        var interfaceName = field.CollectionInterfaceName;

        sb.AppendLine($@"        private {wrapperType} {wrapperFieldName};");
        sb.AppendLine();

        AppendAddedAttributes(sb, field);
        sb.AppendLine($@"        public {interfaceName} {propName}");
        sb.AppendLine(@"        {");
        sb.AppendLine(@"            get");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                GetTracker();");
        sb.AppendLine($@"                if ({wrapperFieldName} != null) return {wrapperFieldName};");
        sb.AppendLine($@"                {fieldName} ??= new {field.StorageTypeName}();");
        sb.AppendLine($@"                {wrapperFieldName} ??= new {wrapperType}(On{propName}Changed, {fieldName});");
        sb.AppendLine($@"                return {wrapperFieldName};");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"            set");
        sb.AppendLine(@"            {");
        sb.AppendLine(
            $@"                if (global::System.Object.ReferenceEquals(value, {wrapperFieldName}) || global::System.Object.ReferenceEquals(value, {fieldName})) return;");
        var copy = string.IsNullOrEmpty(field.CopyExpression)
            ? $"new {field.StorageTypeName}({(field.PopulateCollection ? "" : "value")})"
            : field.CopyExpression;
        sb.AppendLine($"                {field.Type} replacement = value == null ? null : {copy};");
        if (field.PopulateCollection && string.IsNullOrEmpty(field.CopyExpression))
        {
            sb.AppendLine("                if (value != null)");
            sb.AppendLine("                    foreach (var item in value)");
            sb.AppendLine(
                $"                        (({interfaceName})replacement).Add({(field.IsDictionary ? "item.Key, item.Value" : "item")});");
        }

        sb.AppendLine("                GetTracker();");
        sb.AppendLine($@"                {wrapperFieldName}?.Dispose();");
        sb.AppendLine($@"                {fieldName} = replacement;");
        sb.AppendLine(
            $@"                {wrapperFieldName} = {fieldName} == null ? null : new {wrapperType}(On{propName}Changed, {fieldName});");
        sb.AppendLine($@"                On{propName}Changed();");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"        }");
    }

    private static void AppendAddedAttributes(StringBuilder sb, FieldInfo field)
    {
        foreach (var attr in field.AdditionalAttributes)
        {
            sb.AppendLine($"        [{attr}]");
        }
    }

    private static void AppendOnChangeMethods(StringBuilder sb, ClassInfo classInfo)
    {
        for (var i = 0; i < classInfo.Fields.Length; i++)
        {
            var slot = i / 64;
            var bit = i % 64;
            var propName = ToPropertyName(classInfo.Fields[i].Name);
            sb.AppendLine(
                $@"        private void On{propName}Changed() => GetTracker().MarkChanged({slot}, 1L << {bit}, this);");
        }

        sb.AppendLine();
    }

    private static void AppendHelperMethods(StringBuilder sb, ClassInfo classInfo)
    {
        // MarkPropClean (recursive cleanup)
        sb.AppendLine("        private void MarkPropClean(bool recursive = false)");
        sb.AppendLine("        {");

        var trackableFields = classInfo.Fields.Where(f => f.IsTrackable || f.IsCollection && f.NeedsElementTracking)
            .ToList();
        if (trackableFields.Count > 0)
        {
            sb.AppendLine("            if (recursive)");
            sb.AppendLine("            {");
            foreach (var field in trackableFields)
            {
                var fieldName = field.Name;
                if (field.IsTrackable)
                {
                    sb.AppendLine(
                        $@"                if ({fieldName} is global::DeltaTrack.ITrackable trackable_{fieldName})");
                    sb.AppendLine($@"                    trackable_{fieldName}.MarkClean(true);");
                }
                else if (field.IsCollection)
                {
                    var items = field.IsDictionary ? $"{fieldName}?.Values" : fieldName;
                    sb.AppendLine($"                global::DeltaTrack.ChangeTracker.MarkItemsClean({items});");
                }
            }

            sb.AppendLine("            }");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        if (classInfo.Fields.Length <= 64)
        {
            sb.AppendLine("        public DirtyFlag GetDirtyFlags() => (DirtyFlag)GetTracker().DirtyFlagsArray[0];");
            sb.AppendLine();
        }

        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<string> GetChangedProperties()");
        sb.AppendLine("        {");
        sb.AppendLine("            var tracker = GetTracker();");
        sb.AppendLine("            if (!tracker.HasChanges()) return global::System.Array.Empty<string>();");
        sb.AppendLine("            var slots = tracker.DirtyFlagsArray;");
        sb.AppendLine("            var result = new global::System.Collections.Generic.List<string>();");
        sb.AppendLine("            for (int i = 0; i < _dirtyFlagNames.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                if ((slots[i >> 6] & (1L << (i & 63))) != 0)");
        sb.AppendLine("                    result.Add(_dirtyFlagNames[i]);");
        sb.AppendLine("            }");
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();

        if (classInfo.Fields.Length <= 64)
        {
            sb.AppendLine("        public void MarkChanged(DirtyFlag flag)");
            sb.AppendLine("        {");
            sb.AppendLine("            GetTracker().MarkChanged(0, (long)flag, this);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        public void MarkChanged(string property)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int i = 0; i < _dirtyFlagNames.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (_dirtyFlagNames[i] == property)");
        sb.AppendLine("                {");
        sb.AppendLine("                    GetTracker().MarkChanged(i >> 6, 1L << (i & 63), this);");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        AppendDeltaMethods(sb, classInfo);
    }

    private static void AppendDeltaMethods(StringBuilder sb, ClassInfo classInfo)
    {
        sb.AppendLine(
            "        public global::System.Collections.Generic.IReadOnlyDictionary<string, object> GetDelta()");
        sb.AppendLine("        {");
        sb.AppendLine("            var tracker = GetTracker();");
        sb.AppendLine(
            "            if (!tracker.HasChanges()) return global::DeltaTrack.ChangeTracker.EmptyDelta;");
        sb.AppendLine("            var slots = tracker.DirtyFlagsArray;");
        sb.AppendLine("            var result = new global::System.Collections.Generic.Dictionary<string, object>();");
        for (var i = 0; i < classInfo.Fields.Length; i++)
        {
            var slot = i / 64;
            var bit = i % 64;
            var propName = ToPropertyName(classInfo.Fields[i].Name);
            sb.AppendLine(
                $"            if ((slots[{slot}] & (1L << {bit})) != 0) result[\"{propName}\"] = (object){propName};");
        }

        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine(
            "        public void ApplyDelta(global::System.Collections.Generic.IReadOnlyDictionary<string, object> delta)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (delta == null) return;");
        sb.AppendLine("            foreach (var kvp in delta)");
        sb.AppendLine("            {");
        sb.AppendLine("                switch (kvp.Key)");
        sb.AppendLine("                {");
        foreach (var field in classInfo.Fields)
        {
            var propName = ToPropertyName(field.Name);
            string castExpr;
            if (field.IsCollection && field.HasWrapper)
            {
                castExpr = $"global::DeltaTrack.ChangeTracker.DeltaCast<{field.CollectionInterfaceName}>(kvp.Value)";
            }
            else
            {
                castExpr = $"global::DeltaTrack.ChangeTracker.DeltaCast<{field.Type}>(kvp.Value)";
            }

            sb.AppendLine($"                    case \"{propName}\": {propName} = {castExpr}; break;");
        }

        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static string ToPropertyName(string fieldName)
    {
        fieldName = fieldName.TrimStart('@');
        if (string.IsNullOrEmpty(fieldName)) return fieldName;
        if (fieldName.StartsWith("_") && fieldName.Length > 1)
            return char.ToUpperInvariant(fieldName[1]) + fieldName.Substring(2);
        return char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1);
    }

    private static string FormatAttributeArgument(AttributeArgument arg)
    {
        if (arg.IsEnum && arg.EnumTypeName != null && arg.Value != null)
        {
            if (arg.EnumMemberName != null)
                return $"{arg.EnumTypeName}.{arg.EnumMemberName}";

            return $"({arg.EnumTypeName}){FormatAttributeValue(arg.Value)}";
        }

        return FormatAttributeValue(arg.Value);
    }

    private static string FormatAttributeValue(object value) => value switch
    {
        null => "null",
        string s => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(s, true),
        char c => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(c, true),
        ITypeSymbol type => $"typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})",
        bool b => b.ToString().ToLowerInvariant(),
        int i => i.ToString(),
        long l => l + "L",
        decimal d => d.ToString(CultureInfo.InvariantCulture) + "m",
        float f => f.ToString(CultureInfo.InvariantCulture) + "f",
        double d => d.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null"
    };

    private record ClassInfo
    {
        public bool ShouldGenerate { get; set; }
        public string Namespace { get; set; } = string.Empty;
        public string Declaration { get; set; } = string.Empty;
        public string HintName { get; set; } = string.Empty;
        public EquatableArray<string> ContainingTypes { get; set; }
        public EquatableArray<FieldInfo> Fields { get; set; }
    }

    private record FieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsCollection { get; set; }
        public bool IsTrackable { get; set; }
        public string WrapperTypeName { get; set; } = string.Empty;
        public string CollectionInterfaceName { get; set; } = string.Empty;
        public string StorageTypeName { get; set; } = string.Empty;
        public bool PopulateCollection { get; set; }
        public string CopyExpression { get; set; } = string.Empty;
        public bool IsDictionary { get; set; }
        public bool HasWrapper => !string.IsNullOrEmpty(WrapperTypeName);
        public bool NeedsElementTracking { get; set; }

        public EquatableArray<string> AdditionalAttributes { get; set; }
    }

    private sealed class CollectionInfo
    {
        public string Kind { get; set; }
        public ITypeSymbol ElementType { get; set; }
        public string WrapperTypeName { get; set; }
        public string InterfaceName { get; set; }
        public string StorageTypeName { get; set; }
        public bool PopulateCollection { get; set; }
        public string CopyExpression { get; set; } = string.Empty;
    }

    private record AttributeArgument
    {
        public object Value { get; set; }
        public bool IsEnum { get; set; }
        public string EnumTypeName { get; set; }
        public string EnumMemberName { get; set; }
    }
}