using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Handles get branch rule definitions query requests.
/// </summary>
public sealed class GetBranchRuleDefinitionsQueryHandler : IRequestHandler<GetBranchRuleDefinitionsQuery, IEnumerable<BranchRuleDefinitionModel>>
{
    private readonly IBranchRuleRepository _repository;
    private readonly IBranchRuleDefinitionApplicationMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetBranchRuleDefinitionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    /// <param name="mapper">Mapper dependency.</param>
    public GetBranchRuleDefinitionsQueryHandler(IBranchRuleRepository repository, IBranchRuleDefinitionApplicationMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Collection result.</returns>
    public async Task<IEnumerable<BranchRuleDefinitionModel>> Handle(GetBranchRuleDefinitionsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.BranchRuleDefinition> rows = await _repository.GetAll(
            PrimitiveParser.ParseId(request.StageDefinitionId),
            cancellationToken);

        return rows.Select(_mapper.ToModel);
    }
}

