using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.Pelican.Sample.Handlers;

public sealed class RenameCustomerHandler
    : HookedEntityCommandHandler<RenameCustomerCommand, CustomerResponse, Customer>
{
    private readonly SampleDbContext _dbContext;

    public RenameCustomerHandler(
        SampleDbContext dbContext,
        IEnumerable<ICommandHandlerHook<RenameCustomerCommand, Customer>> hooks)
        : base(hooks)
    {
        _dbContext = dbContext;
    }

    protected override ValueTask ValidateAsync(RenameCustomerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new ArgumentException("Customer id is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Customer name is required.", nameof(request));

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<Customer> MapToEntityAsync(
        RenameCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .SingleOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new InvalidOperationException($"Customer '{request.CustomerId}' was not found.");

        customer.Name = request.Name;
        return customer;
    }

    protected override Task SaveEntityAsync(
        RenameCustomerCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    protected override ValueTask<CustomerResponse> MapToResponseAsync(
        RenameCustomerCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new CustomerResponse(entity.Id, entity.Name, entity.Email, entity.Balance));
    }
}
