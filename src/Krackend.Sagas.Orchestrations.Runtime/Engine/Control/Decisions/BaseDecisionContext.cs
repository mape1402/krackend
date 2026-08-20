namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal class BaseDecisionContext
    {
        public BaseDecisionContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }
    }
}
