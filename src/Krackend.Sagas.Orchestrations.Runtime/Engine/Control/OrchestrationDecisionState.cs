using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control;

/// <summary>
/// Scoped, lease-protected view of the state used by one engine activation.
/// Persistence remains authoritative; this avoids re-reading unchanged rows
/// between decisions in the same activation.
/// </summary>
internal sealed class OrchestrationDecisionState
{
    private readonly Dictionary<Id, IReadOnlyCollection<TaskExecution>> _tasksByStage = [];
    private readonly Dictionary<Id, TaskExecution> _taskOverrides = [];

    public OrchestrationDecisionState(OrchestrationInstance instance)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    public OrchestrationInstance Instance { get; }

    public List<StageExecution> StageExecutions { get; } = [];

    public bool StagesLoaded { get; private set; }

    public void SetStages(IEnumerable<StageExecution> stages)
    {
        StageExecutions.Clear();
        StageExecutions.AddRange(stages);
        StagesLoaded = true;
    }

    public void AddStage(StageExecution stage)
    {
        if (StagesLoaded)
            StageExecutions.Add(stage);
        _tasksByStage[stage.Id] = Array.Empty<TaskExecution>();
    }

    public bool TryGetTasks(Id stageExecutionId, out IReadOnlyCollection<TaskExecution> tasks)
        => _tasksByStage.TryGetValue(stageExecutionId, out tasks);

    public void SetTasks(Id stageExecutionId, IReadOnlyCollection<TaskExecution> tasks)
    {
        var merged = tasks
            .Select(task => _taskOverrides.TryGetValue(task.Id, out var current) ? current : task)
            .ToDictionary(task => task.Id);
        foreach (var current in _taskOverrides.Values.Where(task => task.StageExecutionId == stageExecutionId))
        {
            merged[current.Id] = current;
        }

        _tasksByStage[stageExecutionId] = merged.Values.ToArray();
    }

    public void TrackTask(TaskExecution task)
    {
        _taskOverrides[task.Id] = task;
        if (!_tasksByStage.TryGetValue(task.StageExecutionId, out var tasks))
        {
            return;
        }

        _tasksByStage[task.StageExecutionId] = tasks
            .Where(existing => existing.Id != task.Id)
            .Append(task)
            .ToArray();
    }

    public bool TryGetTask(Id taskExecutionId, out TaskExecution task)
    {
        if (_taskOverrides.TryGetValue(taskExecutionId, out task))
            return true;

        task = _tasksByStage.Values
            .SelectMany(static tasks => tasks)
            .FirstOrDefault(candidate => candidate.Id == taskExecutionId);
        return task is not null;
    }

    public bool TryGetStage(Id stageExecutionId, out StageExecution stage)
    {
        stage = StageExecutions.FirstOrDefault(candidate => candidate.Id == stageExecutionId);
        return stage is not null;
    }

    public void InvalidateTasks(Id stageExecutionId)
        => _tasksByStage.Remove(stageExecutionId);
}
