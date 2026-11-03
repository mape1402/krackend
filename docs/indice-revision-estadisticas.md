# Índice de revisión para el backfill de estadísticas

El host de ejemplo de SQL Server configura el índice de
`OrchestrationInstances.StatisticsRevision` con el filtro
`StatisticsRevision = 0`. La consulta de backfill usa esa condición; las
revisiones positivas no necesitan entradas en ese índice. El cambio no altera
la revisión de concurrencia ni elimina las observaciones de estadísticas.

La migración `20261102060000_FilterStatisticsBackfillIndex` crea el índice
filtrado, retira el completo y renombra el nuevo. Su reversión restaura el
índice completo. Ambas operaciones pueden requerir espacio adicional y bloqueos
de esquema mientras se construye el reemplazo. Validar tiempos y espacio antes
de aplicarla en una instalación existente.

La configuración del modelo se ofrece en el paquete opcional
`Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer`
mediante `UseKrackendSqlServerOptimizations`. El host puede desactivar este
índice con `FilterStatisticsBackfillIndex = false` o sobrescribir la
configuración de EF después de aplicar los valores recomendados. Si cambia el
nombre de la columna, debe hacerlo antes de llamar la extensión.

El host de ejemplo aplica esa configuración tanto en runtime como al generar
migraciones. La migración pertenece al host: cada instalación debe definir y
probar la suya. El núcleo y el adaptador EF común no seleccionan un motor ni
reciben esta configuración.
