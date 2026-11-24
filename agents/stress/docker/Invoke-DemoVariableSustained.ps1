param(
    [ValidateRange(1,120)][int]$DurationMinutes = 30,
    [ValidateRange(1,300)][int]$IntervalSeconds = 15,
    [int[]]$BlockSizes = @(100,150,200,250,300,350,400,450,500),
    [ValidateRange(0,120)][int]$SpikeEveryMinutes = 10,
    [ValidateRange(1,10000)][int]$SpikeSize = 1000,
    [ValidateRange(1,60)][int]$MaxInstanceSeconds = 10,
    [int]$Seed = 0,
    [switch]$ViaConsumers,
    [string]$RunId,
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DemoTest.Common.ps1')

if (-not $RunId) { $RunId = New-DemoRunId 'variable-sustained' }
if (-not $ReportPath) { $ReportPath = Join-Path $script:DemoRepoRoot "artifacts/$RunId-results.json" }
if ($Seed -eq 0) {
    $Seed = [int]([DateTime]::UtcNow.Ticks % [int]::MaxValue)
    if ($Seed -eq 0) { $Seed = 1 }
}

$sizes = @($BlockSizes | Sort-Object -Unique)
Assert-Demo ($sizes.Count -gt 0) 'At least one regular block size is required.'
Assert-Demo (@($sizes | Where-Object { $_ -lt 10 -or $_ % 10 -ne 0 }).Count -eq 0) 'Block sizes must be positive multiples of ten.'
Assert-Demo ($SpikeSize % 10 -eq 0) 'SpikeSize must be a multiple of ten.'

$durationSeconds = $DurationMinutes * 60
$blockCount = [math]::Floor($durationSeconds / $IntervalSeconds)
$spikeIntervalSeconds = $SpikeEveryMinutes * 60
if ($spikeIntervalSeconds -gt 0) {
    Assert-Demo ($spikeIntervalSeconds % $IntervalSeconds -eq 0) 'IntervalSeconds must land exactly on each scheduled spike.'
}
$random = [Random]::new($Seed)
$blocks = [Collections.Generic.List[object]]::new()
$allScenarios = [Collections.Generic.List[object]]::new()

for ($blockIndex = 0; $blockIndex -lt $blockCount; $blockIndex++) {
    $scheduledOffset = $blockIndex * $IntervalSeconds
    $isSpike = $spikeIntervalSeconds -gt 0 -and $scheduledOffset -gt 0 -and $scheduledOffset % $spikeIntervalSeconds -eq 0
    $size = if ($isSpike) { $SpikeSize } else { $sizes[$random.Next($sizes.Count)] }
    $perCase = [int]($size / 10)
    $scenarios = [Collections.Generic.List[object]]::new()
    foreach ($caseNumber in 1..10) {
        $caseId = 'UC' + $caseNumber.ToString('00')
        for ($index = 0; $index -lt $perCase; $index++) {
            $scenario = New-DemoScenario $RunId $caseId ("b{0:D3}-{1:D4}" -f ($blockIndex + 1), $index)
            $scenarios.Add($scenario)
            $allScenarios.Add($scenario)
        }
    }
    $blocks.Add([pscustomobject]@{
        Index = $blockIndex + 1
        ScheduledOffsetSeconds = $scheduledOffset
        Size = $size
        IsSpike = $isSpike
        Scenarios = $scenarios.ToArray()
        DispatchOffsetSeconds = $null
        DispatchError = $null
    })
}

function New-SeedEntry([string]$service, $body) {
    [pscustomobject]@{
        Service = $service
        Json = $body | ConvertTo-Json -Depth 30 -Compress
    }
}

function Send-SeedPhase([Net.Http.HttpClient]$client, [object[]]$entries) {
    if ($entries.Count -eq 0) { return }
    $pending = [Collections.Generic.List[object]]::new()
    try {
        foreach ($entry in $entries) {
            $message = [Net.Http.HttpRequestMessage]::new(
                [Net.Http.HttpMethod]::Post,
                $script:DemoUrls[$entry.Service] + '/demo/seed')
            $message.Content = [Net.Http.StringContent]::new(
                $entry.Json,
                [Text.Encoding]::UTF8,
                'application/json')
            $pending.Add([pscustomobject]@{ Message = $message; Task = $client.SendAsync($message) })
        }
        [void][Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($pending.Task)).GetAwaiter().GetResult()
        foreach ($item in $pending) {
            $response = $item.Task.GetAwaiter().GetResult()
            try {
                if (-not $response.IsSuccessStatusCode) {
                    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    throw "Seed request failed with HTTP $([int]$response.StatusCode): $content"
                }
            } finally { $response.Dispose() }
        }
    } finally {
        foreach ($item in $pending) { $item.Message.Dispose() }
    }
}

function Initialize-ScenariosParallel([object[]]$scenarios, [int]$chunkSize = 200) {
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(60)
    try {
        for ($offset = 0; $offset -lt $scenarios.Count; $offset += $chunkSize) {
            $last = [math]::Min($scenarios.Count - 1, $offset + $chunkSize - 1)
            $chunk = @($scenarios[$offset..$last])
            $phase1 = [Collections.Generic.List[object]]::new()
            $phase2 = [Collections.Generic.List[object]]::new()
            $phase3 = [Collections.Generic.List[object]]::new()
            foreach ($scenario in $chunk) {
                $request = $scenario.Request
                $baseSeed = [ordered]@{
                    testRunId = $request.testRunId
                    saleId = $request.saleId
                    total = 20
                    lines = $scenario.OriginalLines
                    onHand = $scenario.OnHand
                    status = 'Confirmed'
                    captured = $true
                    createReservation = $false
                    commitReservation = $false
                    expiresOnUtc = $request.expiresOnUtc
                }
                if ($scenario.CaseId -ne 'UC02') {
                    $phase1.Add((New-SeedEntry 'inventories' $baseSeed))
                }
                if ($scenario.CaseId -eq 'UC07') {
                    $phase1.Add((New-SeedEntry 'inventories' ([ordered]@{
                        testRunId = $request.testRunId
                        lines = $request.replacementLines
                        onHand = 1000
                    })))
                }
                if ($scenario.CaseId -in @('UC04','UC05','UC06','UC07','UC09','UC10')) {
                    $status = if ($scenario.CaseId -in @('UC04','UC09')) { 'AwaitingPayment' } elseif ($scenario.CaseId -eq 'UC06') { 'Delivered' } else { 'Confirmed' }
                    $captured = $scenario.CaseId -notin @('UC04','UC09')
                    $serviceSeed = [ordered]@{
                        testRunId = $request.testRunId
                        saleId = $request.saleId
                        total = 20
                        lines = $scenario.OriginalLines
                        onHand = $scenario.OnHand
                        status = $status
                        captured = $captured
                        createReservation = $false
                        commitReservation = $false
                        expiresOnUtc = $request.expiresOnUtc
                    }
                    $phase2.Add((New-SeedEntry 'sales' $serviceSeed))
                    $phase2.Add((New-SeedEntry 'payments' $serviceSeed))
                    if ($scenario.CaseId -ne 'UC10') {
                        $reservationSeed = [ordered]@{
                            testRunId = $request.testRunId
                            saleId = $request.saleId
                            total = 20
                            lines = $scenario.OriginalLines
                            onHand = $scenario.OnHand
                            status = $status
                            captured = $captured
                            createReservation = $true
                            commitReservation = $captured
                            expiresOnUtc = $request.expiresOnUtc
                        }
                        $phase3.Add((New-SeedEntry 'inventories' $reservationSeed))
                    }
                }
            }
            Send-SeedPhase $client $phase1.ToArray()
            Send-SeedPhase $client $phase2.ToArray()
            Send-SeedPhase $client $phase3.ToArray()
            Write-Output "PREPARED $($last + 1)/$($scenarios.Count)"
        }
    } finally { $client.Dispose() }
}

Wait-DemoHealth
$planCounts = $blocks | Group-Object Size | Sort-Object { [int]$_.Name } | ForEach-Object { "$($_.Name)x$($_.Count)" }
Write-Output "PLAN run=$RunId seed=$Seed blocks=$blockCount instances=$($allScenarios.Count) distribution=$($planCounts -join ',')"
Initialize-ScenariosParallel $allScenarios.ToArray()

Write-Output "READY run=$RunId duration=${DurationMinutes}m interval=${IntervalSeconds}s"
$testStartedOnUtc = [DateTime]::UtcNow
$clock = [Diagnostics.Stopwatch]::StartNew()
$dispatches = [Collections.Generic.List[object]]::new()

foreach ($block in $blocks) {
    while ($clock.Elapsed.TotalSeconds -lt $block.ScheduledOffsetSeconds) { Start-Sleep -Milliseconds 50 }
    $block.DispatchOffsetSeconds = $clock.Elapsed.TotalSeconds
    try {
        $results = @(Start-DemoScenariosParallel $block.Scenarios -ViaConsumer:$ViaConsumers -TimeoutSeconds 30)
        foreach ($result in $results) {
            $scenario = $block.Scenarios[$result.Index]
            $scenario.startedOnUtc = $result.StartedOnUtc
            $scenario.acceptedOnUtc = $result.AcceptedOnUtc
            $scenario.accepted = $result.Accepted
        }
    } catch {
        $block.DispatchError = $_.Exception.Message
    }
    $dispatches.Add([ordered]@{
        block = $block.Index
        size = $block.Size
        spike = $block.IsSpike
        scheduledOffsetSeconds = $block.ScheduledOffsetSeconds
        dispatchOffsetSeconds = $block.DispatchOffsetSeconds
        cadenceDelayMs = ($block.DispatchOffsetSeconds - $block.ScheduledOffsetSeconds) * 1000
        accepted = @($block.Scenarios | Where-Object { $_.accepted -eq $true }).Count
        error = $block.DispatchError
    })
    Write-Output ("DISPATCH block={0}/{1} size={2} spike={3} t={4:N3}s delay={5:N0}ms error={6}" -f $block.Index,$blockCount,$block.Size,$block.IsSpike,$block.DispatchOffsetSeconds,(($block.DispatchOffsetSeconds-$block.ScheduledOffsetSeconds)*1000),$block.DispatchError)
}

while ($clock.Elapsed.TotalSeconds -lt $durationSeconds) { Start-Sleep -Milliseconds 100 }
$runtimeUrl = $script:DemoUrls['runtime'] + '/demo/runs/' + [Uri]::EscapeDataString($RunId) + '/instances'
$rows = @()
$lastQueryError = $null
do {
    try {
        $rows = Invoke-RestMethod -Uri $runtimeUrl -TimeoutSec 120
        $terminal = @($rows | Where-Object status -in @('Completed','CompletedWithErrors','Failed','Compensated','DeadLettered','Stopped','Aborted')).Count
        Write-Output "PROGRESS instances=$(@($rows).Count)/$($allScenarios.Count) terminal=$terminal elapsed=$([math]::Round($clock.Elapsed.TotalSeconds,1))s"
        if ($terminal -eq $allScenarios.Count) { break }
    } catch {
        $lastQueryError = $_.Exception.Message
        Write-Output "QUERY-ERROR $lastQueryError"
    }
    Start-Sleep -Seconds 2
} while ($clock.Elapsed.TotalSeconds -lt $durationSeconds + 300)

$rowByCorrelation = @{}
foreach ($row in @($rows)) { $rowByCorrelation[$row.correlationId] = $row }
$cases = @($allScenarios | ForEach-Object {
    $scenario = $_
    $row = $rowByCorrelation[$scenario.Request.requestId]
    $blockNumber = if ($scenario.Request.requestId -match '-b(\d{3})-') { [int]$Matches[1] } else { 0 }
    $endToEndMs = if ($row -and $scenario.startedOnUtc) {
        ([DateTime]::SpecifyKind([datetime]$row.lastUpdatedOnUtc, [DateTimeKind]::Utc) - [datetime]$scenario.startedOnUtc).TotalMilliseconds
    } else { $null }
    $runtimeMs = if ($row) { ([datetimeoffset]$row.lastUpdatedOnUtc - [datetimeoffset]$row.startedOnUtc).TotalMilliseconds } else { $null }
    $completionOffset = if ($row) { ([DateTime]::SpecifyKind([datetime]$row.lastUpdatedOnUtc, [DateTimeKind]::Utc) - $testStartedOnUtc).TotalSeconds } else { $null }
    [pscustomobject][ordered]@{
        block = $blockNumber
        caseId = $scenario.CaseId
        requestId = $scenario.Request.requestId
        accepted = $null -ne $scenario.accepted
        status = if ($row) { $row.status } else { 'Pending' }
        endToEndMs = $endToEndMs
        runtimeDurationMs = $runtimeMs
        completionOffsetSeconds = $completionOffset
        slaBreached = $null -eq $endToEndMs -or $endToEndMs -gt ($MaxInstanceSeconds * 1000) -or $runtimeMs -gt ($MaxInstanceSeconds * 1000)
    }
})

$batchReports = @($cases | Group-Object block | Sort-Object { [int]$_.Name } | ForEach-Object {
    $blockNumber = [int]$_.Name
    $groupCases = @($_.Group)
    $latencies = @($groupCases | Where-Object status -eq 'Completed' | ForEach-Object { [double]$_.endToEndMs })
    $dispatch = $dispatches[$blockNumber - 1]
    $activeBefore = @($cases | Where-Object {
        $_.block -lt $blockNumber -and $null -ne $_.completionOffsetSeconds -and $_.completionOffsetSeconds -gt $dispatch.dispatchOffsetSeconds
    }).Count
    [ordered]@{
        block = $blockNumber
        size = $dispatch.size
        spike = $dispatch.spike
        scheduledOffsetSeconds = $dispatch.scheduledOffsetSeconds
        dispatchOffsetSeconds = $dispatch.dispatchOffsetSeconds
        cadenceDelayMs = $dispatch.cadenceDelayMs
        activeBefore = $activeBefore
        completed = @($groupCases | Where-Object status -eq 'Completed').Count
        failed = @($groupCases | Where-Object { $_.status -notin @('Completed','Pending') }).Count
        pending = @($groupCases | Where-Object status -eq 'Pending').Count
        slaBreaches = @($groupCases | Where-Object slaBreached).Count
        latency = Get-DemoPercentiles $latencies
        completionOffsetSeconds = ($groupCases.completionOffsetSeconds | Measure-Object -Maximum).Maximum
    }
})

$latencies = @($cases | Where-Object status -eq 'Completed' | ForEach-Object { [double]$_.endToEndMs })
$runtimeLatencies = @($cases | Where-Object status -eq 'Completed' | ForEach-Object { [double]$_.runtimeDurationMs })
$trendWindowSeconds = 10 * 60
$trendWindows = @($cases | Group-Object {
    $dispatch = $dispatches[[int]$_.block - 1]
    [int][math]::Floor([double]$dispatch.scheduledOffsetSeconds / $trendWindowSeconds)
} | Sort-Object { [int]$_.Name } | ForEach-Object {
    $windowIndex = [int]$_.Name
    $windowCases = @($_.Group)
    $windowLatencies = @($windowCases | Where-Object status -eq 'Completed' | ForEach-Object { [double]$_.endToEndMs })
    [pscustomobject][ordered]@{
        window = $windowIndex + 1
        fromSecond = $windowIndex * $trendWindowSeconds
        toSecond = [math]::Min($durationSeconds, ($windowIndex + 1) * $trendWindowSeconds)
        instances = $windowCases.Count
        slaBreaches = @($windowCases | Where-Object slaBreached).Count
        latency = Get-DemoPercentiles $windowLatencies
    }
})
$trendCount = 0.0
$trendSumX = 0.0
$trendSumY = 0.0
$trendSumXX = 0.0
$trendSumXY = 0.0
foreach ($case in $cases | Where-Object status -eq 'Completed') {
    $x = [double]$dispatches[[int]$case.block - 1].scheduledOffsetSeconds / 60.0
    $y = [double]$case.endToEndMs
    $trendCount++
    $trendSumX += $x
    $trendSumY += $y
    $trendSumXX += $x * $x
    $trendSumXY += $x * $y
}
$trendDenominator = $trendCount * $trendSumXX - $trendSumX * $trendSumX
$trendSlopeMsPerMinute = if ($trendDenominator -eq 0) { 0.0 } else {
    ($trendCount * $trendSumXY - $trendSumX * $trendSumY) / $trendDenominator
}
$lastCompletionOffsetSeconds = ($cases.completionOffsetSeconds | Measure-Object -Maximum).Maximum
$completed = @($cases | Where-Object status -eq 'Completed').Count
$failed = @($cases | Where-Object { $_.status -notin @('Completed','Pending') }).Count
$pending = @($cases | Where-Object status -eq 'Pending').Count
$breaches = @($cases | Where-Object slaBreached).Count
$dispatchErrors = @($dispatches | Where-Object { $_.error }).Count
$passed = $completed -eq $allScenarios.Count -and $failed -eq 0 -and $pending -eq 0 -and $breaches -eq 0 -and $dispatchErrors -eq 0

$report = [ordered]@{
    testRunId = $RunId
    seed = $Seed
    status = if ($passed) { 'Passed' } else { 'Failed' }
    error = if ($passed) { $null } else { "Variable sustained test failed: completed=$completed/$($allScenarios.Count), failed=$failed, pending=$pending, SLA breaches=$breaches, dispatch errors=$dispatchErrors." }
    durationMinutes = $DurationMinutes
    intervalSeconds = $IntervalSeconds
    regularBlockSizes = $sizes
    spikeEveryMinutes = $SpikeEveryMinutes
    spikeSize = $SpikeSize
    blockCount = $blockCount
    planned = $allScenarios.Count
    accepted = @($cases | Where-Object accepted).Count
    completed = $completed
    failed = $failed
    pending = $pending
    slaBreaches = $breaches
    dispatchErrors = $dispatchErrors
    elapsedSeconds = $clock.Elapsed.TotalSeconds
    latency = Get-DemoPercentiles $latencies
    runtimeLatency = Get-DemoPercentiles $runtimeLatencies
    trend = [ordered]@{
        windowMinutes = 10
        slopeMsPerMinute = $trendSlopeMsPerMinute
        windows = $trendWindows
    }
    lastOrchestrationClosedOffsetSeconds = $lastCompletionOffsetSeconds
    lastOrchestrationClosedOnUtc = if ($null -eq $lastCompletionOffsetSeconds) { $null } else { $testStartedOnUtc.AddSeconds($lastCompletionOffsetSeconds) }
    dispatches = $dispatches.ToArray()
    blocks = $batchReports
    cases = $cases
    lastQueryError = $lastQueryError
    testStartedOnUtc = $testStartedOnUtc
    testFinishedOnUtc = [DateTime]::UtcNow
}

Write-DemoJsonReport $ReportPath $report
Write-Output "Variable sustained report: $ReportPath"
if (-not $passed) { throw $report.error }
