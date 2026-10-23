[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+[fp]\d+$')]
    [string]$UnityVersion,

    [string]$EditorPath,

    [string]$PackageSource,

    [string]$OutputRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) '.gateway-validation'),

    [ValidateRange(30, 1800)]
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not $PackageSource) { $PackageSource = 'file:' + $packageRoot.Replace('\', '/') }
if (-not $EditorPath) {
    $EditorPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw "Unity Editor not found: $EditorPath"
}

$projectPath = [IO.Path]::GetFullPath((Join-Path $OutputRoot "package-import-$UnityVersion"))
if ($projectPath.Equals($packageRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $projectPath.StartsWith($packageRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The validation project must be outside the SDK package.'
}

$markerPath = Join-Path $projectPath '.gateway-package-import-fixture'
if (Test-Path -LiteralPath $projectPath) {
    if (-not (Test-Path -LiteralPath $markerPath) -or
        ([IO.File]::ReadAllText($markerPath) -ne $packageRoot)) {
        throw "Refusing to overwrite an existing project not owned by this harness: $projectPath"
    }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
function Write-FixtureFile([string]$RelativePath, [string]$Content) {
    $destination = Join-Path $projectPath $RelativePath
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::WriteAllText($destination, $Content, $utf8)
}

Write-FixtureFile '.gateway-package-import-fixture' $packageRoot
Write-FixtureFile 'ProjectSettings/ProjectVersion.txt' "m_EditorVersion: $UnityVersion`n"
# Only Gateway is requested: its manifest must supply all package dependencies.
$manifest = @{ dependencies = @{ 'com.muxy.gateway' = $PackageSource } }
Write-FixtureFile 'Packages/manifest.json' ($manifest | ConvertTo-Json -Depth 5)
Write-FixtureFile 'Assets/Consumer/Gateway.PackageConsumer.asmdef' @'
{
    "name": "Gateway.PackageConsumer",
    "references": ["Muxy.Gateway"],
    "autoReferenced": true
}
'@
Write-FixtureFile 'Assets/Consumer/GatewayPackageConsumer.cs' @'
using System;
using MuxyGateway;

public static class GatewayPackageConsumer
{
    public static readonly Type[] PublicTypes =
    {
        typeof(SDK),
        typeof(GameMetadata),
        typeof(MuxyGatewayManager),
        typeof(MuxyGatewayAuthentication),
        typeof(MuxyGatewayPolling),
        typeof(MuxyGatewayNotificationHistory)
    };
}
'@
Write-FixtureFile 'Assets/Editor/GatewayPackageImportValidation.cs' @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MuxyGateway.Imports.Schema;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

public static class GatewayPackageImportValidation
{
    public static void Validate()
    {
        try
        {
            if (GatewayPackageConsumer.PublicTypes.Length != 6)
                throw new InvalidOperationException("Consumer assembly references were not loaded.");

            foreach (Type type in GatewayPackageConsumer.PublicTypes)
            {
                if (type.Assembly.GetName().Name != "Muxy.Gateway")
                    throw new InvalidOperationException("Public type moved assembly: " + type.FullName);
            }

            PackageInfo tmpProvider = PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
            PackageInfo uiProvider = PackageInfo.FindForAssembly(typeof(UnityEngine.UI.Graphic).Assembly);
            if (tmpProvider == null || uiProvider == null)
                throw new InvalidOperationException("UI package providers were not resolved.");

            ValidateUtf8Marshalling();
            Type linker = Type.GetType("MuxyGateway.Editor.GatewayLinkerProcessor, Muxy.Gateway.Editor");
            if (linker == null || !typeof(UnityEditor.Build.IUnityLinkerProcessor).IsAssignableFrom(linker))
                throw new InvalidOperationException("Package linker callback was not registered.");

            // No SDK, scene owner, transport, or native instance is constructed.
            File.WriteAllLines(Path.Combine(Application.dataPath, "../package-import-result.txt"), new[]
            {
                "unity=" + Application.unityVersion,
                "sdkAssembly=Muxy.Gateway",
                "consumerAssembly=" + typeof(GatewayPackageConsumer).Assembly.GetName().Name,
                "tmpProvider=" + tmpProvider.name + "@" + tmpProvider.version,
                "uiProvider=" + uiProvider.name + "@" + uiProvider.version,
                "utf8StructMarshalling=passed",
                "linkerProcessor=registered",
                "status=passed"
            });
            Debug.Log("GATEWAY_PACKAGE_IMPORT_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateUtf8Marshalling()
    {
        const string text = "Gateway caf\u00e9 \u65e5\u672c\u8a9e \ud83d\ude80";
        var metadata = new MGW_GameMetadata { GameName = text, GameLogo = "", Theme = "" };
        IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MGW_GameMetadata)));
        bool initialized = false;
        try
        {
            Marshal.StructureToPtr(metadata, memory, false);
            initialized = true;
            IntPtr name = Marshal.ReadIntPtr(memory, Marshal.OffsetOf(typeof(MGW_GameMetadata), "GameName").ToInt32());
            byte[] expected = Encoding.UTF8.GetBytes(text);
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(name, actual, 0, actual.Length);
            if (Encoding.UTF8.GetString(actual) != text || Marshal.ReadByte(name, actual.Length) != 0)
                throw new InvalidOperationException("Native metadata UTF-8 encoding was not preserved.");
            var restored = (MGW_GameMetadata)Marshal.PtrToStructure(memory, typeof(MGW_GameMetadata));
            if (restored.GameName != text)
                throw new InvalidOperationException("Native metadata UTF-8 decoding was not preserved.");
        }
        finally
        {
            if (initialized)
                Marshal.DestroyStructure(memory, typeof(MGW_GameMetadata));
            Marshal.FreeHGlobal(memory);
        }
    }
}
'@

$resultPath = Join-Path $projectPath 'package-import-result.txt'
$reportPath = Join-Path $projectPath 'result.json'
# Remove stale success markers only; no project or user assets are deleted.
foreach ($staleResult in @($resultPath, $reportPath)) {
    if (Test-Path -LiteralPath $staleResult) {
        Remove-Item -LiteralPath $staleResult
    }
}

$logPath = Join-Path $projectPath 'import.log'
$arguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'GatewayPackageImportValidation.Validate',
    '-logFile', ('"' + $logPath + '"')
)
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Host "Checking Unity $UnityVersion (PID $($process.Id))"
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    # This handle refers only to the Editor instance started for this fixture.
    $process.Kill()
    $process.WaitForExit()
    $timeoutReport = @{ unityVersion = $UnityVersion; status = 'timeout'; timeoutSeconds = $TimeoutSeconds }
    [IO.File]::WriteAllText($reportPath, ($timeoutReport | ConvertTo-Json), $utf8)
    throw "Unity validation exceeded $TimeoutSeconds seconds. See $logPath"
}
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultPath)) {
    $status = 'failed'
    $reason = 'import-compilation-or-validation-failure'
    if (Test-Path -LiteralPath $logPath) {
        $log = [IO.File]::ReadAllText($logPath)
        if ($log -match 'Unity has not been activated with a valid License|Failed to activate/update license|No valid Unity Editor license') {
            $status = 'environment-blocked'
            $reason = 'unity-license-activation'
        }
    }
    $failureReport = @{ unityVersion = $UnityVersion; status = $status; reason = $reason; exitCode = $process.ExitCode }
    [IO.File]::WriteAllText($reportPath, ($failureReport | ConvertTo-Json), $utf8)
    throw "Unity validation failed (exit $($process.ExitCode)). See $logPath"
}

$observations = @{}
foreach ($line in [IO.File]::ReadAllLines($resultPath)) {
    $pair = $line -split '=', 2
    $observations[$pair[0]] = $pair[1]
}
if ($observations['unity'] -ne $UnityVersion -or $observations['status'] -ne 'passed') {
    throw 'Unity returned an unexpected version or validation result.'
}

$lock = Get-Content -LiteralPath (Join-Path $projectPath 'Packages/packages-lock.json') -Raw | ConvertFrom-Json
$dependencies = @{}
foreach ($dependency in $lock.dependencies.PSObject.Properties) {
    $dependencies[$dependency.Name] = @{
        version = $dependency.Value.version
        source = $dependency.Value.source
        depth = $dependency.Value.depth
        resolvedHash = $dependency.Value.hash
    }
}
$sourceHashes = @{}
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Runtime'), (Join-Path $packageRoot 'Prefabs'), (Join-Path $packageRoot 'Editor') -Filter '*.cs' -Recurse)
$sourceFiles += Get-Item -LiteralPath (Join-Path $packageRoot 'package.json'), (Join-Path $packageRoot 'Muxy.Gateway.asmdef')
foreach ($sourceFile in $sourceFiles) {
    $relativePath = $sourceFile.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
    $sourceHashes[$relativePath] = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash
}
$report = [ordered]@{
    unityVersion = $UnityVersion
    status = 'passed'
    scope = 'clean-package-import-consumer-compilation-and-utf8-struct-marshalling'
    packageSource = $PackageSource
    nativeInstanceConstructed = $false
    networkAuthenticationAttempted = $false
    observations = $observations
    dependencies = $dependencies
    sourceSha256 = $sourceHashes
}
[IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8), $utf8)
Write-Host "Passed: $reportPath"
