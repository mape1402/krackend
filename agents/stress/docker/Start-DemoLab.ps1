param([switch]$Verify, [int]$PerCase = 1)
$ErrorActionPreference = 'Stop'
$demoRoot = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path (Split-Path $demoRoot -Parent) -Parent
docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Docker denied access or is not running. Start Docker Desktop and allow this session to access its daemon.' }
$demoEnvPath = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path -LiteralPath $demoEnvPath)) {
    $sqlSecret = 'Demo!' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    $rabbitSecret = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    @("DEMO_SQL_PASSWORD=$sqlSecret", "DEMO_RABBIT_PASSWORD=$rabbitSecret") | Set-Content -LiteralPath $demoEnvPath -Encoding utf8
}
docker compose --env-file $demoEnvPath -f (Join-Path $PSScriptRoot 'compose.yaml') config --quiet
if ($LASTEXITCODE -ne 0) { throw 'Invalid demo Compose configuration.' }
docker compose --env-file $demoEnvPath -f (Join-Path $PSScriptRoot 'compose.yaml') up -d --build
if ($LASTEXITCODE -ne 0) { throw 'The demo containers could not start.' }
if ($Verify) { & (Join-Path $PSScriptRoot 'Verify-DemoLab.ps1') -PerCase $PerCase -CheckIsolation }

