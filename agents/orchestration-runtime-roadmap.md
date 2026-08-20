# Krackend Orchestration Runtime Roadmap

## Objetivo

Construir el motor de orquestacion real usando el camino actual:

`Ingress -> IntakeBuffer/Mule -> SagaEngine -> DecisionControl -> Decision handlers -> Remote dispatch -> Pigeon/HTTP/etc.`

El objetivo no es agregar mas adapters primero. El objetivo es que Runtime pueda interpretar un `OrchestrationArtifact`, crear una instancia persistente, ejecutar tasks, esperar callbacks cuando aplique, avanzar entre stages y dejar trazabilidad completa.

## Principios

- Design produce la receta estatica: triggers, variables, stages, tasks, branch rules, parallel groups, retry, timeout, compensation y dispatch type.
- Runtime ejecuta la receta y guarda estado dinamico: instances, stage executions, task executions, attempts, dispatches, variables, transitions y compensations.
- Las decisions deben ser datos sobre lo que debe pasar.
- Las task actions o handlers deben ejecutar los efectos.
- Mule y Pigeon son infraestructura. El motor no debe depender directamente de ellos.
- Mule es el intake real. El motor promueve a instancia desde el `WorkItem` que Mule ya acepto.
- Cada paso debe ser idempotente, especialmente promotion, dispatch remoto y callbacks.

## Arquitectura Deseada

### 1. Artifact Resolution

Crear un servicio que resuelva artifacts runtime activos.

Responsabilidades:

- Buscar `RuntimeOrchestrationArtifact` por `ArtifactId`.
- Validar que este activo.
- Deserializar `ArtifactPayload` a `OrchestrationArtifact`.
- Exponer una vista util para el engine.

Propuesta:

```csharp
public interface IRuntimeArtifactResolver
{
    Task<ResolvedOrchestrationArtifact> ResolveAsync(string artifactId, CancellationToken cancellationToken);
}
```

### 2. Promotion

Convertir un trigger entrante en una instancia real de orquestacion.

Responsabilidades:

- Consumir el `WorkItem` ya aceptado por Mule.
- Aplicar idempotencia usando artifact, connector, correlation y payload hash del intake existente.
- Crear `OrchestrationInstance`.
- Crear variables iniciales.
- Crear la primera `StageExecution` candidata.
- Registrar `ExecutionTransition`.

Salida esperada:

```csharp
public sealed record PromotionResult(
    bool Success,
    string SagaId,
    string InstanceId,
    string? Reason);
```

### 3. Decision Model

Reemplazar el modelo donde `IDecision.HandsOn()` fabrica acciones por un modelo de decisions como datos.

Ejemplos:

```csharp
public interface IDecision
{
    string Kind { get; }
}

public sealed record DispatchTaskDecision(
    string InstanceId,
    string StageExecutionId,
    string TaskExecutionId,
    RemoteCommandTransport Transport,
    string SettingsPayload,
    string Payload,
    TaskDispatchType DispatchType) : IDecision
{
    public string Kind => "dispatch-task";
}

public sealed record WaitForCallbackDecision(
    string InstanceId,
    string TaskDispatchId) : IDecision
{
    public string Kind => "wait-for-callback";
}

public sealed record CompleteTaskDecision(
    string InstanceId,
    string TaskExecutionId) : IDecision
{
    public string Kind => "complete-task";
}

public sealed record CompleteInstanceDecision(
    string InstanceId) : IDecision
{
    public string Kind => "complete-instance";
}
```

### 4. Decision Execution

Agregar handlers por tipo de decision.

Propuesta:

```csharp
public interface IDecisionHandler<TDecision>
    where TDecision : IDecision
{
    Task HandleAsync(TDecision decision, CancellationToken cancellationToken);
}
```

Responsabilidades del executor:

- Resolver el handler correcto.
- Ejecutar decisions en orden.
- Registrar errores y transitions.
- Mantener idempotencia por decision.

### 5. Decision Control

`DecisionControl` debe interpretar artifact + estado persistido.

Primera version:

- Tomar la primera stage habilitada por `Order`.
- Tomar la primera task habilitada por `Order`.
- Soportar tasks messaging.
- Soportar `FireAndForget`.
- Soportar `FireAndWaitCallback` dejando dispatch pendiente.

Versiones siguientes:

- Execution conditions.
- Transformations.
- Branch rules.
- Parallel groups.
- Retry policies.
- Timeout policies.
- Compensation.

## Roadmap Por Fases

### Fase 0 - Cierre Del Camino Actual

Estado esperado:

- DI completa del engine.
- Defaults funcionales para messaging dispatch.
- Pigeon conectado para ingress y dispatch.
- Forward/Retry sin perdida de payload, settings ni transport.

Resultado:

- El tubo puede correr sin romper por DI.
- El runtime puede recibir un trigger y llegar al engine.

### Fase 1 - Artifact Resolver

Implementar:

- `IRuntimeArtifactResolver`.
- `ResolvedOrchestrationArtifact`.
- Serializer/deserializer de `OrchestrationArtifact`.
- Default resolver que use storage runtime.
- Default fallback que falle con mensaje claro si no hay storage configurado.

Criterio de salida:

- Dado un `ArtifactId`, el runtime puede cargar y deserializar el artifact activo.

### Fase 2 - Promotion Real

Implementar:

- `Promoter` real.
- Promotion desde el `WorkItem` recibido por Mule.
- Creacion de `OrchestrationInstance`.
- Creacion de variables iniciales.
- Creacion de stage inicial.
- Idempotencia basica para no duplicar instancias por reentregas.

Criterio de salida:

- Un mensaje entrante crea una instancia persistida.

### Fase 3 - Decision Model

Implementar:

- Decisions como records.
- `IDecisionExecutor`.
- Handlers por decision.
- Remover `HandsOn()` de `IDecision`.

Criterio de salida:

- `DecisionControl` solo decide.
- Los handlers ejecutan efectos.

### Fase 4 - Dispatch Messaging Minimo

Implementar:

- Interpretar `MessagingTaskConfigurationArtifact`.
- Crear `TaskExecution`.
- Crear `TaskExecutionAttempt`.
- Crear `TaskDispatch`.
- Construir `RemoteCommand`.
- Publicar por `IMessagingDispatchAdapter`.

Criterio de salida:

- Una task messaging se publica en Rabbit via Pigeon.

### Fase 5 - Fire And Forget

Implementar:

- Marcar dispatch completado tras publish exitoso.
- Marcar task completada.
- Avanzar a la siguiente task.
- Completar stage cuando no haya mas tasks.
- Completar instancia cuando no haya mas stages.

Criterio de salida:

- Una orquestacion simple de una o mas tasks `FireAndForget` corre de inicio a fin.

### Fase 6 - Fire And Wait Callback

Implementar:

- Incluir metadata de correlation en el mensaje saliente.
- Guardar `TaskDispatch` pendiente.
- Backchannel debe resolver instance/task/dispatch por metadata.
- Marcar attempt/task segun respuesta.
- Reinvocar `DecisionControl`.

Criterio de salida:

- Una task puede publicar, esperar callback y continuar la orquestacion.

### Fase 7 - Conditions And Transformations

Implementar:

- Evaluador de `ExecutionConditionArtifact`.
- Evaluador de `TransformationArtifact`.
- Primer engine DSL minimo.
- Manejo de fallos de evaluacion.

Criterio de salida:

- Stage/task puede omitirse o ejecutarse segun condition.
- Payload puede transformarse antes del dispatch.

### Fase 8 - Branch Rules

Implementar:

- Evaluar `BranchRuleArtifact`.
- Navegar a stage/task destino.
- Registrar transition.
- Detectar branch sin destino valido.

Criterio de salida:

- La orquestacion puede tomar caminos alternos segun estado/payload.

### Fase 9 - Parallel Groups

Implementar:

- Agrupar tasks por `ParallelGroupId`.
- Respetar `JoinPolicy`.
- Interpretar `MaxParallelAgents` como limite de concurrencia por grupo cuando aplique.
- Persistir estado de cada task independiente.

Criterio de salida:

- Un stage puede ejecutar varias tasks en paralelo y continuar segun politica de join.

### Fase 10 - Retry, Timeout And Compensation

Implementar:

- Retry policy por task.
- Timeout policy por task.
- Reconcile/wait/fail timeout behaviors.
- Compensation task execution.
- Registro de attempts y errores.

Criterio de salida:

- Una task fallida puede reintentarse, expirar, fallar la instancia o detonar compensacion.

### Fase 11 - Observabilidad Operativa

Implementar:

- Execution transitions completas.
- Logs estructurados con instance/task/dispatch ids.
- Query APIs para runtime state.
- UI runtime basica para ver instances, stages, tasks y dispatches.

Criterio de salida:

- Se puede entender que paso con una orquestacion sin mirar logs crudos.

## Slice Minimo Recomendado

El primer corte funcional debe ser:

1. Recibir trigger por Pigeon.
2. Guardarlo en Mule.
3. Promover a instancia.
4. Cargar artifact.
5. Ejecutar primera stage.
6. Ejecutar primera task messaging.
7. Publicar por Pigeon.
8. Marcar completo si es `FireAndForget`.
9. Cerrar instancia si no hay mas work.

No incluir todavia:

- Branches.
- Parallel groups.
- Retry avanzado.
- Timeout.
- Compensation.
- DSL complejo.

## Riesgos Principales

- Duplicar instancias por reentrega de mensajes.
- Publicar dos veces una misma task si no hay idempotencia en dispatch.
- Perder callbacks si no se manda metadata suficiente.
- Mezclar decisiones con efectos y volver dificil testear el motor.
- Implementar parallel/retry/compensation antes de tener persistencia basica estable.

## Orden De Implementacion Inmediato

1. Crear artifact resolver.
2. Implementar promoter persistente.
3. Convertir decisions a records.
4. Agregar decision executor y handlers.
5. Implementar dispatch messaging desde artifact.
6. Completar `FireAndForget`.
7. Agregar callback correlation.

## Definicion De "Orquestacion Real"

Una orquestacion es real cuando:

- Parte de un `OrchestrationArtifact` generado por Design.
- Crea una `OrchestrationInstance` persistida.
- Crea stage/task executions persistidas.
- Despacha al menos una task usando su configuration real.
- Registra dispatch/attempt/transition.
- Puede continuar o terminar desde estado persistido.
