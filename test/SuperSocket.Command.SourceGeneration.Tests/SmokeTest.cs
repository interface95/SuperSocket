using System;
using Xunit;

namespace SuperSocket.Command.SourceGeneration.Tests;

public class SmokeTest
{
    [Fact]
    public void Generator_EmitsMarkerFile()
    {
        var result = GeneratorTestHarness.Run("namespace SuperSocket.Command.SourceGeneration.Tests.Input;");

        Assert.Contains(
            result.GeneratedTrees,
            static tree => tree.FilePath.EndsWith("__SuperSocketCommandGenerator_Marker.g.cs", StringComparison.Ordinal));
    }
}
