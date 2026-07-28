using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class Stage3Effects
    {
        private const string LogPrefix = "[RedCrow Stage 3]";
        private const string UnconstrainedGene =
            "RC_Evolution_UnconstrainedCarapace";
        private const string ThreatMarkGene =
            "RC_Mutation_ThreatMark";
        private const string DoomOmenGene =
            "RC_Mutation_DoomOmen";

        static Stage3Effects()
        {
            try
            {
                MethodInfo statOffsetFromGear = AccessTools.Method(
                    typeof(StatWorker),
                    "StatOffsetFromGear",
                    new[] { typeof(Thing), typeof(StatDef) });
                MethodInfo forceRecount = AccessTools.Method(
                    typeof(WealthWatcher),
                    "ForceRecount",
                    new[] { typeof(bool) });
                MethodInfo apparelPostfix = AccessTools.Method(
                    typeof(Stage3Effects),
                    "ApparelMoveSpeedPostfix");
                MethodInfo raidTranspiler = AccessTools.Method(
                    typeof(Stage3Effects),
                    "RaidWealthTranspiler");

                if (statOffsetFromGear == null ||
                    forceRecount == null ||
                    apparelPostfix == null ||
                    raidTranspiler == null)
                {
                    Log.Error(
                        LogPrefix + " Patch installation failed: one or " +
                        "more RimWorld targets were not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.Stage3Effects");

                HarmonyMethod apparelPatch =
                    new HarmonyMethod(apparelPostfix);
                apparelPatch.priority = Priority.Last;
                harmony.Patch(
                    statOffsetFromGear,
                    postfix: apparelPatch);

                HarmonyMethod raidPatch =
                    new HarmonyMethod(raidTranspiler);
                raidPatch.priority = Priority.Last;
                harmony.Patch(
                    forceRecount,
                    transpiler: raidPatch);

                Log.Message(
                    LogPrefix + " Apparel and raid-wealth patches " +
                    "installed with Priority.Last (" +
                    Priority.Last + ").");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void ApparelMoveSpeedPostfix(
            Thing gear,
            StatDef stat,
            ref float __result)
        {
            if (__result >= 0f ||
                stat != StatDefOf.MoveSpeed ||
                gear == null)
            {
                return;
            }

            Pawn_ApparelTracker apparelTracker =
                gear.ParentHolder as Pawn_ApparelTracker;
            Pawn pawn = apparelTracker == null
                ? null
                : apparelTracker.pawn;

            if (HasActiveGene(pawn, UnconstrainedGene))
            {
                __result = 0f;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static IEnumerable<CodeInstruction> RaidWealthTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes =
                new List<CodeInstruction>(instructions);
            MethodInfo marketValueGetter = AccessTools.PropertyGetter(
                typeof(Thing),
                "MarketValue");
            MethodInfo modifier = AccessTools.Method(
                typeof(Stage3Effects),
                "ApplyRaidPresenceMultiplier");
            bool patched = false;

            for (int index = 0; index < codes.Count; index++)
            {
                CodeInstruction instruction = codes[index];
                yield return instruction;

                if (index < 2 ||
                    !IsStoreLocal(instruction.opcode) ||
                    !Calls(codes[index - 1], marketValueGetter) ||
                    !IsLoadLocal(codes[index - 2].opcode))
                {
                    continue;
                }

                int valueLocal = GetLocalIndex(instruction);
                if (valueLocal < 0)
                {
                    continue;
                }

                CodeInstruction pawnLoad = codes[index - 2];
                yield return new CodeInstruction(
                    pawnLoad.opcode,
                    pawnLoad.operand);
                yield return CodeInstruction.LoadLocal(valueLocal);
                yield return new CodeInstruction(OpCodes.Call, modifier);
                yield return CodeInstruction.StoreLocal(valueLocal);
                patched = true;
            }

            if (!patched)
            {
                Log.Error(
                    LogPrefix + " Raid wealth transpiler could not find " +
                    "the pawn market-value contribution.");
            }
        }

        public static float ApplyRaidPresenceMultiplier(
            Pawn pawn,
            float marketValue)
        {
            if (HasActiveGene(pawn, DoomOmenGene))
            {
                return marketValue * 4f;
            }

            if (HasActiveGene(pawn, ThreatMarkGene))
            {
                return marketValue * 1.5f;
            }

            return marketValue;
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
                    gene.Active &&
                    gene.def != null &&
                    string.Equals(
                        gene.def.defName,
                        defName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Calls(
            CodeInstruction instruction,
            MethodInfo method)
        {
            return method != null &&
                instruction != null &&
                method.Equals(instruction.operand as MethodInfo);
        }

        private static bool IsLoadLocal(OpCode opcode)
        {
            return opcode == OpCodes.Ldloc ||
                opcode == OpCodes.Ldloc_S ||
                opcode == OpCodes.Ldloc_0 ||
                opcode == OpCodes.Ldloc_1 ||
                opcode == OpCodes.Ldloc_2 ||
                opcode == OpCodes.Ldloc_3;
        }

        private static bool IsStoreLocal(OpCode opcode)
        {
            return opcode == OpCodes.Stloc ||
                opcode == OpCodes.Stloc_S ||
                opcode == OpCodes.Stloc_0 ||
                opcode == OpCodes.Stloc_1 ||
                opcode == OpCodes.Stloc_2 ||
                opcode == OpCodes.Stloc_3;
        }

        private static int GetLocalIndex(
            CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Stloc_0)
            {
                return 0;
            }

            if (instruction.opcode == OpCodes.Stloc_1)
            {
                return 1;
            }

            if (instruction.opcode == OpCodes.Stloc_2)
            {
                return 2;
            }

            if (instruction.opcode == OpCodes.Stloc_3)
            {
                return 3;
            }

            LocalBuilder local = instruction.operand as LocalBuilder;
            if (local != null)
            {
                return local.LocalIndex;
            }

            if (instruction.operand is byte)
            {
                return (byte)instruction.operand;
            }

            if (instruction.operand is int)
            {
                return (int)instruction.operand;
            }

            return -1;
        }
    }

    public class HediffCompProperties_SourceAura :
        HediffCompProperties
    {
        public float radius = 6f;
        public int tickInterval = 500;
        public ThoughtDef thoughtDef;
        public GeneDef excludedGene;

        public HediffCompProperties_SourceAura()
        {
            compClass = typeof(HediffComp_SourceAura);
        }
    }

    public class HediffComp_SourceAura : HediffComp
    {
        private int tickCounter;

        private HediffCompProperties_SourceAura Props
        {
            get
            {
                return (HediffCompProperties_SourceAura)props;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(
                ref tickCounter,
                "tickCounterStage3Aura",
                0);
        }

        public override void CompPostTick(
            ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            tickCounter++;

            if (tickCounter <= Props.tickInterval)
            {
                return;
            }

            tickCounter = 0;
            Pawn source = parent == null ? null : parent.pawn;
            if (source == null ||
                source.Map == null ||
                source.Dead ||
                source.Downed ||
                Props.thoughtDef == null)
            {
                return;
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                source.Position,
                source.Map,
                Props.radius,
                true))
            {
                Pawn recipient = thing as Pawn;
                if (!CanReceiveAura(recipient, source))
                {
                    continue;
                }

                recipient.needs.mood.thoughts.memories.TryGainMemory(
                    Props.thoughtDef,
                    source,
                    null);
            }
        }

        public override void CompPostPostRemoved()
        {
            Pawn source = parent == null ? null : parent.pawn;
            ThoughtDef thoughtDef = Props.thoughtDef;

            if (source != null && thoughtDef != null)
            {
                List<Pawn> pawns =
                    PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
                for (int index = 0; index < pawns.Count; index++)
                {
                    Pawn pawn = pawns[index];
                    MemoryThoughtHandler memories =
                        pawn == null ||
                        pawn.needs == null ||
                        pawn.needs.mood == null ||
                        pawn.needs.mood.thoughts == null
                            ? null
                            : pawn.needs.mood.thoughts.memories;

                    if (memories != null)
                    {
                        memories.RemoveMemoriesOfDefWhereOtherPawnIs(
                            thoughtDef,
                            source);
                    }
                }
            }

            base.CompPostPostRemoved();
        }

        private bool CanReceiveAura(
            Pawn recipient,
            Pawn source)
        {
            if (recipient == null ||
                recipient == source ||
                recipient.Dead ||
                recipient.Downed ||
                recipient.needs == null ||
                recipient.needs.mood == null ||
                recipient.needs.mood.thoughts == null ||
                WildManUtility.AnimalOrWildMan(recipient) ||
                !recipient.RaceProps.IsFlesh ||
                recipient.GetStatValue(
                    StatDefOf.PsychicSensitivity,
                    true,
                    -1) <= 0f)
            {
                return false;
            }

            return Props.excludedGene == null ||
                recipient.genes == null ||
                !recipient.genes.HasActiveGene(Props.excludedGene);
        }
    }

    public class HediffCompProperties_SegmentRegeneration :
        HediffCompProperties
    {
        public IntRange rateInTicks =
            new IntRange(55000, 65000);
        public float healAmount = 1f;

        public HediffCompProperties_SegmentRegeneration()
        {
            compClass = typeof(HediffComp_SegmentRegeneration);
        }
    }

    public class HediffComp_SegmentRegeneration : HediffComp
    {
        private const int InitialRate = 60000;

        private static readonly HashSet<string> ValidPartDefNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Torso",
                "Shoulder",
                "Arm",
                "Hand",
                "Finger",
                "Toe",
                "Ear",
                "Head",
                "Nose",
                "Neck",
                "Leg",
                "Foot",
                "Tongue"
            };

        private int tickCounter;
        private int rate = InitialRate;

        private HediffCompProperties_SegmentRegeneration Props
        {
            get
            {
                return
                    (HediffCompProperties_SegmentRegeneration)props;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(
                ref tickCounter,
                "tickCounterSegmentRegeneration",
                0);
            Scribe_Values.Look(
                ref rate,
                "rateSegmentRegeneration",
                InitialRate);
        }

        public override void CompPostTick(
            ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            tickCounter++;

            if (tickCounter < rate)
            {
                return;
            }

            Pawn pawn = parent == null ? null : parent.pawn;
            if (pawn != null && pawn.health != null)
            {
                List<Hediff_Injury> injuries = GetInjuries(pawn);
                if (injuries.Count > 0)
                {
                    Hediff_Injury injury =
                        injuries.RandomElement();
                    injury.Severity -= Props.healAmount;
                }
                else
                {
                    RestoreLargestMissingPart(pawn);
                }
            }

            rate = Props.rateInTicks.RandomInRange;
            tickCounter = 0;
        }

        private static List<Hediff_Injury> GetInjuries(
            Pawn pawn)
        {
            List<Hediff_Injury> injuries =
                new List<Hediff_Injury>();
            List<Hediff> hediffs =
                pawn.health.hediffSet.hediffs;

            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff_Injury injury =
                    hediffs[index] as Hediff_Injury;
                if (injury != null &&
                    injury.Part != null &&
                    ValidPartDefNames.Contains(
                        injury.Part.def.defName))
                {
                    injuries.Add(injury);
                }
            }

            return injuries;
        }

        private static void RestoreLargestMissingPart(
            Pawn pawn)
        {
            BodyPartRecord part =
                FindLargestMissingPart(pawn);
            if (part == null)
            {
                return;
            }

            pawn.health.RestorePart(part, null, true);
            int damageAmount =
                Math.Max(
                    0,
                    (int)pawn.health.hediffSet.GetPartHealth(part) - 2);
            if (damageAmount <= 0)
            {
                return;
            }

            DamageInfo damageInfo = new DamageInfo(
                DamageDefOf.Cut,
                damageAmount,
                999f,
                -1f,
                null,
                part);
            damageInfo.SetAllowDamagePropagation(false);
            pawn.TakeDamage(damageInfo);
        }

        private static BodyPartRecord FindLargestMissingPart(
            Pawn pawn)
        {
            BodyPartRecord selected = null;

            foreach (Hediff_MissingPart missing in
                pawn.health.hediffSet.GetMissingPartsCommonAncestors())
            {
                BodyPartRecord part = missing.Part;
                if (part == null ||
                    !ValidPartDefNames.Contains(part.def.defName) ||
                    pawn.health.hediffSet
                        .PartOrAnyAncestorHasDirectlyAddedParts(part))
                {
                    continue;
                }

                if (selected == null ||
                    part.coverageAbsWithChildren >
                        selected.coverageAbsWithChildren)
                {
                    selected = part;
                }
            }

            return selected;
        }
    }
}
