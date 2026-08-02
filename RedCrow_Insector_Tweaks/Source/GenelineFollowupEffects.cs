using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    public sealed class RC_JellyConsumptionExtension : DefModExtension
    {
        public float perDayAdditive;
    }

    [StaticConstructorOnStartup]
    public static class GenelineFollowupEffects
    {
        private const string JellyResourceTypeName =
            "VanillaRacesExpandedInsector.Gene_Resource_InsectJelly";
        private const string HiveAnimaGeneDef =
            "RC_Evolution_HiveAnimaResonance";

        static GenelineFollowupEffects()
        {
            try
            {
                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.GenelineFollowupEffects");

                Type jellyType = AccessTools.TypeByName(
                    JellyResourceTypeName);
                MethodInfo jellyTick = jellyType != null
                    ? AccessTools.Method(jellyType, "Tick")
                    : null;
                MethodInfo jellyPostfix = AccessTools.Method(
                    typeof(GenelineFollowupEffects),
                    "JellyTickPostfix");
                if (jellyTick != null && jellyPostfix != null)
                {
                    harmony.Patch(
                        jellyTick,
                        postfix: Last(jellyPostfix));
                }
                else
                {
                    Log.Warning(
                        "[RedCrow Consumption] Insect-jelly resource " +
                        "Tick was not found; jelly modifiers are inactive.");
                }

                MethodInfo focusAvailability = AccessTools.Method(
                    typeof(MeditationFocusTypeAvailabilityCache),
                    "PawnCanUseInt");
                MethodInfo focusPostfix = AccessTools.Method(
                    typeof(GenelineFollowupEffects),
                    "NaturalFocusPostfix");
                if (focusAvailability != null && focusPostfix != null)
                {
                    harmony.Patch(
                        focusAvailability,
                        postfix: Last(focusPostfix));
                }

                MethodInfo explanation = AccessTools.Method(
                    typeof(MeditationFocusDef),
                    "EnablingThingsExplanation");
                MethodInfo explanationPostfix = AccessTools.Method(
                    typeof(GenelineFollowupEffects),
                    "NaturalFocusExplanationPostfix");
                if (explanation != null && explanationPostfix != null)
                {
                    harmony.Patch(
                        explanation,
                        postfix: Last(explanationPostfix));
                }

                Log.Message(
                    "[RedCrow Consumption] Jelly modifiers and hive-anima " +
                    "focus support installed with Priority.Last.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    "[RedCrow Consumption] Patch installation failed:\n" +
                    exception);
            }
        }

        private static HarmonyMethod Last(MethodInfo method)
        {
            HarmonyMethod patch = new HarmonyMethod(method);
            patch.priority = Priority.Last;
            return patch;
        }

        [HarmonyPriority(Priority.Last)]
        public static void JellyTickPostfix(object __instance)
        {
            Gene_Resource resource = __instance as Gene_Resource;
            if (resource == null ||
                resource.def == null ||
                resource.pawn == null ||
                !resource.pawn.Spawned ||
                !resource.Active)
            {
                return;
            }

            float factor;
            float perDayAdditive;
            GetJellyConsumptionModifiers(
                resource.pawn,
                out factor,
                out perDayAdditive);

            float baseLossPerDay = resource.def.resourceLossPerDay;
            float targetLossPerDay = CalculateJellyLossPerDay(
                baseLossPerDay,
                factor,
                perDayAdditive);
            float extraLossPerTick =
                (targetLossPerDay - baseLossPerDay) /
                GenDate.TicksPerDay;

            if (Math.Abs(extraLossPerTick) <= 0.000001f)
            {
                return;
            }

            resource.Value -= extraLossPerTick;
        }

        public static float CalculateJellyLossPerDay(
            float baseLossPerDay,
            float factor,
            float perDayAdditive)
        {
            return Math.Max(
                0f,
                baseLossPerDay * Math.Max(0f, factor) +
                perDayAdditive);
        }

        public static void GetJellyConsumptionModifiers(
            Pawn pawn,
            out float factor,
            out float perDayAdditive)
        {
            factor = 1f;
            perDayAdditive = 0f;
            if (pawn == null || pawn.genes == null)
            {
                return;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene == null || !gene.Active || gene.def == null)
                {
                    continue;
                }

                RC_HungerGeneExtension percentageExtension =
                    gene.def.GetModExtension<RC_HungerGeneExtension>();
                if (percentageExtension != null)
                {
                    factor += percentageExtension.jellyAdditive;
                }

                RC_JellyConsumptionExtension fixedExtension =
                    gene.def.GetModExtension<RC_JellyConsumptionExtension>();
                if (fixedExtension != null)
                {
                    perDayAdditive += fixedExtension.perDayAdditive;
                }
            }

            factor = Math.Max(0f, factor);
        }

        public static float GetJellyConsumptionFactor(Pawn pawn)
        {
            float factor;
            float perDayAdditive;
            GetJellyConsumptionModifiers(
                pawn,
                out factor,
                out perDayAdditive);
            return factor;
        }

        [HarmonyPriority(Priority.Last)]
        public static void NaturalFocusPostfix(
            Pawn p,
            MeditationFocusDef type,
            ref bool __result)
        {
            if (!__result &&
                type == MeditationFocusDefOf.Natural &&
                HasActiveGene(p, HiveAnimaGeneDef))
            {
                __result = true;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void NaturalFocusExplanationPostfix(
            Pawn pawn,
            MeditationFocusDef __instance,
            ref string __result)
        {
            if (__instance != MeditationFocusDefOf.Natural ||
                !HasActiveGene(pawn, HiveAnimaGeneDef))
            {
                return;
            }

            string line =
                "RC_HiveAnimaNaturalFocus".Translate();
            if (!__result.Contains(line))
            {
                __result += "\n  - " + line + ".";
            }
        }

        private static bool HasActiveGene(Pawn pawn, string defName)
        {
            if (pawn == null || pawn.genes == null)
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
                    gene.def.defName == defName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
