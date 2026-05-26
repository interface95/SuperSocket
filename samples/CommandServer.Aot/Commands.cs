using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions.Session;

namespace CommandServer.Aot
{
    [Command("add")]
    [LoggingFilter("add-command")]
    public class ADD : IAsyncCommand<IAppSession, StringPackageInfo>
    {
        public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package, CancellationToken cancellationToken)
        {
            var result = package.Parameters
                .Select(p => int.Parse(p))
                .Sum();

            await session.SendAsync(Encoding.UTF8.GetBytes(result.ToString() + "\r\n"), cancellationToken);
        }
    }

    [Command("sub")]
    public class SUB : IAsyncCommand<IAppSession, StringPackageInfo>
    {
        public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package, CancellationToken cancellationToken)
        {
            var result = package.Parameters
                .Select(p => int.Parse(p))
                .Aggregate((x, y) => x - y);

            await session.SendAsync(Encoding.UTF8.GetBytes(result.ToString() + "\r\n"), cancellationToken);
        }
    }

    [CommandRegistry]
    public partial class AppCommands : IGeneratedCommandRegistry<string, StringPackageInfo>
    {
    }

    public sealed class LoggingFilterAttribute : CommandFilterAttribute
    {
        public LoggingFilterAttribute(string tag)
        {
            Tag = tag;
        }

        public string Tag { get; }

        public override bool OnCommandExecuting(CommandExecutingContext commandContext)
        {
            return true;
        }

        public override void OnCommandExecuted(CommandExecutingContext commandContext)
        {
        }
    }
}
