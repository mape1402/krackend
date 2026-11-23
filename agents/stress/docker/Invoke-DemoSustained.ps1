param(
    [ValidateRange(1,60)][int]$DurationMinutes=10,
    [ValidateRange(1,300)][int]$IntervalSeconds=20,
    [ValidateRange(1,60)][int]$MaxInstanceSeconds=10,
    [switch]$ViaConsumers,
    [string]$RunId,
    [string]$ReportPath
)

$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'DemoTest.Common.ps1')

if (-not $RunId) { $RunId=New-DemoRunId 'sustained' }
if (-not $ReportPath) { $ReportPath=Join-Path $script:DemoRepoRoot "artifacts/$RunId-results.json" }

$durationSeconds=$DurationMinutes*60
$planned=[math]::Floor($durationSeconds/$IntervalSeconds)
Assert-Demo ($planned -gt 0) 'The selected duration and interval produce no test instances.'

$scenarios=[Collections.Generic.List[object]]::new()
foreach ($index in 0..($planned-1)) {
    $caseId='UC'+(($index%10)+1).ToString('00')
    $scenario=New-DemoScenario $RunId $caseId $index.ToString('00')
    $scenario.scheduledOffsetSeconds=$index*$IntervalSeconds
    $scenario.sequence=$index+1
    $scenario.slaBreached=$false
    $scenario.queryErrors=0
    $scenarios.Add($scenario)
}

$terminal=@('Completed','CompletedWithErrors','Failed','Compensated','DeadLettered','Stopped','Aborted')
$active=[Collections.Generic.List[object]]::new()
$overlaps=[Collections.Generic.List[object]]::new()
$progress=[Collections.Generic.List[object]]::new()
$clock=$null
$preparationStartedOnUtc=[DateTime]::UtcNow
$fatalError=$null

try {
    Wait-DemoHealth
    foreach ($scenario in $scenarios) { Initialize-DemoScenario $scenario }

    Write-Output "READY run=$RunId planned=$planned interval=${IntervalSeconds}s max=${MaxInstanceSeconds}s"
    $clock=[Diagnostics.Stopwatch]::StartNew()
    $nextDispatch=0
    $lastProgressSecond=-1

    while ($clock.Elapsed.TotalSeconds -lt $durationSeconds) {
        $elapsed=$clock.Elapsed.TotalSeconds

        if ($nextDispatch -lt $scenarios.Count -and $elapsed -ge $scenarios[$nextDispatch].scheduledOffsetSeconds) {
            $scenario=$scenarios[$nextDispatch]
            if ($active.Count -gt 0) {
                $overlaps.Add([ordered]@{
                    sequence=$scenario.sequence
                    dispatchedAtSeconds=$elapsed
                    stillActive=@($active | ForEach-Object { $_.Request.requestId })
                })
            }

            $scenario.dispatchedOnUtc=[DateTime]::UtcNow
            $scenario.dispatchOffsetSeconds=$elapsed
            try {
                $scenario.accepted=Start-DemoScenario $scenario -ViaConsumer:$ViaConsumers
                $scenario.acceptedOnUtc=[DateTime]::UtcNow
                if ($scenario.accepted.status -ne 'Accepted') {
                    $scenario.dispatchError='Input did not return durable acceptance.'
                } else {
                    $active.Add($scenario)
                }
            } catch {
                $scenario.dispatchError=$_.Exception.Message
            }
            Write-Output ("DISPATCH {0}/{1} {2} t={3:N2}s active-before={4}" -f $scenario.sequence,$planned,$scenario.CaseId,$elapsed,([math]::Max(0,$active.Count-1)))
            $nextDispatch++
        }

        foreach ($scenario in @($active.ToArray())) {
            $ageSeconds=([DateTime]::UtcNow-$scenario.dispatchedOnUtc).TotalSeconds
            try {
                $state=Get-DemoJson 'runtime' ('/demo/instances/'+$scenario.Request.requestId)
                $scenario.lastState=$state
                if (@($state.instances).Count -gt 1) {
                    $scenario.dispatchError='A durable input created duplicate instances.'
                    [void]$active.Remove($scenario)
                    continue
                }
                if (@($state.instances).Count -eq 1 -and $state.instances[0].status -in $terminal) {
                    $scenario.runtime=$state.instances[0]
                    $scenario.finishedOnUtc=[DateTime]::UtcNow
                    $scenario.observedEndToEndMs=($scenario.finishedOnUtc-$scenario.dispatchedOnUtc).TotalMilliseconds
                    $scenario.slaBreached=$scenario.observedEndToEndMs -gt ($MaxInstanceSeconds*1000)
                    [void]$active.Remove($scenario)
                    Write-Output ("COMPLETE {0}/{1} {2} status={3} elapsed={4:N0}ms" -f $scenario.sequence,$planned,$scenario.CaseId,$scenario.runtime.status,$scenario.observedEndToEndMs)
                    continue
                }
            } catch {
                $scenario.queryErrors++
                $scenario.lastQueryError=$_.Exception.Message
            }

            if (-not $scenario.slaBreached -and $ageSeconds -gt $MaxInstanceSeconds) {
                $scenario.slaBreached=$true
                Write-Output ("SLA-BREACH {0}/{1} {2} elapsed>{3}s" -f $scenario.sequence,$planned,$scenario.CaseId,$MaxInstanceSeconds)
            }
        }

        $wholeSecond=[math]::Floor($elapsed)
        if ($wholeSecond -ne $lastProgressSecond) {
            $lastProgressSecond=$wholeSecond
            $progress.Add([ordered]@{
                elapsedSeconds=$elapsed
                dispatched=$nextDispatch
                completed=@($scenarios | Where-Object { $null -ne $_.runtime }).Count
                active=$active.Count
                slaBreaches=@($scenarios | Where-Object slaBreached).Count
                overlaps=$overlaps.Count
            })
        }
        Start-Sleep -Milliseconds 250
    }
} catch {
    $fatalError=$_.Exception.Message
}

$finishedOnUtc=[DateTime]::UtcNow
$cases=@($scenarios | ForEach-Object {
    $runtime=$_.runtime
    [ordered]@{
        sequence=$_.sequence
        caseId=$_.CaseId
        requestId=$_.Request.requestId
        scheduledOffsetSeconds=$_.scheduledOffsetSeconds
        dispatchOffsetSeconds=$_.dispatchOffsetSeconds
        cadenceDelayMs=if ($null -ne $_.dispatchOffsetSeconds) {($_.dispatchOffsetSeconds-$_.scheduledOffsetSeconds)*1000} else {$null}
        accepted=$null -ne $_.accepted
        status=if ($runtime) {$runtime.status} elseif ($_.dispatchError) {'DispatchError'} else {'Pending'}
        observedEndToEndMs=$_.observedEndToEndMs
        runtimeDurationMs=if ($runtime) {([datetimeoffset]$runtime.lastUpdatedOnUtc-[datetimeoffset]$runtime.startedOnUtc).TotalMilliseconds} else {$null}
        slaBreached=[bool]$_.slaBreached
        queryErrors=$_.queryErrors
        dispatchError=$_.dispatchError
        dispatchedOnUtc=$_.dispatchedOnUtc
        finishedOnUtc=$_.finishedOnUtc
        instance=$runtime
    }
})

$completed=@($cases | Where-Object status -eq 'Completed').Count
$slaBreaches=@($cases | Where-Object slaBreached).Count
$pending=@($cases | Where-Object status -eq 'Pending').Count
$dispatchErrors=@($cases | Where-Object status -eq 'DispatchError').Count
$latencies=@($cases | Where-Object status -eq 'Completed' | ForEach-Object {[double]$_.observedEndToEndMs})
$runtimeLatencies=@($cases | Where-Object status -eq 'Completed' | ForEach-Object {[double]$_.runtimeDurationMs})
$passed=(-not $fatalError -and $completed -eq $planned -and $slaBreaches -eq 0 -and $overlaps.Count -eq 0 -and $dispatchErrors -eq 0 -and $pending -eq 0)
$failureMessage=if ($fatalError) {$fatalError} elseif (-not $passed) {"Sustained test failed: completed=$completed/$planned, SLA breaches=$slaBreaches, overlaps=$($overlaps.Count), pending=$pending, dispatch errors=$dispatchErrors."} else {$null}

$snapshot=$null
try { $snapshot=Get-DemoSnapshot $RunId } catch { if (-not $failureMessage) { $failureMessage=$_.Exception.Message;$passed=$false } }

$report=[ordered]@{
    testRunId=$RunId
    status=if ($passed) {'Passed'} else {'Failed'}
    error=$failureMessage
    durationMinutes=$DurationMinutes
    intervalSeconds=$IntervalSeconds
    maxInstanceSeconds=$MaxInstanceSeconds
    planned=$planned
    dispatched=@($cases | Where-Object { $null -ne $_.dispatchOffsetSeconds }).Count
    accepted=@($cases | Where-Object accepted).Count
    completed=$completed
    pending=$pending
    dispatchErrors=$dispatchErrors
    slaBreaches=$slaBreaches
    overlaps=$overlaps.Count
    latency=Get-DemoPercentiles $latencies
    runtimeLatency=Get-DemoPercentiles $runtimeLatencies
    viaConsumers=[bool]$ViaConsumers
    preparationStartedOnUtc=$preparationStartedOnUtc
    testFinishedOnUtc=$finishedOnUtc
    cases=$cases
    overlapEvents=$overlaps.ToArray()
    progress=$progress.ToArray()
    databaseEvidence=$snapshot
}

Write-DemoJsonReport $ReportPath $report
Write-Output "Sustained report: $ReportPath"
if (-not $passed) { throw $failureMessage }
