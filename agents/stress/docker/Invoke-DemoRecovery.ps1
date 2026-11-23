param([string]$ReportPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'DemoTest.Common.ps1')
$runId=New-DemoRunId 'recovery'
if (-not $ReportPath) { $ReportPath=Join-Path $script:DemoRepoRoot "artifacts/$runId-results.json" }
$results=[Collections.Generic.List[object]]::new()
$envPath=Join-Path $PSScriptRoot '.env'
$composePath=Join-Path $PSScriptRoot 'compose.yaml'
function Invoke-LabCompose([ValidateSet('start','stop','restart')][string]$Action,[ValidateSet('sales','email','runtime','rabbit')][string]$Service) {
    $containerIds=@(docker compose --project-name krackend-demo-lab --env-file $envPath -f $composePath ps -a -q $Service)
    Assert-Demo ($LASTEXITCODE -eq 0 -and $containerIds.Count -eq 1) "Expected exactly one lab container for $Service."
    $label=docker inspect --format '{{ index .Config.Labels "com.docker.compose.project" }}|{{ index .Config.Labels "com.docker.compose.service" }}' $containerIds[0]
    Assert-Demo ($LASTEXITCODE -eq 0 -and $label -eq "krackend-demo-lab|$Service") 'Refusing to disrupt a container outside this lab.'
    docker compose --project-name krackend-demo-lab --env-file $envPath -f $composePath $Action $Service | Out-Host
    Assert-Demo ($LASTEXITCODE -eq 0) "Container $Action failed: $Service."
}
function Wait-LabContainerHealthy([ValidateSet('sales','email','runtime','rabbit')][string]$Service,[int]$TimeoutSeconds=90) {
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $containerIds=@(docker compose --project-name krackend-demo-lab --env-file $envPath -f $composePath ps -a -q $Service)
        if ($LASTEXITCODE -eq 0 -and $containerIds.Count -eq 1) {
            $status=docker inspect --format '{{ if .State.Health }}{{ .State.Health.Status }}{{ else }}{{ .State.Status }}{{ end }}' $containerIds[0]
            if ($LASTEXITCODE -eq 0 -and $status -in @('healthy','running')) { return }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Container did not become healthy within $TimeoutSeconds seconds: $Service."
}
function Invoke-RecoveryCase([string]$Name,[scriptblock]$Body) {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    try { $evidence=& $Body; $results.Add(@{name=$Name;status='Passed';elapsedMs=$clock.Elapsed.TotalMilliseconds;evidence=$evidence}) }
    catch { $results.Add(@{name=$Name;status='Failed';elapsedMs=$clock.Elapsed.TotalMilliseconds;error=$_.Exception.Message}) }
}
try {
    Assert-Demo (Test-Path -LiteralPath $envPath) 'Start the named lab before recovery tests.'
    $endpoint=docker context inspect --format '{{ .Endpoints.docker.Host }}'
    Assert-Demo ($LASTEXITCODE -eq 0 -and $endpoint -match '^(npipe://|unix://|tcp://(localhost|127\.0\.0\.1)(:|/))') 'Recovery tests require a local Docker endpoint.'
    Wait-DemoHealth
    foreach ($service in @('email','runtime')) {
        Invoke-RecoveryCase ("Restart "+$service+" while waiting for an external callback") {
            $run=New-DemoRunId ('restart-'+$service)
            $case=New-DemoScenario $run 'UC02'
            $case.Request.delayOperation='email.send';$case.Request.delayMilliseconds=10000
            Start-DemoScenario $case | Out-Null
            Wait-DemoObservation 'email' $case.Request.requestId 'email.send' | Out-Null
            Invoke-LabCompose 'restart' $service
            Wait-DemoHealth 90
            Wait-DemoInstance $case.Request.requestId 90 | Out-Null
            $snapshot=Get-DemoSnapshot $run
            Assert-Demo ($snapshot.payments.payments[0].captured -eq 20 -and $snapshot.payments.payments[0].refunded -eq 0) 'Restart duplicated or reversed the charge.'
            Assert-Demo (@($snapshot.email.emails).Count -eq 1 -and $snapshot.email.emails[0].status -eq 'Delivered') 'Restart lost or duplicated the logical Email.'
            @{testRunId=$run;restartedService=$service}
        }
    }
    Invoke-RecoveryCase 'Accept while broker is unavailable and recover after producer restart' {
        $run=New-DemoRunId 'broker'
        $case=New-DemoScenario $run 'UC02'
        $original=$null
        try {
            Invoke-LabCompose 'stop' 'rabbit'
            $clock=[Diagnostics.Stopwatch]::StartNew()
            $accepted=Start-DemoScenario $case
            $acceptanceMs=$clock.Elapsed.TotalMilliseconds
            Assert-Demo ($accepted.status -eq 'Accepted' -and $acceptanceMs -lt 5000) 'Durable intake waited for the broker.'
            $rows=@(Get-DemoJson 'sales' ('/demo/testing/outbox/'+$case.Request.requestId))
            Assert-Demo ($rows.Count -eq 1) 'The accepted trigger has no durable intent.'
            $original=$rows[0]
            Invoke-LabCompose 'restart' 'sales'
        } finally {
            Invoke-LabCompose 'start' 'rabbit'
            Wait-LabContainerHealthy 'rabbit' 90
            # A producer restarted while Rabbit was down can exit before the broker
            # accepts connections. Start it again only after Rabbit is healthy.
            Invoke-LabCompose 'start' 'sales'
        }
        Wait-DemoHealth 90
        Wait-DemoInstance $case.Request.requestId 90 | Out-Null
        $rows=@(Get-DemoJson 'sales' ('/demo/testing/outbox/'+$case.Request.requestId))
        Assert-Demo (@($rows | Where-Object id -eq $original.id).Count -eq 1) 'Recovery replaced the original durable trigger.'
        $snapshot=Get-DemoSnapshot $run
        Assert-Demo ($snapshot.payments.payments[0].captured -eq 20 -and @($snapshot.email.emails).Count -eq 1) 'Recovery duplicated business effects.'
        @{testRunId=$run;acceptanceMs=$acceptanceMs;originalActionId=$original.id}
    }
} catch { $results.Add(@{name='Recovery preflight';status='Blocked';error=$_.Exception.Message}) }
$failed=@($results | Where-Object status -ne 'Passed').Count
Write-DemoJsonReport $ReportPath @{testRunId=$runId;status=if ($failed) {'Failed'} else {'Passed'};cases=$results.ToArray();failed=$failed;observedOnUtc=[DateTime]::UtcNow}
Write-Output "Recovery report: $ReportPath"
if ($failed) { throw "$failed recovery scenarios failed or were blocked." }

