# Comparación del procesamiento: Errs Rabbit, Errs Orchestrator y Krackend

## Resultado

La referencia del usuario es pertinente: la ruta inspeccionada de Errs Orchestrator procesa un mensaje mediante un resolver registrado en memoria, un scope propio y una operación de etapa. No se encontró un bucle de adquisición de lease de saga en esa ruta. Krackend agrega una espera de lease dentro de la acción ya admitida por Mule, además de conservar el lease durante publicación de comandos y aceptación durable de callbacks.

La reproducción existente muestra que esa espera puede ocupar todos los puestos de un lane y retrasar una instancia independiente que sí tiene su lease disponible. Después, la contención se convierte en un fallo con el reintento ordinario de Mule. Esta conclusión se refiere al acoplamiento entre el adapter de Krackend y el executor: no demuestra que Mule por sí solo provoque el problema en cualquier consumidor.

Errs Orchestrator también referencia Mule 2.3.2, con inbox/outbox Redis opcional y un perfil HighThroughput. Por tanto, no corresponde atribuir toda la diferencia a la presencia de Mule. Hay diferencias de configuración, proveedores y trabajo por mensaje que deben conservarse en cualquier comparación de rendimiento.

## Fuentes locales inspeccionadas

El paquete indicado se encontró en `C:\repos\packages\errs-package-rabbit`. Los repositorios de referencia se leyeron sin modificarlos. Se inspeccionó el código local; no se verificó que coincida con las imágenes desplegadas ni se ejecutó una nueva carga sobre esos servicios.

| Aspecto | Evidencia en la referencia | Diferencia relevante en Krackend |
|---|---|---|
| Subscriber genérico Rabbit | `Errs.Rabbit.Subscriber/MessageQueueService.cs:118-132`: EventingBasicConsumer, autoAck y callback async; scope por mensaje | Este mecanismo no espera una cola durable intermedia antes de aceptar el siguiente mensaje. Su ack ocurre antes del resultado del handler |
| Adapter propio de Errs Orchestrator | `MessageQueue/MessageQueueService.cs:104-129`: prefetch 1 por consumidor, ack manual después de EnqueueAsync | Permite una comparación con aceptación controlada; no depende del autoAck del subscriber genérico |
| Selección del handler | `MessageQueue/ConsumersRegistry.cs` y `OrchestrationMessageProcessor.cs:80-102`: registro en memoria | `PigeonIngressAdapter.cs:149-163` lee todas las configuraciones activas y después selecciona endpoint/versión |
| Scope de ejecución | `OrchestrationMessageProcessor.cs:55`: scope propio para procesar el mensaje | Krackend también usa scopes propios; ese hecho no libera los puestos compartidos de ejecución |
| Ruta durable opcional | `MuleOrchestrationIngress.cs:24-55`: procesa inline o encola un intent durable según la definición | En la recepción de callbacks de Krackend se conserva el lease mientras se espera el enqueue durable |
| Configuración Mule | `MuleOrchestrationExtensions.cs:23-40`: Redis, HighThroughput y lanes de ingreso/salida | Krackend usa una topología y un presupuesto diferentes. Cambiar el presupuesto no elimina la espera circular reproducida |
| Acción admitida | `MuleIngressAction.cs`: delega directamente al procesador del mensaje | `RemoteCommandDispatchAction.cs:91` espera la adquisición del lease antes de ejecutar el paso |
| Estado de una etapa | `StageTrackingManager.cs`: lee y guarda tracking por traceId; existen proveedores Redis/Mongo | La ruta inspeccionada no contiene el sondeo de lease de dos segundos que usa Krackend |

Las rutas de entrega del subscriber genérico y del adapter propio de Errs Orchestrator son diferentes. La comparación equivalente para aceptación durable debe usar el adapter propio y declarar si cada orquestación tiene UseDurableActions habilitado. El código local incluye definiciones que lo habilitan.

## Decisión arquitectónica derivada

La unidad de ejecución debe ser un mensaje o avance breve y durable. Una espera por lease o por una respuesta no debe conservar un ejecutor que podría procesar otra instancia.

1. Resolver el destino de recepción a partir de una vista de rutas preparada para el endpoint/versión, con actualización correcta durante despliegues y rollback. La recepción debe conservar sus reglas de aceptación durable frente a timeouts.
2. Admitir trabajo que pueda avanzar. Una instancia ocupada debe quedar pendiente y liberar el puesto sin consumir intentos funcionales; el paquete Mule y el adapter de Krackend deben implementar juntos esa transición.
3. Limitar el lease a la mutación persistida. Separar la publicación física requiere antes definir reserva durable, idempotencia, confirmación de envío, entrega incierta y callback temprano. Eliminar el lease sin esa transición cambiaría las garantías del estado.
4. Mantener en la ruta crítica únicamente la decisión, sus escrituras necesarias y la entrega durable del trabajo posterior. El fanout opcional ya se desacopló.
5. Medir mensajes aceptados, acciones terminadas y sagas completadas por separado, con los mismos flujos, carga y recursos. Comparar esperas de cola/lease, comandos a almacenamiento, latencia de publicación y recuperación al reiniciar.

## Prioridad

El arreglo principal es eliminar la ocupación de ejecutores por contención y el acoplamiento entre enqueue y lease. La resolución selectiva de rutas y las consultas de etapa activa reducen coste adicional, pero no sustituyen ese arreglo. La capacidad publicada de aplazamiento cooperativo sigue pendiente de integrar.

La experiencia de miles de mensajes/minuto en Errs es la referencia operativa aportada por el usuario. Este análisis de código no produce una medición equivalente ni determina por sí solo la capacidad máxima de Krackend.
