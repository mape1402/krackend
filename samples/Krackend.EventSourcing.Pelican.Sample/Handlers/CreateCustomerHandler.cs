using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;

namespace Krackend.EventSourcing.Pelican.Sample.Handlers;

public sealed class CreateCustomerHandler
    : HookedCreateCommandHandler<CreateCustomerCommand, CustomerResponse, Customer>
{
    private readonly SampleDbContext _dbContext;

    public CreateCustomerHandler(
        SampleDbContext dbContext,
        IEnumerable<ICommandHandlerHook<CreateCustomerCommand, Customer>> hooks)
        : base(hooks)  
    {
        _dbContext = dbContext;
    }

    protected override ValueTask ValidateAsync(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new ArgumentException("Customer id is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Customer name is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Customer email is required.", nameof(request));

        return ValueTask.CompletedTask;
    }

    protected override ValueTask<Customer> MapToEntityAsync(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new Customer
        {
            Id = request.CustomerId,
            Name = request.Name,
            Email = request.Email
        });
    }

    protected override async Task SaveEntityAsync(
        CreateCustomerCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        _dbContext.Customers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    protected override ValueTask<CustomerResponse> MapToResponseAsync(
        CreateCustomerCommand request,
        Customer entity,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new CustomerResponse(entity.Id, entity.Name, entity.Email));
    }
}
