[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+[fp]\d+$')][string]$UnityVersion,
    [ValidateSet('Mono','IL2CPP')][ValidateCount(1,2)][string[]]$Backends=@('Mono','IL2CPP'),
    [string]$OutputRoot=(Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) '.gateway-validation/compatibility-players'),
    [ValidateRange(60,3600)][int]$TimeoutSeconds=1800
)
$ErrorActionPreference='Stop'
$packageRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$root=[IO.Path]::GetFullPath($OutputRoot)
if ($root.Equals($packageRoot,[StringComparison]::OrdinalIgnoreCase) -or $root.StartsWith($packageRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixtures must be outside the SDK package.' }
$project=Join-Path $root ('player-project-'+$UnityVersion)
$marker=Join-Path $project '.gateway-compatibility-player-fixture'
if (Test-Path -LiteralPath $project) {
    if (!(Test-Path -LiteralPath $marker) -or ![IO.Path]::GetFullPath([IO.File]::ReadAllText($marker)).Equals($packageRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing to overwrite an unrelated project.' }
}
$editor="C:/Program Files/Unity/Hub/Editor/$UnityVersion/Editor/Unity.exe"
if (!(Test-Path -LiteralPath $editor)) { throw 'Requested Editor is not installed.' }
$utf8=New-Object Text.UTF8Encoding($false)
foreach ($folder in @('Packages','ProjectSettings','Assets/GatewayPlayerValidation/Editor','Assets/GatewayPlayerValidation/Runtime')) { [IO.Directory]::CreateDirectory((Join-Path $project $folder)) | Out-Null }
[IO.File]::WriteAllText($marker,$packageRoot,$utf8)
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'),"m_EditorVersion: $UnityVersion`n",$utf8)
$dependencies=@{'com.muxy.gateway'=('file:'+$packageRoot.Replace('\','/'));'com.unity.modules.jsonserialize'='1.0.0'}
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'),(@{dependencies=$dependencies}|ConvertTo-Json -Depth 5),$utf8)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GatewayCompatibilityPlayerValidation.cs') -Destination (Join-Path $project 'Assets/GatewayPlayerValidation/Runtime/GatewayCompatibilityPlayerValidation.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GatewayPlayerBuildValidation.cs') -Destination (Join-Path $project 'Assets/GatewayPlayerValidation/Editor/GatewayPlayerBuildValidation.cs')
$output=Join-Path $root ('builds-'+$UnityVersion)
[IO.Directory]::CreateDirectory($output) | Out-Null
foreach ($relative in @('builds-completed.json','build-failure.json','build.log') + @($Backends | ForEach-Object { $_+'/build-result.json' })) {
    $old=Join-Path $output $relative
    if (Test-Path -LiteralPath $old) { Move-Item -LiteralPath $old -Destination ($old+'.'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.previous') }
}
$hashes=@{}
foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Runtime'),(Join-Path $packageRoot 'Prefabs'),(Join-Path $packageRoot 'Editor') -Recurse -File | Where-Object Extension -In @('.cs','.asmdef','.dll','.meta'))) {
    $hashes[$file.FullName.Substring($packageRoot.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $file.FullName).Hash
}
foreach ($relative in @('package.json','Muxy.Gateway.asmdef','Editor.meta')) { $hashes[$relative]=(Get-FileHash -LiteralPath (Join-Path $packageRoot $relative)).Hash }
$identity=@{sdkCommit=(& git -C $packageRoot rev-parse HEAD).Trim();unityVersion=$UnityVersion;timestampUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$hashes;runtimeProbeSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'GatewayCompatibilityPlayerValidation.cs')).Hash;buildProbeSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'GatewayPlayerBuildValidation.cs')).Hash;scope='compatibility-only-High-stripping-build-and-native-utf8-configuration-smoke'}
[IO.File]::WriteAllText((Join-Path $output 'source-identity.json'),($identity|ConvertTo-Json -Depth 10),$utf8)
$settings=Join-Path $root ('build-settings-'+$UnityVersion+'.json')
[IO.File]::WriteAllText($settings,(@{outputRoot=$output;backends=$Backends}|ConvertTo-Json),$utf8)
$arguments=@('-batchmode','-nographics','-projectPath',('"'+$project+'"'),'-executeMethod','GatewayPlayerBuildValidation.Run','-gatewayBuildSettings',('"'+$settings+'"'),'-logFile',('"'+(Join-Path $output 'build.log')+'"'))
$child=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Host "Building compatibility players: $UnityVersion, child PID $($child.Id)"
if (!$child.WaitForExit($TimeoutSeconds*1000)) { $child.Kill(); $child.WaitForExit(); throw 'Owned build timed out.' }
if ($child.ExitCode -ne 0) { throw 'Compatibility build failed; inspect the retained build log.' }
foreach ($backend in $Backends) {
    $directory=Join-Path $output $backend
    $build=Get-Content -LiteralPath (Join-Path $directory 'build-result.json') -Raw|ConvertFrom-Json
    if ($build.status -ne 'passed') { throw ('Build did not pass: '+$backend) }
    $result=Join-Path $directory 'runtime-result.json'
    if (Test-Path -LiteralPath $result) { Move-Item -LiteralPath $result -Destination ($result+'.'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.previous') }
    $player=Join-Path $directory 'GatewayValidation.exe'
    $arguments=@('-batchmode','-nographics','-logFile',('"'+(Join-Path $directory 'compatibility-runtime.log')+'"'),'-gatewayCompatibilityResult',('"'+$result+'"'))
    $runtime=Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$runtime.WaitForExit(60000)) { $runtime.Kill(); $runtime.WaitForExit(); throw 'Owned compatibility player timed out.' }
    if (!(Test-Path -LiteralPath $result)) { throw 'Player produced no compatibility report.' }
    $report=Get-Content -LiteralPath $result -Raw|ConvertFrom-Json
    if ($runtime.ExitCode -ne 0 -or $report.status -ne 'passed') { throw ('Compatibility runtime failed: '+$backend) }
    Write-Host "$UnityVersion ${backend}: build and native UTF-8/configuration smoke passed"
}
foreach ($relative in $hashes.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $packageRoot $relative)).Hash -ne $hashes[$relative]) { throw 'SDK source changed during validation.' } }
$descriptor=Join-Path $project 'Library/MuxyGateway/link.xml'
if (Test-Path -LiteralPath $descriptor) { Copy-Item -LiteralPath $descriptor -Destination (Join-Path $output 'generated-link.xml') }
