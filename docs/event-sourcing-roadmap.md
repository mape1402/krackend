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
  -> projections
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

## Lectura, Append Y Versiones

Append no debe cargar eventos del stream.

La version esperada debe ser una politica explicita:

```csharp
await eventStore.AppendAsync(streamName, streamId, ExpectedVersion.Any, events);
await eventStore.AppendAsync(streamName, streamId, ExpectedVersion.NoStream, events);
await eventStore.AppendAsync(streamName, streamId, ExpectedVersion.Exact(version), events);
```

Uso recomendado:

- `ExpectedVersion.Any`: hooks CRUD / committed event log, donde el evento registra algo que ya paso.
- `ExpectedVersion.NoStream`: creates estrictos donde el stream no debe existir.
- `ExpectedVersion.Exact(version)`: event sourcing real, despues de rehidratar y decidir sobre una version concreta.

`ReadStreamAsync(streamName, streamId, fromVersion, maxCount)` es una API de infraestructura. La aplicacion no deberia adivinar `fromVersion`:

- Sin snapshot, el rehydrator empieza desde version `1`.
- Con snapshot, el rehydrator empieza desde `snapshot.StreamVersion + 1`.
- Para herramientas/proyecciones/rebuilds, el caller avanzado puede controlar rango y batch.

## Ergonomia De Streams

El nombre del stream y el id no deben aparecer como strings en el handler o en el caso de uso.

API deseada:

```csharp
await eventSourcedService.ExecuteAsync(
    CustomerState.Empty,
    new CreateCustomer("customer-001", "Mario", "mario@example.com"));
```

La libreria resuelve el stream con:

- `IEventStreamCommand` para comandos simples que ya pueden exponer `StreamId`.
- `ICommandStreamResolver<TCommand>` para integraciones donde el comando no debe modificarse.
- `EventRoutingOptions` para mapear comandos a stores logicos.

Esto permite que un template use `BaseRequest.Id` desde un resolver externo sin que los handlers base ni los handlers concretos tengan literals del event store.

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

La integracion EF Core debe agregar entidades del event store al modelo del `DbContext` de la app, siguiendo el mismo patron de integracion al modelo que ya se usa en otras librerias de Krackend:

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
- `PostProcess`: append committed events y ejecutar proyecciones.
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

El encaje con event sourcing no debe ser que el handler llame manualmente a `IEventStore`.

El encaje debe ser:

```txt
Handle
  -> ValidateWithHooks
  -> Map/Get/Patch/Delete with hooks
  -> SaveWithHooks
       -> template hace su persistencia actual
       -> event sourcing hook genera/commitea evento dentro del mismo DbContext
  -> ResponseWithHooks
```

Para servicios CRUD actuales, el hook usa adapters:

- `ICommandStreamResolver<TRequest>`: obtiene stream name/id desde el request o entidad.
- `ICommandEventFactory<TRequest, TEntity>`: convierte request + entidad + operacion en evento.
- `IEventStore`: persiste el envelope en la tabla configurada.

Asi el dev que ya hereda de `CreateCommandHandler`, `UpdateCommandHandler` o `DeleteCommandHandler` no tiene que cambiar su handler. Solo registra la integracion y, si su request no expone id suficiente, agrega un resolver/factory tipado.

## Snapshots

Snapshots no deben crearse por default dentro del request path.

La ruta caliente de un comando puede leer el ultimo snapshot para no rehidratar desde cero, pero generar snapshots debe ser trabajo aparte:

- background service
- scheduled job
- worker de mantenimiento
- proceso de rebuild controlado

Ese worker debe operar sobre streams candidatos, no recorrer todos los aggregates a ciegas. Una forma razonable:

```txt
stream candidate
  -> load latest snapshot
  -> read events after snapshot in batches
  -> reduce state
  -> save snapshot at current version
```

La politica de candidatos puede salir de metricas simples:

- streams con mas de N eventos desde el ultimo snapshot
- streams modificados recientemente
- streams marcados por el append path como "snapshot due"

El punto importante: snapshot creation es mantenimiento asíncrono, no trabajo obligatorio del handler.

Implementacion base:

- Append marca candidatos mediante `ISnapshotCandidateMarker`.
- La politica default no marca nada.
- `IntervalSnapshotCandidatePolicy` marca streams con N eventos desde el ultimo snapshot.
- `ISnapshotProcessor<TState>` procesa candidatos por batches y guarda snapshots.
- EF Core agrega tablas para snapshots y candidatos al mismo `DbContext` de la app.
- El worker real puede llamar `ProcessPendingAsync` en un `BackgroundService` o job programado.

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
5. Spider extension.
6. Hooks evolutivos en templates.
7. Pigeon integration.
8. Snapshots.
9. Testing helpers para decider/reducer/state.

## Criterio Para Primer Release

La primera version debe permitir:

- Configurar multiples stores logicos.
- Agregar tablas del event store al `DbContext` de la app.
- Descubrir eventos, deciders y reducers por assembly scanning.
- Rehidratar state desde stream.
- Ejecutar decider.
- Append con expected version.
- Agregar metadata dinamica.
- Correr sample SQLite completo.
