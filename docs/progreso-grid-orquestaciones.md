# Progreso del grid de orquestaciones

Las instancias nuevas guardan la versión de su artifact, el número de tareas
habilitadas y el número de tareas completadas u omitidas. Las tareas y etapas
deshabilitadas no se incluyen. El grid lee estos campos de la instancia sin
consultar el artifact de cada fila.

El modelo agrega `OrchestrationVersion`, `TotalTaskCount` y
`CompletedTaskCount`. La migración del host de ejemplo SQL Server los agrega
como campos anulables; cada host debe preparar la evolución correspondiente a
su proveedor antes de ejecutar el nuevo runtime. El núcleo y el adaptador EF
común no seleccionan el proveedor.

Las instancias históricas sin proyección muestran versión y progreso
desconocidos. Esta entrega no reconstruye esos datos automáticamente. No se
trata una saga fallida como completada ni se inventan contadores históricos.

Para evitar proyecciones obsoletas, no se deben mezclar escritores anteriores
que no actualizan estos campos con los nuevos para una misma instancia. Antes
de cambiar escritores, aplicar el esquema compatible y drenar los anteriores.
Si se vuelve a usar un escritor antiguo, sus modificaciones pueden dejar una
proyección ya poblada desactualizada y requieren reconciliar esos datos antes de
confiar nuevamente en ellos.
