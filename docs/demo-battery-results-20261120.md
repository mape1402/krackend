# Batería de pruebas de Krackend

Fecha de ejecución: 20/21 de noviembre de 2026. Corrida principal: `demo-battery-20261121033303-e0fa5a`.

Estado global: **Failed**. La infraestructura, el smoke test, las cargas, la prueba sostenida y la recuperación funcionan. La batería detectó un defecto real en la transición `StopAndCompensate`; por eso el resultado global no debe considerarse aprobado.

## Resumen

| Escenario | Resultado | Evidencia |
|---|---|---|
| Compilación del laboratorio | Passed | 0 errores; 24 advertencias |
| Domain, outbox and DSL checks | Passed | Verificación local de la batería |
| SQL Server, RabbitMQ y SMTP | Passed | Preflight del laboratorio Docker |
| 10 casos por HTTP | Passed | 10/10 |
| 10 casos por consumers | Passed | 10/10 |
| Carga de 100 entradas | Passed | 100/100 completadas |
| Carga de 200 entradas | Passed | 200/200 completadas |
| Seguridad, duplicados, compensación y carreras | **Failed** | 4/9 aprobadas; 5 fallidas |
| 10 llegadas por minuto durante 5 minutos | Passed | 50/50 completadas |
| Reinicios y recuperación | Passed | 3/3 aprobadas |

La corrida principal se detuvo después de seguridad para no contaminar las siguientes mediciones con trabajo sin resolver. La prueba sostenida y la recuperación se ejecutaron después, de forma independiente, sobre el mismo laboratorio.

## Carga

| Carga | Estado | Aceptadas | Completadas | Fallidas | Pendientes | Duración (s) | Sagas/s | p50 (ms) | p95 (ms) | p99 (ms) | Máxima (ms) |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | Passed | 100 | 100 | 0 | 0 | 138.00 | 0.72 | 96,153.47 | 120,254.67 | 130,362.58 | 132,477.80 |
| 200 | Passed | 200 | 200 | 0 | 0 | 347.50 | 0.58 | 248,241.85 | 318,035.21 | 321,221.47 | 335,725.17 |

La latencia incluye recepción y espera de cola. Los percentiles se calcularon únicamente con sagas completadas exitosamente.

La prueba sostenida envió 50 entradas en 300.40 segundos y completó las 50. Su latencia fue: p50 4,289.18 ms, p95 6,634.16 ms, p99/máxima 8,254.91 ms.

## Seguridad y compensación

Aprobaron:

- aislamiento de una saga y de una rama paralela mientras Email estaba retenido;
- entradas HTTP/consumer duplicadas durante procesamiento activo;
- callbacks duplicados sin cambiar el estado de negocio;
- rechazo de un `RequestId` reutilizado con un payload distinto.

Fallaron:

- rechazo de inventario: la tarea falló, pero la saga quedó `Running`;
- rechazo de pago: la tarea falló, pero la saga quedó `Running`;
- devolución excesiva: el rechazo de negocio ocurrió, pero el runtime terminó en `DeadLettered` y la prueba esperaba `Stopped` o `Failed`;
- 12 reservas concurrentes: 2 terminaron y 10 quedaron `Running` tras el rechazo de inventario;
- carrera pago/caducidad: la saga de caducidad terminó, pero la saga de pago quedó `Running`.

El diagnóstico en SQL confirma el defecto principal: las tareas fallidas tienen política `StopAndCompensate`, sus etapas e instancias permanecen `Running` y no existe ninguna fila en `CompensationExecutions`. No es un timeout artificial de la prueba.

## Recuperación

Las tres pruebas finales aprobaron:

- reinicio de Email mientras esperaba un callback externo;
- reinicio del Runtime mientras esperaba un callback externo;
- aceptación duradera con RabbitMQ caído, reinicio del productor y publicación posterior sin duplicar efectos.

Para que el último caso probara recuperación y no el timeout predeterminado del bloqueo, el laboratorio fija `Demo:LockTimeoutSeconds` en 15 segundos. El guion también espera a que RabbitMQ esté sano antes de volver a iniciar Sales.

## Configuración aplicada

- contexto Docker reducido y publicación fijada a `net10.0`;
- endpoints locales fijados a IPv4 y lectura correcta de health checks de texto;
- compatibilidad de RabbitMQ 4 para las colas transitorias no exclusivas usadas por Pigeon;
- dominio y opciones JSON consistentes en los cinco servicios;
- validación del despliegue y health del Runtime contra los 10 artefactos esperados;
- timeouts de carga acordes al volumen medido;
- bloqueo corto y secuencia de arranque determinista para recuperación.

No se borraron bases de datos, volúmenes ni evidencias.

## Advertencias pendientes

La compilación terminó con 0 errores y 24 advertencias. Entre ellas hay avisos de seguridad conocidos para `System.Security.Cryptography.Pkcs` 9.0.4 (moderada y alta) y `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (alta), además de `NU1510`. Estas dependencias no fueron actualizadas como parte de la configuración de la batería.

## Evidencia

- informe principal: `artifacts/demo-battery-20261121033303-e0fa5a/report.md`;
- seguridad: `artifacts/demo-battery-20261121033303-e0fa5a/safety.json`;
- carga sostenida: `artifacts/demo-battery-20261121033303-e0fa5a/sustained-independent.json`;
- recuperación final: `artifacts/demo-battery-20261121033303-e0fa5a/recovery-independent-final.json`;
- métricas tabulares: `artifacts/demo-battery-20261121033303-e0fa5a/loads.csv`;
- resumen de la corrida principal: `artifacts/demo-battery-20261121033303-e0fa5a/summary.json`.
