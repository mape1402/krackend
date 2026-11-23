param(
    [switch]$SkipBuild,
    [switch]$Sustained
)

$ErrorActionPreference = 'Stop'
$verificationRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$previousReportPath = $env:KRACKEND_REGRESSION_REPORT
Push-Location -LiteralPath $verificationRoot
try {
    New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
    if (-not $SkipBuild) {
        dotnet build tests/Krackend.Sagas.Orchestrations.Tests/Krackend.Sagas.Orchestrations.Tests.csproj --no-restore -f net10.0 -m:1 -p:BuildInParallel=false -p:MinVerSkip=true -v quiet
        if ($LASTEXITCODE -ne 0) { throw 'The runtime/test build failed.' }
        dotnet build benchmarks/Krackend.Concurrency.Diagnostics/Regression/Regression.csproj --no-restore -m:1 -p:BuildProjectReferences=false -p:MinVerSkip=true -v quiet
        if ($LASTEXITCODE -ne 0) { throw 'The in-process runner build failed.' }
    }
    $verificationGroups = [ordered]@{
        suite = @(
            'RuntimeConcurrencyRegressionTests', 'RuntimeReactiveEventMessageTests', 'RedisRuntimeReactiveFanoutTests',
            'RemoteCommandDispatchActionTests', 'RemoteCommandDispatcherTests', 'IntakeAndMuleActionForwardingTests',
            'MuleBufferingTests', 'RuntimeMuleRegistrationTests', 'MessagingDecisionE2ETests', 'PigeonIngressAdapterTests',
            'TaskResponseTimeoutSchedulingTests', 'RuntimeEntityFrameworkRepositoryTests',
            'OrchestrationDispatchRecoveryCoordinatorTests', 'OrchestrationCompensationRecoveryCoordinatorTests',
            'RuntimeDisasterRecoveryTests'
        )
        parallel = @('RuntimeParallelExecutionTests')
    }
    if ($Sustained) { $verificationGroups['sustained-tests'] = @('RuntimeSustainedConcurrencyTests') }
    foreach ($verificationGroup in $verificationGroups.GetEnumerator()) {
        $env:KRACKEND_REGRESSION_REPORT = "artifacts/concurrency-$($verificationGroup.Key).json"
        $verificationArguments = $verificationGroup.Value
        dotnet benchmarks/Krackend.Concurrency.Diagnostics/Regression/bin/Debug/net10.0/Krackend.Concurrency.RegressionRunner.dll @verificationArguments
        if ($LASTEXITCODE -ne 0) { throw "Concurrency verification failed: $($verificationGroup.Key)." }
    }
}
finally {
    $env:KRACKEND_REGRESSION_REPORT = $previousReportPath
    Pop-Location
}
