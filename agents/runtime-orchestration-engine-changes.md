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
