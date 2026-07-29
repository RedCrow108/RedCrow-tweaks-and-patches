using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class Stage4Effects
    {
        private const string LogPrefix = "[RedCrow Stage 4]";
        internal const string JellyResourceGeneDef =
            "VRE_InsectJellyDependency";
        internal const string SynapticNodeGeneDef =
            "RC_Evolution_HiveSynapticNode";
        internal const string SynapticBufferHediffDef =
            "RC_SynapticNodeRemovalBuffer";

        static Stage4Effects()
        {
            try
            {
                MethodInfo getMaxHealth = AccessTools.Method(
                    typeof(BodyPartDef),
                    "GetMaxHealth",
                    new[] { typeof(Pawn) });
                MethodInfo getMaxHealthPostfix = AccessTools.Method(
                    typeof(Stage4Effects),
                    "GetMaxHealthPostfix");
                MethodInfo removeGene = AccessTools.Method(
                    typeof(Pawn_GeneTracker),
                    "RemoveGene",
                    new[] { typeof(Gene) });
                MethodInfo removeGenePostfix = AccessTools.Method(
                    typeof(Stage4Effects),
                    "RemoveGenePostfix");

                if (getMaxHealth == null ||
                    getMaxHealthPostfix == null ||
                    removeGene == null ||
                    removeGenePostfix == null)
                {
                    Log.Error(
                        LogPrefix + " Patch installation failed: one or " +
                        "more health or gene lifecycle methods were not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.Stage4Effects");

                HarmonyMethod healthPostfix =
                    new HarmonyMethod(getMaxHealthPostfix);
                healthPostfix.priority = Priority.Last;
                harmony.Patch(
                    getMaxHealth,
                    postfix: healthPostfix);

                HarmonyMethod removalPostfix =
                    new HarmonyMethod(removeGenePostfix);
                removalPostfix.priority = Priority.Last;
                harmony.Patch(
                    removeGene,
                    postfix: removalPostfix);

                Log.Message(
                    LogPrefix + " Patches installed for brain max health " +
                    "and safe synaptic-node removal with Priority.Last.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GetMaxHealthPostfix(
            BodyPartDef __instance,
            Pawn pawn,
            ref float __result)
        {
            if (__instance != null &&
                __instance.defName == "Brain" &&
                (HasActiveGene(pawn, SynapticNodeGeneDef) ||
                    HasSynapticBuffer(pawn)))
            {
                __result += 10f;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void RemoveGenePostfix(
            Pawn_GeneTracker __instance,
            Gene gene)
        {
            if (__instance == null ||
                gene == null ||
                gene.def == null ||
                gene.def.defName != SynapticNodeGeneDef)
            {
                return;
            }

            PreserveDamagedBrain(__instance.pawn);
        }

        internal static bool HasActiveGene(
            Pawn pawn,
            string defName)
        {
            if (pawn == null ||
                pawn.genes == null ||
                string.IsNullOrEmpty(defName))
            {
                return false;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene != null &&
                    gene.def != null &&
                    gene.def.defName == defName &&
                    gene.Active)
                {
                    return true;
                }
            }

            return false;
        }

        internal static Gene_Resource FindJellyResource(
            Pawn pawn)
        {
            if (pawn == null || pawn.genes == null)
            {
                return null;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene != null &&
                    gene.def != null &&
                    gene.def.defName == JellyResourceGeneDef &&
                    gene.Active)
                {
                    return gene as Gene_Resource;
                }
            }

            return null;
        }

        internal static bool HasSynapticBuffer(
            Pawn pawn)
        {
            if (pawn == null ||
                pawn.health == null ||
                pawn.health.hediffSet == null)
            {
                return false;
            }

            List<Hediff> hediffs =
                pawn.health.hediffSet.hediffs;
            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff hediff = hediffs[index];
                if (hediff != null &&
                    hediff.def != null &&
                    hediff.def.defName ==
                        SynapticBufferHediffDef)
                {
                    return true;
                }
            }

            return false;
        }

        internal static ThingDef ResolveBloodDef(
            Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            if (pawn.genes != null)
            {
                List<Gene> genes =
                    pawn.genes.GenesListForReading;
                for (int geneIndex = 0;
                    geneIndex < genes.Count;
                    geneIndex++)
                {
                    Gene gene = genes[geneIndex];
                    if (gene == null ||
                        gene.def == null ||
                        !gene.Active ||
                        gene.def.modExtensions == null)
                    {
                        continue;
                    }

                    List<DefModExtension> extensions =
                        gene.def.modExtensions;
                    for (int extensionIndex = 0;
                        extensionIndex < extensions.Count;
                        extensionIndex++)
                    {
                        ThingDef customBlood =
                            GetCustomBloodDef(
                                extensions[extensionIndex]);
                        if (customBlood != null)
                        {
                            return customBlood;
                        }
                    }
                }
            }

            return pawn.RaceProps == null
                ? null
                : pawn.RaceProps.BloodDef;
        }

        internal static bool HasInsectBlood(
            Pawn pawn)
        {
            ThingDef actualBlood = ResolveBloodDef(pawn);
            if (actualBlood == null)
            {
                return false;
            }

            ThingDef vanillaInsectBlood =
                DefDatabase<ThingDef>.GetNamedSilentFail(
                    "Filth_BloodInsect");
            ThingDef vreInsectorBlood =
                DefDatabase<ThingDef>.GetNamedSilentFail(
                    "VRE_Filth_BugBlood");

            return actualBlood == vanillaInsectBlood ||
                actualBlood == vreInsectorBlood;
        }

        private static ThingDef GetCustomBloodDef(
            DefModExtension extension)
        {
            if (extension == null)
            {
                return null;
            }

            Type type = extension.GetType();
            FieldInfo field = AccessTools.Field(
                type,
                "customBloodThingDef");
            if (field != null)
            {
                ThingDef fieldValue =
                    field.GetValue(extension) as ThingDef;
                if (fieldValue != null)
                {
                    return fieldValue;
                }
            }

            PropertyInfo property = AccessTools.Property(
                type,
                "customBloodThingDef");
            if (property != null &&
                property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(
                    extension,
                    null) as ThingDef;
            }

            return null;
        }

        private static void PreserveDamagedBrain(
            Pawn pawn)
        {
            if (pawn == null ||
                pawn.health == null ||
                pawn.health.hediffSet == null ||
                pawn.Dead)
            {
                return;
            }

            BodyPartRecord brain =
                pawn.health.hediffSet.GetBrain();
            if (brain == null ||
                brain.def == null ||
                brain.def.defName != "Brain")
            {
                return;
            }

            float maximumHealth =
                brain.def.GetMaxHealth(pawn);
            float destructiveSeverity = 0f;
            List<Hediff> hediffs =
                pawn.health.hediffSet.hediffs;

            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff_Injury injury =
                    hediffs[index] as Hediff_Injury;
                if (injury != null &&
                    injury.Part == brain &&
                    injury.destroysBodyParts)
                {
                    destructiveSeverity += injury.Severity;
                }
            }

            if (destructiveSeverity <= maximumHealth - 1f)
            {
                return;
            }

            HediffDef bufferDef =
                DefDatabase<HediffDef>.GetNamedSilentFail(
                    SynapticBufferHediffDef);
            if (bufferDef == null)
            {
                Log.Error(
                    LogPrefix + " Could not preserve a damaged brain " +
                    "because " + SynapticBufferHediffDef +
                    " was not loaded.");
                return;
            }

            pawn.health.AddHediff(
                bufferDef,
                brain,
                null,
                null);

            Log.Message(
                LogPrefix + " Safe synaptic-node removal added " +
                "temporary structural support without changing injury " +
                "severity: pawn=" + pawn.LabelShort +
                "; unsupported health=" +
                Math.Max(0f, maximumHealth - destructiveSeverity)
                    .ToString("0.###") +
                "; supported health=" +
                pawn.health.hediffSet.GetPartHealth(brain) + ".");
        }
    }

    public class HediffCompProperties_SynapticNodeRemovalBuffer :
        HediffCompProperties
    {
        public HediffCompProperties_SynapticNodeRemovalBuffer()
        {
            compClass =
                typeof(HediffComp_SynapticNodeRemovalBuffer);
        }
    }

    public class HediffComp_SynapticNodeRemovalBuffer :
        HediffComp
    {
        public override void CompPostTick(
            ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent.pawn;
            if (pawn == null ||
                pawn.health == null ||
                !pawn.IsHashIntervalTick(60))
            {
                return;
            }

            if (Stage4Effects.HasActiveGene(
                pawn,
                Stage4Effects.SynapticNodeGeneDef))
            {
                pawn.health.RemoveHediff(parent);
                return;
            }

            BodyPartRecord brain = parent.Part;
            if (brain == null ||
                brain.def == null ||
                brain.def.defName != "Brain")
            {
                pawn.health.RemoveHediff(parent);
                return;
            }

            float supportedMaximum =
                brain.def.GetMaxHealth(pawn);
            float ordinaryMaximum =
                Math.Max(1f, supportedMaximum - 10f);
            float destructiveSeverity = 0f;
            List<Hediff> hediffs =
                pawn.health.hediffSet.hediffs;

            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff_Injury injury =
                    hediffs[index] as Hediff_Injury;
                if (injury != null &&
                    injury.Part == brain &&
                    injury.destroysBodyParts)
                {
                    destructiveSeverity += injury.Severity;
                }
            }

            if (destructiveSeverity <= ordinaryMaximum - 1f)
            {
                pawn.health.RemoveHediff(parent);
                Log.Message(
                    "[RedCrow Stage 4] Removed temporary synaptic " +
                    "support after ordinary brain health became safe: " +
                    "pawn=" + pawn.LabelShort + ".");
            }
        }
    }

    public class CompProperties_AbilityCoagulatingSecretion :
        CompProperties_AbilityEffect
    {
        public float resourceCost = 0.2f;
        public FloatRange tendQualityRange =
            new FloatRange(0.4f, 0.8f);

        public CompProperties_AbilityCoagulatingSecretion()
        {
            compClass =
                typeof(CompAbilityEffect_CoagulatingSecretion);
        }
    }

    public class CompAbilityEffect_CoagulatingSecretion :
        CompAbilityEffect
    {
        private new CompProperties_AbilityCoagulatingSecretion
            Props
        {
            get
            {
                return
                    (CompProperties_AbilityCoagulatingSecretion)
                    props;
            }
        }

        public override bool CanCast
        {
            get
            {
                Gene_Resource resource =
                    Stage4Effects.FindJellyResource(parent.pawn);
                return base.CanCast &&
                    resource != null &&
                    resource.Value >= Props.resourceCost;
            }
        }

        public override bool GizmoDisabled(
            out string reason)
        {
            if (base.GizmoDisabled(out reason))
            {
                return true;
            }

            Gene_Resource resource =
                Stage4Effects.FindJellyResource(parent.pawn);
            if (resource == null)
            {
                reason =
                    "Нет активного запаса инсекторного желе.";
                return true;
            }

            if (resource.Value < Props.resourceCost)
            {
                reason =
                    "Недостаточно инсекторного желе: требуется 20.";
                return true;
            }

            reason = null;
            return false;
        }

        public override bool Valid(
            LocalTargetInfo target,
            bool throwMessages = false)
        {
            Pawn targetPawn = target.Pawn;
            Pawn caster = parent.pawn;

            if (targetPawn == null)
            {
                return Reject(
                    caster,
                    "Цель должна быть живым существом.",
                    throwMessages);
            }

            if (targetPawn == caster)
            {
                return Reject(
                    targetPawn,
                    "Коагулирующий секрет нельзя применить к себе.",
                    throwMessages);
            }

            if (targetPawn.RaceProps == null ||
                targetPawn.RaceProps.IsMechanoid ||
                targetPawn.health == null)
            {
                return Reject(
                    targetPawn,
                    "У цели нет подходящей органической системы здоровья.",
                    throwMessages);
            }

            if (caster != null &&
                targetPawn.HostileTo(caster))
            {
                return Reject(
                    targetPawn,
                    "Коагулирующий секрет нельзя применить к врагу.",
                    throwMessages);
            }

            if (!Stage4Effects.HasInsectBlood(targetPawn))
            {
                return Reject(
                    targetPawn,
                    "У цели нет крови насекомого.",
                    throwMessages);
            }

            if (GetTendableWounds(targetPawn).Count == 0)
            {
                return Reject(
                    targetPawn,
                    "У цели нет ран, доступных для перевязки.",
                    throwMessages);
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(
            LocalTargetInfo target,
            LocalTargetInfo dest)
        {
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null ||
                !Valid(target, false))
            {
                return;
            }

            Gene_Resource resource =
                Stage4Effects.FindJellyResource(parent.pawn);
            if (resource == null ||
                resource.Value < Props.resourceCost)
            {
                return;
            }

            List<Hediff> wounds =
                GetTendableWounds(targetPawn);
            int tendedCount = 0;

            for (int index = 0;
                index < wounds.Count;
                index++)
            {
                Hediff wound = wounds[index];
                if (!wound.TendableNow())
                {
                    continue;
                }

                try
                {
                    wound.Tended(
                        Props.tendQualityRange.RandomInRange,
                        Props.tendQualityRange.TrueMax,
                        tendedCount + 1);
                    tendedCount++;
                }
                catch (Exception exception)
                {
                    Log.Error(
                        "[RedCrow Stage 4] Failed to tend " +
                        wound.def.defName + " on " +
                        targetPawn.LabelShort + ":\n" +
                        exception);
                }
            }

            if (tendedCount == 0)
            {
                return;
            }

            base.Apply(target, dest);
            resource.Value -= Props.resourceCost;

            Messages.Message(
                "Обработано ран: " + tendedCount + ".",
                targetPawn,
                MessageTypeDefOf.PositiveEvent,
                false);

            Log.Message(
                "[RedCrow Stage 4] Coagulating secretion tended " +
                tendedCount + " wound(s): caster=" +
                parent.pawn.LabelShort + "; target=" +
                targetPawn.LabelShort +
                "; jelly cost=" +
                Props.resourceCost.ToString("0.##") +
                "; jelly remaining=" +
                resource.Value.ToString("0.##") + ".");
        }

        private static List<Hediff> GetTendableWounds(
            Pawn pawn)
        {
            List<Hediff> wounds =
                new List<Hediff>();
            if (pawn == null ||
                pawn.health == null ||
                pawn.health.hediffSet == null)
            {
                return wounds;
            }

            List<Hediff> hediffs =
                pawn.health.hediffSet.hediffs;
            for (int index = 0;
                index < hediffs.Count;
                index++)
            {
                Hediff hediff = hediffs[index];
                if ((hediff is Hediff_Injury ||
                    hediff is Hediff_MissingPart) &&
                    hediff.TendableNow())
                {
                    wounds.Add(hediff);
                }
            }

            return wounds;
        }

        private static bool Reject(
            Pawn target,
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
}
