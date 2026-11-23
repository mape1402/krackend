param(
    [ValidateSet(1,2,5,10,20,50,100)][int]$PerCase=1,
    [ValidateRange(5,900)][int]$CompletionTimeoutSeconds=120,
    [ValidateRange(1,900)][int]$MaxInstanceLatencySeconds=10,
    [switch]$ViaConsumers,
    [string]$RunId,
    [string]$ReportPath
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'DemoTest.Common.ps1')
if (-not $RunId) { $RunId=New-DemoRunId 'load' }
if (-not $ReportPath) { $ReportPath=Join-Path $script:DemoRepoRoot "artifacts/$RunId-results.json" }
$scenarios=[Collections.Generic.List[object]]::new()
$failure=$null;$liveStarted=$false;$sqlVerified=$false;$snapshot=$null;$acceptanceMs=$null;$totalMs=$null
$batchClock=$null
$progress=[Collections.Generic.List[object]]::new()
try {
    Wait-DemoHealth
    $catalog=@(Get-DemoJson 'runtime' '/demo/artifacts')
    Assert-Demo ($catalog.Count -eq 10) 'Expected ten orchestration artifacts.'
    foreach ($caseId in 1..10 | ForEach-Object { 'UC'+$_.ToString('00') }) {
        for ($index=0;$index -lt $PerCase;$index++) {
            $scenario=New-DemoScenario $RunId $caseId "$index"
            Initialize-DemoScenario $scenario
            $scenarios.Add($scenario)
        }
    }
    $liveStarted=$true
    $batchClock=[Diagnostics.Stopwatch]::StartNew()
    $acceptances=@(Start-DemoScenariosParallel $scenarios.ToArray() -ViaConsumer:$ViaConsumers)
    foreach ($result in $acceptances) {
        $scenario=$scenarios[$result.Index]
        $scenario.startedOnUtc=$result.StartedOnUtc
        $scenario.accepted=$result.Accepted
        $scenario.acceptedOnUtc=$result.AcceptedOnUtc
        Assert-Demo ($scenario.accepted.status -eq 'Accepted') 'Input did not return durable acceptance.'
    }
    $acceptanceMs=$batchClock.Elapsed.TotalMilliseconds
    $pending=[Collections.Generic.List[object]]::new()
    $pending.AddRange([object[]]$scenarios.ToArray())
    $terminal=@('Completed','CompletedWithErrors','Failed','Compensated','DeadLettered','Stopped','Aborted')
    while ($pending.Count -gt 0 -and $batchClock.Elapsed.TotalSeconds -lt $CompletionTimeoutSeconds) {
        $rows=@(Get-DemoJson 'runtime' ('/demo/runs/'+[Uri]::EscapeDataString($RunId)+'/instances'))
        $rowsByCorrelation=@{}
        foreach ($row in $rows) {
            Assert-Demo (-not $rowsByCorrelation.ContainsKey($row.correlationId)) 'A durable input created duplicate instances.'
            $rowsByCorrelation[$row.correlationId]=$row
        }
        foreach ($scenario in @($pending.ToArray())) {
            $row=$rowsByCorrelation[$scenario.Request.requestId]
            $scenario.lastState=[ordered]@{instances=if ($null -ne $row) {@($row)} else {@()};actions=@()}
            if ($null -ne $row -and $row.status -in $terminal) {
                $scenario.runtime=$row
                $scenario.finishedOnUtc=[DateTime]::UtcNow
                [void]$pending.Remove($scenario)
            }
        }
        $progress.Add(@{elapsedMs=$batchClock.Elapsed.TotalMilliseconds;accepted=$scenarios.Count;terminal=$scenarios.Count-$pending.Count;pending=$pending.Count})
        # Completion is read from persisted timestamps, so 250 ms observation does
        # not change latency results. Avoid turning the load harness itself into a
        # 10 Hz status-query competitor for the runtime database.
        if ($pending.Count -gt 0) { Start-Sleep -Milliseconds 250 }
    }
    $totalMs=$batchClock.Elapsed.TotalMilliseconds
    $snapshot=Get-DemoSnapshot $RunId
    Assert-Demo ($pending.Count -eq 0) "$($pending.Count) accepted inputs did not finish within $CompletionTimeoutSeconds seconds."
    Assert-Demo (@($scenarios | Where-Object { $_.runtime.status -ne 'Completed' }).Count -eq 0) 'Some sagas ended unsuccessfully.'
    $latencyBreaches=@($scenarios | Where-Object {
        ([DateTime]::SpecifyKind([datetime]$_.runtime.lastUpdatedOnUtc,[DateTimeKind]::Utc)-
            [datetime]$_.startedOnUtc).TotalSeconds -gt $MaxInstanceLatencySeconds -or
        (([datetimeoffset]$_.runtime.lastUpdatedOnUtc-[datetimeoffset]$_.runtime.startedOnUtc).TotalSeconds -gt $MaxInstanceLatencySeconds)
    })
    Assert-Demo ($latencyBreaches.Count -eq 0) "$($latencyBreaches.Count) instances exceeded the $MaxInstanceLatencySeconds-second latency limit."
    Assert-Demo (@($snapshot.email.emails).Count -eq $scenarios.Count) 'Expected one logical Email per completed case.'
    Assert-Demo (@($snapshot.email.emails | Where-Object status -ne 'Delivered').Count -eq 0) 'Some Emails were accepted but not delivered.'
    foreach ($scenario in $scenarios) {
        $sale=@($snapshot.sales.sales | Where-Object id -eq $scenario.Request.saleId)
        $expectedStatus=switch ($scenario.CaseId) { 'UC03' {'AwaitingPayment'}; 'UC05' {'Cancelled'}; 'UC06' {'PartiallyReturned'}; 'UC09' {'Expired'}; default {'Confirmed'} }
        if ($scenario.CaseId -ne 'UC08') { Assert-Demo ($sale.Count -eq 1 -and $sale[0].status -eq $expectedStatus) "Invalid sale outcome: $($scenario.CaseId)." }
        $payment=@($snapshot.payments.payments | Where-Object id -eq $scenario.Request.saleId)
        $expectedPayment=switch ($scenario.CaseId) {
            'UC03' {@(0,0)}; 'UC05' {@(20,20)}; 'UC06' {@(20,10)}; 'UC07' {@(25,0)}; 'UC09' {@(0,0)}; 'UC08' {@()}; default {@(20,0)}
        }
        if ($scenario.CaseId -ne 'UC08') {
            Assert-Demo ($payment.Count -eq 1 -and $payment[0].captured -eq $expectedPayment[0] -and $payment[0].refunded -eq $expectedPayment[1]) "Invalid payment outcome: $($scenario.CaseId)."
        }
        if ($scenario.CaseId -ne 'UC02') {
            $stock=@($snapshot.inventories.stock | Where-Object id -eq $scenario.Sku)
            $expectedStock=switch ($scenario.CaseId) { 'UC01' {998}; 'UC04' {998}; 'UC06' {999}; 'UC07' {999}; 'UC08' {1002}; default {1000} }
            $expectedReserved=if ($scenario.CaseId -eq 'UC03') {2} else {0}
            Assert-Demo ($stock.Count -eq 1 -and $stock[0].onHand -eq $expectedStock -and $stock[0].reserved -eq $expectedReserved) "Invalid stock outcome: $($scenario.CaseId)."
        }
        if ($scenario.CaseId -eq 'UC07') {
            $newStock=@($snapshot.inventories.stock | Where-Object id -eq $scenario.Request.replacementLines[0].sku)
            Assert-Demo ($newStock.Count -eq 1 -and $newStock[0].onHand -eq 999 -and $newStock[0].reserved -eq 0) 'Replacement stock was not applied once.'
        }
    }
    $beforeReplay=Get-DemoFingerprint $snapshot
    foreach ($scenario in $scenarios) {
        $repeated=Start-DemoScenario $scenario -ViaConsumer:$ViaConsumers
        Assert-Demo ($repeated.operationId -eq $scenario.accepted.operationId) 'A duplicate input changed its identity.'
    }
    $snapshot=Get-DemoSnapshot $RunId
    Assert-Demo ((Get-DemoFingerprint $snapshot) -eq $beforeReplay) 'A duplicate input changed business records.'
    $sqlVerified=$true
} catch { $failure=$_.Exception.Message }
finally {
    if ($null -ne $batchClock -and $null -eq $totalMs) { $totalMs=$batchClock.Elapsed.TotalMilliseconds }
    if ($liveStarted -and $null -eq $snapshot) { try { $snapshot=Get-DemoSnapshot $RunId } catch { } }
    $cases=@($scenarios | ForEach-Object {
        $runtime=$_.runtime
        [ordered]@{caseId=$_.CaseId;requestId=$_.Request.requestId;accepted=($null -ne $_.accepted);
            status=if ($runtime) {$runtime.status} else {'Pending'};
            acceptanceMs=if ($_.acceptedOnUtc) {($_.acceptedOnUtc-$_.startedOnUtc).TotalMilliseconds} else {$null};
            actualEndToEndMs=if ($runtime) {([DateTime]::SpecifyKind([datetime]$runtime.lastUpdatedOnUtc,[DateTimeKind]::Utc)-[datetime]$_.startedOnUtc).TotalMilliseconds} else {$null};
            observedEndToEndMs=if ($_.finishedOnUtc) {($_.finishedOnUtc-$_.startedOnUtc).TotalMilliseconds} else {$null};
            runtimeDurationMs=if ($runtime) {([datetimeoffset]$runtime.lastUpdatedOnUtc-[datetimeoffset]$runtime.startedOnUtc).TotalMilliseconds} else {$null};
            startedOnUtc=$_.startedOnUtc;finishedOnUtc=$_.finishedOnUtc;instance=$runtime;runtimeActions=$_.lastState.actions}
    })
    $accepted=@($cases | Where-Object accepted).Count
    $completed=@($cases | Where-Object status -eq 'Completed').Count
    $failed=@($cases | Where-Object { $_.status -notin @('Pending','Completed') }).Count
    $latencies=@($cases | Where-Object status -eq 'Completed' | ForEach-Object {[double]$_.actualEndToEndMs})
    $runtimeLatencies=@($cases | Where-Object status -eq 'Completed' | ForEach-Object {[double]$_.runtimeDurationMs})
    $report=[ordered]@{testRunId=$RunId;status=if ($failure) {if ($liveStarted) {'Failed'} else {'Blocked'}} else {'Passed'};
        error=$failure;liveTestStarted=$liveStarted;planned=10*$PerCase;accepted=$accepted;completed=$completed;failed=$failed;
        pending=[math]::Max(0,$accepted-$completed-$failed);notAccepted=10*$PerCase-$accepted;
        acceptanceMs=$acceptanceMs;totalMs=$totalMs;throughputSagasPerSecond=if ($totalMs -gt 0) {$completed/($totalMs/1000)} else {$null};
        intakeMessagesPerSecond=if ($acceptanceMs -gt 0) {$accepted/($acceptanceMs/1000)} else {$null};
        acceptanceLatency=Get-DemoPercentiles @($cases | Where-Object accepted | ForEach-Object {[double]$_.acceptanceMs});
        latency=Get-DemoPercentiles $latencies;runtimeLatency=Get-DemoPercentiles $runtimeLatencies;
        viaConsumers=[bool]$ViaConsumers;maxInstanceLatencySeconds=$MaxInstanceLatencySeconds;checkedPersistedBusinessState=$sqlVerified;observedOnUtc=[DateTime]::UtcNow;
        timingNotes='Actual end-to-end is measured from request start to the persisted runtime completion timestamp. Observed end-to-end also includes monitoring-query delay. Runtime duration excludes pre-start waiting. Percentiles use successful completions only, nearest-rank.';
        cases=$cases;progress=$progress.ToArray();databaseEvidence=$snapshot}
    Write-DemoJsonReport $ReportPath $report
}
Write-Output "Load report: $ReportPath"
if ($failure) { throw $failure }

