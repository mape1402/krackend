using Krackend.Sagas.Orchestrations.Runtime.Engine.Actions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    public interface IDecision
    {
        ITaskAction HandsOn();
    }
}
