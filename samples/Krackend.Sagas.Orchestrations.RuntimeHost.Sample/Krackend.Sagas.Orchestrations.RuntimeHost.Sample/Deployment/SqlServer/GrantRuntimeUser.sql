:setvar RuntimeUser "krackend_runtime"

IF DATABASE_PRINCIPAL_ID(N'$(RuntimeUser)') IS NULL
    THROW 51000, 'Create the contained database user before applying runtime grants.', 1;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[Runtime] TO [$(RuntimeUser)];
GO

-- sp_getapplock and sp_releaseapplock are executable by public by default. The
-- runtime needs no ALTER, CREATE, CONTROL, db_owner, or server-level permission.
