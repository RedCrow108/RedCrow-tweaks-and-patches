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
$abilityXml = Read-ModXml `
    "1.5\Defs\AbilityDefs\Abilities_RedCrowMetapods.xml"
$hediffXml = Read-ModXml `
    "1.5\Defs\HediffDefs\Hediffs_RedCrowMetapods.xml"
$organHediffXml = Read-ModXml `
    "1.5\Defs\HediffDefs\Hediffs_GenelineOrgans.xml"
$metapodThingXml = Read-ModXml `
    "1.5\Defs\ThingDefs\ThingDefs_RedCrowMetapods.xml"

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
}

$geneNodes = @(
    $organXml.SelectNodes(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
    )
) + @(
    $metapodGeneXml.SelectNodes(
        "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
    )
)
Assert-True ($geneNodes.Count -eq 15) (
    "Expected 15 feature genes, got $($geneNodes.Count)"
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
    "RC_SolarStuporCondition"
)) {
    Assert-True ($null -ne $hediffXml.SelectSingleNode(
        "/Defs/HediffDef[defName='$name']"
    )) "Missing HediffDef: $name"
}

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
$projectText = Get-Content -LiteralPath $projectPath `
    -Raw -Encoding UTF8

foreach ($requiredText in @(
    "multiplier *= extension.hungerMultiplier",
    "additive += extension.hungerAdditive",
    "__result *= factor",
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
    "resource.Value -= Math.Min(1f, resource.Value)",
    "GenDate.TicksPerYear",
    "RotStage.Fresh",
    "GetBrain()",
    "CatInHead",
    "Bipolar",
    "RC_SolarStuporCondition",
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
    "HiveInsectFilthPatch.cs"
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
    "Organs/metapods validation passed: 15 genes, 4 abilities, " +
    "3 hediffs, 4 metapods, 26 explicit hive-insect PawnKindDefs, " +
    "local art, unique defs, and no absolute XML/C# paths."
)
