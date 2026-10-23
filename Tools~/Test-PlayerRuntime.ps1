[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('offline','soak','live')][string]$Mode='offline',
    [string]$PinRequestPath,
    [ValidateRange(2,10000)][int]$Cycles=100,
    [ValidateRange(60,1800)][int]$TimeoutSeconds=900
)
$ErrorActionPreference='Stop'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new runtime output directory to prevent stale results.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$utf8=[Text.UTF8Encoding]::new($false)
$settings=Join-Path $output 'settings.json'
[IO.File]::WriteAllText($settings,(@{outputDirectory=$output;gameId='gamelink-testing';mode=$Mode;cycles=$Cycles}|ConvertTo-Json),$utf8)
$arguments=@('-batchmode','-nographics','-logFile',('"'+(Join-Path $output 'player.log')+'"'),'-gatewayPlayerSettings',('"'+$settings+'"'))
$process=Start-Process -FilePath ([IO.Path]::GetFullPath($Player)) -ArgumentList $arguments -WindowStyle Hidden -PassThru
[IO.File]::WriteAllText((Join-Path $output 'process.json'),(@{pid=$process.Id;playerSha256=(Get-FileHash -LiteralPath $Player).Hash;startedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json),$utf8)
Write-Host "Player PID $($process.Id), mode $Mode"
$watch=[Diagnostics.Stopwatch]::StartNew()
$measurements=@()
$seen=@{}
$pinPublished=$false
while (!$process.WaitForExit(100)) {
    if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) { $process.Kill(); $process.WaitForExit(); throw 'Owned validation player timed out.' }
    $statePath=Join-Path $output 'state.json'
    if (!(Test-Path -LiteralPath $statePath)) { continue }
    try { $state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json } catch { continue }
    if ($Mode -eq 'live' -and $PinRequestPath -and !$pinPublished -and $state.phase -eq 'waiting-for-pin') {
        $requestText=[IO.File]::ReadAllText([IO.Path]::GetFullPath($PinRequestPath))
        $request=$requestText|ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($request.pin) -or ([DateTime]::UtcNow-[DateTime]::Parse($request.generatedAtUtc).ToUniversalTime()).TotalSeconds -gt 240) {
            $process.Kill(); $process.WaitForExit(); throw 'Provide a fresh PIN request; this one is missing or too old.'
        }
        [IO.File]::WriteAllText((Join-Path $output 'pin-request.tmp'),$requestText,$utf8)
        Move-Item -LiteralPath (Join-Path $output 'pin-request.tmp') -Destination (Join-Path $output 'pin-request.json')
        $pinPublished=$true
    }
    if ($Mode -eq 'soak' -and !$seen.ContainsKey($state.phase)) {
        $release=switch ($state.phase) { 'soak-baseline' {'continue-soak'} 'soak-midpoint' {'continue-soak-second-half'} 'soak-completed' {'finish-soak'} default {$null} }
        if ($release) {
            $process.Refresh()
            $measurements+=@{phase=$state.phase;timestampUtc=[DateTime]::UtcNow.ToString('o');privateBytes=$process.PrivateMemorySize64;workingSet=$process.WorkingSet64;handles=$process.HandleCount}
            [IO.File]::WriteAllText((Join-Path $output 'windows-process-metrics.json'),(ConvertTo-Json -InputObject $measurements -Depth 8),$utf8)
            $seen[$state.phase]=$true
            [IO.File]::WriteAllText((Join-Path $output $release),'continue',$utf8)
        }
    }
}
$resultPath=Join-Path $output 'result.json'
if (!(Test-Path -LiteralPath $resultPath)) { throw 'Player exited without a runtime report; inspect retained player.log.' }
$result=Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
Write-Host "$($result.unityVersion) $($result.backend): $($result.status), $($result.cycles) disposal/component cycles"
if ($process.ExitCode -ne 0 -or $result.status -ne 'passed') { throw "Runtime validation failed at $($result.failureStep) ($($result.exceptionType))." }
