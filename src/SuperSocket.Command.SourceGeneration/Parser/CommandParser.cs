using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SuperSocket.Command.SourceGeneration.Model;

namespace SuperSocket.Command.SourceGeneration.Parser;

internal static class CommandParser
{
    private const string CommandAttributeName = "SuperSocket.Command.CommandAttribute";
    private const string CommandFilterBaseAttributeName = "SuperSocket.Command.CommandFilterBaseAttribute";
    private const string AttributeUsageAttributeName = "System.AttributeUsageAttribute";
    private const string CommandInterfaceName = "ICommand`2";
    private const string AsyncCommandInterfaceName = "IAsyncCommand`2";
    private const string CommandNamespace = "SuperSocket.Command";

    public static CommandModel? TryParse(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        return TryParseCommand(typeSymbol, context.SemanticModel.Compilation);
    }

    private static CommandModel? TryParseCommand(INamedTypeSymbol typeSymbol, Compilation compilation)
    {
        if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsAbstract)
        {
            return null;
        }

        INamedTypeSymbol? commandInterface = FindCommandInterface(typeSymbol.AllInterfaces, AsyncCommandInterfaceName)
            ?? FindCommandInterface(typeSymbol.AllInterfaces, CommandInterfaceName);

        if (commandInterface is null)
        {
            return null;
        }

        ITypeSymbol sessionType = commandInterface.TypeArguments[0];
        ITypeSymbol packageType = commandInterface.TypeArguments[1];

        (string? attributeName, string? attributeKeyLiteral, string? attributeKeyTypeFullName) = ParseCommandAttribute(typeSymbol);

        (EquatableArray<FilterAttributeModel> filters, EquatableArray<string> unsupportedFilterTypeNames) = CollectFilters(typeSymbol, compilation);

        return new CommandModel(
            typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            typeSymbol.Name,
            sessionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            packageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            packageType.TypeKind == TypeKind.Interface,
            IsCommandInterface(commandInterface, AsyncCommandInterfaceName),
            attributeName,
            attributeKeyLiteral,
            attributeKeyTypeFullName,
            filters,
            unsupportedFilterTypeNames);
    }

    private static INamedTypeSymbol? FindCommandInterface(ImmutableArray<INamedTypeSymbol> interfaces, string metadataName)
    {
        foreach (INamedTypeSymbol interfaceSymbol in interfaces)
        {
            INamedTypeSymbol originalDefinition = interfaceSymbol.OriginalDefinition;

            if (string.Equals(originalDefinition.MetadataName, metadataName, StringComparison.Ordinal)
                && string.Equals(originalDefinition.ContainingNamespace.ToDisplayString(), CommandNamespace, StringComparison.Ordinal))
            {
                return interfaceSymbol;
            }
        }

        return null;
    }

    private static bool IsCommandInterface(INamedTypeSymbol interfaceSymbol, string metadataName)
    {
        INamedTypeSymbol originalDefinition = interfaceSymbol.OriginalDefinition;

        return string.Equals(originalDefinition.MetadataName, metadataName, StringComparison.Ordinal)
            && string.Equals(originalDefinition.ContainingNamespace.ToDisplayString(), CommandNamespace, StringComparison.Ordinal);
    }

    private static (string? AttributeName, string? AttributeKeyLiteral, string? AttributeKeyTypeFullName) ParseCommandAttribute(INamedTypeSymbol typeSymbol)
    {
        AttributeData? commandAttribute = typeSymbol.GetAttributes().FirstOrDefault(static attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), CommandAttributeName, StringComparison.Ordinal));

        if (commandAttribute is null)
        {
            return (null, null, null);
        }

        string? attributeName = null;
        string? attributeKeyLiteral = null;
        string? attributeKeyTypeFullName = null;

        if (commandAttribute.ConstructorArguments.Length > 0)
        {
            attributeName = GetStringValue(commandAttribute.ConstructorArguments[0]);
        }

        if (commandAttribute.ConstructorArguments.Length > 1)
        {
            TypedConstant key = commandAttribute.ConstructorArguments[1];
            attributeKeyLiteral = FormatLiteral(key);
            attributeKeyTypeFullName = GetValueTypeFullName(key);
        }

        foreach (KeyValuePair<string, TypedConstant> namedArgument in commandAttribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, "Name", StringComparison.Ordinal))
            {
                attributeName = GetStringValue(namedArgument.Value);
            }
            else if (string.Equals(namedArgument.Key, "Key", StringComparison.Ordinal))
            {
                attributeKeyLiteral = FormatLiteral(namedArgument.Value);
                attributeKeyTypeFullName = GetValueTypeFullName(namedArgument.Value);
            }
        }

        return (attributeName, attributeKeyLiteral, attributeKeyTypeFullName);
    }

    private static (EquatableArray<FilterAttributeModel> Filters, EquatableArray<string> UnsupportedFilterTypeNames) CollectFilters(INamedTypeSymbol typeSymbol, Compilation compilation)
    {
        var filters = new List<FilterAttributeModel>();
        var unsupportedFilterTypeNames = new List<string>();
        var emittedSingleUseFilterTypes = new HashSet<string>(StringComparer.Ordinal);

        for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
        {
            bool isCurrentCommandType = SymbolEqualityComparer.Default.Equals(current, typeSymbol);

            foreach (AttributeData attribute in current.GetAttributes())
            {
                INamedTypeSymbol? filterAttributeType = attribute.AttributeClass;

                if (!InheritsFrom(filterAttributeType, CommandFilterBaseAttributeName))
                {
                    continue;
                }

                AttributeUsageMetadata usage = GetAttributeUsage(filterAttributeType!);

                if (!isCurrentCommandType && !usage.Inherited)
                {
                    continue;
                }

                string filterTypeName = filterAttributeType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                if (!usage.AllowMultiple && emittedSingleUseFilterTypes.Contains(filterTypeName))
                {
                    continue;
                }

                if (!usage.AllowMultiple)
                {
                    emittedSingleUseFilterTypes.Add(filterTypeName);
                }

                FilterAttributeModel? filter = FilterAttributeReader.TryRead(attribute, compilation);

                if (filter is not null)
                {
                    filters.Add(filter);
                    continue;
                }

                if (!unsupportedFilterTypeNames.Contains(filterTypeName))
                {
                    unsupportedFilterTypeNames.Add(filterTypeName);
                }
            }
        }

        return (new EquatableArray<FilterAttributeModel>(filters.ToArray()), new EquatableArray<string>(unsupportedFilterTypeNames.ToArray()));
    }

    private static bool InheritsFrom(INamedTypeSymbol? typeSymbol, string fullyQualifiedBaseTypeName)
    {
        while (typeSymbol is not null)
        {
            if (string.Equals(typeSymbol.ToDisplayString(), fullyQualifiedBaseTypeName, StringComparison.Ordinal))
            {
                return true;
            }

            typeSymbol = typeSymbol.BaseType;
        }

        return false;
    }

    private static AttributeUsageMetadata GetAttributeUsage(INamedTypeSymbol attributeType)
    {
        bool inherited = true;
        bool allowMultiple = false;

        AttributeData? usage = attributeType.GetAttributes().FirstOrDefault(static attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), AttributeUsageAttributeName, StringComparison.Ordinal));

        if (usage is null)
        {
            return new AttributeUsageMetadata(inherited, allowMultiple);
        }

        foreach (KeyValuePair<string, TypedConstant> namedArgument in usage.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, nameof(AttributeUsageAttribute.Inherited), StringComparison.Ordinal)
                && namedArgument.Value.Value is bool inheritedValue)
            {
                inherited = inheritedValue;
            }
            else if (string.Equals(namedArgument.Key, nameof(AttributeUsageAttribute.AllowMultiple), StringComparison.Ordinal)
                && namedArgument.Value.Value is bool allowMultipleValue)
            {
                allowMultiple = allowMultipleValue;
            }
        }

        return new AttributeUsageMetadata(inherited, allowMultiple);
    }

    private static string? GetStringValue(TypedConstant constant)
    {
        return constant.Value as string;
    }

    private static string? FormatLiteral(TypedConstant constant)
    {
        return constant.Value switch
        {
            string value => SymbolDisplay.FormatLiteral(value, true),
            char value => SymbolDisplay.FormatLiteral(value, true),
            bool value => value ? "true" : "false",
            byte value => "(byte)" + value.ToString(CultureInfo.InvariantCulture),
            sbyte value => "(sbyte)" + value.ToString(CultureInfo.InvariantCulture),
            short value => "(short)" + value.ToString(CultureInfo.InvariantCulture),
            ushort value => "(ushort)" + value.ToString(CultureInfo.InvariantCulture),
            int value => value.ToString(CultureInfo.InvariantCulture),
            uint value => value.ToString(CultureInfo.InvariantCulture) + "U",
            long value => value.ToString(CultureInfo.InvariantCulture) + "L",
            ulong value => value.ToString(CultureInfo.InvariantCulture) + "UL",
            float value => value.ToString("R", CultureInfo.InvariantCulture) + "F",
            double value => value.ToString("R", CultureInfo.InvariantCulture) + "D",
            null => "null",
            _ => null,
        };
    }

    private static string? GetValueTypeFullName(TypedConstant constant)
    {
        return constant.Value switch
        {
            string => "string",
            char => "char",
            bool => "bool",
            byte => "byte",
            sbyte => "sbyte",
            short => "short",
            ushort => "ushort",
            int => "int",
            uint => "uint",
            long => "long",
            ulong => "ulong",
            float => "float",
            double => "double",
            null => "null",
            _ when constant.Type is not null && constant.Type.SpecialType != SpecialType.System_Object => constant.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            _ => null,
        };
    }

    private readonly struct AttributeUsageMetadata
    {
        public AttributeUsageMetadata(bool inherited, bool allowMultiple)
        {
            Inherited = inherited;
            AllowMultiple = allowMultiple;
        }

        public bool Inherited { get; }

        public bool AllowMultiple { get; }
    }
}
