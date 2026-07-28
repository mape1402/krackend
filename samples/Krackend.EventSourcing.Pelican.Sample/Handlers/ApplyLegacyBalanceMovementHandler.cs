using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.Pelican.Sample.Handlers;

public sealed class ApplyLegacyBalanceMovementHandler
    : HookedEntityCommandHandler<ApplyLegacyBalanceMovementCommand, CustomerResponse, Customer>
{
    private readonly SampleDbContext _dbContext;

    public ApplyLegacyBalanceMovementHandler(
        SampleDbContext dbContext,
        IEnumerable<ICommandHandlerHook<ApplyLegacyBalanceMovementCommand, Customer>> hooks)
        : base(hooks)
    {
        _dbContext = dbContext;
    }

    protected override ValueTask ValidateAsync(
        ApplyLegacyBalanceMovementCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new ArgumentException("Customer id is required.", nameof(request));

        if (request.Amount == 0m)
            throw new ArgumentOutOfRangeException(nameof(request), "Movement amount must be different from zero.");

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<Customer> MapToEntityAsync(
        ApplyLegacyBalanceMovementCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .SingleOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new InvalidOperationException($"Customer '{request.CustomerId}' was not found.");

        customer.Balance += request.Amount;
        return customer;
    }

    protected override Task SaveEntityAsync(
        ApplyLegacyBalanceMovementCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    protected override ValueTask<CustomerResponse> MapToResponseAsync(
        ApplyLegacyBalanceMovementCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new CustomerResponse(entity.Id, entity.Name, entity.Email, entity.Balance));
    }
}
