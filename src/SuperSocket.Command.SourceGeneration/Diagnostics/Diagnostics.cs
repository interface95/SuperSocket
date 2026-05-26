using Microsoft.CodeAnalysis;

namespace SuperSocket.Command.SourceGeneration.Diagnostics;

internal static class CommandDiagnostics
{
    public static readonly DiagnosticDescriptor MissingKeyForNonStringKey = new(
        "SSC1001",
        "Command requires an explicit key",
        "Command '{0}' matches a registry with non-string key type '{1}' but does not declare an explicit key",
        "SuperSocket.Command.SourceGeneration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateKey = new(
        "SSC1002",
        "Duplicate command key",
        "Command '{1}' duplicates key '{2}' already used by command '{0}' in this registry",
        "SuperSocket.Command.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandFiltersIgnored = new(
        "SSC1003",
        "Command filter attribute could not be source-generated",
        "Command '{0}' is annotated with filter attribute '{1}', which the SuperSocket source generator cannot reconstruct at compile time; the filter will be skipped under UseGeneratedCommand. Either simplify the attribute's constructor/property arguments to compile-time constants, or register the filter via CommandOptions.AddGlobalCommandFilter<T>().",
        "SuperSocket.Command.SourceGeneration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidCommandKeyType = new(
        "SSC1004",
        "Command key type does not match registry key type",
        "Command '{0}' declares a key of type '{1}', which cannot be assigned to registry key type '{2}'",
        "SuperSocket.Command.SourceGeneration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExistingGetRegistrations = new(
        "SSC1005",
        "Registry already declares GetRegistrations",
        "Registry '{0}' already declares GetRegistrations(), so the generated registry implementation was skipped",
        "SuperSocket.Command.SourceGeneration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
