using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal sealed class MuleRemoteCommandDispatcher : IRemoteCommandDispatcher
    {
        private readonly IMuleClient _muleClient;

        public MuleRemoteCommandDispatcher(IMuleClient muleClient)
        {
            _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
        }

        public async Task DispatchAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            await _muleClient.EnqueueAsync(MuleActionKeys.RemoteCommandDispatchActionKey, command, cancellationToken);
        }
    }
}
