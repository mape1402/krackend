# Requerimiento para Mule: planificación por clave, aplazamiento cooperativo y activación sin bloqueo

**Estado:** propuesta para revisión e implementación en Mule. No describe una funcionalidad disponible actualmente ni autoriza cambios en producción.

**Consumidor inicial:** Krackend Sagas Orchestrations. La capacidad de Mule debe ser genérica y no depender de tipos, repositorios ni reglas de sagas.

**Base inspeccionada:** Mule 2.3.2. Krackend debe integrar una versión publicada que incluya el contrato acordado; no debe compilar contra APIs supuestas.

## 1. Problema y resultado requerido

Un conjunto de acciones de la instancia A puede ocupar todos los ejecutores de un lane esperando el lease de A. Una acción de la instancia B, con su propio lease disponible y su propio scope, queda esperando detrás. Los conflictos se convierten además en fallos de Mule y reintentos ordinarios de aproximadamente 30 segundos.

La reproducción en Krackend usó el executor de Mule 2.3.2 sin modificar, dos puestos de ejecución y dos mensajes competidores de A: B esperó 2026 ms; las acciones de A recibieron reintentos de aproximadamente 30 segundos. Es una reproducción con dependencias de almacenamiento controladas, no la medición del incidente de 50 mensajes que tardó 38 minutos.

Existe un segundo acoplamiento: `MuleClient` espera al commit notifier después de guardar la acción, y el notifier espera una escritura en una cola inmediata acotada. Si esa cola está llena, el productor puede conservar un lease o recurso de negocio mientras espera capacidad que necesita el propio trabajo generado.

**Resultado requerido:** los mensajes de una clave ocupada no monopolizan los ejecutores; la espera esperada se conserva como trabajo durable pendiente; y la señal de activación de una acción persistida no bloquea al productor por saturación de la cola inmediata.

La capacidad total sigue siendo acotada. No se exige paralelismo ilimitado ni ausencia de espera cuando todos los ejecutores están realizando trabajo útil de claves distintas. Tampoco se exige que Mule entienda los leases de dominio de Krackend.

## 2. Responsabilidades

| Responsabilidad | Mule | Krackend |
|---|---|---|
| Admisión a ejecutores y equidad entre claves | Implementa la capacidad genérica | Configura qué acciones comparten clave |
| Exclusión local por clave configurada | Evita operaciones activas simultáneas para esa clave | Usa la instancia como clave donde corresponda |
| Exclusión distribuida de mutaciones de una saga | No interpreta el dominio | Mantiene su lease persistido entre réplicas |
| Recurso de dominio ocupado | Permite aplazar sin fallo | Intenta adquirir el lease sin sondeo de dos segundos dentro del ejecutor |
| Callback todavía no procesable / timeout diferido | Conserva la acción aplazada | Decide cuándo corresponde aplazar y cuándo procesar |
| Persistencia, claims y recuperación de acciones | Conserva sus garantías y contratos de almacenamiento | Conserva idempotencia de transiciones y comandos |
| Dashboard y fanout opcional | Sin dependencia de la UI | Los desacopla de la ruta crítica |

## 3. P0 — Planificación por clave antes de ocupar ejecutores

### 3.1 Clave de ejecución opcional

Agregar una capacidad optativa para obtener una clave de ejecución a partir de una acción. Propuesta de superficie pública, sujeta a acuerdo antes de integrar:

```csharp
Func<DurableAction, string?>? ExecutionKeySelector
```

- Clave nula: conserva el comportamiento sin agrupación por clave.
- Clave no vacía: por defecto admite una operación activa por esa clave dentro del proceso, en el conjunto de lanes a los que se aplique la política.
- La igualdad debe estar definida y documentada; se recomienda comparación ordinal y clave con namespace del consumidor.
- La clave debe poder reconstruirse después de un reinicio a partir de datos durables y configuración estable. No puede depender exclusivamente de un diccionario temporal ni requerir resolver servicios scoped capturados por un singleton.
- Se pueden reutilizar metadatos existentes o un selector sobre `CorrelationId`; no se exige una columna nueva si esos datos bastan. Si se agrega un campo, entregar los cambios de esquema y la compatibilidad de registros anteriores.

### 3.2 Admisión y equidad

- Resolver la elegibilidad por clave **antes** de reservar un puesto de ejecución o invocar el handler.
- Si A está activa, sus siguientes acciones permanecen pendientes sin ocupar otros puestos esperando un semáforo, mailbox o lease local de A.
- Con capacidad disponible, el scheduler debe poder seleccionar B aunque haya muchas acciones de A por delante.
- La coordinación por clave debe funcionar entre lanes configurados para compartir ese espacio de claves; aislarla solo dentro de cada lane no coordina callback, dispatch y timeout de una misma instancia.
- Liberar la clave al terminar la operación, fallar, aplazar o cancelar de acuerdo con la persistencia del resultado.
- Conservar los presupuestos y prioridades de lanes sin permitir inanición indefinida de una clave o del trabajo recuperado.
- Un único lane lleno no debe detener la activación o recuperación de todos los demás por una espera global de capacidad.
- Acotar candidatos, memoria, señales y registros de claves. Eliminar claves inactivas sin carreras que permitan dos propietarios locales. No crear un thread ni un worker permanente por instancia.

La exclusión local de Mule no sustituye el lease distribuido de Krackend. Dos réplicas pueden seleccionar acciones distintas de la misma saga; el consumidor debe poder detectar esa contención y usar el aplazamiento siguiente.

## 4. P0 — Aplazamiento cooperativo como resultado de ejecución

### 4.1 Contrato público mínimo

Agregar al contexto un resultado explícito de aplazamiento, manteniendo compatibles los handlers actuales que devuelven `ValueTask`. Propuesta:

```csharp
context.DeferUntil(notBeforeUtc, reasonCode);
return;
```

El nombre final puede ajustarse, pero el contrato debe distinguir claramente:

| Resultado | Significado |
|---|---|
| Completed | La operación terminó y corresponde completar la acción |
| Deferred | La operación queda pendiente para una nueva oportunidad; no es éxito ni fallo funcional |
| Failed | Hubo un fallo y corresponde aplicar la política de errores |

No implementar `Deferred` como una excepción ordinaria ni como una finalización seguida de una acción nueva.

### 4.2 Garantías obligatorias de Deferred

1. El executor no llama a `MarkCompleted` para una acción aplazada.
2. La misma acción vuelve al estado pendiente con un `notBeforeUtc` durable.
3. Se preservan `Id`, payload, tipo, lane, metadatos, correlación, deduplicación y `CreatedOnUtc`. No reemplazar el mensaje por otro para simular el aplazamiento.
4. No incrementa el contador de intentos de fallo funcional ni agota `MaxAttempts`. Si se cuenta cada invocación física del handler, exponerla por separado.
5. No genera un log de error ni un stack trace por contención esperada. Debe ser observable mediante métricas y eventos de diagnóstico acotados.
6. Libera el puesto de ejecución, la clave local y el scope al persistir el aplazamiento; no mantiene un handler dormido hasta `notBeforeUtc`.
7. Sobrevive a reinicio y cambio de réplica. La elegibilidad futura no depende de un `Task.Delay` en memoria.
8. Permite volver a intentarlo sin la espera ordinaria de fallo de 30 segundos. La política de aplazamiento debe ser independiente de la política de errores, configurable y evitar busy loops; valores pasados o inválidos deben normalizarse o rechazarse de forma documentada.
9. La resolución normal de una clave local ocupada debe aprovechar su liberación, sin depender solo de polling agresivo por mensaje.
10. El aplazamiento no permite una mutación tardía del claim anterior sobre el resultado de un propietario nuevo.

Si el handler solicita aplazamiento y después lanza una excepción, no ocultar el fallo: definir la precedencia y comprobarla. Propuesta: el aplazamiento solo se aplica cuando el handler retorna normalmente; una excepción sigue el camino de fallo o cancelación correspondiente.

### 4.3 Almacenamiento y ownership

- Proporcionar una operación explícita de deferral o una capacidad adicional del storage. Reutilizar `MarkFailed` incrementando `Attempts` no satisface el contrato.
- La transición desde el claim actual a pendiente debe ser atómica y verificar su token de concurrencia/propiedad; un ejecutor vencido no puede reprogramar o completar trabajo ya reclamado por otro.
- No mantener una transacción de base de datos abierta durante toda la ejecución del handler.
- Si un candidato se reclama pero no puede entrar a ejecución, no dejarlo acumulado en una cola local hasta que venza su lock. Evitar reclamarlo prematuramente o devolverlo de forma segura al estado pendiente.
- Si falla la persistencia del aplazamiento, conservar la recuperabilidad del claim y reportar un fallo real de infraestructura. No declarar completada la acción ni descartar su payload.
- Implementar el contrato en los adapters usados por esta integración: EF Core e interfaz durable, Redis FastLane y el almacenamiento en memoria usado en pruebas. Documentar capacidades equivalentes de otros providers y detectar de forma explícita una combinación no soportada.

### 4.4 FastLane Redis

- Mantener coherentes el estado del envelope, sus índices de pendientes, su claim de ejecución y la proyección al storage durable.
- No dejar que un flush atrasado de un estado anterior sobrescriba un claim o aplazamiento más reciente.
- La lectura de estado pendiente usada por consumidores y diagnósticos debe ser compatible con la transición a Deferred.
- Un proceso que cae después de persistir el aplazamiento y antes de emitir una señal no puede perder el trabajo.

## 5. P0 — Activación inmediata sin bloquear al productor después de persistir

`EnqueueAsync` debe seguir esperando la confirmación de persistencia que exige el adapter. Después de esa confirmación, la saturación de una cola local de activación no debe retener al productor.

- Usar señales acotadas, coalescencia o un intento de notificación sin espera de capacidad en la ruta del productor.
- Si no cabe una señal, el registro durable sigue siendo fuente de verdad y debe existir una recuperación garantizada que encuentre esa acción.
- No sustituir la cola acotada por memoria ilimitada, un `Task.Run` por mensaje o un número creciente de tareas bloqueadas en `WriteAsync`.
- Mantener las reglas de commit: una señal asociada a una transacción ambiental no puede publicar trabajo antes del commit; el rollback no debe activar una acción que no quedó confirmada.
- Documentar qué confirma cada adapter y cómo se combina la activación con su persistencia; no prometer una transacción entre Redis y SQL que el adapter no proporciona.
- Resolver el fallback tanto con recuperación por polling como con recuperación programada. Una configuración sin mecanismo de descubrimiento de señales omitidas debe rechazarse o disponer de otro mecanismo durable equivalente.
- La recuperación no debe quedar bloqueada por un lane saturado mientras otros tienen trabajo ejecutable.
- Preservar el comportamiento de acciones programadas: una notificación temprana no debe ejecutarlas antes de su fecha.

## 6. Métricas y diagnóstico requeridos

Separar como mínimo:

| Señal | Propósito |
|---|---|
| Acciones listas, ejecutando y aplazadas | Distinguir trabajo ejecutable de espera esperada |
| Tiempo de espera en cola y tiempo de handler | Localizar dónde se pierde throughput |
| Aplazamientos por motivo y lane | Observar contención sin error storm |
| Intentos funcionales fallidos | Conservar el significado de MaxAttempts |
| Activaciones omitidas/coalescidas y recuperadas | Verificar el fallback de notificaciones |
| Saturación de dispatch y ejecución | Distinguir backpressure de trabajo bloqueado |

Incluir `ActionId`, clave de ejecución, lane, instante de próximo intento y claim/token en logs estructurados de diagnóstico cuando corresponda. No usar IDs de acción, instancia o claves de ejecución como etiquetas de métricas de alta cardinalidad.

## 7. Compatibilidad y alcance

- La agrupación por clave es optativa y conserva a los consumidores existentes que no la configuran.
- Los handlers actuales no requieren cambiar de firma para completar o fallar como hasta ahora.
- Las excepciones reales siguen aplicando su política de fallos; el aplazamiento no la reemplaza.
- Mantener cancelación, replay, deduplicación, acciones futuras y shutdown con presupuesto acotado.
- Garantía de entrega de al menos una vez; no anunciar exactly-once por introducir exclusión local o deferral. La idempotencia de efectos de negocio sigue siendo responsabilidad del consumidor.
- Entregar notas de actualización, capacidades necesarias de storage y migraciones si realmente se requieren. No asumir compatibilidad de un provider que aún no implemente la transición nueva.

No se solicita que Mule implemente un core de sagas, invoque servicios de Krackend o decida qué callback es válido. Tampoco se propone resolver el problema aumentando workers como único cambio.

## 8. Pruebas de aceptación bloqueantes

### A. Equidad entre claves

Con dos ejecutores, mantener un handler de A activo en una barrera controlada. Encolar más trabajo de A y después B. B debe entrar y terminar **antes de liberar A**. Una cola de A no puede ocupar el segundo ejecutor esperando su turno. Usar barreras/TCS y un timeout de seguridad; ese timeout no es el objetivo de latencia de producción.

Repetir con A y B en lanes diferentes y con operaciones de una misma clave que atraviesan lanes configurados para compartir la exclusión.

### B. Lease externo ocupado y aplazamiento

Un handler simula un recurso externo ocupado, solicita `Deferred` y retorna. Mientras A sigue ocupada externamente, B debe procesarse. Verificar que A conserva el mismo ID/payload/deduplicación/fecha de creación, no queda completada y no consume intentos funcionales.

### C. Cola inmediata llena

Llenar una cola de activación de capacidad uno. Persistir otra acción manteniendo un recurso del productor. El encolado debe retornar sin esperar a drenar esa cola y el productor debe poder liberar el recurso. Omitir la señal, comprobar recuperación automática y ejecutar la acción una vez que sea elegible.

### D. Recuperación y claims

Probar al menos estas ventanas:

- Caída antes de persistir el aplazamiento.
- Caída después de persistirlo y antes de notificar.
- Caída después de tomar un claim y antes de entrar al handler.
- Caída durante un flush de FastLane.
- Dos réplicas reclamando o recuperando trabajo de una misma clave.
- Resultado tardío de un claim vencido frente a un claim nuevo.

No debe haber trabajo perdido, finalizaciones falsas, reservas irrecuperables ni sobrescritura por un propietario anterior. Las duplicaciones permitidas por at-least-once deben conservar la identidad para que el consumidor aplique idempotencia.

### E. Errores, cancelación y compatibilidad

- Error funcional después de varios aplazamientos: aplicar el presupuesto de fallos reales acordado, sin agotarlo por deferrals previos.
- Handler antiguo: mismo resultado que antes, sin opt-in.
- Solicitud de Deferred seguida de excepción: no esconder la excepción.
- Shutdown: no declarar éxito por cancelación ni quedar esperando colas indefinidamente.
- Replay, cancelación y deadlines de acciones futuras: conservar sus reglas.

### F. Carga y recursos

Ejecutar cargas de 1, 2, 3, 10, 20 y 50 claves independientes, además de una clave caliente con muchas acciones y otras claves con poco trabajo.

Reportar duración total, throughput, latencia de cola p50/p95/máxima, tiempo de handler, aplazamientos, fallos funcionales, CPU y memoria. Declarar configuración, providers y recursos del proceso. Los mensajes sanos sin contención no deben introducir pausas artificiales del retry ordinario de 30 segundos.

Ejecutar las garantías de almacenamiento con EF y Redis reales además de pruebas unitarias. Una simulación en memoria no valida persistencia, claims o recuperación de esos providers.

## 9. Entrega esperada del trabajo de Mule

1. Contrato público final de clave de ejecución y deferral, con ejemplo compilable.
2. Implementación de scheduler/executor y notificación sin bloqueo posterior a persistencia.
3. Implementación y capacidades de storage para los providers cubiertos.
4. Pruebas de aceptación y resultados de carga, con versión y configuración exactas.
5. Paquetes compatibles de Mule, EF e integración FastLane publicados para que Krackend pueda restaurarlos.
6. Notas de actualización y migraciones, si las hay.

Como riesgos adicionales ya identificados, registrar aparte la finalización de lotes con registros huérfanos y las reservas de flush sin vencimiento en Redis. No se afirma que expliquen el incidente de 38 minutos. Si cualquiera impide superar las pruebas de recuperación anteriores, debe resolverse antes de dar por aceptada esa garantía.

## 10. Trabajo de Krackend que puede adelantarse mientras se implementa Mule

| Trabajo | Puede adelantarse | Dependencia para terminar |
|---|---|---|
| Desacoplar fanout opcional del dashboard | Sí, independientemente de Mule | Verificar que no prolonga las mutaciones ni crece sin límite |
| Diseñar el resultado interno de coordinación y sus motivos | Sí, sin exponer tipos de Mule en el core | Conectar el resultado al contrato de Deferred publicado |
| Preparar el intento de lease sin espera dentro de acciones durables | Sí como cambio aislado y probado | No activarlo enviando la contención al retry ordinario de Mule 2.3.2 |
| Definir y probar el selector de clave por instancia | Sí | Conectarlo al contrato público final del scheduler |
| Mejorar diagnóstico de lease y tiempos de etapas | Sí | Ninguna para la instrumentación propia de Krackend |
| Preparar pruebas de 1/2/3/10/20/50 instancias y hot-key | Sí | Ejecutar la validación final contra el paquete nuevo y el host real |
| Revisar recepción Pigeon y resolución selectiva de ingress | Sí | Confirmar opciones efectivas del host y mantener rollout/rollback correctos |
| Integrar Deferred y retirar esperas de ocupación de ejecutores | Preparar el punto de integración | Requiere contrato y paquete de Mule |
| Afirmar corregido el incidente completo | No | Requiere ambas integraciones y validación con la composición desplegada |

Para el selector de Krackend, callback, dispatch y timeout deben usar el mismo namespace de instancia cuando su payload identifica esa instancia. Un trigger que todavía no tiene instancia usa identidad de inicio/idempotencia; no agrupar sagas distintas solo por compartir una correlación de negocio. La política de clave debe aplicarse a las operaciones que requieren exclusión de estado, sin impedir que los servicios remotos de tareas paralelas trabajen simultáneamente.

Este apartado describe trabajo paralelizable; no significa que ya esté implementado o autorizado por separado. No sustituir permanentemente el scheduler con una segunda implementación en Krackend mientras se espera el contrato de Mule.
