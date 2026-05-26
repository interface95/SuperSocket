using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Middleware;
using SuperSocket.Server.Abstractions.Session;

namespace SuperSocket.Command
{
    /// <summary>
    /// Represents a generated-only command middleware for handling commands in a SuperSocket application.
    /// </summary>
    /// <typeparam name="TKey">The type of the command key.</typeparam>
    /// <typeparam name="TPackageInfo">The type of the package information.</typeparam>
    public class GeneratedCommandMiddleware<TKey, TPackageInfo> : MiddlewareBase, IPackageHandler<TPackageInfo>
        where TPackageInfo : class, IKeyedPackageInfo<TKey>
    {
        private readonly Dictionary<TKey, IGeneratedCommandSet<TKey, TPackageInfo>> _commands;
        private readonly Func<IAppSession, TPackageInfo, CancellationToken, ValueTask> _unknownPackageHandler;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedCommandMiddleware{TKey, TPackageInfo}"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for dependency injection.</param>
        /// <param name="commandOptions">The options for configuring commands.</param>
        /// <param name="generatedRegistries">The generated command registries.</param>
        /// <param name="comparer">The optional comparer for command keys.</param>
        /// <param name="loggerFactory">The optional logger factory.</param>
        public GeneratedCommandMiddleware(
            IServiceProvider serviceProvider,
            IOptions<CommandOptions> commandOptions,
            IEnumerable<IGeneratedCommandRegistry<TKey, TPackageInfo>> generatedRegistries,
            IEqualityComparer<TKey> comparer = null,
            ILoggerFactory loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(commandOptions);
            ArgumentNullException.ThrowIfNull(generatedRegistries);

            _logger = loggerFactory?.CreateLogger("GeneratedCommandMiddleware");

            var registries = generatedRegistries.ToList();

            if (registries.Count == 0)
            {
                throw new InvalidOperationException($"No generated command registry is registered for {typeof(TKey).FullName}, {typeof(TPackageInfo).FullName}.");
            }

            var options = commandOptions.Value;
            _commands = CreateCommandsFromGeneratedRegistries(serviceProvider, options, registries, comparer);
            _unknownPackageHandler = GetUnknownPackageHandler(options);
        }

        /// <summary>
        /// Handles a package by executing the corresponding generated command.
        /// </summary>
        /// <param name="session">The application session.</param>
        /// <param name="package">The package to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected virtual async ValueTask HandlePackage(IAppSession session, TPackageInfo package, CancellationToken cancellationToken)
        {
            if (!_commands.TryGetValue(package.Key, out IGeneratedCommandSet<TKey, TPackageInfo> commandSet))
            {
                var unknownPackageHandler = _unknownPackageHandler;

                if (unknownPackageHandler != null)
                {
                    await unknownPackageHandler.Invoke(session, package, cancellationToken);
                }

                return;
            }

            await commandSet.ExecuteAsync(session, package, cancellationToken);
        }

        ValueTask IPackageHandler<TPackageInfo>.Handle(IAppSession session, TPackageInfo package, CancellationToken cancellationToken)
        {
            return HandlePackage(session, package, cancellationToken);
        }

        private Dictionary<TKey, IGeneratedCommandSet<TKey, TPackageInfo>> CreateCommandsFromGeneratedRegistries(
            IServiceProvider serviceProvider,
            CommandOptions commandOptions,
            IEnumerable<IGeneratedCommandRegistry<TKey, TPackageInfo>> generatedRegistries,
            IEqualityComparer<TKey> comparer)
        {
            var commandDict = comparer == null ?
                new Dictionary<TKey, IGeneratedCommandSet<TKey, TPackageInfo>>() : new Dictionary<TKey, IGeneratedCommandSet<TKey, TPackageInfo>>(comparer);

            foreach (var registration in generatedRegistries.SelectMany(r => r.GetRegistrations()))
            {
                if (commandDict.ContainsKey(registration.Key))
                {
                    var error = $"Duplicated command with Key {registration.Key} is found: {registration.ActualCommandType}";
                    _logger?.LogError(error);
                    throw new Exception(error);
                }

                var commandSet = registration.CommandSetFactory(serviceProvider, commandOptions);
                commandDict.Add(registration.Key, commandSet);
                _logger?.LogDebug($"The command with key {registration.Key} is registered: {registration.ActualCommandType}");
            }

            return commandDict;
        }

        private Func<IAppSession, TPackageInfo, CancellationToken, ValueTask> GetUnknownPackageHandler(CommandOptions commandOptions)
        {
            var unknownPackageHandler = commandOptions.UnknownPackageHandler;

            if (unknownPackageHandler == null)
                return null;

            var typedHandler = unknownPackageHandler as Func<IAppSession, TPackageInfo, CancellationToken, ValueTask>;

            if (typedHandler == null)
            {
                _logger?.LogError($"{nameof(commandOptions.UnknownPackageHandler)} was registered incorrectly. The expected type is {typeof(Func<IAppSession, TPackageInfo, CancellationToken, ValueTask>).Name}.");
            }

            return typedHandler;
        }
    }
}
