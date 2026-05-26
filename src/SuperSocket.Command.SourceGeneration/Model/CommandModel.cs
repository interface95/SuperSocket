namespace SuperSocket.Command.SourceGeneration.Model;

internal sealed record CommandModel(
    string FullTypeName,
    string SimpleName,
    string SessionTypeFullName,
    string PackageTypeFullName,
    bool PackageTypeIsInterface,
    bool IsAsync,
    string? AttributeName,
    string? AttributeKeyLiteral,
    string? AttributeKeyTypeFullName,
    EquatableArray<FilterAttributeModel> Filters,
    EquatableArray<string> UnsupportedFilterTypeNames);
