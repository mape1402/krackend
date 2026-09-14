# TurtlePath Template Use Guide

This guide explains how to create and grow a service generated with `Payments`. It is written for the developer who just created a project and needs to know where code goes, which defaults are already wired, and when to use automations, handlers, hooks, jobs, consumers, and exception handling.

## Index

- [1. What The Template Gives You](#1-what-the-template-gives-you)
- [2. Create A Project](#2-create-a-project)
- [3. Project Shape](#3-project-shape)
- [4. Naming Conventions](#4-naming-conventions)
- [5. Default Dependency Registration](#5-default-dependency-registration)
- [6. Build One Feature From Start To Finish](#6-build-one-feature-from-start-to-finish)
- [7. Mapping With OctoMap](#7-mapping-with-octomap)
- [8. Validation With Crabalidator](#8-validation-with-crabalidator)
- [9. Filtering And Paging With DataScorpio](#9-filtering-and-paging-with-datascorpio)
- [10. Automations](#10-automations)
- [11. Custom Handlers](#11-custom-handlers)
- [12. Hooks](#12-hooks)
- [13. Controllers And REST Routes](#13-controllers-and-rest-routes)
- [14. Spider Pipelines And Transactions](#14-spider-pipelines-and-transactions)
- [15. Pigeon Consumers And Outbox](#15-pigeon-consumers-and-outbox)
- [16. Event Sourcing](#16-event-sourcing)
- [17. Exception Handling](#17-exception-handling)
- [18. Jobs](#18-jobs)
- [19. Testing](#19-testing)
- [20. External Documentation](#20-external-documentation)

## 1. What The Template Gives You

The generated service is not an empty ASP.NET Core project. It already has the standard TurtlePath stack wired:

- `TurtlePath` for request/response models, command handlers, query handlers, hooks, storage abstractions, validation and mapping adapters.
- `TurtlePath.Domain` for `CId`, `BaseEntity`, and `IEntity<TKey>`.
- `TurtlePath.EntityFrameworkCore` for `BaseDbContext`, `IDbContext`, EF Core storage adapters, and CId conversion.
- `TurtlePath.Automations` to generate happy-path Pelican handlers from profiles or attributes.
- `TurtlePath.OctoMap` as the TurtlePath mapper adapter.
- `TurtlePath.Crabalidator` as the TurtlePath validator adapter.
- `TurtlePath.DataScorpio` as the default filtering, sorting, search, and paging adapter.
- `TurtlePath.ExceptionHandling` for transport-neutral exception descriptors.
- `TurtlePath.ExceptionHandling.AspNetCore` for HTTP `ProblemDetails`.
- `TurtlePath.ExceptionHandling.Consumers` for Pigeon consumers.
- `TurtlePath.ExceptionHandling.Workers` for jobs and background work.
- `TurtlePath.Jobs` for one-shot jobs and recurring cron-style jobs.
- `TurtlePath.EventSourcing` with Krackend EF Core event store prepared as an opt-in event sourcing stack.
- `Pelican.Mediator` for request dispatch.
- `Spider.Pipelines` for execution boundaries, including the default transaction boundary.
- `TurtlePath.Spider` for the TurtlePath-owned bridge that sends Pelican requests through Spider without coupling those libraries to each other.
- `TurtlePath.Spider.Transactions` for the ambient transaction boundary, profile discovery, and request filtering.
- `Pigeon.Messaging` with Azure Service Bus and EF Core outbox prepared as an opt-in messaging stack.
- `TurtlePath.Analyzers` to prevent unsafe `CId` comparisons and assignments.

The recommended rule is simple:

- Use automations for CRUD happy paths.
- Add hooks when the happy path is right but needs business steps.
- Create handlers when the flow itself changes.
- Keep service-specific code in the generated project.
- Keep reusable infrastructure in NuGet packages.

## 2. Create A Project

Install the template:

```powershell
dotnet new install Payments
```

Create the default API/consumer host:

```powershell
dotnet new turtlepath -n Billing.Service -o C:\work\Billing.Service --host api-consumer
```

Create a one-shot job host:

```powershell
dotnet new turtlepath -n Billing.Jobs -o C:\work\Billing.Jobs --host job
```

After creation, verify the solution:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Every generated project includes `payments.json` at the solution root:

```json
{
  "packageId": "Payments",
  "version": "1.6.0"
}
```

Use that file to know which `Payments` package version created the service. In the repository source it can show `0.0.0-local`; published template packages are stamped during release with the real package version.

The API/consumer host and job host share the same Business, Domain, Persistence, and testing shape. The main difference is the presentation host:

- `api-consumer` starts an ASP.NET Core app with controllers, Scalar OpenAPI docs, health checks, Spider, and exception filters. Pigeon consumers are ready to enable when the service has broker settings.
- `job` starts a generic host that runs registered one-shot jobs and exits with code `0` when all jobs succeed.

## 3. Project Shape

Generated projects are split by responsibility:

```text
payments.json
src/
  Payments.Api/
    Controllers/
      BaseController.cs
    DependencyInjection/
      ApplicationExtensions.cs
      CustomContainerExtensions.cs
      EventSourcingExtensions.cs
      ExceptionHandlingExtensions.cs
      HealthCheckExtensions.cs
      MessagingExtensions.cs
      MvcExtensions.cs
      PersistenceExtensions.cs
      PipelineExtensions.cs
      StartupExtensions.cs
      OpenApiExtensions.cs
      TransactionBoundaryExtensions.cs
    HubConsumers/
      BaseHubConsumer.cs
    OpenApi/
      CIdSchemaTransformer.cs
      RemoveVersionParametersFilter.cs
      SetVersionInPathsFilter.cs
      OpenApiConstants.cs
    Program.cs
    appsettings.json
    appsettings.Development.json
    Payments.Api.csproj
  Payments.Business/
    Feature/
      README.md
      Commands/
        CreateFeatureCommandHandler.cs
        UpdateFeatureCommandHandler.cs
        ChangeFeatureStatusCommandHandler.cs
      Queries/
        GetFeatureByIdQuery.cs
        GetPagedFeaturesQuery.cs
      Validators/
        CreateFeatureRequestValidator.cs
        UpdateFeatureRequestValidator.cs
      Mappings/
        FeatureMappingProfile.cs
      Hooks/
        StampFeatureBeforeSaveHook.cs
        PublishFeatureAfterSaveHook.cs
      Automations/
        FeatureAutomationProfile.cs
      EventSourcing/
        FeatureEventSourcingProfile.cs
        FeatureCreated.cs
        FeatureUpdated.cs
        FeatureEventSource.cs
      Querying/
        FeatureQueryProfile.cs
      Models/
        Requests/
          CreateFeatureRequest.cs
          UpdateFeatureRequest.cs
          ChangeFeatureStatusRequest.cs
        Responses/
          FeatureResponse.cs
          ChangeFeatureStatusResponse.cs
      Services/
        ExternalProvider/
          IExternalProviderService.cs
          ExternalProviderService.cs
          ExternalProviderResult.cs
    Services/
      Audit/
        IAuditService.cs
        AuditService.cs
        AuditEntry.cs
    Constants.cs
    Payments.Business.csproj
  Payments.Domain/
    Payments.Domain.csproj
  Payments.Persistence/
    AppDbContext.cs
    Payments.Persistence.csproj
tests/
  Payments.Tests/
    Testing/
      TemplateTestHost.cs
    JobCompositionTests.cs
    TemplateCompositionTests.cs
    TurtlePathTestingExamplesTests.cs
    Payments.Tests.csproj
```

`Api` is the host layer. It owns controllers, optional consumers, startup composition, exception handling, Spider transaction boundaries, optional Pigeon configuration, OpenAPI schema configuration, Scalar UI, health checks, and the custom dependency injection entry point.

Transaction boundary implementation is provided by the `TurtlePath.Spider.Transactions` package. The generated API contains only the registration call; it does not duplicate boundary source files. Business profiles use `TransactionBoundaryProfile` from that package and are discovered from the assemblies explicitly passed by the API registration.

`Business` owns use cases. The template includes a `Feature` placeholder only to show the intended folder shape. In real code, replace `Feature` with the actual feature name:

```text
Customers/
  Commands/
  Queries/
  Validators/
  Mappings/
  Hooks/
  Automations/
  Querying/
  Models/
    Requests/
    Responses/
  Services/
```

Recommended meaning for each feature folder:

- `Commands/`: manual command handlers when automations are not enough, for example `CreateCustomerCommandHandler` or `ChangeOrderStatusCommandHandler`.
- `Queries/`: query messages and manual query handlers, for example `GetCustomerByIdQuery` with nested `GetCustomerByIdQueryHandler`, or `GetPagedInvoicesQuery`.
- `Validators/`: Crabalidator validators such as `CreateCustomerRequestValidator`.
- `Mappings/`: OctoMap profiles such as `CustomerMappingProfile`.
- `Hooks/`: TurtlePath hooks that customize handler stages without replacing the whole handler.
- `Automations/`: TurtlePath automation profiles such as `CustomerAutomationProfile`.
- `Querying/`: DataScorpio filter/sort configuration for paged queries.
- `Models/Requests/`: request DTOs. Mutation messages still use the `Request` suffix, for example `CreateCustomerRequest`.
- `Models/Responses/`: response DTOs returned by handlers, controllers, or consumers.
- `Services/`: feature-specific service integrations. Group each service in its own folder.

Examples:

```text
Invoices/
  Commands/
  Queries/
  Validators/
  Mappings/
  Hooks/
  Automations/
  Querying/
  Models/
    Requests/
    Responses/
  Services/

Orders/
  Commands/
  Queries/
  Validators/
  Mappings/
  Hooks/
  Automations/
  Querying/
  Models/
    Requests/
    Responses/
  Services/
```

Use feature folders directly under the Business project. Do not create a global `Features/` folder unless your team explicitly chooses that convention.

Feature-owned services go inside the feature:

```text
Customers/
  Services/
    SAT/
      ISatService.cs
      SatService.cs
      SatValidationResult.cs
```

Shared business services go at the Business root, grouped by service:

```text
Services/
  Audit/
    IAuditService.cs
    AuditService.cs
    AuditEntry.cs
```

That organization makes future extraction into shared libraries much easier.

Custom service registrations belong in `Api/DependencyInjection/CustomContainerExtensions.cs`. Keep the default container methods focused on framework defaults and chain custom registrations from the custom container extension.

`Domain` starts clean on purpose. Put service-owned entities, value objects, enums, and domain contracts there using the structure your service actually needs. TurtlePath identifiers come from the TurtlePath packages, not from generated template folders.

`Persistence` owns database integration. Keep `AppDbContext` clean and do not add `DbSet<TEntity>` properties just to expose tables. Add entity mappings as `IEntityTypeConfiguration<TEntity>` classes under a `Configurations/` folder when your service adds entities. This keeps the DbContext focused on TurtlePath conventions and lets configurations define tables, keys, indexes, relationships, and database conversions.

## 4. Naming Conventions

Use the same naming convention across all generated services. It makes code discovery predictable and keeps automations, handlers, validators, maps, controllers, and consumers easy to connect.

### Requests

Mutation messages are commands conceptually, but their class names keep the `Request` suffix:

- `CreateCustomerRequest`
- `UpdateInvoiceRequest`
- `ChangeOrderStatusRequest`
- `CancelInvoiceRequest`

Requests that target an existing entity should implement or inherit the appropriate TurtlePath request contract, usually `BaseRequest` for `CId`:

```csharp
public sealed class UpdateInvoiceRequest : BaseRequest, IRequest<InvoiceResponse>
{
    public decimal Amount { get; set; }
}
```

### Responses

Responses represent handler output:

- `CustomerResponse`
- `InvoiceResponse`
- `ChangeOrderStatusResponse`

Responses for `BaseEntity` flows should inherit `BaseResponse`:

```csharp
public sealed class InvoiceResponse : BaseResponse
{
    public string Folio { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
```

### Command Handlers

Command handlers express the action and end with `CommandHandler`:

- `CreateCustomerCommandHandler`
- `UpdateInvoiceCommandHandler`
- `ChangeOrderStatusCommandHandler`
- `CancelInvoiceCommandHandler`

When an operation is automated, do not create the handler class. The automation declaration is the source of truth.

### Queries

Query messages and handlers use `Query` terminology:

- `GetCustomerByIdQuery`
- `GetCustomerByIdQueryHandler`
- `GetPagedInvoicesQuery`
- `GetPagedInvoicesQueryHandler`

For small custom query flows, the handler can be nested inside the query:

```csharp
public sealed class GetCustomerByIdQuery : GetByIdQuery<Customer, CustomerResponse>
{
    public GetCustomerByIdQuery(CId id)
        : base(id)
    {
    }

    public sealed class GetCustomerByIdQueryHandler
        : GetByIdQueryHandler<GetCustomerByIdQuery, Customer, CustomerResponse>
    {
        public GetCustomerByIdQueryHandler(IServiceProvider services) : base(services)
        {
        }
    }
}
```

Paged queries use `GetPagedInfoQuery<TEntity, TResponse>` as the base query. Do not use a `PagedRequest` base class.

```csharp
using Billing.Service.Domain;
using Billing.Service.Business.Invoices.Models.Responses;
using TurtlePath.Domain.Identifier;
using TurtlePath.Queries;

namespace Billing.Service.Business.Invoices.Queries;

public sealed class GetPagedInvoicesQuery : GetPagedInfoQuery<Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQuery(PagedSettings pagedSettings)
        : base(pagedSettings)
    {
    }

    public CId? CustomerId { get; set; }
}

public sealed class GetPagedInvoicesQueryHandler
    : GetPagedInfoQueryHandler<GetPagedInvoicesQuery, Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQueryHandler(IServiceProvider services)
        : base(services)
    {
    }
}
```

### Validators

Validators use the request name plus `Validator`:

- `CreateCustomerRequestValidator`
- `UpdateInvoiceRequestValidator`
- `CancelInvoiceRequestValidator`

### Mappings

Mapping profiles use the feature or aggregate name plus `MappingProfile`:

- `CustomerMappingProfile`
- `InvoiceMappingProfile`
- `OrderMappingProfile`

### Automations

Automation profiles use the feature or aggregate name plus `AutomationProfile`:

- `CustomerAutomationProfile`
- `InvoiceAutomationProfile`
- `OrderAutomationProfile`

### Hooks

Hook names should describe what the hook does and where it runs:

- `AssignCustomerNumberBeforeSaveHook`
- `NormalizeCustomerEmailAfterMapHook`
- `PublishInvoiceCanceledAfterSaveHook`
- `AttachInvoiceSummaryAfterQueryHook`

### Services

Feature-owned services live under the feature and inside a service-specific folder:

```text
Customers/
  Services/
    SAT/
      ISatService.cs
      SatService.cs
```

Shared services live at the Business root, grouped by service:

```text
Services/
  Audit/
    IAuditService.cs
    AuditService.cs
```

### Controllers And Consumers

Controllers and hub consumers use plural resource names:

- `CustomersController`
- `InvoicesController`
- `OrdersController`
- `CustomersHubConsumer`
- `InvoicesHubConsumer`

### Entity Configurations

EF configurations use the entity name plus `Configuration`:

- `CustomerConfiguration`
- `InvoiceConfiguration`
- `OrderConfiguration`

## 5. Default Dependency Registration

Most application wiring lives in `Payments.Api/DependencyInjection`.

### Startup Defaults

The API/consumer host uses `AddDefaults`:

```csharp
public static IServiceCollection AddDefaults(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    return services
        .AddMvcDefaults()
        .AddOpenApiDefaults()
        .AddHealthCheckDefaults(configuration)
        .AddPersistenceDefaults(configuration)
        .AddApplicationDefaults()
        // Enable this only when the service needs event sourcing.
        // .AddEventSourcingDefaults()
        // Enable this only when the service needs Pigeon and has broker settings.
        // .AddMessagingDefaults(configuration)
        .AddPipelineDefaults(configuration)
        .AddCustomContainer(configuration);
}
```

`AddCustomContainer` is intentionally last. Put service-specific dependencies there so template defaults stay stable:

```csharp
internal static IServiceCollection AddCustomContainer(this IServiceCollection services, IConfiguration configuration)
{
    services.AddScoped<ICustomerNumberService, CustomerNumberService>();
    services.AddScoped<Services.Audit.IAuditService, Services.Audit.AuditService>();

    return services;
}
```

Use `AddCustomContainer` as the mandatory place for custom dependency injection in a real service. Do not put business dependencies in `AddDefaults`, `AddApplicationDefaults`, optional `AddMessagingDefaults`, or `AddPipelineDefaults` unless you are intentionally changing the base template.

### Application Defaults

`AddApplicationDefaults` registers Pelican, Crabalidator, OctoMap, TurtlePath hooks, automations, DataScorpio profiles, CId defaults, CId profiles, and EF Core adapters:

```csharp
services.AddPelican(typeof(Constants).Assembly);
services.AddCrabalidator(typeof(Constants).Assembly);

services.AddOctoMap(registration =>
{
    registration.Options.EnableRuntimeImplicitMaps = true;
    registration.Options.DuplicateMapPolicy = DuplicateMapPolicy.Throw;
    registration.AddMaps(typeof(Constants).Assembly);
});

services.AddScoped<IMapperAdapter, OctoMapAdapter>();
services.AddScoped<IValidatorAdapter, CrabalidatorAdapter>();

services.AddTurtlePath(typeof(Constants).Assembly)
    .UseAutomations(typeof(Constants).Assembly)
    .UseOctoMap()
    .UseCrabalidator()
    .UseDataScorpio(profiles => profiles.FromAssembly(typeof(Constants).Assembly))
    // Enable this only after adding IEventSourcingProfile implementations.
    // .UseEventSourcingProfiles(typeof(Constants).Assembly)
    .UseCId<Ulid, string>(config =>
    {
        config.DefaultFactory = () => CId.From(Ulid.NewUlid());
        config.ConvertToDb = id => id.ToString();
        config.ConvertFromDb = value => CId.From(Ulid.Parse(value));
        config.JsonConverter = value => string.IsNullOrEmpty(value)
            ? CId.From(Ulid.Empty)
            : CId.From(Ulid.Parse(value));
        config.NullableJsonConverter = value => string.IsNullOrEmpty(value)
            ? null
            : CId.From(Ulid.Parse(value));
        config.ParseFunction = value => CId.From(Ulid.Parse(value));
    })
    .UseCIdProfiles(typeof(DomainConstants).Assembly)
    .UseEntityFrameworkCore<AppDbContext>();
```

For new services, keep one consistent CId target type. The template default is `CId` wrapping `Ulid` in C# and storing it as `string` in the database.

### Custom CId Profiles For Legacy Entities

Do not change the default `UseCId<Ulid, string>()` just because one legacy table uses a different key. Keep the default for the healthy model, and add entity-specific CId definitions through a profile.

Use this when the public code should still see `CId`, but a specific entity is backed by another CLR/database type, for example an old `int` primary key:

```csharp
using TurtlePath.Domain.Identifier;

namespace Billing.Service.Domain.Identifier;

public sealed class BillingIdentifierProfile : CIdProfile
{
    public override void Configure(CIdProfileBuilder builder)
    {
        builder.UseCIdFor<LegacyInvoice, int, int>(config =>
        {
            config.DefaultFactory = () => CId.From(0);
            config.ConvertToDb = id => id.Cast<int>();
            config.ConvertFromDb = value => CId.From(value);
            config.JsonConverter = value => CId.From(int.Parse(value));
            config.NullableJsonConverter = value => string.IsNullOrWhiteSpace(value)
                ? null
                : CId.From(int.Parse(value));
            config.ParseFunction = value => CId.From(int.Parse(value));
            config.ToByteArrayFunction = value => BitConverter.GetBytes(value);
        });
    }
}
```

Then register profiles after the default CId configuration:

```csharp
services.AddTurtlePath(typeof(Constants).Assembly)
    .UseAutomations(typeof(Constants).Assembly)
    .UseOctoMap()
    .UseCrabalidator()
    .UseDataScorpio(profiles => profiles.FromAssembly(typeof(Constants).Assembly))
    .UseCId<Ulid, string>(config =>
    {
        config.DefaultFactory = () => CId.From(Ulid.NewUlid());
        config.ConvertToDb = id => id.ToString();
        config.ConvertFromDb = value => CId.From(Ulid.Parse(value));
        config.JsonConverter = value => string.IsNullOrEmpty(value)
            ? CId.From(Ulid.Empty)
            : CId.From(Ulid.Parse(value));
        config.NullableJsonConverter = value => string.IsNullOrEmpty(value)
            ? null
            : CId.From(Ulid.Parse(value));
        config.ParseFunction = value => CId.From(Ulid.Parse(value));
    })
    .UseCIdProfiles(typeof(BillingIdentifierProfile).Assembly)
    .UseEntityFrameworkCore<AppDbContext>();
```

Recommended placement:

```text
Domain/
  Identifier/
    BillingIdentifierProfile.cs
```

The generic parameters mean:

- `TEntity`: the entity that needs the override, for example `LegacyInvoice`.
- `TTargetType`: the CLR value wrapped by `CId` in application code, for example `int`, `Guid`, or `Ulid`.
- `TDbType`: the value EF stores in the database, for example `int` or `string`.

That means `UseCIdFor<LegacyInvoice, int, int>()` is an `int` inside `CId`, stored as `int` in the database. `UseCIdFor<ImportedOrder, Ulid, string>()` is a `Ulid` inside `CId`, stored as `string`.

If the legacy entity does not use `CId` at all and exposes a plain `int`, use the generic TurtlePath handlers and automations for `IEntity<TKey>` instead of forcing CId into that model.

## 6. Build One Feature From Start To Finish

This section builds an `Invoices` feature using the recommended path: automations plus hooks. Later sections show when to replace this with manual handlers.

### Domain Entity

Create `Domain/Invoice.cs`:

```csharp
using TurtlePath.Domain.Contracts;
using TurtlePath.Domain.Identifier;

namespace Billing.Service.Domain;

public sealed class Invoice : BaseEntity
{
    public CId CustomerId { get; set; }

    public string Folio { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CanceledAt { get; set; }
}

public enum InvoiceStatus
{
    Draft,
    Issued,
    Canceled
}
```

### EF Configuration

Create `Persistence/Configurations/InvoiceConfiguration.cs`:

```csharp
using Billing.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Service.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Folio)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(invoice => invoice.Amount)
            .HasPrecision(18, 2);

        builder.Property(invoice => invoice.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(invoice => invoice.Folio)
            .IsUnique();
    }
}
```

Do not add a `DbSet<Invoice>` property to `AppDbContext`. The template keeps the DbContext clean and relies on `IEntityTypeConfiguration<TEntity>` classes to describe the model. `BaseDbContext` applies TurtlePath identifier conventions, then your configurations define table names, max lengths, indexes, relationships, and conversions.

### Requests

Create request DTOs under `Business/Invoices/Models/Requests`.

```csharp
using Pelican.Mediator;
using TurtlePath.Domain.Identifier;
using TurtlePath.Models.Requests;

namespace Billing.Service.Business.Invoices.Models.Requests;

public sealed class CreateInvoiceRequest : IRequest<InvoiceResponse>
{
    public CId CustomerId { get; set; }

    public decimal Amount { get; set; }
}

public sealed class UpdateInvoiceRequest : BaseRequest, IRequest<InvoiceResponse>
{
    public decimal Amount { get; set; }
}

public sealed class CancelInvoiceRequest : BaseRequest, IRequest<InvoiceResponse>
{
    public string Reason { get; set; } = string.Empty;
}
```

Queries live under `Business/Invoices/Queries`:

```csharp
using Billing.Service.Domain;
using Pelican.Mediator;
using TurtlePath.Domain.Identifier;
using TurtlePath.Models.Requests;
using TurtlePath.Models.Responses;
using TurtlePath.Queries;

namespace Billing.Service.Business.Invoices.Queries;

public sealed class GetInvoiceByIdQuery : BaseRequest, IRequest<InvoiceResponse>;

public sealed class GetPagedInvoicesQuery : GetPagedInfoQuery<Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQuery(PagedSettings pagedSettings)
        : base(pagedSettings)
    {
    }

    public CId? CustomerId { get; set; }
}
```

### Response

Create `Business/Invoices/Models/Responses/InvoiceResponse.cs`:

```csharp
using TurtlePath.Domain.Identifier;
using TurtlePath.Models.Responses;

namespace Billing.Service.Business.Invoices.Models.Responses;

public sealed class InvoiceResponse : BaseResponse
{
    public CId CustomerId { get; set; }

    public string Folio { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
```

### Mapping

Create `Business/Invoices/Mappings/InvoiceMappingProfile.cs`:

```csharp
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Business.Invoices.Models.Responses;
using Billing.Service.Domain;
using OctoMap;

namespace Billing.Service.Business.Invoices.Mappings;

public sealed class InvoiceMappingProfile : OctoMapProfile
{
    public override void Configure(IOctoMapConfigurationBuilder builder)
    {
        builder.CreateMap<CreateInvoiceRequest, Invoice>();
        builder.CreateMap<UpdateInvoiceRequest, Invoice>();

        builder.CreateMap<Invoice, InvoiceResponse>()
            .ForMember(response => response.Status, map => map.MapFrom(invoice => invoice.Status.ToString()));
    }
}
```

The template scans all maps from the Business assembly through:

```csharp
registration.AddMaps(typeof(Constants).Assembly);
```

### Validation

Create `Business/Invoices/Validators/InvoiceRequestValidators.cs`:

```csharp
using Billing.Service.Business.Invoices.Models.Requests;
using Crabalidator;

namespace Billing.Service.Business.Invoices.Validators;

public sealed class CreateInvoiceRequestValidator : CrabValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(request => request.CustomerId).Must(id => !id.IsEmpty);
        RuleFor(request => request.Amount).Must(amount => amount > 0m);
    }
}

public sealed class UpdateInvoiceRequestValidator : CrabValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceRequestValidator()
    {
        RuleFor(request => request.Id).Must(id => !id.IsEmpty);
        RuleFor(request => request.Amount).Must(amount => amount > 0m);
    }
}

public sealed class CancelInvoiceRequestValidator : CrabValidator<CancelInvoiceRequest>
{
    public CancelInvoiceRequestValidator()
    {
        RuleFor(request => request.Id).Must(id => !id.IsEmpty);
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(200);
    }
}
```

The handlers and automations call validation before mapping or saving.

### DataScorpio Query Profile

Create `Business/Invoices/Querying/InvoiceQueryProfile.cs`:

```csharp
using Billing.Service.Domain;
using DataScorpio.Profiles;

namespace Billing.Service.Business.Invoices.Querying;

public sealed class InvoiceQueryProfile : QueryProfile<Invoice>
{
    public override void Configure(IQueryProfileBuilder<Invoice> builder)
    {
        builder
            .AllowFilter(invoice => invoice.Folio)
            .AllowFilter(invoice => invoice.CustomerId)
            .AllowFilter(invoice => invoice.Status)
            .AllowSort(invoice => invoice.Folio)
            .AllowSort(invoice => invoice.Amount)
            .AllowSort(invoice => invoice.CreatedAt)
            .AllowSearch(invoice => invoice.Folio);
    }
}
```

Example HTTP query:

```http
GET /api/v1/invoices?page=1&pageSize=20&filters=Folio@=*2026&sorts=-CreatedAt
```

DataScorpio only allows fields declared in the profile. This protects the entity model from random public filtering.

### Automation Profile

Create `Business/Invoices/Automations/InvoiceAutomationProfile.cs`:

```csharp
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Business.Invoices.Models.Responses;
using Billing.Service.Business.Invoices.Queries;
using Billing.Service.Domain;
using TurtlePath.Automations.Profiles;

namespace Billing.Service.Business.Invoices.Automations;

public sealed class InvoiceAutomationProfile : TurtlePathAutomationProfile
{
    public override void Configure(ITurtlePathAutomationBuilder builder)
    {
        builder.For<Invoice>()
            .ToCreate<CreateInvoiceRequest, InvoiceResponse>()
            .ToUpdate<UpdateInvoiceRequest, InvoiceResponse>()
            .ToUpdate<CancelInvoiceRequest, InvoiceResponse>()
            .ToGetById<GetInvoiceByIdQuery, InvoiceResponse>()
            .ToGetPaged<GetPagedInvoicesQuery, InvoiceResponse>(query => query.DefaultSort("CreatedAt"));
    }
}
```

Automations generate Pelican handlers at runtime from this declaration. You do not create `CreateInvoiceCommandHandler`, `UpdateInvoiceCommandHandler`, or `GetInvoiceByIdQueryHandler` for these happy paths.

### Hooks For Business Behavior

Create `Business/Invoices/Hooks/StampInvoiceBeforeSaveHook.cs`:

```csharp
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Domain;
using TurtlePath.Hooks;

namespace Billing.Service.Business.Invoices.Hooks;

public sealed class StampInvoiceBeforeSaveHook : IBeforeSaveHook<CreateInvoiceRequest, Invoice>
{
    public ValueTask BeforeSaveAsync(
        CommandHookContext<CreateInvoiceRequest, Invoice> context,
        CancellationToken cancellationToken)
    {
        context.Entity.CreatedAt = DateTimeOffset.UtcNow;
        context.Entity.Status = InvoiceStatus.Issued;
        context.Entity.Folio = $"INV-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        return ValueTask.CompletedTask;
    }
}
```

Create `Business/Invoices/Hooks/CancelInvoiceAfterMapHook.cs`:

```csharp
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Domain;
using TurtlePath.Exceptions;
using TurtlePath.Hooks;

namespace Billing.Service.Business.Invoices.Hooks;

public sealed class CancelInvoiceAfterMapHook : IAfterMapHook<CancelInvoiceRequest, Invoice>
{
    public ValueTask AfterMapAsync(
        CommandHookContext<CancelInvoiceRequest, Invoice> context,
        CancellationToken cancellationToken)
    {
        if (context.Entity.Status == InvoiceStatus.Canceled)
            throw new BadRequestException("Invoice is already canceled.");

        context.Entity.Status = InvoiceStatus.Canceled;
        context.Entity.CanceledAt = DateTimeOffset.UtcNow;

        return ValueTask.CompletedTask;
    }
}
```

### Controller

Create `Api/Controllers/InvoicesController.cs`:

```csharp
using Asp.Versioning;
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Business.Invoices.Models.Responses;
using Billing.Service.Business.Invoices.Queries;
using Microsoft.AspNetCore.Mvc;
using TurtlePath.Domain.Identifier;
using TurtlePath.Models.Responses;
using Payments.Api.Controllers;

namespace Billing.Service.Api.Controllers;

[ApiVersion("1.0")]
[Route("invoices")]
public sealed class InvoicesController : BaseController
{
    [HttpPost]
    public Task<InvoiceResponse> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
        => Mediator.Send(request, cancellationToken);

    [HttpPut("{id}")]
    public Task<InvoiceResponse> Update(
        [FromRoute] CId id,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        return Mediator.Send(request, cancellationToken);
    }

    [HttpPost("{id}/cancel")]
    public Task<InvoiceResponse> Cancel(
        [FromRoute] CId id,
        [FromBody] CancelInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        return Mediator.Send(request, cancellationToken);
    }

    [HttpGet("{id}")]
    public Task<InvoiceResponse> GetById(
        [FromRoute] CId id,
        CancellationToken cancellationToken)
        => Mediator.Send(new GetInvoiceByIdQuery { Id = id }, cancellationToken);

    [HttpGet]
    public Task<PagedResponse<InvoiceResponse>> GetPaged(
        [FromQuery] GetPagedInvoicesQuery query,
        CancellationToken cancellationToken)
        => Mediator.Send(query, cancellationToken);
}
```

That is the complete happy path: entity, EF configuration, requests, response, mapping, validation, filtering, automation, hooks, and controller.

## 7. Mapping With OctoMap

Use OctoMap profiles for request-to-entity, entity-to-response, and event projection mapping.

Recommended location:

```text
Invoices/
  Mappings/
    InvoiceMappingProfile.cs
```

Typical profile:

```csharp
public sealed class InvoiceMappingProfile : OctoMapProfile
{
    public override void Configure(IOctoMapConfigurationBuilder builder)
    {
        builder.CreateMap<CreateInvoiceRequest, Invoice>();
        builder.CreateMap<UpdateInvoiceRequest, Invoice>();
        builder.CreateMap<Invoice, InvoiceResponse>();
    }
}
```

Use explicit members when the public response differs from the entity:

```csharp
builder.CreateMap<Invoice, InvoiceResponse>()
    .ForMember(response => response.Status, map => map.MapFrom(invoice => invoice.Status.ToString()))
    .ForMember(response => response.CanCancel, map => map.MapFrom(invoice => invoice.Status == InvoiceStatus.Issued));
```

Use mapping for shape transformation. Do not hide business decisions in maps. If a value requires a service, a database lookup, or side effects, use a hook or a handler.

## 8. Validation With Crabalidator

Use Crabalidator validators for request validation.

Recommended location:

```text
Invoices/
  Validators/
    CreateInvoiceRequestValidator.cs
    UpdateInvoiceRequestValidator.cs
```

Example:

```csharp
public sealed class CreateInvoiceRequestValidator : CrabValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(request => request.CustomerId).Must(id => !id.IsEmpty);
        RuleFor(request => request.Amount).Must(amount => amount > 0m);
    }
}
```

Validation runs before mapping and before persistence. Use validators for input shape and basic business preconditions:

- required values
- length limits
- numeric ranges
- enum/state values received from the client
- valid `CId` values

Use handlers or hooks for rules that need loaded entities or external services.

## 9. Filtering And Paging With DataScorpio

Paged queries derive from `GetPagedInfoQuery<TEntity, TResponse>` and return `PagedResponse<TResponse>`:

```csharp
public sealed class GetPagedInvoicesQuery : GetPagedInfoQuery<Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQuery(PagedSettings pagedSettings)
        : base(pagedSettings)
    {
    }

    public CId? CustomerId { get; set; }
}
```

The generic paged handler or automation reads paging, filters, sorts, and search values from the request. DataScorpio applies the string criteria to the storage query.

Example allowed profile:

```csharp
public sealed class InvoiceQueryProfile : QueryProfile<Invoice>
{
    public override void Configure(IQueryProfileBuilder<Invoice> builder)
    {
        builder
            .AllowFilter(invoice => invoice.Folio)
            .AllowFilter(invoice => invoice.CustomerId)
            .AllowFilter(invoice => invoice.Status)
            .AllowSort(invoice => invoice.CreatedAt)
            .AllowSort(invoice => invoice.Amount)
            .AllowSearch(invoice => invoice.Folio);
    }
}
```

Example requests:

```http
GET /api/v1/invoices?page=1&pageSize=20
GET /api/v1/invoices?filters=Status==Issued
GET /api/v1/invoices?filters=Folio@=*INV-2026&sorts=-CreatedAt
GET /api/v1/invoices?search=ACME&sorts=Folio
```

Use typed query properties when the filter is part of the endpoint contract:

```csharp
public sealed class GetPagedInvoicesQuery : GetPagedInfoQuery<Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQuery(PagedSettings pagedSettings)
        : base(pagedSettings)
    {
    }

    public CId CustomerId { get; set; }
}
```

Configure an automation query when the happy path is enough. For complex mandatory filters, use a custom query handler and override query behavior in the `Custom Handlers` section.

### Advanced DataScorpio Profiles

Use aliases when the public query field should not expose the CLR property name:

```csharp
public sealed class InvoiceQueryProfile : QueryProfile<Invoice>
{
    public override void Configure(IQueryProfileBuilder<Invoice> builder)
    {
        builder
            .AllowFilter("folio", invoice => invoice.Folio)
            .AllowFilter("customer", invoice => invoice.CustomerId)
            .AllowSort("created", invoice => invoice.CreatedAt)
            .AllowSearch(invoice => invoice.Folio)
            .DefaultSort(invoice => invoice.CreatedAt, SortDirection.Descending)
            .MaxPageSize(100);
    }
}
```

Clients can then use API names instead of CLR names:

```http
GET /api/v1/invoices?filters=customer==01J7V8R7RXA6MG9A4R8ZNZ5E3P&sorts=-created
```

Use custom filters for business concepts that do not map cleanly to one field:

```csharp
public sealed class InvoiceQueryProfile : QueryProfile<Invoice>
{
    public override void Configure(IQueryProfileBuilder<Invoice> builder)
    {
        builder
            .AllowFilter(invoice => invoice.Status)
            .AllowFilter(invoice => invoice.DueDate)
            .AllowSort(invoice => invoice.CreatedAt)
            .CustomFilter("Overdue", (query, value) =>
            {
                var enabled = Convert.ToBoolean(value.Value);

                return enabled
                    ? query.Where(invoice =>
                        invoice.Status == InvoiceStatus.Issued &&
                        invoice.DueDate < DateTimeOffset.UtcNow)
                    : query;
            })
            .CustomFilterDescriptor("DueWindow", (query, filter) =>
            {
                var days = Convert.ToInt32(filter.Value.Value);
                var limit = DateTimeOffset.UtcNow.AddDays(days);

                return query.Where(invoice =>
                    invoice.Status == InvoiceStatus.Issued &&
                    invoice.DueDate <= limit);
            });
    }
}
```

Example requests:

```http
GET /api/v1/invoices?filters=Overdue==true
GET /api/v1/invoices?filters=DueWindow==15
```

Use custom sorts when sort meaning is business-specific:

```csharp
public sealed class InvoiceQueryProfile : QueryProfile<Invoice>
{
    public override void Configure(IQueryProfileBuilder<Invoice> builder)
    {
        builder
            .AllowSort(invoice => invoice.CreatedAt)
            .CustomSort("Priority", (query, direction) =>
                direction == SortDirection.Descending
                    ? query
                        .OrderByDescending(invoice => invoice.Status == InvoiceStatus.Overdue)
                        .ThenByDescending(invoice => invoice.Amount)
                        .ThenBy(invoice => invoice.DueDate)
                    : query
                        .OrderBy(invoice => invoice.Status == InvoiceStatus.Overdue)
                        .ThenBy(invoice => invoice.Amount)
                        .ThenBy(invoice => invoice.DueDate));
    }
}
```

Example:

```http
GET /api/v1/invoices?sorts=-Priority
```

Use global conventions when several entities share query behavior:

```csharp
public interface ITenantScoped
{
    string TenantId { get; }
}

public interface IAuditedEntity
{
    DateTimeOffset CreatedAt { get; }
}

public sealed class AppQueryConventions : QueryConventionSet
{
    public override void Configure(IQueryConventionBuilder builder)
    {
        builder
            .CustomFilter<ITenantScoped>("ForTenant", value =>
                entity => entity.TenantId == Convert.ToString(value.Value))
            .CustomSort<IAuditedEntity>("RecentlyCreated", entity => entity.CreatedAt);
    }
}
```

Place query profiles and conventions under the feature, usually in `Business/Invoices/Querying`. The template default already discovers DataScorpio profiles and conventions from the application assembly:

```csharp
services.AddTurtlePath(typeof(Constants).Assembly)
    .UseDataScorpio(profiles => profiles.FromAssembly(typeof(Constants).Assembly));
```

Do not register each query profile one by one. Add the class to the assembly and discovery picks it up. Any profile whose entity implements the matching convention contract receives the custom filter or sort name automatically.

## 10. Automations

Automations generate handlers from declarations. Use them when the operation follows TurtlePath's standard path:

- validate request
- map request to entity or load entity
- apply hooks
- save/delete
- map entity to response
- apply query filters and paging for reads

### Fluent Profile

Recommended for real features:

```csharp
public sealed class InvoiceAutomationProfile : TurtlePathAutomationProfile
{
    public override void Configure(ITurtlePathAutomationBuilder builder)
    {
        builder.For<Invoice>()
            .ToCreate<CreateInvoiceRequest, InvoiceResponse>()
            .ToUpdate<UpdateInvoiceRequest, InvoiceResponse>()
            .ToDelete<DeleteInvoiceRequest>()
            .ToPatch<PatchInvoiceEmailRequest, InvoiceResponse>()
            .ToGetById<GetInvoiceByIdQuery, InvoiceResponse>()
            .ToGetPaged<GetPagedInvoicesQuery, InvoiceResponse>(query => query.DefaultSort("CreatedAt"));
    }
}
```

Use `builder.For<TEntity>()` for recommended TurtlePath entities using `BaseEntity` and `CId`.

Use `builder.For<TEntity, TKey>()` for legacy entities:

```csharp
builder.For<LegacyShipment, int>()
    .ToCreate<CreateLegacyShipmentRequest, LegacyShipmentResponse>()
    .ToGetById<GetLegacyShipmentByIdQuery, LegacyShipmentResponse>(query => query.GetKeyFrom(x => x.Id));
```

### Attribute Automations

Attributes are useful for small flows:

```csharp
[CreateAutomation(typeof(Invoice), typeof(InvoiceResponse))]
public sealed class CreateInvoiceRequest : IRequest<InvoiceResponse>
{
    public CId CustomerId { get; set; }

    public decimal Amount { get; set; }
}
```

Available attributes:

- `CreateAutomationAttribute`
- `UpdateAutomationAttribute`
- `DeleteAutomationAttribute`
- `PatchAutomationAttribute`
- `GetByIdAutomationAttribute`
- `GetManyAutomationAttribute`
- `GetPagedAutomationAttribute`

Prefer profiles when a feature has several operations. Profiles keep the feature automation map in one place.

### When Not To Use Automations

Create a handler instead when:

- the operation touches several aggregates
- the request writes to external services before persistence
- the operation needs a custom transaction shape
- the response is built from several projections
- the business flow would be hard to understand through hooks

## 11. Custom Handlers

Use custom handlers when the control flow matters. Commands and queries both have manual handler paths.

### Base Classes Reference

Use the non-generic base classes for the recommended TurtlePath model: entities inherit `BaseEntity`, ids use `CId`, and responses inherit `BaseResponse`.

Command handlers with response:

- Create: `CreateCommandHandler<TRequest, TResponse, TEntity>`
- Update: `UpdateCommandHandler<TRequest, TResponse, TEntity>`
- Delete: `DeleteCommandHandler<TRequest, TResponse, TEntity>`
- Patch: `PatchCommandHandler<TRequest, TResponse, TEntity>`

Command handlers without response:

- Create: `CreateCommandHandler<TRequest, TEntity>`
- Update: `UpdateCommandHandler<TRequest, TEntity>`
- Delete: `DeleteCommandHandler<TRequest, TEntity>`
- Patch: `PatchCommandHandler<TRequest, TEntity>`

Query messages and handlers:

- Get by id message: `GetByIdQuery<TEntity, TResponse>`
- Get by id handler: `GetByIdQueryHandler<TQuery, TEntity, TResponse>`
- Get one by a non-id value message: `GetOneQuery<TValue, TEntity, TResponse>`
- Get one by a non-id value handler: `GetOneQueryHandler<TQuery, TValue, TEntity, TResponse>`
- Get many message: `GetManyQuery<TEntity, TResponse>`
- Get many handler: `GetManyQueryHandler<TQuery, TEntity, TResponse>`
- Get all: use `GetManyQuery<TEntity, TResponse>` without required filters
- Get paged message: `GetPagedInfoQuery<TEntity, TResponse>`
- Get paged handler: `GetPagedInfoQueryHandler<TQuery, TEntity, TResponse>`

Use the generic base classes only when a legacy/custom entity implements `IEntity<TKey>` without using `BaseEntity` and `CId`:

- `GenericCreateCommandHandler<TRequest, TResponse, TEntity, TKey>`
- `GenericCreateCommandHandler<TRequest, TEntity, TKey>`
- `GenericUpdateCommandHandler<TRequest, TResponse, TEntity, TKey>`
- `GenericUpdateCommandHandler<TRequest, TEntity, TKey>`
- `GenericDeleteCommandHandler<TRequest, TResponse, TEntity, TKey>`
- `GenericDeleteCommandHandler<TRequest, TEntity, TKey>`
- `GenericPatchCommandHandler<TRequest, TResponse, TEntity, TKey>`
- `GenericPatchCommandHandler<TRequest, TEntity, TKey>`
- `GenericGetByIdQuery<TEntity, TResponse, TKey>`
- `GenericGetByIdQueryHandler<TQuery, TEntity, TResponse, TKey>`
- `GenericGetOneQuery<TValue, TEntity, TResponse, TKey>`
- `GenericGetOneQueryHandler<TQuery, TValue, TEntity, TResponse, TKey>`
- `GenericGetManyQuery<TEntity, TResponse, TKey>`
- `GenericGetManyQueryHandler<TQuery, TEntity, TResponse, TKey>`
- `GenericGetPagedInfoQuery<TEntity, TResponse, TKey>`
- `GenericGetPagedInfoQueryHandler<TQuery, TEntity, TResponse, TKey>`

### Command Handlers

The recommended command handlers depend on `BaseEntity` and `CId`:

```csharp
public sealed class CreateInvoiceCommandHandler
    : CreateCommandHandler<CreateInvoiceRequest, InvoiceResponse, Invoice>
{
    public CreateInvoiceCommandHandler(IServiceProvider services) : base(services)
    {
    }
}
```

Use the no-response overload when the command only needs to complete successfully:

```csharp
public sealed class DeleteInvoiceCommandHandler
    : DeleteCommandHandler<DeleteInvoiceRequest, Invoice>
{
    public DeleteInvoiceCommandHandler(IServiceProvider services) : base(services)
    {
    }
}
```

Use generic handlers for legacy keys:

```csharp
public sealed class CreateLegacyShipmentCommandHandler
    : GenericCreateCommandHandler<CreateLegacyShipmentRequest, LegacyShipmentResponse, LegacyShipment, int>
{
    public CreateLegacyShipmentCommandHandler(IServiceProvider services) : base(services)
    {
    }
}
```

### Virtual Methods On Command Handlers

Use virtual methods for handler-specific customization. Use hooks for reusable behavior.

Create handlers:

- `ValidateRequest`
- `UseProjectionFromStorage`
- `ValidateAsync(request, cancellationToken)`
- `MapToEntityAsync(request, cancellationToken)`
- `SaveEntityAsync(request, entity, cancellationToken)`
- `MapToResponseAsync(request, entity, cancellationToken)`

Update handlers:

- `ValidateRequest`
- `UseProjectionFromStorage`
- `GetEntityAsync(request, cancellationToken)`
- `ValidateAsync(request, entity, cancellationToken)`
- `MapEntityAsync(request, entity, cancellationToken)`
- `UpdateEntityAsync(request, entity, cancellationToken)`
- `MapToResponseAsync(request, entity, cancellationToken)`

Delete handlers:

- `ValidateRequest`
- `GetEntityAsync(request, cancellationToken)`
- `ValidateAsync(request, entity, cancellationToken)`
- `DeleteEntityAsync(entity, cancellationToken)`
- `BuildResponseAsync(request, entity, cancellationToken)`

Patch handlers:

- `ValidateRequest`
- `GetEntityAsync(request, cancellationToken)`
- `ValidateAsync(request, entity, cancellationToken)`
- `PatchEntityAsync(request, entity, cancellationToken)`
- `UpdateEntityAsync(request, entity, cancellationToken)`
- `BuildResponseAsync(request, entity, cancellationToken)`

### Query Handlers

Use query handlers when the endpoint needs mandatory filters, custom lookup rules, or feature-specific query behavior that should not be expressed as a public DataScorpio filter.

Get-by-id query handlers:

```csharp
public sealed class GetCustomerByIdQuery : GetByIdQuery<Customer, CustomerResponse>
{
    public GetCustomerByIdQuery(CId id)
        : base(id)
    {
    }
}

public sealed class GetCustomerByIdQueryHandler
    : GetByIdQueryHandler<GetCustomerByIdQuery, Customer, CustomerResponse>
{
    public GetCustomerByIdQueryHandler(IServiceProvider services)
        : base(services)
    {
    }
}
```

Paged query handlers:

```csharp
public sealed class GetPagedInvoicesQuery : GetPagedInfoQuery<Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQuery(PagedSettings pagedSettings)
        : base(pagedSettings)
    {
    }

    public CId? CustomerId { get; set; }
}

public sealed class GetPagedInvoicesQueryHandler
    : GetPagedInfoQueryHandler<GetPagedInvoicesQuery, Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQueryHandler(IServiceProvider services)
        : base(services)
    {
    }
}
```

Use generic query handlers when an entity implements `IEntity<TKey>` without using `BaseEntity` and `CId`:

```csharp
public sealed class GetPagedLegacyShipmentsQuery
    : GenericGetPagedInfoQuery<LegacyShipment, LegacyShipmentResponse, int>
{
    public GetPagedLegacyShipmentsQuery(PagedSettings pagedSettings)
        : base(pagedSettings)
    {
    }
}

public sealed class GetPagedLegacyShipmentsQueryHandler
    : GenericGetPagedInfoQueryHandler<GetPagedLegacyShipmentsQuery, LegacyShipment, LegacyShipmentResponse, int>
{
    public GetPagedLegacyShipmentsQueryHandler(IServiceProvider services)
        : base(services)
    {
    }
}
```

### Virtual Methods On Query Handlers

Get-by-id query handlers:

- `Handle(request, cancellationToken)`
- `GetFilterExpression(request)`

Paged query handlers:

- `DefaultSorts`
- `GetFiltersExpression(request)`
- `GetSortingExpression(request)`

Override `Handle` only when the entire query flow changes. Prefer `GetFilterExpression`, `GetFiltersExpression`, `GetSortingExpression`, or `DefaultSorts` when the standard storage, hooks, paging, projection, and response path still applies.

### Command Examples

Disable validation for an internal command:

```csharp
public sealed class RecalculateInvoiceTotalsCommandHandler
    : UpdateCommandHandler<RecalculateInvoiceTotalsRequest, InvoiceResponse, Invoice>
{
    public RecalculateInvoiceTotalsCommandHandler(IServiceProvider services) : base(services)
    {
    }

    protected override bool ValidateRequest => false;
}
```

Use a custom lookup:

```csharp
protected override async Task<Invoice> GetEntityAsync(
    CancelInvoiceRequest request,
    CancellationToken cancellationToken)
{
    var invoice = await StorageReaderAdapter
        .For<Invoice>()
        .Where(x => x.Id == request.Id && x.Status == InvoiceStatus.Issued)
        .FirstOrDefaultAsync(cancellationToken);

    return invoice ?? throw new NotFoundException(nameof(Invoice), request.Id.ToString());
}
```

Build a response from storage after save:

```csharp
protected override bool UseProjectionFromStorage => true;
```

Use this when EF-generated values, triggers, computed columns, or includes are needed in the response.

### Query Examples

Override a paged query when the endpoint needs mandatory filters or feature-specific query behavior:

```csharp
using System.Linq.Expressions;
using Billing.Service.Domain;
using Billing.Service.Business.Invoices.Models.Responses;
using TurtlePath.Domain.Identifier;
using TurtlePath.Queries;

namespace Billing.Service.Business.Invoices.Queries;

public sealed class GetPagedInvoicesQuery : GetPagedInfoQuery<Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQuery(PagedSettings pagedSettings)
        : base(pagedSettings)
    {
    }

    public CId? CustomerId { get; set; }
}

public sealed class GetPagedInvoicesQueryHandler
    : GetPagedInfoQueryHandler<GetPagedInvoicesQuery, Invoice, InvoiceResponse>
{
    public GetPagedInvoicesQueryHandler(IServiceProvider services)
        : base(services)
    {
    }

    protected override string DefaultSorts => "-CreatedAt";

    protected override Expression<Func<Invoice, bool>> GetFiltersExpression(GetPagedInvoicesQuery query)
    {
        if (query.CustomerId is null)
            return invoice => invoice.Status != InvoiceStatus.Canceled;

        var customerId = query.CustomerId.Value;

        return invoice =>
            invoice.CustomerId == customerId &&
            invoice.Status != InvoiceStatus.Canceled;
    }
}
```

## 12. Hooks

Hooks are the best extension point when the standard handler path is still correct.

Command hook stages:

- `IBeforeValidationHook<TRequest, TEntity>`
- `IAfterValidationHook<TRequest, TEntity>`
- `IBeforeGetEntityHook<TRequest, TEntity>`
- `IAfterGetEntityHook<TRequest, TEntity>`
- `IBeforeMapHook<TRequest, TEntity>`
- `IAfterMapHook<TRequest, TEntity>`
- `IBeforePatchHook<TRequest, TEntity>`
- `IAfterPatchHook<TRequest, TEntity>`
- `IBeforeSaveHook<TRequest, TEntity>`
- `IAfterSaveHook<TRequest, TEntity>`
- `IBeforeDeleteHook<TRequest, TEntity>`
- `IAfterDeleteHook<TRequest, TEntity>`
- `IBeforeResponseHook<TRequest, TEntity, TResponse>`
- `IAfterResponseHook<TRequest, TEntity, TResponse>`

Query hook stages:

- `IBeforeQueryHook<TQuery, TResult>`
- `IAfterQueryHook<TQuery, TResult>`

Use `IOrderedHook` when several hooks run on the same stage:

```csharp
public sealed class NormalizeInvoiceBeforeValidationHook
    : IBeforeValidationHook<CreateInvoiceRequest, Invoice>, IOrderedHook
{
    public int Order => 0;

    public ValueTask BeforeValidationAsync(
        CommandHookContext<CreateInvoiceRequest, Invoice> context,
        CancellationToken cancellationToken)
    {
        context.Request.Folio = context.Request.Folio?.Trim();
        return ValueTask.CompletedTask;
    }
}
```

Publish an event after persistence:

```csharp
public sealed class PublishInvoiceCreatedAfterSaveHook
    : IAfterSaveHook<CreateInvoiceRequest, Invoice>
{
    private readonly ISpider spider;

    public PublishInvoiceCreatedAfterSaveHook(ISpider spider)
    {
        this.spider = spider;
    }

    public ValueTask AfterSaveAsync(
        CommandHookContext<CreateInvoiceRequest, Invoice> context,
        CancellationToken cancellationToken)
    {
        return new ValueTask(spider.Send(new InvoiceCreatedEvent(context.Entity.Id), cancellationToken));
    }
}
```

Use hooks for:

- id assignment
- audit fields
- request normalization
- response enrichment
- publishing messages after save
- simple business rules at a known stage

Avoid hooks when the feature cannot be understood without chasing many files. In that case, use a custom handler.

## 13. Controllers And REST Routes

Controllers inherit from `BaseController`, which exposes `Mediator` and `Spider`.

Use plural resource names:

- `CustomersController`
- `InvoicesController`
- `OrdersController`

Recommended routes:

- `POST /customers`
- `PUT /customers/{id}`
- `DELETE /customers/{id}`
- `GET /customers`
- `GET /customers/{id}`
- `POST /customers/{id}/deactivate`
- `GET /customers/{id}/orders`
- `DELETE /orders/{id}/details/{detailId}`

Complete example:

```csharp
[ApiVersion("1.0")]
[Route("customers")]
public sealed class CustomersController : BaseController
{
    [HttpPost]
    public Task<CustomerResponse> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
        => Mediator.Send(request, cancellationToken);

    [HttpPut("{id}")]
    public Task<CustomerResponse> Update(
        [FromRoute] CId id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        return Mediator.Send(request, cancellationToken);
    }

    [HttpGet]
    public Task<PagedResponse<CustomerResponse>> GetPaged(
        [FromQuery] GetPagedCustomersQuery query,
        CancellationToken cancellationToken)
        => Mediator.Send(query, cancellationToken);

    [HttpGet("{id}")]
    public Task<CustomerResponse> GetById(
        [FromRoute] CId id,
        CancellationToken cancellationToken)
        => Mediator.Send(new GetCustomerByIdQuery { Id = id }, cancellationToken);

    [HttpPost("{id}/deactivate")]
    public Task<CustomerResponse> Deactivate(
        [FromRoute] CId id,
        CancellationToken cancellationToken)
        => Spider.DefaultSend<DeactivateCustomerRequest, CustomerResponse>(
            new DeactivateCustomerRequest { Id = id },
            cancellationToken);
}
```

Use `Mediator.Send` for normal in-process request dispatch. Use `Spider.DefaultSend<TRequest, TResponse>` from `TurtlePath.Spider` when the request should run through Spider boundaries explicitly. TurtlePath owns this bridge so Spider and Pelican do not need to reference each other.

## 14. Spider Pipelines And Transactions

The template uses Spider execution boundaries for cross-cutting execution behavior. The default boundary is transaction handling.

Registration:

```csharp
services.AddTurtlePathSpiderTransactions(
    configuration,
    typeof(Constants).Assembly,
    typeof(PipelineExtensions).Assembly);
```

The `TurtlePath.Spider.Transactions` package discovers request types and transaction profiles from the assemblies passed to the registration call. The template passes the Business assembly and the API assembly, so feature-specific profiles can live in Business without referencing API.

Configuration:

```json
"TransactionBoundary": {
  "Enabled": true,
  "IncludeQueries": false,
  "IsolationLevel": "ReadCommitted",
  "TimeoutSeconds": 30,
  "ExcludedRequestTypes": []
}
```

Default behavior:

- mutations run inside `TransactionScope`
- queries are skipped unless `IncludeQueries` is enabled
- `[SkipTransactionBoundary]` skips the transaction
- configured excluded request types are skipped
- `TransactionBoundaryProfile` classes can add exclusions or request discovery by code
- request type decisions are discovered once and cached

Skip a request:

```csharp
[SkipTransactionBoundary]
public sealed class RebuildSearchIndexRequest : IRequest
{
}
```

Prefer a profile when a feature or module has several transaction rules. Put the profile close to the feature, for example `Business/Search/Boundaries/Transactions/SearchTransactionBoundaryProfile.cs`:

```csharp
using TurtlePath.Spider.Transactions;

namespace Payments.Business.Search.Boundaries.Transactions;

public sealed class SearchTransactionBoundaryProfile : TransactionBoundaryProfile
{
    public override void Configure(TransactionBoundaryOptions options)
    {
        options.Exclude<RebuildSearchIndexRequest>();
        options.DiscoverRequestsFrom<RebuildSearchIndexRequest>();
    }
}
```

The `TurtlePath.Spider.Transactions` package discovers transaction boundary profiles from the assemblies passed to `AddTurtlePathSpiderTransactions`. Do not edit `AddPipelineDefaults` for feature-specific transaction rules.

Use Spider when a flow must go through execution boundaries. The controller base and hub consumer base expose `Spider` for that reason.

## 15. Pigeon Consumers And Outbox

The template includes Pigeon with Azure Service Bus and EF Core outbox as an opt-in messaging stack. It is not registered by default because Azure Service Bus requires a real connection string.

To enable it, configure the `Pigeon` section with real broker values and uncomment the `.AddMessagingDefaults(configuration)` line in `AddDefaults`.

The prepared registration is:

```csharp
services.AddPigeon(configuration, builder =>
{
    builder.ConfigurePublishing(publishing =>
    {
        publishing.AmbientTransactionBehavior = AmbientTransactionPublishBehavior.SuppressTransaction;
    });

    builder.UseAzureServiceBus();
    builder.UseEntityFrameworkOutbox<AppDbContext>(outbox =>
    {
        outbox.Enabled = true;
        outbox.SchemaMode = OutboxSchemaMode.AutoCreate;
        configuration.GetSection("Pigeon:Outbox").Bind(outbox);
    });
});
```

When Pigeon is enabled, the outbox defaults persist messages with the database transaction and dispatch them after commit.

### Control consumer throughput and concurrency

Pigeon 2.4.0 lets you protect downstream resources by configuring consumer execution. `MaxConcurrency` is the maximum number of messages that Pigeon dispatches to handlers at the same time. `QueueCapacity` is the number of messages that can wait in the internal dispatch queue while those handlers are busy:

```csharp
services.AddPigeon(configuration, builder =>
{
    builder.ConfigureConsumerExecution(execution =>
    {
        execution.MaxConcurrency = 32;
        execution.QueueCapacity = 256;
        execution.HandlerTimeout = TimeSpan.FromSeconds(30);
    });

    builder.UseAzureServiceBus();
});
```

Use `MaxConcurrency` to match the capacity of the slowest dependency used by the consumer, such as a database connection pool or a downstream HTTP API. Use `QueueCapacity` to absorb short bursts without allowing unbounded in-memory growth. A value that is `null` or less than `1` leaves that limit unbounded. When `MaxConcurrency` is explicitly set, broker adapters can use it to align delivery or prefetch behavior.

This setting controls handler dispatch, not outbox batch size. Configure `Pigeon:Outbox:DispatchBatchSize` separately when tuning how many persisted messages are published in one outbox cycle. Start with a conservative concurrency value, observe processing latency and failures, and increase it only when the downstream dependencies can sustain the additional parallel work.

Consumer example:

```csharp
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Business.Invoices.Models.Responses;
using Pigeon.Messaging.Consuming;
using TurtlePath.Spider;
using Payments.Api.HubConsumers;

namespace Billing.Service.Api.HubConsumers;

public sealed class InvoicesHubConsumer : BaseHubConsumer
{
    public Task Consume(InvoiceIssuedMessage message, CancellationToken cancellationToken)
    {
        return ConsumerExceptionBoundary.RunAsync(
            token => Spider.DefaultSend<CreateInvoiceRequest, InvoiceResponse>(
                new CreateInvoiceRequest
                {
                    CustomerId = message.CustomerId,
                    Amount = message.Amount
                },
                token),
            Context,
            cancellationToken);
    }
}

public sealed record InvoiceIssuedMessage(CId CustomerId, decimal Amount);
```

Use `BaseHubConsumer` because it exposes:

- `Mediator`
- `Spider`
- `ConsumerExceptionBoundary`

Use consumers for integration messages. Keep business behavior in requests, handlers, automations, and hooks.

## 16. Event Sourcing

The template includes `TurtlePath.EventSourcing` and `Krackend.EventSourcing.EntityFrameworkCore` as an opt-in event sourcing stack. It is not registered by default because event sourcing needs deliberate stream names, event schemas, expected-version rules, and database tables.

Use Event Sourcing when the service must keep an append-only history of domain transitions. Examples:

- an invoice was created, authorized, paid, canceled, or reissued
- an order changed status
- a customer risk profile changed
- a business transition must be replayable or auditable

Do not enable Event Sourcing just to notify another service. For integration messages, use Pigeon with the EF outbox.

### Folder Shape

Put the event sourcing files inside the feature that owns the events:

```text
Business/
  Invoices/
    Commands/
      CreateInvoiceRequest.cs
      UpdateInvoiceRequest.cs
    EventSourcing/
      InvoiceEventSourcingProfile.cs
      InvoiceEventSource.cs
      InvoiceCreated.cs
      InvoiceUpdated.cs
      InvoiceCanceled.cs
    Mappings/
      InvoiceMappingProfile.cs
    Models/
      Requests/
      Responses/
```

The profile describes when events are appended. Event payload records describe what is stored. Mapping profiles describe how TurtlePath turns a source object into an event payload.

To enable it:

1. Create one or more `IEventSourcingProfile` implementations in the feature that owns the events.
2. Add event payload contracts beside the profile, usually in `Business/<Feature>/EventSourcing`.
3. Add OctoMap maps for any source-to-event projections.
4. Uncomment `.UseEventSourcingProfiles(typeof(Constants).Assembly)` in `AddApplicationDefaults`.
5. Uncomment `.AddEventSourcingDefaults()` in `AddDefaults`.
6. Add an EF Core migration so the event store tables are created.

### Event Payloads

Keep event payloads small, explicit, and version-friendly. Avoid storing full entity graphs.

```csharp
namespace Billing.Service.Business.Invoices.EventSourcing;

public sealed record InvoiceCreated(
    string InvoiceId,
    string CustomerId,
    decimal Total,
    string Currency,
    DateTimeOffset OccurredAt);

public sealed record InvoiceUpdated(
    string InvoiceId,
    decimal Total,
    string Currency,
    DateTimeOffset OccurredAt);

public sealed record InvoiceCanceled(
    string InvoiceId,
    string Reason,
    DateTimeOffset OccurredAt);
```

When the event needs data from both the command and the saved entity, create a small source model:

```csharp
namespace Billing.Service.Business.Invoices.EventSourcing;

public sealed record InvoiceEventSource(
    string InvoiceId,
    string CustomerId,
    decimal Total,
    string Currency,
    string Reason,
    DateTimeOffset OccurredAt);
```

### Mapping Events With OctoMap

`TurtlePath.EventSourcing` uses the configured `IMapperAdapter`. In this template that means OctoMap.

```csharp
using Billing.Service.Business.Invoices.EventSourcing;
using OctoMap;

namespace Billing.Service.Business.Invoices.Mappings;

public sealed class InvoiceEventMappingProfile : OctoMapProfile
{
    public override void Configure(IOctoMapConfigurationBuilder builder)
    {
        builder.CreateMap<InvoiceEventSource, InvoiceCreated>();
        builder.CreateMap<InvoiceEventSource, InvoiceUpdated>();
        builder.CreateMap<InvoiceEventSource, InvoiceCanceled>();
    }
}
```

Prepared TurtlePath registration:

```csharp
services.AddTurtlePath(typeof(Constants).Assembly)
    .UseAutomations(typeof(Constants).Assembly)
    .UseOctoMap()
    .UseCrabalidator()
    .UseDataScorpio(profiles => profiles.FromAssembly(typeof(Constants).Assembly))
    .UseEventSourcingProfiles(typeof(Constants).Assembly)
    .UseCId<Ulid, string>(config =>
    {
        config.DefaultFactory = () => CId.From(Ulid.NewUlid());
    })
    .UseCIdProfiles(typeof(DomainConstants).Assembly)
    .UseEntityFrameworkCore<AppDbContext>();
```

### Register The EF Event Store

The template includes `EventSourcingExtensions.cs` ready to use:

```csharp
internal static IServiceCollection AddEventSourcingDefaults(this IServiceCollection services)
{
    services.AddKrackendEntityFrameworkEventStore<AppDbContext>();

    return services;
}
```

Enable it in `AddDefaults` only when the service has event streams:

```csharp
return services
    .AddMvcDefaults()
    .AddOpenApiDefaults()
    .AddHealthCheckDefaults(configuration)
    .AddPersistenceDefaults(configuration)
    .AddApplicationDefaults()
    .AddEventSourcingDefaults()
    .AddPipelineDefaults(configuration)
    .AddCustomContainer(configuration);
```

### Create The Profile

```csharp
using Krackend.EventSourcing.Stores;
using TurtlePath.EventSourcing;

namespace Billing.Service.Business.Invoices.EventSourcing;

public sealed class InvoiceEventSourcingProfile : IEventSourcingProfile
{
    public void Configure(IEventSourcingProfileBuilder builder)
    {
        builder.For<CreateInvoiceRequest, Invoice>()
            .UseStream("invoices", context => context.Entity.Id.ToString())
            .ToEvent<InvoiceEventSource, InvoiceCreated>(
                ToSource,
                options => options.UseExpectedVersion(ExpectedVersion.NoStream));

        builder.For<UpdateInvoiceRequest, Invoice>()
            .UseStream("invoices", context => context.Entity.Id.ToString())
            .ToEvent<InvoiceEventSource, InvoiceUpdated>(
                ToSource);

        builder.For<CancelInvoiceRequest, Invoice>()
            .UseStream("invoices", context => context.Entity.Id.ToString())
            .ToEvent<InvoiceEventSource, InvoiceCanceled>(
                ToSource,
                options => options.When(context => context.Entity.Canceled));
    }

    private static InvoiceEventSource ToSource<TRequest>(CommandHookContext<TRequest, Invoice> context)
        where TRequest : class
    {
        return new InvoiceEventSource(
            context.Entity.Id.ToString(),
            context.Entity.CustomerId.ToString(),
            context.Entity.Total,
            context.Entity.Currency,
            context.Request is CancelInvoiceRequest cancel ? cancel.Reason : string.Empty,
            DateTimeOffset.UtcNow);
    }
}
```

`UseStream("invoices", ...)` chooses the logical stream and stream id. `ToEvent<TSource, TEvent>(...)` maps the command/entity hook context to a source object, then uses OctoMap to create the final event payload.

Use expected versions intentionally:

```csharp
options.UseExpectedVersion(ExpectedVersion.NoStream); // first event in a stream
options.UseExpectedVersion(ExpectedVersion.Any);      // append without optimistic concurrency
```

Use `When(...)` when an event is conditional:

```csharp
.ToEvent<InvoiceEventSource, InvoiceCanceled>(
    ToSource,
    options => options.When(context => context.Entity.Canceled));
```

### How It Runs

Event Sourcing runs through TurtlePath command handler hooks:

1. Pelican sends the command.
2. TurtlePath creates or updates the entity.
3. The handler saves the entity through EF Core.
4. `EventSourcingAfterSaveHook` runs after save.
5. The hook resolves the stream and maps the command/entity context to event payloads.
6. Krackend appends the events through `IEventStore`.

This means automations and base command handlers can emit events without custom handler code. If the happy path is enough, add the Event Sourcing profile and keep the handler generated. If the business flow is special, create a custom handler and the same hooks still apply after save.

### EF Migration

After enabling `.AddEventSourcingDefaults()`, add a migration so EF creates the Krackend event store tables:

```powershell
dotnet ef migrations add AddEventSourcingStore `
  --project src/Billing.Service.Persistence `
  --startup-project src/Billing.Service.Api

dotnet ef database update `
  --project src/Billing.Service.Persistence `
  --startup-project src/Billing.Service.Api
```

### Testing Event Sourcing

For integration tests, enable the same registrations in the test host and assert that the command appends events.

```csharp
await using var host = await TemplateTestHost.CreateAsync(services =>
{
    services.AddEventSourcingDefaults();
});

var mediator = host.Services.GetRequiredService<IMediator>();

var response = await mediator.Send(new CreateInvoiceRequest
{
    CustomerId = customerId,
    Total = 1250m,
    Currency = "USD"
});

var eventStore = host.Services.GetRequiredService<IEventStore>();
var stream = await eventStore.ReadStreamAsync("invoices", response.Id.ToString());

stream.Events.Should().ContainSingle(e => e.Payload is InvoiceCreated);
```

## 17. Exception Handling

TurtlePath exception handling is transport-neutral. The core creates an `ExceptionDescriptor`:

- `Kind`
- `Code`
- `Messages`
- `Metadata`
- `TraceIdentifier`

HTTP, consumers, and workers decide how to represent that descriptor for their target.

The template registers default mappings:

```csharp
services.AddTurtlePathExceptionHandlingCore(builder =>
{
    builder.For<ValidationException>(
        _ => ExceptionKind.Validation,
        exception => "validation",
        exception => exception.Errors);

    builder.For<BadRequestException>(ExceptionKind.Validation, exception => exception.Message);
    builder.For<ForbiddenException>(ExceptionKind.Forbidden, exception => exception.Message);
    builder.For<NotFoundException>(ExceptionKind.NotFound, exception => exception.Message);
    builder.For<UnauthorizedException>(ExceptionKind.Unauthorized, exception => exception.Message);
});

services.AddTurtlePathAspNetCoreExceptionHandling();
services.AddTurtlePathConsumerExceptionHandling();
services.AddTurtlePathWorkerExceptionHandling();
```

Do not register the TurtlePath exception adapters again from `CustomContainer`. The template already calls:

```csharp
services.AddTurtlePathExceptionHandlingCore(...);
services.AddTurtlePathAspNetCoreExceptionHandling(...);
services.AddTurtlePathConsumerExceptionHandling(...);
services.AddTurtlePathWorkerExceptionHandling(...);
```

Service-specific exceptions are added with profiles. The template discovers exception profiles automatically from the Business and API assemblies, so a generated service only needs to add profile classes.

Use a core profile to describe the exception once:

```csharp
using TurtlePath.ExceptionHandling;

namespace Billing.Service.Business.Subscriptions.Exceptions;

public static class SubscriptionExceptionKinds
{
    public static readonly ExceptionKind SubscriptionExpired = new("subscription_expired");
}

public sealed class SubscriptionExpiredException : Exception
{
    public SubscriptionExpiredException(string customerId)
        : base($"Customer '{customerId}' has an expired subscription.")
    {
        CustomerId = customerId;
    }

    public string CustomerId { get; }
}

public sealed class SubscriptionExceptionProfile : ExceptionHandlingProfile
{
    public override void Configure(ExceptionHandlingOptionsBuilder builder)
    {
        builder.For<SubscriptionExpiredException>(
            SubscriptionExceptionKinds.SubscriptionExpired,
            exception => $"Subscription expired for customer '{exception.CustomerId}'.");
    }
}
```

Use an HTTP profile when the API should return a specific HTTP status:

```csharp
using Microsoft.AspNetCore.Http;
using TurtlePath.ExceptionHandling.AspNetCore;

namespace Billing.Service.Business.Subscriptions.Exceptions;

public sealed class SubscriptionHttpExceptionProfile : HttpExceptionHandlingProfile
{
    public override void Configure(HttpExceptionHandlingOptionsBuilder builder)
    {
        builder.Map(SubscriptionExceptionKinds.SubscriptionExpired, StatusCodes.Status403Forbidden);
    }
}
```

Use a consumer profile when message handling should complete, rethrow, retry through the broker, or apply a specific reporting strategy:

```csharp
using TurtlePath.ExceptionHandling;
using TurtlePath.ExceptionHandling.Consumers;

namespace Billing.Service.Business.Subscriptions.Exceptions;

public sealed class SubscriptionConsumerExceptionProfile : ConsumerExceptionHandlingProfile
{
    public override void Configure(ConsumerExceptionHandlingOptionsBuilder builder)
    {
        builder.RethrowWhen((descriptor, context) =>
            descriptor.Kind != SubscriptionExceptionKinds.SubscriptionExpired);
    }
}
```

With this profile, `SubscriptionExpiredException` is handled and completed by the consumer boundary, while other exceptions are rethrown so the broker can apply its normal retry or dead-letter behavior.

Use a worker profile when jobs or background services should behave differently:

```csharp
using TurtlePath.ExceptionHandling;
using TurtlePath.ExceptionHandling.Workers;

namespace Billing.Service.Business.Subscriptions.Exceptions;

public sealed class SubscriptionWorkerExceptionProfile : BackgroundExceptionHandlingProfile
{
    public override void Configure(BackgroundExceptionHandlingOptionsBuilder builder)
    {
        builder.RethrowWhen(descriptor =>
            descriptor.Kind == ExceptionKind.Transient);
    }
}
```

HTTP uses `ProblemDetails` through `GlobalExceptionFilter`. Consumers use `IConsumerExceptionBoundary`. Jobs use `IBackgroundExceptionBoundary` through the TurtlePath job executor.

Then throw the domain-specific exception from a service, hook, automation action, or manual handler:

```csharp
if (!subscription.IsActive)
    throw new SubscriptionExpiredException(subscription.CustomerId);
```

## 18. Jobs

Use TurtlePath jobs when a workload is not naturally an HTTP endpoint or a message consumer.

There are two standard shapes:

- one-shot jobs: the process starts, runs one or more registered jobs, returns an exit code, and stops. This is the recommended shape for Kubernetes `CronJob`.
- recurring cron-style jobs: the host stays alive and runs one or more jobs on intervals managed by the application.

The template can be created directly as a one-shot job host:

```powershell
dotnet new turtlepath -n Billing.Jobs -o C:\work\Billing.Jobs --host job
```

The same Business, Domain, Persistence, automations, handlers, hooks, exception handling, Spider boundaries, OctoMap mappings, Crabalidator validators, and DataScorpio query configuration are available. Only the host startup changes.

### Create A Job

```csharp
using TurtlePath.Jobs;

namespace Billing.Service.Business.Invoices.Jobs;

public sealed class CloseExpiredInvoicesJob : TurtlePathJob
{
    private readonly IInvoiceExpirationService invoiceExpirationService;
    private readonly ILogger<CloseExpiredInvoicesJob> logger;

    public CloseExpiredInvoicesJob(
        IInvoiceExpirationService invoiceExpirationService,
        ILogger<CloseExpiredInvoicesJob> logger)
    {
        this.invoiceExpirationService = invoiceExpirationService;
        this.logger = logger;
    }

    public override async Task ExecuteAsync(
        TurtlePathJobContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Closing expired invoices from job {JobName}.", context.Name);

        await invoiceExpirationService.CloseExpiredInvoicesAsync(cancellationToken);
    }
}
```

Keep job classes thin. The recommended path is `Job -> Service` because scheduled work normally orchestrates a background process directly. Use `Mediator.Send(...)` only when the job intentionally reuses an existing request/handler that is also used by HTTP or consumers. Do not create a handler just so a job can call it; that only adds boilerplate.

### One-Shot Jobs For Kubernetes CronJob

Register one or many one-shot jobs:

```csharp
services.AddTurtlePathJobs(options =>
{
    options.ExecutionMode = TurtlePathJobExecutionMode.Parallel;
    options.MaxDegreeOfParallelism = Environment.ProcessorCount;
    options.Retries = 2;
    options.RetryDelay = TimeSpan.FromSeconds(10);
    options.FailureBehavior = TurtlePathJobFailureBehavior.Rethrow;
})
.AddJob<ImportCustomersJob>("import-customers")
.AddJob<ImportInvoicesJob>("import-invoices");
```

`AddJob<TJob>()` registers a one-shot job. When multiple jobs are registered, the manager can run all of them in parallel or sequentially according to `ExecutionMode`.

The job host runs registered one-shot jobs and maps the result to the process exit code:

```csharp
var result = await host.Services.RunTurtlePathJobsAsync();
Environment.ExitCode = result.Succeeded ? 0 : 1;
```

Run selected jobs when a host contains several jobs but a specific deployment should execute only some of them:

```csharp
var result = await host.Services.RunTurtlePathJobsAsync(
    new[] { typeof(ImportInvoicesJob), typeof(CloseExpiredInvoicesJob) },
    cancellationToken);
```

Use one-shot jobs for Kubernetes `CronJob` workloads where Kubernetes controls the schedule and TurtlePath controls scoped DI, retries, exception handling, and parallel execution.

Example Kubernetes manifest:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: close-expired-invoices
spec:
  schedule: "*/30 * * * *"
  jobTemplate:
    spec:
      template:
        spec:
          restartPolicy: Never
          containers:
            - name: close-expired-invoices
              image: registry.example.com/billing-jobs:1.0.0
              command: ["dotnet", "Billing.Jobs.dll"]
```

One-shot job options:

- `ExecutionMode`: `Parallel` runs registered jobs concurrently; `Sequential` runs them one by one.
- `MaxDegreeOfParallelism`: caps concurrent jobs when `ExecutionMode` is `Parallel`.
- `Retries`: number of retry attempts after the first failure.
- `RetryDelay`: delay between retry attempts.
- `FailureBehavior`: `Rethrow` fails the run, `Continue` records the failure and keeps processing, `StopHost` asks the host to stop.

### Recurring Cron-Style Jobs

Create the recurring job class the same way as any TurtlePath job:

```csharp
using TurtlePath.Jobs;

namespace Billing.Service.Business.Catalog.Jobs;

public sealed class RefreshCatalogJob : TurtlePathJob
{
    private readonly ICatalogRefreshService catalogRefreshService;
    private readonly ILogger<RefreshCatalogJob> logger;

    public RefreshCatalogJob(
        ICatalogRefreshService catalogRefreshService,
        ILogger<RefreshCatalogJob> logger)
    {
        this.catalogRefreshService = catalogRefreshService;
        this.logger = logger;
    }

    public override async Task ExecuteAsync(
        TurtlePathJobContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Refreshing catalog from cron job {JobName}.", context.Name);

        await catalogRefreshService.RefreshAsync(cancellationToken);
    }
}
```

Register recurring jobs when the same host should keep running:

```csharp
services.AddTurtlePathJobs()
    .AddCronJob<RefreshCatalogJob>(options =>
    {
        options.EveryMinutes(30);
        options.RunOnStart = true;
        options.Retries = 3;
        options.RetryDelay = TimeSpan.FromSeconds(15);
        options.FailureBehavior = TurtlePathJobFailureBehavior.Continue;
    }, "refresh-catalog");
```

Register multiple cron jobs in the same host:

```csharp
services.AddTurtlePathJobs()
    .AddCronJob<RefreshCatalogJob>(options =>
    {
        options.EveryMinutes(30);
        options.RunOnStart = true;
        options.Retries = 3;
        options.RetryDelay = TimeSpan.FromSeconds(15);
        options.FailureBehavior = TurtlePathJobFailureBehavior.Continue;
    }, "refresh-catalog")
    .AddCronJob<CloseExpiredInvoicesJob>(options =>
    {
        options.EveryHours(1);
        options.RunOnStart = false;
        options.Retries = 1;
        options.FailureBehavior = TurtlePathJobFailureBehavior.Rethrow;
    }, "close-expired-invoices");
```

Recurring cron options:

- `Every(TimeSpan interval)`: sets the exact interval.
- `EverySeconds(int seconds)`: shortcut for second-based intervals.
- `EveryMinutes(int minutes)`: shortcut for minute-based intervals.
- `EveryHours(int hours)`: shortcut for hour-based intervals.
- `Interval`: the resolved interval used by the hosted service.
- `RunOnStart`: runs immediately when the host starts instead of waiting for the first interval.
- `Retries`, `RetryDelay`, and `FailureBehavior`: same retry and failure behavior used by one-shot jobs, but applied to each execution cycle.

Multiple cron jobs are supported. Each registered cron job runs its own loop inside `TurtlePathCronJobHostedService`, so each job can have its own interval, retry policy, and failure behavior.

## 19. Testing

The template includes testing setup so feature tests do not start from zero. The developer should write the use case and assertions, while the template keeps the TurtlePath test host, Pelican, OctoMap, Crabalidator, Spider, DataScorpio, SQLite, jobs, and exception handling ready.

Use this split:

- unit tests for manual handlers, hooks, and small services
- integration tests for automations because their handlers are generated
- Spider integration tests for flows where transaction boundaries or execution boundaries are part of the contract
- SQLite integration tests for EF configuration, CId conversion, query translation, and DataScorpio filters
- composition tests to prove the template host starts with the expected defaults
- job tests for one-shot and recurring job registration
- exception tests when a feature owns custom exception mappings

The generated test project includes `Testing/TemplateTestHost.cs`. Keep using that wrapper instead of configuring every package in every test.

### Unit Test A Handler

Use a unit test when your service owns a concrete handler class. Delegate maps and validators avoid booting the full adapter stack:

```csharp
await using var host = await TemplateTestHost
    .CreateUnitHost()
    .WithMap<CreateInvoiceRequest, Invoice>(request => new Invoice
    {
        CustomerId = request.CustomerId,
        Amount = request.Amount
    })
    .WithMap<Invoice, InvoiceResponse>(invoice => new InvoiceResponse
    {
        Id = invoice.Id,
        Amount = invoice.Amount
    })
    .WithValidRequest<CreateInvoiceRequest>()
    .BuildAsync();

var handler = new CreateInvoiceCommandHandler(host.Services);
var response = await handler.Handle(new CreateInvoiceRequest { Amount = 100m });

Assert.Equal(100m, response.Amount);
```

This is the fastest path for custom handlers because the test exercises the handler directly with in-memory storage.

### Integration Test Automations With SQLite

Use integration tests for automations because the handler is generated by TurtlePath.Automations and resolved through Pelican. SQLite keeps the test close to production EF behavior without requiring an external database:

```csharp
await using var host = await TemplateTestHost
    .CreateIntegrationHost<AppDbContext>()
    .UsePelican(typeof(Constants).Assembly)
    .UseOctoMapTesting(typeof(Constants).Assembly)
    .UseCrabalidatorTesting(typeof(Constants).Assembly)
    .UseDataScorpioTesting(profiles => profiles.FromAssembly(typeof(Constants).Assembly))
    .BuildAsync();

await host.CreateSchemaAsync<AppDbContext>();

var response = await host.SendAsync(new CreateInvoiceRequest
{
    CustomerId = customerId,
    Amount = 100m
});

Assert.False(response.Id.IsEmpty);
```

Use this style for create, update, delete, get by id, and paged automation flows.

### Integration Test A Manual Handler Through Pelican

When the handler is manually written but you want to test the same dispatch path used by controllers and consumers, call `host.SendAsync(...)`:

```csharp
await using var host = await TemplateTestHost
    .CreateUnitHost()
    .UsePelican(typeof(CreateInvoiceCommandHandler).Assembly)
    .WithMap<CreateInvoiceRequest, Invoice>(request => new Invoice
    {
        CustomerId = request.CustomerId,
        Amount = request.Amount
    })
    .WithMap<Invoice, InvoiceResponse>(invoice => new InvoiceResponse
    {
        Id = invoice.Id,
        Amount = invoice.Amount
    })
    .WithValidRequest<CreateInvoiceRequest>()
    .BuildAsync();

var response = await host.SendAsync(new CreateInvoiceRequest
{
    CustomerId = customerId,
    Amount = 100m
});
```

This proves the request is registered with Pelican and can be resolved by DI.

### Integration Test Through Spider

Use Spider tests for use cases that must run through execution boundaries. This matters for transaction behavior, boundary ordering, and flows called from controllers or consumers through `Spider`.

```csharp
using Spider.Testing;
using Spider.Testing.Assertions;
using TurtlePath.Spider.Transactions;

await using var host = await TemplateTestHost
    .CreateIntegrationHost<AppDbContext>()
    .UsePelican(typeof(Constants).Assembly)
    .UseSpiderTesting(typeof(TransactionExecutionBoundary).Assembly)
    .WithMap<CreateInvoiceRequest, Invoice>(request => new Invoice
    {
        CustomerId = request.CustomerId,
        Amount = request.Amount
    })
    .WithMap<Invoice, InvoiceResponse>(invoice => new InvoiceResponse
    {
        Id = invoice.Id,
        Amount = invoice.Amount
    })
    .WithValidRequest<CreateInvoiceRequest>()
    .BuildAsync();

await host.CreateSchemaAsync<AppDbContext>();

var spider = host.Resolve<ISpiderTesting>();

var response = await spider.ExecuteAsync<InvoiceResponse>(new CreateInvoiceRequest
{
    CustomerId = customerId,
    Amount = 100m
});

Assert.False(response.Id.IsEmpty);
spider.Trace.ShouldContain(nameof(TransactionExecutionBoundary));
spider.Trace.Transaction.ShouldBegin();
spider.Trace.Transaction.ShouldCommit();
```

Use a direct boundary filter test when the important behavior is whether a request should open a transaction:

```csharp
using Microsoft.Extensions.Options;
using TurtlePath.Spider.Transactions;

var options = Options.Create(new TransactionBoundaryOptions
{
    IncludeQueries = false,
    ExcludedRequestTypes = new HashSet<string>
    {
        nameof(RebuildSearchIndexRequest)
    }
});

var filter = new TransactionBoundaryRequestFilter(options);
filter.Discover(typeof(Constants).Assembly);

Assert.True(filter.ShouldOpenTransaction(typeof(CreateInvoiceRequest)));
Assert.False(filter.ShouldOpenTransaction(typeof(GetPagedInvoicesQuery)));
Assert.False(filter.ShouldOpenTransaction(typeof(RebuildSearchIndexRequest)));
```

Use Pelican tests when you only need handler dispatch. Use Spider tests when the boundary behavior is part of the use case contract.

### Test DataScorpio Filters

Use SQLite when the query must prove filters, sorts, search, or paging translate correctly:

```csharp
await using var host = await TemplateTestHost
    .CreateIntegrationHost<AppDbContext>(profiles =>
    {
        profiles.FromAssembly(typeof(Constants).Assembly);
    })
    .UsePelican(typeof(Constants).Assembly)
    .BuildAsync();

await host.CreateSchemaAsync<AppDbContext>();

var page = await host.SendAsync(new GetPagedInvoicesQuery(new PagedSettings
{
    PageNumber = 1,
    PageSize = 20,
    Filters = "Status==Issued",
    Sorts = "-CreatedAt"
}));

Assert.All(page.Results, invoice => Assert.Equal("Issued", invoice.Status));
```

### Test Hooks

Use hook tracing when a test needs to prove a hook stage ran:

```csharp
await using var host = await TemplateTestHost
    .CreateUnitHost()
    .TraceHooks()
    .BuildAsync();

var trace = host.Resolve<HookTrace>();
Assert.Contains(trace.Entries, entry => entry.Stage == "AfterSave");
```

Use hook tracing for audit hooks, event sourcing hooks, publishing hooks, and validation hooks that must run around a handler stage.

### Test Exception Mappings

Feature-specific exception mappings should be tested once so HTTP, consumers, and jobs receive the expected descriptor:

```csharp
await using var host = await TemplateTestHost
    .CreateUnitHost()
    .UseExceptionHandling(builder =>
    {
        builder.For<InvoiceAlreadyCanceledException>(
            ExceptionKind.Conflict,
            exception => $"Invoice '{exception.InvoiceId}' is already canceled.");
    })
    .BuildAsync();

var handler = host.Resolve<IExceptionHandler>();
var descriptor = handler.Handle(new InvoiceAlreadyCanceledException(invoiceId));

Assert.Equal(ExceptionKind.Conflict, descriptor.Kind);
```

### Test Jobs

Register jobs in the test host and execute them without starting the full app:

```csharp
await using var host = await TemplateTestHost
    .CreateUnitHost()
    .UseJobs(options =>
    {
        options.ExecutionMode = TurtlePathJobExecutionMode.Sequential;
        options.Retries = 1;
    })
    .WithJob<CloseExpiredInvoicesJob>()
    .BuildAsync();

var result = await host.RunJobsAsync(new[] { typeof(CloseExpiredInvoicesJob) });

Assert.True(result.Succeeded);
```

### Composition Tests

Keep at least one composition test for each host variant:

```csharp
[Fact]
public async Task ApiHost_Composes()
{
    await using var host = await TemplateTestHost
        .CreateIntegrationHost<AppDbContext>()
        .BuildAsync();

    Assert.NotNull(host.Services);
}
```

Composition tests should stay boring. Their value is catching broken package registration, missing adapters, invalid configuration, or accidental changes to startup defaults.

## 20. External Documentation

Use these references for deeper package behavior:

- [TurtlePath NuGet](https://www.nuget.org/packages/TurtlePath)
- [Payments NuGet](https://www.nuget.org/packages/Payments)
- [TurtlePath.Automations NuGet](https://www.nuget.org/packages/TurtlePath.Automations)
- [TurtlePath.EntityFrameworkCore NuGet](https://www.nuget.org/packages/TurtlePath.EntityFrameworkCore)
- [TurtlePath.Jobs NuGet](https://www.nuget.org/packages/TurtlePath.Jobs)
- [TurtlePath.ExceptionHandling NuGet](https://www.nuget.org/packages/TurtlePath.ExceptionHandling)
- [TurtlePath.Testing NuGet](https://www.nuget.org/packages/TurtlePath.Testing)
- [Pelican.Mediator NuGet](https://www.nuget.org/packages/Pelican.Mediator)
- [OctoMap NuGet](https://www.nuget.org/packages/OctoMap)
- [Crabalidator NuGet](https://www.nuget.org/packages/Crabalidator)
- [DataScorpio NuGet](https://www.nuget.org/packages/DataScorpio)
- [Spider.Pipelines NuGet](https://www.nuget.org/packages/Spider.Pipelines)
- [Pigeon.Messaging NuGet](https://www.nuget.org/packages/Pigeon.Messaging)
- [Pigeon Azure Service Bus Adapter NuGet](https://www.nuget.org/packages/Pigeon.Messaging.Azure.ServiceBus)
- [Pigeon Outbox EF Core NuGet](https://www.nuget.org/packages/Pigeon.Messaging.Outbox.EntityFrameworkCore)
- [TurtlePath.EventSourcing NuGet](https://www.nuget.org/packages/TurtlePath.EventSourcing)
- [Krackend.EventSourcing.EntityFrameworkCore NuGet](https://www.nuget.org/packages/Krackend.EventSourcing.EntityFrameworkCore)
- [NuGet package management docs](https://learn.microsoft.com/en-us/nuget/)

Use NuGet pages to confirm installation commands, supported target frameworks, package versions, dependencies, and README examples. Use package repository docs when you need deeper adapter-specific behavior.
