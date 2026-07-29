# Krackend Event Sourcing Roadmap

## Objetivo

Convertir `Krackend.EventSourcing` en una libreria estable, modular y extensible para event sourcing en .NET.

La version estable debe dejar un core pequeno y claro, con adapters de storage separados, versionado explicito de eventos y state, snapshots seguros, errores ruidosos cuando falten piezas criticas, y analyzers que detecten problemas antes de ejecutar la aplicacion.

## Principios De Diseno

- El core contiene abstracciones y comportamiento de event sourcing, no storage concreto.
- Los eventos se identifican por `EventSchema(name, version)`.
- El state se identifica por su propio schema, separado del schema del evento.
- Cada evento persistido se resuelve por `EventType + EventSchemaVersion`.
- Si falta un reducer para `TState + TEvent`, debe fallar.
- El registro manual de eventos sigue permitido.
- No hay aplicacion magica de eventos por reflection.
- No hay aggregates base obligatorios.
- No se carga un stream completo en APIs de produccion.
- EF Core, ADO, Mongo u otros storages son adapters intercambiables.
- Proyecciones viven en paquete separado.
- Spider, Pelican, templates u otras librerias del ecosistema son extensiones, no parte del core.
- Los analyzers deben ayudar a detectar configuraciones incompletas.

## Paquetes Objetivo

```txt
Krackend.EventSourcing
Krackend.EventSourcing.Abstractions
Krackend.EventSourcing.EntityFrameworkCore
Krackend.EventSourcing.Projections
Krackend.EventSourcing.SpiderExtensions
Krackend.EventSourcing.PelicanExtensions
Krackend.EventSourcing.Analyzers
Krackend.EventSourcing.Testing
Krackend.EventSourcing.Pelican.Sample
```

### `Krackend.EventSourcing.Abstractions`

Contratos publicos compartidos:

- `IEventStore`
- `IEventLogReader`
- `IEventTypeRegistry`
- `IEventReducer<TState, TEvent>`
- `IEventReducerRegistry`
- `IStateRehydrator`
- `ISnapshotStore`
- `ISnapshotProcessor<TState>`
- `IEventSerializer`
- `ISnapshotSerializer`
- `ExpectedVersion`
- `EventSchemaAttribute`
- `StateSchemaAttribute`
- `SemanticVersion`
- envelope contracts

### `Krackend.EventSourcing`

Core runtime:

- registro de schemas
- envelope factory
- rehidratacion de state
- reducer registry
- decider/application service
- snapshot processor
- serializers default
- metadata collector
- stream routing
- DI basico

Este paquete no debe tener referencias a EF Core.

### `Krackend.EventSourcing.EntityFrameworkCore`

Adapter EF Core:

- `EntityFrameworkEventStore<TDbContext>`
- `EntityFrameworkSnapshotStore<TDbContext>`
- `EntityFrameworkSnapshotCandidateStore<TDbContext>`
- model builder extensions
- integration con el `DbContext` de la app
- configuracion de multiples stores/tablas

### `Krackend.EventSourcing.Projections`

Runtime de lectura:

- projection handlers
- checkpoint store
- projection runner
- adapters de checkpoints
- rebuild helpers

Este paquete debe depender de abstractions/core, pero no mezclar logica de snapshots.

### `Krackend.EventSourcing.SpiderExtensions`

Integracion opcional con Spider.

Debe contener solamente:

- adapters para conectar event sourcing al pipeline de Spider
- hooks/middlewares propios de Spider
- DI extensions especificas de Spider
- documentacion de integracion Spider

No debe contener core runtime, storage, EF Core, snapshots ni proyecciones.

### `Krackend.EventSourcing.PelicanExtensions`

Integracion opcional con Pelican/templates.

Debe contener solamente:

- hooks para handlers base
- committed event factories/mappers
- resolvers para requests del template
- DI extensions especificas de Pelican/templates

No debe formar parte de `Krackend.EventSourcing` core.

### `Krackend.EventSourcing.Analyzers`

Analyzers Roslyn:

- evento con `[EventSchema]` sin reducer para states conocidos
- dos CLR types con mismo `EventSchema(name, version)`
- evento usado en reducer sin `[EventSchema]`
- reducer registrado para un evento no registrado
- state usado en snapshots sin `[StateSchema]`
- uso de APIs peligrosas o deprecated
- comandos event-sourced sin stream resolver

### `Krackend.EventSourcing.Testing`

Helpers:

- builders de event streams
- assertions para reducers
- assertions para deciders
- in-memory stores orientados a tests
- snapshot fixtures

## Roadmap Por Fases

## Fase 1: Contrato Publico Estable

### 1.1 Quitar `LoadAsync`

`LoadAsync(streamName, streamId)` debe salir del contrato publico `IEventStore`.

Motivo:

- incentiva cargar streams completos
- puede romper aplicaciones con miles o millones de eventos
- contradice snapshots y lecturas paginadas

La alternativa oficial queda:

```csharp
ReadStreamAsync(streamName, streamId, fromVersion, maxCount)
```

La rehidratacion decide `fromVersion`:

- sin snapshot: `1`
- con snapshot: `snapshot.StreamVersion + 1`

Si se necesita una herramienta para debug o tests, debe vivir fuera del contrato principal, por ejemplo en `Krackend.EventSourcing.Testing`.

### 1.2 Mantener Registro Manual De Eventos

El registro por atributo sigue siendo el camino recomendado:

```csharp
[EventSchema("CustomerBalanceMoved", "1.1.0")]
public sealed record CustomerBalanceMoved;
```

Pero el registro manual debe quedarse:

```csharp
registry.Register<CustomerBalanceMoved>("CustomerBalanceMoved", "1.1.0");
```

Esto permite escenarios dinamicos, generacion de tipos, integraciones avanzadas y adapters futuros.

### 1.3 Reducer Faltante Debe Fallar

`IEventReducerRegistry.Apply` no debe ignorar eventos sin reducer.

Debe lanzar una excepcion clara:

```txt
No reducer registered for state 'CustomerState' and event 'CustomerBalanceMovedV1'.
```

Esto evita rehidrataciones incompletas y snapshots incorrectos.

## Fase 2: Versionado De State

### 2.1 Crear `StateSchemaAttribute`

El schema del state es independiente del schema de eventos.

```csharp
[StateSchema("CustomerState", "1.0.0")]
public sealed record CustomerState(...);
```

Motivo:

```txt
Event schema version != State schema version
```

Un evento puede no cambiar, pero el state si.

### 2.2 Persistir Metadata De State En Snapshots

La tabla de snapshots debe guardar:

- `SnapshotId`
- `StreamName`
- `StreamId`
- `StreamVersion`
- `StateType`
- `StateSchemaVersion`
- `Payload`
- `CreatedAt`

`Payload` debe ser solamente el state serializado.

Las demas columnas son metadata tecnica del snapshot.

### 2.3 Resolver State Exacto Al Leer Snapshots

Al cargar snapshot:

```txt
StateType + StateSchemaVersion -> CLR state type
```

Si no coincide con el `TState` solicitado:

- fallar con error claro, o
- usar migracion de state cuando exista.

### 2.4 Migraciones De State

Definir contrato:

```csharp
public interface IStateSnapshotMigrator
{
    string StateType { get; }
    SemanticVersion FromSchemaVersion { get; }
    SemanticVersion ToSchemaVersion { get; }
    string Migrate(string payload);
}
```

No debe ejecutarse magicamente si falta una ruta completa de migracion.

## Fase 3: Storage Adapter Pattern

### 3.1 Extraer Abstracciones

Mover contratos a `Krackend.EventSourcing.Abstractions`.

El core no debe conocer EF Core.

### 3.2 Extraer EF Core

Mover todo lo siguiente a `Krackend.EventSourcing.EntityFrameworkCore`:

- `EntityFrameworkEventStore`
- `EventStoreRecord`
- `EventSnapshotRecord`
- `SnapshotCandidateRecord`
- model builder extensions
- db context factory
- EF model customizer
- EF DI extensions

### 3.3 Mantener Integracion Con App `DbContext`

La app debe seguir pudiendo hacer:

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

Sin agregar `DbSet<EventStoreRecord>` manualmente.

### 3.4 Preparar Adapters Futuros

El diseno debe permitir:

- `Krackend.EventSourcing.Ado`
- `Krackend.EventSourcing.Mongo`
- `Krackend.EventSourcing.PostgreSql`

sin tocar el core.

## Fase 4: Event Store Robusto

### 4.1 Concurrencia

Mantener:

```csharp
ExpectedVersion.Any
ExpectedVersion.NoStream
ExpectedVersion.Exact(version)
```

Validar en todos los adapters:

- append concurrente
- stream inexistente
- stream existente
- append multi-event
- rollback transaccional

### 4.2 Multiples Stores

Soportar multiples stores logicos:

```csharp
options.Stores.Add("customers", x => x.TableName = "CustomerEvents");
options.Stores.Add("orders", x => x.TableName = "OrderEvents");
```

Validar:

- tabla default `Events`
- PascalCase naming
- schema opcional
- indices por tabla
- global position por store

### 4.3 Ranged Reads Solamente

API oficial:

```csharp
ReadStreamAsync(streamName, streamId, fromVersion, maxCount)
ReadFromAsync(streamName, afterGlobalPosition, maxCount)
GetCurrentVersionAsync(streamName, streamId)
AppendAsync(...)
```

No debe existir API publica que lea un stream completo sin limite.

## Fase 5: Snapshots

### 5.1 Snapshot Del State

Confirmar invariantes:

```txt
events -> reducers -> TState -> snapshot payload
```

No snapshot de:

- request
- event payload
- entidad EF de la app
- proyeccion
- envelope

### 5.2 Snapshot Fuera Del Request Path

La ruta caliente puede leer snapshots, pero no debe generarlos obligatoriamente.

Creacion:

- background service
- scheduled job
- maintenance worker
- rebuild controlado

### 5.3 Snapshot Candidates

Mantener candidatos:

```txt
append path marks stream candidate
worker processes candidates in batches
```

Politicas:

- nunca crear snapshots por default
- intervalo por numero de eventos
- politica custom

### 5.4 Background Worker Opcional

Agregar worker opcional:

```csharp
services.AddKrackendSnapshotWorker<CustomerState>(options =>
{
    options.Interval = TimeSpan.FromMinutes(5);
    options.BatchSize = 100;
});
```

Debe ser opcional y vivir en core o en paquete separado si requiere hosting.

## Fase 6: Projections Como Paquete Separado

### 6.1 Extraer Projections

Mover:

- `IProjectionHandler`
- `IProjectionRunner`
- `ICheckpointStore`
- checkpoint implementations

a `Krackend.EventSourcing.Projections`.

### 6.2 Projection Dispatch Exacto

Igual que rehidratacion:

```txt
EventType + EventSchemaVersion -> CLR exacto -> handler exacto
```

No usar latest automatico.

### 6.3 Checkpoints Por Proyeccion

Checkpoint key:

- projection name
- stream name/store
- global position

## Fase 7: Analyzers

### 7.1 Analyzer De Reducers Faltantes

Detectar:

- evento con `[EventSchema]`
- evento aparece en streams/reducers/projections
- no hay reducer para un state conocido

Este analyzer probablemente necesite convenciones o configuracion:

```csharp
[EventSourcedState(typeof(CustomerState))]
public sealed record CustomerCreated;
```

o configuracion por assembly.

### 7.2 Analyzer De Schemas Duplicados

Detectar dos tipos con:

```csharp
[EventSchema("CustomerRenamed", "1.0.0")]
```

en el mismo assembly/proyecto.

### 7.3 Analyzer De Evento Sin Schema

Detectar eventos usados en:

- `IEventReducer<TState, TEvent>`
- `IProjectionHandler<TEvent>`
- event factories

sin `[EventSchema]` ni registro explicito conocido.

### 7.4 Analyzer De State Sin Schema

Detectar states usados en:

- `IStateRehydrator`
- `ISnapshotProcessor<TState>`
- `IEventSourcedApplicationService<TState, TCommand>`

sin `[StateSchema]`.

## Fase 8: Extensiones De Integracion

### 8.1 Spider Extensions

Crear `Krackend.EventSourcing.SpiderExtensions`.

Objetivo:

- conectar event sourcing al pipeline de Spider
- permitir pre/post/middleware/boundaries sin acoplar el core
- mantener el core usable sin Spider instalado

Regla:

```txt
Krackend.EventSourcing.SpiderExtensions -> depende de Krackend.EventSourcing
Krackend.EventSourcing -> no depende de Spider
```

### 8.2 Pelican/Template Extensions

Crear `Krackend.EventSourcing.PelicanExtensions` o nombre equivalente cuando el contrato del template este maduro.

Objetivo:

- integrar committed events con handlers base
- usar hooks sin modificar handlers concretos
- mapear request + entity a eventos
- resolver stream desde request/entity

Regla:

```txt
Krackend.EventSourcing.PelicanExtensions -> depende de Krackend.EventSourcing
Krackend.EventSourcing -> no depende de Pelican ni templates
```

### 8.3 Otras Integraciones

Cualquier integracion con otra libreria debe seguir el mismo patron:

```txt
Krackend.EventSourcing.SomeLibraryExtensions
```

El core no debe tomar dependencias por conveniencia.

## Fase 9: Errores Y Diagnosticos

Agregar excepciones especificas:

- `EventSchemaMissingException`
- `DuplicateEventSchemaException`
- `EventTypeNotRegisteredException`
- `EventReducerNotRegisteredException`
- `StateSchemaMissingException`
- `SnapshotStateSchemaMismatchException`
- `EventPayloadDeserializationException`
- `SnapshotDeserializationException`
- `SnapshotSerializerMissingException`
- `EventStoreConcurrencyException` ya existe

Cada error debe incluir:

- stream
- event type
- event schema version
- state type
- state schema version
- sugerencia de fix cuando aplique

## Fase 10: Samples

### 10.1 Core Sample

Sin Pelican, sin EF integrado.

Debe mostrar:

- commands
- deciders
- reducers
- in-memory store
- snapshots
- eventos versionados

### 10.2 EF Sample

Debe mostrar:

- SQL Server
- multiples stores/tablas
- EF integrado al app `DbContext`
- migraciones EF reales
- snapshots con state schema

### 10.3 Pelican Sample

Debe mostrar:

- handlers con hooks
- committed events
- multiples versiones del mismo evento
- reducers separados por version
- snapshots del state, no de entidad/proyeccion

El sample puede imprimir payloads de eventos para explicar diferencias de versiones, pero no debe contaminar `CustomerState` con campos didacticos.

## Fase 11: Documentacion

Documentar:

- event sourcing vs committed event log
- event schema versioning
- state schema versioning
- snapshots
- reducers por version
- storage adapters
- extension packages
- Spider extensions
- Pelican/template extensions
- EF Core integration
- expected versions
- metadata/correlation/causation
- errores comunes
- analyzers

## Criterio Para Version Estable

La version estable debe cumplir:

- `LoadAsync` eliminado del contrato publico.
- Core sin dependencia EF Core.
- EF Core extraido a adapter.
- Projections en paquete separado.
- Spider/Pelican/templates fuera del core y en extension packages.
- `EventSchema` estable.
- `StateSchema` implementado.
- Snapshots guardan `StateType` y `StateSchemaVersion`.
- Reducer faltante falla.
- Eventos no registrados fallan.
- Schemas duplicados fallan.
- Registro manual de eventos soportado.
- Analyzers basicos disponibles.
- Tests para todos los casos criticos.
- Samples actualizados.
- Documentacion minima completa.

## Orden Recomendado De Implementacion

1. Eliminar `LoadAsync`.
2. Hacer que reducer faltante truene.
3. Agregar `StateSchemaAttribute`.
4. Agregar metadata de state schema a snapshots.
5. Crear paquete `Krackend.EventSourcing.Abstractions`.
6. Extraer EF Core a `Krackend.EventSourcing.EntityFrameworkCore`.
7. Extraer projections a `Krackend.EventSourcing.Projections`.
8. Crear extension packages para Spider/Pelican si aplica.
9. Ajustar tests y samples a paquetes nuevos.
10. Crear analyzers basicos.
11. Endurecer excepciones y diagnosticos.
12. Completar documentacion.
13. Preparar release estable.
