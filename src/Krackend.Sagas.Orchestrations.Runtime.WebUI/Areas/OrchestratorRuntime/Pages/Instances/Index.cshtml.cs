using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;

public sealed class IndexModel : PageModel
{
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly RuntimeEnvironmentDescriptor _runtimeEnvironment;
    private readonly OrchestratorRuntimeWebUIOptions _options;

    public IndexModel(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        IExecutionTransitionRepository transitionRepository,
        RuntimeEnvironmentDescriptor runtimeEnvironment,
        IOptions<OrchestratorRuntimeWebUIOptions> options)
    {
        _instanceRepository = instanceRepository;
        _stageRepository = stageRepository;
        _taskRepository = taskRepository;
        _transitionRepository = transitionRepository;
        _runtimeEnvironment = runtimeEnvironment;
        _options = options.Value;
    }

    public string EnvironmentKey => _runtimeEnvironment.EnvironmentKey;

    public string LiveHubPath
    {
        get
        {
            var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "runtime" : _options.RoutePrefix.Trim('/');
            return $"/{prefix}/live";
        }
    }

    public IReadOnlyCollection<OrchestrationInstance> Instances { get; private set; } = Array.Empty<OrchestrationInstance>();

    public OrchestrationInstance SelectedInstance { get; private set; }

    public IReadOnlyCollection<StageExecution> Stages { get; private set; } = Array.Empty<StageExecution>();

    public IReadOnlyCollection<TaskExecution> Tasks { get; private set; } = Array.Empty<TaskExecution>();

    public IReadOnlyCollection<ExecutionTransition> Transitions { get; private set; } = Array.Empty<ExecutionTransition>();

    public async Task OnGetAsync(string instanceId = null, CancellationToken cancellationToken = default)
    {
        Instances = await _instanceRepository.GetRecent(EnvironmentKey, 50, cancellationToken);
        SelectedInstance = await ResolveSelectedInstance(instanceId, cancellationToken);

        if (SelectedInstance is null)
            return;

        Stages = await _stageRepository.GetByInstanceId(SelectedInstance.Id, cancellationToken);
        Tasks = await _taskRepository.GetByInstanceId(SelectedInstance.Id, cancellationToken);
        Transitions = await _transitionRepository.GetByInstanceId(SelectedInstance.Id, cancellationToken);
    }

    public int ActiveCount => Instances.Count(x => x.Status is OrchestrationInstanceStatus.Running or OrchestrationInstanceStatus.Waiting);

    public int CompletedCount => Instances.Count(x => x.Status == OrchestrationInstanceStatus.Completed);

    public int FailedCount => Instances.Count(x => x.Status == OrchestrationInstanceStatus.Failed);

    public int WaitingCount => Instances.Count(x => x.Status == OrchestrationInstanceStatus.Waiting);

    public static string StatusClass(string status)
    {
        return status switch
        {
            nameof(OrchestrationInstanceStatus.Running) => "od-status-running",
            nameof(OrchestrationInstanceStatus.Waiting) => "od-status-waiting",
            nameof(OrchestrationInstanceStatus.Completed) => "od-status-active",
            nameof(OrchestrationInstanceStatus.Failed) => "od-status-danger",
            _ => "od-status-inactive"
        };
    }

    private async Task<OrchestrationInstance> ResolveSelectedInstance(string instanceId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            var selectedId = new Id(Ulid.Parse(instanceId));
            return await _instanceRepository.GetById(selectedId, cancellationToken);
        }

        return Instances.FirstOrDefault();
    }
}
