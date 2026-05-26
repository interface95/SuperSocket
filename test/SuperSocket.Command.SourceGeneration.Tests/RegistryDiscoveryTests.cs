using System;
using System.Linq;
using Xunit;

namespace SuperSocket.Command.SourceGeneration.Tests;

public class RegistryDiscoveryTests
{
    [Fact]
    public void Generator_DiscoversAttributedPartialRegistryClass()
    {
        var result = GeneratorTestHarness.Run("""
            using SuperSocket.Command;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            [CommandRegistry]
            public partial class MyCommands : IGeneratedCommandRegistry<string, FakePkg>
            {
            }
            """);

        Assert.Contains(
            result.GeneratedTrees,
            static tree => tree.FilePath.EndsWith("Demo.MyCommands.Registry.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_SkipsAttributedNonPartialRegistryClass()
    {
        var result = GeneratorTestHarness.Run("""
            using System;
            using System.Collections.Generic;
            using SuperSocket.Command;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            [CommandRegistry]
            public class MyCommands : IGeneratedCommandRegistry<string, FakePkg>
            {
                public IEnumerable<CommandRegistration<string, FakePkg>> GetRegistrations()
                    => throw new NotImplementedException();
            }
            """);

        Assert.DoesNotContain(
            result.GeneratedTrees,
            static tree => tree.FilePath.EndsWith("Demo.MyCommands.Registry.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_SkipsPartialRegistryClassWithoutCommandRegistryAttribute()
    {
        var result = GeneratorTestHarness.Run("""
            using System;
            using System.Collections.Generic;
            using SuperSocket.Command;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            public partial class MyCommands : IGeneratedCommandRegistry<string, FakePkg>
            {
                public IEnumerable<CommandRegistration<string, FakePkg>> GetRegistrations()
                    => throw new NotImplementedException();
            }
            """);

        Assert.DoesNotContain(
            result.GeneratedTrees,
            static tree => tree.FilePath.EndsWith("Demo.MyCommands.Registry.g.cs", StringComparison.Ordinal));
    }
}
