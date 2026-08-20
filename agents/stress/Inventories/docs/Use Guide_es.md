# Guia De Uso De TurtlePath Template

Esta guia explica como crear y crecer un servicio generado con `Inventories`. Esta pensada para el dev que acaba de crear un proyecto y necesita saber donde va cada clase, que defaults ya vienen configurados, y cuando usar automations, handlers, hooks, jobs, consumers y exception handling.

## Indice

- [1. Que Te Da El Template](#1-que-te-da-el-template)
- [2. Crear Un Proyecto](#2-crear-un-proyecto)
- [3. Estructura Del Proyecto](#3-estructura-del-proyecto)
- [4. Convenciones De Nomenclatura](#4-convenciones-de-nomenclatura)
- [5. Registro De Dependencias Por Default](#5-registro-de-dependencias-por-default)
- [6. Crear Un Feature De Inicio A Fin](#6-crear-un-feature-de-inicio-a-fin)
- [7. Mapping Con OctoMap](#7-mapping-con-octomap)
- [8. Validacion Con Crabalidator](#8-validacion-con-crabalidator)
- [9. Filtros Y Paginado Con DataScorpio](#9-filtros-y-paginado-con-datascorpio)
- [10. Automations](#10-automations)
- [11. Handlers Custom](#11-handlers-custom)
- [12. Hooks](#12-hooks)
- [13. Controllers Y Rutas REST](#13-controllers-y-rutas-rest)
- [14. Spider Pipelines Y Transacciones](#14-spider-pipelines-y-transacciones)
- [15. Consumers Con Pigeon Y Outbox](#15-consumers-con-pigeon-y-outbox)
- [16. Event Sourcing](#16-event-sourcing)
- [17. Exception Handling](#17-exception-handling)
- [18. Jobs](#18-jobs)
- [19. Testing](#19-testing)
- [20. Documentacion Externa](#20-documentacion-externa)

## 1. Que Te Da El Template

El proyecto generado no es un ASP.NET Core vacio. Ya trae configurado el stack estandar:

- `TurtlePath` para requests, responses, command handlers, query handlers, hooks, storage abstractions, validation y mapping adapters.
- `TurtlePath.Domain` para `CId`, `BaseEntity` e `IEntity<TKey>`.
- `TurtlePath.EntityFrameworkCore` para `BaseDbContext`, `IDbContext`, storage adapters de EF Core y conversion de `CId`.
- `TurtlePath.Automations` para generar handlers de Pelican desde profiles o attributes.
- `TurtlePath.OctoMap` como adapter de mapping.
- `TurtlePath.Crabalidator` como adapter de validacion.
- `TurtlePath.DataScorpio` como motor default de filtros, sort, search y paging.
- `TurtlePath.ExceptionHandling` para descriptores de excepcion independientes del transporte.
- `TurtlePath.ExceptionHandling.AspNetCore` para respuestas HTTP `ProblemDetails`.
- `TurtlePath.ExceptionHandling.Consumers` para consumers de Pigeon.
- `TurtlePath.ExceptionHandling.Workers` para jobs y background work.
- `TurtlePath.Jobs` para jobs one-shot y jobs recurrentes.
- `TurtlePath.EventSourcing` con event store de Krackend sobre EF Core preparado como stack opcional.
- `Pelican.Mediator` para dispatch de requests.
- `Spider.Pipelines` para execution boundaries, incluyendo el transaction boundary.
- `TurtlePath.Spider` para el bridge propio de TurtlePath que envia requests de Pelican por Spider sin acoplar esas librerias entre si.
- `TurtlePath.Spider.Transactions` para el transaction boundary ambiente, discovery de perfiles y filtro de requests.
- `Pigeon.Messaging` con Azure Service Bus y EF Core outbox preparado como stack opcional.
- `TurtlePath.Analyzers` para evitar comparaciones y asignaciones inseguras de `CId`.

La regla recomendada:

- Usa automations para CRUD happy paths.
- Agrega hooks cuando el happy path esta bien pero necesita pasos de negocio.
- Crea handlers cuando cambia el flujo.
- Deja codigo especifico del servicio en el proyecto generado.
- Deja infraestructura reusable en paquetes NuGet.

## 2. Crear Un Proyecto

Instala el template:

```powershell
dotnet new install Inventories
```

Crea el host API/consumer:

```powershell
dotnet new turtlepath -n Billing.Service -o C:\work\Billing.Service --host api-consumer
```

Crea un host de jobs one-shot:

```powershell
dotnet new turtlepath -n Billing.Jobs -o C:\work\Billing.Jobs --host job
```

Verifica el proyecto:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Cada proyecto generado incluye `inventories.json` en la raiz de la solucion:

```json
{
  "packageId": "Inventories",
  "version": "1.6.0"
}
```

Usa ese archivo para saber con que version de `Inventories` se creo el servicio. En el codigo fuente del repositorio puede aparecer como `0.0.0-local`; los paquetes publicados se estampan durante el release con la version real del paquete.

Ambos hosts comparten la misma forma de Business, Domain, Persistence y tests. La diferencia principal es la capa de presentacion:

- `api-consumer` levanta ASP.NET Core con controllers, Scalar OpenAPI docs, health checks, Spider y exception filters. Los consumers con Pigeon quedan listos para habilitarse cuando el servicio tenga configuracion del broker.
- `job` levanta un generic host, ejecuta los jobs registrados y termina con exit code `0` si todo salio bien.

## 3. Estructura Del Proyecto

La solucion generada esta separada por responsabilidad:

```text
inventories.json
src/
  Inventories.Api/
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
    Inventories.Api.csproj
  Inventories.Business/
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
    Inventories.Business.csproj
  Inventories.Domain/
    Inventories.Domain.csproj
  Inventories.Persistence/
    AppDbContext.cs
    Inventories.Persistence.csproj
tests/
  Inventories.Tests/
    Testing/
      TemplateTestHost.cs
    JobCompositionTests.cs
    TemplateCompositionTests.cs
    TurtlePathTestingExamplesTests.cs
    Inventories.Tests.csproj
```

`Api` es la capa host. Ahi viven controllers, consumers opcionales, composicion de arranque, exception handling, boundaries transaccionales con Spider, configuracion opcional de Pigeon, Scalar OpenAPI docs, health checks y el punto para registrar dependencias custom.

La implementacion del transaction boundary la proporciona el paquete `TurtlePath.Spider.Transactions`. El API generado solo contiene el registro y no duplica los archivos fuente del boundary. Los perfiles de Business usan `TransactionBoundaryProfile` de ese paquete y se descubren desde los assemblies que el registro de API pasa explicitamente.

`Business` contiene los casos de uso. El template incluye una carpeta `Feature` solo como placeholder para mostrar la estructura esperada. En codigo real se sustituye por el nombre del feature:

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

Uso recomendado de cada carpeta del feature:

- `Commands/`: command handlers manuales cuando las automations no alcanzan, por ejemplo `CreateCustomerCommandHandler` o `ChangeOrderStatusCommandHandler`.
- `Queries/`: mensajes de query y query handlers manuales, por ejemplo `GetCustomerByIdQuery` con `GetCustomerByIdQueryHandler` anidado, o `GetPagedInvoicesQuery`.
- `Validators/`: validadores de Crabalidator como `CreateCustomerRequestValidator`.
- `Mappings/`: profiles de OctoMap como `CustomerMappingProfile`.
- `Hooks/`: hooks de TurtlePath para customizar etapas del handler sin reemplazar el handler completo.
- `Automations/`: automation profiles de TurtlePath como `CustomerAutomationProfile`.
- `Querying/`: configuracion de filtros y sorts con DataScorpio para queries paginados.
- `Models/Requests/`: DTOs de entrada. Las mutaciones usan sufijo `Request`, por ejemplo `CreateCustomerRequest`.
- `Models/Responses/`: DTOs de salida que devuelven handlers, controllers o consumers.
- `Services/`: servicios propios del feature. Agrupa cada servicio en su propia carpeta.

Ejemplos:

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

Cada feature vive directamente debajo del proyecto Business. No crees un contenedor global `Features/` salvo que tu equipo lo decida explicitamente.

Los servicios propios del feature van dentro del feature:

```text
Customers/
  Services/
    SAT/
      ISatService.cs
      SatService.cs
      SatValidationResult.cs
```

Los servicios compartidos entre features van en la raiz de Business agrupados por servicio:

```text
Services/
  Audit/
    IAuditService.cs
    AuditService.cs
    AuditEntry.cs
```

Esta estructura deja limpio el camino para extraer servicios a librerias compartidas si despues se vuelven reutilizables.

Las dependencias custom se registran en `Api/DependencyInjection/CustomContainerExtensions.cs`. MantÃ©n los defaults enfocados en configuracion base del framework y encadena las dependencias propias desde el custom container.

`Domain` nace limpio a proposito. Coloca ahi entidades, value objects, enums y contratos de dominio propios del servicio usando la estructura que realmente necesite el servicio. Los identificadores de TurtlePath vienen desde los paquetes de TurtlePath, no desde carpetas generadas por el template.

`Persistence` contiene la integracion con base de datos. MantÃ©n `AppDbContext` limpio y no agregues propiedades `DbSet<TEntity>` solo para exponer tablas. Cuando agregues entidades, crea mappings como `IEntityTypeConfiguration<TEntity>` dentro de una carpeta `Configurations/`. Asi el DbContext se queda enfocado en convenciones de TurtlePath y las configuraciones definen tablas, llaves, indices, relaciones y conversiones de base de datos.

## 4. Convenciones De Nomenclatura

Usa la misma nomenclatura en todos los servicios. Eso hace que cualquier dev encuentre rapido requests, responses, handlers, validators, maps, controllers y consumers.

### Requests

Los mensajes de mutacion son conceptualmente commands, pero conservan el sufijo `Request`:

- `CreateCustomerRequest`
- `UpdateInvoiceRequest`
- `ChangeOrderStatusRequest`
- `CancelInvoiceRequest`

Los requests que apuntan a una entidad existente normalmente heredan `BaseRequest`:

```csharp
public sealed class UpdateInvoiceRequest : BaseRequest, IRequest<InvoiceResponse>
{
    public decimal Amount { get; set; }
}
```

### Responses

Los responses representan la salida de cualquier handler:

- `CustomerResponse`
- `InvoiceResponse`
- `ChangeOrderStatusResponse`

Para entidades con `BaseEntity`, hereda `BaseResponse`:

```csharp
public sealed class InvoiceResponse : BaseResponse
{
    public string Folio { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
```

### Command Handlers

Los command handlers expresan la accion y terminan con `CommandHandler`:

- `CreateCustomerCommandHandler`
- `UpdateInvoiceCommandHandler`
- `ChangeOrderStatusCommandHandler`
- `CancelInvoiceCommandHandler`

Si una operacion esta automatizada, no crees handler manual. La automation es la fuente de verdad.

### Queries

Los query messages y sus handlers usan terminologia `Query`:

- `GetCustomerByIdQuery`
- `GetCustomerByIdQueryHandler`
- `GetPagedInvoicesQuery`
- `GetPagedInvoicesQueryHandler`

Para queries pequenos, puedes anidar el handler dentro del query:

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

Los queries paginados usan `GetPagedInfoQuery<TEntity, TResponse>` como base. No uses una base `PagedRequest`.

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

Los validators usan el nombre del request mas `Validator`:

- `CreateCustomerRequestValidator`
- `UpdateInvoiceRequestValidator`
- `CancelInvoiceRequestValidator`

### Mappings

Los mapping profiles usan el nombre del feature o aggregate mas `MappingProfile`:

- `CustomerMappingProfile`
- `InvoiceMappingProfile`
- `OrderMappingProfile`

### Automations

Los automation profiles usan el nombre del feature o aggregate mas `AutomationProfile`:

- `CustomerAutomationProfile`
- `InvoiceAutomationProfile`
- `OrderAutomationProfile`

### Hooks

El nombre del hook debe decir que hace y en que stage corre:

- `AssignCustomerNumberBeforeSaveHook`
- `NormalizeCustomerEmailAfterMapHook`
- `PublishInvoiceCanceledAfterSaveHook`
- `AttachInvoiceSummaryAfterQueryHook`

### Services

Los servicios propios del feature viven bajo una carpeta especifica:

```text
Customers/
  Services/
    SAT/
      ISatService.cs
      SatService.cs
```

Los servicios compartidos van en la raiz de Business:

```text
Services/
  Audit/
    IAuditService.cs
    AuditService.cs
```

### Controllers Y Consumers

Controllers y hub consumers usan nombres plurales:

- `CustomersController`
- `InvoicesController`
- `OrdersController`
- `CustomersHubConsumer`
- `InvoicesHubConsumer`

### Entity Configurations

Las configuraciones de EF usan el nombre de la entidad mas `Configuration`:

- `CustomerConfiguration`
- `InvoiceConfiguration`
- `OrderConfiguration`

## 5. Registro De Dependencias Por Default

La mayor parte del wiring vive en `Inventories.Api/DependencyInjection`.

### Startup Defaults

El host API/consumer usa `AddDefaults`:

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
        // Habilita esto solo cuando el servicio use event sourcing.
        // .AddEventSourcingDefaults()
        // Habilita esto solo cuando el servicio use Pigeon y tenga configuracion del broker.
        // .AddMessagingDefaults(configuration)
        .AddPipelineDefaults(configuration)
        .AddCustomContainer(configuration);
}
```

`AddCustomContainer` es el punto obligatorio para inyeccion de dependencias custom. Va al final para que puedas registrar dependencias del servicio sin tocar los defaults del template:

```csharp
internal static IServiceCollection AddCustomContainer(this IServiceCollection services, IConfiguration configuration)
{
    services.AddScoped<ICustomerNumberService, CustomerNumberService>();
    services.AddScoped<Services.Audit.IAuditService, Services.Audit.AuditService>();

    return services;
}
```

No metas dependencias de negocio en `AddDefaults`, `AddApplicationDefaults`, el `AddMessagingDefaults` opcional o `AddPipelineDefaults` salvo que estes cambiando el template base. Para un servicio real, usa `AddCustomContainer`.

### Application Defaults

`AddApplicationDefaults` registra Pelican, Crabalidator, OctoMap, hooks de TurtlePath, automations, perfiles de DataScorpio, CId default, perfiles de CId y EF Core:

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
    // Habilita esto solo despues de agregar perfiles IEventSourcingProfile.
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

Para proyectos nuevos, lo sano es usar un solo tipo de `CId` en todas las entidades. El default del template es `CId` envolviendo `Ulid` en C# y persistido como `string`.

### Profiles Custom De CId Para Entidades Legacy

No cambies el `UseCId<Ulid, string>()` default solo porque una tabla legacy usa otra llave. MantÃ©n el default para el modelo sano, y agrega definiciones de CId por entidad usando un profile.

Usa esto cuando el codigo publico debe seguir viendo `CId`, pero una entidad especifica esta respaldada por otro tipo CLR/base de datos, por ejemplo una PK vieja tipo `int`:

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

Despues registra los profiles despues de la configuracion default de CId:

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

Ubicacion recomendada:

```text
Domain/
  Identifier/
    BillingIdentifierProfile.cs
```

Los parametros genericos significan:

- `TEntity`: la entidad que necesita el override, por ejemplo `LegacyInvoice`.
- `TTargetType`: el valor CLR envuelto por `CId` en el codigo de aplicacion, por ejemplo `int`, `Guid` o `Ulid`.
- `TDbType`: el valor que EF guarda en base de datos, por ejemplo `int` o `string`.

Eso significa que `UseCIdFor<LegacyInvoice, int, int>()` es un `int` dentro de `CId`, guardado como `int` en base de datos. `UseCIdFor<ImportedOrder, Ulid, string>()` es un `Ulid` dentro de `CId`, guardado como `string`.

Si la entidad legacy no usa `CId` y expone un `int` directo, usa los handlers y automations genericos de TurtlePath para `IEntity<TKey>` en lugar de forzar CId en ese modelo.

## 6. Crear Un Feature De Inicio A Fin

Este ejemplo crea `Invoices` usando el camino recomendado: automations + hooks. Despues la guia explica cuando reemplazarlo por handlers manuales.

### Entidad

`Domain/Invoice.cs`:

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

### Configuracion EF

`Persistence/Configurations/InvoiceConfiguration.cs`:

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

No agregues una propiedad `DbSet<Invoice>` al `AppDbContext`. El template mantiene limpio el DbContext y usa clases `IEntityTypeConfiguration<TEntity>` para describir el modelo. `BaseDbContext` aplica las convenciones de identificadores de TurtlePath y tus configuraciones definen nombres de tabla, max lengths, indices, relaciones y conversiones.

### Requests

`Business/Invoices/Models/Requests`:

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

Queries:

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

### Validacion

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

public sealed class CancelInvoiceRequestValidator : CrabValidator<CancelInvoiceRequest>
{
    public CancelInvoiceRequestValidator()
    {
        RuleFor(request => request.Id).Must(id => !id.IsEmpty);
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(200);
    }
}
```

### DataScorpio

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

Ejemplo:

```http
GET /api/v1/invoices?page=1&pageSize=20&filters=Folio@=*2026&sorts=-CreatedAt
```

### Automation Profile

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

Con esto no creas handlers para esos happy paths. TurtlePath genera handlers Pelican desde la declaracion.

### Hooks

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

```csharp
using Asp.Versioning;
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Business.Invoices.Models.Responses;
using Billing.Service.Business.Invoices.Queries;
using Microsoft.AspNetCore.Mvc;
using TurtlePath.Domain.Identifier;
using TurtlePath.Models.Responses;
using Inventories.Api.Controllers;

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

Ese es el happy path completo: entidad, EF, requests, response, mapping, validation, filtering, automation, hooks y controller.

## 7. Mapping Con OctoMap

Usa OctoMap para transformar requests a entidades, entidades a responses y eventos a mensajes.

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

Usa mapeos explicitos cuando el response no coincide con la entidad:

```csharp
builder.CreateMap<Invoice, InvoiceResponse>()
    .ForMember(response => response.Status, map => map.MapFrom(invoice => invoice.Status.ToString()))
    .ForMember(response => response.CanCancel, map => map.MapFrom(invoice => invoice.Status == InvoiceStatus.Issued));
```

Usa mapping para cambios de forma. No escondas reglas de negocio en maps; si requiere servicios, base de datos o side effects, usa hooks o handlers.

## 8. Validacion Con Crabalidator

Usa validators para validar requests:

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

Usa validators para:

- requeridos
- longitudes
- rangos
- valores de enum/state recibidos del cliente
- `CId` validos

Usa handlers o hooks cuando la regla necesita entidades cargadas o servicios externos.

## 9. Filtros Y Paginado Con DataScorpio

Los queries paginados heredan `GetPagedInfoQuery<TEntity, TResponse>` y retornan `PagedResponse<TResponse>`:

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

DataScorpio aplica `filters`, `sorts` y `search` sobre campos permitidos por el profile.

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

Ejemplos:

```http
GET /api/v1/invoices?page=1&pageSize=20
GET /api/v1/invoices?filters=Status==Issued
GET /api/v1/invoices?filters=Folio@=*INV-2026&sorts=-CreatedAt
GET /api/v1/invoices?search=ACME&sorts=Folio
```

Si un filtro es parte obligatoria del endpoint, usa una propiedad tipada en el query y un handler custom en la seccion de `Handlers Custom`.

### Profiles Avanzados De DataScorpio

Usa aliases cuando el nombre publico del query no debe exponer el nombre de la propiedad C#:

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

El cliente usa nombres de API, no necesariamente nombres CLR:

```http
GET /api/v1/invoices?filters=customer==01J7V8R7RXA6MG9A4R8ZNZ5E3P&sorts=-created
```

Usa custom filters para conceptos de negocio que no son simplemente una propiedad:

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

Ejemplos:

```http
GET /api/v1/invoices?filters=Overdue==true
GET /api/v1/invoices?filters=DueWindow==15
```

Usa custom sorts cuando el ordenamiento tiene significado de negocio:

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

Ejemplo:

```http
GET /api/v1/invoices?sorts=-Priority
```

Usa conventions globales cuando varias entidades comparten comportamiento de query:

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

Coloca los query profiles y conventions dentro del feature, normalmente en `Business/Invoices/Querying`. El default del template ya descubre los profiles y conventions de DataScorpio desde el assembly de la aplicacion:

```csharp
services.AddTurtlePath(typeof(Constants).Assembly)
    .UseDataScorpio(profiles => profiles.FromAssembly(typeof(Constants).Assembly));
```

No registres cada query profile uno por uno. Agrega la clase al assembly y el discovery la toma. Cualquier profile cuya entidad implemente el contrato de la convention recibe automaticamente el custom filter o sort.

## 10. Automations

Automations generan handlers desde declaracion. Usalas cuando el flujo es estandar:

- validar request
- mapear request a entidad o cargar entidad
- ejecutar hooks
- guardar o eliminar
- mapear response
- aplicar filtros/paging en queries

Profile recomendado:

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

Para entidades legacy con llave custom:

```csharp
builder.For<LegacyShipment, int>()
    .ToCreate<CreateLegacyShipmentRequest, LegacyShipmentResponse>()
    .ToGetById<GetLegacyShipmentByIdQuery, LegacyShipmentResponse>(query => query.GetKeyFrom(x => x.Id));
```

Attributes para flujos pequenos:

```csharp
[CreateAutomation(typeof(Invoice), typeof(InvoiceResponse))]
public sealed class CreateInvoiceRequest : IRequest<InvoiceResponse>
{
    public CId CustomerId { get; set; }

    public decimal Amount { get; set; }
}
```

Attributes disponibles:

- `CreateAutomationAttribute`
- `UpdateAutomationAttribute`
- `DeleteAutomationAttribute`
- `PatchAutomationAttribute`
- `GetByIdAutomationAttribute`
- `GetManyAutomationAttribute`
- `GetPagedAutomationAttribute`

No uses automations si la operacion toca varios aggregates, requiere side effects antes de persistir, necesita un flujo transaccional distinto o el comportamiento seria dificil de entender con hooks.

## 11. Handlers Custom

Usa handlers custom cuando el flujo importa. Commands y queries tienen caminos manuales.

### Referencia De Clases Base

Usa las clases base no genericas para el modelo recomendado de TurtlePath: entidades que heredan `BaseEntity`, ids con `CId` y responses que heredan `BaseResponse`.

Command handlers con response:

- Create: `CreateCommandHandler<TRequest, TResponse, TEntity>`
- Update: `UpdateCommandHandler<TRequest, TResponse, TEntity>`
- Delete: `DeleteCommandHandler<TRequest, TResponse, TEntity>`
- Patch: `PatchCommandHandler<TRequest, TResponse, TEntity>`

Command handlers sin response:

- Create: `CreateCommandHandler<TRequest, TEntity>`
- Update: `UpdateCommandHandler<TRequest, TEntity>`
- Delete: `DeleteCommandHandler<TRequest, TEntity>`
- Patch: `PatchCommandHandler<TRequest, TEntity>`

Query messages y handlers:

- Get by id message: `GetByIdQuery<TEntity, TResponse>`
- Get by id handler: `GetByIdQueryHandler<TQuery, TEntity, TResponse>`
- Get one por un valor que no es id message: `GetOneQuery<TValue, TEntity, TResponse>`
- Get one por un valor que no es id handler: `GetOneQueryHandler<TQuery, TValue, TEntity, TResponse>`
- Get many message: `GetManyQuery<TEntity, TResponse>`
- Get many handler: `GetManyQueryHandler<TQuery, TEntity, TResponse>`
- Get all: usa `GetManyQuery<TEntity, TResponse>` sin filtros obligatorios
- Get paged message: `GetPagedInfoQuery<TEntity, TResponse>`
- Get paged handler: `GetPagedInfoQueryHandler<TQuery, TEntity, TResponse>`

Usa las clases base genericas solo cuando una entidad legacy/custom implementa `IEntity<TKey>` sin usar `BaseEntity` y `CId`:

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

Los handlers recomendados dependen de `BaseEntity` y `CId`:

```csharp
public sealed class CreateInvoiceCommandHandler
    : CreateCommandHandler<CreateInvoiceRequest, InvoiceResponse, Invoice>
{
    public CreateInvoiceCommandHandler(IServiceProvider services) : base(services)
    {
    }
}
```

Usa la sobrecarga sin response cuando el command solo necesita terminar correctamente:

```csharp
public sealed class DeleteInvoiceCommandHandler
    : DeleteCommandHandler<DeleteInvoiceRequest, Invoice>
{
    public DeleteInvoiceCommandHandler(IServiceProvider services) : base(services)
    {
    }
}
```

Para llaves legacy:

```csharp
public sealed class CreateLegacyShipmentCommandHandler
    : GenericCreateCommandHandler<CreateLegacyShipmentRequest, LegacyShipmentResponse, LegacyShipment, int>
{
    public CreateLegacyShipmentCommandHandler(IServiceProvider services) : base(services)
    {
    }
}
```

### Metodos Virtuales En Command Handlers

Usa metodos virtuales para ajustes especificos del handler. Usa hooks para comportamiento reusable.

- Create: `ValidateRequest`, `UseProjectionFromStorage`, `ValidateAsync`, `MapToEntityAsync`, `SaveEntityAsync`, `MapToResponseAsync`.
- Update: `ValidateRequest`, `UseProjectionFromStorage`, `GetEntityAsync`, `ValidateAsync`, `MapEntityAsync`, `UpdateEntityAsync`, `MapToResponseAsync`.
- Delete: `ValidateRequest`, `GetEntityAsync`, `ValidateAsync`, `DeleteEntityAsync`, `BuildResponseAsync`.
- Patch: `ValidateRequest`, `GetEntityAsync`, `ValidateAsync`, `PatchEntityAsync`, `UpdateEntityAsync`, `BuildResponseAsync`.

### Query Handlers

Usa query handlers cuando el endpoint necesita filtros obligatorios, reglas custom de busqueda o comportamiento especifico del feature que no debe expresarse como filtro publico de DataScorpio.

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

Usa generic query handlers cuando una entidad implementa `IEntity<TKey>` sin usar `BaseEntity` y `CId`:

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

### Metodos Virtuales En Query Handlers

Get-by-id query handlers:

- `Handle(request, cancellationToken)`
- `GetFilterExpression(request)`

Paged query handlers:

- `DefaultSorts`
- `GetFiltersExpression(request)`
- `GetSortingExpression(request)`

Sobreescribe `Handle` solo cuando cambia todo el flujo del query. Prefiere `GetFilterExpression`, `GetFiltersExpression`, `GetSortingExpression` o `DefaultSorts` cuando todavia sirve el flujo estandar de storage, hooks, paging, proyeccion y response.

### Ejemplos De Commands

Deshabilitar validacion:

```csharp
protected override bool ValidateRequest => false;
```

Lookup custom:

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

Response desde storage despues de guardar:

```csharp
protected override bool UseProjectionFromStorage => true;
```

Usalo cuando EF, triggers, columnas calculadas o includes son necesarios para el response.

### Ejemplos De Queries

Override de un query paginado cuando el endpoint necesita filtros obligatorios o comportamiento especifico del feature:

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

Hooks extienden el flujo estandar sin reemplazar el handler.

Command hooks:

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

Query hooks:

- `IBeforeQueryHook<TQuery, TResult>`
- `IAfterQueryHook<TQuery, TResult>`

Usa `IOrderedHook` cuando varios hooks corren en el mismo stage:

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

Publicar evento despues de persistir:

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

Usa hooks para auditoria, asignacion de ids o folios, normalizacion, enriquecimiento de responses, publicacion despues de save y reglas simples en stages claros.

## 13. Controllers Y Rutas REST

Controllers heredan `BaseController`, que expone `Mediator` y `Spider`.

Rutas recomendadas:

- `POST /customers`
- `PUT /customers/{id}`
- `DELETE /customers/{id}`
- `GET /customers`
- `GET /customers/{id}`
- `POST /customers/{id}/deactivate`
- `GET /customers/{id}/orders`
- `DELETE /orders/{id}/details/{detailId}`

Ejemplo completo:

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

Usa `Mediator.Send` para dispatch normal. Usa `Spider.DefaultSend<TRequest, TResponse>` desde `TurtlePath.Spider` cuando el request debe pasar explicitamente por boundaries de Spider. TurtlePath es quien posee este bridge para que Spider y Pelican no se referencien entre si.

## 14. Spider Pipelines Y Transacciones

El template usa Spider execution boundaries para comportamiento transversal. El default es el transaction boundary.

```csharp
services.AddTurtlePathSpiderTransactions(
    configuration,
    typeof(Constants).Assembly,
    typeof(PipelineExtensions).Assembly);
```

El paquete `TurtlePath.Spider.Transactions` descubre requests y perfiles desde los assemblies que se pasan al registro. El template pasa el assembly de Business y el assembly de API, por eso los perfiles especificos de cada feature pueden vivir en Business sin referenciar Api.

```json
"TransactionBoundary": {
  "Enabled": true,
  "IncludeQueries": false,
  "IsolationLevel": "ReadCommitted",
  "TimeoutSeconds": 30,
  "ExcludedRequestTypes": []
}
```

Por default:

- mutations corren dentro de `TransactionScope`
- queries se omiten salvo que `IncludeQueries` sea `true`
- `[SkipTransactionBoundary]` omite la transaccion
- tipos en `ExcludedRequestTypes` se omiten
- las clases `TransactionBoundaryProfile` pueden agregar exclusiones o ensamblados para discovery por codigo
- las decisiones por tipo se descubren una vez y quedan cacheadas

```csharp
[SkipTransactionBoundary]
public sealed class RebuildSearchIndexRequest : IRequest
{
}
```

Prefiere un perfil cuando un feature o modulo tenga varias reglas de transaccion. Coloca el perfil cerca del feature, por ejemplo `Business/Search/Boundaries/Transactions/SearchTransactionBoundaryProfile.cs`:

```csharp
using TurtlePath.Spider.Transactions;

namespace Inventories.Business.Search.Boundaries.Transactions;

public sealed class SearchTransactionBoundaryProfile : TransactionBoundaryProfile
{
    public override void Configure(TransactionBoundaryOptions options)
    {
        options.Exclude<RebuildSearchIndexRequest>();
        options.DiscoverRequestsFrom<RebuildSearchIndexRequest>();
    }
}
```

El paquete `TurtlePath.Spider.Transactions` descubre perfiles de transaction boundary desde los assemblies pasados a `AddTurtlePathSpiderTransactions`. No edites `AddPipelineDefaults` para reglas especificas de un feature.

## 15. Consumers Con Pigeon Y Outbox

El template incluye Pigeon con Azure Service Bus y EF Core outbox como stack opcional. No se registra por default porque Azure Service Bus necesita una cadena de conexion real.

Para habilitarlo, configura la seccion `Pigeon` con valores reales del broker y descomenta la linea `.AddMessagingDefaults(configuration)` en `AddDefaults`.

El registro preparado es:

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

### Controlar throughput y concurrencia de consumers

Pigeon 2.4.0 permite proteger dependencias externas configurando la ejecucion de los consumers. `MaxConcurrency` es el maximo de mensajes que Pigeon puede despachar a los handlers al mismo tiempo. `QueueCapacity` es la cantidad de mensajes que pueden esperar en la cola interna mientras los handlers estan ocupados:

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

Usa `MaxConcurrency` de acuerdo con la capacidad de la dependencia mas lenta que utiliza el consumer, como el pool de conexiones de la base de datos o una API HTTP externa. Usa `QueueCapacity` para absorber picos cortos sin permitir un crecimiento ilimitado en memoria. Un valor `null` o menor que `1` deja ese limite sin restringir. Cuando `MaxConcurrency` se configura explicitamente, los adapters del broker pueden usarlo para alinear la entrega o el prefetch.

Esta configuracion controla el despacho de handlers, no el tamano de lote del outbox. Configura `Pigeon:Outbox:DispatchBatchSize` por separado cuando necesites ajustar cuantos mensajes persistidos se publican en cada ciclo. Empieza con una concurrencia conservadora, observa la latencia y los errores, e incrementala solo cuando las dependencias puedan soportar el trabajo paralelo adicional.

Consumer:

```csharp
using Billing.Service.Business.Invoices.Models.Requests;
using Billing.Service.Business.Invoices.Models.Responses;
using Pigeon.Messaging.Consuming;
using TurtlePath.Spider;
using Inventories.Api.HubConsumers;

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

`BaseHubConsumer` expone `Mediator`, `Spider` y `ConsumerExceptionBoundary`. Deja la logica de negocio en requests, handlers, automations y hooks.

## 16. Event Sourcing

El template incluye `TurtlePath.EventSourcing` y `Krackend.EventSourcing.EntityFrameworkCore` como stack opcional. No se registra por default porque event sourcing necesita nombres de streams, contratos de eventos, reglas de expected version y tablas en base de datos definidas de forma intencional.

Para habilitarlo:

1. Crea uno o mas perfiles `IEventSourcingProfile` dentro del feature dueÃ±o de los eventos.
2. Agrega los payloads de evento junto al perfil, normalmente en `Business/<Feature>/EventSourcing`.
3. Descomenta `.UseEventSourcingProfiles(typeof(Constants).Assembly)` en `AddApplicationDefaults`.
4. Descomenta `.AddEventSourcingDefaults()` en `AddDefaults`.
5. Agrega una migracion de EF Core para crear las tablas del event store.

Registro preparado de TurtlePath:

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

Registro preparado del event store con EF:

```csharp
internal static IServiceCollection AddEventSourcingDefaults(this IServiceCollection services)
{
    services.AddKrackendEntityFrameworkEventStore<AppDbContext>();

    return services;
}
```

Ejemplo de profile:

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
                context => new InvoiceEventSource(
                    context.Entity.Id.ToString(),
                    context.Entity.CustomerId.ToString(),
                    context.Entity.Total),
                options => options.UseExpectedVersion(ExpectedVersion.NoStream));

        builder.For<UpdateInvoiceRequest, Invoice>()
            .UseStream("invoices", context => context.Entity.Id.ToString())
            .ToEvent<InvoiceEventSource, InvoiceUpdated>(
                context => new InvoiceEventSource(
                    context.Entity.Id.ToString(),
                    context.Entity.CustomerId.ToString(),
                    context.Entity.Total));
    }
}

public sealed record InvoiceEventSource(string InvoiceId, string CustomerId, decimal Total);
public sealed record InvoiceCreated(string InvoiceId, string CustomerId, decimal Total);
public sealed record InvoiceUpdated(string InvoiceId, string CustomerId, decimal Total);
```

`TurtlePath.EventSourcing` trabaja por medio de hooks de command handlers. El handler guarda la entidad primero; despues el hook `AfterSave` mapea el contexto command/entity a uno o mas eventos y los agrega al store de Krackend. Asi mantienes el happy path simple y, cuando hace falta, tienes event streams.

Usalo para transiciones de dominio auditables e historial append-only. No lo actives solo para publicar mensajes de integracion; para eso usa Pigeon/outbox.

### Implementacion Completa

Usa Event Sourcing cuando el servicio necesita guardar historial append-only de transiciones de dominio. Ejemplos:

- una invoice fue creada, autorizada, pagada, cancelada o reemitida
- una order cambio de status
- cambio el perfil de riesgo de un customer
- una transicion de negocio debe poder auditarse o reproducirse

No actives Event Sourcing solo para notificar a otro servicio. Para mensajes de integracion usa Pigeon con EF outbox.

### Estructura De Carpetas

Coloca los archivos de event sourcing dentro del feature dueÃ±o de los eventos:

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

El profile describe cuando se agregan eventos. Los records de eventos describen que se guarda. Los mapping profiles describen como TurtlePath convierte un source object al payload final del evento.

Para habilitarlo:

1. Crea uno o mas perfiles `IEventSourcingProfile` dentro del feature dueÃ±o de los eventos.
2. Agrega los payloads de evento junto al perfil, normalmente en `Business/<Feature>/EventSourcing`.
3. Agrega mappings de OctoMap para cualquier proyeccion source-to-event.
4. Descomenta `.UseEventSourcingProfiles(typeof(Constants).Assembly)` en `AddApplicationDefaults`.
5. Descomenta `.AddEventSourcingDefaults()` en `AddDefaults`.
6. Agrega una migracion de EF Core para crear las tablas del event store.

### Event Payloads

Manten los eventos chicos, explicitos y faciles de versionar. Evita guardar grafos completos de entidades.

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

Cuando el evento necesita datos del command y de la entidad guardada, crea un source model pequeÃ±o:

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

### Mapping De Eventos Con OctoMap

`TurtlePath.EventSourcing` usa el `IMapperAdapter` configurado. En este template eso significa OctoMap.

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

### Crear El Profile

```csharp
using Krackend.EventSourcing.Stores;
using TurtlePath.EventSourcing;
using TurtlePath.Hooks;

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
            .ToEvent<InvoiceEventSource, InvoiceUpdated>(ToSource);

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

`UseStream("invoices", ...)` define el stream logico y el stream id. `ToEvent<TSource, TEvent>(...)` mapea el contexto command/entity a un source object, y despues usa OctoMap para crear el payload final del evento.

Usa expected versions de forma intencional:

```csharp
options.UseExpectedVersion(ExpectedVersion.NoStream); // primer evento del stream
options.UseExpectedVersion(ExpectedVersion.Any);      // append sin optimistic concurrency
```

Usa `When(...)` cuando un evento es condicional:

```csharp
.ToEvent<InvoiceEventSource, InvoiceCanceled>(
    ToSource,
    options => options.When(context => context.Entity.Canceled));
```

### Como Corre

Event Sourcing corre por medio de hooks de command handlers:

1. Pelican envia el command.
2. TurtlePath crea o actualiza la entidad.
3. El handler guarda la entidad con EF Core.
4. `EventSourcingAfterSaveHook` corre despues del save.
5. El hook resuelve el stream y mapea el contexto command/entity a payloads de eventos.
6. Krackend agrega los eventos por medio de `IEventStore`.

Esto significa que automations y command handlers base pueden emitir eventos sin escribir handlers custom. Si el happy path es suficiente, agrega el profile de Event Sourcing y deja el handler generado. Si el flujo de negocio es especial, crea un handler custom y los mismos hooks siguen aplicando despues del save.

### Migracion De EF

Despues de habilitar `.AddEventSourcingDefaults()`, agrega una migracion para que EF cree las tablas del event store de Krackend:

```powershell
dotnet ef migrations add AddEventSourcingStore `
  --project src/Billing.Service.Persistence `
  --startup-project src/Billing.Service.Api

dotnet ef database update `
  --project src/Billing.Service.Persistence `
  --startup-project src/Billing.Service.Api
```

### Probar Event Sourcing

En integration tests, habilita los mismos registros en el test host y valida que el command agregue eventos.

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

TurtlePath genera un `ExceptionDescriptor` neutral:

- `Kind`
- `Code`
- `Messages`
- `Metadata`
- `TraceIdentifier`

HTTP, consumers y workers deciden como representarlo.

Defaults:

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
```

No vuelvas a registrar los adapters de excepciones desde `CustomContainer`. El template ya llama:

```csharp
services.AddTurtlePathExceptionHandlingCore(...);
services.AddTurtlePathAspNetCoreExceptionHandling(...);
services.AddTurtlePathConsumerExceptionHandling(...);
services.AddTurtlePathWorkerExceptionHandling(...);
```

Las excepciones propias se agregan con profiles. El template descubre automaticamente los profiles de excepciones desde los ensamblados de Business y API, entonces un servicio generado solo necesita crear las clases de profile.

Usa un profile core para describir la excepcion una sola vez:

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

Usa un profile HTTP cuando la API debe devolver un status concreto:

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

Usa un profile de consumer cuando el mensaje debe completarse, relanzarse, reintentarse por broker o reportarse con una estrategia distinta:

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

Con este profile, `SubscriptionExpiredException` queda handled y complete dentro del boundary del consumer. Las demas excepciones se relanzan para que el broker aplique su retry o dead-letter normal.

Usa un profile de worker cuando jobs o background services deben comportarse distinto:

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

HTTP usa `ProblemDetails`, consumers usan `IConsumerExceptionBoundary` y jobs usan `IBackgroundExceptionBoundary`.

Despues lanza la excepcion de dominio desde un service, hook, automation action o handler manual:

```csharp
if (!subscription.IsActive)
    throw new SubscriptionExpiredException(subscription.CustomerId);
```

## 18. Jobs

Usa TurtlePath jobs cuando un workload no encaja como endpoint HTTP ni como consumer.

Hay dos formas estandar:

- one-shot jobs: el proceso arranca, ejecuta uno o varios jobs registrados, devuelve exit code y termina. Es el camino recomendado para Kubernetes `CronJob`.
- cron jobs recurrentes: el host se queda vivo y ejecuta uno o varios jobs en intervalos manejados por la aplicacion.

Puedes crear el template directamente como host one-shot:

```powershell
dotnet new turtlepath -n Billing.Jobs -o C:\work\Billing.Jobs --host job
```

El mismo Business, Domain, Persistence, automations, handlers, hooks, exception handling, boundaries de Spider, mappings de OctoMap, validators de Crabalidator y configuracion de queries con DataScorpio siguen disponibles. Solo cambia el arranque del host.

### Crear Un Job

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

MantÃ©n las clases de job delgadas. El camino recomendado es `Job -> Service` porque un proceso calendarizado normalmente orquesta trabajo de fondo de forma directa. Usa `Mediator.Send(...)` solo cuando el job reutiliza intencionalmente un request/handler existente que tambien se usa desde HTTP o consumers. No crees un handler solo para que el job lo llame; eso solo agrega boilerplate.

### One-Shot Para Kubernetes CronJob

Registra uno o varios jobs one-shot:

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

`AddJob<TJob>()` registra un job de una sola ejecucion. Cuando hay varios jobs registrados, el manager puede ejecutarlos en paralelo o secuencial segun `ExecutionMode`.

El host ejecuta los jobs one-shot registrados y convierte el resultado en exit code:

```csharp
var result = await host.Services.RunTurtlePathJobsAsync();
Environment.ExitCode = result.Succeeded ? 0 : 1;
```

Puedes ejecutar solo algunos jobs cuando el host contiene varios pero un deployment debe correr un subconjunto:

```csharp
var result = await host.Services.RunTurtlePathJobsAsync(
    new[] { typeof(ImportInvoicesJob), typeof(CloseExpiredInvoicesJob) },
    cancellationToken);
```

Usa one-shot jobs para workloads de Kubernetes `CronJob`, donde Kubernetes controla el calendario y TurtlePath controla scoped DI, retries, exception handling y ejecucion paralela.

Ejemplo de manifiesto Kubernetes:

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

Opciones one-shot:

- `ExecutionMode`: `Parallel` ejecuta jobs registrados al mismo tiempo; `Sequential` los ejecuta uno por uno.
- `MaxDegreeOfParallelism`: limita cuantos jobs corren al mismo tiempo cuando `ExecutionMode` es `Parallel`.
- `Retries`: numero de reintentos despues del primer fallo.
- `RetryDelay`: espera entre reintentos.
- `FailureBehavior`: `Rethrow` falla la ejecucion, `Continue` registra el fallo y sigue, `StopHost` pide detener el host.

### Cron Jobs Recurrentes

Primero crea la clase del job recurrente igual que cualquier job de TurtlePath:

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

Registra jobs recurrentes cuando el mismo host debe quedarse vivo:

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

Registra varios cron jobs en el mismo host:

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

Opciones recurrentes:

- `Every(TimeSpan interval)`: define el intervalo exacto.
- `EverySeconds(int seconds)`: shortcut para intervalos por segundos.
- `EveryMinutes(int minutes)`: shortcut para intervalos por minutos.
- `EveryHours(int hours)`: shortcut para intervalos por horas.
- `Interval`: intervalo final usado por el hosted service.
- `RunOnStart`: ejecuta inmediatamente cuando arranca el host en vez de esperar al primer intervalo.
- `Retries`, `RetryDelay` y `FailureBehavior`: aplican el mismo comportamiento de retries/fallos que one-shot, pero en cada ciclo de ejecucion.

Se soportan multiples cron jobs. Cada job registrado corre su propio loop dentro de `TurtlePathCronJobHostedService`, asi que cada uno puede tener su propio intervalo, retry policy y failure behavior.

## 19. Testing

El template trae una base de testing para que las pruebas de features no empiecen desde cero. El dev debe escribir el caso de uso y los asserts; el template deja listo el test host de TurtlePath, Pelican, OctoMap, Crabalidator, Spider, DataScorpio, SQLite, jobs y exception handling.

Usa esta division:

- unit tests para handlers manuales, hooks y servicios pequeÃ±os
- integration tests para automations porque los handlers se generan
- integration tests con Spider cuando las transacciones o execution boundaries son parte del contrato del caso de uso
- integration tests con SQLite para configuracion EF, conversiones de CId, traduccion de queries y filtros de DataScorpio
- composition tests para validar que el host del template arranca con los defaults esperados
- tests de jobs para registros one-shot y cron
- tests de exceptions cuando un feature tenga mappings propios

El proyecto de pruebas generado incluye `Testing/TemplateTestHost.cs`. Usa ese wrapper en vez de configurar cada paquete en cada test.

### Unit Test De Handler

Usa unit test cuando tu servicio tiene un handler concreto. Los maps y validators por delegado evitan levantar todo el stack de adapters:

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

Este es el camino mas rapido para handlers custom porque prueba el handler directo con storage en memoria.

### Integration Test De Automations Con SQLite

Usa integration tests para automations porque TurtlePath.Automations genera el handler y Pelican lo resuelve. SQLite mantiene la prueba cerca del comportamiento real de EF sin requerir una base de datos externa:

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

Usa este estilo para create, update, delete, get by id y flujos paginados con automations.

### Integration Test De Handler Manual Con Pelican

Cuando el handler es manual pero quieres probar el mismo camino de dispatch que usan controllers y consumers, llama `host.SendAsync(...)`:

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

Esto prueba que el request esta registrado en Pelican y puede resolverse por DI.

### Integration Test Con Spider

Usa pruebas con Spider para casos de uso que deben correr por execution boundaries. Esto importa para transacciones, orden de boundaries y flujos llamados desde controllers o consumers por medio de `Spider`.

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

Usa una prueba directa del filtro cuando lo importante sea validar si un request debe abrir transaccion:

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

Usa pruebas con Pelican cuando solo necesitas validar dispatch de handlers. Usa pruebas con Spider cuando el boundary sea parte del contrato del caso de uso.

### Probar Filtros De DataScorpio

Usa SQLite cuando el query debe probar filtros, sorts, search o paginado traducidos correctamente:

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

### Probar Hooks

Usa hook tracing cuando necesites probar que una etapa de hooks se ejecuto:

```csharp
await using var host = await TemplateTestHost
    .CreateUnitHost()
    .TraceHooks()
    .BuildAsync();

var trace = host.Resolve<HookTrace>();
Assert.Contains(trace.Entries, entry => entry.Stage == "AfterSave");
```

Usa tracing para hooks de auditoria, event sourcing, publicacion y validaciones que deben correr alrededor de una etapa del handler.

### Probar Exception Mappings

Los mappings de exceptions propios de un feature se prueban una vez para asegurar que HTTP, consumers y jobs reciban el descriptor esperado:

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

### Probar Jobs

Registra jobs en el test host y ejecutalos sin levantar toda la app:

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

Deja al menos un composition test por variante de host:

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

Los composition tests deben mantenerse aburridos. Su valor es detectar registros rotos, adapters faltantes, configuracion invalida o cambios accidentales en defaults de arranque.

## 20. Documentacion Externa

Referencias utiles:

- [TurtlePath NuGet](https://www.nuget.org/packages/TurtlePath)
- [Inventories NuGet](https://www.nuget.org/packages/Inventories)
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

Usa las paginas de NuGet para confirmar comandos de instalacion, frameworks soportados, versiones, dependencias y ejemplos del README. Usa la documentacion del repositorio de cada paquete cuando necesites comportamiento especifico de algun adapter.
