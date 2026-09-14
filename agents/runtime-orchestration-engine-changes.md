# Runtime Orchestration Engine Changes

## Alcance

Se implemento el camino minimo hasta fase 6 del roadmap:

- Resolver artifacts runtime.
- Promover desde el `WorkItem` aceptado por Mule.
- Convertir decisions a datos.
- Ejecutar decisions con handlers.
- Despachar tasks messaging.
- Completar `FireAndForget`.
- Soportar callback correlation para `FireAndWaitCallback`.

No se hizo commit.

## Cambios En Runtime Core

### Referencia a Abstractions

Archivo:

- `src/Krackend.Sagas.Orchestrations.Runtime/Krackend.Sagas.Orchestrations.Runtime.csproj`

Cambio:

- Se agrego referencia a `Krackend.Sagas.Orchestrations.Abstractions`.

Justificacion:

- El motor runtime ahora interpreta `OrchestrationArtifact` y persiste modelos como `OrchestrationInstance`, `StageExecution`, `TaskExecution`, `TaskDispatch` y `ExecutionTransition`.
- Esos contratos viven en Abstractions, asi que Runtime necesita esa referencia para ejecutar artifacts generados por Design.

### Artifact Resolution

Archivos:

- `Engine/Artifacts/IRuntimeArtifactResolver.cs`
- `Engine/Artifacts/DefaultRuntimeArtifactResolver.cs`
- `Engine/Artifacts/IRuntimeArtifactSerializer.cs`
- `Engine/Artifacts/DefaultRuntimeArtifactSerializer.cs`
- `Engine/Artifacts/ResolvedOrchestrationArtifact.cs`
- `Engine/Artifacts/IResolvedOrchestrationArtifactAccessor.cs`
- `Engine/Artifacts/DefaultResolvedOrchestrationArtifactAccessor.cs`

Cambio:

- Se agrego resolucion de `RuntimeOrchestrationArtifact` por id.
- Se valida que el artifact este activo.
- Se deserializa el `ArtifactPayload` hacia `OrchestrationArtifact`.

Justificacion:

- Para correr una orquestacion real, Runtime debe interpretar el artifact generado por Design, no operar con configuracion hardcodeada.

### Promotion Desde Mule

Archivos:

- `Engine/Promotion/PromotionRequest.cs`
- `Engine/Promotion/PromotionResult.cs`
- `Engine/Promotion/Promoter.cs`

Cambio:

- `Promoter` ahora crea una `OrchestrationInstance` desde el `WorkItem` que Mule ya acepto.
- No se crea otro intake.
- Se registra una transition `InstancePromoted`.
- `PromotionResult` devuelve `SagaId` e `InstanceId`.

Justificacion:

- Mule es el intake real. Promotion solo convierte trabajo aceptado en una instancia de orquestacion persistida.

### Metadata De Instancia

Archivo:

- `Metadata/InstanceMetadata.cs`

Cambio:

- Se agregaron `OrchestrationInstanceId`, `TaskExecutionId`, `DispatchId` y `Attempt`.

Justificacion:

- El callback necesita correlacionar la respuesta con instancia, task, dispatch y attempt.
- Pigeon ya propaga `InstanceMetadata` via interceptors, por eso se aprovecha el mecanismo existente.

### Decisions Como Datos

Archivos:

- `Engine/Control/IDecision.cs`
- `Engine/Control/IDecisionExecutor.cs`
- `Engine/Control/IDecisionHandler.cs`
- `Engine/Control/DecisionExecutor.cs`
- `Engine/Control/Decisions/*.cs`

Cambio:

- `IDecision` ya no expone `HandsOn()`.
- Las decisions son records/datos.
- Se agrego executor para resolver handlers por tipo.

Justificacion:

- DecisionControl debe decidir, no construir efectos.
- Los handlers concentran efectos, persistencia y dispatch.
- Esto deja el motor mas testeable y preparado para retry, timeout, compensation y branches.

### Decision Control

Archivo:

- `Engine/Control/DecisionControl.cs`

Cambio:

- Se implemento decision control minimo:
  - Completa callback si la task esta esperando respuesta.
  - Arranca la siguiente stage.
  - Despacha la siguiente task habilitada.
  - Completa stage cuando no hay mas tasks.
  - Completa instancia cuando no hay mas stages.

Justificacion:

- Este es el slice minimo para correr una orquestacion secuencial real con messaging.
- Conditions, transformations, branches y parallel groups quedan fuera porque pertenecen a fases posteriores.

### Decision Handlers

Archivos:

- `Engine/Control/Handlers/StartStageDecisionHandler.cs`
- `Engine/Control/Handlers/DispatchTaskDecisionHandler.cs`
- `Engine/Control/Handlers/CompleteStageDecisionHandler.cs`
- `Engine/Control/Handlers/CompleteInstanceDecisionHandler.cs`
- `Engine/Control/Handlers/CompleteCallbackDecisionHandler.cs`

Cambio:

- Se agregaron handlers para mutar estado runtime y ejecutar efectos.
- `DispatchTaskDecisionHandler` crea `TaskExecution`, `TaskExecutionAttempt` y `TaskDispatch`.
- Para `FireAndForget`, marca task/attempt/dispatch como completados despues de publicar.
- Para `FireAndWaitCallback`, deja task/attempt/dispatch esperando respuesta.
- `CompleteCallbackDecisionHandler` completa task/attempt/dispatch al recibir backchannel.

Justificacion:

- Las acciones del motor necesitan persistir cada paso y dejar una linea de tiempo auditable.

### Saga Engine

Archivo:

- `Engine/SagaEngine.cs`

Cambio:

- El engine ahora ejecuta ciclos de decisions hasta que no haya mas trabajo inmediato.
- `StartOrchestrationAsync` actualiza metadata con `SagaId` e `InstanceId` despues de promotion.
- El engine usa `IDecisionExecutor`.

Justificacion:

- Una sola decision no basta para correr una orquestacion simple. El flujo necesita promover, iniciar stage, despachar task, completar stage y completar instance.

### Messaging Command Serializer

Archivos:

- `Engine/Dispatching/Messaging/IMessagingCommandSerializer.cs`
- `Engine/Dispatching/Messaging/DefaultMessagingCommandSerializer.cs`

Cambio:

- Se agrego `Serialize`.

Justificacion:

- El handler de dispatch construye el settings payload desde `MessagingTaskConfigurationArtifact` y el executor existente lo deserializa antes de publicar.

### Storage Default In-Memory

Archivos:

- `Storage/InMemory/InMemoryRuntimeStore.cs`
- `Storage/InMemory/InMemoryRuntimeStorageUnitOfWork.cs`
- `Storage/InMemory/InMemoryRuntimeArtifactRepository.cs`
- `Storage/InMemory/InMemoryOrchestrationInstanceRepository.cs`
- `Storage/InMemory/InMemoryStageExecutionRepository.cs`
- `Storage/InMemory/InMemoryTaskExecutionRepository.cs`
- `Storage/InMemory/InMemoryTaskExecutionAttemptRepository.cs`
- `Storage/InMemory/InMemoryTaskDispatchRepository.cs`
- `Storage/InMemory/InMemoryExecutionTransitionRepository.cs`

Cambio:

- Se agregaron implementaciones default in-memory de storage runtime necesarias para el motor.

Justificacion:

- Los contratos de storage runtime existian, pero no habia provider concreto en Runtime core.
- Estos defaults permiten que el runtime arranque y que un provider SQL futuro pueda reemplazarlos via DI.

### Dependency Injection

Archivo:

- `DependencyInjection/ServiceCollectionExtensions.cs`

Cambio:

- Se registraron artifact resolver/serializer/accessor.
- Se registraron decision executor y handlers.
- Se registraron repos in-memory default.
- Se mantuvieron `TryAdd*` para permitir reemplazos por host/adapters.

Justificacion:

- El host debe poder levantar el runtime sin registrar cada componente manualmente.
- Los providers reales pueden reemplazar defaults.

## Cambios En Adapters Runtime

No se cambio Mule ni Pigeon directamente en esta tanda.

Los adapters existentes se benefician de:

- `InstanceMetadata` ampliada para correlation.
- `PigeonDispatchAdapter` ya registrado previamente.
- `Pigeon` continua propagando metadata por interceptors.

## Validacion

Comandos ejecutados:

- `dotnet build src/Krackend.Sagas.Orchestrations.Runtime/Krackend.Sagas.Orchestrations.Runtime.csproj --no-restore`
- `dotnet build samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Krackend.Sagas.Orchestrations.RuntimeHost.Sample.csproj --no-restore`
- `dotnet build Krackend.sln --no-restore`

Resultado:

- Build exitoso.
- Quedaron 4 warnings existentes por `SQLitePCLRaw.lib.e_sqlite3` en samples.

## Limites Conscientes

- No se implementaron conditions.
- No se implementaron transformations.
- No se creo otro intake; Mule sigue siendo el intake real.

## Cambios Posteriores Al Tag `runtime-orchestration-phase6`

### Runtime Storage SQL Server

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/**`
- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Migrations/RuntimeStorage/**`
- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Program.cs`

Cambio:

- Se agrego provider SQL Server para runtime storage.
- Se agrego `RuntimeStorageDbContext`.
- Se agregaron repositorios EF para artifacts, instances, stages, tasks, attempts, dispatches, transitions, variables y compensations.
- Se conecto el sample host al provider SQL runtime.
- Se agrego migracion inicial y ejecucion de migrations al arrancar.

Justificacion:

- El motor ya no debe depender de storage in-memory para correr una orquestacion real.
- Mule sigue siendo intake; runtime storage persiste la instancia y su ejecucion.

### Parallel Groups

Archivo:

- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/DecisionControl.cs`

Cambio:

- El decision control detecta tasks de un mismo parallel group.
- Respeta `MaxParallelAgents`.
- Mantiene semantica `WaitAll`: no avanza mientras haya tasks corriendo o esperando callback.
- El selector de siguiente task solo considera tasks no iniciadas, evitando re-dispatch de pasos ya completados, fallidos o saltados.

Justificacion:

- El artifact ya modela parallel groups; el runtime debe respetar el limite de concurrencia definido por Design.
- Una task terminal representa avance dentro de la instancia; volverla a despachar duplicaria efectos externos.

### Retry De Dispatch

Archivo:

- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/Handlers/DispatchTaskDecisionHandler.cs`

Cambio:

- El dispatch intenta publicar hasta `RetryPolicy.MaxRetries + 1`.
- Registra transition `TaskDispatchRetrying`.
- Si agota reintentos, marca dispatch, attempt y task como failed.

Justificacion:

- El primer punto real de fallo es el dispatch remoto. El motor debe persistir el fallo sin tirar el proceso completo.

### Compensation Inversa

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/Decisions/CompensateInstanceDecision.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/Handlers/CompensateInstanceDecisionHandler.cs`

Cambio:

- Si una task falla con `OnErrorPolicy.StopAndCompensate`, el runtime crea compensation executions.
- Ejecuta compensaciones de tasks completadas en orden inverso.
- Por ahora soporta compensation messaging.

Justificacion:

- La semantica de saga necesita compensar efectos ya confirmados cuando una task posterior falla.

### Pendientes Explícitos

- Conditions esperan definicion de DSL.
- Transformations esperan definicion de DSL.
- Branch rules condicionados quedan esperando evaluator de conditions.
- `FireAndWait` queda reservado para HTTP.
- Timeout scheduler operativo queda pendiente; el timeout se conserva en metadata de task para que el scheduler lo procese despues.

## Cambios Posteriores: Ingress Configurations Desde Artifact

### Persistencia De Ingress Configurations

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime/Ingress/RuntimeIngressConfiguration.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Ingress/IRuntimeIngressConfigurationRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Storage/InMemory/InMemoryRuntimeIngressConfigurationRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/RuntimeIngressConfigurationRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Infrastructure/RuntimeStorageDbContext.cs`
- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Migrations/RuntimeStorage/**`

Cambio:

- Se agrego una entidad persistida para configuraciones de ingress runtime.
- La entidad conserva `SettingsPayload` como JSON dinamico del transporte; no guarda `Topic` ni `Version` como columnas.
- SQL agrega indices para lectura rapida de configuraciones activas en standup y busqueda por artifact.

Justificacion:

- El standup debe leer configuraciones listas, sin transformar el artifact en cada arranque.
- El runtime debe seguir abierto a otros transportes futuros como HTTP.

### Proyeccion Al Publicar Artifact

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime/Ingress/IRuntimeIngressConfigurationProjector.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Ingress/DefaultRuntimeIngressConfigurationProjector.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Storage/InMemory/InMemoryRuntimeArtifactRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/RuntimeArtifactRepository.cs`

Cambio:

- Al hacer `Upsert` de `RuntimeOrchestrationArtifact`, se proyectan configuraciones de ingress.
- Por cada trigger binding event/messaging habilitado se crea una configuracion `Trigger`.
- Por cada artifact/version se crea una configuracion `Backchannel` con version del artifact y topic calculado por `IBackchannelTopicFormatter`.
- Al desactivar artifacts previos, tambien se desactivan sus ingress configurations.
- La key interna de configuracion la genera `IRuntimeIngressConfigurationKeyBuilder`.
- El `SettingsPayload` messaging lo genera `IMessagingConfigurationSerializer`, no JSON armado manualmente en el proyector.

Justificacion:

- La configuracion nace del artifact publicado y queda materializada una sola vez para runtime.
- La convención default del backchannel sigue siendo `orchestrations.{artifact.Key}`, pero queda encapsulada y reemplazable.

## Runtime host happy path seed

- Se agregó una semilla idempotente en el sample runtime host para registrar el artifact `sales.sale.created` version `1.0.0`.
- La semilla busca primero por `EnvironmentKey`, `OrchestrationDefinitionKey` y `Version`; si ya existe, reutiliza el `Id` del artifact y actualiza el payload, evitando duplicados en `RuntimeOrchestrationArtifacts`.
- El artifact contiene el trigger explicito `events.sales.sale.created` y dos tareas messaging con `FireAndWaitCallback`: `tasks.inventories.reserve.requested` y `tasks.payments.capture.requested`.
- No se guarda manualmente el backchannel en el seeder. Se conserva el camino correcto del runtime: `IRuntimeArtifactRepository.Upsert` proyecta las configuraciones de ingress, incluyendo el backchannel implicito `orchestrations.sales.sale.created`.
- Se cambio `IngressRegistryBackgroundService` de `BackgroundService` a `IHostedService`. Justificacion: el standup de ingress debe terminar durante `StartAsync` para que los consumers dinamicos queden registrados antes de que Pigeon inicie consumo; cuando corria como `BackgroundService`, Pigeon podia empezar a consumir antes de tener el handler dinamico en su configuracion.
- Se agrego un converter JSON para `SemanticVersion`. Justificacion: al serializar/deserializar el artifact, `System.Text.Json` no podia reconstruir correctamente el value object y el trigger messaging se proyectaba como version `0.0.0`; con el converter, las versiones viajan como string semver (`1.0.0`) y se conservan entre artifact, runtime e ingress.

### Standup Sin Transformar Artifacts

Archivo:

- `src/Krackend.Sagas.Orchestrations.Runtime/Ingress/DefaultIngressConfigurationAccessor.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Ingress/RuntimeIngressConfigurationAccessor.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/ServiceCollectionExtensions.cs`

Cambio:

- El accessor deja de regresar configuracion hardcodeada.
- Ahora lee configuraciones activas desde repositorio en paginas de 500 registros.
- El adapter SQL reemplaza los accessors de lectura y consulta directamente `RuntimeStorageDbContext.RuntimeIngressConfigurations` con `AsNoTracking`.

Justificacion:

- El arranque debe ser rapido y directo: leer rows activas, conectar ingress, avanzar.
- Cuando el host usa SQL, el standup no debe pasar por defaults in-memory ni por transformacion de artifacts.

### Backchannel En Metadata

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime/Metadata/InstanceMetadata.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/Handlers/DispatchTaskDecisionHandler.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/Handlers/CompensateInstanceDecisionHandler.cs`

Cambio:

- `InstanceMetadata` ahora incluye `BackchannelTopic` y `BackchannelVersion`.
- Antes de publicar una task messaging, runtime resuelve el backchannel del artifact y lo agrega a la metadata propagada por Pigeon.
- Compensation tambien setea metadata antes de publicar.

Justificacion:

- Los servicios orquestados necesitan saber a que topic/version responder cuando terminen su operacion.

### Fix De Tracking EF En Runtime SQL

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/RuntimeRepositoryBase.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/OrchestrationInstanceRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/StageExecutionRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/TaskExecutionRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/TaskExecutionAttemptRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/TaskDispatchRepository.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer/Repositories/CompensationExecutionRepository.cs`

Cambio:

- Antes de actualizar entidades runtime, el repositorio SQL desprende cualquier instancia local ya trackeada con la misma primary key.

Justificacion:

- En el e2e, Mule ejecuta varias decisions dentro del mismo scope/runtime storage context.
- Algunas entidades se crean y quedan trackeadas; despues el motor las vuelve a leer con `AsNoTracking` y manda actualizar otra instancia con la misma llave.
- EF rechazaba ese update con `The instance of entity type ... cannot be tracked because another instance with the same key value is already being tracked`, bloqueando el avance de la orquestacion antes del dispatch de tasks.

### Publish Directo Con Payload JSON

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon/PigeonDispatchAdapter.cs`

Cambio:

- El runtime adapter publica el `Payload` de `MessagingCommand` como JSON parseado, no como string crudo.
- El publish se mantiene directo al queue/topic default de Pigeon, sin exchange ni routing key personalizados.

Justificacion:

- El motor serializa el command como envelope interno, pero el payload que viaja al servicio debe conservar forma de objeto JSON para que Pigeon lo deserialice al contrato del consumer.
- La prueba debe usar la topologia default de Pigeon y publicar directo al queue.

## Cambios Posteriores: Orchestration Client Y Backchannel

### Contratos Compartidos Cliente/Runtime

Archivos:

- `src/Krackend.Sagas.Orchestrations.Abstractions/Runtime/Metadata/InstanceMetadata.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/Runtime/Metadata/IInstanceMetadataAccessor.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/Runtime/Metadata/IInstanceMetadataSetter.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/Runtime/Metadata/OrchestrationMetadataConstants.cs`

Cambio:

- Se movio `InstanceMetadata` y sus interfaces a Abstractions.
- Se movio la key de metadata de Pigeon a Abstractions.

Justificacion:

- Runtime y client necesitan compartir exactamente los mismos contratos para que Pigeon pueda transportar metadata sin acoplar servicios orquestados al proyecto Runtime completo.

### Runtime Usando Metadata Compartida

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime/Metadata/DefaultInstanceMetadataAccessor.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/**/*.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon/Interceptors/KrackendPublishInterceptor.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon/Interceptors/KrackendConsumeInterceptor.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon/PigeonIngressAdapter.cs`

Cambio:

- Runtime conserva su implementacion scoped default de metadata, pero ahora implementa las interfaces compartidas.
- El adapter Pigeon de Runtime usa `OrchestrationMetadataConstants.InstanceMetadataKey`.
- Se elimino la constante local duplicada de Pigeon.

Justificacion:

- Evita duplicidad de tipos/strings entre runtime y cliente.
- Mantiene el comportamiento actual de propagacion de metadata por Pigeon.

### Semantica De Callback

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/DecisionControl.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/Decisions/CompleteCallbackDecision.cs`
- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/Handlers/CompleteCallbackDecisionHandler.cs`

Cambio:

- `DecisionControl` identifica callbacks usando `InstanceMetadata`.
- `CompleteCallbackDecisionHandler` marca task/attempt/dispatch como completed cuando llega el callback.
- El payload de negocio llega intacto y se guarda en `TaskExecutionAttempt.ResponsePayload`.
- Se registra la transition `TaskCallbackCompleted`.

Justificacion:

- El orquestador debe cerrar el task usando metadata de orquestacion, sin envolver ni modificar el payload de negocio.

### Libreria Cliente

Archivos:

- `src/Krackend.Sagas.Orchestrations.Client/**`
- `src/Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon/**`
- `Krackend.sln`

Cambio:

- Se creo la libreria cliente core.
- Se agregaron servicios default de DI, metadata scoped, response factory, publisher abstraction y extensiones `UseOrchestration` para Spider.
- Se agrego adapter Pigeon para publicar respuestas/triggers con `IProducer`.
- Se agrego consume interceptor del client para leer `InstanceMetadata` desde Pigeon.
- Se agregaron los proyectos a la solucion.

Justificacion:

- Los servicios generados por TurtlePath pueden enganchar orquestacion desde Spider sin meter logica de Krackend en controllers, consumers o handlers.

### Payload De Negocio Entre Tasks

Archivos:

- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Control/DecisionControl.cs`

Cambio:

- Se retiro el merge automatico entre snapshot de instancia y payload de callback.
- El runtime conserva el payload de negocio tal como viene en el flujo; la informacion de correlacion y control debe viajar por `InstanceMetadata` o metadata de Pigeon.

Justificacion:

- El runtime no debe modificar el contrato de negocio para transportar estado interno de orquestacion. La metadata existe precisamente para separar control/correlacion del payload funcional.
