using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions.Session;

namespace SuperSocket.Tests.Command
{
    public sealed class CountingFilterAttribute : CommandFilterAttribute
    {
        public static int ExecutingCount;

        public static int ExecutedCount;

        public override bool OnCommandExecuting(CommandExecutingContext commandContext)
        {
            Interlocked.Increment(ref ExecutingCount);
            return true;
        }

        public override void OnCommandExecuted(CommandExecutingContext commandContext)
        {
            Interlocked.Increment(ref ExecutedCount);
        }
    }

    public sealed class GlobalCountingFilterAttribute : CommandFilterAttribute
    {
        public static int ExecutingCount;

        public static int ExecutedCount;

        public override bool OnCommandExecuting(CommandExecutingContext commandContext)
        {
            Interlocked.Increment(ref ExecutingCount);
            return true;
        }

        public override void OnCommandExecuted(CommandExecutingContext commandContext)
        {
            Interlocked.Increment(ref ExecutedCount);
        }
    }

    [CountingFilter]
    public sealed class FILTERED : IAsyncCommand<IAppSession, StringPackageInfo>
    {
        public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package, CancellationToken cancellationToken)
        {
            await session.SendAsync(Encoding.UTF8.GetBytes("filtered\r\n"), cancellationToken);
        }
    }
}
