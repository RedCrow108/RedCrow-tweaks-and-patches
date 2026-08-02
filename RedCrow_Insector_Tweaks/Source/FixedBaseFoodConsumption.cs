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
                    " Fixed daily offsets are applied to base food " +
                    "consumption before pawn-specific factors.");
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
            HungerCategory hunger,
            bool ignoreMalnutrition,
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

            float scaledOffsetPerTick =
                CalculateScaledOffsetPerTick(
                    ___pawn,
                    hunger,
                    ignoreMalnutrition,
                    basePerDayOffset);

            __result = Math.Max(0f, __result + scaledOffsetPerTick);
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

        private static float CalculateScaledOffsetPerTick(
            Pawn pawn,
            HungerCategory hunger,
            bool ignoreMalnutrition,
            float basePerDayOffset)
        {
            float factor = hunger.HungerMultiplier();

            if (pawn.health != null)
            {
                factor *= pawn.health.hediffSet.GetHungerRateFactor(
                    ignoreMalnutrition
                        ? HediffDefOf.Malnutrition
                        : null);
            }

            if (pawn.story != null && pawn.story.traits != null)
            {
                factor *= pawn.story.traits.HungerRateFactor;
            }

            Building_Bed bed = pawn.CurrentBed();
            if (bed != null)
            {
                factor *= bed.GetStatValue(
                    StatDefOf.BedHungerRateFactor);
            }

            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                int metabolism = 0;
                List<Gene> genes = pawn.genes.GenesListForReading;
                for (int index = 0; index < genes.Count; index++)
                {
                    Gene gene = genes[index];
                    if (gene != null &&
                        gene.def != null &&
                        !gene.Overridden)
                    {
                        metabolism += gene.def.biostatMet;
                    }
                }

                factor *=
                    GeneTuning.MetabolismToFoodConsumptionFactorCurve
                        .Evaluate(metabolism);
            }

            return basePerDayOffset * factor / GenDate.TicksPerDay;
        }
    }
}
