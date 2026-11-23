# Plan de corrección: independencia entre sagas y contención dentro de un solo host

**Fecha:** 19 de noviembre de 2026.

**Estado:** diagnóstico y plan de la siguiente corrección. Las correcciones previas de fanout y resultado tras pérdida de lease permanecen implementadas; el bloqueo principal sigue abierto. Este documento no declara implementadas las fases propuestas.

## Condición principal de aceptación

Si la saga A está esperando, la saga B debe poder avanzar con capacidad disponible. A no debe monopolizar los ejecutores por esperar su propio lease, una respuesta o espacio en una cola de activación. La protección de estado debe pertenecer a la instancia modificada y durar únicamente la mutación necesaria.

La referencia es la ruta de Errs Orchestrator: destino resuelto desde un registro preparado, scope por mensaje y procesamiento de un paso. El trabajo pendiente sigue durable entre mensajes. Se aprovecharán las estructuras existentes de Krackend; no se añadirá otro scheduler permanente ni una cola global que serialice todas las instancias.

## Qué significa el error recibido

- Acción: `01M7S762JY2377GDD8X7AD66CD`.
- Instancia de saga: `01M7S7627CDZMM1MK8AS43QF40`.
- Origen de la excepción: `RemoteCommandDispatchAction.ExecuteAsync`, después de que `TryExecuteAsync` devuelve false.
- La frase `another runtime node` está escrita literalmente en `OrchestrationInstanceLeaseUnavailableException`. La excepción solo recibe el ID de la saga; no recibe ni identifica otro pod, proceso o propietario.
- El coordinador genera un token `runtime:{Id.New()}` por adquisición. Dos coordinadores de scopes distintos del mismo proceso usan tokens distintos y compiten por el mismo lease persistido. La reentrada solo es válida para el coordinador dueño del mismo scope.
- Por tanto, el error significa que ese intento no obtuvo el lease en su ventana de adquisición. No demuestra la existencia de otro nodo ni identifica al propietario que provocó la contención.

La base SQL protege la fila por ID de instancia y token de lease, sin exigir que el dueño pertenezca a otro proceso. Las actualizaciones normales de instancia ya excluyen los campos de lease: no se propone arreglar un supuesto sobrescrito de esos campos que el código inspeccionado evita.

### Cómo puede ocurrir con un solo pod

1. Un trigger o callback adquiere el lease de A y entra al motor de decisiones.
2. El motor prepara el dispatch y persiste `QueuePending`.
3. Todavía dentro de la operación coordinada, encola el trabajo de dispatch en Mule.
4. Mule puede iniciar ese dispatch en otro scope del mismo proceso antes de que termine la operación original.
5. El dispatch intenta adquirir el lease de A, que el productor aún conserva. También puede competir con otro dispatch de A que conserve el lease mientras publica por red.
6. Esa espera ocupa un puesto de ejecución; al agotarse, Krackend lanza la excepción y Mule la trata como un fallo ordinario.

Esta secuencia está habilitada por el código. El log aportado no permite identificar cuál de esas operaciones tenía el lease en ese momento. Un lease aún vigente de un arranque anterior también necesita distinguirse mediante su propietario y expiración; no corresponde asumirlo a partir del mensaje.

## Evidencia local nueva

Se amplió la reproducción de `benchmarks/Krackend.Concurrency.Diagnostics/Pipeline` para registrar el número de procesos, el PID, el número de proveedores raíz y el texto de los errores persistidos.

La ejecución produjo: un proceso, un proveedor raíz, tres scopes de acciones distintos, dos fallos locales con el texto `another runtime node`, 2,103 ms de espera para B pese a tener lease libre y reintentos de aproximadamente 30 s para los competidores de A. Los dos puestos del lane estaban ocupados esperando el lease de A.

El caso usa el executor original de Mule 2.3.2 y el coordinador real enlazado desde Krackend, con almacenamiento y contratos auxiliares controlados. Demuestra el mecanismo dentro de un proceso; no identifica al propietario del lease del incidente ni sustituye una prueba con el host y los proveedores desplegados.

## Plan de trabajo, por orden

| Fase | Cambio concreto | Dependencia | Cierre verificable |
|---|---|---|---|
| **1. Diagnóstico correcto** | Cambiar el texto de lease no disponible para que no invente otro nodo. Conservar el tipo público de excepción durante la transición. Asociar adquisición/renovación/liberación con réplica, arranque, operación y acción, además de instancia, token y expiración | Krackend | Un choque entre scopes locales se identifica como local cuando existe evidencia; un dueño desconocido permanece desconocido |
| **2. Mutaciones cortas** | Persistir el avance y su intención de dispatch bajo el lease. Liberarlo antes de activar o esperar trabajo posterior. Usar `TaskDispatch.QueuePending` y su recuperación como base del handoff durable existente | Krackend; revisión de recuperación | Retener la activación en una barrera no conserva el lease del productor. Un reinicio entre commit y activación recupera el mismo dispatch |
| **3. Publicación independiente** | Reservar el dispatch, liberar la protección de la saga, publicar y confirmar mediante otra mutación breve. Mantener la identidad de comando y dispatch y el tratamiento de entrega incierta | Fase 2; semántica de callback temprano | Un envío lento de A no impide mutaciones de B. Los envíos paralelos de una misma saga mantienen su paralelismo declarado |
| **4. Contención sin ocupar ejecutores** | Adquirir el lease sin el sondeo de dos segundos en la ruta cooperativa. Ante ocupación, conservar la misma acción pendiente y liberar el puesto sin consumir un intento funcional ni aplicar el reintento ordinario de 30 s | API publicada de Mule y adapter de Krackend | Con A retenida y presupuesto fijo, B entra antes de liberar A. La contención normal no aparece como fallo funcional |
| **5. Recepción directa** | Resolver rutas por endpoint/versión con una vista preparada, actualizada por despliegue/retirada/rollback. Consultar tareas de la etapa activa en lugar de cargar las de toda la instancia | Krackend, independiente del nuevo Mule | Mismo enrutamiento y decisiones, con menos filas leídas y consultas medidas por mensaje |
| **6. Aceptación con el flujo real** | Carga y recuperación con un host, después varias réplicas, sobre flujos equivalentes a la referencia Errs | Fases anteriores y proveedores reales | Progreso independiente, backlog controlado a la carga objetivo, latencias y recursos registrados, sin reinicios provocados por acumulación |

### Reglas de implementación

- La fase 1 es una corrección de diagnóstico; el cierre del bloqueo depende de las fases de ejecución.
- La intención de envío debe quedar persistida antes de terminar el avance que la produjo. Activar después de liberar el lease requiere recuperación durable si el proceso cae entre ambos momentos.
- La recepción de callbacks debe conservar una aceptación durable que pueda competir correctamente con el timeout. Su protección se limita a esa aceptación; no se retiene esperando que otro trabajo termine.
- Un callback temprano o ya aceptado pendiente de procesamiento necesita un resultado esperado distinto de un fallo funcional. Un callback obsoleto/resuelto se termina de forma idempotente.
- La publicación puede ejecutarse con identidad propia por dispatch. La exclusión de mutaciones de instancia no debe transformarse en una exclusión de toda la publicación de red cuando existen tareas paralelas en esa saga. El mapa de claves final debe reflejar las unidades de ejecución resultantes.
- La coordinación de distintas réplicas se conserva mediante la protección persistida. No se habilita el bypass del lease en scopes independientes ni se comparten DbContexts para aparentar paralelismo.
- La adquisición sin espera se activa cuando el adapter puede conservar el trabajo pendiente correctamente. Con Mule 2.3.2, devolver normalmente marcaría la acción completada y lanzar seguiría produciendo un fallo ordinario; ninguna de las dos opciones implementa el resultado esperado.
- Se conserva capacidad acotada. Los ejecutores deben ocuparse con trabajo útil elegible; no se añade una tarea ilimitada por mensaje ni se eleva la concurrencia para ocultar una espera circular.
- Los cambios de temporizadores, snapshots y consultas adicionales se priorizan por su coste medido después del bloqueo principal.

## Pruebas obligatorias

| Caso | Resultado esperado |
|---|---|
| Un proceso, A ocupada, dos mensajes de A y un mensaje de B, dos puestos | B avanza antes de liberar A; A permanece pendiente |
| Un productor de A genera un dispatch antes de terminar | El trabajo generado no queda consumiendo el puesto para esperar al productor |
| Activación llena o detenida después de persistir | El lease de negocio se libera y el dispatch sigue recuperable |
| Publicación de un dispatch bloqueada | Otras sagas progresan; la saga conserva estado coherente y sus tareas paralelas no se serializan innecesariamente |
| Callback antes de confirmar la publicación | La respuesta no se pierde y no se degrada un resultado ya resuelto |
| Callback aceptado antes del deadline, procesado después | La aceptación sigue prevaleciendo frente al timeout |
| Commit realizado y caída antes de activar | Se recupera el mismo trabajo con su identidad original |
| Broker recibió el comando y el proceso cayó antes de confirmar | Se trata la entrega incierta y se comprueba idempotencia del efecto |
| Lease de un arranque anterior aún vigente | El log identifica lo que se conoce; se respeta expiración/fencing y B no queda detenida globalmente |
| Dos réplicas intentando la misma mutación | Estado consistente y trabajo recuperable, sin escritura válida del propietario vencido |
| Aceptación local con 10-100 mensajes, ráfagas y 5-10 mensajes/minuto durante 5-10 minutos | Se observa progreso por mensaje y latencia frente a una saga de control; se verifican independencia entre sagas y pasos paralelos habilitados de la misma saga |

La prueba comparativa declara versión de código, proveedores, configuración de lanes/consumo, payload, pasos y recursos. Cuenta por separado mensajes aceptados, acciones terminadas y sagas completadas. La experiencia operativa de miles por minuto en Errs es la referencia aportada por el usuario, no una medición ejecutada en esta sesión.

La observación de 5-10 minutos no es un tiempo permitido de bloqueo por saga. Las pruebas locales pequeñas deben demostrar el defecto y su corrección; la certificación de miles por minuto se realiza después en un entorno dimensionado para ello.

## Estado actual y siguiente entrega

Las correcciones previas de fanout y resultado tras pérdida de lease tienen 26 casos enfocados aprobados. La reproducción ampliada volvió a confirmar los defectos abiertos con un único proceso.

La siguiente implementación debe entregar las fases 1–3 en Krackend y la fase 4 con el contrato real de Mule. Las fases 2–3 requieren verificar los estados durables y la recuperación existentes; el nuevo Mule no sustituye esa corrección del productor. La fase 5 reduce coste adicional. El cierre del incidente requiere la aceptación de la fase 6.

Este turno agregó evidencia y el plan solicitado. No cambió el texto de la excepción ni activó aún la nueva semántica de ejecución en código de producción.
