using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SuperSocket.Command.SourceGeneration.Emit;
using SuperSocket.Command.SourceGeneration.Model;
using SuperSocket.Command.SourceGeneration.Parser;
using System.Collections.Immutable;
using System.Text;

namespace SuperSocket.Command.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public sealed class CommandRegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "__SuperSocketCommandGenerator_Marker.g.cs",
                "// SuperSocket.Command.SourceGeneration loaded\n");
        });

        IncrementalValuesProvider<RegistryModel> registries = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsCandidateRegistryDeclaration(node),
                static (syntaxContext, cancellationToken) => RegistryParser.TryParse(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        IncrementalValuesProvider<CommandModel> commands = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsCandidateCommandDeclaration(node),
                static (syntaxContext, cancellationToken) => CommandParser.TryParse(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        IncrementalValuesProvider<(RegistryModel Registry, ImmutableArray<CommandModel> Commands)> registriesWithCommands = registries.Combine(commands.Collect());

        context.RegisterSourceOutput(registriesWithCommands, static (sourceProductionContext, model) =>
        {
            sourceProductionContext.AddSource(GetHintName(model.Registry), SourceText.From(RegistryEmitter.Emit(model.Registry, model.Commands, sourceProductionContext), Encoding.UTF8));
        });
    }

    private static bool IsCandidateRegistryDeclaration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration
            && classDeclaration.BaseList is not null
            && classDeclaration.AttributeLists.Count > 0
            && classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static bool IsCandidateCommandDeclaration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration
            && classDeclaration.BaseList is not null
            && !classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword);
    }

    private static string GetHintName(RegistryModel model)
    {
        string qualifiedClassName = string.IsNullOrWhiteSpace(model.Namespace)
            ? model.ClassName
            : $"{model.Namespace}.{model.ClassName}";

        return $"{qualifiedClassName}.Registry.g.cs";
    }

}
