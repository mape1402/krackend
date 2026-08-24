using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Blocks design mutation commands when their orchestration version is no longer Draft.
/// </summary>
public sealed class OrchestrationDraftMutationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IOrchestrationVersionEditGuard _editGuard;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationDraftMutationPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    public OrchestrationDraftMutationPipelineBehavior(IOrchestrationVersionEditGuard editGuard)
    {
        _editGuard = editGuard ?? throw new ArgumentNullException(nameof(editGuard));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    public async Task<TResponse> Handle(TRequest request, Handler<TResponse> next, CancellationToken cancellationToken)
    {
        await Guard(request, cancellationToken);
        return await next(cancellationToken);
    }

    private async Task Guard(TRequest request, CancellationToken cancellationToken)
    {
        switch (request)
        {
            case UpdateOrchestrationVersionCommand command:
                await _editGuard.EnsureVersionIsDraft(command.Id, cancellationToken);
                break;
            case CreateStageDefinitionCommand command:
                await _editGuard.EnsureVersionIsDraft(command.OrchestrationVersionId, cancellationToken);
                break;
            case UpdateStageDefinitionCommand command:
                await _editGuard.EnsureStageVersionIsDraft(command.Id, cancellationToken);
                break;
            case DeleteStageDefinitionCommand command:
                await _editGuard.EnsureStageVersionIsDraft(command.Id, cancellationToken);
                break;
            case SetStageExecutionConditionCommand command:
                await _editGuard.EnsureStageVersionIsDraft(command.Id, cancellationToken);
                break;
            case CreateTriggerBindingCommand command:
                await _editGuard.EnsureVersionIsDraft(command.OrchestrationVersionId, cancellationToken);
                break;
            case UpdateTriggerBindingCommand command:
                await _editGuard.EnsureTriggerVersionIsDraft(command.Id, cancellationToken);
                break;
            case DeleteTriggerBindingCommand command:
                await _editGuard.EnsureTriggerVersionIsDraft(command.Id, cancellationToken);
                break;
            case EnableTriggerBindingCommand command:
                await _editGuard.EnsureTriggerVersionIsDraft(command.Id, cancellationToken);
                break;
            case DisableTriggerBindingCommand command:
                await _editGuard.EnsureTriggerVersionIsDraft(command.Id, cancellationToken);
                break;
            case CreateVariableDefinitionCommand command:
                await _editGuard.EnsureVersionIsDraft(command.OrchestrationVersionId, cancellationToken);
                break;
            case UpdateVariableDefinitionCommand command:
                await _editGuard.EnsureVariableVersionIsDraft(command.Id, cancellationToken);
                break;
            case DeleteVariableDefinitionCommand command:
                await _editGuard.EnsureVariableVersionIsDraft(command.Id, cancellationToken);
                break;
            case CreateTaskDefinitionCommand command:
                await _editGuard.EnsureStageVersionIsDraft(command.StageDefinitionId, cancellationToken);
                break;
            case UpdateTaskDefinitionCommand command:
                await _editGuard.EnsureTaskVersionIsDraft(command.Id, cancellationToken);
                break;
            case DeleteTaskDefinitionCommand command:
                await _editGuard.EnsureTaskVersionIsDraft(command.Id, cancellationToken);
                break;
            case EnableTaskDefinitionCommand command:
                await _editGuard.EnsureTaskVersionIsDraft(command.Id, cancellationToken);
                break;
            case DisableTaskDefinitionCommand command:
                await _editGuard.EnsureTaskVersionIsDraft(command.Id, cancellationToken);
                break;
            case SetTaskExecutionConditionCommand command:
                await _editGuard.EnsureTaskVersionIsDraft(command.Id, cancellationToken);
                break;
            case SetTaskTransformationCommand command:
                await _editGuard.EnsureTaskVersionIsDraft(command.Id, cancellationToken);
                break;
            case CreateParallelGroupDefinitionCommand command:
                await _editGuard.EnsureStageVersionIsDraft(command.StageDefinitionId, cancellationToken);
                break;
            case UpdateParallelGroupDefinitionCommand command:
                await _editGuard.EnsureParallelGroupVersionIsDraft(command.Id, cancellationToken);
                break;
            case DeleteParallelGroupDefinitionCommand command:
                await _editGuard.EnsureParallelGroupVersionIsDraft(command.Id, cancellationToken);
                break;
            case CreateBranchRuleDefinitionCommand command:
                await EnsureBranchSourceIsDraft(command.FromType, command.FromId, cancellationToken);
                break;
            case UpdateBranchRuleDefinitionCommand command:
                await _editGuard.EnsureBranchRuleVersionIsDraft(command.Id, cancellationToken);
                break;
            case DeleteBranchRuleDefinitionCommand command:
                await _editGuard.EnsureBranchRuleVersionIsDraft(command.Id, cancellationToken);
                break;
        }
    }

    private async Task EnsureBranchSourceIsDraft(
        ElementType fromType,
        string fromId,
        CancellationToken cancellationToken)
    {
        if (fromType == ElementType.Task)
        {
            await _editGuard.EnsureTaskVersionIsDraft(fromId, cancellationToken);
            return;
        }

        await _editGuard.EnsureStageVersionIsDraft(fromId, cancellationToken);
    }
}
