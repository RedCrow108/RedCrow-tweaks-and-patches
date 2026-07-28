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
    Assert-True ($null -ne $node) "Missing stage-2 gene: $DefName"
    return $node
}

function Get-Hediff {
    param([string]$DefName)

    $node = $script:HediffXml.SelectSingleNode(
        "/Defs/HediffDef[defName='$DefName']"
    )
    Assert-True ($null -ne $node) "Missing stage-2 hediff: $DefName"
    return $node
}

function Get-Thought {
    param([string]$DefName)

    $node = $script:ThoughtXml.SelectSingleNode(
        "/Defs/ThoughtDef[defName='$DefName']"
    )
    Assert-True ($null -ne $node) "Missing stage-2 thought: $DefName"
    return $node
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

function Assert-GeneValue {
    param(
        [string]$DefName,
        [string]$XPath,
        [string]$Expected
    )

    Assert-NodeValue (Get-Gene $DefName) $XPath $Expected $DefName
}

function Assert-GeneTag {
    param(
        [string]$DefName,
        [string]$Tag
    )

    $tags = @((Get-Gene $DefName).SelectNodes("./exclusionTags/li") |
        ForEach-Object { $_.InnerText })
    Assert-True ($tags -contains $Tag) "$DefName is missing exclusion tag $Tag"
}

$genePath = Join-Path $ModRoot "1.5\Defs\GeneDefs\GeneDefs_GenelineStage2.xml"
$hediffPath = Join-Path $ModRoot "1.5\Defs\HediffDefs\Hediffs_GenelineStage2.xml"
$thoughtPath = Join-Path $ModRoot "1.5\Defs\ThoughtDefs\Thoughts_GenelineStage2.xml"
$compatPath = Join-Path $ModRoot "1.5\Patches\GenelineStage2Compatibility.xml"
$stage1CompatPath = Join-Path $ModRoot "1.5\Patches\GenelineStage1SourceIcons.xml"
$sourcePath = Join-Path $ModRoot "Source\Stage2Effects.cs"
$projectPath = Join-Path $ModRoot "Source\RedCrow.InsectorTweaks.csproj"
$cleanupPath = Join-Path $ModRoot "Source\GeneReferenceCleanup.cs"
$iconPath = Join-Path $ModRoot "Textures\UI\Icons\Genes\RC_GenelineFallback.png"

[xml]$script:GeneXml = Get-Content -LiteralPath $genePath -Raw -Encoding UTF8
[xml]$script:HediffXml = Get-Content -LiteralPath $hediffPath -Raw -Encoding UTF8
[xml]$script:ThoughtXml = Get-Content -LiteralPath $thoughtPath -Raw -Encoding UTF8
[xml]$compatXml = Get-Content -LiteralPath $compatPath -Raw -Encoding UTF8
[xml]$stage1CompatXml = Get-Content -LiteralPath $stage1CompatPath -Raw -Encoding UTF8

$expectedGenes = [ordered]@{
    RC_Mutation_MildPhotophobia = @("mutation", "1")
    RC_Mutation_SolarVulnerability = @("mutation", "1")
    RC_Mutation_SwarmSensoryCrown = @("mutation", "2")
    RC_Evolution_LongImagoCycle = @("evolution", "2")
    RC_Evolution_CuriosityShooting = @("evolution", "2")
    RC_Evolution_CuriosityMelee = @("evolution", "2")
    RC_Evolution_CuriosityConstruction = @("evolution", "2")
    RC_Evolution_CuriosityMining = @("evolution", "2")
    RC_Evolution_CuriosityCooking = @("evolution", "2")
    RC_Evolution_CuriosityPlants = @("evolution", "2")
    RC_Evolution_CuriosityAnimals = @("evolution", "2")
    RC_Evolution_CuriosityCrafting = @("evolution", "2")
    RC_Evolution_CuriosityArtistic = @("evolution", "2")
    RC_Evolution_CuriosityMedicine = @("evolution", "2")
    RC_Evolution_CuriositySocial = @("evolution", "2")
    RC_Evolution_CuriosityIntellectual = @("evolution", "2")
    RC_Mutation_SolarExhaustion = @("mutation", "2")
    RC_Evolution_AcceleratedBroodMaturity = @("evolution", "3")
    RC_Evolution_AgelessImago = @("evolution", "3")
    RC_Evolution_MatriarchWail = @("evolution", "3")
    RC_Mutation_LightStride = @("mutation", "3")
    RC_Mutation_TwilightStride = @("mutation", "3")
    RC_Evolution_PsiMimicry = @("evolution", "4")
    RC_Evolution_InsanityPulse = @("evolution", "4")
    RC_Mutation_SolarOverdrive = @("mutation", "4")
    RC_Mutation_SolarStupor = @("mutation", "4")
    RC_Evolution_ChlorophyllMetabolism = @("evolution", "4")
    RC_Mutation_SolarDeath = @("mutation", "5")
}

$concreteGenes = @($script:GeneXml.SelectNodes(
    "/Defs/VanillaRacesExpandedInsector.GenelineGeneDef[defName]"
))
Assert-True ($concreteGenes.Count -eq 28) (
    "Expected 28 concrete stage-2 genes, got $($concreteGenes.Count)"
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

    $opposite = if ($kind -eq "evolution") { "mutation" } else { "evolution" }
    Assert-True ($null -eq $gene.SelectSingleNode("./$opposite")) (
        "$($entry.Key) unexpectedly contains $opposite"
    )
}

$actualDefNames = @($concreteGenes | ForEach-Object { $_.defName })
Assert-True (
    @($actualDefNames | Sort-Object -Unique).Count -eq 28
) "Stage-2 gene defNames are not unique"

foreach ($kind in @("evolution", "mutation")) {
    $orders = @($concreteGenes |
        Where-Object { $null -ne $_.$kind } |
        ForEach-Object { $_.displayOrderInCategory })
    Assert-True (
        @($orders | Sort-Object -Unique).Count -eq $orders.Count
    ) "Stage-2 $kind displayOrderInCategory values are not unique"
}

$curiosityGenes = @($concreteGenes |
    Where-Object { $_.defName -like "RC_Evolution_Curiosity*" })
Assert-True ($curiosityGenes.Count -eq 12) (
    "Expected 12 curiosity evolutions, got $($curiosityGenes.Count)"
)
foreach ($gene in $curiosityGenes) {
    $tags = @($gene.SelectNodes("./exclusionTags/li") |
        ForEach-Object { $_.InnerText })
    Assert-True ($tags.Count -eq 2) (
        "$($gene.defName) must have exactly two curiosity exclusion tags"
    )
    Assert-True ($tags -contains "VRE_Curiosity") (
        "$($gene.defName) is missing VRE_Curiosity"
    )
    Assert-True ($tags -contains "Curiosity") (
        "$($gene.defName) is missing Curiosity"
    )
    Assert-True ($null -eq $gene.SelectSingleNode(
        ".//*[local-name()='disabledWorkTypes' or local-name()='workDisables']"
    )) "$($gene.defName) must not disable work"
}

$uvGenes = @(
    "RC_Mutation_MildPhotophobia",
    "RC_Mutation_SolarVulnerability",
    "RC_Mutation_SolarExhaustion",
    "RC_Mutation_SolarStupor",
    "RC_Mutation_SolarDeath"
)
foreach ($defName in $uvGenes) {
    Assert-GeneTag $defName "UVSensitivity"
    Assert-GeneTag $defName "RC_Conflict_UVSensitivity_LightStride"
    Assert-GeneTag $defName "RC_Conflict_UVSensitivity_SolarNutrition"
}

foreach ($defName in @(
    "RC_Mutation_LightStride",
    "RC_Mutation_TwilightStride"
)) {
    Assert-GeneTag $defName "RC_LightDependence"
}
Assert-GeneTag "RC_Mutation_LightStride" `
    "RC_Conflict_UVSensitivity_LightStride"
Assert-GeneTag "RC_Mutation_TwilightStride" `
    "RC_Conflict_TwilightStride_SolarNutrition"

foreach ($defName in @(
    "RC_Mutation_SolarOverdrive",
    "RC_Evolution_ChlorophyllMetabolism"
)) {
    Assert-GeneTag $defName "RC_SolarMetabolism"
    Assert-GeneTag $defName "RC_Conflict_UVSensitivity_SolarNutrition"
    Assert-GeneTag $defName `
        "RC_Conflict_TwilightStride_SolarNutrition"
}

foreach ($defName in @(
    "RC_Evolution_LongImagoCycle",
    "RC_Evolution_AgelessImago"
)) {
    Assert-GeneTag $defName "AG_Aging"
    Assert-GeneTag $defName "Aging"
}

Assert-GeneValue "RC_Mutation_SolarVulnerability" `
    "./conditionalStatAffecters/li/statFactors/MoveSpeed" "0.9"
Assert-GeneValue "RC_Mutation_SolarExhaustion" `
    "./conditionalStatAffecters/li/statFactors/MoveSpeed" "0.8"
Assert-GeneValue "RC_Evolution_LongImagoCycle" `
    "./statFactors/LifespanFactor" "8"
Assert-GeneValue "RC_Evolution_LongImagoCycle" `
    "./biologicalAgeTickFactorFromAgeCurve/points/li[1]" "(13, 0.2)"
Assert-GeneValue "RC_Evolution_LongImagoCycle" `
    "./biologicalAgeTickFactorFromAgeCurve/points/li[2]" "(18, 1)"
Assert-GeneValue "RC_Evolution_AgelessImago" `
    "./biologicalAgeTickFactorFromAgeCurve/points/li[1]" "(13, 1)"
Assert-GeneValue "RC_Evolution_AgelessImago" `
    "./biologicalAgeTickFactorFromAgeCurve/points/li[2]" "(18.5, 0)"

Assert-GeneValue "RC_Mutation_LightStride" `
    "./conditionalStatAffecters/li[@Class='VanillaGenesExpanded.ConditionalStatAffecter_Darkness']/statFactors/MoveSpeed" "0.5"
Assert-GeneValue "RC_Mutation_LightStride" `
    "./conditionalStatAffecters/li[@Class='VanillaGenesExpanded.ConditionalStatAffecter_InLight']/statFactors/MoveSpeed" "1.5"
Assert-GeneValue "RC_Mutation_TwilightStride" `
    "./conditionalStatAffecters/li[@Class='VanillaGenesExpanded.ConditionalStatAffecter_Darkness']/statFactors/MoveSpeed" "1.5"
Assert-GeneValue "RC_Mutation_TwilightStride" `
    "./conditionalStatAffecters/li[@Class='VanillaGenesExpanded.ConditionalStatAffecter_InLight']/statFactors/MoveSpeed" "0.5"

$solarOverdrive = Get-Gene "RC_Mutation_SolarOverdrive"
Assert-NodeValue $solarOverdrive `
    "./conditionalStatAffecters/li[@Class='ConditionalStatAffecter_InSunlight']/statFactors/MoveSpeed" "1.3" "RC_Mutation_SolarOverdrive"
Assert-NodeValue $solarOverdrive `
    "./conditionalStatAffecters/li[@Class='ConditionalStatAffecter_InSunlight']/statFactors/WorkSpeedGlobal" "1.15" "RC_Mutation_SolarOverdrive"
Assert-NodeValue $solarOverdrive `
    "./conditionalStatAffecters/li[@Class='ConditionalStatAffecter_InSunlight']/statFactors/RestFallRateFactor" "0.8" "RC_Mutation_SolarOverdrive"
Assert-NodeValue $solarOverdrive `
    "./conditionalStatAffecters/li[@Class='VanillaGenesExpanded.ConditionalStatAffecter_NoSunlight']/statFactors/MoveSpeed" "0.5" "RC_Mutation_SolarOverdrive"
Assert-NodeValue $solarOverdrive `
    "./conditionalStatAffecters/li[@Class='VanillaGenesExpanded.ConditionalStatAffecter_NoSunlight']/statFactors/WorkSpeedGlobal" "0.8" "RC_Mutation_SolarOverdrive"
Assert-NodeValue $solarOverdrive `
    "./conditionalStatAffecters/li[@Class='VanillaGenesExpanded.ConditionalStatAffecter_NoSunlight']/statFactors/RestFallRateFactor" "1.25" "RC_Mutation_SolarOverdrive"

$antenna = Get-Hediff "RC_SwarmSensoryAntenna"
Assert-NodeValue $antenna "./stages/li/painOffset" "0.00675" "RC_SwarmSensoryAntenna"
Assert-NodeValue $antenna "./stages/li/statOffsets/PawnBeauty" "-1" "RC_SwarmSensoryAntenna"
$antennaParts = @((Get-Gene "RC_Mutation_SwarmSensoryCrown").SelectNodes(
    "./modExtensions/li/hediffsToBodyParts/li/bodyparts/li"
))
Assert-True ($antennaParts.Count -eq 2) (
    "RC_Mutation_SwarmSensoryCrown must target two Ear parts"
)
Assert-True (
    @($antennaParts | Where-Object { $_.InnerText -eq "Ear" }).Count -eq 2
) "RC_Mutation_SwarmSensoryCrown body parts must both be Ear"

$mild = Get-Hediff "RC_MildPhotophobia"
Assert-NodeValue $mild "./stages/li[2]/capMods/li/offset" "-0.1" "RC_MildPhotophobia"
$stupor = Get-Hediff "RC_SolarStupor"
Assert-NodeValue $stupor "./stages/li[3]/capMods/li/setMax" "0.1" "RC_SolarStupor"
$photosynthesis = Get-Hediff "RC_ChlorophyllMetabolism"
Assert-NodeValue $photosynthesis `
    "./stages/li[1]/hungerRateFactorOffset" "-0.9999" "RC_ChlorophyllMetabolism"
$lethal = Get-Hediff "RC_SolarDeath"
Assert-NodeValue $lethal "./comps/li/damageToInflict" "VEF_PermanentBurn" "RC_SolarDeath"
Assert-NodeValue $lethal "./comps/li/damageAmount" "15" "RC_SolarDeath"
Assert-NodeValue $lethal "./comps/li/tickInterval" "500" "RC_SolarDeath"
Assert-NodeValue $lethal "./comps/li/sunlightBurns" "true" "RC_SolarDeath"

Assert-NodeValue (Get-Thought "RC_MildPhotophobiaThought") `
    "./stages/li/baseMoodEffect" "-3" "RC_MildPhotophobiaThought"
Assert-NodeValue (Get-Thought "RC_SolarVulnerabilityThought") `
    "./stages/li/baseMoodEffect" "-6" "RC_SolarVulnerabilityThought"
Assert-NodeValue (Get-Thought "RC_SolarExhaustionThought") `
    "./stages/li/baseMoodEffect" "-12" "RC_SolarExhaustionThought"

$earlyMaturity = Get-Gene "RC_Evolution_AcceleratedBroodMaturity"
Assert-True (
    $earlyMaturity.GetAttribute("MayRequire") -eq
        "CarbineAction.HSK.VRE.Archon"
) "Early maturity must require the installed HSK VRE Archon package"

$expectedWorkAges = [ordered]@{
    Firefighter = "4"
    Patient = "0"
    Doctor = "7"
    PatientBedRest = "0"
    Childcare = "0"
    BasicWorker = "3"
    Warden = "7"
    Handling = "4"
    Cooking = "4"
    Hunting = "4"
    Construction = "7"
    Growing = "4"
    Mining = "4"
    PlantCutting = "4"
    Smithing = "10"
    Tailoring = "4"
    Art = "7"
    Hauling = "3"
    Cleaning = "3"
    Research = "10"
}
foreach ($entry in $expectedWorkAges.GetEnumerator()) {
    Assert-NodeValue $earlyMaturity (
        "./modExtensions/li[@Class='VREArchon.LifeStageWorkSettingsExtension']/lifeStageWorkSettings/$($entry.Key)"
    ) $entry.Value "RC_Evolution_AcceleratedBroodMaturity"
}

$conditionalAbilities = [ordered]@{
    RC_Evolution_MatriarchWail = @("sarg.alphagenes", "AG_BansheeScream")
    RC_Evolution_PsiMimicry = @(
        "sarg.alphagenes,Ludeon.RimWorld.Royalty",
        "AG_Invisibility"
    )
    RC_Evolution_InsanityPulse = @(
        "sarg.alphagenes",
        "AG_InsanityBlast"
    )
}
foreach ($entry in $conditionalAbilities.GetEnumerator()) {
    $gene = Get-Gene $entry.Key
    Assert-True (
        $gene.GetAttribute("MayRequire") -eq $entry.Value[0]
    ) "$($entry.Key) has an unsafe or incorrect MayRequire"
    Assert-NodeValue $gene "./abilities/li" $entry.Value[1] $entry.Key
}

$sourceText = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$curiosityMappings = [regex]::Matches(
    $sourceText,
    '\{\s*"RC_Evolution_Curiosity[A-Za-z]+",\s*"[A-Za-z]+"\s*\}'
)
Assert-True ($curiosityMappings.Count -eq 12) (
    "Stage2Effects.cs must contain exactly 12 curiosity mappings"
)
foreach ($requiredText in @(
    "xp <= 0f",
    "xp * 0.001f",
    "Gaming_Cerebral",
    "VRE_Curiosity_",
    "ThoughtWorker_Dark",
    "RC_SwarmSensoryAntenna",
    "hediffSet.HasHediff",
    "ThoughtState.Inactive",
    "Priority.Last"
)) {
    Assert-True ($sourceText.Contains($requiredText)) (
        "Stage2Effects.cs is missing required behavior: $requiredText"
    )
}

$projectText = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
Assert-True ($projectText.Contains(
    '<Compile Include="Stage2Effects.cs" />'
)) "Stage2Effects.cs is not included in the project"

$cleanupText = Get-Content -LiteralPath $cleanupPath -Raw -Encoding UTF8
foreach ($requiredText in @(
    "RestoreAbilitiesGrantedByCurrentGenes",
    "GetAbility(",
    "GainAbility("
)) {
    Assert-True ($cleanupText.Contains($requiredText)) (
        "GeneReferenceCleanup.cs is missing ability lifecycle support: " +
        $requiredText
    )
}

$foreignIcons = @(
    "AG_SolarMinor",
    "AGI_AntennaIcon",
    "AG_ExtraordinaryLifespan",
    "AG_LightStrider",
    "AG_NightStrider",
    "AG_UVPoweredMajor",
    "AG_SolarUnconscious",
    "AG_SolarAnnihilation",
    "Gene_CuriosityShooting",
    "Gene_Photosynthesis"
)
$compatText = $compatXml.OuterXml
foreach ($icon in $foreignIcons) {
    Assert-True ($compatText.Contains($icon)) (
        "Conditional compatibility is missing source icon $icon"
    )
}
Assert-True ($compatText.Contains("PatchOperationFindMod")) (
    "Stage-2 foreign resources are not protected by PatchOperationFindMod"
)
foreach ($modName in @(
    "Alpha Genes",
    "VRE Genie (HSK/CE Patched)",
    "VRE Phytokin (HSK/CE Patched)"
)) {
    Assert-True ($compatText.Contains($modName)) (
        "PatchOperationFindMod must use the installed display name: $modName"
    )
}
foreach ($packageId in @(
    "sarg.alphagenes",
    "CarbineAction.HSK.VRE.Genie",
    "vanillaracesexpanded.phytokin"
)) {
    Assert-True (-not $compatText.Contains("<li>$packageId</li>")) (
        "PatchOperationFindMod does not match packageId values: $packageId"
    )
}

$stage1CompatText = $stage1CompatXml.OuterXml
foreach ($modName in @("Alpha Genes", "HSK more content")) {
    Assert-True ($stage1CompatText.Contains($modName)) (
        "Stage-1 PatchOperationFindMod must use the installed name: $modName"
    )
}
foreach ($packageId in @("sarg.alphagenes", "arpomo6.hmc.project")) {
    Assert-True (-not $stage1CompatText.Contains("<li>$packageId</li>")) (
        "Stage-1 PatchOperationFindMod still uses packageId: $packageId"
    )
}

$xmlFiles = Get-ChildItem -LiteralPath $ModRoot -Recurse -Filter "*.xml"
foreach ($file in $xmlFiles) {
    try {
        [xml](Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8) |
            Out-Null
    }
    catch {
        throw "Malformed XML: $($file.FullName): $($_.Exception.Message)"
    }
}

Assert-True (Test-Path -LiteralPath $iconPath) (
    "Local fallback icon is missing: $iconPath"
)
Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($iconPath)
try {
    Assert-True (
        $image.Width -eq 256 -and $image.Height -eq 256
    ) "Fallback icon must be 256x256, got $($image.Width)x$($image.Height)"
}
finally {
    $image.Dispose()
}

Write-Output (
    "Stage 2 validation passed: 28 genes (19 evolutions, 9 mutations), " +
    "12 exclusive curiosity variants, exact sunlight/aging/ability data, " +
    "all XML files parse, and the fallback icon is 256x256."
)
