[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+[fp]\d+$')]
    [string]$UnityVersion,
    [string]$EditorPath,
    [ValidateSet('Lifecycle', 'Client')][string]$Probe = 'Lifecycle',
    [string]$OutputRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) '.gateway-validation/transport-lifecycle'),
    [ValidateRange(30, 1800)] [int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
if (!$EditorPath) { $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$UnityVersion/Editor/Unity.exe" }
$project = [IO.Path]::GetFullPath((Join-Path $OutputRoot "package-import-$UnityVersion"))
$packageRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if ($project.Equals($packageRoot, [StringComparison]::OrdinalIgnoreCase) -or $project.StartsWith($packageRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'The test fixture must be outside the SDK package.' }
$marker = Join-Path $project '.gateway-package-import-fixture'
$helper = if ($Probe -eq 'Client') { 'GatewayClientWebSocketValidation' } else { 'GatewayTransportLifecycleValidation' }
$resultFile = if ($Probe -eq 'Client') { 'clientwebsocket-result.json' } else { 'transport-lifecycle-result.json' }
if ((Test-Path -LiteralPath $marker) -and [IO.File]::ReadAllText($marker) -eq $packageRoot) {
    # Remove only this harness's previous helper before the dependency-only import.
    foreach ($name in @('GatewayTransportLifecycleValidation.cs', 'GatewayTransportLifecycleValidation.cs.meta', 'GatewayClientWebSocketValidation.cs', 'GatewayClientWebSocketValidation.cs.meta')) {
        $previousHelper = Join-Path $project "Assets/Editor/$name"
        if (Test-Path -LiteralPath $previousHelper) { Remove-Item -LiteralPath $previousHelper }
    }
}
# The import harness owns/marks its fixture and refuses to overwrite unrelated projects.
& (Join-Path $PSScriptRoot 'Test-PackageImport.ps1') -UnityVersion $UnityVersion -EditorPath $EditorPath -OutputRoot $OutputRoot -TimeoutSeconds $TimeoutSeconds
$manifestPath = Join-Path $project 'Packages/manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.dependencies | Add-Member -NotePropertyName 'com.unity.modules.jsonserialize' -NotePropertyValue '1.0.0' -Force
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 10), $utf8)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot ($helper + '.cs')) -Destination (Join-Path $project ('Assets/Editor/' + $helper + '.cs'))
$resultPath = Join-Path $project $resultFile
if (Test-Path -LiteralPath $resultPath) {
    Move-Item -LiteralPath $resultPath -Destination ($resultPath + '.' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.previous')
}
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $project + '"'), '-executeMethod', ($helper + '.Run'), '-logFile', ('"' + (Join-Path $project ($helper + '.log')) + '"'))
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    # Only terminate the child Editor created by this invocation.
    $process.Kill()
    $process.WaitForExit()
    throw 'Local transport validation timed out; inspect the retained fixture log.'
}
if (!(Test-Path -LiteralPath $resultPath)) { throw 'Transport validation did not write a result.' }
$report = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or $report.status -ne 'passed' -or $report.unityVersion -ne $UnityVersion) {
    throw "Transport validation failed: $($report.failureStep) ($($report.failureType))."
}
Write-Host "Passed: $resultPath"
