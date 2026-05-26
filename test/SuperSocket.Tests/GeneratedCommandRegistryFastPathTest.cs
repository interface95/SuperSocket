using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperSocket;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using Xunit;

namespace SuperSocket.Tests
{
    [Trait("Category", "Command")]
    public class GeneratedCommandRegistryFastPathTest : TestClassBase
    {
        public GeneratedCommandRegistryFastPathTest(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [Fact]
        public async Task TestHandwrittenRegistryFastPath()
        {
            var hostConfigurator = new RegularHostConfigurator();
            using (var server = CreateSocketServerBuilder<StringPackageInfo, CommandLinePipelineFilter>(hostConfigurator)
                .UseCommand(commandOptions =>
                {
                    commandOptions.AddCommand<LegacyAdd>();
                    commandOptions.RegisterUnknownPackageHandler<StringPackageInfo>(async (session, package, cancellationToken) =>
                    {
                        await session.SendAsync(Encoding.UTF8.GetBytes("X\r\n"));
                    });
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IGeneratedCommandRegistry<string, StringPackageInfo>, HandwrittenRegistry>();
                })
                .BuildAsServer())
            {
                Assert.Equal("TestServer", server.Name);

                Assert.True(await server.StartAsync());
                OutputHelper.WriteLine("Server started.");

                var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await client.ConnectAsync(hostConfigurator.GetServerEndPoint());
                OutputHelper.WriteLine("Connected.");

                using (var stream = await hostConfigurator.GetClientStream(client))
                using (var streamReader = new StreamReader(stream, Utf8Encoding, true))
                using (var streamWriter = new StreamWriter(stream, Utf8Encoding, 1024 * 1024 * 4))
                {
                    await streamWriter.WriteAsync("ADD 1 2 3\r\n");
                    await streamWriter.FlushAsync();
                    var line = await streamReader.ReadLineAsync();
                    Assert.Equal("6", line);
                }

                await server.StopAsync();
            }
        }

        [Fact]
        public async Task TestGeneratedCommandMiddlewareUsesHandwrittenRegistry()
        {
            var hostConfigurator = new RegularHostConfigurator();
            using (var server = CreateSocketServerBuilder<StringPackageInfo, CommandLinePipelineFilter>(hostConfigurator)
                .UseGeneratedCommand<string, StringPackageInfo>()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IGeneratedCommandRegistry<string, StringPackageInfo>, HandwrittenRegistry>();
                })
                .BuildAsServer())
            {
                Assert.Equal("TestServer", server.Name);

                Assert.True(await server.StartAsync());
                OutputHelper.WriteLine("Server started.");

                var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await client.ConnectAsync(hostConfigurator.GetServerEndPoint());
                OutputHelper.WriteLine("Connected.");

                using (var stream = await hostConfigurator.GetClientStream(client))
                using (var streamReader = new StreamReader(stream, Utf8Encoding, true))
                using (var streamWriter = new StreamWriter(stream, Utf8Encoding, 1024 * 1024 * 4))
                {
                    await streamWriter.WriteAsync("ADD 1 2 3\r\n");
                    await streamWriter.FlushAsync();
                    var line = await streamReader.ReadLineAsync();
                    Assert.Equal("6", line);
                }

                await server.StopAsync();
            }
        }

        [Fact]
        public void TestGeneratedCommandMiddlewareRequiresRegistry()
        {
            var services = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new GeneratedCommandMiddleware<string, StringPackageInfo>(
                    services,
                    Options.Create(new CommandOptions()),
                    Array.Empty<IGeneratedCommandRegistry<string, StringPackageInfo>>(),
                    null,
                    services.GetRequiredService<ILoggerFactory>()));

            Assert.Contains("No generated command registry is registered for", exception.Message);
        }

        [Command("ADD")]
        private sealed class LegacyAdd : IAsyncCommand<StringPackageInfo>
        {
            public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package, System.Threading.CancellationToken cancellationToken)
            {
                await session.SendAsync(Encoding.UTF8.GetBytes("legacy\r\n"));
            }
        }

        private sealed class HandwrittenRegistry : IGeneratedCommandRegistry<string, StringPackageInfo>
        {
            public IEnumerable<CommandRegistration<string, StringPackageInfo>> GetRegistrations()
            {
                yield return new CommandRegistration<string, StringPackageInfo>(
                    "ADD",
                    typeof(ADD),
                    (sp, commandOptions) => new TypedCommandSet<string, IAppSession, StringPackageInfo>(
                        "ADD",
                        new CommandMetadata("ADD", "ADD"),
                        ActivatorUtilities.CreateInstance<ADD>(sp),
                        Array.Empty<ICommandFilter>()));
            }
        }
    }
}
