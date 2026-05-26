using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Host;

namespace CommandServer.Aot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = SuperSocketHostBuilder.Create<StringPackageInfo, CommandLinePipelineFilter>(args)
                .UseGeneratedCommand<string, StringPackageInfo>()
                .ConfigureServices((hostCtx, services) =>
                {
                    services.AddSingleton<IGeneratedCommandRegistry<string, StringPackageInfo>, AppCommands>();
                })
                .ConfigureAppConfiguration((hostCtx, configApp) =>
                {
                    configApp.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "serverOptions:name", "AotCommandServer" },
                        { "serverOptions:listeners:0:ip", "Any" },
                        { "serverOptions:listeners:0:port", "4040" }
                    });
                })
                .ConfigureLogging((hostCtx, loggingBuilder) =>
                {
                    loggingBuilder.AddConsole();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
