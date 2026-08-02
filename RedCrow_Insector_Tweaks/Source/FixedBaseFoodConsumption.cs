using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    public sealed class RC_BaseFoodConsumptionExtension : DefModExtension
    {
        public float basePerDayOffset;
    }

    [StaticConstructorOnStartup]
    public static class FixedBaseFoodConsumption
    {
        private const string LogPrefix =
            "[RedCrow Base Food Consumption]";
        private const float HumanBaseNutritionPerDay = 1.6f;

        private static readonly HashSet<int> LoggedPawnIds =
            new HashSet<int>();
        private static readonly HashSet<int> LoggedExplanationPawnIds =
            new HashSet<int>();

        static FixedBaseFoodConsumption()
        {
            try
            {
                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.FixedBaseFoodConsumption");

                MethodInfo hungerTarget = AccessTools.Method(
                    typeof(Need_Food),
                    "FoodFallPerTickAssumingCategory",
                    new[]
                    {
                        typeof(HungerCategory),
                        typeof(bool)
                    });
                MethodInfo hungerPostfix = AccessTools.Method(
                    typeof(FixedBaseFoodConsumption),
                    "FoodFallPerTickPostfix");

                if (hungerTarget == null || hungerPostfix == null)
                {
                    Log.Error(
                        LogPrefix +
                        " Need_Food.FoodFallPerTickAssumingCategory " +
                        "could not be patched.");
                }
                else
                {
                    harmony.Patch(
                        hungerTarget,
                        postfix: Last(hungerPostfix));
                }

                MethodInfo explanationTarget = AccessTools.Method(
                    typeof(RaceProperties),
                    "NutritionEatenPerDayExplanation",
                    new[]
                    {
                        typeof(Pawn),
                        typeof(bool),
                        typeof(bool),
                        typeof(bool)
                    });
                MethodInfo explanationPostfix = AccessTools.Method(
                    typeof(FixedBaseFoodConsumption),
                    "NutritionExplanationPostfix");

                if (explanationTarget == null || explanationPostfix == null)
                {
                    Log.Error(
                        LogPrefix +
                        " RaceProperties.NutritionEatenPerDayExplanation " +
                        "could not be patched.");
                }
                else
                {
                    harmony.Patch(
                        explanationTarget,
                        postfix: Last(explanationPostfix));
                }

                Log.Message(
                    LogPrefix +
                    " Fixed daily offsets scale the completed hunger rate; " +
                    "the food-consumption tooltip lists each active gene " +
                    "offset before the final value.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
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
        public static void FoodFallPerTickPostfix(
            Pawn ___pawn,
            ref float __result)
        {
            if (___pawn == null || __result <= 0f)
            {
                return;
            }

            float basePerDayOffset = GetBasePerDayOffset(___pawn);
            if (Math.Abs(basePerDayOffset) <= 0.0001f)
            {
                return;
            }

            float basePerDay = GetUnmodifiedBasePerDay(___pawn);
            if (basePerDay <= 0.0001f)
            {
                return;
            }

            float targetBasePerDay = Math.Max(
                0f,
                basePerDay + basePerDayOffset);
            float baseMultiplier = targetBasePerDay / basePerDay;

            __result = Math.Max(0f, __result * baseMultiplier);

            if (LoggedPawnIds.Add(___pawn.thingIDNumber))
            {
                Log.Message(
                    LogPrefix + " Applied to " + ___pawn.LabelShort +
                    ": base=" + basePerDay.ToString("0.###") +
                    ", offset=" + basePerDayOffset.ToString("+0.###;-0.###;0") +
                    ", multiplier=" + baseMultiplier.ToString("0.###") + ".");
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void NutritionExplanationPostfix(
            Pawn p,
            bool showCalculations,
            ref string __result)
        {
            if (p == null || !showCalculations || __result.NullOrEmpty())
            {
                return;
            }

            string geneLines = BuildGeneOffsetLines(p);
            if (geneLines.NullOrEmpty())
            {
                return;
            }

            string finalPrefix =
                "StatsReport_FinalValue".Translate() + ":";
            int finalIndex = __result.LastIndexOf(
                finalPrefix,
                StringComparison.Ordinal);

            string insertion = "\n\n" + geneLines + "\n";
            if (finalIndex >= 0)
            {
                __result = __result.Insert(finalIndex, insertion);
            }
            else
            {
                __result += insertion;
            }

            if (LoggedExplanationPawnIds.Add(p.thingIDNumber))
            {
                Log.Message(
                    LogPrefix + " Added tooltip breakdown for " +
                    p.LabelShort + ": " +
                    geneLines.Replace("\n", "; ") + ".");
            }
        }

        private static string BuildGeneOffsetLines(Pawn pawn)
        {
            if (pawn == null || pawn.genes == null)
            {
                return string.Empty;
            }

            StringBuilder lines = new StringBuilder();
            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene == null ||
                    !gene.Active ||
                    gene.def == null)
                {
                    continue;
                }

                RC_BaseFoodConsumptionExtension extension =
                    gene.def.GetModExtension<
                        RC_BaseFoodConsumptionExtension>();
                if (extension == null ||
                    Math.Abs(extension.basePerDayOffset) <= 0.0001f)
                {
                    continue;
                }

                if (lines.Length > 0)
                {
                    lines.AppendLine();
                }

                lines.Append("    ");
                lines.Append(gene.LabelCap);
                lines.Append(": ");
                lines.Append(
                    extension.basePerDayOffset.ToString(
                        "+0.##;-0.##;0"));
            }

            return lines.ToString();
        }

        public static float GetBasePerDayOffset(Pawn pawn)
        {
            float offset = 0f;
            if (pawn == null || pawn.genes == null)
            {
                return offset;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene == null ||
                    !gene.Active ||
                    gene.def == null)
                {
                    continue;
                }

                RC_BaseFoodConsumptionExtension extension =
                    gene.def.GetModExtension<
                        RC_BaseFoodConsumptionExtension>();
                if (extension != null)
                {
                    offset += extension.basePerDayOffset;
                }
            }

            return offset;
        }

        public static float GetUnmodifiedBasePerDay(Pawn pawn)
        {
            if (pawn == null ||
                pawn.RaceProps == null ||
                pawn.ageTracker == null ||
                pawn.ageTracker.CurLifeStage == null)
            {
                return 0f;
            }

            return pawn.RaceProps.baseHungerRate *
                pawn.ageTracker.CurLifeStage.hungerRateFactor *
                HumanBaseNutritionPerDay;
        }
    }
}
