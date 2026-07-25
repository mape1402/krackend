# Krackend Event Sourcing Roadmap

## Objetivo

Construir una libreria standalone para event sourcing en .NET, independiente de templates, mediators, mensajeria o frameworks especificos de Krackend.

La libreria debe resolver primero el problema central:

- Persistir eventos como fuente de verdad.
- Soportar multiples tablas o stores de eventos definidos por configuracion.
- Permitir un envelope base extensible.
- Rehidratar estado desde streams de eventos.
- Generar proyecciones y eventos de integracion sin acoplar el dominio a infraestructura externa.

La integracion con templates existentes, Pelican, Pigeon, pipelines o DbContext debe planearse como adaptadores posteriores, no como requisito del core.

## Principios De Diseno

- El evento representa un hecho aceptado por el bounded context, no el request crudo.
- El event store local del bounded context es privado y es la fuente de verdad del dominio.
- Las proyecciones son derivadas y regenerables.
- El event log central, si existe, debe tratarse como log de integracion, auditoria o distribucion, no como fuente canonica de todos los dominios.
- La libreria debe exponer contratos pequenos y estables.
- La configuracion debe resolver detalles fisicos como tabla, schema, serializer y routing.
- La aplicacion no debe depender de nombres fisicos de tablas al escribir eventos.

## Alcance Inicial

Incluido:

- Event store local.
- Multiples event tables configurables.
- Envelope base con metadata extensible.
- Append con concurrencia optimista.
- Load por stream.
- Serializacion configurable.
- Registro y resolucion de tipos de evento.
- Rehidratacion de aggregates.
- Testing helpers.

Fuera del primer alcance:

- Integracion obligatoria con EF Core.
- Integracion obligatoria con Pelican.Mediator.
- Publicacion obligatoria con Pigeon.Messaging.
- Event store centralizado como fuente de verdad compartida.
- Snapshots avanzados.
- Proyecciones distribuidas complejas.

## Modelo Conceptual

Flujo principal:

```txt
command/request
  -> application
  -> load stream
  -> rehydrate aggregate
  -> domain decision
  -> pending events
  -> append to configured event store
  -> committed events
  -> projections / outbox / integration log
```

Estados de un evento:

```txt
Pending event
  Existe en memoria como resultado de una decision de dominio.

Committed event
  Fue persistido exitosamente en el event store.
```

## Fase 1: Core Contracts

Definir los contratos minimos sin dependencia de storage especifico.

Entregables:

- `IEvent`
- `IEventEnvelope`
- `IEventStore`
- `IEventSerializer`
- `IEventTypeRegistry`
- `IAggregateRoot`
- `IEventStreamResolver`
- `IEventMetadataProvider`

API esperada:

```csharp
public interface IEventStore
{
    Task<IReadOnlyCollection<EventEnvelope>> LoadAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default);
}
```

Decisiones:

- `streamName` identifica configuracion logica, no tabla fisica.
- `streamId` identifica el aggregate o flujo de negocio.
- `expectedVersion` habilita concurrencia optimista.

## Fase 2: Envelope Base Y Extensibilidad

Definir un envelope base estable.

Campos base recomendados:

- `EventId`
- `StreamName`
- `StreamId`
- `StreamType`
- `StreamVersion`
- `GlobalPosition`
- `EventType`
- `EventVersion`
- `OccurredAt`
- `Payload`
- `Metadata`

Metadata recomendada:

- `CorrelationId`
- `CausationId`
- `TenantId`
- `UserId`
- `TraceId`
- `Source`

Regla de diseno:

Los campos estructurales necesarios para guardar, ordenar, rehidratar y aplicar concurrencia deben ser parte del contrato base. La metadata puede ser dinamica.

Configuracion esperada:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.Envelope.AddMetadata("tenantId", provider => provider.GetRequiredService<ITenantAccessor>().TenantId);
    options.Envelope.AddMetadata("traceId", provider => Activity.Current?.TraceId.ToString());
});
```

## Fase 3: Multiples Event Stores O Tablas

Soportar multiples stores logicos sobre una misma base de datos o multiples bases.

Ejemplo:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("orders", store =>
    {
        store.Schema = "orders";
        store.TableName = "events";
    });

    options.Stores.Add("payments", store =>
    {
        store.Schema = "payments";
        store.TableName = "events";
    });

    options.Stores.Add("integration", store =>
    {
        store.Schema = "integration";
        store.TableName = "events";
    });
});
```

Routing por tipo:

```csharp
options.Routing.Route<OrderCreated>("orders");
options.Routing.Route<OrderPaid>("orders");
options.Routing.Route<PaymentAuthorized>("payments");
options.Routing.Route<OrderPlacedIntegrationEvent>("integration");
```

Reglas:

- La aplicacion escribe contra un store logico.
- La infraestructura resuelve tabla, schema, serializer y dialecto.
- Una tabla debe poder almacenar multiples tipos de evento.
- Un bounded context puede tener mas de una tabla si necesita separar dominio, integracion, auditoria o alta cardinalidad.

## Fase 4: Storage Provider Inicial

Implementar primero un provider relacional.

Prioridad:

1. SQL Server.
2. PostgreSQL.
3. SQLite/InMemory para tests.

Tabla base propuesta:

```sql
EventId uniqueidentifier not null
StreamName nvarchar(200) not null
StreamId nvarchar(300) not null
StreamType nvarchar(300) null
StreamVersion bigint not null
GlobalPosition bigint identity not null
EventType nvarchar(500) not null
EventVersion int not null
OccurredAt datetimeoffset not null
Payload nvarchar(max) not null
Metadata nvarchar(max) null
```

Indices:

- Unique: `(StreamName, StreamId, StreamVersion)`
- Unique: `EventId`
- Ordered read: `(StreamName, StreamId, StreamVersion)`
- Subscription read: `(GlobalPosition)`
- Type scan: `(EventType, GlobalPosition)`

## Fase 5: Aggregate Support

Proveer una base opcional para aggregates.

```csharp
public abstract class AggregateRoot
{
    private readonly List<object> _pendingEvents = [];

    public string Id { get; protected set; }
    public long Version { get; private set; }

    public IReadOnlyCollection<object> PendingEvents => _pendingEvents;

    protected void Raise(object @event);
    public void LoadFromHistory(IEnumerable<object> events);
    public void ClearPendingEvents();
}
```

Regla:

El core debe permitir usar aggregates propios sin heredar de una clase base. La clase base es conveniencia, no requisito.

## Fase 6: Repository Opcional

Agregar un repository para simplificar rehidratacion y append.

```csharp
public interface IEventSourcedRepository<TAggregate>
{
    Task<TAggregate> LoadAsync(string streamId, CancellationToken cancellationToken = default);
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
```

Responsabilidades:

- Resolver `streamName`.
- Cargar eventos.
- Rehidratar aggregate.
- Guardar eventos pendientes con `expectedVersion`.
- Limpiar eventos pendientes tras append exitoso.

## Fase 7: Projections

Implementar un modelo de proyecciones desacoplado.

Contratos:

- `IProjection`
- `IProjectionHandler<TEvent>`
- `ICheckpointStore`
- `IProjectionRunner`

Checkpoint minimo:

- `ProjectionName`
- `StoreName`
- `LastGlobalPosition`
- `UpdatedAt`

Reglas:

- Las proyecciones consumen committed events.
- Los handlers deben poder reprocesarse.
- La libreria debe soportar proyecciones sync para casos simples y async/background para casos reales.

## Fase 8: Outbox E Integration Events

Agregar un mecanismo para derivar integration events desde domain events.

Flujo:

```txt
domain event committed
  -> integration event mapper
  -> outbox append
  -> publisher externo
  -> central event log / broker
```

Contratos:

- `IIntegrationEventMapper<TDomainEvent>`
- `IOutboxStore`
- `IOutboxPublisher`

Regla:

El outbox no debe ser requerido para usar event sourcing local. Debe ser modulo opcional.

## Fase 9: Versionado Y Upcasting

Agregar soporte para evolucionar eventos.

Contratos:

- `IEventUpcaster`
- `IEventVersionResolver`

Reglas:

- El envelope guarda `EventVersion`.
- El registry resuelve version y tipo CLR.
- Los upcasters transforman payload viejo antes de entregar el evento al dominio.

## Fase 10: Snapshots

Agregar snapshots solo despues de validar necesidad real.

Contratos:

- `ISnapshotStore`
- `ISnapshotSerializer`
- `ISnapshotStrategy`

Estrategias:

- Cada N eventos.
- Por tiempo.
- Manual.
- Por tipo de aggregate.

Regla:

Snapshots optimizan lectura; no reemplazan el event store.

## Fase 11: Testing Helpers

Crear utilidades para probar aggregates y handlers.

Ejemplo:

```csharp
Given(events)
    .When(command)
    .Then(expectedEvents);
```

Casos:

- Concurrencia optimista.
- Metadata generada.
- Routing correcto.
- Rehidratacion.
- Upcasting.
- Proyecciones idempotentes.

## Fase 12: Integraciones Planeadas

Estas integraciones deben vivir fuera del core.

### Krackend Templates

Objetivo:

- Permitir que un template CRUD/CQRS escriba committed events u outbox sin modificar handlers base.
- Integrar mediante pipeline behavior, decorator o adapter.

Posible integracion:

```txt
Pelican request
  -> TransactionPipelineBehavior
  -> handler existente
  -> committed event behavior
  -> local outbox/event log
```

Riesgo:

Si el handler llama `SaveChangesAsync` internamente, el behavior transaccional solo agrupa por `TransactionScope`. La integracion debe asegurar que el committed event se escriba antes de completar la transaccion.

### Pelican.Mediator

Objetivo:

- Agregar behavior opcional para commands que producen eventos.

Opciones:

- Marker interface: `IProducesEvents`
- Factory: `ICommittedEventFactory<TRequest, TResponse>`
- Decorator del handler.

Recomendacion:

Preferir factory configurable sobre marker interface cuando se quiera evitar contaminar commands.

### Pigeon.Messaging

Objetivo:

- Publicar outbox events mediante Pigeon.
- Mantener idempotencia y retry fuera del dominio.

Integracion:

```txt
OutboxStore
  -> BackgroundPublisher
  -> Pigeon publisher
```

### EF Core

Objetivo:

- Provider relacional y migraciones.
- Interceptors opcionales para casos outbox/committed log.

Restriccion:

El core no debe depender directamente de EF Core si puede evitarse. EF Core debe ser provider/adaptador.

## Paquetes Propuestos

```txt
Krackend.EventSourcing.Abstractions
Krackend.EventSourcing.Core
Krackend.EventSourcing.SqlServer
Krackend.EventSourcing.PostgreSql
Krackend.EventSourcing.EntityFrameworkCore
Krackend.EventSourcing.Projections
Krackend.EventSourcing.Outbox
Krackend.EventSourcing.Pelican
Krackend.EventSourcing.Pigeon
Krackend.EventSourcing.Testing
```

## Orden Recomendado De Implementacion

1. `Abstractions`: contracts, envelope, options.
2. `Core`: registry, serializer, metadata providers, routing.
3. `SqlServer`: append/load con concurrencia optimista.
4. `Testing`: tests de event store y aggregates.
5. `AggregateRoot` y repository opcional.
6. `Projections`: checkpoint y runner simple.
7. `Outbox`: store y publisher contract.
8. `Pelican` adapter.
9. `Pigeon` adapter.
10. `PostgreSql` provider.
11. Upcasters.
12. Snapshots.

## Preguntas Pendientes

- El primer provider debe ser SQL Server, PostgreSQL o ambos?
- El event store debe crear tablas automaticamente o solo exponer migraciones/scripts?
- El envelope fisico debe ser columnar, JSON completo o hibrido?
- Los eventos se guardaran como clases CLR, records, o payloads anonimos serializables?
- Se requiere multi-tenant desde el primer release?
- El central event store sera un paquete separado o solo un patron documentado mediante outbox?
- El primer caso de uso sera event sourcing real o committed outbox para servicios CRUD/CQRS existentes?

## Criterio Para Primer Release

La primera version debe permitir:

- Configurar al menos dos stores logicos con tablas distintas.
- Registrar tipos de eventos.
- Guardar eventos con append atomico y concurrencia optimista.
- Leer un stream completo en orden.
- Rehidratar un aggregate.
- Agregar metadata dinamica.
- Probar el flujo con helpers.

No debe requerir templates, Pelican, Pigeon ni EF Core como dependencia obligatoria.
