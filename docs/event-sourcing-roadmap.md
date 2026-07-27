# Krackend Event Sourcing Roadmap

## Objetivo

Construir una libreria de event sourcing para .NET que pueda usarse de forma standalone y que tambien pueda integrarse con el ecosistema Krackend sin obligar a reescribir servicios existentes.

La prioridad es:

- Core real de event sourcing, sin reflection y sin aggregates magicos.
- Estado rehidratado mediante reducers explicitos.
- Decisiones de negocio mediante deciders explicitos.
- Event store EF Core integrado al mismo `DbContext` de la app.
- Extension rica para Spider.
- Evolucion transparente de los handlers base mediante hooks.

No se implementara integracion para Mediator directo por ahora.

## Principios

- El evento es un hecho aceptado por el bounded context.
- El estado se rehidrata reduciendo eventos.
- La decision de negocio produce eventos, no muta entidades directamente.
- No usar reflection para aplicar eventos.
- No exigir heredar de `AggregateRoot`.
- No obligar a los devs a cambiar controllers o handlers existentes.
- EF Core es el provider principal para integrarse al `DbContext` de la app.
- Spider es la integracion principal para composicion avanzada.

## Modelo Core

Flujo:

```txt
command
  -> load stream
  -> deserialize events
  -> reduce events into state
  -> decider decides new events
  -> append events with expected version
  -> reduce new events into current state
  -> projections/outbox/integration
```

Contratos principales:

```csharp
public interface IEventDecider<TState, TCommand>
{
    ValueTask<IReadOnlyCollection<object>> DecideAsync(
        TState state,
        TCommand command,
        CancellationToken cancellationToken = default);
}

public interface IEventReducer<TState, TEvent>
{
    TState Apply(TState state, TEvent @event);
}

public interface IEventReducerRegistry
{
    IEventReducerRegistry Register<TState, TEvent>(Func<TState, TEvent, TState> reducer);
    TState Apply<TState>(TState state, object @event);
}
```

El reducer registry debe ejecutar delegates cacheados, no reflection.

## Event Store

El event store mantiene:

- Multiples stores logicos.
- Multiples tablas configurables.
- Envelope base estable.
- Metadata extensible.
- Append con concurrencia optimista.
- Load por stream.
- Read por global position para proyecciones.

Envelope:

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

## EF Core

La integracion EF Core debe agregar entidades del event store al modelo del `DbContext` de la app, siguiendo el patron usado por Pigeon Outbox:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });
});

services.AddKrackendEntityFrameworkEventStore<AppDbContext>();
```

La app no debe necesitar:

```csharp
public DbSet<EventStoreRecord> Events { get; set; }
```

## Spider Extension

Spider sera la integracion avanzada.

Objetivo:

- Permitir plug-in de event sourcing en pre/post/middleware/boundaries.
- Componer event sourcing con otras capacidades.
- Evitar meter toda la logica en handlers o controllers.

Posible API:

```csharp
spider.AsMediator()
    .DefaultForwading<CreateCustomer, CustomerResult>()
    .UseEventSourcing<CustomerState, CreateCustomer>("customers", command => command.CustomerId)
    .Send(command);
```

Puntos naturales:

- `PreProcess`: cargar metadata/correlation, preparar stream.
- `Middleware`: envolver ejecucion con contexto event sourced.
- `PostProcess`: append committed events, ejecutar proyecciones, outbox.
- `Boundary`: lifecycle de complete/fault/cancel.

## Hooks En Templates

Los handlers base pueden evolucionar sin breaking change si los metodos virtuales actuales permanecen.

Ejemplo:

```csharp
await ValidateWithHooksAsync(request, cancellationToken);
```

Implementacion conceptual:

```csharp
protected virtual ValueTask ValidateAsync(TRequest request, CancellationToken cancellationToken);

private async ValueTask ValidateWithHooksAsync(TRequest request, CancellationToken cancellationToken)
{
    await Hooks.PreValidationAsync(context, cancellationToken);
    await ValidateAsync(request, cancellationToken);
    await Hooks.PostValidationAsync(context, cancellationToken);
}
```

Hooks por etapa:

- `PreValidation`
- `PostValidation`
- `PreMapToEntity`
- `PostMapToEntity`
- `PreSaveEntity`
- `PostSaveEntity`
- `PreMapToResponse`
- `PostMapToResponse`
- `PreGetEntity`
- `PostGetEntity`
- `PreUpdateEntity`
- `PostUpdateEntity`
- `PreDeleteEntity`
- `PostDeleteEntity`

Esto permite registrar comportamiento externo sin obligar a los devs a reescribir handlers existentes.

## Committed Events Para Handlers CRUD

Para el template actual, existe otro caso distinto a event sourcing puro:

```txt
handler CRUD actual
  -> valida
  -> mapea entidad
  -> guarda proyeccion/estado actual
  -> PostSave hook produce committed event
  -> append al event store
```

Esto debe nombrarse como committed events/event log transaccional, no como event sourcing puro.

## Paquetes Propuestos

```txt
Krackend.EventSourcing
Krackend.EventSourcing.Spider
Krackend.EventSourcing.TemplateHooks
Krackend.EventSourcing.Pigeon
Krackend.EventSourcing.Testing
```

## Orden Recomendado

1. Core state/decider/reducer sin reflection.
2. EF event store integrado al app `DbContext`.
3. SQLite sample usando commands, state, deciders y reducers.
4. Projections y checkpoints.
5. Outbox/integration events.
6. Spider extension.
7. Hooks evolutivos en templates.
8. Pigeon integration.
9. Snapshots.
10. Testing helpers para decider/reducer/state.

## Criterio Para Primer Release

La primera version debe permitir:

- Configurar multiples stores logicos.
- Agregar tablas del event store al `DbContext` de la app.
- Registrar eventos y reducers.
- Rehidratar state desde stream.
- Ejecutar decider.
- Append con expected version.
- Agregar metadata dinamica.
- Correr sample SQLite completo.
