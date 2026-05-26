using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SuperSocket.Command.SourceGeneration.Parser;
using Xunit;

namespace SuperSocket.Command.SourceGeneration.Tests;

public class FilterAttributeReaderTests
{
    [Fact]
    public void TryRead_ReturnsNull_WhenInternalAttributeFromReferenceIsNotAccessible()
    {
        CSharpCompilation filterLibrary = CreateCompilation(
            "FilterLibrary",
            """
            using SuperSocket.Command;
            using SuperSocket.Server.Abstractions.Session;

            namespace FilterLibrary;

            internal sealed class InternalFilterAttribute : CommandFilterAttribute
            {
                public override bool OnCommandExecuting(CommandExecutingContext c) => true;
                public override void OnCommandExecuted(CommandExecutingContext c)
                {
                }
            }

            [InternalFilter]
            public sealed class CommandInReference
            {
            }
            """);

        CSharpCompilation referencingCompilation = CreateCompilation(
            "ReferencingAssembly",
            "namespace ReferencingAssembly; public sealed class Marker { }",
            [filterLibrary.ToMetadataReference()]);

        INamedTypeSymbol commandType = referencingCompilation.GetTypeByMetadataName("FilterLibrary.CommandInReference")!;
        AttributeData filterAttribute = commandType.GetAttributes().Single();

        Assert.Null(FilterAttributeReader.TryRead(filterAttribute, referencingCompilation));
    }

    [Fact]
    public void TryRead_ReturnsFilter_WhenInternalAttributeFromReferenceIsVisibleThroughInternalsVisibleTo()
    {
        CSharpCompilation filterLibrary = CreateCompilation(
            "FilterLibrary",
            """
            using System.Runtime.CompilerServices;
            using SuperSocket.Command;
            using SuperSocket.Server.Abstractions.Session;

            [assembly: InternalsVisibleTo("ReferencingAssembly")]

            namespace FilterLibrary;

            internal sealed class InternalFilterAttribute : CommandFilterAttribute
            {
                public override bool OnCommandExecuting(CommandExecutingContext c) => true;
                public override void OnCommandExecuted(CommandExecutingContext c)
                {
                }
            }

            [InternalFilter]
            public sealed class CommandInReference
            {
            }
            """);

        CSharpCompilation referencingCompilation = CreateCompilation(
            "ReferencingAssembly",
            "namespace ReferencingAssembly; public sealed class Marker { }",
            [filterLibrary.ToMetadataReference()]);

        INamedTypeSymbol commandType = referencingCompilation.GetTypeByMetadataName("FilterLibrary.CommandInReference")!;
        AttributeData filterAttribute = commandType.GetAttributes().Single();

        Assert.NotNull(FilterAttributeReader.TryRead(filterAttribute, referencingCompilation));
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, string source, MetadataReference[]? additionalReferences = null)
    {
        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static location => MetadataReference.CreateFromFile(location))
            .Concat(additionalReferences ?? [])
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
