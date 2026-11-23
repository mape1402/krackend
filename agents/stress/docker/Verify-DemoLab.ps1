param(
    [ValidateRange(1,20)][int]$PerCase=1,
    [ValidateRange(5,180)][int]$CompletionTimeoutSeconds=120,
    [switch]$CheckIsolation,
    [switch]$ViaConsumers
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-DemoLoad.ps1') -PerCase $PerCase -CompletionTimeoutSeconds $CompletionTimeoutSeconds -ViaConsumers:$ViaConsumers
if ($CheckIsolation) { & (Join-Path $PSScriptRoot 'Invoke-DemoSafety.ps1') -OnlyIsolation }

