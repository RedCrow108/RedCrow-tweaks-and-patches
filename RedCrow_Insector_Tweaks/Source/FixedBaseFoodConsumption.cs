using System;
using System.Collections.Generic;
using System.Reflection;
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

        static FixedBaseFoodConsumption()
        {
            try
            {
                MethodInfo target = AccessTools.Method(
                    typeof(Need_Food),
                    "FoodFallPerTickAssumingCategory",
                    new[]
                    {
                        typeof(HungerCategory),
                        typeof(bool)
                    });
                MethodInfo postfix = AccessTools.Method(
                    typeof(FixedBaseFoodConsumption),
                    "FoodFallPerTickPostfix");

                if (target == null || postfix == null)
                {
                    Log.Error(
                        LogPrefix +
                        " Need_Food.FoodFallPerTickAssumingCategory " +
                        "could not be patched.");
                    return;
                }

                HarmonyMethod patch = new HarmonyMethod(postfix);
                patch.priority = Priority.Last;

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.FixedBaseFoodConsumption");
                harmony.Patch(target, postfix: patch);

                Log.Message(
                    LogPrefix +
                    " Fixed daily offsets scale the completed hunger rate " +
                    "from the adjusted base food consumption.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
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
