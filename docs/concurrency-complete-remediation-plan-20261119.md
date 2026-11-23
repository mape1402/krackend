# Plan completo de corrección de recepción y ejecución paralela

**Fecha:** 19 de noviembre de 2026.

**Estado:** implementación y aceptación local concluidas. Krackend referencia Mule 2.4.0. La validación utiliza una compilación del tag publicado, verificada contra sus 135 archivos fuente/proyecto. Se aprobaron 328 casos seleccionados: 317 regresiones, diez escenarios SQL/Mule y una prueba de 30 mensajes durante cinco minutos. El candidato está sin desplegar; la capacidad de producción y múltiples pods siguen sin certificarse. Véanse [implementación, mediciones y límites](concurrency-implementation-results-20261119.md).

## Cierre local de las fases

| Fase | Entrega comprobada |
|---|---|
| 1. Base y medición | Reproducción causal del bloqueo anterior; métricas de recepción, lease, mutación, publicación y aplazamiento; tiempos de entrada y finalización separados |
| 2. Mule coherente | Referencias 2.4.0, ejecución por identidad y recuperación de cien entradas aceptadas con capacidad inmediata uno y ejecutores detenidos |
| 3. Contención cooperativa | A retenida libera puestos para que B complete; aplazamientos conservan identidad y presupuesto funcional |
| 4. Mutación breve | Activación después del lease, red fuera del lease, publicaciones paralelas, recibo de envío y fencing relacional |
| 5. Recepción durable | Recibo SQL recuperable de callback, protección frente a timeout obsoleto y recuperación de handoff interrumpido |
| 6. Coste por mensaje | Resolución selectiva entre mil rutas, lectura de dos tareas de etapa entre cien, eliminación de despertares locales por acción |
| 7. Aceptación local | Ráfagas de 10/20/50/100 y 30 llegadas a seis por minuto durante cinco minutos con motor, repositorios SQLite y ejecutores Mule reales |
| 8. Candidato revisable | Diff local, script de verificación, procedencia de paquetes, migración SQL, scripts de actualización/regreso y límites documentados |

El transporte externo del laboratorio es controlado y su entrada es `IIntakeBuffer`. La campaña sobre el broker, SQL Server, Redis y los recursos/configuración del pod productivo no se ha ejecutado. Las pruebas locales no certifican miles de mensajes por minuto, memoria de producción ni recuperación entre varios procesos. La secuencia siguiente conserva los objetivos del plan; este cierre delimita lo que efectivamente se comprobó.

## Resultado exigido

Una saga A que espera su lease, una respuesta o una activación no debe monopolizar la capacidad que permite avanzar a la saga B. El trabajo se protege por la instancia que modifica; la publicación de comandos y las esperas externas no deben prolongar esa sección.

La recepción debe confirmar después de una aceptación durable y recuperable, sin depender de que haya espacio en la cola de activación ni de que se ejecute la saga. La capacidad sigue acotada y la carga pendiente conserva identidad, estado y recuperación.

La referencia funcional es Errs Orchestrator: registro de resolvers preparado, scope por mensaje y procesamiento de un avance. El adaptador propio de ese proyecto admite ack manual e inbox/outbox opcional con Mule. Se compararán rutas equivalentes y mensajes completados, no solo mensajes recibidos.

## Hallazgos que justifican el plan

1. `OrchestrationInstanceLeaseUnavailableException` afirma literalmente que existe otro runtime node sin identificarlo. Una prueba con un proceso y un proveedor raíz reprodujo el mismo texto en dos acciones locales.
2. Krackend admite una acción al executor y después puede sondear el lease hasta dos segundos. La reproducción ocupó dos puestos con A y retrasó 2,103 ms a B aunque su lease estaba libre; los competidores recibieron reintentos de unos 30 s.
3. El productor puede persistir `TaskDispatch.QueuePending` y activar el dispatch mientras todavía conserva el lease de la misma saga. El hijo puede arrancar en otro scope antes de que termine el productor.
4. `RemoteCommandDispatchAction` conserva el lease durante el envío físico por red. La aceptación de callbacks conserva el suyo durante el enqueue durable.
5. El handler de `PigeonIngressAdapter` lee y pagina todas las configuraciones activas, selecciona destinos, espera los enqueues y después completa el mensaje. La finalización del consumo está acoplada a esas esperas.
6. Mule 2.3.2 guarda la acción y después espera al commit notifier; una cola inmediata llena puede bloquear ese notifier. Este mecanismo puede retrasar el ack aun después de persistir el trabajo.
7. El caso reportado de 3,000 mensajes en aproximadamente una hora representa un throughput efectivo cercano a 0.83 mensajes/s. Ese cociente no identifica por sí solo dónde se consumió el tiempo ni equivale a la latencia individual de cada mensaje.

## Disponibilidad nueva de Mule

Se verificó la publicación de [Mule v2.4.0](https://github.com/mape1402/mule/releases/tag/v2.4.0). Su contrato público incorpora `ExecutionKey`, `ExecutionKeySelector`, `MuleActionContext.DeferUntil`, razones de aplazamiento, activación no bloqueante y recuperación de trabajo cuya señal se omite. También cambia claims/proyección y FastLane.

Eso habilita revisar la integración; no acredita por sí solo que Krackend esté reparado. La publicación de un paquete y las pruebas de su proveedor no sustituyen las pruebas de composición del host de Krackend.

La descarga directa desde NuGet falló por resolución DNS en este entorno. Los paquetes locales encontrados tenían hashes distintos de los assets publicados y se descartaron como prueba de equivalencia binaria. Se verificaron los 135 archivos fuente/proyecto contra el commit publicado `264770bb73764c1026f1724ed530c0bb7af583cb` y se construyó un feed aislado desde esas fuentes para net10. La aceptación local utiliza esa compilación del tag; no se afirma igualdad binaria con los paquetes oficiales de NuGet.

## Secuencia de implementación

### 1. Conservar una base comprobable y medir el recorrido

- Conservar el diff previo de fanout y resultado tras pérdida de lease, así como los diagnósticos existentes.
- Confirmar versiones realmente cargadas y configuración efectiva de recepción, lanes, proveedores, réplicas y recursos. Los binarios antiguos de pruebas no sirven para demostrar la versión del código actual.
- Medir recepción a aceptación durable, aceptación a ack, espera de cola, intento de lease, mutación, activación y publicación. Registrar contadores de mensajes aceptados, acciones completadas y sagas terminadas por separado.
- Añadir las mediciones mínimas necesarias al recorrido existente; no crear otra plataforma de telemetría ni consultas adicionales por cada medición.
- Conservar una reproducción de un solo host y otra del flujo usado en la carga reportada.

**Cierre:** se puede atribuir el tiempo a una etapa y comparar el mismo escenario antes/después.

### 2. Integrar Mule 2.4.0 como un conjunto coherente

- Actualizar conjuntamente core Mule y los providers usados por Krackend, muestras y pruebas; revisar el grafo restaurado para excluir mezclas accidentales 2.2/2.3/2.4.
- Revisar contratos token-aware, esquema EF, serialización Redis/FastLane, acciones existentes e índices antes de definir migraciones. Preparar cambios de esquema y compatibilidad de escritores cuando sean necesarios; no asumir que solo cambia el número de versión.
- Verificar que un commit notifier saturado retorna sin esperar capacidad y que el trabajo persistido se recupera aunque se pierda la señal inmediata.
- Configurar selección de trabajo elegible por clave. Las unidades que mutan una misma saga comparten su identidad de instancia; sagas distintas conservan claves distintas aunque compartan una correlación de negocio.
- Los triggers todavía sin instancia usan su identidad estable de inicio/idempotencia. Resolver acciones antiguas a partir de su identidad/payload existente; no dejarlas sin la política acordada por carecer de metadata nueva.
- La publicación física puede tener identidad por dispatch. Su reserva/confirmación sigue protegida como mutación de instancia; una clave global de saga no debe serializar toda la red ni anular sus tareas paralelas.

**Cierre:** compila contra APIs reales y la activación llena no bloquea después de persistir. La prueba usa capacidad fija y conserva la recuperación.

### 3. Tratar contención como trabajo pendiente

- Integrar trigger, callback, dispatch y timeout con el resultado durable de aplazamiento de Mule.
- En esas rutas, un intento de lease ocupado debe terminar sin el sondeo de dos segundos dentro del executor. La misma acción conserva ID, payload, deduplicación y fecha original; el aplazamiento no consume intentos funcionales.
- Distinguir lease ocupado, callback temprano, respuesta aceptada pendiente de procesar y despertar anterior al deadline. Cada motivo esperado debe conservar el trabajo adecuado y volver a evaluarse cuando corresponda.
- Una respuesta obsoleta, duplicada o de un intento ya resuelto se maneja de forma idempotente; no se aplaza para siempre.
- Mantener el tratamiento de los fallos reales y su presupuesto de reintentos. La pérdida de un lease durante una mutación tampoco se convierte en éxito.
- Cambiar el texto engañoso de otro nodo. Correlacionar instancia, operación/acción, réplica/arranque, token y expiración observados. Identificar dueño local/remoto solo con evidencia; dueño desconocido debe permanecer desconocido.

**Cierre:** A ocupada queda pendiente y libera el puesto. B avanza antes de liberar A. La contención normal deja de producir un fallo ordinario con pausa de 30 s.

### 4. Acortar la sección que conserva el lease

- Delimitar un avance de estado breve y persistir su intención de envío antes de finalizarlo.
- Aprovechar `TaskDispatch.QueuePending` y su recuperación existente como base del handoff; evitar un segundo scheduler o una cola global de negocio.
- Liberar el lease del productor antes de activar trabajo posterior. Debe sobrevivir una caída entre commit y activación conservando el mismo dispatch.
- En el envío físico: reservar el dispatch mediante una mutación breve, liberar protección, publicar y confirmar con otra mutación breve. Mantener idempotencia, identidad del comando y entrega incierta cuando el broker recibe pero el proceso cae antes de confirmar.
- Asegurar que una confirmación tardía de publicación no sobrescribe un callback que ya resolvió el intento. El callback temprano debe poder conservarse y procesarse correctamente.
- Verificar la protección persistida frente a un propietario vencido y las escrituras después de perder el lease; la cancelación por sí sola no implementa fencing.

**Cierre:** bloquear activación o red mediante una barrera no conserva el lease de negocio del productor. Las tareas paralelas declaradas siguen pudiendo publicar concurrentemente.

### 5. Separar recepción durable de ejecución

- La recepción resuelve destinos y registra trabajo recuperable; no espera a que el flujo avance ni a que exista un puesto libre de ejecución.
- Completar el mensaje del broker solo después de la aceptación durable. Si se omite la señal inmediata, la recuperación debe encontrar la misma recepción.
- Mantener la atomicidad necesaria entre aceptación de callback y decisión de timeout. Un callback aceptado antes del deadline conserva esa protección aunque se procese después.
- Revisar específicamente el lease de `IntakeBufferMule`: limitarlo a la aceptación necesaria, sin esperar ejecución o activación. No se elimina antes de definir y comprobar cómo compite el registro durable con el timeout.
- Resolver la contención de recepción sin mantener un handler ocupado en el sondeo prolongado; conservar la entrega pendiente/redelivery correcta y evitar confirmaciones prematuras.
- Probar aceptación parcial con varios destinos y redelivery: la repetición no debe duplicar efectos en destinos ya aceptados.
- Declarar y verificar la configuración efectiva de concurrencia/prefetch/ack del transporte. No asumir que los valores de la muestra son los del pod.

**Cierre:** con ejecutores detenidos o cola inmediata saturada, el trabajo se acepta de forma durable y el consumo puede confirmar sin esperar a ejecutarlo. Se conserva el contrato de deadlines y redelivery.

### 6. Quitar trabajo redundante por mensaje

- Sustituir la lectura de todas las configuraciones activas por una resolución selectiva de endpoint/versión, mediante una vista de rutas preparada. Actualizar/invalidate por despliegue, retirada, rollback y cambios de generación.
- Conservar rutas archivadas de callbacks, consumidores compartidos, staged rollout y rechazo de triggers retirados.
- Consultar tareas de la etapa activa y agrupar lecturas relacionadas. Mantener scopes y DbContexts propios; no ejecutar consultas concurrentes sobre un mismo DbContext.
- Medir filas leídas, comandos a almacenamiento y asignaciones antes/después. Incorporar solo las optimizaciones que reduzcan coste conservando el resultado.
- Consolidar los despertares locales de timeouts/comandos programados aprovechando la programación durable y el ciclo de vida del host. Evitar una tarea no cancelable por cada espera ya resuelta.

**Cierre:** mismo enrutamiento y decisiones con menor coste medido. La memoria y los despertares permanecen acotados bajo carga sostenida.

### 7. Validar comportamiento y capacidad con los proveedores reales

**Aceptación local acordada:** comprobar el arreglo con 10-100 mensajes y ventanas de observación de 5-10 minutos. No se condiciona esta corrección a reproducir localmente miles de mensajes por minuto. La ventana sirve para observar progreso y recuperación; no convierte cinco o diez minutos en una latencia aceptable por saga.

Primero se mide una saga de control con el mismo flujo y dependencias. Después se prueban ráfagas de 10, 20, 50 y 100 mensajes y llegadas de 5-10 mensajes por minuto durante 5-10 minutos. Se cuentan por separado mensajes de entrada, acciones internas, comandos, callbacks y sagas completadas.

Se retiene A deliberadamente y se verifica que B avance antes de liberar A, con el mismo presupuesto de ejecutores. Dentro de una saga se habilitan dos pasos paralelos y se comprueba que ambos llegan a sus barreras antes de liberar cualquiera; las dependencias reales del flujo se mantienen. La mutación breve del mismo estado sigue protegida sin convertirla en una exclusión durante toda la ejecución o publicación.

Cada mensaje debe tener trazabilidad de recepción, aceptación, comienzo y fin de sus acciones, más la causa de cualquier espera. Se comparan las latencias con la saga de control y se inspecciona el progreso durante toda la prueba, no solamente el resultado final. No se aceptan congelamientos ni sondeo de dos segundos/reintentos de treinta segundos por contención normal.

| Escenario | Criterio |
|---|---|
| Un proceso, A retenida, dos mensajes de A y uno de B, dos puestos | B avanza antes de liberar A; A sigue pendiente y no consume intentos funcionales |
| Varias sagas con la misma correlación de negocio | Cada instancia mantiene su independencia |
| Productor de A genera su dispatch antes de finalizar | El hijo no monopoliza capacidad esperando al productor |
| Activación llena con trabajo ya persistido | Enqueue/ack no esperan capacidad inmediata; recuperación conserva IDs |
| Publicación detenida por red | B y otras publicaciones elegibles progresan; no hay lease de saga prolongado por red |
| Callback temprano, duplicado y obsoleto | Se conserva el resultado válido y la repetición es idempotente |
| Callback aceptado a tiempo frente a timeout | No aparece timeout falso por espera interna de procesamiento |
| Caída antes/después del commit, enqueue y confirmación de envío | Trabajo recuperable y tratamiento correcto de entrega incierta |
| Reinicio con leases vigentes y dos réplicas | Expiración/propiedad seguras; la recuperación de A no detiene globalmente B |
| Fallo funcional después de varios aplazamientos | Mantiene su presupuesto real de fallo |
| Ráfagas locales de 10, 20, 50 y 100 mensajes | Progreso concurrente observable, resultados correctos y memoria acotada; no aparecen congelamientos ni pausas ordinarias de 30 s por contención |
| Llegadas de 5-10 mensajes/minuto durante 5-10 minutos | Las sagas anteriores no impiden que las nuevas avancen; cada espera conserva una causa y una duración registradas |
| Dos pasos paralelos habilitados de la misma saga | Ambos comienzan y alcanzan sus barreras antes de liberar cualquiera; no se serializa la publicación completa por el lease de la saga |

Comparar un host y después varias réplicas con el mismo flujo, payload, proveedores y recursos declarados. Una campaña posterior con recursos adecuados medirá miles de mensajes por minuto y el límite de capacidad; no es un prerrequisito para demostrar y corregir este bloqueo local. Las pruebas pequeñas validan la independencia y las carreras, sin certificar por sí solas la capacidad máxima.

### 8. Preparar una entrega revisable

- Entregar diff de implementación, versiones exactas, migraciones cuando correspondan, pruebas y resultados antes/después.
- Documentar recuperación de acciones pendientes anteriores al upgrade, compatibilidad de lectores/escritores y límites de rollback.
- Mantener despliegue, publicación y replay masivo de trabajo productivo fuera de esta implementación local.

**Cierre:** candidato verificable para revisión y posterior despliegue autorizado, con limitaciones pendientes explícitas.

## Orden y alcance

El primer bloque entrega las fases 1-3 y la corrección de activación/contención. El segundo entrega las fases 4-5 para separar las esperas de la mutación y recepción. El tercero reduce coste mediante la fase 6. Cada bloque incluye sus regresiones; la fase 7 valida el conjunto con carga real y la fase 8 prepara la entrega.

No se considerará reparado el incidente solo porque desaparezca el texto del error, haya más workers, los mensajes entren más rápido que antes o las sagas terminen eventualmente. El resultado requerido es progreso independiente, recuperación correcta y throughput útil demostrado.
