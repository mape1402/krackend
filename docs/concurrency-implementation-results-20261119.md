# Corrección de concurrencia: implementación y evidencia local

Fecha: 19 de noviembre de 2026. Estado: implementación y aceptación local terminadas; candidato sin desplegar.

## Causa y cambio

La independencia de los IDs de saga no impedía que sus mensajes compitieran por todos los puestos de un carril de Mule. Un mensaje admitido podía ocupar su puesto sondeando durante dos segundos el lease de A; suficientes competidores de A dejaban a B sin executor aunque B tuviera su propio lease libre. La excepción convertía esa contención en reintentos funcionales de aproximadamente treinta segundos. El productor también podía activar hijos mientras conservaba el lease que esos hijos necesitaban. La publicación de red y la aceptación durable de callbacks prolongaban esa exclusión.

La implementación incorpora Mule 2.4.0, claves de ejecución por instancia para callbacks/timeouts y por dispatch para publicaciones físicas, adquisición sin sondeo en esas rutas y `DeferUntil` para contención normal. Se conserva el ID de la acción, su creación y su presupuesto funcional de fallos. `TaskDispatch.QueuePending` sigue siendo la intención durable; su activación se realiza después de liberar el lease productor.

El envío físico ocurre fuera del lease. Su recibo de publicación se escribe de forma condicional sin sobrescribir el estado de callback; una confirmación aplazada reutiliza ese recibo y evita repetir el envío confirmado. Un reinicio entre la entrega remota y la escritura del recibo conserva la ventana habitual de entrega al menos una vez; el receptor debe deduplicar por dispatch como antes.

Los callbacks con identidad de dispatch válida se aceptan mediante un recibo SQL atómico que incluye el payload recuperable. Su confirmación de transporte espera el enqueue durable, sin esperar que la saga se ejecute o libere su lease. El timeout observa el recibo y la concurrencia de la misma fila evita que una decisión cargada antes del callback lo sobrescriba. El reconciliador existente recupera handoffs interrumpidos; conserva los resultados terminales de Mule.

Las escrituras relacionales bajo lease validan y bloquean la fila de instancia en la misma transacción breve que guarda el cambio. Un propietario antiguo no puede confirmar después de que otro haya adquirido la instancia. El coordinador cancela al perder la renovación y tampoco devuelve éxito si el delegado ignoró esa cancelación.

La recepción Pigeon usa una resolución indexada de endpoint/versión con fallback para rutas antiguas; los despliegues y retiros actualizan la proyección almacenada. Las decisiones leen tareas de la etapa activa. Se eliminaron los `Task.Run`/`Task.Delay` locales por timeout/comando programado y se mantiene el ciclo durable de recuperación de Mule. Se conserva polling para descubrir commits y trabajo creado por otras réplicas; una señal local no prueba que otro proceso haya observado el commit.

El fanout Redis del monitor se procesa en un canal separado y acotado, sin esperar Redis dentro de la mutación de saga. Los cambios descartados producen una solicitud de resincronización.

## Proveedores, capacidad y alcance de la medición

Las pruebas nuevas usan el motor, los repositorios de Krackend, transacciones SQLite y el dispatcher/ejecutores reales de Mule. El servicio de negocio externo se controla mediante barreras y callbacks. Se fija un lector, dos puestos de ejecución por carril, cola inmediata de capacidad uno y cola de ejecución de capacidad dos. La recuperación se configura a 50 ms en este laboratorio. El proveedor SQLite usa conversiones UTC para ordenar fechas de Mule.

La entrada del laboratorio es `IIntakeBuffer`; no se presenta como una medición de RabbitMQ, Azure Service Bus o K8s. Las regresiones del adaptador Pigeon verifican aparte la selección y el settlement. Docker negó el acceso al daemon y LocalDB no pudo consultar su configuración. No hay certificación de capacidad máxima ni resultados de múltiples pods/Redis real.

La compilación del proyecto de pruebas y sus dependencias terminó con cero errores y 90 advertencias. La verificación final aprobó **328 casos seleccionados, cero fallos**: 317 regresiones, diez escenarios SQL/Mule y una prueba sostenida de cinco minutos. Los informes autoritativos de esta ejecución son `artifacts/concurrency-suite.json`, `artifacts/concurrency-parallel.json` y `artifacts/concurrency-sustained-tests.json`.

Los diez escenarios SQL/Mule comprueban las ráfagas, B completada mientras A conserva deliberadamente su lease, publicaciones paralelas de la misma saga, aceptación con ejecutores detenidos, callback frente a timeout y recuperación de handoff, fencing del propietario vencido, rutas/etapa selectivas, timeout aplazado sin consumir intentos, comando futuro recuperado sin timer local e inicios durables distintos con correlación de negocio compartida. Las regresiones también cubren activación de todos los hijos persistidos aunque uno encuentre un conflicto, recuperación, compensación, idempotencia y settlement de Pigeon.

Mediciones finales de ráfagas, con dos puestos por carril. La latencia de entrada empieza en la creación de la acción trigger durable; el total del lote incluye su aceptación. La latencia de saga empieza cuando se crea la instancia y por eso excluye la espera anterior al inicio.

| Entradas | Lote completo (s) | Entrada a fin p95 (s) | Entrada a fin máxima (s) | Saga iniciada a fin máxima (s) |
|---:|---:|---:|---:|---:|
| 1, control frío | 2.85 | 2.76 | 2.76 | 2.34 |
| 10 | 5.82 | 4.83 | 4.94 | 4.63 |
| 20 | 7.74 | 6.47 | 6.52 | 5.99 |
| 50 | 14.03 | 12.27 | 12.77 | 7.13 |
| 100 | 26.41 | 23.17 | 23.57 | 7.51 |

Se completaron las 181 sagas de esas ráfagas, sin acciones fallidas. Con los ejecutores detenidos, capacidad inmediata uno y ninguna saga iniciada, se aceptaron otras cien entradas durables en **277 ms**; al iniciar Mule se completaron sus cien sagas. Esta prueba distingue aceptación durable de ejecución.

La prueba sostenida introdujo **30 mensajes a seis por minuto durante 300.04 segundos**. Cada entrada esperó su propia finalización antes de la siguiente llegada programada: latencia desde la llamada de recepción hasta saga completada p50 **581 ms**, p95 **814 ms** y máxima **3.21 s**, incluyendo el arranque frío. Se verificaron las treinta sagas completadas y cero acciones fallidas, sin acumular esperas entre llegadas. Las mediciones están en `concurrency-burst-results.json`, `concurrency-activation-results.json` y `concurrency-sustained-measurements.json`, bajo `artifacts/`.

Estos resultados demuestran la independencia bajo contención deliberada y la recuperación local. No demuestran que cien entradas se ejecuten simultáneamente con dos puestos por carril, ni miles de mensajes/minuto en una infraestructura que no se ha medido.

## Reproducción

Proyecto de pruebas: `tests/Krackend.Sagas.Orchestrations.Tests`. Escenarios: `RuntimeParallelExecutionTests`, `RuntimeSustainedConcurrencyTests` y `RuntimeConcurrencyRegressionTests`.

Se usa un runner en proceso porque VSTest no pudo abrir su conexión de socket en este entorno. Ejecuta `Fact` y teorías con `InlineData` de las clases seleccionadas; no sustituye una campaña de tests con fixtures/MemberData ni se reporta como VSTest aprobado.

```powershell
dotnet restore tests/Krackend.Sagas.Orchestrations.Tests/Krackend.Sagas.Orchestrations.Tests.csproj -p:MinVerSkip=true
dotnet restore benchmarks/Krackend.Concurrency.Diagnostics/Regression/Regression.csproj -p:MinVerSkip=true
powershell -NoProfile -File benchmarks/Krackend.Concurrency.Diagnostics/verify-concurrency.ps1 -Sustained
```

El [script de verificación](../benchmarks/Krackend.Concurrency.Diagnostics/verify-concurrency.ps1) compila y ejecuta los tres grupos; `-SkipBuild` reutiliza un candidato ya compilado. Las restauraciones anteriores requieren un feed accesible con Mule 2.4.0. En este entorno sin DNS se utilizó el feed aislado descrito abajo y el cache de paquetes existente. Los JSON de medición y los logs se guardan en `artifacts/`. La latencia de recepción se mide por separado de la ejecución; los informes de ráfagas incluyen la espera desde creación de la acción trigger.

## Dependencias y procedencia

Todas las referencias directas de Mule pasan a 2.4.0. El tag publicado es `v2.4.0`, commit `264770bb73764c1026f1724ed530c0bb7af583cb`. La resolución DNS impidió descargar NuGet. Los nupkg locales encontrados tenían hashes distintos de los assets publicados, por lo que no se usaron como supuestos binarios oficiales equivalentes.

Se compararon los 135 archivos fuente/proyecto de `C:\elysium\mule` contra los hashes Git del tag, normalizando exclusivamente CRLF a LF y conservando el BOM: ninguna diferencia. Esas fuentes se copiaron a `artifacts/mule-2.4.0-tag` y se empaquetaron para net10 con versión y commit explícitos en `artifacts/mule-feed`. La validación utiliza esa compilación del tag, no una afirmación de igualdad binaria con NuGet. El feed y el cache se encuentran bajo `artifacts/` y no cambian la configuración NuGet del equipo.

## Migración y regreso

La migración de ejemplo SQL Server es `20261119152406_RemoveCrossSagaContention`. Agrega campos de callback y rutas, `PendingReason`/`DeferralReasonCode` de Mule y sus índices, y proyecta rutas existentes a partir de los settings. El resto de modelos de Mule se obtiene del contrato real 2.4.0 y del snapshot existente; no se inventa una tabla de cola adicional.

Se generaron los scripts de [actualización](sql/concurrency-20261119-up.sql) y [regreso](sql/concurrency-20261119-down.sql) desde el ensamblado de migraciones compilado; no se aplicaron a una base externa. EF confirmó que el modelo no tiene cambios pendientes respecto al snapshot. El script de actualización es idempotente para esta migración sobre la base anterior `20261111210042_AddFailureEventScope`; no sustituye las migraciones previas de un host nuevo.

Antes de actualizar: detener consumidores/escritores antiguos, conservar una copia recuperable de SQL y de los buffers durables Redis, y aplicar la migración con el ensamblado y personalización de esquema del host. Actualizar Krackend y todos los paquetes Mule de forma coherente; no mezclar escritores antiguos que ignoren los recibos/fences nuevos. Arrancar recuperación y después reabrir intake. El diagnóstico debe contar entradas aceptadas, acciones aplazadas, comandos, callbacks y sagas realmente completadas.

El downgrade de esquema se bloquea si existen callbacks aceptados pendientes, porque eliminar sus columnas destruiría payloads recuperables. Antes de cualquier downgrade: detener intake, terminar o exportar esos recibos y handoffs, vaciar/respaldar buffers y detener todos los escritores nuevos. Conservar los campos aditivos es preferible a eliminarlos durante un rollback de binarios. Las acciones aplazadas conservan su identidad y fecha de elegibilidad, pero un runtime anterior restaura los defectos de concurrencia descritos.

Los repositorios externos personalizados necesitan implementar los contratos suplementarios de recibo atómico para la nueva ruta de publicación/aceptación. EF implementa ambos recibos. InMemory implementa el recibo de publicación; como su unidad de trabajo no ofrece rollback de un grafo completo, conserva la aceptación de callbacks bajo el lease breve y sin sondeo, con el notifier no bloqueante de Mule. Así no promete una atomicidad de timeout/callback que ese proveedor no puede ofrecer. La protección transaccional de lease está implementada para proveedores relacionales; Mongo/Redis real necesita su campaña de integración antes de certificarlo.

No se despliega, publica paquetes ni ejecuta replay masivo de producción como parte de esta entrega local.

## Estado del código

Los cambios están en `C:\elysium\Krackend`, sin commit ni push. La rama local sigue siendo `codex/work-20261118`, HEAD `af0b50477e578caefe7854f98c1d8570d8bc7149`; su árbol base se comprobó idéntico al main analizado. Se creó la rama remota `fix/concurrency-root-cause-20261118` desde main. Los metadatos Git locales son de solo lectura en este entorno, por lo que no se pudo cambiar la rama local ni registrar los cambios; el diff y los archivos nuevos permanecen disponibles para revisión.
