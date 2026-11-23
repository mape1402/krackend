param(
    [Alias('Profile')][ValidateSet('Smoke','Load','Full')][string]$Suite='Full',
    [ValidateSet(100,200)][int[]]$LoadSizes=@(100,200),
    [switch]$IncludeContainerRecovery,
    [switch]$StartEnvironment,
    [switch]$SkipBuild,
    [switch]$PlanOnly
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'DemoTest.Common.ps1')
$plan=[Collections.Generic.List[object]]::new()
$plan.Add(@{id='local';name='Domain, outbox and DSL checks';kind='Local';count=$null})
$plan.Add(@{id='smoke-http';name='Ten cases through HTTP';kind='Load';count=10;via=$false})
if ($Suite -ne 'Load') { $plan.Add(@{id='smoke-consumers';name='Ten cases through consumers';kind='Load';count=10;via=$true}) }
if ($Suite -ne 'Smoke') {
    foreach ($size in $LoadSizes) { $plan.Add(@{id="load-$size";name="$size mixed inputs through consumers";kind='Load';count=$size;via=$true}) }
}
if ($Suite -eq 'Smoke') { $plan.Add(@{id='isolation';name='Independent sagas and parallel branches';kind='Isolation';count=$null}) }
if ($Suite -eq 'Full') {
    $plan.Add(@{id='safety';name='Isolation, duplicates, rejections, compensation and races';kind='Safety';count=$null})
    $plan.Add(@{id='sustained';name='Ten arrivals per minute for five minutes';kind='Sustained';count=50})
}
if ($IncludeContainerRecovery) { $plan.Add(@{id='recovery';name='Email/runtime restart and broker/producer recovery';kind='Recovery';count=$null}) }
if ($PlanOnly) { $plan | Select-Object id,name,kind,count | Format-Table -AutoSize; return }

$batteryId=New-DemoRunId 'battery'
$reportDirectory=Join-Path $script:DemoRepoRoot "artifacts/$batteryId"
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$results=[Collections.Generic.List[object]]::new()
$blockedReason=$null;$previousFailure=$false
$startedOnUtc=[DateTime]::UtcNow
try {
    Push-Location $script:DemoRepoRoot
    try {
        if (-not $SkipBuild) {
            dotnet build agents/stress/DemoLab.slnx --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:MinVerSkip=true -v quiet *> (Join-Path $reportDirectory 'build.log')
            if ($LASTEXITCODE -ne 0) { throw 'DemoLab build failed. See build.log.' }
        }
        dotnet agents/stress/Demo.Verification/bin/Debug/net10.0/Demo.Verification.dll *> (Join-Path $reportDirectory 'local-checks.log')
        if ($LASTEXITCODE -ne 0) { throw 'Local checks failed. See local-checks.log.' }
        $local=Get-Content (Join-Path $script:DemoRepoRoot 'artifacts/demo-domain-checks.json') -Raw | ConvertFrom-Json
        $results.Add(@{id='local';name=$plan[0].name;status='Passed';checks=$local.passed;provider=$local.provider})
    } finally { Pop-Location }
} catch {
    $previousFailure=$true;$blockedReason=$_.Exception.Message
    $results.Add(@{id='local';name=$plan[0].name;status='Failed';error=$blockedReason})
}
if (-not $previousFailure) {
    try {
        if ($StartEnvironment -or $IncludeContainerRecovery) {
            $dockerResult=(docker info --format '{{.ServerVersion}}' 2>&1 | Out-String).Trim()
            if ($LASTEXITCODE -ne 0) { throw "Docker access unavailable: $dockerResult" }
            $endpoint=docker context inspect --format '{{ .Endpoints.docker.Host }}'
            if ($LASTEXITCODE -ne 0 -or $endpoint -notmatch '^(npipe://|unix://|tcp://(localhost|127\.0\.0\.1)(:|/))') { throw 'The battery only starts or disrupts a local Docker lab.' }
        }
        if ($StartEnvironment) { & (Join-Path $PSScriptRoot 'Start-DemoLab.ps1') }
        Wait-DemoHealth 30
        $results.Add(@{id='environment';name='SQL Server/RabbitMQ/SMTP live lab preflight';status='Passed'})
    } catch {
        $blockedReason=$_.Exception.Message
        $results.Add(@{id='environment';name='SQL Server/RabbitMQ/SMTP live lab preflight';status='Blocked';error=$blockedReason})
    }
}
foreach ($step in @($plan | Where-Object id -ne 'local')) {
    $stepReport=Join-Path $reportDirectory ($step.id+'.json')
    if ($blockedReason -or $previousFailure) {
        $results.Add(@{id=$step.id;name=$step.name;status=if ($blockedReason) {'Blocked'} else {'Skipped'};
            planned=$step.count;error=if ($blockedReason) {$blockedReason} else {'A prior case failed; do not mix unresolved work into the next load.'}})
        continue
    }
    $clock=[Diagnostics.Stopwatch]::StartNew()
    try {
        Write-Host ("Running "+$step.name)
        switch ($step.kind) {
            'Load' {
                $completionTimeout = if ($step.count -ge 200) { 600 } elseif ($step.count -ge 100) { 300 } else { 120 }
                & (Join-Path $PSScriptRoot 'Invoke-DemoLoad.ps1') -PerCase ([int]($step.count/10)) `
                    -CompletionTimeoutSeconds $completionTimeout -ViaConsumers:$step.via `
                    -RunId ($batteryId+'-'+$step.id) -ReportPath $stepReport
            }
            'Isolation' { & (Join-Path $PSScriptRoot 'Invoke-DemoSafety.ps1') -OnlyIsolation -ReportPath $stepReport }
            'Safety' { & (Join-Path $PSScriptRoot 'Invoke-DemoSafety.ps1') -ReportPath $stepReport }
            'Sustained' { & (Join-Path $PSScriptRoot 'Invoke-DemoSustained.ps1') -ReportPath $stepReport }
            'Recovery' { & (Join-Path $PSScriptRoot 'Invoke-DemoRecovery.ps1') -ReportPath $stepReport }
        }
        $detail=Get-Content $stepReport -Raw | ConvertFrom-Json
        Assert-Demo ($detail.status -eq 'Passed') 'Step produced an unsuccessful report.'
        $results.Add(@{id=$step.id;name=$step.name;status='Passed';elapsedMs=$clock.Elapsed.TotalMilliseconds;planned=$step.count;report=$stepReport;metrics=$detail})
    } catch {
        $detail=$null
        if (Test-Path -LiteralPath $stepReport) { $detail=Get-Content $stepReport -Raw | ConvertFrom-Json }
        $results.Add(@{id=$step.id;name=$step.name;status='Failed';elapsedMs=$clock.Elapsed.TotalMilliseconds;planned=$step.count;report=$stepReport;metrics=$detail;error=$_.Exception.Message})
        $previousFailure=$true
    }
}
$loads=@($results | Where-Object { $_.id -like 'load-*' } | ForEach-Object {
    $m=$_.metrics
    [ordered]@{load=$_.planned;status=$_.status;accepted=$m.accepted;completed=$m.completed;failed=$m.failed;pending=$m.pending;
        totalSeconds=if ($m.totalMs) {$m.totalMs/1000} else {$null};throughputSagasPerSecond=$m.throughputSagasPerSecond;
        p50Ms=$m.latency.p50Ms;p95Ms=$m.latency.p95Ms;p99Ms=$m.latency.p99Ms;maxMs=$m.latency.maxMs;
        runtimeP95Ms=$m.runtimeLatency.p95Ms;sqlVerified=$m.checkedPersistedBusinessState}
})
$status=if (@($results | Where-Object status -eq 'Failed').Count) {'Failed'} elseif (@($results | Where-Object status -eq 'Blocked').Count) {'Blocked'} else {'Passed'}
$summary=[ordered]@{batteryId=$batteryId;profile=$Suite;status=$status;startedOnUtc=$startedOnUtc;finishedOnUtc=[DateTime]::UtcNow;
    loadOrder=$LoadSizes;liveProvidersExpected=@('SQL Server in Docker','RabbitMQ','local SMTP capture');results=$results.ToArray();loads=$loads}
Write-DemoJsonReport (Join-Path $reportDirectory 'summary.json') $summary
if ($loads.Count) { $loads | ForEach-Object {[pscustomobject]$_} | Export-Csv -LiteralPath (Join-Path $reportDirectory 'loads.csv') -NoTypeInformation -Encoding utf8 }
$markdown=[Collections.Generic.List[string]]::new()
$markdown.Add('# Batería de pruebas de Krackend')
$markdown.Add('')
$markdown.Add("Estado: **$status**. Corrida: $batteryId.")
$markdown.Add('')
$markdown.Add('| Carga | Estado | Aceptadas | Completadas | Fallidas | Pendientes | Duración (s) | Sagas/s | p50 (ms) | p95 (ms) | p99 (ms) | Máxima (ms) |')
$markdown.Add('|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach ($row in $loads) {
    $values=@($row.load,$row.status,$row.accepted,$row.completed,$row.failed,$row.pending,$row.totalSeconds,$row.throughputSagasPerSecond,$row.p50Ms,$row.p95Ms,$row.p99Ms,$row.maxMs)
    $markdown.Add('| '+(($values | ForEach-Object {if ($null -eq $_) {'—'} elseif ($_ -is [double]) {$_.ToString('0.00',[Globalization.CultureInfo]::InvariantCulture)} else {"$_"}}) -join ' | ')+' |')
}
$markdown.Add('')
$markdown.Add('La latencia observada incluye recepción y espera de cola; la duración del runtime excluye la espera anterior al inicio. Los percentiles usan completadas exitosas y rango más próximo. Un estado bloqueado no es una prueba ejecutada ni un throughput de cero.')
$markdown.Add('')
$markdown.Add('| Escenario | Resultado | Detalle |')
$markdown.Add('|---|---|---|')
foreach ($result in $results) { $message=("$($result.error)" -replace '\|','/' -replace '[\r\n]+',' ');$markdown.Add("| $($result.name) | $($result.status) | $message |") }
$markdown.Add('')
$markdown.Add('Los JSON por escenario conservan IDs, resultados por saga y evidencia de base de datos. No se borran datos de pruebas ni volúmenes.')
$markdown | Set-Content -LiteralPath (Join-Path $reportDirectory 'report.md') -Encoding utf8
$htmlRows=@($loads | ForEach-Object {[pscustomobject]$_})
$html=($htmlRows | ConvertTo-Html -Title 'Krackend: cargas de 100 y 200' -PreContent ('<h1>Krackend — '+[Net.WebUtility]::HtmlEncode($status)+'</h1>') -PostContent '<p>Los datos ausentes corresponden a cargas no ejecutadas; consultar summary.json y report.md.</p>')
$html | Set-Content -LiteralPath (Join-Path $reportDirectory 'report.html') -Encoding utf8
Write-Output ("Battery report: "+(Join-Path $reportDirectory 'report.md'))
if ($status -ne 'Passed') { throw "Battery ended as $status. See the report for observed results and unexecuted cases." }

