using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Actions
{
    internal class InvokeRemoteCommandAction : ITaskAction
    {
        private readonly InvokeRemoteCommandContext _context;

        public InvokeRemoteCommandAction(InvokeRemoteCommandContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var command = new RemoteCommand
            {
                Payload = _context.Payload,
                RemoteCommandTransport = _context.RemoteCommandTransport,
                SettingsPayload = _context.SettingsPayload
            };

            var dispatcher = _context.ServiceProvider.GetRequiredService<IRemoteCommandDispatcher>();
            await dispatcher.DispatchAsync(command, cancellationToken);
        }
    }
}
