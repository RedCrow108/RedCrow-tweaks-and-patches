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

function Assert-NodeValue {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$XPath,
        [string]$Expected,
        [string]$Context
    )

    $valueNode = $Node.SelectSingleNode($XPath)
    Assert-True ($null -ne $valueNode) "$Context is missing $XPath"
    Assert-True ($valueNode.InnerText -eq $Expected) (
        "$Context $XPath expected '$Expected', got '$($valueNode.InnerText)'"
    )
}

function Get-Gene {
    param([string]$DefName)

    $node = $script:GeneXml.SelectSingleNode(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName='$DefName']"
    )
    Assert-True ($null -ne $node) "Missing stage-3 gene: $DefName"
    return $node
}

function Get-Hediff {
    param([string]$DefName)

    $node = $script:HediffXml.SelectSingleNode(
        "/Defs/HediffDef[defName='$DefName']"
    )
    Assert-True ($null -ne $node) "Missing stage-3 hediff: $DefName"
    return $node
}

function Get-Thought {
    param([string]$DefName)

    $node = $script:ThoughtXml.SelectSingleNode(
        "/Defs/ThoughtDef[defName='$DefName']"
    )
    Assert-True ($null -ne $node) "Missing stage-3 thought: $DefName"
    return $node
}

$genePath = Join-Path $ModRoot `
    "1.5\Defs\GeneDefs\GeneDefs_GenelineStage3.xml"
$hediffPath = Join-Path $ModRoot `
    "1.5\Defs\HediffDefs\Hediffs_GenelineStage3.xml"
$thoughtPath = Join-Path $ModRoot `
    "1.5\Defs\ThoughtDefs\Thoughts_GenelineStage3.xml"
$compatPath = Join-Path $ModRoot `
    "1.5\Patches\GenelineStage3Compatibility.xml"
$sourcePath = Join-Path $ModRoot "Source\Stage3Effects.cs"
$projectPath = Join-Path $ModRoot `
    "Source\RedCrow.InsectorTweaks.csproj"

[xml]$script:GeneXml =
    Get-Content -LiteralPath $genePath -Raw -Encoding UTF8
[xml]$script:HediffXml =
    Get-Content -LiteralPath $hediffPath -Raw -Encoding UTF8
[xml]$script:ThoughtXml =
    Get-Content -LiteralPath $thoughtPath -Raw -Encoding UTF8
[xml]$compatXml =
    Get-Content -LiteralPath $compatPath -Raw -Encoding UTF8

$expectedGenes = [ordered]@{
    RC_Mutation_AlienHiveVisage = @("mutation", "1")
    RC_Evolution_PheromoneUnity = @("evolution", "2")
    RC_Evolution_UnconstrainedCarapace = @("evolution", "2")
    RC_Mutation_HiveElectroOrgan = @("mutation", "3")
    RC_Mutation_ThreatMark = @("mutation", "3")
    RC_Evolution_MatriarchCalmAura = @("evolution", "4")
    RC_Mutation_DoomOmen = @("mutation", "5")
    RC_Evolution_SegmentRestoration = @("evolution", "5")
}

$concreteGenes = @($script:GeneXml.SelectNodes(
    "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
))
Assert-True ($concreteGenes.Count -eq 8) (
    "Expected 8 concrete stage-3 genes, got $($concreteGenes.Count)"
)

foreach ($entry in $expectedGenes.GetEnumerator()) {
    $gene = Get-Gene $entry.Key
    $kind = $entry.Value[0]
    $tier = $entry.Value[1]
    Assert-True (-not [string]::IsNullOrWhiteSpace($gene.label)) (
        "$($entry.Key) has no label"
    )
    Assert-True (-not [string]::IsNullOrWhiteSpace($gene.description)) (
        "$($entry.Key) has no description"
    )
    Assert-NodeValue $gene "./$kind" $tier $entry.Key

    $opposite = if ($kind -eq "evolution") {
        "mutation"
    }
    else {
        "evolution"
    }
    Assert-True ($null -eq $gene.SelectSingleNode("./$opposite")) (
        "$($entry.Key) unexpectedly contains $opposite"
    )
}

$actualDefNames = @($concreteGenes |
    ForEach-Object { $_.defName })
Assert-True (
    @($actualDefNames | Sort-Object -Unique).Count -eq 8
) "Stage-3 gene defNames are not unique"

foreach ($kind in @("evolution", "mutation")) {
    $orders = @($concreteGenes |
        Where-Object { $null -ne $_.$kind } |
        ForEach-Object { $_.displayOrderInCategory })
    Assert-True (
        @($orders | Sort-Object -Unique).Count -eq $orders.Count
    ) "Stage-3 $kind displayOrderInCategory values are not unique"
}

$visage = Get-Gene "RC_Mutation_AlienHiveVisage"
Assert-NodeValue $visage "./statOffsets/PawnBeauty" "-3" `
    "RC_Mutation_AlienHiveVisage"
Assert-NodeValue $visage "./missingGeneRomanceChanceFactor" "0" `
    "RC_Mutation_AlienHiveVisage"
Assert-NodeValue $visage "./exclusionTags/li" "Beauty" `
    "RC_Mutation_AlienHiveVisage"

$pheromone = Get-Hediff "RC_PheromoneUnity"
Assert-NodeValue $pheromone "./stages/li/painOffset" "0.0125" `
    "RC_PheromoneUnity"
Assert-NodeValue $pheromone `
    "./stages/li/statOffsets/SocialImpact" "0.25" `
    "RC_PheromoneUnity"
Assert-True (
    $null -eq $pheromone.SelectSingleNode("./comps")
) "Pheromone unity must not invent an euphoria aura"

$electroGene = Get-Gene "RC_Mutation_HiveElectroOrgan"
Assert-NodeValue $electroGene `
    "./statOffsets/MentalBreakThreshold" "0.2" `
    "RC_Mutation_HiveElectroOrgan"
$electro = Get-Hediff "RC_HiveElectroOrgan"
$electroComp = $electro.SelectSingleNode(
    "./comps/li[@Class='AnimalBehaviours.HediffCompProperties_Electrified']"
)
Assert-True ($null -ne $electroComp) (
    "RC_HiveElectroOrgan must use the audited VEF electrified component"
)
Assert-NodeValue $electroComp "./electroRate" "40" `
    "RC_HiveElectroOrgan"
Assert-NodeValue $electroComp "./electroRadius" "5" `
    "RC_HiveElectroOrgan"
Assert-NodeValue $electroComp "./electroChargeAmount" "1" `
    "RC_HiveElectroOrgan"
$batteryNames = @($electroComp.SelectNodes(
    "./batteriesToAffect/li"
) | ForEach-Object { $_.InnerText })
Assert-True ($batteryNames.Count -eq 24) (
    "Expected 24 audited battery defName strings, got $($batteryNames.Count)"
)
Assert-True ($batteryNames -contains "Battery") (
    "Tesla whitelist is missing the base Battery"
)
Assert-True ($batteryNames -contains "ShipCapacitorSmall") (
    "Tesla whitelist is missing the final audited optional battery"
)

$aura = Get-Gene "RC_Evolution_MatriarchCalmAura"
Assert-NodeValue $aura "./statOffsets/PsychicSensitivity" "0.5" `
    "RC_Evolution_MatriarchCalmAura"
Assert-NodeValue $aura "./statOffsets/MeditationFocusGain" "0.3" `
    "RC_Evolution_MatriarchCalmAura"
Assert-NodeValue $aura `
    "./statOffsets/PsychicEntropyRecoveryRate" "0.25" `
    "RC_Evolution_MatriarchCalmAura"
Assert-NodeValue $aura "./exclusionTags/li" "PsychicAbility" `
    "RC_Evolution_MatriarchCalmAura"

$threat = Get-Gene "RC_Mutation_ThreatMark"
$doom = Get-Gene "RC_Mutation_DoomOmen"
foreach ($gene in @($threat, $doom)) {
    $tags = @($gene.SelectNodes("./exclusionTags/li") |
        ForEach-Object { $_.InnerText })
    Assert-True ($tags -contains "RC_RaidPresence") (
        "$($gene.defName) is missing RC_RaidPresence"
    )
    Assert-True ($tags -contains "AG_Presence") (
        "$($gene.defName) is missing AG_Presence"
    )
}

$regen = Get-Hediff "RC_SegmentRestoration"
$regenComp = $regen.SelectSingleNode(
    "./comps/li[@Class='RedCrow.InsectorTweaks.HediffCompProperties_SegmentRegeneration']"
)
Assert-True ($null -ne $regenComp) (
    "RC_SegmentRestoration is missing its local regeneration component"
)
Assert-NodeValue $regenComp "./rateInTicks" "55000~65000" `
    "RC_SegmentRestoration"
Assert-NodeValue $regenComp "./healAmount" "1" `
    "RC_SegmentRestoration"

$visageMemory = Get-Thought "RC_AlienHiveVisageMemory"
Assert-NodeValue $visageMemory "./durationDays" "1" `
    "RC_AlienHiveVisageMemory"
Assert-NodeValue $visageMemory "./stackLimit" "1" `
    "RC_AlienHiveVisageMemory"
Assert-NodeValue $visageMemory "./stages/li/baseMoodEffect" "-5" `
    "RC_AlienHiveVisageMemory"

$calmMemory = Get-Thought "RC_MatriarchCalmMemory"
Assert-NodeValue $calmMemory "./durationDays" "1" `
    "RC_MatriarchCalmMemory"
Assert-NodeValue $calmMemory "./stackLimit" "1" `
    "RC_MatriarchCalmMemory"
Assert-NodeValue $calmMemory "./stages/li/baseMoodEffect" "10" `
    "RC_MatriarchCalmMemory"

$sourceText =
    Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
foreach ($requiredText in @(
    "StatOffsetFromGear",
    "StatDefOf.MoveSpeed",
    "__result = 0f",
    "ForceRecount",
    "marketValue * 1.5f",
    "marketValue * 4f",
    "RemoveMemoriesOfDefWhereOtherPawnIs",
    "tickCounterStage3Aura",
    "rateSegmentRegeneration",
    "55000",
    "65000",
    "PartIsMissing",
    '"Spine"',
    "HediffMaker.MakeHediff",
    "HediffDefOf.Cut",
    "Healed segment injury",
    "Restored segment",
    "Priority.Last"
)) {
    Assert-True ($sourceText.Contains($requiredText)) (
        "Stage3Effects.cs is missing required behavior: $requiredText"
    )
}

foreach ($forbidden in @(
    "Synaptic",
    "Realism",
    "WoundHealingWithoutHemogen"
)) {
    Assert-True (
        -not $script:GeneXml.OuterXml.Contains($forbidden)
    ) "Forbidden out-of-scope stage-3 item found: $forbidden"
}

$compatText = $compatXml.OuterXml
$unsafeExclusionAdds = $compatXml.SelectNodes(
    "//li[@Class='PatchOperationAdd']/value/exclusionTags"
)
Assert-True ($unsafeExclusionAdds.Count -eq 0) (
    "Stage-3 compatibility must not add a second exclusionTags container"
)
foreach ($patch in @(
    @("AG_VFEI_PheromoneSecretor", "RC_PheromoneSecretor"),
    @("AG_TetraCoils", "RC_TeslaOrgan"),
    @("VREH_Unconstrained", "RC_ApparelMovePenaltyImmunity")
)) {
    $defName = $patch[0]
    $tag = $patch[1]
    $defXPath = "Defs/GeneDef[defName=`"$defName`"]"
    $listXPath = "$defXPath/exclusionTags"
    $conditional = $compatXml.SelectSingleNode(
        "//li[@Class='PatchOperationConditional']" +
        "[xpath='$defXPath']/match" +
        "[@Class='PatchOperationConditional']" +
        "[xpath='$listXPath']"
    )
    Assert-True ($null -ne $conditional) (
        "Stage-3 compatibility is missing safe exclusion handling for $defName"
    )
    Assert-True ($null -ne $conditional.SelectSingleNode(
        "./match[@Class='PatchOperationAdd']" +
        "[xpath='$listXPath']/value/li[text()='$tag']"
    )) "Stage-3 compatibility does not append $tag for $defName"
    Assert-True ($null -ne $conditional.SelectSingleNode(
        "./nomatch[@Class='PatchOperationAdd']" +
        "[xpath='$defXPath']/value/exclusionTags/li[text()='$tag']"
    )) "Stage-3 compatibility has no missing-list fallback for $defName"
}
foreach ($modName in @(
    "Alpha Genes",
    "VRE Hussar (HSK/CE Patched)"
)) {
    Assert-True ($compatText.Contains($modName)) (
        "Stage-3 compatibility is missing installed mod name: $modName"
    )
}
foreach ($icon in @(
    "AG_UnfathomablyUgly",
    "AGI_PheromonalSecretor",
    "Gene_Unconstrained",
    "AG_TetraCoilsIcon",
    "AG_DangerousPresence",
    "AG_MasterfulPsychic",
    "AG_DeadlyPresence",
    "AG_LimbRegeneration"
)) {
    Assert-True ($compatText.Contains($icon)) (
        "Stage-3 compatibility is missing source icon: $icon"
    )
}
Assert-True ($compatText.Contains("PatchOperationFindMod")) (
    "Stage-3 foreign resources are not conditional"
)

$projectText =
    Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
Assert-True ($projectText.Contains(
    '<Compile Include="Stage3Effects.cs" />'
)) "Stage3Effects.cs is not included in the project"

$xmlFiles = Get-ChildItem -LiteralPath $ModRoot `
    -Recurse -Filter "*.xml"
foreach ($file in $xmlFiles) {
    try {
        [xml](Get-Content -LiteralPath $file.FullName `
            -Raw -Encoding UTF8) | Out-Null
    }
    catch {
        throw (
            "Malformed XML: $($file.FullName): " +
            "$($_.Exception.Message)"
        )
    }
}

Write-Output (
    "Stage 3 validation passed: 8 autonomous genes, exact aura, " +
    "pheromone, Tesla, apparel, raid-wealth and regeneration data; " +
    "all source art is conditional and all XML files parse."
)
