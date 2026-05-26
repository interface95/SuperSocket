using System;

namespace SuperSocket.Command
{
    /// <summary>
    /// Represents a generated command registration.
    /// </summary>
    /// <typeparam name="TKey">The type of the command key.</typeparam>
    /// <typeparam name="TPackageInfo">The type of the package information.</typeparam>
    public sealed class CommandRegistration<TKey, TPackageInfo>
        where TPackageInfo : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandRegistration{TKey, TPackageInfo}"/> class.
        /// </summary>
        /// <param name="key">The command key.</param>
        /// <param name="actualCommandType">The concrete command type.</param>
        /// <param name="commandSetFactory">The factory that creates the generated command set.</param>
        public CommandRegistration(
            TKey key,
            Type actualCommandType,
            Func<IServiceProvider, CommandOptions, IGeneratedCommandSet<TKey, TPackageInfo>> commandSetFactory)
        {
            ArgumentNullException.ThrowIfNull(actualCommandType);
            ArgumentNullException.ThrowIfNull(commandSetFactory);

            Key = key;
            ActualCommandType = actualCommandType;
            CommandSetFactory = commandSetFactory;
        }

        /// <summary>
        /// Gets the command key.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the concrete command type.
        /// </summary>
        public Type ActualCommandType { get; }

        /// <summary>
        /// Gets the factory that creates the generated command set.
        /// </summary>
        public Func<IServiceProvider, CommandOptions, IGeneratedCommandSet<TKey, TPackageInfo>> CommandSetFactory { get; }
    }
}
