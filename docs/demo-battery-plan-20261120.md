# Batería para los servicios demo

Fecha: 20 de noviembre de 2026. Implementada en `agents/stress/docker`. La compilación y las comprobaciones locales están aprobadas; la ejecución en contenedores tiene su propio resultado y no se da por aprobada a partir de esos checks.

## Matriz

| Grupo | Entrada / carga | Condición comprobada |
|---|---|---|
| Dominio y contratos | 179 comprobaciones locales | Transacciones, idempotencia, rechazo/rollback, intención durable, versiones y DSL de los diez casos |
| Control HTTP | Una entrada de cada caso | Diez sagas completadas, resultado de negocio y Email entregado |
| Control consumer | Una entrada de cada caso mediante RabbitMQ | Mismo resultado al iniciar desde el consumer real |
| Carga 100 | Diez entradas de cada artefacto, juntas | Finalización y estado SQL correctos; sin sustituir terminación por aceptación |
| Carga 200 | Veinte entradas de cada artefacto, después de la carga 100 | Mismas aserciones y comparación de throughput/latencias |
| Aislamiento | Una venta con Email retenido veinte segundos y otra libre | Se observa primero la entrada del consumer retenido; la segunda completa y la rama digital de la primera avanza |
| Duplicados activos | Entrada HTTP repetida y consumer del mismo RequestId mientras se procesa | Una instancia, un cobro, un movimiento y un Email lógico |
| Duplicados de callback | Repetición durable de callbacks de una saga completada | Los registros de negocio permanecen iguales |
| Conflicto idempotente | Mismo RequestId con otro importe/payload válido | HTTP 400 y ninguna mutación adicional |
| Sin stock | Venta física con una unidad disponible y dos solicitadas | Compensación, venta rechazada, ninguna captura y ninguna reserva pendiente |
| Pago rechazado | Reserva válida y rechazo dirigido a captura | Reserva liberada, venta rechazada y ningún cobro |
| Devolución excesiva | Cantidad superior a la venta | Rechazo antes de reintegrar stock o devolver dinero |
| SKU compartido | Doce ventas de dos unidades frente a cinco disponibles | Solo dos completadas; una unidad restante, cero reservas y ningún sobregiro |
| Pago contra vencimiento | UC04 y UC09 sobre el mismo apartado | Un resultado coherente: venta confirmada o apartado vencido con pago tardío devuelto |
| Sostenida | Diez llegadas por minuto durante cinco minutos | Se siguen enviando entradas sin esperar a la anterior; cada saga tarda menos de diez segundos |
| Reinicio de Email | Consumer retenido antes de su efecto | Redelivery/recuperación sin perder la venta ni duplicar cobro o Email lógico |
| Reinicio de runtime | Esperando callback externo | La instancia sigue avanzando después del arranque |
| Caída de broker y reinicio del productor | Aceptación SQL con RabbitMQ detenido | Se conserva la misma intención durable y se procesa después de recuperar broker/productor |

Los casos negativos verifican rechazos de negocio previstos por separado de fallos técnicos. Una compensación prevista no cuenta como una venta exitosa. Los reinicios son opcionales mediante `-IncludeContainerRecovery`, comprueban el endpoint local de Docker y las etiquetas de pertenencia al proyecto; no eliminan contenedores ni volúmenes.

## Ejecución

```powershell
pwsh -File agents/stress/docker/Run-DemoBattery.ps1 -Suite Full -StartEnvironment -IncludeContainerRecovery
```

`-PlanOnly` muestra la matriz sin ejecutar. `-Suite Load` limita la ejecución a comprobaciones locales, control HTTP y las dos cargas. `-SkipBuild` reutiliza un candidato compilado, pero sigue ejecutando las comprobaciones locales. La restauración de paquetes debe haberse completado antes del build.

El controlador se detiene para los escenarios dependientes si una corrida falla; no inicia 200 sobre trabajo de 100 que quedó sin resolver. El verificador registra el fallo y su evidencia antes de terminar con código de error. Tras corregirlo corresponde repetir 100 y después 200 desde nuevas identidades de corrida.

## Medición

- La preparación de fixtures se realiza antes del reloj de carga.
- Cada entrada registra comienzo de recepción, aceptación y finalización observada.
- El throughput útil es sagas completadas por segundo hasta drenar el lote; el de recepción se mide aparte.
- La latencia observada incluye recepción y cola. La duración del runtime usa su inicio/finalización y excluye la espera anterior al inicio.
- p50/p95/p99 se calculan por rango más próximo sobre las completadas exitosas, indicando el número de muestras. Fallidas, pendientes y no aceptadas se cuentan aparte.
- Los informes incluyen resultados por RequestId, muestras de progreso y evidencia obtenida de los repositorios SQL a través de las APIs de los servicios.
- `report.md` y `report.html` resumen; `loads.csv` permite comparar cargas; `summary.json` y los JSON de escenario conservan detalle.

No se generan latencias ni throughput para pruebas bloqueadas o no ejecutadas. Los datos semilla y todas las comprobaciones de negocio se identifican por TestRunId; no se hace limpieza destructiva de SQL.

## Alcance actual

La biblioteca y los cinco hosts compilaron; las 179 comprobaciones locales aprobaron. Los scripts pasaron análisis sintáctico y se comprobaron los percentiles con series conocidas de 100 y 200 valores. Estos checks usan SQLite/modelo SQL y no equivalen a ejecutar SQL Server, RabbitMQ ni SMTP reales. El estado efectivo del intento solicitado está en [el reporte de resultados](demo-battery-results-20261120.md).
