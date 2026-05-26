using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SuperSocket.Command.SourceGeneration.Model;

namespace SuperSocket.Command.SourceGeneration.Parser;

internal static class FilterAttributeReader
{
    public static FilterAttributeModel? TryRead(AttributeData attribute, Compilation compilation)
    {
        if (attribute.AttributeClass is null || !IsAccessibleFromGeneratedRegistry(attribute.AttributeClass, compilation))
        {
            return null;
        }

        var constructorArguments = new List<FilterArgumentModel>(attribute.ConstructorArguments.Length);

        foreach (TypedConstant argument in attribute.ConstructorArguments)
        {
            string? expression = TryFormatArgument(argument);

            if (expression is null)
            {
                return null;
            }

            constructorArguments.Add(new FilterArgumentModel(expression));
        }

        var namedArguments = new List<FilterNamedArgumentModel>(attribute.NamedArguments.Length);

        foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
        {
            string? expression = TryFormatArgument(namedArgument.Value);

            if (expression is null)
            {
                return null;
            }

            namedArguments.Add(new FilterNamedArgumentModel(namedArgument.Key, new FilterArgumentModel(expression)));
        }

        return new FilterAttributeModel(
            attribute.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            new EquatableArray<FilterArgumentModel>(constructorArguments.ToArray()),
            new EquatableArray<FilterNamedArgumentModel>(namedArguments.ToArray()));
    }

    private static bool IsAccessibleFromGeneratedRegistry(INamedTypeSymbol attributeType, Compilation compilation)
    {
        return compilation.IsSymbolAccessibleWithin(attributeType, compilation.Assembly);
    }

    private static string? TryFormatArgument(TypedConstant constant)
    {
        return constant.Kind switch
        {
            TypedConstantKind.Primitive => FormatPrimitive(constant.Value),
            TypedConstantKind.Enum => FormatEnum(constant),
            TypedConstantKind.Type => FormatType(constant.Value as ITypeSymbol),
            TypedConstantKind.Array => FormatArray(constant),
            _ => null,
        };
    }

    private static string? FormatPrimitive(object? value)
    {
        return value switch
        {
            null => "null",
            string stringValue => SymbolDisplay.FormatLiteral(stringValue, true),
            char charValue => SymbolDisplay.FormatLiteral(charValue, true),
            bool boolValue => boolValue ? "true" : "false",
            byte byteValue => "(byte)" + byteValue.ToString(CultureInfo.InvariantCulture),
            sbyte sbyteValue => "(sbyte)" + sbyteValue.ToString(CultureInfo.InvariantCulture),
            short shortValue => "(short)" + shortValue.ToString(CultureInfo.InvariantCulture),
            ushort ushortValue => "(ushort)" + ushortValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            uint uintValue => uintValue.ToString(CultureInfo.InvariantCulture) + "U",
            long longValue => longValue.ToString(CultureInfo.InvariantCulture) + "L",
            ulong ulongValue => ulongValue.ToString(CultureInfo.InvariantCulture) + "UL",
            float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture) + "F",
            double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture) + "D",
            _ => null,
        };
    }

    private static string? FormatEnum(TypedConstant constant)
    {
        if (constant.Type is not INamedTypeSymbol enumType)
        {
            return null;
        }

        string? underlyingValue = FormatPrimitive(constant.Value);

        return underlyingValue is null
            ? null
            : $"({enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){underlyingValue}";
    }

    private static string? FormatType(ITypeSymbol? type)
    {
        return type is null
            ? null
            : "typeof(" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")";
    }

    private static string? FormatArray(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return constant.Type is IArrayTypeSymbol nullArrayType
                ? $"({GetArrayElementTypeName(nullArrayType.ElementType)}[])null"
                : null;
        }

        if (constant.Type is not IArrayTypeSymbol arrayType)
        {
            return null;
        }

        var elements = new List<string>(constant.Values.Length);

        foreach (TypedConstant value in constant.Values)
        {
            string? expression = TryFormatArgument(value);

            if (expression is null)
            {
                return null;
            }

            elements.Add(expression);
        }

        return $"new {GetArrayElementTypeName(arrayType.ElementType)}[] {{ {string.Join(", ", elements)} }}";
    }

    private static string GetArrayElementTypeName(ITypeSymbol elementType)
    {
        return elementType.SpecialType switch
        {
            SpecialType.System_String => "global::System.String",
            SpecialType.System_Char => "global::System.Char",
            SpecialType.System_Boolean => "global::System.Boolean",
            SpecialType.System_Byte => "global::System.Byte",
            SpecialType.System_SByte => "global::System.SByte",
            SpecialType.System_Int16 => "global::System.Int16",
            SpecialType.System_UInt16 => "global::System.UInt16",
            SpecialType.System_Int32 => "global::System.Int32",
            SpecialType.System_UInt32 => "global::System.UInt32",
            SpecialType.System_Int64 => "global::System.Int64",
            SpecialType.System_UInt64 => "global::System.UInt64",
            SpecialType.System_Single => "global::System.Single",
            SpecialType.System_Double => "global::System.Double",
            _ => elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        };
    }
}
