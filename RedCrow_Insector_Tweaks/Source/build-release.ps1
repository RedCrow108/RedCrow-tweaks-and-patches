param(
    [Parameter(Mandatory = $true)]
    [string]$GameReferencesPath,

    [Parameter(Mandatory = $true)]
    [string]$TargetFrameworkRootPath
)

$ErrorActionPreference = "Stop"

$msbuild = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$project = Join-Path $PSScriptRoot "RedCrow.InsectorTweaks.csproj"
$resolvedReferencesPath = (Resolve-Path -LiteralPath $GameReferencesPath).Path
$resolvedFrameworkRoot = (Resolve-Path -LiteralPath $TargetFrameworkRootPath).Path
$frameworkReferencePath = Join-Path $resolvedFrameworkRoot ".NETFramework\v4.7.2"

if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "MSBuild was not found at $msbuild"
}

foreach ($reference in @("Assembly-CSharp.dll", "0Harmony.dll")) {
    $referencePath = Join-Path $resolvedReferencesPath $reference
    if (-not (Test-Path -LiteralPath $referencePath)) {
        throw "Required reference was not found: $referencePath"
    }
}

$previousGameReferencesPath =
    [Environment]::GetEnvironmentVariable("GameReferencesPath", "Process")
$previousTargetFrameworkRootPath =
    [Environment]::GetEnvironmentVariable("TargetFrameworkRootPath", "Process")
$previousFrameworkPathOverride =
    [Environment]::GetEnvironmentVariable("FrameworkPathOverride", "Process")

try {
    # MSBuild 4.8 receives /p values containing spaces as separate arguments
    # under Windows PowerShell 5.1. Process-scoped properties avoid that split.
    [Environment]::SetEnvironmentVariable(
        "GameReferencesPath",
        $resolvedReferencesPath,
        "Process")
    [Environment]::SetEnvironmentVariable(
        "TargetFrameworkRootPath",
        $resolvedFrameworkRoot,
        "Process")
    [Environment]::SetEnvironmentVariable(
        "FrameworkPathOverride",
        $frameworkReferencePath,
        "Process")

    & $msbuild $project `
        /t:Rebuild `
        /p:Configuration=Release `
        /p:Platform=AnyCPU

    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }
}
finally {
    [Environment]::SetEnvironmentVariable(
        "GameReferencesPath",
        $previousGameReferencesPath,
        "Process")
    [Environment]::SetEnvironmentVariable(
        "TargetFrameworkRootPath",
        $previousTargetFrameworkRootPath,
        "Process")
    [Environment]::SetEnvironmentVariable(
        "FrameworkPathOverride",
        $previousFrameworkPathOverride,
        "Process")
}
