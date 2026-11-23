# Diagnóstico de concurrencia — 18 de noviembre de 2026

## Cuello de botella entre instancias: reproducción adicional del pipeline

El usuario precisa que una ejecución individual funciona bien, pero dos o tres concurrentes se degradan, y que 50 mensajes tardaron 38 minutos en drenarse. Esto exige investigar el procesamiento agregado; no atribuirlo de entrada a una saga de negocio defectuosa ni limitar las pruebas a varios competidores de una sola instancia.

Se creó `benchmarks/Krackend.Concurrency.Diagnostics/Pipeline`, que ejecuta el código original de Mule 2.3.2 para el executor, su cola acotada de notificaciones y el commit notifier, junto con el coordinador actual de Krackend enlazado desde `src`. Almacenamiento, tipos de mensajes, registry y métricas son dependencias controladas; no se usó infraestructura real.

### Resultado medido entre dos instancias diferentes

Con un lane de dos puestos de ejecución:

1. Se retuvo el lease de la instancia A y se pusieron dos mensajes de A a procesar.
2. Ambos mensajes ocuparon los dos ejecutores de Mule mientras esperaban ese lease.
3. Se comprobó directamente que el lease de la instancia B estaba libre.
4. Se encoló un mensaje de B. B no pudo empezar hasta que los competidores de A agotaron su espera.
5. B esperó **2026 ms** en esa ejecución. Los dos mensajes de A fallaron y Mule les programó reintentos de aproximadamente **30 segundos**. Se verificaron tres scopes de acción distintos.

Esto reproduce un efecto entre sagas distintas: una instancia con mensajes competidores puede monopolizar un lane y retrasar otra cuyo estado y lease están disponibles. No requiere compartir DbContext. Los slots de ejecución siguen ocupados durante el `await` del handler, aunque el hilo físico se libere. La configuración de dos puestos es una elección controlada para revelar el mecanismo, no una medición de la imagen desplegada.

### Resultado medido en el encolado

Se llenó una cola acotada de notificaciones inmediatas y se llamó al `AmbientTransactionMuleCommitNotifier.NotifySavedAsync` original dentro de un lease. La llamada quedó esperando capacidad en `ChannelMuleDispatchQueue.EnqueueAsync`, y otro scope no pudo adquirir ese lease. Al drenar la cola, la operación terminó y liberó el lease.

El `MuleClient` llama a ese notifier después de guardar la acción. Por tanto, persistir el trabajo no garantiza que el productor pueda continuar: todavía puede quedar esperando una posición en la cola inmediata. En Krackend, ese productor puede estar dentro de una mutación de saga. La combinación crea una dependencia circular de espera: el productor retiene el lease mientras intenta encolar; los ejecutores consumen capacidad esperando ese lease; otros mensajes esperan detrás. La espera de adquisición puede romper temporalmente la dependencia mediante un fallo, pero introduce después el retraso de reintento. No se afirma un deadlock permanente ni que se haya medido la saturación de la cola desplegada.

### Qué se verificó sobre scopes y colas

| Capa | Implementación revisada | Implicación |
|---|---|---|
| Runtime EF | `AddDbContext<RuntimeDbContext>` y repositorios scoped | La biblioteca no registra un único DbContext global. |
| Pigeon dispatcher 4.2.0 | Crea un scope por despacho | Los mensajes no comparten obligatoriamente el mismo contexto scoped. |
| Pigeon manager 4.2.0 | Una cola compartida si se configura `MaxConcurrency` o `QueueCapacity`; workers esperan el despacho completo | Triggers y callbacks pueden competir por el mismo presupuesto de recepción. |
| Service Bus provider 4.2.0 | Solo asigna `MaxConcurrentCalls` cuando se configura `MaxConcurrency` | Si queda nulo, se conserva el valor del SDK: una llamada simultánea por suscripción. No se ha confirmado la opción efectiva de la imagen. |
| Mule executor 2.3.2 | Colas y workers por lane, compartidos por sus instancias | Hay múltiples lanes, pero la espera dentro de un handler ocupa un puesto y retrasa otros mensajes del lane. |
| Mule notifier 2.3.2 | Espera `WriteAsync` sobre cola acotada | Puede mantener al productor y su lease esperando capacidad aun después de guardar la acción. |
| Ingress de Krackend | Relee todas las configuraciones activas por mensaje antes de seleccionar destinos | Añade consultas y materialización en la recepción; su coste debe medirse y evitarse con resolución selectiva compatible con cambios de configuración. |

Las opciones por defecto del SDK se inspeccionaron localmente: `MaxConcurrentCalls=1`, `PrefetchCount=0`. No se dedujo de ello que la imagen desplegada use esos valores. También se comprobó que el `Mule.dll` de los binarios de pruebas locales informa versión 2.2.0, mientras el código actual fija 2.3.2; por eso esos binarios no se tomaron como una validación de la dependencia actual. La reproducción compila snapshots del tag 2.3.2 y las clases actuales del coordinador.

### Cambio de prioridad de la reparación

El primer objetivo es eliminar el acoplamiento entre espera de lease, presupuesto compartido de ejecutores y encolado inmediato. Una instancia no debe ocupar todos los ejecutores con mensajes que todavía no puede procesar. La notificación de trabajo ya persistido no debe bloquear una mutación de saga esperando una cola que depende de esa misma ejecución. Deben conservarse la durabilidad, deduplicación, orden de las mutaciones y recuperación distribuida.

Un mailbox local por instancia por sí solo no basta si el llamador sigue ocupando un puesto de Mule esperando ese mailbox. La reprogramación o selección de trabajo debe liberar de verdad la capacidad compartida. También hay que revisar los límites efectivos de recepción y de cada lane, separar el coste de aceptar mensajes del de ejecutar decisiones y medir la espera en cola, el tiempo de lease, la publicación al broker y el trabajo de almacenamiento.

Los nuevos casos terminaron correctamente verificando ambos mecanismos defectuosos. No reproducen ni explican cuantitativamente los **38 minutos**: esa validación exige cargas de 1, 2, 3, 10 y 50 mensajes con la composición y opciones efectivas del host. La afirmación sustentada es que el propio pipeline puede crear bloqueo entre instancias y amplificación de reintentos, incluso con scopes separados y un lease independiente disponible.

Fuentes revisadas: `MuleActionExecutor.cs:145` y `:189`, `ChannelMuleDispatchQueue.cs:23`, `AmbientTransactionMuleCommitNotifier.cs:45`, `MuleClient.cs:54` del tag [Mule v2.3.2](https://github.com/mape1402/mule/tree/v2.3.2); `ConsumingManager.cs`, `ConsumingDispatcher.cs` y `ServiceBusProvider.cs` del tag [Pigeon v4.2.0](https://github.com/mape1402/pigeon-messaging/tree/v4.2.0).

## Evidencia adicional del incidente reportado

Se incorporó el informe del usuario `Runtime orchestration concurrency failure — technical incident report`, recibido después del análisis inicial. Es evidencia aportada del entorno desplegado; no se ejecutaron esas observaciones de AKS desde esta sesión.

El informe identifica la imagen `dmx-orchestrator-runtime:32773`, un solo pod activo y una ventana de 15 minutos con estos resultados:

| Señal del informe | Resultado | Consecuencia para este diagnóstico |
|---|---:|---|
| Conflictos de lease | 129 | Priorizar qué operación retiene cada instancia y cómo se reprograman sus competidores. |
| Callback con estado todavía no preparado | 4 | Hay una brecha entre la respuesta externa y el estado persistido que permite procesarla. |
| Timeout aplazado por respuesta recibida a tiempo | 15 | Hay callbacks pendientes protegidos frente al timeout; eso no prueba que estén avanzando. |
| Errores de flush de FastLane | 0 | El defecto del lote terminal sigue siendo real, pero no está evidenciado como causa de esta captura. |
| Rechazos de admisión / Recovering | 0 | El cierre de admisión no es la explicación observada. |
| Reinicios del pod actual | 0 | No atribuir este episodio a un crash loop u OOM del pod actual. Un pod reemplazado es distinto de reiniciar el mismo contenedor. |

### Explicación ahora prioritaria

La evidencia encaja con sagas que se atascan individualmente en su secuencia dispatch → callback → timeout, mientras otras completan normalmente. No hace falta un candado global entre distintas instancias para producir esa selectividad. El informe tampoco demuestra que dos mutaciones de la misma instancia hayan obtenido simultáneamente el lease: los rechazos pueden indicar que la exclusión está funcionando, pero los competidores no reciben una reprogramación apropiada ni hay progreso suficiente del propietario.

El código permite reconstruir esta secuencia de fallo:

1. La respuesta vuelve del servicio externo, pero la tarea y el intento siguen en `Running` y el dispatch en `Scheduled` o `Enqueued`.
2. `DecisionControl.TryBuildCallbackDecision`, después de verificar las identidades del intento, exige el estado `WaitingResponse` y lanza exactamente la excepción registrada en el informe (`DecisionControl.cs:583`).
3. El callback durable queda sujeto al reintento de Mule. Si el timeout identifica una respuesta aceptada antes de su deadline, la protege y aplaza su propia ejecución. El inspector consulta la existencia y el estado pendiente del callback; el mensaje de timeout no demuestra que un worker esté procesando activamente esa respuesta.
4. Dispatch, callback y timeout de esa misma instancia siguen compitiendo por su lease. Los rechazos y reintentos pueden mantenerla sin avance mientras otras instancias recorren el camino normal.

Esta es la cadena causal sustentada por los síntomas y el código. Aún falta determinar qué retiene el lease durante las repeticiones y por qué no se completa la transición de dispatch en las instancias afectadas. Un envío publicado antes de una caída y todavía no confirmado en el estado durable es una ventana posible, no un hecho demostrado por el informe.

### Brecha concreta en una prueba existente

`MessagingDecisionE2ETests.EngineRejectsCallbackAsRetryableWhenDispatchStateIsNotReadyYet` (`MessagingDecisionE2ETests.cs:767`) espera esa excepción. Después cambia directamente los repositorios de tarea, intento y dispatch a `WaitingResponse`, y vuelve a invocar el callback. Comprueba que una segunda ejecución funciona después de preparar el estado; no prueba que el runtime recupere por sí mismo esa transición, ni que Mule reprograme el callback con latencia aceptable durante un reemplazo de pod. Esto explica por qué esa prueba puede pasar sin cubrir el incidente reportado.

### Arranque y readiness

La biblioteca inicializa su estado operacional como `Healthy` antes de evaluar dependencias. Las comprobaciones de dependencias no equivalen a comprobar que todas las transiciones de cada instancia sean recuperables. La cronología del informe confirma consumo antes del HTTP 200 de readiness, pero esa cronología sola no demuestra un defecto del endpoint: readiness de Kubernetes no detiene por sí misma consumidores de Service Bus ni workers de Mule.

Hay que verificar la inicialización mínima antes de habilitar ejecución durable y, por separado, la recuperación por instancia. Un gate global que espere a que termine todo el backlog puede bloquear sagas sanas y no resuelve callbacks recibidos después del arranque mientras un dispatch sigue incompleto.

### Propiedad del lease y recursos

Con los valores por defecto del código revisado, un lease dura 30 segundos y se renueva cada 15 segundos. Una sola reserva abandonada al iniciar el pod no explica por sí sola conflictos más de tres minutos después, si los relojes están alineados y esa configuración es efectiva. Hay que comprobar `ActiveLeaseId`, `ActiveLeaseExpiresOnUtc`, reintentos, acciones activas y configuración de la imagen. Además, el ID actual se genera como `runtime:<id>`: no contiene la réplica ni el tipo de acción, por lo que no permite atribuir directamente el propietario a un pod y una operación sin correlación adicional.

El informe observa 230 MiB de 256 MiB y un límite de CPU de 250m. Es un margen de memoria pequeño, pero no demuestra OOM ni saturación de CPU. No se identifica falta de recursos como causa confirmada ni se propone aumentar la concurrencia como arreglo de la secuencia de estados.

### Próximo dato que permite cerrar la causa específica

Correlacionar, sin modificar datos, las instancias `01M7RC6WN0Y1C610220HZH38P9`, `01M7RCPR0P76KT81RFJYVBEGKR`, `01M7RCNN98TJC2TD46RD47WV17` y `01M7RT8ZX6THNS9537KV2M78H3` con sus leases, tareas, intentos, dispatches, últimas transiciones y acciones Mule pendientes/bloqueadas/fallidas. Confirmar también las versiones y opciones efectivas de la imagen 32773. El informe propone precisamente esa lectura; no se ha realizado desde esta sesión y no se requiere borrar registros para diagnosticarlo.

## Resultado

Hay defectos de coordinación y recuperación que amplifican esperas y errores. Se reprodujeron cuatro comportamientos defectuosos con código de producción y dependencias controladas; el control de veinte instancias independientes pasó. Esto identifica mecanismos reales, pero no demuestra por sí solo cuál ocurrió en el pod reportado.

No se modificó código de producción. Los archivos nuevos son este informe y el ejecutable de diagnóstico en `benchmarks/Krackend.Concurrency.Diagnostics`.

## Base revisada y estado de Git

- `main` remoto verificado mediante GitHub: `43b099ec1ccf10e6632f06f3200a37bc79306b95`, versión 6.5.0.
- Rama remota de diagnóstico verificada en ese mismo SHA: [`fix/concurrency-root-cause-20261118`](https://github.com/mape1402/Krackend/tree/fix/concurrency-root-cause-20261118).
- El checkout local permanece en `codex/work-20261118`, commit `af0b5047`. Su árbol `76580bb6eeefebb6f087015611e903f84a3dbbab` coincide con el contenido remoto: se compararon los SHA de todos los archivos y subárboles de primer nivel, incluidos `src`, `tests`, `samples` y `docs`.
- No se pudo completar `git fetch` ni cambiar la rama local: `.git/FETCH_HEAD` rechazó escritura. La referencia local `origin/main` está desactualizada en `9c60caad`; no se la trató como el main actual después de verificar GitHub.
- La rama remota contiene la base de main. Este informe y las reproducciones quedan como archivos locales nuevos; no se hizo commit ni push de ellos.

## Hallazgos prioritarios

### 1. Un registro terminal huérfano bloquea un lote de finalizaciones de Mule

**Confirmado con reproducción aislada. Impacto alto; ocurrencia en producción pendiente de logs.**

Krackend fija Mule 2.3.2. En esa versión, `FastLaneRedisFlushService.FlushTerminalsAsync` toma un lote, actualiza sus registros durables y solo después confirma el lote en Redis. Si falta en SQL uno de los IDs, `EntityFrameworkMuleStorage.FindAsync` lanza una excepción. El `catch` reencola **todos** los IDs del lote, incluidos los sanos. No hay separación del registro defectuoso ni cuarentena.

La reproducción usa el servicio de flush original, con una acción sana y otra ausente en el almacenamiento controlado: tres ejecuciones producen tres excepciones, tres reencolados y **cero finalizaciones sanas confirmadas**. No es una prueba con Redis/SQL reales.

El ciclo exterior registra `Mule FastLane Redis flush failed.` y vuelve a intentar. El host SQL de ejemplo configura `FlushInterval = 25 ms`, `IntentFlushSize = 1000` y `CompletionFlushSize = 2000`. Con un fallo inmediato, la configuración permite un orden de hasta 40 ciclos fallidos por segundo, menos el tiempo de trabajo; no es una tasa medida en producción. Además, la finalización de una acción puede quedar pendiente de persistir aun si su trabajo de negocio ya terminó.

Fuentes de la dependencia fijada:

- [Flush del lote, líneas 86–157](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule.FastLane.Redis/FastLaneRedisFlushService.cs#L86-L157).
- [Persistencia de completados, líneas 214–237](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule.EntityFrameworkCore/EntityFrameworkMuleStorage.cs#L214-L237).
- [Registro faltante, líneas 404–406](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule.EntityFrameworkCore/EntityFrameworkMuleStorage.cs#L404-L406).

El propio changelog 6.5.0 reconoce que esta corrección de Mule no está incluida. Actualizar a 6.5.0 no la resuelve.

**Corrección necesaria:** aislar el registro inconsistente sin descartar trabajo recuperable, permitir progreso del resto del lote y reintentar con espera acotada. También hay que resolver la causa de la divergencia entre Redis y SQL y conservar evidencia del registro apartado.

### 2. La sección protegida por el lease incluye red y notificaciones opcionales

**Confirmado en código; bloqueo por fanout lento reproducido.**

`RemoteCommandDispatchAction.ExecuteAsync` adquiere el lease de la instancia alrededor de todo `ExecuteCoreAsync`, incluyendo `executor.ExecuteAsync`, que en mensajería espera la publicación al broker. No se limita a las mutaciones persistentes. Al mismo tiempo, `IntakeBufferMule` necesita el **mismo lease** para aceptar una respuesta y el motor vuelve a adquirirlo para procesarla. Publicaciones, callbacks y tareas hermanas compiten por la misma exclusión.

También se espera trabajo del dashboard dentro del recorrido protegido. `RuntimeStorageUnitOfWork.CommitAsync` confirma SQL y luego espera `PublishPendingEvents`. Ese método publica secuencialmente con `CancellationToken.None`. `SignalRRuntimeReactiveEventPublisher.Publish` espera a su vez el fanout remoto. El fanout Redis puede esperar hasta un segundo por publicación. Aunque es opcional y sus errores se capturan, su latencia sigue bloqueando la ejecución que sostiene el lease.

La reproducción enlaza el coordinador y publisher originales: tres publicaciones con un fanout controlado de 1.1 segundos mantienen ocupado el lease durante **3346 ms**, y el competidor agota su espera de dos segundos. El fanout concreto de Redis usa un límite de un segundo; la prueba usa una implementación controlada del contrato para exhibir el acoplamiento, no para medir Redis.

Referencias locales:

- `src/Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule/RemoteCommandDispatchAction.cs:91` y `:126`.
- `src/Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule/IntakeBufferMule.cs:85`.
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework/Repositories/RuntimeStorageUnitOfWork.cs:57`, `:69`, `:89`.
- `src/Krackend.Sagas.Orchestrations.Runtime.WebUI/Reactive/SignalRRuntimeReactiveEventPublisher.cs:36`.
- `src/Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis/RedisRuntimeReactiveFanout.cs:106`.

**Corrección necesaria:** sacar el fanout opcional de la ruta crítica mediante una cola acotada. Acortar las secciones de mutación alrededor de las publicaciones de comandos preservando intentos, confirmaciones, deduplicación y tratamiento de entregas inciertas; quitar el lease sin rediseñar esa transición introduciría carreras.

### 3. Contención normal se convierte en consultas repetidas, errores y reintentos de 30 segundos

**Amplificación reproducida; política de reintento confirmada en la dependencia.**

El coordinador usa por defecto una espera de 2000 ms con intentos cada 25 ms, sin coordinación local de competidores ni jitter. Cada intento fallido en el repositorio EF hace un `ExecuteUpdate` y una consulta para distinguir una instancia ausente o un lease ya obtenido. Mantener ocupados varios slots de ejecución esperando la misma instancia consume capacidad sin avanzar trabajo útil.

Al agotarse la espera, los consumidores de dispatch y motor lanzan `OrchestrationInstanceLeaseUnavailableException`. Mule maneja esa condición como cualquier otro fallo de la acción: registra error y aplica la política de reintento. Los lanes principales de Krackend no la distinguen de una excepción funcional; en Mule 2.3.2 el valor por defecto es **30 segundos de retraso y 10 intentos**. La contención de milisegundos o segundos puede por tanto transformarse en pausas de decenas de segundos y repetirse entre etapas. Estos son valores por defecto; no se leyó la configuración efectiva del pod.

Reproducción: veinte competidores por **una instancia ya ocupada** generaron **1261 llamadas de adquisición en 2006 ms**, con veinte rechazos. En SQL eso equivale a dos operaciones por intento fallido en el camino actual; el diagnóstico no ejecutó esas operaciones SQL. El control separado de veinte IDs independientes completó los veinte con veinte adquisiciones. La prueba no afirma que veinte orquestaciones independientes fallen automáticamente.

Referencias:

- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Coordination/OrchestrationCoordinationOptions.cs`.
- `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Coordination/DefaultOrchestrationInstanceCoordinator.cs:84`.
- `src/Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework/Repositories/OrchestrationInstanceRepository.cs:84`.
- [Mule executor, cálculo de reintento y log, líneas 194–205](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule/Dispatching/MuleActionExecutor.cs#L194-L205).
- [Valores por defecto de Mule](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule/MuleSettings.cs).

**Corrección necesaria:** tratar la contención como reprogramación corta y acotada que libere el slot, no como error de negocio. Evitar multiplicar sondeos locales por instancia, mantener exclusión distribuida y preservar la redelivery de respuestas.

### 4. Perder el lease todavía puede reportarse como éxito

**Confirmado con reproducción aislada. Defecto de corrección, además de rendimiento.**

El renovador cancela un `CancellationTokenSource` cuando pierde el lease. Sin embargo, `TryExecuteAsync` espera a que el delegado termine y devuelve `true` sin comprobar que la cancelación represente pérdida de propiedad. Si el delegado ignora la cancelación o finaliza un trabajo no cancelable y retorna normalmente, el coordinador declara éxito.

La reproducción fuerza la pérdida en la renovación de un lease de un segundo y hace terminar normalmente al delegado: el token está cancelado y el resultado es **`true`**. No se simula una escritura real posterior de otro propietario; esa corrupción no se afirma como observada.

Además, el `finally` espera al renovador y libera el lease con `CancellationToken.None`. La espera de adquisición de dos segundos tampoco limita por sí misma la duración de una consulta individual a SQL. No hay un presupuesto total propio para esos I/O; dependen de límites del proveedor.

Referencia: `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Coordination/DefaultOrchestrationInstanceCoordinator.cs:64`, `:74`, `:80`, `:143`.

**Corrección necesaria:** representar explícitamente pérdida de propiedad, impedir confirmar éxito después de ella y acotar renovación/liberación. Para garantizar que un propietario viejo no escriba, revisar también el fencing de las mutaciones persistentes; comprobar el token al final no basta.

### 5. La recuperación del buffer tras un cierre abrupto tiene marcas sin vencimiento

**Confirmado por inspección de scripts; no reproducido contra Redis en esta sesión.**

En Mule 2.3.2, `TakeIntentFlushScript` y `TakeTerminalFlushScript` marcan `intentFlushing = true` o `terminalFlushing = true`. Solo los caminos de finalización o cancelación limpian las marcas. Las condiciones de toma excluyen una marca ya activa; no se registra propietario ni vencimiento de esa reserva de flush. Si el proceso termina abruptamente entre la toma y su finalización, un `catch` no se ejecuta y la marca puede dejar el registro fuera de futuras tomas.

Eso puede impedir que progresen persistencia o finalizaciones después de reinicios. La `LeaseDuration` de dos minutos de FastLane se usa para ejecutar acciones, no para vencer esas dos marcas de flush.

Separadamente, Mule usa por defecto `LockTimeout = 5 minutos`; el host de ejemplo no lo reduce. Los registros durables de acciones FastLane se materializan bloqueados. Si una acción queda bloqueada por la caída de un ejecutor, la recuperación puede esperar al vencimiento correspondiente. Es un mecanismo concreto de espera de minutos, no prueba de que explique los diez minutos del caso reportado.

Fuentes:

- [Scripts de toma de flush, líneas 30–84](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule.FastLane.Redis/RedisFastLaneBuffer.cs#L30-L84).
- [Limpieza de marcas, líneas 227–335](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule.FastLane.Redis/RedisFastLaneBuffer.cs#L227-L335).
- [Materialización durable, líneas 700–714](https://github.com/mape1402/mule/blob/v2.3.2/src/Mule.FastLane.Redis/RedisFastLaneBuffer.cs#L700-L714).

**Corrección necesaria:** reservas de flush con propietario, vencimiento y recuperación segura entre réplicas; una réplica no debe simplemente borrar la reserva activa de otra. Alinear los límites de recuperación con las garantías de ejecución y medirlos mediante terminación abrupta.

### Interacción adicional: los timeouts pueden quedar aplazados por una respuesta sin progreso

`MuleOrchestrationDurableWorkInspector.InspectPendingResponseAsync` consulta la deduplicación y el estado en SQL. Si encuentra un ID de respuesta cuya fila SQL falta, el lector devuelve `Unknown`; el inspector lo convierte en `Present` si la fecha del ID es anterior al deadline. No hay una comprobación de progreso ni un límite temporal en esa rama. Una fila SQL `Pending` o `Locked` también se considera presente sin verificar si su ejecutor sigue vivo.

`DefaultOrchestrationTimeoutProcessor` aplaza el timeout cuando recibe `Present`, y `TaskResponseTimeoutAction` lanza una excepción de aplazamiento para conservar la acción mediante reintentos. Proteger una respuesta recibida a tiempo es necesario; el defecto es que esa protección depende de que su recuperación tenga progreso garantizado. Una referencia de deduplicación y las fallas de flush anteriores pueden mantener esa condición aun cuando el trabajo esté atascado. Esto se confirmó por inspección, sin reproducir un callback perdido contra Redis/SQL.

Referencias: `src/Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule/MuleOrchestrationDurableWorkInspector.cs:23`, `src/Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework/Repositories/OrchestrationDurableActionStateReader.cs:14`, `src/Krackend.Sagas.Orchestrations.Runtime/Engine/Timeouts/DefaultOrchestrationTimeoutProcessor.cs:275` y `src/Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule/TaskResponseTimeoutAction.cs:26`.

La corrección debe recuperar el callback o llevar la inconsistencia a un estado diagnosticable. No debe declarar un timeout falso ni confiar indefinidamente en la existencia de la clave de deduplicación.

## Brecha de las pruebas anteriores

El harness de infraestructura real fuerza `KrackendMuleRuntimeProfile.HighThroughput` (`RealMessagingRuntimeHarness.cs:159`), mientras el runtime por defecto usa `Balanced`. Las pruebas de carga esperan hasta 120 segundos para observar completados o avances de etapa. Además, ese harness no registra `AddOrchestratorRuntimeWebUI`, por lo que no recorre el publisher SignalR que participa en el problema del fanout.

Estas pruebas pueden demostrar finalización eventual bajo otro perfil, pero no garantizan la latencia exigida con la composición del host desplegado. Se necesita el mismo proveedor de broker, perfil, réplicas, límites de CPU/memoria y UI que producción, con presupuesto explícito de latencia y verificación de errores/reintentos, además del estado final.

## Validación realizada y límites

El ejecutable de diagnóstico terminó con código cero verificando este resultado:

| Caso | Resultado observado |
|---|---|
| 20 IDs independientes | 20 completados; 20 adquisiciones |
| 20 competidores de un ID ocupado | 20 rechazos; 1261 adquisiciones; 2006 ms |
| Fanout opcional lento | lease ocupado 3346 ms; competidor rechazado |
| Renovación rechazada | token cancelado; coordinador devuelve éxito |
| Lote con terminal huérfano | 3 fallos; 3 reencolados; 0 completados sanos confirmados |

Las dependencias de almacenamiento y red del diagnóstico son dobles controlados. Estos valores no son métricas de producción. La copia de `FastLaneRedisFlushService.cs` pertenece exactamente al tag Mule v2.3.2; las clases Krackend se enlazan desde `src` sin modificar.

Se intentó ejecutar pruebas existentes con `dotnet test --no-build --no-restore --framework net10.0`, pero VSTest no logró conectarse a testhost en 90 segundos y abortó. No se contabilizó ese intento como pruebas aprobadas o defectos del runtime. Docker no tiene un daemon disponible y `kubectl config current-context` no tiene contexto configurado.

Por eso no se atribuye el reinicio del pod a OOM, probe fallido o excepción fatal: falta el motivo de terminación, `exitCode`, eventos y logs previos del contenedor, junto con la imagen y configuración realmente desplegadas. Tampoco se asegura que el pod ejecute 6.5.0 solo porque main ya la contiene.

## Qué ya corrigió 6.5.0 y no se cuenta como hallazgo pendiente

La referencia local vieja de main contiene renovación sobre el mismo DbContext, herencia de leases entre scopes y cierre de admisión durante reconciliación periódica. El main remoto actual ya incluye correcciones para esos tres puntos y la supresión del ExecutionContext en los avisos programados. Esas fallas históricas no sustituyen el diagnóstico de los problemas que permanecen.

## Orden de reparación

1. Corregir el lote terminal defectuoso y la recuperación de reservas de flush en Mule; agregar pruebas con almacenamiento real y caída abrupta.
2. Desacoplar el fanout opcional del trabajo protegido de la saga y reducir el tiempo de publicación de comandos dentro del lease mediante una transición durable bien definida.
3. Separar contención de fallos funcionales en el scheduler y eliminar la amplificación local de sondeos.
4. Corregir éxito tras pérdida de propiedad y acotar los I/O del coordinador.
5. Ejecutar cargas de 10, 20 y superiores con la configuración efectiva del host y un objetivo de latencia definido, incluyendo callbacks simultáneos, publicación lenta, fallos de Redis/SQL, múltiples réplicas y reinicio del ejecutor.

No se considera resuelto el incidente hasta completar esa validación y correlacionar los reinicios con evidencia del contenedor.
## Comparación con la referencia de Errs

Se inspeccionaron `C:\repos\packages\errs-package-rabbit` y `C:\repos\errs-orchestrator`. El [análisis comparativo](concurrency-comparison-errs.md) documenta sus rutas de consumo, scopes, resolución en memoria y el inbox/outbox opcional de Mule 2.3.2. Esa referencia precisa la atribución: el bloqueo reproducido aparece por la interacción del lease/encolado de Krackend con los puestos de Mule, no por usar Mule de forma aislada. La prioridad sigue siendo eliminar la espera dentro del ejecutor; las mejoras de consultas/rutas reducen coste adicional.

