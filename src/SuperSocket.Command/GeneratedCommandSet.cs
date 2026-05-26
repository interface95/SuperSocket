using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Server.Abstractions.Session;

namespace SuperSocket.Command
{
    /// <summary>
    /// Represents a generated command set.
    /// </summary>
    /// <typeparam name="TKey">The type of the command key.</typeparam>
    /// <typeparam name="TPackageInfo">The type of the package information.</typeparam>
    public interface IGeneratedCommandSet<TKey, TPackageInfo>
        where TPackageInfo : class
    {
        /// <summary>
        /// Gets the command key.
        /// </summary>
        TKey Key { get; }

        /// <summary>
        /// Gets the command metadata.
        /// </summary>
        CommandMetadata Metadata { get; }

        /// <summary>
        /// Executes the generated command set.
        /// </summary>
        /// <param name="session">The application session.</param>
        /// <param name="package">The package information.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous execution operation.</returns>
        ValueTask ExecuteAsync(IAppSession session, TPackageInfo package, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents a generated command set for a specific application session type.
    /// </summary>
    /// <typeparam name="TKey">The type of the command key.</typeparam>
    /// <typeparam name="TAppSession">The type of the application session.</typeparam>
    /// <typeparam name="TPackageInfo">The type of the package information.</typeparam>
    public sealed class TypedCommandSet<TKey, TAppSession, TPackageInfo> : IGeneratedCommandSet<TKey, TPackageInfo>
        where TAppSession : IAppSession
        where TPackageInfo : class
    {
        private static readonly IReadOnlyList<ICommandFilter> EmptyFilters = Array.Empty<ICommandFilter>();
        private readonly ICommand<TAppSession, TPackageInfo> _command;
        private readonly IAsyncCommand<TAppSession, TPackageInfo> _asyncCommand;
        private readonly IReadOnlyList<ICommandFilter> _filters;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypedCommandSet{TKey, TAppSession, TPackageInfo}"/> class.
        /// </summary>
        /// <param name="key">The command key.</param>
        /// <param name="metadata">The command metadata.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="filters">The command filters.</param>
        public TypedCommandSet(TKey key, CommandMetadata metadata, ICommand command, IReadOnlyList<ICommandFilter> filters)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(command);

            Key = key;
            Metadata = metadata;
            _command = command as ICommand<TAppSession, TPackageInfo>;
            _asyncCommand = command as IAsyncCommand<TAppSession, TPackageInfo>;
            _filters = filters ?? EmptyFilters;

            if (_command == null && _asyncCommand == null)
            {
                throw new ArgumentException($"The command must implement {typeof(ICommand<TAppSession, TPackageInfo>).Name} or {typeof(IAsyncCommand<TAppSession, TPackageInfo>).Name}.", nameof(command));
            }
        }

        /// <summary>
        /// Gets the command key.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the command metadata.
        /// </summary>
        public CommandMetadata Metadata { get; }

        /// <summary>
        /// Executes the generated command set.
        /// </summary>
        /// <param name="session">The application session.</param>
        /// <param name="package">The package information.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous execution operation.</returns>
        public async ValueTask ExecuteAsync(IAppSession session, TPackageInfo package, CancellationToken cancellationToken)
        {
            if (_filters.Count > 0)
            {
                await ExecuteAsyncWithFilter(session, package, cancellationToken);
                return;
            }

            await ExecuteCommandAsync(session, package, cancellationToken);
        }

        private async ValueTask ExecuteAsyncWithFilter(IAppSession session, TPackageInfo package, CancellationToken cancellationToken)
        {
            var context = new CommandExecutingContext();
            context.Package = package;
            context.Session = session;
            context.CancellationToken = cancellationToken;

            var command = _asyncCommand != null ? (_asyncCommand as ICommand) : (_command as ICommand);

            if (command is ICommandWrap commandWrap)
                command = commandWrap.InnerCommand;

            context.CurrentCommand = command;

            var filters = _filters;
            var continued = true;

            for (var i = 0; i < filters.Count; i++)
            {
                var f = filters[i];

                if (f is AsyncCommandFilterAttribute asyncCommandFilter)
                {
                    continued = await asyncCommandFilter.OnCommandExecutingAsync(context);
                }
                else if (f is CommandFilterAttribute commandFilter)
                {
                    continued = commandFilter.OnCommandExecuting(context);
                }

                if (!continued)
                    break;
            }

            if (!continued)
                return;

            try
            {
                await ExecuteCommandAsync(session, package, cancellationToken);
            }
            catch (Exception e)
            {
                // Preserve CommandMiddleware filter semantics: expose command failures through the context without rethrowing.
                context.Exception = e;
            }
            finally
            {
                for (var i = 0; i < filters.Count; i++)
                {
                    var f = filters[i];

                    if (f is AsyncCommandFilterAttribute asyncCommandFilter)
                    {
                        await asyncCommandFilter.OnCommandExecutedAsync(context);
                    }
                    else if (f is CommandFilterAttribute commandFilter)
                    {
                        commandFilter.OnCommandExecuted(context);
                    }
                }
            }

        }

        private async ValueTask ExecuteCommandAsync(IAppSession session, TPackageInfo package, CancellationToken cancellationToken)
        {
            var appSession = (TAppSession)session;
            var asyncCommand = _asyncCommand;

            if (asyncCommand != null)
            {
                await asyncCommand.ExecuteAsync(appSession, package, cancellationToken);
                return;
            }

            _command.Execute(appSession, package);
        }
    }
}
