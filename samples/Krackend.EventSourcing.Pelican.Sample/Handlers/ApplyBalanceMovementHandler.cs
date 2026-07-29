using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.Pelican.Sample.Handlers;

public sealed class ApplyBalanceMovementHandler
    : HookedEntityCommandHandler<ApplyBalanceMovementCommand, CustomerResponse, Customer>
{
    private readonly SampleDbContext _dbContext;

    public ApplyBalanceMovementHandler(
        SampleDbContext dbContext,
        IEnumerable<ICommandHandlerHook<ApplyBalanceMovementCommand, Customer>> hooks)
        : base(hooks)
    {
        _dbContext = dbContext;
    }

    protected override ValueTask ValidateAsync(
        ApplyBalanceMovementCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new ArgumentException("Customer id is required.", nameof(request));

        if (request.Amount == 0m)
            throw new ArgumentOutOfRangeException(nameof(request), "Movement amount must be different from zero.");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("Movement description is required.", nameof(request));

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<Customer> MapToEntityAsync(
        ApplyBalanceMovementCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .SingleOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new InvalidOperationException($"Customer '{request.CustomerId}' was not found.");

        var fee = MovementFees.Calculate(request.Amount);
        customer.Balance += request.Amount - fee;
        return customer;
    }

    protected override Task SaveEntityAsync(
        ApplyBalanceMovementCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    protected override ValueTask<CustomerResponse> MapToResponseAsync(
        ApplyBalanceMovementCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new CustomerResponse(entity.Id, entity.Name, entity.Email, entity.Balance));
    }
}
