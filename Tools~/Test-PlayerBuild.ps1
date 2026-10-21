[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+[fp]\d+$')][string]$UnityVersion,
    [ValidateSet('Mono','IL2CPP')][string[]]$Backends=@('Mono','IL2CPP'),
    [string]$OutputRoot=(Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) '.gateway-validation/players'),
    [ValidateRange(60,3600)][int]$TimeoutSeconds=1800
)
$ErrorActionPreference='Stop'
$packageRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$root=[IO.Path]::GetFullPath($OutputRoot)
if ($root.Equals($packageRoot,[StringComparison]::OrdinalIgnoreCase) -or $root.StartsWith($packageRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Player fixtures must be outside the package.' }
$project=Join-Path $root "project-$UnityVersion"
$marker=Join-Path $project '.gateway-player-fixture'
if ((Test-Path -LiteralPath $project) -and (!(Test-Path -LiteralPath $marker) -or [IO.File]::ReadAllText($marker) -ne $packageRoot)) { throw 'Refusing to overwrite an unrelated fixture.' }
$editor="C:/Program Files/Unity/Hub/Editor/$UnityVersion/Editor/Unity.exe"
if (!(Test-Path -LiteralPath $editor)) { throw 'Requested Editor is not installed.' }
$utf8=New-Object Text.UTF8Encoding($false)
foreach ($relative in @('Packages','ProjectSettings','Assets/GatewayPlayerValidation/Editor','Assets/GatewayPlayerValidation/Runtime')) { [IO.Directory]::CreateDirectory((Join-Path $project $relative)) | Out-Null }
[IO.File]::WriteAllText($marker,$packageRoot,$utf8)
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'),"m_EditorVersion: $UnityVersion`n",$utf8)
$dependencies=@{'com.muxy.gateway'=('file:'+$packageRoot.Replace('\','/'));'com.unity.modules.jsonserialize'='1.0.0'}
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'),(@{dependencies=$dependencies} | ConvertTo-Json -Depth 8),$utf8)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GatewayPlayerValidation.cs') -Destination (Join-Path $project 'Assets/GatewayPlayerValidation/Runtime/GatewayPlayerValidation.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GatewayCertificatePlayerValidation.cs') -Destination (Join-Path $project 'Assets/GatewayPlayerValidation/Runtime/GatewayCertificatePlayerValidation.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GatewayPlayerBuildValidation.cs') -Destination (Join-Path $project 'Assets/GatewayPlayerValidation/Editor/GatewayPlayerBuildValidation.cs')
$output=Join-Path $root "builds-$UnityVersion"
[IO.Directory]::CreateDirectory($output) | Out-Null
foreach ($old in @('builds-completed.json','build-failure.json','build.log')) {
    $oldPath=Join-Path $output $old
    if (Test-Path -LiteralPath $oldPath) { Move-Item -LiteralPath $oldPath -Destination ($oldPath+'.'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.previous') }
}
foreach ($backend in $Backends) {
    $oldPath=Join-Path $output "$backend/build-result.json"
    if (Test-Path -LiteralPath $oldPath) { Move-Item -LiteralPath $oldPath -Destination ($oldPath+'.'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.previous') }
}
$settingsPath=Join-Path $root "build-settings-$UnityVersion.json"
[IO.File]::WriteAllText($settingsPath,(@{outputRoot=$output;backends=$Backends} | ConvertTo-Json -Depth 8),$utf8)
$hashes=@{}
foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Runtime'),(Join-Path $packageRoot 'Prefabs'),(Join-Path $packageRoot 'Editor') -Recurse -File | Where-Object Extension -In @('.cs','.asmdef','.dll'))) { $hashes[$file.FullName.Substring($packageRoot.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $file.FullName).Hash }
$identity=@{unityVersion=$UnityVersion;timestampUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$hashes;runtimeProbeSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'GatewayPlayerValidation.cs')).Hash;buildProbeSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'GatewayPlayerBuildValidation.cs')).Hash}
$identity.sdkCommit=(& git -C $packageRoot rev-parse HEAD).Trim()
foreach ($relative in @('package.json','Muxy.Gateway.asmdef')) { $hashes[$relative]=(Get-FileHash -LiteralPath (Join-Path $packageRoot $relative)).Hash }
[IO.File]::WriteAllText((Join-Path $output 'source-identity.json'),($identity | ConvertTo-Json -Depth 10),$utf8)
$arguments=@('-batchmode','-nographics','-projectPath',('"'+$project+'"'),'-executeMethod','GatewayPlayerBuildValidation.Run','-gatewayBuildSettings',('"'+$settingsPath+'"'),'-logFile',('"'+(Join-Path $output 'build.log')+'"'))
$process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Host "Building Unity $UnityVersion ($($Backends -join ', ')), child PID $($process.Id)"
if (!$process.WaitForExit($TimeoutSeconds*1000)) { $process.Kill(); $process.WaitForExit(); throw 'Owned player build process exceeded its timeout.' }
foreach ($backend in $Backends) {
    $resultPath=Join-Path $output "$backend/build-result.json"
    if (!(Test-Path -LiteralPath $resultPath)) { throw "No $backend build result. Inspect retained build.log." }
    $result=Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    Write-Host "$UnityVersion $backend : $($result.status)"
}
if ($process.ExitCode -ne 0) { throw 'One or more requested builds failed.' }
foreach ($relative in $hashes.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $packageRoot $relative)).Hash -ne $hashes[$relative]) { throw 'SDK source changed during this build; repeat before reporting results.' }
}
$descriptor=Join-Path $project 'Library/MuxyGateway/link.xml'
if (Test-Path -LiteralPath $descriptor) { Copy-Item -LiteralPath $descriptor -Destination (Join-Path $output 'generated-link.xml') }
