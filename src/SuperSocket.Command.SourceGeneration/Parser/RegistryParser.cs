using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SuperSocket.Command.SourceGeneration.Model;

namespace SuperSocket.Command.SourceGeneration.Parser;

internal static class RegistryParser
{
    private const string CommandRegistryAttributeName = "SuperSocket.Command.CommandRegistryAttribute";
    private const string GeneratedCommandRegistryName = "IGeneratedCommandRegistry`2";
    private const string GeneratedCommandRegistryNamespace = "SuperSocket.Command";

    public static RegistryModel? TryParse(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        if (!HasCommandRegistryAttribute(classSymbol))
        {
            return null;
        }

        INamedTypeSymbol? registryInterface = FindGeneratedCommandRegistryInterface(classSymbol.AllInterfaces);

        if (registryInterface is null)
        {
            return null;
        }

        ITypeSymbol keyType = registryInterface.TypeArguments[0];
        ITypeSymbol packageInfoType = registryInterface.TypeArguments[1];

        return new RegistryModel(
            GetNamespace(classSymbol),
            classSymbol.Name,
            GetAccessibility(classSymbol),
            keyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            packageInfoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            GetInterfaceTypeFullNames(packageInfoType),
            keyType.SpecialType == SpecialType.System_String,
            HasGetRegistrationsMethod(classSymbol),
            GetContainingTypes(classSymbol, cancellationToken));
    }

    private static bool HasCommandRegistryAttribute(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetAttributes().Any(static attribute =>
            string.Equals(
                attribute.AttributeClass?.ToDisplayString(),
                CommandRegistryAttributeName,
                StringComparison.Ordinal));
    }

    private static INamedTypeSymbol? FindGeneratedCommandRegistryInterface(ImmutableArray<INamedTypeSymbol> interfaces)
    {
        foreach (INamedTypeSymbol interfaceSymbol in interfaces)
        {
            INamedTypeSymbol originalDefinition = interfaceSymbol.OriginalDefinition;

            if (string.Equals(originalDefinition.MetadataName, GeneratedCommandRegistryName, StringComparison.Ordinal)
                && string.Equals(originalDefinition.ContainingNamespace.ToDisplayString(), GeneratedCommandRegistryNamespace, StringComparison.Ordinal))
            {
                return interfaceSymbol;
            }
        }

        return null;
    }

    private static EquatableArray<string> GetInterfaceTypeFullNames(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return Array.Empty<string>();
        }

        return new EquatableArray<string>(namedTypeSymbol.AllInterfaces.Select(static interfaceSymbol =>
            interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToArray());
    }

    private static bool HasGetRegistrationsMethod(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetMembers("GetRegistrations")
            .OfType<IMethodSymbol>()
            .Any(static method => !method.IsStatic && method.Parameters.Length == 0);
    }

    private static string GetNamespace(INamedTypeSymbol classSymbol)
    {
        INamespaceSymbol containingNamespace = classSymbol.ContainingNamespace;

        return containingNamespace.IsGlobalNamespace
            ? string.Empty
            : containingNamespace.ToDisplayString();
    }

    private static string GetAccessibility(INamedTypeSymbol classSymbol)
    {
        return classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal",
        };
    }

    private static EquatableArray<ContainingTypeModel> GetContainingTypes(INamedTypeSymbol classSymbol, CancellationToken cancellationToken)
    {
        if (classSymbol.ContainingType is null)
        {
            return Array.Empty<ContainingTypeModel>();
        }

        var containingTypes = new Stack<ContainingTypeModel>();
        INamedTypeSymbol? containingType = classSymbol.ContainingType;

        while (containingType is not null)
        {
            ContainingTypeModel? containingTypeModel = TryCreateContainingTypeModel(containingType, cancellationToken);

            if (containingTypeModel is null)
            {
                return Array.Empty<ContainingTypeModel>();
            }

            containingTypes.Push(containingTypeModel);
            containingType = containingType.ContainingType;
        }

        return containingTypes.ToArray();
    }

    private static ContainingTypeModel? TryCreateContainingTypeModel(INamedTypeSymbol containingType, CancellationToken cancellationToken)
    {
        TypeDeclarationSyntax? declarationSyntax = containingType.DeclaringSyntaxReferences.Length == 0
            ? null
            : containingType.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as TypeDeclarationSyntax;

        if (declarationSyntax is null || GetTypeKeyword(containingType) is not string typeKeyword)
        {
            return null;
        }

        return new ContainingTypeModel(
            GetAccessibility(containingType),
            GetNonAccessibilityModifiers(declarationSyntax.Modifiers),
            typeKeyword,
            declarationSyntax.Identifier.Text,
            GetTypeParameterList(containingType),
            GetConstraintClauses(containingType));
    }

    private static string? GetTypeKeyword(INamedTypeSymbol containingType)
    {
        if (containingType.IsRecord)
        {
            return containingType.TypeKind switch
            {
                TypeKind.Class => "record class",
                TypeKind.Struct => "record struct",
                _ => null,
            };
        }

        return containingType.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => null,
        };
    }

    private static string GetNonAccessibilityModifiers(SyntaxTokenList modifiers)
    {
        return string.Join(
            " ",
            modifiers
                .Where(static modifier => !IsAccessibilityOrPartialModifier(modifier))
                .Select(static modifier => modifier.Text));
    }

    private static bool IsAccessibilityOrPartialModifier(SyntaxToken modifier)
    {
        return modifier.IsKind(SyntaxKind.PublicKeyword)
            || modifier.IsKind(SyntaxKind.InternalKeyword)
            || modifier.IsKind(SyntaxKind.ProtectedKeyword)
            || modifier.IsKind(SyntaxKind.PrivateKeyword)
            || modifier.IsKind(SyntaxKind.PartialKeyword);
    }

    private static string GetTypeParameterList(INamedTypeSymbol containingType)
    {
        if (containingType.TypeParameters.Length == 0)
        {
            return string.Empty;
        }

        return "<" + string.Join(", ", containingType.TypeParameters.Select(GetTypeParameterDeclaration)) + ">";
    }

    private static string GetTypeParameterDeclaration(ITypeParameterSymbol typeParameter)
    {
        return typeParameter.Variance switch
        {
            VarianceKind.Out => "out " + EscapeIdentifier(typeParameter.Name),
            VarianceKind.In => "in " + EscapeIdentifier(typeParameter.Name),
            _ => EscapeIdentifier(typeParameter.Name),
        };
    }

    private static EquatableArray<string> GetConstraintClauses(INamedTypeSymbol containingType)
    {
        if (containingType.TypeParameters.Length == 0)
        {
            return Array.Empty<string>();
        }

        var constraints = new List<string>();

        foreach (ITypeParameterSymbol typeParameter in containingType.TypeParameters)
        {
            string constraintClause = GetConstraintClause(typeParameter);

            if (!string.IsNullOrWhiteSpace(constraintClause))
            {
                constraints.Add(constraintClause);
            }
        }

        return new EquatableArray<string>(constraints.ToArray());
    }

    private static string GetConstraintClause(ITypeParameterSymbol typeParameter)
    {
        var constraints = new List<string>();

        if (typeParameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (typeParameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (typeParameter.HasReferenceTypeConstraint)
        {
            constraints.Add("class");
        }

        if (typeParameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        constraints.AddRange(typeParameter.ConstraintTypes.Select(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

        if (typeParameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count == 0
            ? string.Empty
            : $"where {EscapeIdentifier(typeParameter.Name)} : {string.Join(", ", constraints)}";
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;
    }
}
