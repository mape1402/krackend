# Laboratorio de diez orquestaciones

Los demos de Sales, Inventories y Payments se ampliaron con entradas HTTP, consumers y persistencia. Email es un nuevo demo local. `Demo.RuntimeHost` carga diez artefactos distintos y ejecuta el motor, repositorios y Mule de Krackend. La solución es `DemoLab.slnx`.

## Arranque

Desde la raíz del repositorio, con PowerShell 7 y Docker Desktop para Linux:

```powershell
pwsh -File agents/stress/docker/Start-DemoLab.ps1 -Verify
```

El script prepara credenciales aleatorias en `docker/.env`, excluido de Git, construye las imágenes y arranca SQL Server, RabbitMQ, Mailpit y los cinco hosts. Conservar `.env` junto con los volúmenes: generar otra contraseña no cambia las credenciales de una base ya creada. No se borran volúmenes ni datos al arrancar.

| Componente | Dirección local |
|---|---|
| Runtime | http://localhost:5410 |
| Sales | http://localhost:5411 |
| Inventories | http://localhost:5412 |
| Payments | http://localhost:5413 |
| Email | http://localhost:5414 |
| Bandeja SMTP | http://localhost:18025 |
| RabbitMQ Management | http://localhost:15673 |
| SQL Server | localhost,15433 |

Los puertos se publican en `127.0.0.1`. SQL Server conserva cinco bases en un volumen: `SalesDemo`, `InventoriesDemo`, `PaymentsDemo`, `EmailDemo` y `KrackendRuntimeDemo`. Los hosts crean su esquema inicial con `EnsureCreated`; este arranque es para bases nuevas de laboratorio y no migra bases de otra versión.

La configuración de SQL Server y el healthcheck usan la [imagen y las herramientas documentadas por Microsoft](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker). Las etiquetas de imagen del laboratorio no son un despliegue de producción fijado por digest.

## Usar las entradas

Los diez POST y sus topics están en [el catálogo de casos](../../docs/demo-orchestration-use-cases-20261119.md). La misma ruta con `?via=consumer` persiste una publicación al consumer de entrada. Este registra la solicitud y emite el evento que inicia Krackend; no ejecuta toda la saga.

Ejemplo para `POST /sales/physical`, después de preparar stock en Inventories mediante `/demo/seed`:

```json
{
  "requestId": "run1-sale1-request",
  "testRunId": "run1",
  "saleId": "run1-sale1",
  "customerId": "customer1",
  "email": "customer@example.test",
  "total": 20,
  "lines": [{ "sku": "run1-sku", "quantity": 2, "unitPrice": 10 }]
}
```

Cada solicitud posterior de pago, cancelación, devolución o reenvío utiliza otro `RequestId`, aunque conserve `SaleId`. Repetir el mismo ID con distinto contenido se rechaza. La ruta de vencimiento recibe el identificador de venta/apartado representado por `SaleId`. HTTP 202 acredita aceptación durable, no finalización.

Consultas disponibles en cada servicio:

- `GET /demo/cases`.
- `POST /demo/seed`: fixtures idempotentes sin borrar datos.
- `GET /demo/requests/{requestId}`.
- `GET /demo/results/{testRunId}`: estados y registros persistidos.
- Runtime: `GET /demo/artifacts` y `GET /demo/instances/{requestId}`.

Los registros incluyen ventas, reservas, stock, balances de pago, movimientos/reembolsos en el diario, comunicaciones y solicitudes/intentos de Email. Cada mensaje usa scope y DbContext propios. La mutación de negocio y su acción durable de publicación se guardan en la misma transacción usando el modelo público de Mule 2.4.0. La activación sucede después del commit y el polling recupera señales omitidas. SMTP tiene un carril separado de publicaciones/callbacks.

El pago se simula con registros SQL. Email entrega al receptor SMTP local y solo acepta destinatarios `.test` o `localhost`. Un reinicio después de entregar SMTP pero antes de guardar el resultado puede repetir la entrega; la solicitud lógica y el encabezado identificador se conservan.

Las entradas antiguas siguen disponibles, incluido el topic histórico de captura terminado en punto. Los handlers anteriores ahora escriben en SQL; Inventories prepara `demo-sku` para requests antiguos sin líneas. `ConnectionStrings:Demo` es obligatorio y Compose lo configura explícitamente.

## Verificación

```powershell
dotnet restore agents/stress/DemoLab.slnx -p:MinVerSkip=true
dotnet build agents/stress/DemoLab.slnx --no-restore -m:1 -p:BuildInParallel=false -p:MinVerSkip=true
dotnet agents/stress/Demo.Verification/bin/Debug/net10.0/Demo.Verification.dll

# Con los contenedores disponibles: diez casos por HTTP y prueba de aislamiento.
pwsh -File agents/stress/docker/Verify-DemoLab.ps1 -PerCase 1 -CheckIsolation

# Diez casos a través de sus consumers reales.
pwsh -File agents/stress/docker/Verify-DemoLab.ps1 -PerCase 1 -ViaConsumers

# Cien entradas mezcladas: diez de cada artefacto.
pwsh -File agents/stress/docker/Verify-DemoLab.ps1 -PerCase 10 -ViaConsumers
```

La restauración necesita feeds accesibles. En esta sesión se usó el cache existente y `artifacts/mule-feed`, construido desde las fuentes previamente verificadas del tag Mule 2.4.0. Docker restaura desde sus feeds configurados y no incluye ese cache ni configuraciones personales.

El verificador local comprueba dominio, idempotencia, rollback, intención durable, concurrencia y mapeos DSL. Usa SQLite y genera el DDL de SQL Server sin conectarse. La ejecución del 20 de noviembre de 2026 aprobó **179 comprobaciones, cero fallos**, incluido el replay de un apartado previamente aceptado que ya venció y los dos órdenes de confirmación/cancelación de un pago tardío. El recorrido de negocio de los diez casos y sus mapeos se comprueban localmente; esto no equivale a ejecutar los contenedores.

### Batería completa y cargas de 100/200

```powershell
# Mostrar el orden y alcance sin ejecutar servicios.
pwsh -File agents/stress/docker/Run-DemoBattery.ps1 -PlanOnly -Suite Full -LoadSizes 100,200 -IncludeContainerRecovery

# Construir, arrancar el laboratorio y ejecutar toda la batería.
pwsh -File agents/stress/docker/Run-DemoBattery.ps1 -Suite Full -LoadSizes 100,200 -StartEnvironment -IncludeContainerRecovery
```

El controlador ejecuta controles HTTP/consumer, carga de 100 y después 200, aislamiento y pruebas negativas, cinco minutos de llegadas sostenidas y recuperación opcional de contenedores. Si falla una carga, las siguientes quedan sin ejecutar para no mezclar su trabajo pendiente con otra medición. Los reinicios se limitan a contenedores del proyecto local `krackend-demo-lab`, verificados por etiquetas.

Cada corrida genera `summary.json`, `report.md`, `report.html`, `loads.csv` y JSON por escenario bajo `artifacts/demo-battery-*`. Se registran aceptadas, completadas, fallidas, pendientes, throughput y latencias p50/p95/p99. Los reportes también se generan ante bloqueo o fallo: un dato ausente no se sustituye por un cero ficticio. Véanse [matriz y condiciones](../../docs/demo-battery-plan-20261120.md) y [resultado del intento de ejecución](../../docs/demo-battery-results-20261120.md).

Los endpoints de observación y repetición de callbacks solo se habilitan con `Demo:TestingEnabled`; Compose lo activa en el laboratorio. Las observaciones son metadata acotada en memoria y no ejecutan trabajo ni retienen transacciones. La repetición de callbacks crea publicaciones durables conservando dispatch e intento originales y exige el `TestRunId` correspondiente.

El verificador en vivo prepara los requisitos de cada caso antes del lote, espera sagas `Completed`, contrasta estados/importes/stock y Email entregado, y repite entradas para comprobar su identidad. `-CheckIsolation` retiene Email veinte segundos en una venta digital y exige que otra saga complete mientras la rama de acceso de la primera ya avanzó. Escribe el informe bajo `artifacts/` después de pasar las aserciones; los timeouts o resultados incorrectos terminan con error.

`rejectOperation` dirige un rechazo a un paso concreto. `delayOperation` y `delayMilliseconds` permiten retener un comando hasta 30000 ms, fuera de transacciones SQL. La resolución de pago frente a vencimiento usa un token de concurrencia en Sales. Los pasos reversibles tienen compensaciones; los flujos que requieren recuperación hacia adelante conservan su estado pendiente cuando falla otro paso.

## Estado de la validación

La solución completa `DemoLab.slnx` compiló con **cero errores** y una advertencia NU1510 de una referencia preexistente del runtime. Los scripts PowerShell pasaron el análisis sintáctico, Compose validó la configuración y `git diff --check` no encontró errores. Los archivos nuevos del laboratorio ya están incluidos en las excepciones de `.gitignore`; los binarios, caches y `.env` permanecen excluidos.

Evidencia local: `artifacts/demo-lab-final-build.log`, `artifacts/demo-lab-final-checks.log`, `artifacts/demo-domain-checks.json` y `artifacts/demo-service-schema.sql`.

La campaña en vivo sobre SQL Server, RabbitMQ y SMTP sigue pendiente. Docker Desktop está arriba según el usuario, pero `docker info` devuelve `permission denied` al abrir `npipe:////./pipe/dockerDesktopLinuxEngine` desde esta sesión. La petición de acceso ampliado fue rechazada automáticamente por la política `sandbox_approval: false`. Arrancar Docker no modifica ese permiso de la sesión. No se acredita ejecución en contenedores, capacidad entre réplicas ni miles de mensajes por minuto.
