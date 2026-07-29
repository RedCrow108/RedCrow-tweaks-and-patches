using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class Stage2Effects
    {
        private const string LogPrefix = "[RedCrow Stage 2]";
        private const string CuriosityPrefix = "RC_Evolution_Curiosity";
        private const string OriginalCuriosityPrefix = "VRE_Curiosity_";
        private const string SensoryAntennaDefName =
            "RC_SwarmSensoryAntenna";

        private static readonly Dictionary<string, string>
            CuriositySkillByGene =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "RC_Evolution_CuriosityShooting", "Shooting" },
                    { "RC_Evolution_CuriosityMelee", "Melee" },
                    {
                        "RC_Evolution_CuriosityConstruction",
                        "Construction"
                    },
                    { "RC_Evolution_CuriosityMining", "Mining" },
                    { "RC_Evolution_CuriosityCooking", "Cooking" },
                    { "RC_Evolution_CuriosityPlants", "Plants" },
                    { "RC_Evolution_CuriosityAnimals", "Animals" },
                    { "RC_Evolution_CuriosityCrafting", "Crafting" },
                    { "RC_Evolution_CuriosityArtistic", "Artistic" },
                    { "RC_Evolution_CuriosityMedicine", "Medicine" },
                    { "RC_Evolution_CuriositySocial", "Social" },
                    {
                        "RC_Evolution_CuriosityIntellectual",
                        "Intellectual"
                    }
                };

        private static JoyKindDef cerebralGaming;
        private static HediffDef sensoryAntenna;

        static Stage2Effects()
        {
            try
            {
                MethodInfo learnMethod = AccessTools.Method(
                    typeof(Pawn_SkillTracker),
                    "Learn",
                    new[]
                    {
                        typeof(SkillDef),
                        typeof(float),
                        typeof(bool),
                        typeof(bool)
                    });
                MethodInfo intervalMethod = AccessTools.Method(
                    typeof(SkillRecord),
                    "Interval",
                    Type.EmptyTypes);
                MethodInfo darknessThoughtMethod = AccessTools.Method(
                    typeof(ThoughtWorker_Dark),
                    "CurrentStateInternal",
                    new[] { typeof(Pawn) });

                MethodInfo learnPostfix = AccessTools.Method(
                    typeof(Stage2Effects),
                    "LearnPostfix");
                MethodInfo intervalPrefix = AccessTools.Method(
                    typeof(Stage2Effects),
                    "IntervalPrefix");
                MethodInfo darknessThoughtPostfix = AccessTools.Method(
                    typeof(Stage2Effects),
                    "DarknessThoughtPostfix");

                if (learnMethod == null ||
                    intervalMethod == null ||
                    darknessThoughtMethod == null ||
                    learnPostfix == null ||
                    intervalPrefix == null ||
                    darknessThoughtPostfix == null)
                {
                    Log.Error(
                        LogPrefix + " Patch installation failed: one or " +
                        "more RimWorld skill or thought methods were not " +
                        "found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.Stage2Effects");

                HarmonyMethod learnPatch = new HarmonyMethod(learnPostfix);
                learnPatch.priority = Priority.Last;
                harmony.Patch(learnMethod, postfix: learnPatch);

                HarmonyMethod intervalPatch =
                    new HarmonyMethod(intervalPrefix);
                intervalPatch.priority = Priority.Last;
                harmony.Patch(intervalMethod, prefix: intervalPatch);

                HarmonyMethod darknessPatch =
                    new HarmonyMethod(darknessThoughtPostfix);
                darknessPatch.priority = Priority.Last;
                harmony.Patch(
                    darknessThoughtMethod,
                    postfix: darknessPatch);

                cerebralGaming =
                    DefDatabase<JoyKindDef>.GetNamedSilentFail(
                        "Gaming_Cerebral");
                sensoryAntenna =
                    DefDatabase<HediffDef>.GetNamedSilentFail(
                        SensoryAntennaDefName);

                if (cerebralGaming == null)
                {
                    Log.Error(
                        LogPrefix + " JoyKindDef Gaming_Cerebral was not " +
                        "found. Curiosity skill-loss protection remains " +
                        "active, but learning cannot grant recreation.");
                }

                if (sensoryAntenna == null)
                {
                    Log.Error(
                        LogPrefix + " HediffDef " +
                        SensoryAntennaDefName + " was not found. The " +
                        "sensory crown cannot suppress the darkness " +
                        "thought.");
                }

                Log.Message(
                    LogPrefix + " Curiosity and darkness-thought patches " +
                    "installed with Priority.Last (" +
                    Priority.Last + "). Curiosity variants=" +
                    CuriositySkillByGene.Count + ".");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void LearnPostfix(
            Pawn ___pawn,
            SkillDef sDef,
            float xp)
        {
            if (xp <= 0f ||
                sDef == null ||
                cerebralGaming == null ||
                !HasMatchingCuriosity(___pawn, sDef))
            {
                return;
            }

            Need_Joy joy = ___pawn.needs == null
                ? null
                : ___pawn.needs.joy;
            if (joy != null)
            {
                joy.GainJoy(xp * 0.001f, cerebralGaming);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static bool IntervalPrefix(
            Pawn ___pawn,
            SkillRecord __instance)
        {
            if (__instance == null || __instance.def == null)
            {
                return true;
            }

            return !HasMatchingCuriosity(
                ___pawn,
                __instance.def);
        }

        [HarmonyPriority(Priority.Last)]
        public static void DarknessThoughtPostfix(
            Pawn p,
            ref ThoughtState __result)
        {
            if (p != null &&
                sensoryAntenna != null &&
                p.health != null &&
                p.health.hediffSet != null &&
                p.health.hediffSet.HasHediff(
                    sensoryAntenna,
                    false))
            {
                __result = ThoughtState.Inactive;
            }
        }

        private static bool HasMatchingCuriosity(
            Pawn pawn,
            SkillDef skill)
        {
            if (pawn == null ||
                pawn.genes == null ||
                skill == null)
            {
                return false;
            }

            string selectedSkill = null;
            List<Gene> genes = pawn.genes.GenesListForReading;

            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene == null ||
                    gene.def == null ||
                    !gene.Active)
                {
                    continue;
                }

                string defName = gene.def.defName;
                if (defName.StartsWith(
                    OriginalCuriosityPrefix,
                    StringComparison.Ordinal))
                {
                    // VFE already handles an active original curiosity.
                    // Never add a second recreation or no-loss effect.
                    return false;
                }

                string mappedSkill;
                if (defName.StartsWith(
                        CuriosityPrefix,
                        StringComparison.Ordinal) &&
                    CuriositySkillByGene.TryGetValue(
                        defName,
                        out mappedSkill))
                {
                    selectedSkill = mappedSkill;
                }
            }

            return selectedSkill != null &&
                string.Equals(
                    selectedSkill,
                    skill.defName,
                    StringComparison.Ordinal);
        }
    }
}
