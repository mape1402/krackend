# Krackend Event Sourcing

`Krackend.EventSourcing` implementa el runtime base para persistir eventos, rehidratar state con reducers, manejar snapshots y construir envelopes con metadata tecnica.

La libreria esta separada por paquetes para evitar acoplar el core con storage, proyecciones o integraciones externas.

## Paquetes

- `Krackend.EventSourcing.Abstractions`: contratos publicos, attributes, envelopes, stores, snapshots, streams y excepciones.
- `Krackend.EventSourcing`: runtime core, serializers default, registry, reducers, rehidratacion, snapshots y DI base.
- `Krackend.EventSourcing.EntityFrameworkCore`: adapter EF Core para event store, snapshots y candidates integrados al `DbContext` de la app.
- `Krackend.EventSourcing.Projections`: runtime opcional de proyecciones.
- `Krackend.EventSourcing.SpiderExtensions`: extension point opcional para Spider.
- `Krackend.EventSourcing.PelicanExtensions`: extension point opcional para Pelican/templates.
- `Krackend.EventSourcing.Analyzers`: diagnostics Roslyn para errores comunes de schema/reducers.

## Evento

Cada evento persistible debe tener un schema estable:

```csharp
[EventSchema("CustomerBalanceMoved", "1.1.0")]
public sealed record CustomerBalanceMoved(string CustomerId, decimal Amount, decimal Balance);
```

`EventSchema.Name` y `EventSchema.Version` son parte del contrato persistido. Dos CLR types pueden representar el mismo evento de negocio en versiones distintas, pero no pueden compartir el mismo `name + version`.

El registro por atributo es recomendado:

```csharp
registry.Register<CustomerBalanceMoved>();
```

El registro manual sigue soportado para escenarios dinamicos:

```csharp
registry.Register<CustomerBalanceMoved>("CustomerBalanceMoved", "1.1.0");
```

## State

El state tambien tiene schema propio:

```csharp
[StateSchema("CustomerState", "1.0.0")]
public sealed record CustomerState(string CustomerId, string Name, decimal Balance);
```

El schema del state no es el schema del evento. Un evento puede mantenerse igual mientras el state cambia, o al reves.

Para application services, registra el estado inicial una sola vez en DI:

```csharp
services.AddEventSourcedInitialState(() => CustomerState.Empty);
```

Con eso el flujo normal no pide `initialState` por cada ejecucion:

```csharp
await customerService.ExecuteAsync(new RenameCustomer("customer-001", "New Name"));
```

Las sobrecargas que reciben `initialState` siguen disponibles para tests o escenarios avanzados.

## Reducers

Cada version de evento que participa en la rehidratacion debe tener reducer exacto:

```csharp
reducers.Register<CustomerState, CustomerBalanceMovedV1>((state, @event) =>
    state with { Balance = @event.Balance });

reducers.Register<CustomerState, CustomerBalanceMovedV2>((state, @event) =>
    state with { Balance = @event.NewBalance });
```

Si falta reducer, la libreria falla con `EventReducerNotRegisteredException`. No se ignoran eventos durante rehidratacion.

## Lecturas

La API publica no expone lectura ilimitada de streams.

Usa:

```csharp
ReadStreamAsync(streamName, streamId, fromVersion, maxCount)
```

La rehidratacion calcula `fromVersion`:

- sin snapshot: `1`
- con snapshot: `snapshot.StreamVersion + 1`

## Append

El append debe usar version esperada:

```csharp
ExpectedVersion.Any
ExpectedVersion.NoStream
ExpectedVersion.Exact(version)
```

`Any` existe para escenarios donde no quieres control optimista, por ejemplo committed events de hooks. Para flujos event-sourced estrictos, prefiere `Exact(version)`.

## Snapshots

El snapshot es del state:

```txt
events -> reducers -> TState -> snapshot payload
```

No es snapshot del request, envelope, entidad EF ni proyeccion.

La tabla de snapshots guarda metadata tecnica:

- `StreamName`
- `StreamId`
- `StreamVersion`
- `StateType`
- `StateSchemaVersion`
- `Payload`
- `CreatedAt`

`Payload` contiene solamente el state serializado.

La generacion de snapshots debe vivir fuera del request path: worker, job programado o proceso de mantenimiento. La ruta caliente puede leer snapshots, pero no debe depender de generarlos.

## EF Core

El adapter EF permite integrar las tablas del event store al `DbContext` de la app:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });
});

services.AddKrackendEntityFrameworkEventStore<AppDbContext>();
```

La tabla default debe nombrarse `Events`.

## Errores

Las excepciones publicas viven en `Krackend.EventSourcing.Diagnostics`:

- `EventSchemaMissingException`
- `DuplicateEventSchemaException`
- `EventTypeNotRegisteredException`
- `EventReducerNotRegisteredException`
- `StateSchemaMissingException`
- `SnapshotStateSchemaMismatchException`
- `EventPayloadDeserializationException`
- `SnapshotDeserializationException`
- `SnapshotSerializerMissingException`
- `EventStoreConcurrencyException`

## Analyzers

Los analyzers basicos detectan:

- `KES0001`: dos CLR types con el mismo `EventSchema(name, version)`.
- `KES0002`: reducer para evento sin `[EventSchema]`.

Estos diagnostics complementan los errores runtime. El registro manual de eventos sigue permitido, asi que no todo se puede validar estaticamente.
