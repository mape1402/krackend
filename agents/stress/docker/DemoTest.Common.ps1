$script:DemoUrls = @{ sales='http://127.0.0.1:5411'; inventories='http://127.0.0.1:5412'; payments='http://127.0.0.1:5413'; email='http://127.0.0.1:5414'; runtime='http://127.0.0.1:5410' }
$script:DemoRepoRoot = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
function New-DemoRunId([string]$Label='test') { 'demo-' + $Label + '-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6) }
function Assert-Demo($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Invoke-DemoHttp([string]$Service, [string]$Path, [string]$Method='GET', $Body=$null, [int]$TimeoutSeconds=10) {
    $parameters = @{ Uri=$script:DemoUrls[$Service]+$Path; Method=$Method; TimeoutSec=$TimeoutSeconds; SkipHttpErrorCheck=$true }
    if ($null -ne $Body) { $parameters.ContentType='application/json'; $parameters.Body=$Body | ConvertTo-Json -Depth 30 }
    $response = Invoke-WebRequest @parameters
    $parsed = if ([string]::IsNullOrWhiteSpace($response.Content)) {
        $null
    } else {
        try { $response.Content | ConvertFrom-Json -ErrorAction Stop }
        catch { $response.Content }
    }
    @{ Status=[int]$response.StatusCode; Body=$parsed }
}
function Get-DemoJson([string]$Service, [string]$Path) {
    $response = Invoke-DemoHttp $Service $Path
    Assert-Demo ($response.Status -ge 200 -and $response.Status -lt 300) "$Service $Path returned HTTP $($response.Status)."
    $response.Body
}
function Send-DemoJson([string]$Service, [string]$Path, $Body) {
    $response = Invoke-DemoHttp $Service $Path 'POST' $Body
    Assert-Demo ($response.Status -ge 200 -and $response.Status -lt 300) "$Service $Path returned HTTP $($response.Status): $($response.Body | ConvertTo-Json -Compress -Depth 8)"
    $response.Body
}
function Wait-DemoHealth([int]$TimeoutSeconds=60) {
    foreach ($service in $script:DemoUrls.Keys) {
        $clock=[Diagnostics.Stopwatch]::StartNew(); $ready=$false
        while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
            try { if ((Invoke-DemoHttp $service '/health' -TimeoutSeconds 2).Status -eq 200) { $ready=$true; break } } catch { }
            Start-Sleep -Milliseconds 250
        }
        Assert-Demo $ready "Service $service is unavailable."
    }
}
function Wait-DemoInstance([string]$RequestId, [int]$TimeoutSeconds=60, [string[]]$Expected=@('Completed')) {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    $terminal=@('Completed','CompletedWithErrors','Failed','Compensated','DeadLettered','Stopped','Aborted')
    while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $state=Get-DemoJson 'runtime' ('/demo/instances/'+$RequestId)
        Assert-Demo (@($state.instances).Count -le 1) "Duplicate saga instances for $RequestId."
        if (@($state.instances).Count -eq 1 -and $state.instances[0].status -in $terminal) {
            Assert-Demo ($state.instances[0].status -in $Expected) "$RequestId ended as $($state.instances[0].status)."
            return $state.instances[0]
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Saga $RequestId exceeded $TimeoutSeconds seconds."
}
function Wait-DemoObservation([string]$Service, [string]$RequestId, [string]$Operation, [int]$TimeoutSeconds=15) {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $rows=@(Get-DemoJson $Service ('/demo/testing/observations/'+$RequestId))
        $entered=@($rows | Where-Object { $_.operation -eq $Operation -and $_.phase -eq 'Entered' })
        if ($entered.Count -eq 1) { return $entered[0] }
        Start-Sleep -Milliseconds 100
    }
    throw "Consumer $Service did not enter controlled operation $Operation."
}
function Get-DemoSnapshot([string]$RunId) {
    $snapshot=[ordered]@{}
    foreach ($service in @('sales','inventories','payments','email')) { $snapshot[$service]=Get-DemoJson $service ('/demo/results/'+$RunId) }
    $snapshot
}
function Get-DemoFingerprint($Snapshot) {
    $stable=[ordered]@{}
    foreach ($service in @('sales','inventories','payments','email')) {
        $data=[ordered]@{}
        foreach ($kind in @('sales','stock','payments','reservations','entries','emails')) { $data[$kind]=@($Snapshot[$service].$kind | Sort-Object id) }
        $data.operations=@($Snapshot[$service].operations | Where-Object { $_.id -notmatch ':(transport-)?input$' } | Sort-Object id)
        $stable[$service]=$data
    }
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($stable | ConvertTo-Json -Depth 40 -Compress))))
}
function New-DemoScenario([string]$RunId, [string]$CaseId, [string]$Suffix='one') {
    $id="$RunId-$CaseId-$Suffix"; $sku="$id-sku"
    $routes=@{ UC01='/sales/physical'; UC02='/sales/digital'; UC03='/sales/reservations'; UC04='/payments/confirmations';
        UC05="/sales/$id/cancellations"; UC06="/sales/$id/returns"; UC07="/sales/$id/exchanges";
        UC08='/inventories/receipts'; UC09="/sales/reservations/$id/expirations"; UC10="/sales/$id/receipt-deliveries" }
    $service=if ($CaseId -eq 'UC04') {'payments'} elseif ($CaseId -eq 'UC08') {'inventories'} else {'sales'}
    $original=@(@{sku=$sku;quantity=2;unitPrice=10})
    $request=@{requestId="$id-request";testRunId=$RunId;saleId=$id;customerId='test-customer';email='customer@example.test';total=20;
        lines=$original;replacementLines=@();expiresOnUtc=[DateTime]::UtcNow.AddMinutes(10).ToString('o')}
    if ($CaseId -in @('UC06','UC07')) { $request.lines=@(@{sku=$sku;quantity=1;unitPrice=10}) }
    if ($CaseId -eq 'UC07') { $request.replacementLines=@(@{sku="$id-new";quantity=1;unitPrice=15}) }
    if ($CaseId -eq 'UC09') { $request.expiresOnUtc=[DateTime]::UtcNow.AddSeconds(-10).ToString('o') }
    if ($CaseId -eq 'UC04') { $request.providerNotificationId="$id-provider" }
    @{CaseId=$CaseId;Service=$service;Route=$routes[$CaseId];Request=$request;OriginalLines=$original;Sku=$sku;OnHand=1000}
}
function Initialize-DemoScenario($Scenario) {
    $r=$Scenario.Request
    $seed=@{testRunId=$r.testRunId;saleId=$r.saleId;total=20;lines=$Scenario.OriginalLines;onHand=$Scenario.OnHand;
        status='Confirmed';captured=$true;createReservation=$false;commitReservation=$false;expiresOnUtc=$r.expiresOnUtc}
    if ($Scenario.CaseId -ne 'UC02') { Send-DemoJson 'inventories' '/demo/seed' $seed | Out-Null }
    if ($Scenario.CaseId -eq 'UC07') { Send-DemoJson 'inventories' '/demo/seed' @{testRunId=$r.testRunId;lines=$r.replacementLines;onHand=1000} | Out-Null }
    if ($Scenario.CaseId -in @('UC04','UC05','UC06','UC07','UC09','UC10')) {
        if ($Scenario.CaseId -in @('UC04','UC09')) { $seed.status='AwaitingPayment';$seed.captured=$false }
        if ($Scenario.CaseId -eq 'UC06') { $seed.status='Delivered' }
        Send-DemoJson 'sales' '/demo/seed' $seed | Out-Null
        Send-DemoJson 'payments' '/demo/seed' $seed | Out-Null
        if ($Scenario.CaseId -ne 'UC10') { $seed.createReservation=$true;$seed.commitReservation=$seed.captured;Send-DemoJson 'inventories' '/demo/seed' $seed | Out-Null }
    }
}
function Start-DemoScenario($Scenario, [switch]$ViaConsumer) {
    $route=$Scenario.Route+$(if ($ViaConsumer) {'?via=consumer'} else {''})
    Send-DemoJson $Scenario.Service $route $Scenario.Request
}
function Start-DemoScenariosParallel($Scenarios, [switch]$ViaConsumer, [int]$TimeoutSeconds=15) {
    $items=@($Scenarios)
    if ($items.Count -eq 0) { return @() }
    $client=[Net.Http.HttpClient]::new()
    $client.Timeout=[TimeSpan]::FromSeconds($TimeoutSeconds)
    $pending=[Collections.Generic.List[object]]::new()
    try {
        for ($index=0;$index -lt $items.Count;$index++) {
            $scenario=$items[$index]
            $route=$scenario.Route+$(if ($ViaConsumer) {'?via=consumer'} else {''})
            $message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post,$script:DemoUrls[$scenario.Service]+$route)
            $message.Content=[Net.Http.StringContent]::new(
                ($scenario.Request|ConvertTo-Json -Depth 30 -Compress),[Text.Encoding]::UTF8,'application/json')
            $started=[DateTime]::UtcNow
            $pending.Add([pscustomobject]@{Index=$index;StartedOnUtc=$started;Message=$message;Task=$client.SendAsync($message)})
        }
        [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($pending.Task)).GetAwaiter().GetResult()
        $results=[Collections.Generic.List[object]]::new()
        foreach ($entry in $pending) {
            $response=$entry.Task.GetAwaiter().GetResult()
            try {
                $content=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                if (-not $response.IsSuccessStatusCode) {
                    throw "HTTP $([int]$response.StatusCode): $content"
                }
                $body=if ([string]::IsNullOrWhiteSpace($content)) {$null} else {$content|ConvertFrom-Json}
                $results.Add([pscustomobject]@{Index=$entry.Index;StartedOnUtc=$entry.StartedOnUtc;
                    AcceptedOnUtc=[DateTime]::UtcNow;Accepted=$body})
            } finally { $response.Dispose() }
        }
        return $results.ToArray()
    } finally {
        foreach ($entry in $pending) { $entry.Message.Dispose() }
        $client.Dispose()
    }
}
function Get-DemoPercentiles([double[]]$Values) {
    $sorted=@($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return @{samples=0;p50Ms=$null;p95Ms=$null;p99Ms=$null;maxMs=$null;meanMs=$null} }
    $data=@{samples=$sorted.Count;minMs=$sorted[0];maxMs=$sorted[-1];meanMs=($sorted | Measure-Object -Average).Average}
    foreach ($p in @(50,95,99)) { $data["p$($p)Ms"]=$sorted[[math]::Max(0,[int][math]::Ceiling($sorted.Count*$p/100)-1)] }
    $data
}
function Write-DemoJsonReport([string]$Path, $Report) {
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    $Report | ConvertTo-Json -Depth 45 | Set-Content -LiteralPath $Path -Encoding utf8
}

