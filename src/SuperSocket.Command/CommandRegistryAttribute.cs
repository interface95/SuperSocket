using System;

namespace SuperSocket.Command
{
    /// <summary>
    /// Marks a partial command registry class for command source generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CommandRegistryAttribute : Attribute
    {
    }
}
