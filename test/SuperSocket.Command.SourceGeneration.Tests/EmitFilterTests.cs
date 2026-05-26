using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace SuperSocket.Command.SourceGeneration.Tests;

public class EmitFilterTests
{
    private const string Prelude = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using SuperSocket.Command;
        using SuperSocket.ProtoBase;
        using SuperSocket.Server.Abstractions.Session;

        namespace Demo;

        public class FakePkg : IKeyedPackageInfo<string>
        {
            public string Key { get; set; } = "";
        }

        [CommandRegistry]
        public partial class Reg : IGeneratedCommandRegistry<string, FakePkg>
        {
        }

        public abstract class FilterBase : CommandFilterAttribute
        {
            public override bool OnCommandExecuting(CommandExecutingContext c) => true;
            public override void OnCommandExecuted(CommandExecutingContext c)
            {
            }
        }
        """;

    [Fact]
    public void EmitsParameterlessAttribute()
    {
        string source = Prelude + """

            public sealed class MarkerFilterAttribute : FilterBase
            {
            }

            [Command("X"), MarkerFilter]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, var diagnostics) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("new global::Demo.MarkerFilterAttribute()", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "SSC1003");
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsConstructorAndNamedArguments()
    {
        string source = Prelude + """

            public sealed class RoleFilterAttribute : FilterBase
            {
                public RoleFilterAttribute(string role)
                {
                    Role = role;
                }

                public string Role { get; }

                public int MaxRetries { get; set; }
            }

            [Command("X"), RoleFilter("admin", MaxRetries = 3, Order = 5)]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("new global::Demo.RoleFilterAttribute(\"admin\") { MaxRetries = 3, Order = 5 }", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsMultipleAttributesInDeclaredOrder()
    {
        string source = Prelude + """

            public sealed class AFilterAttribute : FilterBase
            {
            }

            public sealed class BFilterAttribute : FilterBase
            {
            }

            [Command("X"), AFilter, BFilter]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        int a = generatedSource.IndexOf("AFilterAttribute()", StringComparison.Ordinal);
        int b = generatedSource.IndexOf("BFilterAttribute()", StringComparison.Ordinal);
        Assert.True(a > 0 && b > a, $"Expected A before B; got a={a}, b={b}");
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsBaseClassFiltersBeforeOwnFilters()
    {
        string source = Prelude + """

            public sealed class BaseFilterAttribute : FilterBase
            {
            }

            public sealed class DerivedFilterAttribute : FilterBase
            {
            }

            [BaseFilter]
            public abstract class BaseCmd
            {
            }

            [Command("X"), DerivedFilter]
            public sealed class Cmd : BaseCmd, IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        int derived = generatedSource.IndexOf("DerivedFilterAttribute()", StringComparison.Ordinal);
        int baseFilter = generatedSource.IndexOf("BaseFilterAttribute()", StringComparison.Ordinal);
        Assert.True(derived > 0 && baseFilter > derived, $"Expected derived before base; got derived={derived}, base={baseFilter}");
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void SkipsInheritedBaseFilter_WhenAttributeUsageDisablesInheritance()
    {
        string source = Prelude + """

            [AttributeUsage(AttributeTargets.Class, Inherited = false)]
            public sealed class NonInheritedFilterAttribute : FilterBase
            {
            }

            [NonInheritedFilter]
            public abstract class BaseCmd
            {
            }

            [Command("X")]
            public sealed class Cmd : BaseCmd, IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.DoesNotContain("NonInheritedFilterAttribute", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void SkipsBaseDuplicateFilter_WhenAttributeUsageDisallowsMultiple()
    {
        string source = Prelude + """

            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
            public sealed class SingleFilterAttribute : FilterBase
            {
                public SingleFilterAttribute(string value)
                {
                    Value = value;
                }

                public string Value { get; }
            }

            [SingleFilter("base")]
            public abstract class BaseCmd
            {
            }

            [Command("X"), SingleFilter("derived")]
            public sealed class Cmd : BaseCmd, IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("new global::Demo.SingleFilterAttribute(\"derived\")", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new global::Demo.SingleFilterAttribute(\"base\")", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsEnumArgument()
    {
        string source = Prelude + """

            public enum Severity
            {
                Low,
                High,
            }

            public sealed class LevelFilterAttribute : FilterBase
            {
                public LevelFilterAttribute(Severity severity)
                {
                    Severity = severity;
                }

                public Severity Severity { get; }
            }

            [Command("X"), LevelFilter(Severity.High)]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("new global::Demo.LevelFilterAttribute((global::Demo.Severity)1)", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsTypeofArgument()
    {
        string source = Prelude + """

            public sealed class HandlerFilterAttribute : FilterBase
            {
                public HandlerFilterAttribute(System.Type handlerType)
                {
                    HandlerType = handlerType;
                }

                public System.Type HandlerType { get; }
            }

            public class TargetHandler
            {
            }

            [Command("X"), HandlerFilter(typeof(TargetHandler))]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("typeof(global::Demo.TargetHandler)", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsArrayArgument()
    {
        string source = Prelude + """

            public sealed class TagsFilterAttribute : FilterBase
            {
                public TagsFilterAttribute(params string[] tags)
                {
                    Tags = tags;
                }

                public string[] Tags { get; }
            }

            [Command("X"), TagsFilter("admin", "ops")]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("new global::System.String[] { \"admin\", \"ops\" }", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsNullArrayArgumentWithElementTypeCast()
    {
        string source = Prelude + """

            public sealed class NullTagsFilterAttribute : FilterBase
            {
                public NullTagsFilterAttribute(string[] tags)
                {
                    Tags = tags;
                }

                public NullTagsFilterAttribute(object value)
                {
                    Value = value;
                }

                public string[] Tags { get; }

                public object Value { get; }
            }

            [Command("X"), NullTagsFilter((string[])null)]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("new global::Demo.NullTagsFilterAttribute((global::System.String[])null)", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void EmitsFiltersForWrappedInterfacePackageCommand()
    {
        string source = Prelude + """

            public sealed class MarkerFilterAttribute : FilterBase
            {
            }

            [Command("X"), MarkerFilter]
            public sealed class Cmd : IAsyncCommand<IAppSession, IKeyedPackageInfo<string>>
            {
                public ValueTask ExecuteAsync(IAppSession s, IKeyedPackageInfo<string> p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("global::SuperSocket.Command.AsyncCommandWrap<", generatedSource, StringComparison.Ordinal);
        Assert.Contains("new global::Demo.MarkerFilterAttribute()", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void WarnsAndSkipsInaccessibleNestedFilter()
    {
        string source = Prelude + """

            public sealed class Holder
            {
                private sealed class HiddenFilterAttribute : FilterBase
                {
                }

                [Command("X"), HiddenFilter]
                public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
                {
                    public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
                }
            }
            """;

        (_, string generatedSource, var diagnostics) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "SSC1003");
        Assert.DoesNotContain("HiddenFilterAttribute", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    [Fact]
    public void StillEmitsArrayEmpty_WhenNoFilters()
    {
        string source = Prelude + """

            [Command("X")]
            public sealed class Cmd : IAsyncCommand<IAppSession, FakePkg>
            {
                public ValueTask ExecuteAsync(IAppSession s, FakePkg p, CancellationToken c) => default;
            }
            """;

        (_, string generatedSource, _) = GeneratorTestHarness.RunAndGetGenerated(source, "Demo.Reg.Registry.g.cs");

        Assert.Contains("global::System.Array.Empty<global::SuperSocket.Command.ICommandFilter>()", generatedSource, StringComparison.Ordinal);
        AssertNoCompilerErrors(source);
    }

    private static void AssertNoCompilerErrors(string source)
    {
        Diagnostic[] diagnostics = GeneratorTestHarness.RunAndGetCompilationDiagnostics(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
