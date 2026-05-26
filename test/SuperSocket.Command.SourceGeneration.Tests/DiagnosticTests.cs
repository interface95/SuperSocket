using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace SuperSocket.Command.SourceGeneration.Tests;

public class DiagnosticTests
{
    [Fact]
    public void Generator_ReportsMissingKeyForNonStringRegistryKey()
    {
        var result = GeneratorTestHarness.Run("""
            using SuperSocket.Command;
            using SuperSocket.Server.Abstractions.Session;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            [Command("ADD")]
            public class AddCmd : ICommand<IAppSession, FakePkg>
            {
                public void Execute(IAppSession session, FakePkg package)
                {
                }
            }

            [CommandRegistry]
            public partial class Reg : IGeneratedCommandRegistry<int, FakePkg>
            {
            }
            """);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Id == "SSC1001");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Demo.AddCmd", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("int", diagnostic.GetMessage(), StringComparison.Ordinal);

        string generatedSource = result.GeneratedTrees
            .Single(static tree => tree.FilePath.EndsWith("Demo.Reg.Registry.g.cs", StringComparison.Ordinal))
            .GetText(TestContext.Current.CancellationToken)
            .ToString();

        Assert.DoesNotContain("global::Demo.AddCmd", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_ReportsDuplicateKeysAndSkipsDuplicateRegistration()
    {
        var result = GeneratorTestHarness.Run("""
            using SuperSocket.Command;
            using SuperSocket.Server.Abstractions.Session;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            [Command("FIRST", "DUP")]
            public class FirstCmd : ICommand<IAppSession, FakePkg>
            {
                public void Execute(IAppSession session, FakePkg package)
                {
                }
            }

            [Command("SECOND", "DUP")]
            public class SecondCmd : ICommand<IAppSession, FakePkg>
            {
                public void Execute(IAppSession session, FakePkg package)
                {
                }
            }

            [CommandRegistry]
            public partial class Reg : IGeneratedCommandRegistry<string, FakePkg>
            {
            }
            """);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Id == "SSC1002");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Demo.FirstCmd", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Demo.SecondCmd", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("DUP", diagnostic.GetMessage(), StringComparison.Ordinal);

        string generatedSource = result.GeneratedTrees
            .Single(static tree => tree.FilePath.EndsWith("Demo.Reg.Registry.g.cs", StringComparison.Ordinal))
            .GetText(TestContext.Current.CancellationToken)
            .ToString();

        Assert.Contains("global::Demo.FirstCmd", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Demo.SecondCmd", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_EmitsReconstructibleCommandFilters()
    {
        var result = GeneratorTestHarness.Run("""
            using SuperSocket.Command;
            using SuperSocket.Server.Abstractions.Session;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            public sealed class AuditFilterAttribute : CommandFilterAttribute
            {
                public override bool OnCommandExecuting(CommandExecutingContext commandContext)
                    => true;

                public override void OnCommandExecuted(CommandExecutingContext commandContext)
                {
                }
            }

            [AuditFilter]
            [Command("ADD")]
            public class AddCmd : ICommand<IAppSession, FakePkg>
            {
                public void Execute(IAppSession session, FakePkg package)
                {
                }
            }

            [CommandRegistry]
            public partial class Reg : IGeneratedCommandRegistry<string, FakePkg>
            {
            }
            """);

        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Id == "SSC1003");

        string generatedSource = result.GeneratedTrees
            .Single(static tree => tree.FilePath.EndsWith("Demo.Reg.Registry.g.cs", StringComparison.Ordinal))
            .GetText(TestContext.Current.CancellationToken)
            .ToString();

        Assert.Contains("new global::Demo.AuditFilterAttribute()", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_ReportsInvalidExplicitCommandKeyTypeAndSkipsRegistration()
    {
        Diagnostic[] diagnostics = GeneratorTestHarness.RunAndGetCompilationDiagnostics("""
            using SuperSocket.Command;
            using SuperSocket.Server.Abstractions.Session;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            [Command("ONE", (byte)1)]
            public class OneCmd : ICommand<IAppSession, FakePkg>
            {
                public void Execute(IAppSession session, FakePkg package)
                {
                }
            }

            [CommandRegistry]
            public partial class Reg : IGeneratedCommandRegistry<string, FakePkg>
            {
            }
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "SSC1004");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Demo.OneCmd", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("byte", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("string", diagnostic.GetMessage(), StringComparison.Ordinal);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "CS0029");
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generator_ReportsExistingGetRegistrationsAndDoesNotEmitDuplicateMember()
    {
        Diagnostic[] diagnostics = GeneratorTestHarness.RunAndGetCompilationDiagnostics("""
            using System.Collections.Generic;
            using SuperSocket.Command;

            namespace Demo;

            public sealed class FakePkg
            {
            }

            [CommandRegistry]
            public partial class Reg : IGeneratedCommandRegistry<string, FakePkg>
            {
                public IEnumerable<CommandRegistration<string, FakePkg>> GetRegistrations()
                    => [];
            }
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "SSC1005");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Demo.Reg", diagnostic.GetMessage(), StringComparison.Ordinal);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "CS0111");
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
