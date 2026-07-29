using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    public static class RC_SunlightUtility
    {
        public static bool IsDirectSunlight(Pawn pawn)
        {
            return pawn != null &&
                pawn.Spawned &&
                pawn.Position.InSunlight(pawn.Map);
        }
    }

    public static class RC_MetapodUtility
    {
        private const string LogPrefix = "[RedCrow Metapods]";

        public static bool TryCreateForPawn(
            Pawn pawn,
            ThingDef metapodDef,
            RC_MetapodTransformationData data,
            out RC_MetapodBase metapod,
            out string reason,
            bool allowUnspawned = false,
            IntVec3? positionOverride = null,
            Map mapOverride = null)
        {
            metapod = null;
            reason = null;
            if (pawn == null || metapodDef == null)
            {
                reason = "RC_Metapod_MissingDefinition".Translate();
                return false;
            }

            Map map = mapOverride ?? pawn.MapHeld;
            IntVec3 position = positionOverride ??
                pawn.PositionHeld;
            if (map == null || !position.IsValid)
            {
                reason = "RC_Metapod_PawnUnavailable".Translate();
                return false;
            }

            metapod =
                ThingMaker.MakeThing(metapodDef) as RC_MetapodBase;
            if (metapod == null)
            {
                reason = "RC_Metapod_MissingDefinition".Translate();
                return false;
            }

            GenSpawn.Spawn(metapod, position, map);
            if (metapod.TryAcceptPawn(
                pawn,
                data,
                out reason,
                allowUnspawned))
            {
                Log.Message(
                    LogPrefix + " Created " +
                    metapodDef.defName + " for " +
                    pawn.LabelShort + "; mode=" +
                    data.mode + ".");
                return true;
            }

            metapod.Destroy(DestroyMode.Vanish);
            metapod = null;
            return false;
        }

        public static bool HasFullJellyResource(
            Pawn pawn,
            out Gene_Resource resource)
        {
            resource = Stage4Effects.FindJellyResource(pawn);
            return resource != null &&
                resource.Max > 0f &&
                resource.Value >= resource.Max - 0.0001f;
        }
    }

    public static class RC_PawnTransformationUtility
    {
        private const string LogPrefix =
            "[RedCrow Transformations]";
        private const string GenelineDefTypeName =
            "VanillaRacesExpandedInsector.GenelineGeneDef";
        private const string GenelineTrackerTypeName =
            "VanillaRacesExpandedInsector.Gene_GenelineEvolution";

        public static RC_MetapodTransformationData
            CaptureSourceSnapshot(
                Pawn source,
                RC_MetapodMode mode,
                AbilityDef sourceAbility)
        {
            RC_MetapodTransformationData data =
                new RC_MetapodTransformationData
                {
                    mode = mode,
                    startedAtTick = Find.TickManager.TicksGame,
                    sourceAbility = sourceAbility,
                    sourceRace = source != null ? source.def : null,
                    sourceKind = source != null
                        ? source.kindDef
                        : null,
                    sourceFaction = source != null
                        ? source.Faction
                        : null,
                    sourceIdeo = source != null &&
                        source.ideo != null
                            ? source.Ideo
                            : null
                };

            if (source == null || source.genes == null)
            {
                return data;
            }

            data.sourceXenotype = source.genes.Xenotype;
            data.sourceXenotypeName =
                source.genes.xenotypeName;
            data.sourceXenotypeIcon = source.genes.iconDef;
            data.sourceHybrid = source.genes.hybrid;

            foreach (Gene gene in
                source.genes.GenesListForReading)
            {
                if (gene == null ||
                    gene.def == null ||
                    IsGenelineGeneDef(gene.def))
                {
                    continue;
                }

                data.ordinaryGenes.Add(new RC_GeneSnapshot
                {
                    geneDef = gene.def,
                    xenogene = source.genes.IsXenogene(gene)
                });
            }

            return data;
        }

        public static Pawn ApplyTransformation(
            Pawn pawn,
            RC_MetapodTransformationData data)
        {
            if (pawn == null || data == null)
            {
                return pawn;
            }

            if (data.transformationApplied)
            {
                return pawn;
            }

            bool succeeded;
            switch (data.mode)
            {
                case RC_MetapodMode.Usurpation:
                    succeeded = ApplyUsurpation(pawn, data);
                    break;
                case RC_MetapodMode.CorpseMemory:
                    succeeded = ApplyCorpseMemory(pawn, data);
                    break;
                case RC_MetapodMode.LarvalRebirth:
                    succeeded = ApplyAgeRenewal(pawn, 0);
                    break;
                case RC_MetapodMode.PerfectImago:
                    succeeded = ApplyAgeRenewal(pawn, 20);
                    break;
                default:
                    succeeded = false;
                    break;
            }

            if (!succeeded)
            {
                Log.Error(
                    LogPrefix + " Transformation could not be " +
                    "completed for " + pawn.LabelShort +
                    "; mode=" + data.mode + ".");
                return null;
            }

            data.transformationApplied = true;
            Log.Message(
                LogPrefix + " Completed " + data.mode +
                " for " + pawn.LabelShort + ".");
            return pawn;
        }

        public static bool CanChangeRace(
            Pawn pawn,
            ThingDef targetRace,
            out string reason)
        {
            reason = null;
            if (pawn == null ||
                targetRace == null ||
                targetRace.race == null ||
                !targetRace.race.Humanlike)
            {
                reason = "RC_Metapod_InvalidTargetRace".Translate();
                return false;
            }

            List<RC_ArtificialPartRecord> artificialParts =
                RC_MetapodHealthUtility.CaptureArtificialParts(pawn);
            string missingPath;
            if (!RC_MetapodHealthUtility.CanMapArtificialParts(
                artificialParts,
                targetRace.race.body,
                out missingPath))
            {
                reason =
                    "RC_Metapod_IncompatibleArtificialPart".Translate(
                        missingPath ?? "unknown");
                return false;
            }

            return true;
        }

        public static bool IsGenelineGeneDef(GeneDef def)
        {
            Type type = def != null ? def.GetType() : null;
            while (type != null)
            {
                if (type.FullName == GenelineDefTypeName)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool ApplyUsurpation(
            Pawn pawn,
            RC_MetapodTransformationData data)
        {
            if (!PrepareRaceAndHealth(pawn, data))
            {
                return false;
            }

            ClearGeneline(pawn);
            ReplaceGenesFromSnapshot(pawn, data);
            ApplyFactionAndIdeo(pawn, data);
            FinalizePawnAfterTransformation(pawn);
            return true;
        }

        private static bool ApplyCorpseMemory(
            Pawn pawn,
            RC_MetapodTransformationData data)
        {
            if (!PrepareRaceAndHealth(pawn, data))
            {
                return false;
            }

            XenotypeDef insector =
                data.sourceXenotype ??
                DefDatabase<XenotypeDef>.GetNamedSilentFail(
                    "VRE_Insector");
            if (pawn.genes == null || insector == null)
            {
                return false;
            }

            ClearGeneline(pawn);
            RemoveAllGenes(pawn);
            pawn.genes.SetXenotype(insector);
            ApplyFactionAndIdeo(pawn, data);

            HediffDef solarCondition =
                DefDatabase<HediffDef>.GetNamedSilentFail(
                    "RC_SolarStuporCondition");
            if (solarCondition == null)
            {
                return false;
            }

            if (!pawn.health.hediffSet.HasHediff(
                solarCondition,
                false))
            {
                pawn.health.AddHediff(solarCondition);
            }

            if (!EnsureRequiredTrait(pawn, "CatInHead") ||
                !EnsureRequiredTrait(pawn, "Bipolar"))
            {
                return false;
            }

            FinalizePawnAfterTransformation(pawn);
            return true;
        }

        private static bool ApplyAgeRenewal(
            Pawn pawn,
            int biologicalAgeYears)
        {
            RC_MetapodHealthUtility.CleanForTransformation(pawn);
            pawn.ageTracker.AgeBiologicalTicks =
                (long)biologicalAgeYears * GenDate.TicksPerYear;
            FinalizePawnAfterTransformation(pawn);
            return true;
        }

        private static bool PrepareRaceAndHealth(
            Pawn pawn,
            RC_MetapodTransformationData data)
        {
            string reason;
            if (!CanChangeRace(
                pawn,
                data.sourceRace,
                out reason))
            {
                Log.Error(
                    LogPrefix + " " + reason +
                    " Pawn=" + pawn.LabelShort + ".");
                return false;
            }

            RC_MetapodHealthUtility.CleanForTransformation(pawn);
            List<Hediff> preservedPartHediffs =
                pawn.health.hediffSet.hediffs
                    .Where(hediff => hediff.Part != null)
                    .ToList();
            List<RC_ArtificialPartRecord> records =
                preservedPartHediffs.Select(
                    hediff =>
                        new RC_ArtificialPartRecord
                        {
                            hediffDef = hediff.def,
                            bodyPartDef = hediff.Part != null
                                ? hediff.Part.def
                                : null,
                            bodyPartPath =
                                RC_BodyPartPathUtility.GetPath(
                                    hediff.Part),
                            groups = hediff.Part != null &&
                                hediff.Part.groups != null
                                    ? hediff.Part.groups.ToList()
                                    : new List<BodyPartGroupDef>()
                        })
                    .ToList();

            if (pawn.def != data.sourceRace)
            {
                pawn.def = data.sourceRace;
                if (data.sourceKind != null &&
                    data.sourceKind.race == data.sourceRace)
                {
                    pawn.kindDef = data.sourceKind;
                }
                else if (pawn.kindDef == null ||
                    pawn.kindDef.race != data.sourceRace)
                {
                    pawn.kindDef =
                        DefDatabase<PawnKindDef>
                            .AllDefsListForReading
                            .FirstOrDefault(
                                kind =>
                                    kind.race ==
                                    data.sourceRace);
                }

                if (pawn.kindDef == null)
                {
                    return false;
                }

                for (int index = 0;
                    index < preservedPartHediffs.Count;
                    index++)
                {
                    BodyPartRecord target =
                        RC_MetapodHealthUtility
                            .ResolveCompatiblePart(
                                data.sourceRace.race.body,
                                records[index]);
                    if (target == null)
                    {
                        return false;
                    }

                    preservedPartHediffs[index].Part = target;
                }

                if (!ReinitializeRaceComps(pawn))
                {
                    return false;
                }

                PawnComponentsUtility
                    .AddAndRemoveDynamicComponents(pawn);
                pawn.health.Notify_HediffChanged(null);
            }

            return true;
        }

        private static bool ReinitializeRaceComps(Pawn pawn)
        {
            try
            {
                FieldInfo compsField =
                    typeof(ThingWithComps).GetField(
                        "comps",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                if (compsField == null)
                {
                    Log.Error(
                        LogPrefix +
                        " Could not locate ThingWithComps.comps " +
                        "while changing pawn race.");
                    return false;
                }

                compsField.SetValue(pawn, null);
                pawn.InitializeComps();
                return true;
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix +
                    " Failed to rebuild race components for " +
                    pawn.LabelShort + ": " + exception);
                return false;
            }
        }

        private static void ReplaceGenesFromSnapshot(
            Pawn pawn,
            RC_MetapodTransformationData data)
        {
            if (pawn.genes == null)
            {
                return;
            }

            RemoveAllGenes(pawn);
            pawn.genes.SetXenotypeDirect(
                data.sourceXenotype ?? XenotypeDefOf.Baseliner);
            pawn.genes.xenotypeName =
                data.sourceXenotypeName;
            pawn.genes.iconDef = data.sourceXenotypeIcon;
            pawn.genes.hybrid = data.sourceHybrid;

            foreach (RC_GeneSnapshot snapshot in
                data.ordinaryGenes ?? new List<RC_GeneSnapshot>())
            {
                if (snapshot != null &&
                    snapshot.geneDef != null &&
                    !IsGenelineGeneDef(snapshot.geneDef))
                {
                    pawn.genes.AddGene(
                        snapshot.geneDef,
                        snapshot.xenogene);
                }
            }
        }

        private static void RemoveAllGenes(Pawn pawn)
        {
            if (pawn == null || pawn.genes == null)
            {
                return;
            }

            foreach (Gene gene in
                pawn.genes.GenesListForReading.ToList())
            {
                pawn.genes.RemoveGene(gene);
            }
        }

        private static void ClearGeneline(Pawn pawn)
        {
            if (pawn == null || pawn.genes == null)
            {
                return;
            }

            foreach (Gene gene in
                pawn.genes.GenesListForReading.ToList())
            {
                if (gene == null || gene.def == null)
                {
                    continue;
                }

                if (IsGenelineGeneDef(gene.def))
                {
                    pawn.genes.RemoveGene(gene);
                    continue;
                }

                Type type = gene.GetType();
                if (type.FullName != GenelineTrackerTypeName)
                {
                    continue;
                }

                FieldInfo field = type.GetField(
                    "geneline",
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);
                object geneline = field != null
                    ? field.GetValue(gene)
                    : null;
                if (geneline == null)
                {
                    continue;
                }

                MethodInfo remove = geneline.GetType().GetMethod(
                    "RemovePawnDirectly",
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);
                if (remove != null)
                {
                    remove.Invoke(
                        geneline,
                        new object[] { pawn, gene });
                }
                else
                {
                    field.SetValue(gene, null);
                }
            }
        }

        private static void ApplyFactionAndIdeo(
            Pawn pawn,
            RC_MetapodTransformationData data)
        {
            if (pawn.Faction != data.sourceFaction)
            {
                pawn.SetFaction(data.sourceFaction);
            }

            if (data.sourceKind != null &&
                data.sourceKind.race == pawn.def)
            {
                pawn.kindDef = data.sourceKind;
            }

            if (pawn.ideo != null && data.sourceIdeo != null)
            {
                pawn.ideo.SetIdeo(data.sourceIdeo);
                pawn.ideo.OffsetCertainty(1f);
            }
        }

        private static bool EnsureRequiredTrait(
            Pawn pawn,
            string defName)
        {
            TraitDef traitDef =
                DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (traitDef == null ||
                pawn == null ||
                pawn.story == null ||
                pawn.story.traits == null)
            {
                return false;
            }

            if (pawn.story.traits.allTraits.Any(
                trait =>
                    trait.def == traitDef &&
                    trait.Degree == 0))
            {
                return true;
            }

            pawn.story.traits.GainTrait(
                new Trait(traitDef, 0, true),
                true);
            return pawn.story.traits.allTraits.Any(
                trait =>
                    trait.def == traitDef &&
                    trait.Degree == 0);
        }

        private static void FinalizePawnAfterTransformation(
            Pawn pawn)
        {
            if (pawn.needs != null)
            {
                pawn.needs.AddOrRemoveNeedsAsAppropriate();
            }
            pawn.Notify_DisabledWorkTypesChanged();
            if (pawn.Drawer != null &&
                pawn.Drawer.renderer != null)
            {
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
        }
    }

    public class HediffCompProperties_UsurpationLarva :
        HediffCompProperties
    {
        public ThingDef metapodDef;
        public HediffDef comaDef;
        public int incubationTicks = 5 * GenDate.TicksPerDay;
        public int comaTicks = GenDate.TicksPerDay;

        public HediffCompProperties_UsurpationLarva()
        {
            compClass = typeof(HediffComp_UsurpationLarva);
        }
    }

    public class HediffComp_UsurpationLarva : HediffComp
    {
        private int infectionTicks;
        private int nextPodRetryTick;
        private bool comaStarted;
        private bool podCreated;

        public RC_MetapodTransformationData transformationData;

        private HediffCompProperties_UsurpationLarva Props
        {
            get
            {
                return
                    (HediffCompProperties_UsurpationLarva)props;
            }
        }

        public override void CompPostTick(
            ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = parent.pawn;
            if (pawn == null ||
                pawn.Dead ||
                transformationData == null ||
                podCreated)
            {
                return;
            }

            infectionTicks++;
            parent.Severity = Math.Min(
                1f,
                (float)infectionTicks /
                Math.Max(1, Props.incubationTicks));

            if (!comaStarted &&
                infectionTicks >= Props.incubationTicks)
            {
                StartComa(pawn);
            }

            if (infectionTicks >=
                    Props.incubationTicks + Props.comaTicks &&
                pawn.Spawned &&
                Find.TickManager.TicksGame >= nextPodRetryTick)
            {
                TryEnterMetapod(pawn);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(
                ref infectionTicks,
                "infectionTicks");
            Scribe_Values.Look(
                ref nextPodRetryTick,
                "nextPodRetryTick");
            Scribe_Values.Look(
                ref comaStarted,
                "comaStarted");
            Scribe_Values.Look(
                ref podCreated,
                "podCreated");
            Scribe_Deep.Look(
                ref transformationData,
                "transformationData");
        }

        private void StartComa(Pawn pawn)
        {
            comaStarted = true;
            transformationData.comaStartedAtTick =
                Find.TickManager.TicksGame;
            transformationData.comaEndsAtTick =
                Find.TickManager.TicksGame + Props.comaTicks;
            if (Props.comaDef != null &&
                !pawn.health.hediffSet.HasHediff(
                    Props.comaDef,
                    false))
            {
                pawn.health.AddHediff(Props.comaDef);
            }
        }

        private void TryEnterMetapod(Pawn pawn)
        {
            string raceReason;
            if (!RC_PawnTransformationUtility.CanChangeRace(
                pawn,
                transformationData.sourceRace,
                out raceReason))
            {
                nextPodRetryTick =
                    Find.TickManager.TicksGame + 2500;
                Messages.Message(
                    raceReason,
                    pawn,
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            transformationData.artificialParts =
                RC_MetapodHealthUtility
                    .CaptureArtificialParts(pawn);
            RC_MetapodBase pod;
            string reason;
            if (!RC_MetapodUtility.TryCreateForPawn(
                pawn,
                Props.metapodDef,
                transformationData,
                out pod,
                out reason))
            {
                nextPodRetryTick =
                    Find.TickManager.TicksGame + 2500;
                Log.Error(
                    "[RedCrow Usurpation] Automatic metapod " +
                    "entry failed for " + pawn.LabelShort +
                    ": " + reason);
                return;
            }

            podCreated = true;
            Hediff coma = Props.comaDef != null
                ? pawn.health.hediffSet.GetFirstHediffOfDef(
                    Props.comaDef,
                    false)
                : null;
            if (coma != null)
            {
                pawn.health.RemoveHediff(coma);
            }

            pawn.health.RemoveHediff(parent);
        }
    }

    public class HediffCompProperties_SolarStuporCondition :
        HediffCompProperties
    {
        public HediffCompProperties_SolarStuporCondition()
        {
            compClass =
                typeof(HediffComp_SolarStuporCondition);
        }
    }

    public class HediffComp_SolarStuporCondition : HediffComp
    {
        public override void CompPostTick(
            ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (parent.pawn != null &&
                parent.pawn.IsHashIntervalTick(60))
            {
                parent.Severity =
                    RC_SunlightUtility.IsDirectSunlight(
                        parent.pawn)
                            ? 1f
                            : 0.1f;
            }
        }
    }

    public abstract class RC_AnnualMetapodAbilityEffect :
        CompAbilityEffect
    {
        protected int nextUseTick;

        protected bool CooldownReady
        {
            get
            {
                return Find.TickManager.TicksGame >= nextUseTick;
            }
        }

        public override bool CanCast
        {
            get
            {
                return base.CanCast;
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason))
            {
                return true;
            }

            if (!CooldownReady)
            {
                reason = "RC_Metapod_AnnualCooldown".Translate(
                    Math.Max(
                        0,
                        nextUseTick -
                        Find.TickManager.TicksGame)
                        .ToStringTicksToPeriod());
                return true;
            }

            Gene_Resource resource;
            if (!RC_MetapodUtility.HasFullJellyResource(
                parent.pawn,
                out resource))
            {
                reason =
                    "RC_Metapod_FullJellyRequired".Translate();
                return true;
            }

            reason = null;
            return false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(
                ref nextUseTick,
                "nextUseTick");
        }

        protected void CommitAnnualUse(
            Gene_Resource resource,
            RC_MetapodTransformationData data)
        {
            resource.Value -= Math.Min(1f, resource.Value);
            nextUseTick =
                Find.TickManager.TicksGame + GenDate.TicksPerYear;
            if (data != null)
            {
                data.costPaid = true;
                data.cooldownStartedAtTick =
                    Find.TickManager.TicksGame;
            }
        }

        protected static bool Reject(
            Thing target,
            string reason,
            bool throwMessages)
        {
            if (throwMessages)
            {
                Messages.Message(
                    reason,
                    target,
                    MessageTypeDefOf.RejectInput,
                    false);
            }

            return false;
        }
    }

    public class CompProperties_AbilityUsurpation :
        CompProperties_AbilityEffect
    {
        public HediffDef infectionDef;

        public CompProperties_AbilityUsurpation()
        {
            compClass =
                typeof(CompAbilityEffect_Usurpation);
        }
    }

    public class CompAbilityEffect_Usurpation :
        RC_AnnualMetapodAbilityEffect
    {
        private new CompProperties_AbilityUsurpation Props
        {
            get
            {
                return
                    (CompProperties_AbilityUsurpation)props;
            }
        }

        public override bool Valid(
            LocalTargetInfo target,
            bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;
            if (victim == null ||
                victim == caster ||
                victim.Dead ||
                victim.RaceProps == null ||
                !victim.RaceProps.Humanlike ||
                !victim.RaceProps.IsFlesh ||
                victim.RaceProps.IsMechanoid ||
                victim.genes == null)
            {
                return Reject(
                    target.Thing,
                    "RC_Usurpation_InvalidTarget".Translate(),
                    throwMessages);
            }

            if (Props.infectionDef == null ||
                victim.health.hediffSet.HasHediff(
                    Props.infectionDef,
                    false))
            {
                return Reject(
                    victim,
                    "RC_Usurpation_AlreadyInfected".Translate(),
                    throwMessages);
            }

            if (victim.HostileTo(caster) &&
                !victim.IsPrisoner &&
                !victim.Downed &&
                victim.health.capacities.CapableOf(
                    PawnCapacityDefOf.Consciousness))
            {
                return Reject(
                    victim,
                    "RC_Usurpation_TargetCanResist".Translate(),
                    throwMessages);
            }

            string reason;
            if (!RC_PawnTransformationUtility.CanChangeRace(
                victim,
                caster.def,
                out reason))
            {
                return Reject(victim, reason, throwMessages);
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(
            LocalTargetInfo target,
            LocalTargetInfo dest)
        {
            Pawn victim = target.Pawn;
            Gene_Resource resource;
            if (victim == null ||
                !CooldownReady ||
                !Valid(target, false) ||
                !RC_MetapodUtility.HasFullJellyResource(
                    parent.pawn,
                    out resource))
            {
                return;
            }

            RC_MetapodTransformationData data =
                RC_PawnTransformationUtility
                    .CaptureSourceSnapshot(
                        parent.pawn,
                        RC_MetapodMode.Usurpation,
                        parent.def);
            data.artificialParts =
                RC_MetapodHealthUtility
                    .CaptureArtificialParts(victim);

            Hediff infection =
                victim.health.AddHediff(
                    Props.infectionDef);
            HediffComp_UsurpationLarva comp =
                infection.TryGetComp<
                    HediffComp_UsurpationLarva>();
            if (comp == null)
            {
                victim.health.RemoveHediff(infection);
                return;
            }

            comp.transformationData = data;
            base.Apply(target, dest);
            CommitAnnualUse(resource, data);

            Messages.Message(
                "RC_Usurpation_Implanted".Translate(
                    victim.Named("PAWN")),
                victim,
                MessageTypeDefOf.NegativeEvent,
                false);
        }
    }

    public class CompProperties_AbilityCorpseMemory :
        CompProperties_AbilityEffect
    {
        public ThingDef metapodDef;
        public ThingDef resultRace;
        public XenotypeDef resultXenotype;
        public string requiredTraitA = "CatInHead";
        public string requiredTraitB = "Bipolar";

        public CompProperties_AbilityCorpseMemory()
        {
            compClass =
                typeof(CompAbilityEffect_CorpseMemory);
        }
    }

    public class CompAbilityEffect_CorpseMemory :
        RC_AnnualMetapodAbilityEffect
    {
        private const string HmcPackageId =
            "arpomo6.hmc.project";

        private new CompProperties_AbilityCorpseMemory Props
        {
            get
            {
                return
                    (CompProperties_AbilityCorpseMemory)props;
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (!RequiredDefsLoaded())
            {
                reason =
                    "RC_CorpseMemory_HMCRequired".Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(
            LocalTargetInfo target,
            bool throwMessages = false)
        {
            Corpse corpse = target.Thing as Corpse;
            Pawn pawn = corpse != null
                ? corpse.InnerPawn
                : null;
            if (corpse == null ||
                pawn == null ||
                !corpse.Spawned ||
                corpse.GetRotStage() != RotStage.Fresh ||
                pawn.RaceProps == null ||
                !pawn.RaceProps.Humanlike ||
                !pawn.RaceProps.IsFlesh ||
                pawn.RaceProps.IsMechanoid)
            {
                return Reject(
                    target.Thing,
                    "RC_CorpseMemory_InvalidCorpse".Translate(),
                    throwMessages);
            }

            if (pawn.health == null ||
                pawn.health.hediffSet.GetBrain() == null)
            {
                return Reject(
                    corpse,
                    "RC_CorpseMemory_BrainRequired".Translate(),
                    throwMessages);
            }

            string reason;
            if (!RC_PawnTransformationUtility.CanChangeRace(
                pawn,
                Props.resultRace,
                out reason))
            {
                return Reject(corpse, reason, throwMessages);
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(
            LocalTargetInfo target,
            LocalTargetInfo dest)
        {
            Corpse corpse = target.Thing as Corpse;
            Pawn pawn = corpse != null
                ? corpse.InnerPawn
                : null;
            Gene_Resource resource;
            if (pawn == null ||
                !CooldownReady ||
                !RequiredDefsLoaded() ||
                !Valid(target, false) ||
                !RC_MetapodUtility.HasFullJellyResource(
                    parent.pawn,
                    out resource))
            {
                return;
            }

            IntVec3 position = corpse.Position;
            Map map = corpse.Map;
            RC_MetapodTransformationData data =
                new RC_MetapodTransformationData
                {
                    mode = RC_MetapodMode.CorpseMemory,
                    startedAtTick = Find.TickManager.TicksGame,
                    sourceAbility = parent.def,
                    sourceRace = Props.resultRace,
                    sourceXenotype = Props.resultXenotype,
                    sourceFaction = parent.pawn.Faction,
                    sourceIdeo = parent.pawn.ideo != null
                        ? parent.pawn.Ideo
                        : null,
                    artificialParts =
                        RC_MetapodHealthUtility
                            .CaptureArtificialParts(pawn)
                };

            RC_MetapodBase pod =
                ThingMaker.MakeThing(
                    Props.metapodDef) as RC_MetapodBase;
            if (pod == null)
            {
                return;
            }

            GenSpawn.Spawn(pod, position, map);
            bool resurrected =
                ResurrectionUtility.TryResurrect(
                    pawn,
                    new ResurrectionParams
                    {
                        dontSpawn = true,
                        restoreMissingParts = false,
                        gettingScarsChance = 0f,
                        removeDiedThoughts = false
                    });
            if (!resurrected)
            {
                pod.Destroy(DestroyMode.Vanish);
                return;
            }

            string reason;
            if (!pod.TryAcceptPawn(
                pawn,
                data,
                out reason,
                true))
            {
                pod.Destroy(DestroyMode.Vanish);
                if (!pawn.Spawned)
                {
                    GenSpawn.Spawn(pawn, position, map);
                }
                Log.Error(
                    "[RedCrow Corpse Memory] Could not place " +
                    pawn.LabelShort + " in metapod: " + reason);
                return;
            }

            base.Apply(target, dest);
            CommitAnnualUse(resource, data);
            Messages.Message(
                "RC_CorpseMemory_Started".Translate(
                    pawn.Named("PAWN")),
                pod,
                MessageTypeDefOf.PositiveEvent,
                false);
        }

        private bool RequiredDefsLoaded()
        {
            return ModsConfig.IsActive(HmcPackageId) &&
                Props.metapodDef != null &&
                Props.resultRace != null &&
                Props.resultXenotype != null &&
                DefDatabase<TraitDef>.GetNamedSilentFail(
                    Props.requiredTraitA) != null &&
                DefDatabase<TraitDef>.GetNamedSilentFail(
                    Props.requiredTraitB) != null &&
                DefDatabase<HediffDef>.GetNamedSilentFail(
                    "RC_SolarStuporCondition") != null;
        }
    }

    public class CompProperties_AbilitySelfMetapod :
        CompProperties_AbilityEffect
    {
        public ThingDef metapodDef;
        public RC_MetapodMode mode;

        public CompProperties_AbilitySelfMetapod()
        {
            compClass =
                typeof(CompAbilityEffect_SelfMetapod);
        }
    }

    public class CompAbilityEffect_SelfMetapod :
        CompAbilityEffect
    {
        private new CompProperties_AbilitySelfMetapod Props
        {
            get
            {
                return
                    (CompProperties_AbilitySelfMetapod)props;
            }
        }

        public override bool CanCast
        {
            get
            {
                return base.CanCast;
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason))
            {
                return true;
            }

            Pawn pawn = parent.pawn;
            if (pawn == null ||
                !pawn.Spawned ||
                pawn.ParentHolder != null)
            {
                reason =
                    "RC_Metapod_PawnUnavailable".Translate();
                return true;
            }

            string mappingReason;
            if (!RC_PawnTransformationUtility.CanChangeRace(
                pawn,
                pawn.def,
                out mappingReason))
            {
                reason = mappingReason;
                return true;
            }

            reason = null;
            return false;
        }

        public override void Apply(
            LocalTargetInfo target,
            LocalTargetInfo dest)
        {
            Pawn pawn = parent.pawn;
            string mappingReason;
            if (pawn == null ||
                !CanCast ||
                !pawn.Spawned ||
                pawn.ParentHolder != null ||
                !RC_PawnTransformationUtility.CanChangeRace(
                    pawn,
                    pawn.def,
                    out mappingReason))
            {
                return;
            }

            RC_MetapodTransformationData data =
                RC_PawnTransformationUtility
                    .CaptureSourceSnapshot(
                        pawn,
                        Props.mode,
                        parent.def);
            data.artificialParts =
                RC_MetapodHealthUtility
                    .CaptureArtificialParts(pawn);

            RC_MetapodBase pod;
            string reason;
            if (!RC_MetapodUtility.TryCreateForPawn(
                pawn,
                Props.metapodDef,
                data,
                out pod,
                out reason))
            {
                Log.Error(
                    "[RedCrow Self Metapod] Could not start " +
                    Props.mode + " for " + pawn.LabelShort +
                    ": " + reason);
                return;
            }

            base.Apply(target, dest);
            Messages.Message(
                "RC_SelfMetapod_Started".Translate(
                    pawn.Named("PAWN")),
                pod,
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
