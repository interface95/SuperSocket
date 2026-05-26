using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions.Host;

namespace SuperSocket.Server
{
    /// <summary>
    /// Provides extension methods for configuring generated-only command middleware in a SuperSocket application.
    /// </summary>
    public static class GeneratedCommandMiddlewareExtensions
    {
        /// <summary>
        /// Adds generated-only command middleware to the SuperSocket host builder.
        /// </summary>
        /// <typeparam name="TKey">The type of the command key.</typeparam>
        /// <typeparam name="TPackageInfo">The type of the package.</typeparam>
        /// <param name="builder">The SuperSocket host builder.</param>
        /// <param name="configurator">The optional configurator for command options.</param>
        /// <param name="comparer">The optional comparer for command keys.</param>
        /// <returns>The configured host builder.</returns>
        public static ISuperSocketHostBuilder<TPackageInfo> UseGeneratedCommand<TKey, TPackageInfo>(
            this ISuperSocketHostBuilder<TPackageInfo> builder,
            Action<CommandOptions> configurator = null,
            IEqualityComparer<TKey> comparer = null)
            where TPackageInfo : class, IKeyedPackageInfo<TKey>
        {
            return builder.UseMiddleware<GeneratedCommandMiddleware<TKey, TPackageInfo>>()
                .ConfigureServices((hostCtx, services) =>
                {
                    if (configurator != null)
                    {
                        services.Configure(configurator);
                    }

                    if (comparer != null)
                    {
                        services.AddSingleton<IEqualityComparer<TKey>>(comparer);
                    }
                }) as ISuperSocketHostBuilder<TPackageInfo>;
        }
    }
}
