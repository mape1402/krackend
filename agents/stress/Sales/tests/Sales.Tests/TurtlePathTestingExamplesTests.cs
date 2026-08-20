using DataScorpio.Profiles;
using DataScorpio.Testing;
using Sales.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pelican.Mediator;
using TurtlePath.Commands;
using TurtlePath.Domain.Contracts;
using TurtlePath.EntityFrameworkCore;
using TurtlePath.EntityFrameworkCore.Conventions;
using TurtlePath.Models.Responses;
using TurtlePath.Testing;
using TurtlePath.Testing.EntityFrameworkCore;
using TurtlePath.Testing.Integration;

namespace Sales.Tests;

public sealed class TurtlePathTestingExamplesTests
{
    [Fact]
    public async Task Handler_can_be_tested_without_mocking_turtlepath_dependencies()
    {
        await using var host = await TemplateTestHost
            .CreateUnitHost()
            .WithMap<CreateCustomerRequest, Customer>(request => new Customer
            {
                Id = request.Id,
                Name = request.Name
            })
            .WithMap<Customer, CustomerResponse>(customer => new CustomerResponse
            {
                Id = customer.Id,
                Name = customer.Name
            })
            .WithValidRequest<CreateCustomerRequest>()
            .BuildAsync();

        var handler = new CreateCustomerCommandHandler(host.Services);

        var response = await handler.Handle(new CreateCustomerRequest(1, "Ada"));

        Assert.Equal("Ada", response.Name);
        Assert.True(host.Store<Customer>().Contains(customer => customer.Id == 1));
    }

    [Fact]
    public async Task Handler_can_be_tested_through_pelican_sqlite_and_datascorpio()
    {
        await using var host = await TemplateTestHost
            .CreateIntegrationHost<SampleDbContext>(profiles => profiles.AddProfile<CustomerQueryProfile>())
            .UsePelican(typeof(TurtlePathTestingExamplesTests).Assembly)
            .WithMap<CreateCustomerRequest, Customer>(request => new Customer
            {
                Id = request.Id,
                Name = request.Name
            })
            .WithMap<Customer, CustomerResponse>(customer => new CustomerResponse
            {
                Id = customer.Id,
                Name = customer.Name
            })
            .WithValidRequest<CreateCustomerRequest>()
            .BuildAsync();

        await host.CreateSchemaAsync<SampleDbContext>();

        var response = await host.SendAsync(new CreateCustomerRequest(2, "Grace"));
        var dbContext = host.Resolve<SampleDbContext>();

        Assert.Equal("Grace", response.Name);
        Assert.True(await dbContext.Customers.AnyAsync(customer => customer.Id == 2));

        var dataScorpio = host.Resolve<IDataScorpioTesting<Customer>>();

        await dataScorpio.SeedAsync(
        [
            new Customer { Id = 3, Name = "Ada" },
            new Customer { Id = 4, Name = "Adam" },
            new Customer { Id = 5, Name = "Grace" }
        ]);

        var result = await dataScorpio.ApplyAsync(filters: "Name@=*ada", sorts: "Name");

        Assert.True(result.IsSuccess);
        Assert.Equal(["Ada", "Adam"], result.Result.Items.Select(customer => customer.Name));
    }

    public sealed record CreateCustomerRequest(int Id, string Name) : IRequest<CustomerResponse>;

    public sealed class Customer : IEntity<int>
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public sealed class CustomerResponse : IBaseResponse<int>
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public sealed class CreateCustomerCommandHandler
        : GenericCreateCommandHandler<CreateCustomerRequest, CustomerResponse, Customer, int>
    {
        public CreateCustomerCommandHandler(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }
    }

    private sealed class CustomerQueryProfile : QueryProfile<Customer>
    {
        public override void Configure(IQueryProfileBuilder<Customer> builder)
        {
            builder
                .AllowFilter(customer => customer.Name)
                .AllowSort(customer => customer.Name);
        }
    }

    public sealed class SampleDbContext : BaseDbContext
    {
        public SampleDbContext(
            DbContextOptions<SampleDbContext> options,
            TurtlePathDbContextOptions turtlePathOptions,
            IEnumerable<ITurtlePathModelConvention> modelConventions)
            : base(options, turtlePathOptions, modelConventions)
        {
        }

        public DbSet<Customer> Customers { get; set; }
    }

    public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.HasKey(customer => customer.Id);
            builder.Property(customer => customer.Name).HasMaxLength(120).IsRequired();
        }
    }
}
