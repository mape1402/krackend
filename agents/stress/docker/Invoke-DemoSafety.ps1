param([switch]$OnlyIsolation,[string]$ReportPath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'DemoTest.Common.ps1')
$runId=New-DemoRunId 'safety'
if (-not $ReportPath) { $ReportPath=Join-Path $script:DemoRepoRoot "artifacts/$runId-results.json" }
$results=[Collections.Generic.List[object]]::new()
function Invoke-SafetyCase([string]$Name,[scriptblock]$Body) {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    try { $evidence=& $Body; $results.Add(@{name=$Name;status='Passed';elapsedMs=$clock.Elapsed.TotalMilliseconds;evidence=$evidence}) }
    catch { $results.Add(@{name=$Name;status='Failed';elapsedMs=$clock.Elapsed.TotalMilliseconds;error=$_.Exception.Message}) }
}
try {
    Wait-DemoHealth
    Invoke-SafetyCase 'Independent saga and parallel branch while Email is held' {
        $run=New-DemoRunId 'isolation'
        $slow=New-DemoScenario $run 'UC02' 'slow'
        $slow.Request.delayOperation='email.send';$slow.Request.delayMilliseconds=20000
        Start-DemoScenario $slow | Out-Null
        Wait-DemoObservation 'email' $slow.Request.requestId 'email.send' | Out-Null
        $fast=New-DemoScenario $run 'UC02' 'fast'
        $clock=[Diagnostics.Stopwatch]::StartNew()
        Start-DemoScenario $fast | Out-Null
        Wait-DemoInstance $fast.Request.requestId 10 | Out-Null
        $fastMs=$clock.Elapsed.TotalMilliseconds
        $pending=Get-DemoJson 'runtime' ('/demo/instances/'+$slow.Request.requestId)
        Assert-Demo (@($pending.instances | Where-Object status -eq 'Completed').Count -eq 0) 'Slow saga was not still held.'
        $sales=Get-DemoJson 'sales' ('/demo/results/'+$run)
        Assert-Demo (@($sales.entries | Where-Object { $_.saleId -eq $slow.Request.saleId -and $_.kind -eq 'DigitalAccess' }).Count -eq 1) 'The parallel access branch waited for Email.'
        Wait-DemoInstance $slow.Request.requestId 45 | Out-Null
        @{testRunId=$run;independentCompletionMs=$fastMs;consumerBarrierObserved=$true}
    }
    if (-not $OnlyIsolation) {
        Invoke-SafetyCase 'Duplicate HTTP and consumer inputs during active processing' {
            $run=New-DemoRunId 'duplicate'
            $case=New-DemoScenario $run 'UC01'
            Initialize-DemoScenario $case
            $case.Request.delayOperation='payments.capture';$case.Request.delayMilliseconds=3000
            Start-DemoScenario $case | Out-Null
            Wait-DemoObservation 'payments' $case.Request.requestId 'payments.capture' | Out-Null
            Start-DemoScenario $case -ViaConsumer | Out-Null
            Start-DemoScenario $case | Out-Null
            Wait-DemoInstance $case.Request.requestId | Out-Null
            $snapshot=Get-DemoSnapshot $run
            Assert-Demo (@($snapshot.email.emails).Count -eq 1) 'Duplicate Email request.'
            Assert-Demo ($snapshot.payments.payments[0].captured -eq 20) 'Duplicate charge.'
            Assert-Demo ($snapshot.inventories.stock[0].onHand -eq 998 -and $snapshot.inventories.stock[0].reserved -eq 0) 'Duplicate stock effect.'
            @{testRunId=$run;logicalInputs=1;inputDeliveries=3}
        }
        Invoke-SafetyCase 'Duplicate callbacks do not change completed business state' {
            $run=New-DemoRunId 'callbacks'
            $case=New-DemoScenario $run 'UC02'
            Start-DemoScenario $case | Out-Null
            Wait-DemoInstance $case.Request.requestId | Out-Null
            $before=Get-DemoFingerprint (Get-DemoSnapshot $run)
            $replayed=0
            foreach ($service in @('sales','inventories','payments','email')) {
                $reply=Send-DemoJson $service ('/demo/testing/replies/'+$case.Request.requestId+'/replay') @{replayId='repeat1';testRunId=$run}
                $replayed+=$reply.replayed
            }
            Assert-Demo ($replayed -gt 0) 'No persisted callback was replayed.'
            $deadline=[DateTime]::UtcNow.AddSeconds(20)
            $drained=$false
            while ([DateTime]::UtcNow -lt $deadline) {
                $pending=0
                foreach ($service in @('sales','inventories','payments','email')) {
                    $rows=@(Get-DemoJson $service ('/demo/testing/outbox/'+$case.Request.requestId))
                    $pending+=@($rows | Where-Object status -ne 'Completed').Count
                }
                if ($pending -eq 0) { $drained=$true;break }
                Start-Sleep -Milliseconds 100
            }
            Assert-Demo $drained 'Replayed callbacks did not drain.'
            Assert-Demo ((Get-DemoFingerprint (Get-DemoSnapshot $run)) -eq $before) 'Duplicate callbacks changed business state.'
            Wait-DemoInstance $case.Request.requestId | Out-Null
            @{testRunId=$run;callbacksReplayed=$replayed}
        }
        Invoke-SafetyCase 'RequestId reused with changed payload is rejected' {
            $run=New-DemoRunId 'conflict'
            $case=New-DemoScenario $run 'UC02'
            Start-DemoScenario $case | Out-Null
            Wait-DemoInstance $case.Request.requestId | Out-Null
            $before=Get-DemoFingerprint (Get-DemoSnapshot $run)
            $case.Request.total=22;$case.Request.lines[0].unitPrice=11
            $response=Invoke-DemoHttp 'sales' '/sales/digital' 'POST' $case.Request
            Assert-Demo ($response.Status -eq 400) 'Conflicting idempotency key was accepted.'
            Assert-Demo ((Get-DemoFingerprint (Get-DemoSnapshot $run)) -eq $before) 'Rejected input changed business state.'
            @{testRunId=$run;httpStatus=$response.Status}
        }
        foreach ($failure in @('stock','payment')) {
            Invoke-SafetyCase ("Compensation after "+$failure+" rejection") {
                $run=New-DemoRunId $failure
                $case=New-DemoScenario $run 'UC01'
                if ($failure -eq 'stock') { $case.OnHand=1 } else { $case.Request.rejectOperation='payments.capture' }
                Initialize-DemoScenario $case
                Start-DemoScenario $case | Out-Null
                Wait-DemoInstance $case.Request.requestId 60 @('Compensated') | Out-Null
                $snapshot=Get-DemoSnapshot $run
                Assert-Demo ($snapshot.sales.sales[0].status -eq 'Rejected') 'Sale was not rejected after compensation.'
                Assert-Demo ($snapshot.inventories.stock[0].onHand -eq $case.OnHand -and $snapshot.inventories.stock[0].reserved -eq 0) 'Compensation leaked a reservation.'
                Assert-Demo (@($snapshot.payments.payments).Count -eq 0) 'A rejected capture left a payment.'
                Assert-Demo (@($snapshot.email.emails).Count -eq 0) 'Rejected sale sent a confirmation.'
                @{testRunId=$run}
            }
        }
        Invoke-SafetyCase 'Excessive return is rejected before stock or refund effects' {
            $run=New-DemoRunId 'return'
            $case=New-DemoScenario $run 'UC06'
            Initialize-DemoScenario $case
            $case.Request.lines[0].quantity=3
            Start-DemoScenario $case | Out-Null
            Wait-DemoInstance $case.Request.requestId 60 @('Stopped','Failed') | Out-Null
            $snapshot=Get-DemoSnapshot $run
            Assert-Demo ($snapshot.sales.sales[0].status -eq 'Delivered') 'Rejected return changed the sale.'
            Assert-Demo ($snapshot.payments.payments[0].refunded -eq 0) 'Rejected return refunded money.'
            Assert-Demo ($snapshot.inventories.stock[0].onHand -eq 998) 'Rejected return changed stock.'
            @{testRunId=$run}
        }
        Invoke-SafetyCase 'Concurrent reservations of one SKU cannot oversell' {
            $run=New-DemoRunId 'sharedsku'
            $requests=@()
            foreach ($index in 1..12) {
                $case=New-DemoScenario $run 'UC01' "$index"
                $case.Sku="$run-sku";$case.OriginalLines[0].sku=$case.Sku;$case.Request.lines[0].sku=$case.Sku;$case.OnHand=5
                Initialize-DemoScenario $case
                $requests+=,$case
            }
            foreach ($case in $requests) { Start-DemoScenario $case | Out-Null }
            $outcomes=@($requests | ForEach-Object { Wait-DemoInstance $_.Request.requestId 90 @('Completed','Compensated') })
            $snapshot=Get-DemoSnapshot $run
            Assert-Demo (@($outcomes | Where-Object status -eq 'Completed').Count -eq 2) 'Unexpected number of successful stock claims.'
            Assert-Demo ($snapshot.inventories.stock[0].onHand -eq 1 -and $snapshot.inventories.stock[0].reserved -eq 0) 'Stock was oversold or left reserved.'
            Assert-Demo (@($snapshot.payments.payments).Count -eq 2) 'Unfulfilled sales were charged.'
            @{testRunId=$run;attempts=12;completed=2;rejected=10}
        }
        Invoke-SafetyCase 'Payment confirmation versus reservation expiration converges consistently' {
            $run=New-DemoRunId 'race'
            $expiry=New-DemoScenario $run 'UC09'
            Initialize-DemoScenario $expiry
            $payment=New-DemoScenario $run 'UC04'
            $payment.Request.saleId=$expiry.Request.saleId;$payment.Request.lines=$expiry.Request.lines
            Start-DemoScenario $payment | Out-Null
            Start-DemoScenario $expiry | Out-Null
            $p=Wait-DemoInstance $payment.Request.requestId 90 @('Completed','Compensated')
            $e=Wait-DemoInstance $expiry.Request.requestId 90 @('Completed','Stopped','Failed')
            $snapshot=Get-DemoSnapshot $run
            $sale=$snapshot.sales.sales[0];$balance=$snapshot.payments.payments[0];$stock=$snapshot.inventories.stock[0]
            if ($sale.status -eq 'Confirmed') {
                Assert-Demo ($p.status -eq 'Completed' -and $balance.captured -eq 20 -and $balance.refunded -eq 0 -and $stock.onHand -eq 998 -and $stock.reserved -eq 0) 'Payment won but balances are inconsistent.'
            } else {
                Assert-Demo ($sale.status -eq 'Expired' -and $e.status -eq 'Completed' -and $balance.captured -eq 20 -and $balance.refunded -eq 20 -and $stock.onHand -eq 1000 -and $stock.reserved -eq 0) 'Expiration won but the late payment was not refunded.'
            }
            @{testRunId=$run;winningOutcome=$sale.status}
        }
    }
} catch { $results.Add(@{name='Safety preflight';status='Blocked';error=$_.Exception.Message}) }
$failed=@($results | Where-Object status -ne 'Passed').Count
Write-DemoJsonReport $ReportPath @{testRunId=$runId;status=if ($failed) {'Failed'} else {'Passed'};cases=$results.ToArray();failed=$failed;observedOnUtc=[DateTime]::UtcNow}
Write-Output "Safety report: $ReportPath"
if ($failed) { throw "$failed safety scenarios failed or were blocked." }

