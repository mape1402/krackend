# Diez casos de uso para los servicios demo

Fecha del diseño: 19 de noviembre de 2026. Implementación local compilada y con 179 comprobaciones locales aprobadas el 20 de noviembre. La ejecución de contenedores está pendiente por permisos de la sesión. Véanse [arranque, contratos y estado de pruebas del laboratorio](../agents/stress/README-demo-lab.md).

## Objetivo y punto de partida

Ampliar los demos para ejecutar diez orquestaciones con entradas HTTP, eventos, comandos, callbacks y cambios de negocio verificables en SQL Server dentro de Docker. La prueba posterior debe recorrer los servicios y el transporte reales, además del motor de Krackend.

En esta copia están `agents/stress/Sales`, `Inventories` y `Payments`. Ventas expone `POST /sales` en varias versiones y publica el evento de inicio mediante Spider. Inventarios consume reservas y Pagos consume capturas. Los tres handlers revisados devuelven respuestas y escriben en consola; sus `AppDbContext` todavía no declaran entidades de estos casos. Email no se encontró dentro de `agents/stress`: antes de implementarlo se ubicará su demo correspondiente o se agregará el demo local con el contrato aquí definido.

Los diez casos tendrán claves de orquestación y eventos distintos. Las versiones 1.0–1.4 del `POST /sales` actual no cuentan como cinco casos diferentes. Las rutas y topics que siguen son contratos propuestos; se conservará la compatibilidad de las entradas existentes.

## Catálogo de entradas

Cada endpoint acepta un `RequestId` idempotente y registra su solicitud antes de producir el evento. Además, cada servicio tendrá un consumer de la solicitud equivalente para poder iniciar el mismo caso desde mensajería, usando el mismo handler y las mismas reglas de persistencia. El evento resultante dispara el artefacto de Krackend; el consumer de entrada no ejecuta el flujo completo.

Todos los mensajes nuevos se proponen en versión `1.0.0`. El método de estas entradas es `POST`.

| ID / artefacto | Caso | Servicio y endpoint propuesto | Consumer de entrada alternativo | Evento que inicia la saga |
|---|---|---|---|---|
| UC01 `demo.sale.standard` | Venta de producto físico | Sales: `/sales/physical` | `requests.sales.physical.create` | `events.sales.physical.requested` |
| UC02 `demo.sale.digital` | Venta de producto digital | Sales: `/sales/digital` | `requests.sales.digital.create` | `events.sales.digital.requested` |
| UC03 `demo.sale.reservation` | Apartado pendiente de pago | Sales: `/sales/reservations` | `requests.sales.reservation.create` | `events.sales.reservation.requested` |
| UC04 `demo.payment.confirmation` | Confirmación de pago de un apartado | Payments: `/payments/confirmations` | `requests.payments.confirmation.register` | `events.payments.confirmation.received` |
| UC05 `demo.sale.cancellation` | Cancelación antes de entrega | Sales: `/sales/{saleId}/cancellations` | `requests.sales.cancellation.create` | `events.sales.cancellation.requested` |
| UC06 `demo.sale.partial-return` | Devolución parcial | Sales: `/sales/{saleId}/returns` | `requests.sales.return.create` | `events.sales.return.requested` |
| UC07 `demo.sale.exchange` | Cambio de producto | Sales: `/sales/{saleId}/exchanges` | `requests.sales.exchange.create` | `events.sales.exchange.requested` |
| UC08 `demo.inventory.receipt` | Recepción de mercancía | Inventories: `/inventories/receipts` | `requests.inventories.receipt.register` | `events.inventories.receipt.requested` |
| UC09 `demo.reservation.expiration` | Vencimiento de un apartado | Sales: `/sales/reservations/{reservationId}/expirations` | `requests.sales.reservation.expire` | `events.sales.reservation.expiration-requested` |
| UC10 `demo.receipt.reissue` | Reenvío de comprobante | Sales: `/sales/{saleId}/receipt-deliveries` | `requests.sales.receipt-delivery.create` | `events.sales.receipt-delivery.requested` |

## Flujos y resultados esperados

### UC01. Venta de producto físico

- Entrada: cliente, líneas SKU/cantidad, importe y referencia de pago demo.
- Flujo: crear venta pendiente; reservar inventario; capturar pago; convertir reserva en salida de stock; confirmar venta; enviar comprobante por Email.
- Escrituras: venta y líneas, reserva y movimientos de stock, pago, mensaje de Email y su entrega.
- Fallo de negocio: sin stock se rechaza antes de cobrar; pago rechazado libera la reserva. Si ya se cobró y no puede completarse la venta, se registra el reembolso necesario y su seguimiento. Un fallo de Email conserva la venta confirmada y reintenta la notificación.
- Resultado: venta `Confirmed`, pago `Captured`, salida aplicada una vez y notificación registrada. La respuesta HTTP inicial solo acredita aceptación.

### UC02. Venta de producto digital

- Entrada: cliente, producto digital, importe y referencia de pago demo.
- Flujo: crear venta digital; capturar pago; habilitar en paralelo el acceso digital y el envío del comprobante de pago; reunir resultados y cerrar el flujo.
- Escrituras: venta, pago, acceso digital con referencia única y mensaje de Email. Este caso no reserva inventario físico.
- Fallo de negocio: pago rechazado impide emitir el acceso. Un fallo técnico posterior conserva la intención y recupera la emisión o la notificación sin cobrar otra vez.
- Resultado: un acceso y un cobro por operación. Una notificación lenta no retiene el avance de la rama de acceso digital.

### UC03. Apartado pendiente de pago

- Entrada: cliente, líneas, importe y fecha de vencimiento.
- Flujo: crear apartado; reservar stock y crear intención de pago en paralelo; reunir resultados; marcar `AwaitingPayment`; enviar instrucciones por Email.
- Escrituras: venta/apartado, reserva con vencimiento, intención de pago y notificación.
- Fallo de negocio: si no se puede reservar o crear la intención, se cancela el recurso que sí se obtuvo. El caso no simula una espera de minutos dentro de un consumer: termina al dejar preparado el apartado; UC04 o UC09 resuelve su continuación de negocio.
- Resultado: apartado pendiente, sin captura de dinero y con reserva identificable.

### UC04. Confirmación de pago de un apartado

- Entrada: identificador de notificación del proveedor demo, intención/apartado e importe.
- Flujo: persistir la confirmación; comprobar identidad e importe; reclamar condicionalmente la resolución del apartado; registrar pago confirmado; convertir la reserva en salida; confirmar la venta; notificar.
- Escrituras: notificación del proveedor, pago, movimiento de stock, estado de venta y Email.
- Fallo de negocio: una notificación repetida no cobra ni descuenta otra vez. Si UC09 ya ganó el vencimiento, se registra la devolución del pago tardío y no se reactiva silenciosamente el apartado.
- Resultado: pago aplicado una vez o devolución tardía explícita. El proveedor de pago es el demo, no una pasarela externa.

### UC05. Cancelación antes de entrega

- Entrada: venta existente y motivo.
- Flujo: validar y reclamar la cancelación; en paralelo liberar/reintegrar stock y anular la autorización o reembolsar el cobro según su estado; marcar venta cancelada; notificar.
- Escrituras: solicitud de cancelación, liberación o entrada de stock, anulación/reembolso, estado de venta y Email.
- Fallo: si una rama termina y otra falla, el caso permanece pendiente de resolver esa operación; repetirlo no repite la rama ya confirmada. No se reporta cancelación completada mientras queda una compensación requerida sin terminar.
- Resultado: venta `Cancelled` y saldo/stock coherentes con su estado anterior. No admite una venta entregada, que corresponde a UC06.

### UC06. Devolución parcial

- Entrada: venta entregada, líneas y cantidades devueltas.
- Flujo: validar lo vendido y lo ya devuelto; autorizar devolución; en paralelo reintegrar las unidades y emitir reembolso proporcional; registrar devolución completada; notificar.
- Escrituras: devolución y líneas, movimientos de entrada, reembolso y Email.
- Fallo de negocio: cantidades superiores a las disponibles para devolver se rechazan sin efectos. Si una rama ya tuvo efecto, se recupera la restante; no se intenta deshacer ciegamente un reembolso confirmado.
- Resultado: venta `PartiallyReturned` o `Returned`, con cantidades e importes acumulados correctos.

### UC07. Cambio de producto

- Entrada: venta original, línea devuelta, SKU nuevo y cantidad.
- Flujo: validar cambio; reservar producto nuevo; calcular diferencia; cobrar, reembolsar o saltar pago según sea positiva, negativa o cero; confirmar movimientos de entrada/salida; actualizar venta; notificar.
- Escrituras: solicitud de cambio, reserva nueva, movimientos de stock, ajuste de pago y vínculo entre líneas original/nueva.
- Fallo de negocio: falta de stock o rechazo del cargo adicional libera la reserva nueva y conserva la venta anterior. Los fallos técnicos después de un efecto confirmado conservan su estado para recuperar el resto.
- Resultado: cambio aplicado una vez. Se prueban las tres ramas de diferencia de precio.

### UC08. Recepción de mercancía

- Entrada: referencia de recepción y varias líneas SKU/cantidad.
- Flujo: registrar recepción; validar líneas; aplicar existencias y movimientos en una transacción local; registrar el resumen de recepción y enviar aviso por Email; cerrar el caso.
- Escrituras: cabecera/líneas de recepción, balances y movimientos de inventario, notificación.
- Fallo: referencia repetida devuelve la recepción existente sin sumar otra vez. Una línea inválida impide aplicar parcialmente el lote de esa transacción. Email puede recuperarse después de confirmar el stock.
- Resultado: stock incrementado exactamente por las cantidades aceptadas. Incluye una variante con varios SKU y otra con operaciones concurrentes sobre el mismo SKU.

### UC09. Vencimiento de un apartado

- Entrada: apartado y fecha observada. La ruta HTTP/consumer permite probarlo sin depender del reloj de un cliente ni mantener una llamada abierta. Un disparador programado puede emitir la misma solicitud durable.
- Flujo: comprobar vencimiento y reclamar condicionalmente el estado `Expiring`; liberar stock y cancelar intención de pago en paralelo; marcar `Expired`; notificar.
- Escrituras: resolución del apartado, liberación de reserva, cancelación de intención y Email.
- Carrera con UC04: ambas resoluciones compiten por el mismo estado de negocio mediante actualización condicional/versionada en Sales. Solo una gana. Si el pago externo llega tras el vencimiento se sigue la devolución explícita de UC04. Un nuevo ID de saga no autoriza a ignorar esta contención de la misma entidad.
- Resultado: vence una sola vez, sin liberar dos veces ni confirmar y vencer simultáneamente el mismo apartado.

### UC10. Reenvío de comprobante

- Entrada: venta, destinatario demo y nuevo `RequestId` de envío.
- Flujo: recuperar datos de venta y comprobante de pago; crear solicitud de Email; procesar entrega local; registrar comunicación en Sales.
- Escrituras: solicitud de envío, intentos de entrega y comunicación de venta. No modifica cobros ni stock.
- Fallo: un error temporal recupera el envío sin crear otra solicitud lógica. Repetir el mismo `RequestId` es idempotente; solicitar deliberadamente otro reenvío usa otro identificador y sí genera otro registro.
- Resultado: comprobante enviado al receptor local de pruebas y comunicación trazable. No se envían correos a destinatarios reales.

## Consumers de pasos a incorporar

Cada comando tiene su propio contrato y callback de éxito/rechazo. Un consumer atiende el paso de su servicio, confirma la transacción local y deja su respuesta durable; no espera la terminación de toda la saga.

| Servicio | Operaciones propuestas |
|---|---|
| Sales | crear venta física/digital, confirmar venta, habilitar acceso digital, preparar apartado, reclamar pago/cancelación/vencimiento, validar y aplicar devolución/cambio, obtener comprobante, registrar comunicación |
| Inventories | reservar, confirmar reserva, liberar reserva, reintegrar unidades, registrar/aplicar recepción y confirmar cambio de producto |
| Payments | crear/cancelar intención, capturar, registrar confirmación externa demo, anular autorización, reembolsar total/parcial, ajustar diferencia y obtener comprobante |
| Email | registrar y entregar comprobante, instrucciones de pago, cancelación, devolución, cambio, recepción de mercancía y vencimiento; registrar reenvío e intentos |

Los comandos nuevos usarán `commands.<servicio>.<operación>` y conservarán metadata de saga, task, dispatch e intento en la respuesta. El consumer actual de captura tiene un punto final en `commands.payments.payment.capture.`; al implementar se verificará el contrato efectivo de emisor/receptor y se mantendrá un alias compatible en lugar de renombrarlo unilateralmente.

## SQL Server en Docker

Se propone una instancia local de SQL Server en Docker, con volumen persistente y una base separada por servicio. La instancia compartida reduce requisitos del laboratorio; la aplicación no comparte DbContexts ni transacciones entre servicios.

| Base | Registros de negocio mínimos |
|---|---|
| `SalesDemo` | Sales, SaleLines, Requests, Returns, Exchanges, DigitalDeliveries, Communications |
| `InventoriesDemo` | StockBalances, Reservations, StockMovements, GoodsReceipts y líneas |
| `PaymentsDemo` | PaymentIntents, Payments, Refunds, ProviderNotifications |
| `EmailDemo` | EmailMessages, DeliveryAttempts |
| `KrackendRuntimeDemo` | Artefactos, instancias, pasos, dispatches y acciones durables del runtime |

Cada servicio conserva inbox/outbox o su mecanismo durable equivalente para hacer recuperable el evento/callback después del commit. Los registros de negocio y su intención de publicación se confirman de forma atómica en su base. No se adopta una cola global nueva para ejecutar secuencialmente todos los servicios.

Cada mensaje/solicitud usa su scope y DbContext. Las transacciones SQL son breves y no incluyen llamadas de red ni retrasos de prueba. Los balances y transiciones usan actualizaciones condicionales para impedir stock negativo, sobrerreembolsos o resoluciones incompatibles. La idempotencia usa claves de operación estables con índices únicos; no se confunde `SaleId` con la identidad de cada solicitud posterior.

El pago se simula persistiendo autorizaciones, capturas y reembolsos. Email persiste solicitudes/intentos y entrega a un receptor SMTP local de laboratorio. Así se ejercita transporte y almacenamiento sin pasarela de pago ni correos externos.

Todos los registros de prueba incluyen `TestRunId` y la identidad de operación. Se agregan consultas HTTP de estado por solicitud/venta/reserva/pago/Email y comprobaciones SQL para verificar resultados, además de consultar las instancias de Krackend. Los datos semilla son idempotentes y los escenarios de carga usan identidades y saldos preparados para que la falta de stock no falsee las mediciones.

## Condiciones de aceptación de la implementación posterior

1. Ejecutar una entrada de cada caso y comprobar su resultado y escrituras en los servicios correspondientes. Preparar ventas/apartados previos para los casos que los necesitan; no depender del orden accidental de los tests.
2. Repetir la misma entrada por HTTP y por consumer con el mismo `RequestId`: una sola operación de negocio y efectos idempotentes. HTTP 202/ack acredita recepción durable, no saga completada.
3. Ejecutar cien entradas mezcladas, diez por artefacto, y una llegada sostenida de 5–10 mensajes por minuto durante cinco minutos. Registrar recepción, commit de entrada, inicio, comandos, callbacks y finalización, además de las filas SQL resultantes.
4. Retener un paso de una saga antes de su efecto y demostrar progreso de otras sagas y otros casos. En UC02, UC03, UC05, UC06 y UC09 comprobar que las ramas elegibles empiezan antes de liberar la otra. Las dependencias de negocio de cada caso se conservan.
5. Inyectar rechazo funcional, demora de un paso, callback duplicado, redelivery después de commit y reinicio del servicio tras persistir su intención. Cada fallo se dirige a una operación identificada; no se introduce una pausa global.
6. Enfrentar UC04 y UC09 sobre el mismo apartado y verificar una resolución de negocio válida, más la devolución si corresponde. También probar concurrencia sobre el mismo SKU sin stock negativo y distinguirla del aislamiento entre entidades distintas.
7. Verificar cero cobros, movimientos, devoluciones o accesos duplicados. Un Email puede tener varios intentos; conserva una sola solicitud lógica. El resultado de negocio rechazado se cuenta por separado de un fallo técnico y de una saga exitosa.
8. Medir latencia desde la entrada hasta el resultado final y progreso durante toda la prueba. Comparar con un control del mismo caso y sus dependencias; no presentar mensajes aceptados ni ventanas de cinco minutos como tiempos de terminación aceptables.

Este documento conserva los diez casos y sus objetivos de aceptación. Los endpoints, consumers, modelos SQL y Compose están implementados en `agents/stress`; la validación efectiva y sus límites se registran en el README del laboratorio. El esquema inicial del laboratorio usa `EnsureCreated` sobre bases nuevas y no sustituye migraciones de producción.
