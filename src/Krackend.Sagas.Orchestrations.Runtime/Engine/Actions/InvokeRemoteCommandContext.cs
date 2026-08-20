using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Actions
{
    internal class InvokeRemoteCommandContext : BaseActionContext
    {
        public InvokeRemoteCommandContext(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public RemoteCommandTransport RemoteCommandTransport { get; init; }

        public string Payload { get; init; }

        public string SettingsPayload { get; init; }
    }
}
