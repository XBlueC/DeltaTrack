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
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsSyntaxTargetForGeneration(node),
                static (ctx, _) => GetClassInfo(ctx))
            .Where(static c => c.ShouldGenerate);

        context.RegisterSourceOutput(provider, GenerateCode);
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        if (!classDecl.Modifiers.Any(m => m.IsKind(PartialKeyword)))
            return false;

        if (classDecl.AttributeLists.Count > 0)
            return true;

        foreach (var member in classDecl.Members)
        {
            if (member is FieldDeclarationSyntax field && field.AttributeLists.Count > 0)
            {
                foreach (var attrList in field.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        var name = attr.Name.ToString();
                        if (name == "TrackableField" || name.EndsWith("TrackableField"))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static ClassInfo GetClassInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var typeSymbol = ModelExtensions.GetDeclaredSymbol(model, classDecl);
        if (typeSymbol == null)
            return new ClassInfo { ShouldGenerate = false };

        var hasClassTrackableAttribute = HasTrackableAttribute(typeSymbol);

        var fields = classDecl.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => ShouldTrackField(f, hasClassTrackableAttribute))
            .SelectMany(f => f.Declaration.Variables.Select(v => new
            {
                Variable = v,
                FieldDeclaration = f,
                TypeSymbol = model.GetTypeInfo(f.Declaration.Type).Type,
                FieldSymbol = (IFieldSymbol)model.GetDeclaredSymbol(v)
            }))
            .Where(x => x.TypeSymbol != null && !HasTrackIgnoreAttribute(x.FieldSymbol))
            .ToList();

        return new ClassInfo
        {
            ShouldGenerate = fields.Any(),
            Namespace = typeSymbol.ContainingNamespace?.IsGlobalNamespace == true ? string.Empty : typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            ClassName = typeSymbol.Name,
            Fields = fields.Select(f =>
            {
                var wrapper = TryGetCollectionWrapperType(f.TypeSymbol);
                return new FieldInfo
                {
                    Name = f.Variable.Identifier.Text,
                    Type = f.TypeSymbol.ToDisplayString(),
                    IsCollection = IsCollectionType(f.TypeSymbol!),
                    IsTrackable = HasTrackableAttribute(f.TypeSymbol!),
                    WrapperTypeName = wrapper?.WrapperTypeName ?? string.Empty,
                    CollectionInterfaceName = wrapper?.InterfaceName ?? string.Empty,
                    HasCollectionOfTrackable = IsCollectionOfTrackable(f.TypeSymbol!),
                    HasSetOfTrackable = IsSetOfTrackable(f.TypeSymbol!),
                    HasDictionaryWithTrackableValues = IsDictionaryWithTrackableValues(f.TypeSymbol!),
                    AdditionalAttributes = ExtractAdditionalAttributes(f.FieldSymbol)
                };
            }).ToImmutableArray()
        };
    }

    private static bool ShouldTrackField(FieldDeclarationSyntax field, bool hasClassTrackableAttribute)
    {
        if (HasTrackIgnoreAttributeSyntax(field))
            return false;

        if (hasClassTrackableAttribute && IsPrivateField(field))
            return true;

        if (HasTrackableFieldAttribute(field))
            return true;

        return false;
    }

    private static bool IsPrivateField(FieldDeclarationSyntax field)
    {
        return field.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));
    }

    private static bool HasTrackIgnoreAttributeSyntax(FieldDeclarationSyntax field)
    {
        return field.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr =>
                attr.Name.ToString() == "TrackIgnore" ||
                attr.Name.ToString() == TrackIgnoreAttributeFullName ||
                attr.Name.ToFullString().EndsWith("TrackIgnore"));
    }

    private static bool HasTrackIgnoreAttribute(IFieldSymbol fieldSymbol)
    {
        return fieldSymbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == TrackIgnoreAttributeFullName);
    }

    private static ImmutableArray<PropertyAttributeInfo> ExtractAdditionalAttributes(IFieldSymbol fieldSymbol)
    {
        if (fieldSymbol == null) return ImmutableArray<PropertyAttributeInfo>.Empty;

        var result = ImmutableArray.CreateBuilder<PropertyAttributeInfo>();
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

            result.Add(new()
            {
                AttributeFullName = attrFullName,
                Arguments = args.ToImmutable()
            });
        }

        return result.ToImmutable();
    }

    private static void AddAttributeArgument(ImmutableArray<AttributeArgument>.Builder args, TypedConstant typedConst, SymbolDisplayFormat format)
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

    private static bool IsCollectionType(ITypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i =>
            i.Name == "ICollection" &&
            i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");
    }

    private static (string WrapperTypeName, string InterfaceName)? TryGetCollectionWrapperType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
            return null;

        var format = SymbolDisplayFormat.FullyQualifiedFormat;
        if (ImplementsIListInterface(namedType))
        {
            var itemType = namedType.TypeArguments[0].ToDisplayString(format);
            return ($"global::DeltaTrack.TrackableList<{itemType}>", $"global::System.Collections.Generic.IList<{itemType}>");
        }

        if (ImplementsIDictionaryInterface(namedType))
        {
            var keyType = namedType.TypeArguments[0].ToDisplayString(format);
            var valueType = namedType.TypeArguments[1].ToDisplayString(format);
            return ($"global::DeltaTrack.TrackableDictionary<{keyType}, {valueType}>", $"global::System.Collections.Generic.IDictionary<{keyType}, {valueType}>");
        }

        if (ImplementsISetInterface(namedType))
        {
            var setType = namedType.TypeArguments[0].ToDisplayString(format);
            return ($"global::DeltaTrack.TrackableSet<{setType}>", $"global::System.Collections.Generic.ISet<{setType}>");
        }

        return null;
    }

    private static bool HasTrackableAttribute(ISymbol symbol) =>
        symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == TrackableAttributeFullName);

    private static bool HasTrackableFieldAttribute(FieldDeclarationSyntax field) =>
        field.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr =>
                attr.Name.ToString() == "TrackableField" ||
                attr.Name.ToString() == TrackableFieldAttributeFullName ||
                attr.Name.ToFullString().EndsWith("TrackableField"));

    private static bool ImplementsIListInterface(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.AllInterfaces.Any(i =>
                i.Name == "ISet" &&
                i.IsGenericType &&
                i.TypeArguments.Length == 1))
        {
            return false;
        }

        var ienumerableT = typeSymbol.AllInterfaces.FirstOrDefault(i =>
            i.Name == "IEnumerable" &&
            i.IsGenericType &&
            i.TypeArguments.Length == 1);

        if (ienumerableT == null) return false;

        return typeSymbol.GetMembers("Add")
            .OfType<IMethodSymbol>()
            .Any(m => m.Parameters.Length == 1 &&
                      m.Parameters[0].Type.Equals(ienumerableT.TypeArguments[0], SymbolEqualityComparer.Default));
    }

    private static bool ImplementsIDictionaryInterface(INamedTypeSymbol typeSymbol)
    {
        // 检查是否实现 IEnumerable<KeyValuePair<TKey, TValue>>
        return typeSymbol.AllInterfaces.Any(i =>
            i.Name == "IEnumerable" &&
            i.IsGenericType &&
            i.TypeArguments.Length == 1 &&
            i.TypeArguments[0] is INamedTypeSymbol constructed &&
            constructed.Name == "KeyValuePair" &&
            constructed.IsGenericType &&
            constructed.TypeArguments.Length == 2);
    }

    private static bool ImplementsISetInterface(INamedTypeSymbol typeSymbol)
    {
        if (ImplementsIListInterface(typeSymbol)) return false;

        var ienumerableT = typeSymbol.AllInterfaces.FirstOrDefault(i =>
            i.Name == "IEnumerable" &&
            i.IsGenericType &&
            i.TypeArguments.Length == 1);

        if (ienumerableT == null) return false;

        return typeSymbol.GetMembers("Add")
            .OfType<IMethodSymbol>()
            .Any(m => m.Parameters.Length == 1 &&
                      m.Parameters[0].Type.Equals(ienumerableT.TypeArguments[0], SymbolEqualityComparer.Default));
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

        sb.AppendLine($"    partial class {classInfo.ClassName} : global::DeltaTrack.ITrackable");
        sb.AppendLine("    {");

        AppendDirtyFlagEnum(sb, classInfo);
        AppendFieldIndexConstants(sb, classInfo);
        AppendDirtyFlagNames(sb, classInfo);
        AppendTrackerProperty(sb, classInfo);
        AppendProperties(sb, classInfo);
        AppendOnChangeMethods(sb, classInfo);
        AppendHelperMethods(sb, classInfo);

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(classInfo.Namespace))
        {
            sb.AppendLine("}");
        }

        string hintName;
        if (!string.IsNullOrEmpty(classInfo.Namespace))
        {
            hintName = $"{classInfo.Namespace}.{classInfo.ClassName}.Track.g.cs";
        }
        else
        {
            hintName = $"{classInfo.ClassName}.Track.g.cs";
        }

        context.AddSource(hintName, sb.ToString());
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
        sb.AppendLine($"        private static readonly string[] _dirtyFlagNames = new string[] {{ {string.Join(", ", names)} }};");
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
            sb.AppendLine($"            tracker.Subscribe({propName}, On{propName}Changed);");
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
        sb.AppendLine($@"                if (!global::System.Collections.Generic.EqualityComparer<{fieldType}>.Default.Equals({fieldName}, value))");
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
        sb.AppendLine($@"                GetTracker();");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _tracker!.Unsubscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        sb.AppendLine($@"                {fieldName} = value;");
        sb.AppendLine($@"                On{propName}Changed();");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _tracker!.Subscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
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
        sb.AppendLine($@"                {fieldName} ??= new {field.Type}();");
        sb.AppendLine($@"                {wrapperFieldName} ??= new {wrapperType}(On{propName}Changed, {fieldName});");
        sb.AppendLine($@"                return {wrapperFieldName};");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"            set");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    GetTracker().Unsubscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        // Handle null safely
        sb.AppendLine($@"                {fieldName} = value == null ? null : new {field.Type}(value);");
        sb.AppendLine($@"                {wrapperFieldName} = {fieldName} == null ? null : new {wrapperType}(On{propName}Changed, {fieldName});");
        sb.AppendLine($@"                On{propName}Changed();");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    GetTracker().Subscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"        }");
    }

    private static void AppendAddedAttributes(StringBuilder sb, FieldInfo field)
    {
        foreach (var attr in field.AdditionalAttributes)
        {
            if (attr.Arguments.Length == 0)
            {
                sb.AppendLine($"        [{attr.AttributeFullName}]");
            }
            else
            {
                var args = string.Join(", ", attr.Arguments.Select(FormatAttributeArgument));
                sb.AppendLine($"        [{attr.AttributeFullName}({args})]");
            }
        }
    }

    private static void AppendOnChangeMethods(StringBuilder sb, ClassInfo classInfo)
    {
        for (var i = 0; i < classInfo.Fields.Length; i++)
        {
            var slot = i / 64;
            var bit = i % 64;
            var propName = ToPropertyName(classInfo.Fields[i].Name);
            sb.AppendLine($@"        private void On{propName}Changed() => GetTracker().MarkChanged({slot}, 1L << {bit}, this);");
        }

        sb.AppendLine();
    }

    private static void AppendHelperMethods(StringBuilder sb, ClassInfo classInfo)
    {
        // MarkPropClean (recursive cleanup)
        sb.AppendLine("        private void MarkPropClean(bool recursive = false)");
        sb.AppendLine("        {");

        var trackableFields = classInfo.Fields.Where(f =>
            f.IsTrackable ||
            f.HasCollectionOfTrackable ||
            f.HasSetOfTrackable ||
            f.HasDictionaryWithTrackableValues).ToList();
        if (trackableFields.Count > 0)
        {
            sb.AppendLine("            if (recursive)");
            sb.AppendLine("            {");
            foreach (var field in trackableFields)
            {
                var fieldName = field.Name;
                if (field.IsTrackable)
                {
                    sb.AppendLine($@"                if ({fieldName} is global::DeltaTrack.ITrackable trackable_{fieldName})");
                    sb.AppendLine($@"                    trackable_{fieldName}.MarkClean(true);");
                }
                else if (field.IsCollection)
                {
                    if (field.HasCollectionOfTrackable || field.HasSetOfTrackable)
                    {
                        sb.AppendLine($@"                if ({fieldName} != null)");
                        sb.AppendLine($@"                {{");
                        sb.AppendLine($@"                    foreach (var item in {fieldName})");
                        sb.AppendLine($@"                    {{");
                        sb.AppendLine($@"                        if (item is global::DeltaTrack.ITrackable trackableItem)");
                        sb.AppendLine($@"                            trackableItem.MarkClean(true);");
                        sb.AppendLine($@"                    }}");
                        sb.AppendLine($@"                }}");
                    }
                    else if (field.HasDictionaryWithTrackableValues)
                    {
                        sb.AppendLine($@"                if ({fieldName} != null)");
                        sb.AppendLine($@"                {{");
                        sb.AppendLine($@"                    foreach (var kvp in {fieldName})");
                        sb.AppendLine($@"                    {{");
                        sb.AppendLine($@"                        if (kvp.Value is global::DeltaTrack.ITrackable trackableValue)");
                        sb.AppendLine($@"                            trackableValue.MarkClean(true);");
                        sb.AppendLine($@"                    }}");
                        sb.AppendLine($@"                }}");
                    }
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
            sb.AppendLine("            GetTracker().MarkChanged(0, (long)flag);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        public void MarkChanged(string property)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int i = 0; i < _dirtyFlagNames.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (_dirtyFlagNames[i] == property)");
        sb.AppendLine("                {");
        sb.AppendLine("                    GetTracker().MarkChanged(i >> 6, 1L << (i & 63));");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        AppendDeltaMethods(sb, classInfo);
    }

    private static void AppendDeltaMethods(StringBuilder sb, ClassInfo classInfo)
    {
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyDictionary<string, object> GetDelta()");
        sb.AppendLine("        {");
        sb.AppendLine("            var tracker = GetTracker();");
        sb.AppendLine("            if (!tracker.HasChanges()) return new global::System.Collections.Generic.Dictionary<string, object>(0);");
        sb.AppendLine("            var slots = tracker.DirtyFlagsArray;");
        sb.AppendLine("            var result = new global::System.Collections.Generic.Dictionary<string, object>();");
        for (var i = 0; i < classInfo.Fields.Length; i++)
        {
            var slot = i / 64;
            var bit = i % 64;
            var propName = ToPropertyName(classInfo.Fields[i].Name);
            sb.AppendLine($"            if ((slots[{slot}] & (1L << {bit})) != 0) result[\"{propName}\"] = (object){propName};");
        }

        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        public void ApplyDelta(global::System.Collections.Generic.IReadOnlyDictionary<string, object> delta)");
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

    private static bool IsCollectionOfTrackable(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;

        if (!ImplementsIListInterface(namedType)) return false;

        if (namedType.TypeArguments.Length == 0) return false;

        var elementType = namedType.TypeArguments[0];
        return HasTrackableAttribute(elementType);
    }

    private static bool IsSetOfTrackable(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;

        if (!ImplementsISetInterface(namedType)) return false;

        if (namedType.TypeArguments.Length == 0) return false;

        var elementType = namedType.TypeArguments[0];
        return HasTrackableAttribute(elementType);
    }

    private static bool IsDictionaryWithTrackableValues(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;

        if (!ImplementsIDictionaryInterface(namedType)) return false;

        if (namedType.TypeArguments.Length < 2) return false;

        var valueType = namedType.TypeArguments[1];
        return HasTrackableAttribute(valueType);
    }

    private static string ToPropertyName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return fieldName;
        if (fieldName.StartsWith("_") && fieldName.Length > 1)
            return char.ToUpper(fieldName[1]) + fieldName.Substring(2);
        return char.ToUpper(fieldName[0]) + fieldName.Substring(1);
    }

    private static string FormatAttributeArgument(AttributeArgument arg)
    {
        if (arg.IsEnum && arg.EnumTypeName != null && arg.Value != null)
        {
            if (arg.EnumMemberName != null)
                return $"{arg.EnumTypeName}.{arg.EnumMemberName}";

            return $"{arg.EnumTypeName}.{arg.Value}";
        }

        return FormatAttributeValue(arg.Value);
    }

    private static string FormatAttributeValue(object value) => value switch
    {
        null => "null",
        string s => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
        char c => $"'{c}'",
        bool b => b.ToString().ToLowerInvariant(),
        int i => i.ToString(),
        long l => l + "L",
        float f => f.ToString(CultureInfo.InvariantCulture) + "f",
        double d => d.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null"
    };

    private record ClassInfo
    {
        public bool ShouldGenerate { get; set; }
        public string Namespace { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public ImmutableArray<FieldInfo> Fields { get; set; } = ImmutableArray<FieldInfo>.Empty;
    }

    private record FieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsCollection { get; set; }
        public bool IsTrackable { get; set; }
        public string WrapperTypeName { get; set; } = string.Empty;
        public string CollectionInterfaceName { get; set; } = string.Empty;
        public bool HasWrapper => !string.IsNullOrEmpty(WrapperTypeName);
        public bool HasCollectionOfTrackable { get; set; }
        public bool HasSetOfTrackable { get; set; }
        public bool HasDictionaryWithTrackableValues { get; set; }
        public ImmutableArray<PropertyAttributeInfo> AdditionalAttributes { get; set; } = ImmutableArray<PropertyAttributeInfo>.Empty;
    }

    private record PropertyAttributeInfo
    {
        public string AttributeFullName { get; set; } = string.Empty;
        public ImmutableArray<AttributeArgument> Arguments { get; set; } = ImmutableArray<AttributeArgument>.Empty;
    }

    private record AttributeArgument
    {
        public object Value { get; set; }
        public bool IsEnum { get; set; }
        public string EnumTypeName { get; set; }
        public string EnumMemberName { get; set; }
    }
}