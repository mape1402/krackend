param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\..\Krackend.Sagas.Orchestrations.RuntimeHost.Sample.csproj"
$output = Join-Path $PSScriptRoot "KrackendRuntime.idempotent.sql"
$generated = Join-Path ([IO.Path]::GetTempPath()) ("KrackendRuntime-" + [guid]::NewGuid() + ".sql")

try {
    dotnet ef migrations script --idempotent --project $project --context RuntimeDbContext `
        --configuration $Configuration --output $generated
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations script failed." }

    $sessionOptions = @"
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

"@
    Set-Content -LiteralPath $output -Value ($sessionOptions + (Get-Content -LiteralPath $generated -Raw)) -NoNewline
}
finally {
    if (Test-Path -LiteralPath $generated) { Remove-Item -LiteralPath $generated -Force }
}

Write-Host "Generated $output"
