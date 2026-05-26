namespace SuperSocket.Command.SourceGeneration.Model;

internal sealed record RegistryModel(
    string Namespace,
    string ClassName,
    string Accessibility,
    string KeyTypeFullName,
    string PackageInfoTypeFullName,
    EquatableArray<string> PackageInfoInterfaceTypeFullNames,
    bool IsKeyString,
    bool HasGetRegistrationsMethod,
    EquatableArray<ContainingTypeModel> ContainingTypes);

internal sealed record ContainingTypeModel(
    string Accessibility,
    string Modifiers,
    string TypeKeyword,
    string Name,
    string TypeParameterList,
    EquatableArray<string> ConstraintClauses);
