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

function Read-ModXml {
    param([string]$RelativePath)

    $path = Join-Path $ModRoot $RelativePath
    Assert-True (Test-Path -LiteralPath $path) "Missing XML: $RelativePath"
    return [xml](Get-Content -LiteralPath $path -Raw -Encoding UTF8)
}

$organXml = Read-ModXml `
    "1.5\Defs\GeneDefs\GeneDefs_GenelineOrgans.xml"
$metapodGeneXml = Read-ModXml `
    "1.5\Defs\GeneDefs\GeneDefs_RedCrowMetapods.xml"
$consumptionGeneXml = Read-ModXml `
    "1.5\Defs\GeneDefs\GeneDefs_RedCrowConsumptionAndAffinity.xml"
$abilityXml = Read-ModXml `
    "1.5\Defs\AbilityDefs\Abilities_RedCrowMetapods.xml"
$stage4AbilityXml = Read-ModXml `
    "1.5\Defs\AbilityDefs\Abilities_GenelineStage4.xml"
$hediffXml = Read-ModXml `
    "1.5\Defs\HediffDefs\Hediffs_RedCrowMetapods.xml"
$organHediffXml = Read-ModXml `
    "1.5\Defs\HediffDefs\Hediffs_GenelineOrgans.xml"
$metapodThingXml = Read-ModXml `
    "1.5\Defs\ThingDefs\ThingDefs_RedCrowMetapods.xml"
$tendrilPatchXml = Read-ModXml `
    "1.5\Compat\VFEInsectoids\Patches\TendrilmossBalance.xml"

$expectedGenes = [ordered]@{
    RC_Evolution_EfficientHiveMetabolism = "evolution:4"
    RC_Evolution_HivePsyfocusRecycling = "evolution:4"
    RC_Mutation_SmallThoracicArms = "mutation:2"
    RC_Mutation_DorsalManipulators = "mutation:3"
    RC_Mutation_PelvicWalkingLimbs = "mutation:4"
    RC_Mutation_HypertrophiedJellyAbdomen = "mutation:5"
    RC_Mutation_BiologicalSickle = "mutation:2"
    RC_Mutation_BiologicalDiggingTools = "mutation:3"
    RC_Mutation_BiologicalHandaxe = "mutation:2"
    RC_Mutation_BiologicalHammer = "mutation:3"
    RC_Mutation_DuplicateCerebellum = "mutation:5"
    RC_Evolution_UsurpationLarva = "evolution:5"
    RC_Evolution_HiveMemoryEgg = "evolution:5"
    RC_Evolution_LarvalRebirth = "evolution:4"
    RC_Evolution_PerfectImago = "evolution:5"
    RC_Mutation_RavenousCrop = "mutation:3"
    RC_Mutation_DevouringCrop = "mutation:6"
    RC_Mutation_PorousJellyReservoir = "mutation:3"
    RC_Mutation_LeakingJellyReservoir = "mutation:6"
    RC_Evolution_EfficientCrop = "evolution:3"
    RC_Evolution_ClosedDigestiveCycle = "evolution:6"
    RC_Evolution_JellyConservation = "evolution:3"
    RC_Evolution_SealedJellyReservoir = "evolution:6"
    RC_Evolution_HiveAnimaResonance = "evolution:3"
    RC_Evolution_HiveRegeneratorCells = "evolution:4"
}

$geneNodes = @(
    $organXml.SelectNodes(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
    )
) + @(
    $metapodGeneXml.SelectNodes(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
    )
) + @(
    $consumptionGeneXml.SelectNodes(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
    )
)
Assert-True ($geneNodes.Count -eq 25) (
    "Expected 25 feature genes, got $($geneNodes.Count)"
)

foreach ($entry in $expectedGenes.GetEnumerator()) {
    $node = $geneNodes |
        Where-Object { $_.defName -eq $entry.Key } |
        Select-Object -First 1
    Assert-True ($null -ne $node) "Missing feature gene: $($entry.Key)"
    $parts = $entry.Value.Split(":")
    Assert-NodeValue $node "./$($parts[0])" $parts[1] $entry.Key
}

foreach ($baseName in @(
    "RC_OrgansEvolutionBase",
    "RC_OrgansMutationBase",
    "RC_MetapodEvolutionBase"
)) {
    $base = if ($baseName -eq "RC_MetapodEvolutionBase") {
        $metapodGeneXml.SelectSingleNode(
            "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[@Name='$baseName']"
        )
    }
    else {
        $organXml.SelectSingleNode(
            "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[@Name='$baseName']"
        )
    }
    Assert-True ($null -ne $base) "Missing feature gene base: $baseName"
    Assert-NodeValue $base "./unlockable" "false" $baseName
    Assert-NodeValue $base "./selectionWeight" "0" $baseName
    Assert-NodeValue $base "./iconPath" `
        "UI/Icons/Genes/RC_GenelineFallback" $baseName
}

$psyfocus = $geneNodes |
    Where-Object {
        $_.defName -eq "RC_Evolution_HivePsyfocusRecycling"
    } |
    Select-Object -First 1
Assert-True (
    $psyfocus.GetAttribute("MayRequire") -eq
        "VanillaExpanded.VPsycastsE"
) "Hive psyfocus recycling must be conditional on VPE"
Assert-NodeValue $psyfocus "./statFactors/VPE_PsyfocusCostFactor" `
    "0.5" "RC_Evolution_HivePsyfocusRecycling"

$consumptionPairs = [ordered]@{
    RC_Mutation_RavenousCrop = "hungerMultiplier:1.1"
    RC_Mutation_DevouringCrop = "hungerMultiplier:1.2"
    RC_Mutation_PorousJellyReservoir = "jellyAdditive:0.1"
    RC_Mutation_LeakingJellyReservoir = "jellyAdditive:0.2"
    RC_Evolution_EfficientCrop = "hungerMultiplier:0.9"
    RC_Evolution_ClosedDigestiveCycle = "hungerMultiplier:0.8"
    RC_Evolution_JellyConservation = "jellyAdditive:-0.1"
    RC_Evolution_SealedJellyReservoir = "jellyAdditive:-0.2"
}
foreach ($entry in $consumptionPairs.GetEnumerator()) {
    $node = $geneNodes |
        Where-Object { $_.defName -eq $entry.Key } |
        Select-Object -First 1
    $parts = $entry.Value.Split(":")
    Assert-NodeValue $node (
        "./modExtensions/li[@Class=" +
        "'RedCrow.InsectorTweaks.RC_HungerGeneExtension']/" +
        $parts[0]
    ) $parts[1] $entry.Key
}

$foodPositiveTags = @(
    $consumptionGeneXml.SelectNodes(
        "/Defs/*[starts-with(defName,'RC_Mutation_')]" +
        "[modExtensions/li/hungerMultiplier]/exclusionTags/li"
    ) | ForEach-Object { $_.InnerText }
)
$foodNegativeTags = @(
    $consumptionGeneXml.SelectNodes(
        "/Defs/*[starts-with(defName,'RC_Evolution_')]" +
        "[modExtensions/li/hungerMultiplier]/exclusionTags/li"
    ) | ForEach-Object { $_.InnerText }
)
Assert-True (
    @($foodPositiveTags | Where-Object { $foodNegativeTags -contains $_ }).Count -eq 4
) "Every positive food gene must conflict with every negative food gene"

$jellyPositiveTags = @(
    $consumptionGeneXml.SelectNodes(
        "/Defs/*[starts-with(defName,'RC_Mutation_')]" +
        "[modExtensions/li/jellyAdditive]/exclusionTags/li"
    ) | ForEach-Object { $_.InnerText }
)
$jellyNegativeTags = @(
    $consumptionGeneXml.SelectNodes(
        "/Defs/*[starts-with(defName,'RC_Evolution_')]" +
        "[modExtensions/li/jellyAdditive]/exclusionTags/li"
    ) | ForEach-Object { $_.InnerText }
)
Assert-True (
    @($jellyPositiveTags | Where-Object { $jellyNegativeTags -contains $_ }).Count -eq 4
) "Every positive jelly gene must conflict with every negative jelly gene"

$anima = $geneNodes |
    Where-Object { $_.defName -eq "RC_Evolution_HiveAnimaResonance" } |
    Select-Object -First 1
foreach ($removedNode in @(
    "./biostatCpx",
    "./biostatMet",
    "./biostatArc",
    "./statOffsets/MeditationPlantGrowthOffset"
)) {
    Assert-True ($null -eq $anima.SelectSingleNode($removedNode)) (
        "Hive-anima resonance still contains removed node: $removedNode"
    )
}
Assert-True ($null -eq $anima.SelectSingleNode("./abilities")) (
    "Hive-anima resonance must not grant Anima song"
)
Assert-True (
    @($anima.SelectNodes("./customEffectDescriptions/li")).Count -eq 1
) "Hive-anima resonance must expose only Natural Meditation focus"
Assert-True (
    $anima.customEffectDescriptions.li -eq
        "Guarantees Natural Meditation focus on the pawn."
) "Hive-anima resonance has an unexpected remaining effect"

$regenerator = $geneNodes |
    Where-Object { $_.defName -eq "RC_Evolution_HiveRegeneratorCells" } |
    Select-Object -First 1
Assert-NodeValue $regenerator "./geneClass" "Gene_Healing" `
    "RC_Evolution_HiveRegeneratorCells"
Assert-NodeValue $regenerator "./preventPermanentWounds" "true" `
    "RC_Evolution_HiveRegeneratorCells"
foreach ($removedNode in @("./biostatCpx", "./biostatArc")) {
    Assert-True ($null -eq $regenerator.SelectSingleNode($removedNode)) (
        "Hive regenerator still contains removed node: $removedNode"
    )
}

$limbGenes = @(
    "RC_Mutation_SmallThoracicArms",
    "RC_Mutation_DorsalManipulators",
    "RC_Mutation_PelvicWalkingLimbs"
)
foreach ($name in $limbGenes) {
    $node = $geneNodes |
        Where-Object { $_.defName -eq $name } |
        Select-Object -First 1
    Assert-True ($null -eq $node.SelectSingleNode("./exclusionTags")) (
        "$name must remain compatible with the other additional limbs"
    )
}

$toolGenes = @(
    "RC_Mutation_BiologicalSickle",
    "RC_Mutation_BiologicalDiggingTools",
    "RC_Mutation_BiologicalHandaxe",
    "RC_Mutation_BiologicalHammer"
)
foreach ($name in $toolGenes) {
    $node = $geneNodes |
        Where-Object { $_.defName -eq $name } |
        Select-Object -First 1
    Assert-NodeValue $node "./exclusionTags/li" "RC_BiologicalTool" $name
}

$abdomen = $organHediffXml.SelectSingleNode(
    "/Defs/HediffDef[defName='RC_HypertrophiedJellyAbdomen']"
)
Assert-True ($null -ne $abdomen) "Missing jelly-abdomen HediffDef"
$spawner = $abdomen.SelectSingleNode(
    "./comps/li[@Class='AnimalBehaviours.HediffCompProperties_Spawner']"
)
Assert-True ($null -ne $spawner) "Jelly abdomen is missing its spawner"
Assert-NodeValue $spawner "./thingToSpawn" "InsectJelly" `
    "RC_HypertrophiedJellyAbdomen"
Assert-NodeValue $spawner "./spawnCount" "50" `
    "RC_HypertrophiedJellyAbdomen"
Assert-NodeValue $spawner "./spawnIntervalRange" "60000~60000" `
    "RC_HypertrophiedJellyAbdomen"
Assert-True (
    -not $abdomen.OuterXml.Contains("VRE_InsectJellyProduction")
) "Jelly abdomen must use its own Hediff"
Assert-True (
    -not $abdomen.OuterXml.Contains("Tail")
) "Jelly abdomen must not claim the Tail exclusion"

$expectedAbilities = @(
    "RC_Ability_ImplantUsurpationLarva",
    "RC_Ability_ImplantCorpseEgg",
    "RC_Ability_LarvalRebirth",
    "RC_Ability_PerfectImago"
)
foreach ($name in $expectedAbilities) {
    Assert-True ($null -ne $abilityXml.SelectSingleNode(
        "/Defs/AbilityDef[defName='$name']"
    )) "Missing AbilityDef: $name"
}

foreach ($name in @(
    "RC_UsurpationLarva",
    "RC_UsurpationComa",
    "RC_SolarStuporCondition",
    "RC_SwarmConsumed"
)) {
    Assert-True ($null -ne $hediffXml.SelectSingleNode(
        "/Defs/HediffDef[defName='$name']"
    )) "Missing HediffDef: $name"
}

$swarmConsumed = $hediffXml.SelectSingleNode(
    "/Defs/HediffDef[defName='RC_SwarmConsumed']"
)
Assert-NodeValue $swarmConsumed "./hediffClass" "HediffWithComps" `
    "RC_SwarmConsumed"
Assert-NodeValue $swarmConsumed "./disablesNeeds/li[.='Mood']" "Mood" `
    "RC_SwarmConsumed"
Assert-NodeValue $swarmConsumed "./disablesNeeds/li[.='Joy']" "Joy" `
    "RC_SwarmConsumed"
Assert-NodeValue $swarmConsumed `
    "./stages/li/statFactors/Suppressability" "0.5" `
    "RC_SwarmConsumed"
Assert-True ($null -eq $swarmConsumed.SelectSingleNode(
    "./stages/li/statFactors/SuppressionSusceptibility"
)) "RC_SwarmConsumed contains an unresolved StatDef name"

$usurpationAbility = $abilityXml.SelectSingleNode(
    "/Defs/AbilityDef[defName='RC_Ability_ImplantUsurpationLarva']"
)
Assert-NodeValue $usurpationAbility `
    "./comps/li[@Class='RedCrow.InsectorTweaks.CompProperties_AbilityUsurpation']/resultRace" `
    "Human" "RC_Ability_ImplantUsurpationLarva"
Assert-NodeValue $usurpationAbility `
    "./comps/li[@Class='RedCrow.InsectorTweaks.CompProperties_AbilityUsurpation']/resultXenotype" `
    "VRE_Insector" "RC_Ability_ImplantUsurpationLarva"
Assert-NodeValue $usurpationAbility `
    "./comps/li[@Class='RedCrow.InsectorTweaks.CompProperties_AbilityUsurpation']/requiredTraitA" `
    "CatInHead" "RC_Ability_ImplantUsurpationLarva"

$coagulate = $stage4AbilityXml.SelectSingleNode(
    "/Defs/AbilityDef[defName='RC_CoagulatingSecretion']"
)
Assert-NodeValue $coagulate "./warmupStartSound" "Coagulate_Cast" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $coagulate "./warmupEffecter" "Coagulate" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $coagulate "./jobDef" "CastAbilityOnThingMelee" `
    "RC_CoagulatingSecretion"
Assert-NodeValue $coagulate `
    "./comps/li[@Class='CompProperties_AbilityRequiresCapacity']/capacity" `
    "Manipulation" "RC_CoagulatingSecretion"

Assert-True (
    $tendrilPatchXml.OuterXml.Contains(
        'VFEI2_TendrilmossVines')
) "Tendrilmoss patch must target VFEI2_TendrilmossVines"
Assert-True (
    $tendrilPatchXml.OuterXml.Contains(
        '<harvestYield>10</harvestYield>')
) "Tendrilmoss harvest yield must be 10"
Assert-True (
    $tendrilPatchXml.OuterXml.Contains(
        '<growMinGlow>0</growMinGlow>')
) "Tendrilmoss minimum light must be 0"

$metapodBalance = [ordered]@{
    RC_Metapod_Usurpation = "1800000:25:150"
    RC_Metapod_CorpseMemory = "3600000:25:300"
    RC_Metapod_LarvalRebirth = "1800000:20:120"
    RC_Metapod_PerfectImago = "7200000:50:1200"
}
foreach ($entry in $metapodBalance.GetEnumerator()) {
    $node = $metapodThingXml.SelectSingleNode(
        "/Defs/ThingDef[defName='$($entry.Key)']"
    )
    Assert-True ($null -ne $node) "Missing ThingDef: $($entry.Key)"
    $parts = $entry.Value.Split(":")
    Assert-NodeValue $node `
        "./modExtensions/li/baseDurationTicks" $parts[0] $entry.Key
    Assert-NodeValue $node `
        "./modExtensions/li/fuelPerDay" $parts[1] $entry.Key
    Assert-NodeValue $node `
        "./comps/li[@Class='CompProperties_Refuelable']/fuelConsumptionRate" `
        $parts[1] $entry.Key
    Assert-NodeValue $node `
        "./comps/li[@Class='CompProperties_Refuelable']/fuelCapacity" `
        $parts[2] $entry.Key
    Assert-NodeValue $node `
        "./comps/li[@Class='CompProperties_Refuelable']/fuelFilter/thingDefs/li" `
        "InsectJelly" $entry.Key
}

$organSourcePath = Join-Path $ModRoot "Source\GenelineOrganEffects.cs"
$metapodSourcePath = Join-Path $ModRoot "Source\MetapodTransformations.cs"
$infrastructurePath = Join-Path $ModRoot "Source\MetapodInfrastructure.cs"
$filthSourcePath = Join-Path $ModRoot "Source\HiveInsectFilthPatch.cs"
$stage4SourcePath = Join-Path $ModRoot "Source\Stage4Effects.cs"
$followupSourcePath = Join-Path $ModRoot "Source\GenelineFollowupEffects.cs"
$projectPath = Join-Path $ModRoot "Source\RedCrow.InsectorTweaks.csproj"

$organSource = Get-Content -LiteralPath $organSourcePath `
    -Raw -Encoding UTF8
$metapodSource = Get-Content -LiteralPath $metapodSourcePath `
    -Raw -Encoding UTF8
$infrastructure = Get-Content -LiteralPath $infrastructurePath `
    -Raw -Encoding UTF8
$filthSource = Get-Content -LiteralPath $filthSourcePath `
    -Raw -Encoding UTF8
$stage4Source = Get-Content -LiteralPath $stage4SourcePath `
    -Raw -Encoding UTF8
$followupSource = Get-Content -LiteralPath $followupSourcePath `
    -Raw -Encoding UTF8
$projectText = Get-Content -LiteralPath $projectPath `
    -Raw -Encoding UTF8

$fixedBaseSourcePath = Join-Path $ModRoot "Source\FixedBaseFoodConsumption.cs"
$fixedBasePatchPath = Join-Path $ModRoot `
    "1.5\Patches\Patch_FixedBaseFoodConsumption.xml"
Assert-True (-not (Test-Path -LiteralPath $fixedBaseSourcePath)) `
    "Obsolete FixedBaseFoodConsumption.cs must be removed"
Assert-True (-not (Test-Path -LiteralPath $fixedBasePatchPath)) `
    "Obsolete Patch_FixedBaseFoodConsumption.xml must be removed"
Assert-True (-not $projectText.Contains("FixedBaseFoodConsumption.cs")) `
    "Project still compiles obsolete FixedBaseFoodConsumption.cs"

foreach ($requiredText in @(
    "multiplier *= extension.hungerMultiplier",
    "additive += extension.hungerAdditive",
    "__result *= factor",
    "NutritionEatenPerDayExplanation",
    "RC_HungerGroupLabel",
    "RC_HungerTotalFactor",
    "RC_BiologicalTool",
    "SurvivalToolsLite.StatPart_SurvivalTool",
    "Priority.Last"
)) {
    Assert-True ($organSource.Contains($requiredText)) (
        "GenelineOrganEffects.cs is missing: $requiredText"
    )
}

foreach ($requiredText in @(
    "Stage4Effects.FindJellyResource",
    "AnnualJellyCost = 0.8f",
    "RC_StingerWoundUtility.DamageAndBleed",
    "HealthUtility.DamageUntilDowned",
    "GenDate.TicksPerYear",
    "RotStage.Fresh",
    "GetBrain()",
    "CatInHead",
    "RC_SwarmConsumed",
    "RC_SolarStuporCondition",
    "ApplySwarmConversion",
    "RC_Usurpation_AlreadyConsumed",
    "jellyResource.Value = jellyResource.Max",
    "CompTipStringExtra",
    "AgeBiologicalTicks",
    "RemovePawnDirectly",
    "SetXenotypeDirect",
    "ReinitializeRaceComps"
)) {
    Assert-True ($metapodSource.Contains($requiredText)) (
        "MetapodTransformations.cs is missing: $requiredText"
    )
}
Assert-True ($stage4Source.Contains("VRE_InsectJellyDependency")) (
    "The exact personal insect-jelly resource Def is not resolved"
)
foreach ($requiredText in @(
    "jellyAdditive",
    "RC_SwarmConsumed",
    "MeditationFocusDefOf.Natural",
    "RC_Evolution_HiveAnimaResonance",
    "Priority.Last"
)) {
    Assert-True ($followupSource.Contains($requiredText)) (
        "GenelineFollowupEffects.cs is missing: $requiredText"
    )
}

foreach ($requiredText in @(
    "ThingOwner",
    "IThingHolder",
    "Scribe_Deep.Look",
    "Scribe_Collections.Look",
    "EstimatedTicksRemaining",
    "RC_MetapodHealthUtility",
    "Hediff_Implant",
    "PartOrAnyAncestorHasDirectlyAddedParts"
)) {
    Assert-True (
        $infrastructure.Contains($requiredText) -or
        $metapodSource.Contains($requiredText)
    ) "Metapod infrastructure is missing: $requiredText"
}

$targetKinds = @(
    "Megascarab",
    "Spelopede",
    "Megaspider",
    "VFEI2_Megapede",
    "VFEI2_Queen",
    "VFEI2_Swarmling",
    "VFEI2_Boomtick",
    "VFEI2_Hellbeetle",
    "VFEI2_Fuelmite",
    "VFEI2_Macrofly",
    "VFEI2_Megawasp",
    "VFEI2_Gigalocust",
    "VFEI2_Megathrips",
    "VFEI2_Venomite",
    "VFEI2_Acidspitter",
    "VFEI2_Durapod",
    "VFEI2_Tankroach",
    "VFEI2_Ironclad",
    "AA_MammothWorm",
    "AA_MegaLouse",
    "AA_Ravager",
    "AA_BlackScarab",
    "AA_BlackSpelopede",
    "AA_BlackSpider",
    "VFEI2_BlackQueen",
    "VFEI2_BlackSwarmling"
)
foreach ($name in $targetKinds) {
    Assert-True ($filthSource.Contains('"' + $name + '"')) (
        "Filth target list is missing PawnKindDef: $name"
    )
}
Assert-True (
    ([regex]::Matches(
        $filthSource,
        '^\s+"(?:AA_|VFEI2_|Mega|Spelo)[A-Za-z0-9_]+",?$',
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )).Count -eq 26
) "Filth target list must contain exactly 26 explicit PawnKindDef names"
foreach ($requiredText in @(
    "pawn.kindDef.defName",
    "pawn.RaceProps.Animal",
    "stat == StatDefOf.FilthRate",
    "__result = 0f",
    "Priority.Last"
)) {
    Assert-True ($filthSource.Contains($requiredText)) (
        "HiveInsectFilthPatch.cs is missing: $requiredText"
    )
}
foreach ($forbiddenText in @(
    '"Human"',
    '"VRE_TribalInsector"',
    "Filth_Blood",
    "FilthMaker.TryMakeFilth",
    "Notify_EnteredNewCell"
)) {
    Assert-True (-not $filthSource.Contains($forbiddenText)) (
        "Hive filth patch is broader than intended: $forbiddenText"
    )
}

foreach ($sourceFile in @(
    "GenelineOrganEffects.cs",
    "MetapodInfrastructure.cs",
    "MetapodTransformations.cs",
    "HiveInsectFilthPatch.cs",
    "GenelineFollowupEffects.cs"
)) {
    Assert-True ($projectText.Contains(
        '<Compile Include="' + $sourceFile + '" />'
    )) "Project does not compile $sourceFile"
}
Assert-True ($projectText.Contains("<Private>false</Private>")) (
    "Project references must keep Copy Local disabled"
)

$fallback = Join-Path $ModRoot `
    "Textures\UI\Icons\Genes\RC_GenelineFallback.png"
Assert-True (Test-Path -LiteralPath $fallback) (
    "Missing local fallback gene icon"
)
foreach ($baseName in @(
    "RC_SmallThoracicArms",
    "RC_DorsalManipulators",
    "RC_PelvicWalkingLimbs"
)) {
    foreach ($direction in @("north", "east", "south", "west")) {
        $texture = Join-Path $ModRoot (
            "Textures\Things\Pawn\Humanlike\BodyAttachments\" +
            $baseName + "_" + $direction + ".png"
        )
        Assert-True (Test-Path -LiteralPath $texture) (
            "Missing local render texture: $baseName`_$direction.png"
        )
        $header = [System.IO.File]::ReadAllBytes($texture)
        Assert-True (
            $header.Length -gt 24 -and
            $header[16] -eq 0 -and $header[17] -eq 0 -and
            $header[18] -eq 1 -and $header[19] -eq 0 -and
            $header[20] -eq 0 -and $header[21] -eq 0 -and
            $header[22] -eq 1 -and $header[23] -eq 0
        ) "Render texture must be 256x256: $baseName`_$direction.png"
    }
}

$structuredFiles = Get-ChildItem -LiteralPath $ModRoot -Recurse -File |
    Where-Object { $_.Extension -in @(".xml", ".cs") }
foreach ($file in $structuredFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    Assert-True (
        -not [regex]::IsMatch(
            $text,
            '[A-Za-z]:\\(?:Users|Program Files|Windows|Dev|Ins|Downloads)'
        )
    ) "Absolute Windows path found in $($file.FullName)"
}

$defKeys = @()
$xmlFiles = Get-ChildItem -LiteralPath $ModRoot `
    -Filter "*.xml" -Recurse -File
foreach ($file in $xmlFiles) {
    [xml]$xml = Get-Content -LiteralPath $file.FullName `
        -Raw -Encoding UTF8
    if ($xml.DocumentElement.Name -ne "Defs") {
        continue
    }
    foreach ($node in $xml.DocumentElement.ChildNodes) {
        if ($node.NodeType -ne "Element") {
            continue
        }
        $defName = $node.SelectSingleNode("./defName")
        if ($null -ne $defName) {
            $defKeys += (
                $node.Name + "|" + $defName.InnerText
            )
        }
    }
}
Assert-True (
    @($defKeys | Sort-Object -Unique).Count -eq $defKeys.Count
) "A concrete defName is duplicated within the same Def type"

$assembliesPath = Join-Path $ModRoot "1.5\Assemblies"
$distributedDlls = @(
    Get-ChildItem -LiteralPath $assembliesPath -Filter "*.dll" -File
)
Assert-True (
    $distributedDlls.Count -eq 1 -and
    $distributedDlls[0].Name -eq "RedCrow.InsectorTweaks.dll"
) "Assemblies must contain only RedCrow.InsectorTweaks.dll"

Write-Output (
    "Organs/metapods validation passed: 25 genes, 4 metapod abilities, " +
    "4 metapod hediffs, 4 metapods, 26 explicit hive-insect PawnKindDefs, " +
    "local art, unique defs, and no absolute XML/C# paths."
)
