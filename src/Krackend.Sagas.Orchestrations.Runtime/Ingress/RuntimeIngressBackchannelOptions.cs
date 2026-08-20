namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public sealed class RuntimeIngressBackchannelOptions
    {
        public string TopicPrefix { get; set; } = "orchestrations";
    }
}
