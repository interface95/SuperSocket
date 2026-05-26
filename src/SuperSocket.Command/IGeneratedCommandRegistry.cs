using System.Collections.Generic;

namespace SuperSocket.Command
{
    /// <summary>
    /// Represents a generated registry that exposes command registrations.
    /// </summary>
    /// <typeparam name="TKey">The type of the command key.</typeparam>
    /// <typeparam name="TPackageInfo">The type of the package information.</typeparam>
    public interface IGeneratedCommandRegistry<TKey, TPackageInfo>
        where TPackageInfo : class
    {
        /// <summary>
        /// Gets the generated command registrations.
        /// </summary>
        /// <returns>The generated command registrations.</returns>
        IEnumerable<CommandRegistration<TKey, TPackageInfo>> GetRegistrations();
    }
}
