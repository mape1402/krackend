# Índice de actividad de estadísticas en SQL Server

El paquete `Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer`
ofrece un índice filtrado para las consultas de actividad de estadísticas. El host
lo habilita al configurar el modelo:

```csharp
modelBuilder.UseKrackendSqlServerOptimizations(options =>
    options.FilterStatisticsActivityIndex = true);
```

El índice incluye `Status` y contiene únicamente observaciones procesadas con
cambio de estado que no proceden del backfill. La opción está desactivada por
defecto y el host puede modificar el índice después de aplicar la extensión.
Los nombres de las columnas deben configurarse antes de llamar a la extensión.

Cambiar el modelo no modifica bases de datos existentes: cada host debe generar
y aplicar su propia migración. El host de ejemplo incluye una migración que crea
el índice nuevo antes de retirar el anterior y permite revertir el cambio. La
operación requiere espacio adicional temporal y bloqueos de esquema. Conviene
evaluar los planes de consulta y el costo de escritura con datos representativos
antes de aplicarla en producción; SQL Server decide si utiliza el índice.
