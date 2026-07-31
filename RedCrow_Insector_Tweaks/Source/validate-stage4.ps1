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
    Assert-True ($null -ne $node) "Missing follow-up gene: $DefName"
    return $node
}

$genePath = Join-Path $ModRoot `
    "1.5\Defs\GeneDefs\GeneDefs_GenelineStage4.xml"
$abilityPath = Join-Path $ModRoot `
    "1.5\Defs\AbilityDefs\Abilities_GenelineStage4.xml"
$hediffPath = Join-Path $ModRoot `
    "1.5\Defs\HediffDefs\Hediffs_GenelineStage4.xml"
$sourcePath = Join-Path $ModRoot "Source\Stage4Effects.cs"
$projectPath = Join-Path $ModRoot `
    "Source\RedCrow.InsectorTweaks.csproj"

[xml]$script:GeneXml =
    Get-Content -LiteralPath $genePath -Raw -Encoding UTF8
[xml]$abilityXml =
    Get-Content -LiteralPath $abilityPath -Raw -Encoding UTF8
[xml]$hediffXml =
    Get-Content -LiteralPath $hediffPath -Raw -Encoding UTF8

$concreteGenes = @($script:GeneXml.SelectNodes(
    "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
))
Assert-True ($concreteGenes.Count -eq 3) (
    "Expected 3 concrete follow-up genes, got $($concreteGenes.Count)"
)

foreach ($baseName in @(
    "RC_Stage4EvolutionBase",
    "RC_Stage4MutationBase"
)) {
    $base = $script:GeneXml.SelectSingleNode(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[@Name='$baseName']"
    )
    Assert-True ($null -ne $base) "Missing abstract base: $baseName"
    Assert-NodeValue $base "./unlockable" "false" $baseName
    Assert-NodeValue $base "./selectionWeight" "0" $baseName
    Assert-NodeValue $base "./iconPath" `
        "UI/Icons/Genes/RC_GenelineFallback" $baseName
}

$coldLogic = Get-Gene "RC_Mutation_ColdHiveLogic"
Assert-NodeValue $coldLogic "./mutation" "1" `
    "RC_Mutation_ColdHiveLogic"
Assert-NodeValue $coldLogic "./minAgeActive" "0" `
    "RC_Mutation_ColdHiveLogic"
Assert-NodeValue $coldLogic "./forcedTraits/li/def" "Pragmatist" `
    "RC_Mutation_ColdHiveLogic"
Assert-NodeValue $coldLogic "./forcedTraits/li/degree" "0" `
    "RC_Mutation_ColdHiveLogic"
Assert-True (
    $null -eq $coldLogic.SelectSingleNode(".//*[@MayRequire]")
) "Cold hive logic must not depend on HSK More Content"

$synaptic = Get-Gene "RC_Evolution_HiveSynapticNode"
Assert-NodeValue $synaptic "./evolution" "3" `
    "RC_Evolution_HiveSynapticNode"
Assert-NodeValue $synaptic `
    "./statOffsets/PsychicSensitivity" "0.5" `
    "RC_Evolution_HiveSynapticNode"
Assert-NodeValue $synaptic `
    "./statOffsets/ResearchSpeed" "0.33" `
    "RC_Evolution_HiveSynapticNode"
Assert-NodeValue $synaptic `
    "./statOffsets/TradePriceImprovement" "-0.1" `
    "RC_Evolution_HiveSynapticNode"
Assert-True (
    $null -eq $synaptic.SelectSingleNode("./exclusionTags")
) "Synaptic node must stack with psychic effects"
Assert-True (
    $null -eq $synaptic.SelectSingleNode(".//painOffset")
) "Synaptic node must not add a pain offset"

$coagulating = Get-Gene "RC_Evolution_CoagulatingSecretion"
Assert-NodeValue $coagulating "./evolution" "4" `
    "RC_Evolution_CoagulatingSecretion"
Assert-NodeValue $coagulating "./abilities/li" `
    "RC_CoagulatingSecretion" `
    "RC_Evolution_CoagulatingSecretion"

$ability = $abilityXml.SelectSingleNode(
    "/Defs/AbilityDef[defName='RC_CoagulatingSecretion']"
)
Assert-True ($null -ne $ability) (
    "Missing RC_CoagulatingSecretion ability"
)
Assert-True ($ability.GetAttribute("ParentName") -eq "AbilityTouchBase") (
    "Coagulating secretion must use the generic touch base"
)
Assert-NodeValue $ability "./hostile" "false" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $ability `
    "./verbProperties/targetParams/canTargetSelf" "false" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $ability `
    "./verbProperties/targetParams/canTargetMechs" "false" `
    "RC_CoagulatingSecretion"
Assert-True (
    $null -eq $ability.SelectSingleNode("./cooldownTicksRange")
) "Coagulating secretion must not have a cooldown"
Assert-NodeValue $ability "./warmupMote" "Mote_CoagulateStencil" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $ability "./warmupEffecter" "Coagulate" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $ability "./warmupStartSound" "Coagulate_Cast" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $ability "./jobDef" "CastAbilityOnThingMelee" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $ability `
    "./comps/li[@Class='CompProperties_AbilityRequiresCapacity']/capacity" `
    "Manipulation" "RC_CoagulatingSecretion"

$abilityComp = $ability.SelectSingleNode(
    "./comps/li[@Class='RedCrow.InsectorTweaks.CompProperties_AbilityCoagulatingSecretion']"
)
Assert-True ($null -ne $abilityComp) (
    "Coagulating secretion is missing its autonomous comp"
)
Assert-NodeValue $abilityComp "./resourceCost" "0.2" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $abilityComp "./tendQualityRange" "0.4~0.8" `
    "RC_CoagulatingSecretion"

$buffer = $hediffXml.SelectSingleNode(
    "/Defs/HediffDef[defName='RC_SynapticNodeRemovalBuffer']"
)
Assert-True ($null -ne $buffer) (
    "Missing synaptic-node removal buffer"
)
Assert-True ($null -ne $buffer.SelectSingleNode(
    "./comps/li[@Class='RedCrow.InsectorTweaks.HediffCompProperties_SynapticNodeRemovalBuffer']"
)) "Synaptic-node removal buffer has no local lifecycle comp"

$geneNames = @($concreteGenes |
    ForEach-Object { [string]$_.defName })
Assert-True (
    @($geneNames | Sort-Object -Unique).Count -eq 3
) "Follow-up gene defNames are not unique"

$allGeneNames = @()
$allOrders = @()
$geneFiles = Get-ChildItem -LiteralPath (
    Join-Path $ModRoot "1.5\Defs\GeneDefs"
) -Filter "*.xml"
foreach ($file in $geneFiles) {
    [xml]$fileXml = Get-Content -LiteralPath $file.FullName `
        -Raw -Encoding UTF8
    $allGeneNames += @($fileXml.SelectNodes(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
    ) | ForEach-Object { [string]$_.defName })
    $allOrders += @($fileXml.SelectNodes(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName and displayOrderInCategory]"
    ) | ForEach-Object {
        [string]$_.displayOrderInCategory
    })
}
Assert-True (
    @($allGeneNames | Sort-Object -Unique).Count -eq
        $allGeneNames.Count
) "A Geneline gene defName is duplicated across current Def files"
Assert-True (
    @($allOrders | Sort-Object -Unique).Count -eq
        $allOrders.Count
) "A Geneline displayOrderInCategory is duplicated"

$sourceText =
    Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
foreach ($requiredText in @(
    "VRE_InsectJellyDependency",
    "Gene_Resource",
    "resource.Value -= Props.resourceCost",
    "TendableNow",
    "Hediff_Injury",
    "Hediff_MissingPart",
    "customBloodThingDef",
    "RaceProps.BloodDef",
    "Filth_BloodInsect",
    "VRE_Filth_BugBlood",
    "targetPawn.HostileTo(caster)",
    "targetPawn.RaceProps.IsMechanoid",
    "GetMaxHealthPostfix",
    "__result += 10f",
    "RC_SynapticNodeRemovalBuffer",
    "without changing injury",
    "Priority.Last"
)) {
    Assert-True ($sourceText.Contains($requiredText)) (
        "Stage4Effects.cs is missing required behavior: $requiredText"
    )
}

foreach ($forbidden in @(
    "AbilityDefOf.Coagulate",
    "CompAbilityEffect_Coagulate ",
    "VFEI2_RoyalInsectJelly",
    "AlphaGenes.",
    "AG_InsectBlood"
)) {
    Assert-True (-not $sourceText.Contains($forbidden)) (
        "Stage4Effects.cs has a forbidden direct dependency: $forbidden"
    )
}

$allNewXml =
    $script:GeneXml.OuterXml +
    $abilityXml.OuterXml +
    $hediffXml.OuterXml
foreach ($forbiddenXml in @(
    "MayRequire",
    "AlphaGenes",
    "AG_InsectBlood",
    "VFEI2_RoyalInsectJelly"
)) {
    Assert-True (-not $allNewXml.Contains($forbiddenXml)) (
        "Follow-up XML has a forbidden dependency or source def: " +
        $forbiddenXml
    )
}

$projectText =
    Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
Assert-True ($projectText.Contains(
    '<Compile Include="Stage4Effects.cs" />'
)) "Stage4Effects.cs is not included in the project"

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
    "Follow-up validation passed: 3 autonomous Geneline elements, " +
    "jelly-gated touch tending, actual BloodDef resolution, safe " +
    "brain-health removal support, source-aware Pragmatist, and all XML."
)
