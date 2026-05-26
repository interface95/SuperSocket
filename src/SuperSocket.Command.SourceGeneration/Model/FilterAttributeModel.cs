namespace SuperSocket.Command.SourceGeneration.Model;

internal sealed record FilterAttributeModel(
    string TypeFullName,
    EquatableArray<FilterArgumentModel> ConstructorArguments,
    EquatableArray<FilterNamedArgumentModel> NamedArguments);

internal sealed record FilterNamedArgumentModel(string Name, FilterArgumentModel Value);
