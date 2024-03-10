using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace zzre.sourcegen;

[Generator]
public class ConfigSourceGenerator : IIncrementalGenerator
{
    public enum TypeCategory
    {
        Double,
        String,
        Boolean,
        NumericCastable,
        Enumeration
    }

    public record Variable
    {
        public required string FieldName { get; init; }
        public required string LocalKey { get; init; }
        public required string ClassFullName { get; init; }
        public required string Namespace { get; init; }
        public required TypeCategory TypeCategory { get; init; }
        public required string TypeName { get; init; }
        public required bool HasMetadata { get; init; }
    }

    public record Section
    {
        public string ClassFullName => Variables.First().ClassFullName;
        public string Namespace => Variables.First().Namespace;
        public required ImmutableEnumerable<Variable> Variables { get; init; }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configurationAttributes = context.SyntaxProvider.ForAttributeWithMetadataName(
            "zzre.ConfigurationAttribute",
            (_, _) => true,
            GetModelFor)
            .Collect()
            .SelectMany(GroupVariables);

        context.RegisterSourceOutput(configurationAttributes, GenerateSource);
    }

    private Variable GetModelFor(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        var attribute = context.Attributes.First();

        var localKey = attribute.NamedArguments
            .FirstOrDefault(kv => kv.Key == "Key")
            .Value.Value?.ToString()
            ?? context.TargetSymbol.Name;

        var namespaceName = "";
        var curSymbol = context.TargetSymbol;
        while (curSymbol.ContainingType is not null)
            curSymbol = curSymbol.ContainingType;
        if (curSymbol.ContainingNamespace is not null)
            namespaceName = curSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (namespaceName.StartsWith("global::"))
            namespaceName = namespaceName.Substring("global::".Length);

        var fieldSymbol = (IFieldSymbol)context.TargetSymbol;
        if (!TryGetTypeInfoFromSpecialType(fieldSymbol.Type.SpecialType, out var typeCategory, out var typeName))
        {
            if (fieldSymbol.Type.TypeKind == TypeKind.Enum)
            {
                typeCategory = TypeCategory.Enumeration;
                typeName = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else
            {
                typeCategory = TypeCategory.String;
                typeName = "There was an error with this type: " + fieldSymbol.Type.ToDisplayString();
            }
        }

        return new()
        {
            FieldName = context.TargetSymbol.Name,
            ClassFullName = context.TargetSymbol.ContainingType
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Substring("global::".Length),
            Namespace = namespaceName,
            LocalKey = localKey,
            HasMetadata = attribute.NamedArguments.Any(kv => kv.Key == "Description"),
            TypeCategory = typeCategory,
            TypeName = typeName,
        };
    }

    private bool TryGetTypeInfoFromSpecialType(SpecialType specialType, out TypeCategory category,  out string typeName)
    {
        switch(specialType)
        {
            case SpecialType.System_String:
                category = TypeCategory.String;
                typeName = "string";
                return true;
            case SpecialType.System_Byte:
                category = TypeCategory.NumericCastable;
                typeName = "byte";
                return true;
            case SpecialType.System_SByte:
                category = TypeCategory.NumericCastable;
                typeName = "sbyte";
                return true;
            case SpecialType.System_Int16:
                category = TypeCategory.NumericCastable;
                typeName = "short";
                return true;
            case SpecialType.System_UInt16:
                category = TypeCategory.NumericCastable;
                typeName = "ushort";
                return true;
            case SpecialType.System_Int32:
                category = TypeCategory.NumericCastable;
                typeName = "int";
                return true;
            case SpecialType.System_UInt32:
                category = TypeCategory.NumericCastable;
                typeName = "uint";
                return true;
            case SpecialType.System_Int64:
                category = TypeCategory.NumericCastable;
                typeName = "long";
                return true;
            case SpecialType.System_UInt64:
                category = TypeCategory.NumericCastable;
                typeName = "ulong";
                return true;
            case SpecialType.System_Single:
                category = TypeCategory.NumericCastable;
                typeName = "float";
                return true;
            case SpecialType.System_Double:
                category = TypeCategory.Double;
                typeName = "double";
                return true;
            case SpecialType.System_Boolean:
                category = TypeCategory.Boolean;
                typeName = "bool";
                return true;
            default:
                category = TypeCategory.String;
                typeName = "Unknown special type naeme";
                return false;
        }
    }

    private ImmutableArray<Section> GroupVariables(ImmutableArray<Variable> array, CancellationToken token) => array
        .GroupBy(v => v.ClassFullName)
        .Select(group => new Section()
        {
            Variables = ImmutableEnumerable<Variable>.Create(group)
        }).ToImmutableArray();

    private void GenerateSource(SourceProductionContext context, Section section)
    {
        int i;
        var classPath = section.ClassFullName.Substring(section.Namespace.Length + 1);
        var source = new StringBuilder();
        source
            .AppendLine("// This file was generated by zzre.sourcegen (ConfigSourceGenerator)")
            .AppendLine("using System;")
            .AppendLine("using System.Collections.Generic;")
            .AppendLine()
            .AppendLine($"namespace {section.Namespace};")
            .AppendLine();

        var classes = classPath.Split('.');
        for (i = 0; i < classes.Length - 1; i++)
            source.AppendLine("partial class " + classes[i] + " {");
        source.AppendLine($"partial class {classes.Last()} : IConfigurationSection {{");

        // Static constructor
        if (section.Variables.Any(v => v.HasMetadata))
        {
            source.AppendLine($"    static {classes.Last()}");
            source.AppendLine("    {");
            i = -1;
            foreach (var variable in section.Variables)
            {
                i++;
                if (!variable.HasMetadata)
                    continue;
                source.Append("        zzre.Configuration.RegisterMetadataField(ConfigurationKeys[");
                source.Append(i);
                source.Append("], typeof(");
                source.Append(classes.Last());
                source.Append("), nameof(");
                source.Append(variable.FieldName);
                source.AppendLine("));");
            }
            source.AppendLine("    }");
            source.AppendLine();
        }

        // Configuration Section/Keys
        source.Append("    private const string ConfigurationSection = \"zzre.\" + nameof(");
        source.Append(classes.Last());
        source.AppendLine(");");
        source.AppendLine("    private static readonly string[] ConfigurationKeys =");
        source.AppendLine("    [");
        foreach (var variable in section.Variables)
        {
            source.Append("        $\"{ConfigurationSection}.");
            source.Append(variable.LocalKey);
            source.AppendLine("\",");
        }
        source.AppendLine("    ];");
        source.AppendLine("    IEnumerable<string> IConfigurationSection.Keys => ConfigurationKeys;");
        source.AppendLine();

        // subscript getter
        source.AppendLine("    ConfigurationValue IConfigurationSection.this[string key]");
        source.AppendLine("    {");
        source.AppendLine("        get => Array.IndexOf(ConfigurationKeys , key) switch");
        source.AppendLine("        {");
        i = 0;
        foreach (var variable in section.Variables)
        {
            source.Append("            ");
            source.Append(i++);
            source.Append(" => new(");
            switch(variable.TypeCategory)
            {
                case TypeCategory.Double:
                case TypeCategory.String:
                    source.Append(variable.FieldName);
                    break;
                case TypeCategory.NumericCastable:
                case TypeCategory.Enumeration:
                    source.Append("checked((double)");
                    source.Append(variable.FieldName);
                    source.Append(")");
                    break;
                case TypeCategory.Boolean:
                    source.Append(variable.FieldName);
                    source.Append(" ? 1.0 : 0.0");
                    break;
                default: throw new NotImplementedException();
            }
            source.AppendLine("),");
        }
        source.AppendLine("            _ => throw new KeyNotFoundException()");
        source.AppendLine("        };");
        source.AppendLine();

        // subscript setter
        source.AppendLine("        set");
        source.AppendLine("        {");
        source.AppendLine("            switch (Array.IndexOf(ConfigurationKeys, key))");
        source.AppendLine("            {");
        i = 0;
        foreach (var variable in section.Variables)
        {
            source.Append("                case ");
            source.Append(i++);
            source.Append(": ");
            source.Append(variable.FieldName);
            source.Append(" = ");
            switch(variable.TypeCategory)
            {
                case TypeCategory.Double:
                    source.Append("value.Numeric");
                    break;
                case TypeCategory.String:
                    source.Append("value.String");
                    break;
                case TypeCategory.NumericCastable:
                    source.Append("checked((");
                    source.Append(variable.TypeName);
                    source.Append(")value.Numeric)");
                    break;
                case TypeCategory.Boolean:
                    source.Append("value.Numeric != 0.0");
                    break;
                case TypeCategory.Enumeration:
                    source.Append("value.IsNumeric ? (");
                    source.Append(variable.TypeName);
                    source.Append(")value.Numeric : Enum.Parse<");
                    source.Append(variable.TypeName);
                    source.Append(">(value.String)");
                    break;
                default: throw new NotImplementedException();
            }
            source.AppendLine("; break;");
        }
        source.AppendLine("                default: throw new KeyNotFoundException();");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("    }");

        source.AppendLine(new string('}', classes.Length));

        context.AddSource($"{classPath}.Config.g.cs", source.ToString());
    }
}
