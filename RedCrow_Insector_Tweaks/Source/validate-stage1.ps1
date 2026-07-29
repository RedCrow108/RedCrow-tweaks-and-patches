[CmdletBinding()]
param(
    [string]$ModRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ModRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ModRoot = Split-Path -Parent $scriptDirectory
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-Gene {
    param([string]$DefName)

    $node = $script:GeneXml.SelectSingleNode(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName='$DefName']"
    )
    Assert-True ($null -ne $node) "Missing stage-1 gene: $DefName"
    return $node
}

function Assert-Value {
    param(
        [string]$DefName,
        [string]$RelativeXPath,
        [string]$Expected
    )

    $node = (Get-Gene $DefName).SelectSingleNode($RelativeXPath)
    Assert-True ($null -ne $node) "$DefName is missing $RelativeXPath"
    Assert-True ($node.InnerText -eq $Expected) (
        "$DefName $RelativeXPath expected '$Expected', got '$($node.InnerText)'"
    )
}

function Assert-Exclusion {
    param(
        [string]$DefName,
        [string]$Tag
    )

    $tags = @((Get-Gene $DefName).SelectNodes("./exclusionTags/li") |
        ForEach-Object { $_.InnerText })
    Assert-True ($tags -contains $Tag) "$DefName is missing exclusion tag $Tag"
}

$genePath = Join-Path $ModRoot "1.5\Defs\GeneDefs\GeneDefs_GenelineStage1.xml"
$hediffPath = Join-Path $ModRoot "1.5\Defs\HediffDefs\Hediffs_GenelineStage1.xml"
$thoughtPath = Join-Path $ModRoot "1.5\Defs\ThoughtDefs\Thoughts_GenelineStage1.xml"
$iconPath = Join-Path $ModRoot "Textures\UI\Icons\Genes\RC_GenelineFallback.png"

[xml]$script:GeneXml = Get-Content -LiteralPath $genePath -Raw -Encoding UTF8
[xml]$hediffXml = Get-Content -LiteralPath $hediffPath -Raw -Encoding UTF8
[xml]$thoughtXml = Get-Content -LiteralPath $thoughtPath -Raw -Encoding UTF8

$expectedGenes = [ordered]@{
    RC_Evolution_HivePsiResonator = @("evolution", "1")
    RC_Mutation_HeavyCasteStride = @("mutation", "1")
    RC_Evolution_ScoutStride = @("evolution", "1")
    RC_Evolution_CaffeineRejection = @("evolution", "1")
    RC_Evolution_ChipfirRejection = @("evolution", "1")
    RC_Evolution_RoyalJellyRejection = @("evolution", "1")
    RC_Evolution_ForagerInstinct = @("evolution", "2")
    RC_Mutation_ExternalNoiseCutoff = @("mutation", "2")
    RC_Evolution_CollectiveSensitivity = @("evolution", "2")
    RC_Evolution_MineralMandibles = @("evolution", "2")
    RC_Evolution_CarryingFolds = @("evolution", "2")
    RC_Evolution_CargoCarapace = @("evolution", "3")
    RC_Evolution_DeepHiveResonance = @("evolution", "3")
    RC_Evolution_SwarmRunningImpulse = @("evolution", "3")
    RC_Mutation_ExposedNociceptors = @("mutation", "3")
    RC_Evolution_DulledPain = @("evolution", "3")
    RC_Evolution_BroodHyperregeneration = @("evolution", "4")
    RC_Evolution_HunterBurst = @("evolution", "4")
    RC_Evolution_UnityEuphoria = @("evolution", "4")
    RC_Evolution_CompressedRestCycle = @("evolution", "4")
    RC_Evolution_PainCutoff = @("evolution", "4")
    RC_Evolution_EmotionalSilence = @("evolution", "5")
    RC_Evolution_ArchiteNutrition = @("evolution", "5")
    RC_Evolution_ContinuousWakefulness = @("evolution", "5")
}

$concreteGenes = @($script:GeneXml.SelectNodes(
    "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[not(@Abstract)]"
))
Assert-True ($concreteGenes.Count -eq 24) (
    "Expected 24 concrete stage-1 genes, got $($concreteGenes.Count)"
)

foreach ($entry in $expectedGenes.GetEnumerator()) {
    $gene = Get-Gene $entry.Key
    Assert-True (-not [string]::IsNullOrWhiteSpace($gene.label)) "$($entry.Key) has no label"
    Assert-True (-not [string]::IsNullOrWhiteSpace($gene.description)) (
        "$($entry.Key) has no description"
    )
    Assert-Value $entry.Key "./$($entry.Value[0])" $entry.Value[1]
}

# Corrected speed mapping.
Assert-Value "RC_Mutation_HeavyCasteStride" "./statOffsets/MoveSpeed" "-0.2"
Assert-Value "RC_Evolution_ScoutStride" "./statOffsets/MoveSpeed" "0.2"
Assert-Value "RC_Evolution_SwarmRunningImpulse" "./statOffsets/MoveSpeed" "0.4"
Assert-Value "RC_Evolution_HunterBurst" "./statOffsets/MoveSpeed" "1"

foreach ($defName in @(
    "RC_Mutation_HeavyCasteStride",
    "RC_Evolution_ScoutStride",
    "RC_Evolution_SwarmRunningImpulse",
    "RC_Evolution_HunterBurst"
)) {
    Assert-Exclusion $defName "MoveSpeed"
}

foreach ($defName in @(
    "RC_Evolution_HivePsiResonator",
    "RC_Evolution_CollectiveSensitivity",
    "RC_Evolution_DeepHiveResonance"
)) {
    Assert-Exclusion $defName "PsychicAbility"
}

foreach ($defName in @(
    "RC_Mutation_ExposedNociceptors",
    "RC_Evolution_DulledPain",
    "RC_Evolution_PainCutoff"
)) {
    Assert-Exclusion $defName "Pain"
}

foreach ($defName in @(
    "RC_Evolution_CompressedRestCycle",
    "RC_Evolution_ContinuousWakefulness"
)) {
    Assert-Exclusion $defName "Sleep"
}

foreach ($defName in @(
    "RC_Evolution_UnityEuphoria",
    "RC_Evolution_EmotionalSilence"
)) {
    Assert-Exclusion $defName "Mood"
}

# Exact simple effects.
Assert-Value "RC_Evolution_HivePsiResonator" "./statOffsets/PsychicSensitivity" "0.1"
Assert-Value "RC_Evolution_HivePsiResonator" "./statOffsets/MeditationFocusGain" "0.05"
Assert-Value "RC_Evolution_HivePsiResonator" "./statOffsets/PsychicEntropyRecoveryRate" "0.05"
Assert-Value "RC_Evolution_CollectiveSensitivity" "./statOffsets/PsychicSensitivity" "0.2"
Assert-Value "RC_Evolution_DeepHiveResonance" "./statOffsets/PsychicSensitivity" "0.4"
Assert-Value "RC_Evolution_ForagerInstinct" "./statFactors/ForagedNutritionPerDay" "1.75"
Assert-Value "RC_Evolution_MineralMandibles" "./statFactors/MiningYield" "1.2"
Assert-Value "RC_Evolution_CarryingFolds" "./statOffsets/CarryBulk" "20"
Assert-Value "RC_Evolution_CarryingFolds" "./statOffsets/CarryingCapacity" "20"
Assert-Value "RC_Evolution_CargoCarapace" "./statFactors/CarryingCapacity" "1.5"
Assert-Value "RC_Evolution_CargoCarapace" "./statFactors/VEF_MassCarryCapacity" "1.5"
Assert-Value "RC_Evolution_CargoCarapace" "./statFactors/CarryWeight" "1.5"
Assert-Value "RC_Evolution_DulledPain" "./painFactor" "0.5"
Assert-Value "RC_Evolution_PainCutoff" "./painFactor" "0"
Assert-Value "RC_Evolution_BroodHyperregeneration" "./statFactors/InjuryHealingFactor" "4"
Assert-Value "RC_Evolution_CompressedRestCycle" "./statFactors/RestFallRateFactor" "0.4"
Assert-Value "RC_Evolution_EmotionalSilence" "./disablesNeeds/li" "Mood"
Assert-Value "RC_Evolution_ContinuousWakefulness" "./disablesNeeds/li" "Rest"
Assert-Value "RC_Mutation_ExposedNociceptors" "./forcedTraits/li/def" "Wimp"

# Optional chemicals must be guarded by their actual package IDs.
$chemicalCases = @(
    @("RC_Evolution_CaffeineRejection", "skyarkhangel.HSK", "Caffeine"),
    @("RC_Evolution_ChipfirRejection", "HSK.WatcherContent", "Chipfir"),
    @(
        "RC_Evolution_RoyalJellyRejection",
        "CarbineAction.HSK.VFE.Insectoid2",
        "VFEI2_RoyalJellyChemical"
    )
)
foreach ($case in $chemicalCases) {
    $gene = Get-Gene $case[0]
    Assert-True ($gene.GetAttribute("MayRequire") -eq $case[1]) (
        "$($case[0]) has the wrong MayRequire"
    )
    Assert-Value $case[0] "./chemical" $case[2]
    Assert-Value $case[0] "./addictionChanceFactor" "0"
}

# Removable custom hediffs and the gene-gated thought.
$hearing = $hediffXml.SelectSingleNode(
    "/Defs/HediffDef[defName='RC_ExternalNoiseCutoff']"
)
Assert-True ($hearing.stages.li.capMods.li.capacity -eq "Hearing") (
    "RC_ExternalNoiseCutoff must affect Hearing"
)
Assert-True ($hearing.stages.li.capMods.li.setMax -eq "0") (
    "RC_ExternalNoiseCutoff Hearing setMax must be 0"
)

$nutrition = $hediffXml.SelectSingleNode(
    "/Defs/HediffDef[defName='RC_ArchiteNutrition']"
)
Assert-True ($nutrition.stages.li.hungerRateFactorOffset -eq "-0.9999") (
    "RC_ArchiteNutrition hungerRateFactorOffset must be -0.9999"
)

$euphoria = $thoughtXml.SelectSingleNode(
    "/Defs/ThoughtDef[defName='RC_UnityEuphoria']"
)
Assert-True ($euphoria.workerClass -eq "ThoughtWorker_AlwaysActive") (
    "RC_UnityEuphoria must always be active"
)
Assert-True ($euphoria.requiredGenes.li -eq "RC_Evolution_UnityEuphoria") (
    "RC_UnityEuphoria must be gated by its stage-1 gene"
)
Assert-True ($euphoria.stages.li.baseMoodEffect -eq "10") (
    "RC_UnityEuphoria mood effect must be 10"
)

Assert-True (Test-Path -LiteralPath $iconPath -PathType Leaf) (
    "Missing local fallback icon: $iconPath"
)
Assert-True ((Get-Item -LiteralPath $iconPath).Length -gt 0) (
    "Local fallback icon is empty"
)

$fallbackGenes = @(
    "RC_Evolution_ForagerInstinct",
    "RC_Mutation_ExternalNoiseCutoff",
    "RC_Evolution_MineralMandibles",
    "RC_Evolution_CarryingFolds",
    "RC_Evolution_CargoCarapace",
    "RC_Evolution_PainCutoff",
    "RC_Evolution_EmotionalSilence",
    "RC_Evolution_ArchiteNutrition"
)
foreach ($defName in $fallbackGenes) {
    Assert-Value $defName "./iconPath" "UI/Icons/Genes/RC_GenelineFallback"
}

# Parse every project XML and ensure every new defName is declared only once.
$allXmlFiles = Get-ChildItem -LiteralPath $ModRoot -Recurse -File -Filter "*.xml"
foreach ($file in $allXmlFiles) {
    [void][xml](Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8)
}

$allDefNames = foreach ($file in $allXmlFiles) {
    Select-String -LiteralPath $file.FullName -Pattern "<defName>([^<]+)</defName>" -AllMatches |
        ForEach-Object { $_.Matches } |
        ForEach-Object { $_.Groups[1].Value }
}
foreach ($defName in $expectedGenes.Keys) {
    Assert-True (@($allDefNames | Where-Object { $_ -eq $defName }).Count -eq 1) (
        "Stage-1 defName '$defName' is not unique"
    )
}

Write-Host "Stage 1 static validation passed: 24 genes, exact effects, exclusions, dependencies, XML, and local icon."
