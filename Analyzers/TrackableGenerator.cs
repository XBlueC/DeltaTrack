using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace XAnalyzers;

[Generator]
public class TrackableGenerator : IIncrementalGenerator
{
    private const string TrackableAttributeFullName = "DeltaTrack.TrackableAttribute";
    private const string TrackableFieldAttributeFullName = "DeltaTrack.TrackableFieldAttribute";
    private const string AttachAttributeAttributeFullName = "DeltaTrack.AttachAttributeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsSyntaxTargetForGeneration(node),
                static (ctx, _) => GetClassInfo(ctx))
            .Where(static c => c.ShouldGenerate);

        context.RegisterSourceOutput(provider, GenerateCode);
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node) =>
        node is ClassDeclarationSyntax
        {
            AttributeLists.Count: > 0,
            Modifiers: var mods
        } && mods.Any(m => m.IsKind(PartialKeyword));

    private static ClassInfo GetClassInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var typeSymbol = ModelExtensions.GetDeclaredSymbol(model, classDecl);

        if (typeSymbol == null || !HasTrackableAttribute(typeSymbol))
            return new() { ShouldGenerate = false };

        var fields = classDecl.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(HasTrackableFieldAttribute)
            .SelectMany(f => f.Declaration.Variables.Select(v => new
            {
                Variable = v,
                FieldDeclaration = f,
                TypeSymbol = model.GetTypeInfo(f.Declaration.Type).Type,
                FieldSymbol = (IFieldSymbol)model.GetDeclaredSymbol(v)
            }))
            .Where(x => x.TypeSymbol != null)
            .ToList();

        return new()
        {
            ShouldGenerate = fields.Any(),
            Namespace = typeSymbol.ContainingNamespace?.IsGlobalNamespace == true ? string.Empty : typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            ClassName = typeSymbol.Name,
            Fields = fields.Select(f => new FieldInfo
            {
                Name = f.Variable.Identifier.Text,
                Type = f.FieldDeclaration.Declaration.Type.ToString(),
                TypeSymbol = f.TypeSymbol!,
                IsCollection = IsCollectionType(f.TypeSymbol!),
                IsTrackable = HasTrackableAttribute(f.TypeSymbol!),
                AdditionalAttributes = ExtractAdditionalAttributes(f.FieldSymbol)
            }).ToList()
        };
    }

    private static List<PropertyAttributeInfo> ExtractAdditionalAttributes(IFieldSymbol fieldSymbol)
    {
        var result = new List<PropertyAttributeInfo>();
        if (fieldSymbol == null) return result;

        foreach (var attr in fieldSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != AttachAttributeAttributeFullName)
                continue;

            if (attr.ConstructorArguments.Length < 1)
                continue;

            var attrType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
            if (attrType == null)
                continue;

            var attrFullName = attrType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var args = new List<AttributeArgument>();

            for (var i = 1; i < attr.ConstructorArguments.Length; i++)
            {
                var typedConst = attr.ConstructorArguments[i];
                if (typedConst.Kind == TypedConstantKind.Array)
                {
                    foreach (var element in typedConst.Values)
                    {
                        args.Add(new()
                        {
                            Value = element.Value,
                            Type = element.Type
                        });
                    }
                }
                else
                {
                    args.Add(new()
                    {
                        Value = typedConst.Value,
                        Type = typedConst.Type
                    });
                }
            }

            result.Add(new()
            {
                AttributeFullName = attrFullName,
                Arguments = args
            });
        }

        return result;
    }

    private static bool IsCollectionType(ITypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i => 
            i.Name == "ICollection" && 
            i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");
    }

    private static bool TryGetCollectionWrapperType(ITypeSymbol typeSymbol, out string wrapperTypeName, out string interfaceName)
    {
        wrapperTypeName = string.Empty;
        interfaceName = string.Empty;

        if (typeSymbol is not INamedTypeSymbol namedType)
            return false;

        var format = SymbolDisplayFormat.FullyQualifiedFormat;
        if (ImplementsIListInterface(namedType))
        {
            var itemType = namedType.TypeArguments[0].ToDisplayString(format);
            wrapperTypeName = $"global::DeltaTrack.TrackableList<{itemType}>";
            interfaceName = $"global::System.Collections.Generic.IList<{itemType}>";
            return true;
        }

        if (ImplementsIDictionaryInterface(namedType))
        {
            var keyType = namedType.TypeArguments[0].ToDisplayString(format);
            var valueType = namedType.TypeArguments[1].ToDisplayString(format);
            wrapperTypeName = $"global::DeltaTrack.TrackableDictionary<{keyType}, {valueType}>";
            interfaceName = $"global::System.Collections.Generic.IDictionary<{keyType}, {valueType}>";
            return true;
        }
        
        if (ImplementsISetInterface(namedType))
        {
            var setType = namedType.TypeArguments[0].ToDisplayString(format);
            wrapperTypeName = $"global::DeltaTrack.TrackableSet<{setType}>";
            interfaceName = $"global::System.Collections.Generic.ISet<{setType}>";
            return true;
        }

        return false;
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
        return typeSymbol.AllInterfaces.Any(i => 
            i.Name == "IList" && 
            i.IsGenericType &&
            i.TypeArguments.Length == 1 &&
            i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");
    }

    private static bool ImplementsIDictionaryInterface(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i => 
            i.Name == "IDictionary" && 
            i.IsGenericType &&
            i.TypeArguments.Length == 2 &&
            i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");
    }

    private static bool ImplementsISetInterface(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i => 
            i.Name == "ISet" && 
            i.IsGenericType &&
            i.TypeArguments.Length == 1 &&
            i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");
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

        AppendTrackerProperty(sb);
        AppendConstructor(sb, classInfo);
        AppendProperties(sb, classInfo);
        AppendOnChangeMethods(sb, classInfo);
        AppendHelperMethods(sb, classInfo);

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(classInfo.Namespace))
        {
            sb.AppendLine("}");
        }

        context.AddSource($"{classInfo.ClassName}Properties.g.cs", sb.ToString());
    }

    private static void AppendTrackerProperty(StringBuilder sb)
    {        
        sb.AppendLine("        private global::DeltaTrack.ChangeTracker _changeTracker = new global::DeltaTrack.ChangeTracker();");
        sb.AppendLine();
        sb.AppendLine("        public global::DeltaTrack.IChangeTracker GetChangeTracker() => _changeTracker;");
        sb.AppendLine();
    }

    private static void AppendConstructor(StringBuilder sb, ClassInfo classInfo)
    {
        sb.AppendLine($"        public {classInfo.ClassName}()");
        sb.AppendLine("        {");
        sb.AppendLine("            _changeTracker.OnClean += MarkPropClean;");
        var trackableFields = classInfo.Fields.Where(f => f.IsTrackable);
        foreach (var field in trackableFields)
        {
            var propName = ToPropertyName(field.Name);
            sb.AppendLine($"            _changeTracker.Subscribe({propName}, On{propName}Changed);");
        }
        sb.AppendLine("        }");
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
        sb.AppendLine($@"            get => {fieldName};");
        sb.AppendLine(@"            set");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _changeTracker.Unsubscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        sb.AppendLine($@"                {fieldName} = value;");
        sb.AppendLine($@"                On{propName}Changed();");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _changeTracker.Subscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"        }");
    }

    private static void GenerateTrackingCollectionsProperty(StringBuilder sb, FieldInfo field)
    {
        var fieldName = field.Name;
        var propName = ToPropertyName(fieldName);
        var wrapperFieldName = $"_wrapper{propName}";

        if (field.TypeSymbol is not INamedTypeSymbol namedType ||
            !TryGetCollectionWrapperType(namedType, out var wrapperType, out var interfaceName))
        {
            GenerateTrackingProperty(sb, field);
            return;
        }

        sb.AppendLine($@"        private {wrapperType} {wrapperFieldName};");
        sb.AppendLine();

        AppendAddedAttributes(sb, field);
        sb.AppendLine($@"        public {interfaceName} {propName}");
        sb.AppendLine(@"        {");
        sb.AppendLine(@"            get");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                if ({wrapperFieldName} != null) return {wrapperFieldName};");
        sb.AppendLine($@"                {fieldName} ??= new {field.Type}();");
        sb.AppendLine($@"                {wrapperFieldName} ??= new {wrapperType}(On{propName}Changed, {fieldName});");
        sb.AppendLine($@"                return {wrapperFieldName};");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"            set");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _changeTracker.Unsubscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        // Handle null safely
        sb.AppendLine($@"                {fieldName} = value == null ? null : new {field.Type}(value);");
        sb.AppendLine($@"                {wrapperFieldName} = {fieldName} == null ? null : new {wrapperType}(On{propName}Changed, {fieldName});");
        sb.AppendLine($@"                On{propName}Changed();");
        sb.AppendLine($@"                if ({fieldName} != null)");
        sb.AppendLine(@"                {");
        sb.AppendLine($@"                    _changeTracker.Subscribe({propName}, On{propName}Changed);");
        sb.AppendLine(@"                }");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"        }");
    }

    private static void AppendAddedAttributes(StringBuilder sb, FieldInfo field)
    {
        foreach (var attr in field.AdditionalAttributes)
        {
            if (attr.Arguments.Count == 0)
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
        foreach (var field in classInfo.Fields)
        {
            var propName = ToPropertyName(field.Name);
            sb.AppendLine($@"        private void On{propName}Changed() => MarkPropChanged(nameof({propName}));");
        }
        sb.AppendLine();
    }

    private static void AppendHelperMethods(StringBuilder sb, ClassInfo classInfo)
    {
        sb.AppendLine("        private void MarkPropChanged(string property)");
        sb.AppendLine("        {");
        sb.AppendLine("            _changeTracker?.MarkChanged(property);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private void MarkPropClean(bool recursive = false)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (recursive)");
        sb.AppendLine("            {");
        
        var trackableFields = classInfo.Fields.Where(f => f.IsTrackable || f.IsCollection);
        foreach (var field in trackableFields)
        {
            var propName = ToPropertyName(field.Name);
            if (field.IsTrackable)
            {
                sb.AppendLine($@"                if ({propName} is global::DeltaTrack.ITrackable trackable_{propName} && trackable_{propName}.GetChangeTracker().HasChanges())");
                sb.AppendLine($@"                    trackable_{propName}.GetChangeTracker().MarkClean(true);");
            }
            else if (field.IsCollection)
            {
                if (IsCollectionOfTrackable(field.TypeSymbol))
                {
                    sb.AppendLine($@"                if ({propName} != null)");
                    sb.AppendLine($@"                {{");
                    sb.AppendLine($@"                    foreach (var item in {propName})");
                    sb.AppendLine($@"                    {{");
                    sb.AppendLine($@"                        if (item is global::DeltaTrack.ITrackable trackableItem && trackableItem.GetChangeTracker().HasChanges())");
                    sb.AppendLine($@"                            trackableItem.GetChangeTracker().MarkClean(true);");
                    sb.AppendLine($@"                    }}");
                    sb.AppendLine($@"                }}");
                }
                else if (IsDictionaryWithTrackableValues(field.TypeSymbol))
                {
                    sb.AppendLine($@"                if ({propName} != null)");
                    sb.AppendLine($@"                {{");
                    sb.AppendLine($@"                    foreach (var kvp in {propName})");
                    sb.AppendLine($@"                    {{");
                    sb.AppendLine($@"                        if (kvp.Value is global::DeltaTrack.ITrackable trackableValue && trackableValue.GetChangeTracker().HasChanges())");
                    sb.AppendLine($@"                            trackableValue.GetChangeTracker().MarkClean(true);");
                    sb.AppendLine($@"                    }}");
                    sb.AppendLine($@"                }}");
                }
            }
        }
        
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static bool IsCollectionOfTrackable(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;
        
        var constructedFrom = namedType.ConstructedFrom.ToDisplayString();
        if (constructedFrom != "System.Collections.Generic.List<T>" && 
            constructedFrom != "System.Collections.Generic.HashSet<T>") return false;
            
        if (namedType.TypeArguments.Length == 0) return false;
        
        var elementType = namedType.TypeArguments[0];
        return HasTrackableAttribute(elementType);
    }

    private static bool IsDictionaryWithTrackableValues(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;
        
        var constructedFrom = namedType.ConstructedFrom.ToDisplayString();
        if (constructedFrom != "System.Collections.Generic.Dictionary<TKey, TValue>") return false;
            
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
        if (arg.IsEnum && arg.Type is INamedTypeSymbol enumType && arg.Value != null)
        {
            var enumFullName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.ConstantValue?.Equals(arg.Value) == true)
                    return $"{enumFullName}.{member.Name}";
            }
            return $"{enumFullName}.{arg.Value}";
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
        _ => value?.ToString() ?? "null"
    };

    private record ClassInfo
    {
        public bool ShouldGenerate { get; set; }
        public string Namespace { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public List<FieldInfo> Fields { get; set; } = new();
    }

    private record FieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public ITypeSymbol TypeSymbol { get; set; } = null!;
        public bool IsCollection { get; set; }
        public bool IsTrackable { get; set; }
        public List<PropertyAttributeInfo> AdditionalAttributes { get; set; } = new();
    }

    private record PropertyAttributeInfo
    {
        public string AttributeFullName { get; set; } = string.Empty;
        public List<AttributeArgument> Arguments { get; set; } = new();
    }

    private record AttributeArgument
    {
        public object Value { get; set; }
        public ITypeSymbol Type { get; set; }
        public bool IsEnum => Type?.TypeKind == TypeKind.Enum;
    }
}