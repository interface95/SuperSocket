# SuperSocket.Command AOT Source Generator

`SuperSocket.Command.SourceGeneration` provides a Roslyn incremental source generator for applications that need command dispatch without reflection-heavy discovery. It is intended for Native AOT and trimming-sensitive applications that already know their command set at compile time.

## Goal

The legacy command APIs can discover command types from assemblies and attributes at runtime. That path is convenient for JIT applications, but it relies on reflection and dynamic code patterns that are not suitable for Native AOT.

The generated command path moves command discovery to compile time:

- command classes are discovered by the source generator;
- a partial registry class receives a generated `GetRegistrations()` implementation;
- `UseGeneratedCommand<TKey, TPackageInfo>()` consumes only generated registrations;
- no command assembly scanning is required on the dispatch path.

## Quick start

### 1. Define commands

Commands continue to implement the regular command interfaces:

```csharp
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions.Session;

[Command("add")]
public sealed class AddCommand : IAsyncCommand<IAppSession, StringPackageInfo>
{
    public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package, CancellationToken cancellationToken)
    {
        int result = package.Parameters.Select(int.Parse).Sum();
        await session.SendAsync(Encoding.UTF8.GetBytes(result.ToString() + "\r\n"), cancellationToken);
    }
}
```

### 2. Declare a generated registry

Create a partial class, mark it with `[CommandRegistry]`, and implement `IGeneratedCommandRegistry<TKey, TPackageInfo>`:

```csharp
using SuperSocket.Command;
using SuperSocket.ProtoBase;

[CommandRegistry]
public partial class AppCommands : IGeneratedCommandRegistry<string, StringPackageInfo>
{
}
```

The generator emits the `GetRegistrations()` implementation for this partial class.

### 3. Register the registry and generated-only middleware

Register the generated registry in DI and use `UseGeneratedCommand<TKey, TPackageInfo>()`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Host;

var host = SuperSocketHostBuilder.Create<StringPackageInfo, CommandLinePipelineFilter>()
    .UseGeneratedCommand<string, StringPackageInfo>()
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<IGeneratedCommandRegistry<string, StringPackageInfo>, AppCommands>();
    })
    .Build();

await host.RunAsync();
```

### 4. Publish with Native AOT

Enable Native AOT in the application project:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

The repository includes `samples/CommandServer.Aot`, which demonstrates a full source-generated command server that builds and publishes with Native AOT.

## Analyzer reference in source builds

When using the repository source projects directly, reference the generator project as an analyzer:

```xml
<ProjectReference Include="../../src/SuperSocket.Command.SourceGeneration/SuperSocket.Command.SourceGeneration.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Package consumers should get the analyzer through the packaged command/source-generation assets for the release they reference.

## Diagnostics

The generator reports diagnostics for invalid generated registrations:

- `SSC1001`: a command requires an explicit key for a non-string registry key type.
- `SSC1002`: duplicate command keys were discovered for the same generated registry.

## Limitations

- `AddCommandAssembly(Assembly)` is reflection-based and is not AOT-friendly.
- Legacy attribute or assembly scanning remains available for existing JIT applications, but it is annotated with trimming and dynamic-code warnings.
- `UseCommand<TKey, TPackageInfo>(...)` is still the mixed legacy middleware path. For an AOT proof, use `UseGeneratedCommand<TKey, TPackageInfo>()`.
- The source generator intentionally discovers commands at compile time. Commands that are loaded only dynamically at runtime must stay on the legacy reflection path.
