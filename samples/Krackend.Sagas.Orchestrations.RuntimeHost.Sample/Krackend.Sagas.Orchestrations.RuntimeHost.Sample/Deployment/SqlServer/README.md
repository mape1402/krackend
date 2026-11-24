# SQL Server runtime deployment

`KrackendRuntime.idempotent.sql` is generated from the host-owned Entity Framework
migrations and includes the physical model supplied by
`Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer`.

Run the schema script once with a DBA deployment identity. Then create a contained
runtime user and run `GrantRuntimeUser.sql` with `RuntimeUser` set to that user.
The application identity only needs DML on the `Runtime` schema; it does not need
DDL or database-owner permissions.

Regenerate the idempotent script (including the session options required by
filtered indexes) from this directory:

```powershell
./Generate.ps1
```

Production should leave `Runtime:Storage:ApplyMigrationsOnStartup` disabled. The
sample enables it only in `appsettings.Development.json` for local development.
