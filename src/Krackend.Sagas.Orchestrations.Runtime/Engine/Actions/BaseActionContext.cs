namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Actions
{
    internal class BaseActionContext
    {
        public BaseActionContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }
    }
}
