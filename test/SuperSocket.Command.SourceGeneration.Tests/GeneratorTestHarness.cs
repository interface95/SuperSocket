using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SuperSocket.Command;
using SuperSocket.Command.SourceGeneration;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions.Session;

namespace SuperSocket.Command.SourceGeneration.Tests;

internal static class GeneratorTestHarness
{
    public static GeneratorDriverRunResult Run(string sourceCode)
    {
        CSharpCompilation compilation = CreateCompilation(sourceCode);
        GeneratorDriver driver = CreateDriver();

        driver = driver.RunGenerators(compilation);

        return driver.GetRunResult();
    }

    public static Diagnostic[] RunAndGetCompilationDiagnostics(string sourceCode)
    {
        CSharpCompilation compilation = CreateCompilation(sourceCode);
        GeneratorDriver driver = CreateDriver();

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> generatorDiagnostics);

        return outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .ToArray();
    }

    public static (CSharpCompilation Compilation, string GeneratedText, ImmutableArray<Diagnostic> Diagnostics) RunAndGetGenerated(string sourceCode, string expectedFileSuffix)
    {
        CSharpCompilation compilation = CreateCompilation(sourceCode);
        GeneratorDriver driver = CreateDriver();

        driver = driver.RunGenerators(compilation);
        GeneratorDriverRunResult runResult = driver.GetRunResult();
        SyntaxTree? generatedTree = runResult.GeneratedTrees.FirstOrDefault(tree => tree.FilePath.EndsWith(expectedFileSuffix, StringComparison.Ordinal));

        if (generatedTree is null)
        {
            throw new Xunit.Sdk.XunitException($"No generated file matching '{expectedFileSuffix}'. Generated: {string.Join(", ", runResult.GeneratedTrees.Select(static tree => tree.FilePath))}");
        }

        return (compilation, generatedTree.ToString(), runResult.Diagnostics);
    }

    private static CSharpCompilation CreateCompilation(string sourceCode)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            sourceCode,
            new CSharpParseOptions(LanguageVersion.CSharp12));

        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => assembly.Location)
            .Concat([
                typeof(IGeneratedCommandRegistry<,>).Assembly.Location,
                typeof(IKeyedPackageInfo<>).Assembly.Location,
                typeof(IAppSession).Assembly.Location,
                typeof(ActivatorUtilities).Assembly.Location,
                typeof(IServiceProvider).Assembly.Location,
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static location => MetadataReference.CreateFromFile(location))
            .ToArray();

        return CSharpCompilation.Create(
            "SuperSocketCommandGeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorDriver CreateDriver()
    {
        IIncrementalGenerator generator = new CommandRegistryGenerator();
        return CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
    }
}
