using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Host;
using SuperSocket.Tests.Command;
using Xunit;

namespace SuperSocket.Tests
{
    [Trait("Category", "Command")]
    public class GeneratedCommandRegistryEndToEndTest : TestClassBase
    {
        public GeneratedCommandRegistryEndToEndTest(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [Theory]
        [InlineData(typeof(RegularHostConfigurator))]
        public async Task GeneratedRegistry_ExecutesCommands(Type hostConfiguratorType)
        {
            var hostConfigurator = CreateObject<IHostConfigurator>(hostConfiguratorType);
            using (var server = CreateSocketServerBuilder<StringPackageInfo, CommandLinePipelineFilter>(hostConfigurator)
                .UseGeneratedCommand<string, StringPackageInfo>()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IGeneratedCommandRegistry<string, StringPackageInfo>, GeneratedRegistry>();
                })
                .BuildAsServer())
            {
                Assert.True(await server.StartAsync(CancellationToken));
                OutputHelper.WriteLine("Server started.");

                var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await client.ConnectAsync(hostConfigurator.GetServerEndPoint(), CancellationToken);
                OutputHelper.WriteLine("Connected.");

                using (var stream = await hostConfigurator.GetClientStream(client))
                using (var streamReader = new StreamReader(stream, Utf8Encoding, true))
                using (var streamWriter = new StreamWriter(stream, Utf8Encoding, 1024 * 1024 * 4))
                {
                    await streamWriter.WriteAsync("MIN 3 1 4 1 5\r\n".AsMemory(), CancellationToken);
                    await streamWriter.FlushAsync(CancellationToken);
                    var line = await streamReader.ReadLineAsync(CancellationToken);
                    Assert.Equal("1", line);
                }

                await server.StopAsync(CancellationToken);
            }
        }

        [Theory]
        [InlineData(typeof(RegularHostConfigurator))]
        public async Task GeneratedRegistry_ExecutesInlineFilter(Type hostConfiguratorType)
        {
            CountingFilterAttribute.ExecutingCount = 0;
            CountingFilterAttribute.ExecutedCount = 0;

            var hostConfigurator = CreateObject<IHostConfigurator>(hostConfiguratorType);
            using (var server = CreateSocketServerBuilder<StringPackageInfo, CommandLinePipelineFilter>(hostConfigurator)
                .UseGeneratedCommand<string, StringPackageInfo>()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IGeneratedCommandRegistry<string, StringPackageInfo>, GeneratedRegistry>();
                })
                .BuildAsServer())
            {
                Assert.True(await server.StartAsync(CancellationToken));
                OutputHelper.WriteLine("Server started.");

                var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await client.ConnectAsync(hostConfigurator.GetServerEndPoint(), CancellationToken);
                OutputHelper.WriteLine("Connected.");

                using (var stream = await hostConfigurator.GetClientStream(client))
                using (var streamReader = new StreamReader(stream, Utf8Encoding, true))
                using (var streamWriter = new StreamWriter(stream, Utf8Encoding, 1024 * 1024 * 4))
                {
                    await streamWriter.WriteAsync("FILTERED\r\n".AsMemory(), CancellationToken);
                    await streamWriter.FlushAsync(CancellationToken);
                    var line = await streamReader.ReadLineAsync(CancellationToken);
                    Assert.Equal("filtered", line);
                }

                await server.StopAsync(CancellationToken);
            }

            Assert.Equal(1, CountingFilterAttribute.ExecutingCount);
            Assert.Equal(1, CountingFilterAttribute.ExecutedCount);
        }

        [Theory]
        [InlineData(typeof(RegularHostConfigurator))]
        public async Task GeneratedRegistry_ExecutesGlobalFilter(Type hostConfiguratorType)
        {
            GlobalCountingFilterAttribute.ExecutingCount = 0;
            GlobalCountingFilterAttribute.ExecutedCount = 0;

            var hostConfigurator = CreateObject<IHostConfigurator>(hostConfiguratorType);
            using (var server = CreateSocketServerBuilder<StringPackageInfo, CommandLinePipelineFilter>(hostConfigurator)
                .UseGeneratedCommand<string, StringPackageInfo>(options => options.AddGlobalCommandFilter<GlobalCountingFilterAttribute>())
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IGeneratedCommandRegistry<string, StringPackageInfo>, GeneratedRegistry>();
                })
                .BuildAsServer())
            {
                Assert.True(await server.StartAsync(CancellationToken));
                OutputHelper.WriteLine("Server started.");

                var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await client.ConnectAsync(hostConfigurator.GetServerEndPoint(), CancellationToken);
                OutputHelper.WriteLine("Connected.");

                using (var stream = await hostConfigurator.GetClientStream(client))
                using (var streamReader = new StreamReader(stream, Utf8Encoding, true))
                using (var streamWriter = new StreamWriter(stream, Utf8Encoding, 1024 * 1024 * 4))
                {
                    await streamWriter.WriteAsync("MIN 3 1 4 1 5\r\n".AsMemory(), CancellationToken);
                    await streamWriter.FlushAsync(CancellationToken);
                    var line = await streamReader.ReadLineAsync(CancellationToken);
                    Assert.Equal("1", line);
                }

                await server.StopAsync(CancellationToken);
            }

            Assert.Equal(1, GlobalCountingFilterAttribute.ExecutingCount);
            Assert.Equal(1, GlobalCountingFilterAttribute.ExecutedCount);
        }
    }
}
