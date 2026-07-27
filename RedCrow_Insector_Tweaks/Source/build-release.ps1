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

& $msbuild $project `
    /t:Rebuild `
    /p:Configuration=Release `
    /p:Platform=AnyCPU `
    /p:GameReferencesPath="$resolvedReferencesPath" `
    /p:TargetFrameworkRootPath="$resolvedFrameworkRoot\" `
    /p:FrameworkPathOverride="$frameworkReferencePath"

if ($LASTEXITCODE -ne 0) {
    throw "Release build failed with exit code $LASTEXITCODE"
}
