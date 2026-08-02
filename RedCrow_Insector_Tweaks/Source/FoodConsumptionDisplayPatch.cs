using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class FoodConsumptionDisplayPatch
    {
        private const string LogPrefix =
            "[RedCrow Food Consumption Display]";
        private const string FoodConsumptionStatDefName =
            "FoodConsumption";

        private static readonly StatDef FoodConsumptionStat;
        private static bool loggedFirstApplication;

        static FoodConsumptionDisplayPatch()
        {
            try
            {
                FoodConsumptionStat =
                    DefDatabase<StatDef>.GetNamedSilentFail(
                        FoodConsumptionStatDefName);
                if (FoodConsumptionStat == null ||
                    FoodConsumptionStat.Worker == null)
                {
                    Log.Error(
                        LogPrefix +
                        " FoodConsumption StatDef or worker was not found.");
                    return;
                }

                MethodInfo valueMethod = AccessTools.Method(
                    typeof(StatWorker),
                    "GetValue",
                    new[] { typeof(StatRequest), typeof(bool) });
                MethodInfo explanationMethod = AccessTools.Method(
                    typeof(StatWorker),
                    "GetExplanationFull",
                    new[]
                    {
                        typeof(StatRequest),
                        typeof(ToStringNumberSense),
                        typeof(float)
                    });

                MethodInfo valuePostfix = AccessTools.Method(
                    typeof(FoodConsumptionDisplayPatch),
                    "ValuePostfix");
                MethodInfo explanationPostfix = AccessTools.Method(
                    typeof(FoodConsumptionDisplayPatch),
                    "ExplanationPostfix");

                if (valueMethod == null ||
                    explanationMethod == null ||
                    valuePostfix == null ||
                    explanationPostfix == null)
                {
                    Log.Error(
                        LogPrefix +
                        " Final StatWorker display methods were not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.FoodConsumptionDisplay");
                harmony.Patch(
                    valueMethod,
                    postfix: Last(valuePostfix));
                harmony.Patch(
                    explanationMethod,
                    postfix: Last(explanationPostfix));

                Log.Message(
                    LogPrefix +
                    " Final FoodConsumption value and explanation are " +
                    "patched through StatWorker wrappers. Worker=" +
                    FoodConsumptionStat.Worker.GetType().FullName + ".");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" + exception);
            }
        }

        private static HarmonyMethod Last(MethodInfo method)
        {
            HarmonyMethod patch = new HarmonyMethod(method);
            patch.priority = Priority.Last;
            return patch;
        }

        [HarmonyPriority(Priority.Last)]
        public static void ValuePostfix(
            StatWorker __instance,
            StatRequest req,
            ref float __result)
        {
            if (FoodConsumptionStat == null ||
                __instance != FoodConsumptionStat.Worker)
            {
                return;
            }

            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return;
            }

            float multiplier;
            float additive;
            float factor;
            if (!TryGetHungerFormula(
                    pawn,
                    out multiplier,
                    out additive,
                    out factor))
            {
                return;
            }

            float originalValue = __result;
            __result = Math.Max(0f, originalValue * factor);

            if (!loggedFirstApplication)
            {
                loggedFirstApplication = true;
                Log.Message(
                    LogPrefix + " Applied factor " +
                    factor.ToString("0.###") + " to " +
                    pawn.LabelShortCap + ": " +
                    originalValue.ToString("0.###") + " -> " +
                    __result.ToString("0.###") + ".");
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void ExplanationPostfix(
            StatWorker __instance,
            StatRequest req,
            ref string __result)
        {
            if (FoodConsumptionStat == null ||
                __instance != FoodConsumptionStat.Worker)
            {
                return;
            }

            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return;
            }

            AppendHungerBreakdown(pawn, ref __result);
        }

        private static bool TryGetHungerFormula(
            Pawn pawn,
            out float multiplier,
            out float additive,
            out float factor)
        {
            multiplier = 1f;
            additive = 0f;
            factor = 1f;
            bool found = false;

            if (pawn == null || pawn.genes == null)
            {
                return false;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene == null || !gene.Active || gene.def == null)
                {
                    continue;
                }

                RC_HungerGeneExtension extension =
                    gene.def.GetModExtension<RC_HungerGeneExtension>();
                if (extension == null)
                {
                    continue;
                }

                found = true;
                multiplier *= extension.hungerMultiplier;
                additive += extension.hungerAdditive;
            }

            Hediff swarmConsumed =
                pawn.health != null
                    ? pawn.health.hediffSet.GetFirstHediffOfDef(
                        DefDatabase<HediffDef>.GetNamedSilentFail(
                            "RC_SwarmConsumed"),
                        false)
                    : null;
            if (swarmConsumed != null)
            {
                found = true;
                additive += 1f;
            }

            if (!found)
            {
                return false;
            }

            factor = Math.Max(0f, multiplier + additive);
            return true;
        }

        private static void AppendHungerBreakdown(
            Pawn pawn,
            ref string explanation)
        {
            float multiplier;
            float additive;
            float factor;
            if (!TryGetHungerFormula(
                    pawn,
                    out multiplier,
                    out additive,
                    out factor))
            {
                return;
            }

            string formulaLine =
                "RC_HungerFormulaLine".Translate(
                    multiplier.ToString("0.##"),
                    additive.ToString("+0.##;-0.##;0"),
                    factor.ToString("0.##"));
            if (!explanation.NullOrEmpty() &&
                explanation.Contains(formulaLine))
            {
                return;
            }

            StringBuilder lines = new StringBuilder();
            if (pawn.genes != null)
            {
                List<Gene> genes = pawn.genes.GenesListForReading;
                for (int index = 0; index < genes.Count; index++)
                {
                    Gene gene = genes[index];
                    if (gene == null || !gene.Active || gene.def == null)
                    {
                        continue;
                    }

                    RC_HungerGeneExtension extension =
                        gene.def.GetModExtension<RC_HungerGeneExtension>();
                    if (extension == null)
                    {
                        continue;
                    }

                    if (Math.Abs(extension.hungerMultiplier - 1f) >
                        0.0001f)
                    {
                        lines.Append(
                            "\n" +
                            "RC_HungerSourceMultiplier".Translate(
                                gene.LabelCap,
                                extension.hungerMultiplier
                                    .ToStringPercent()));
                    }

                    if (Math.Abs(extension.hungerAdditive) > 0.0001f)
                    {
                        lines.Append(
                            "\n" +
                            "RC_HungerSourceAdditive".Translate(
                                gene.LabelCap,
                                extension.hungerAdditive.ToString(
                                    "+0.##;-0.##;0")));
                    }
                }
            }

            HediffDef swarmDef =
                DefDatabase<HediffDef>.GetNamedSilentFail(
                    "RC_SwarmConsumed");
            if (swarmDef != null &&
                pawn.health != null &&
                pawn.health.hediffSet.HasHediff(swarmDef, false))
            {
                lines.Append(
                    "\n" +
                    "RC_HungerSourceAdditive".Translate(
                        swarmDef.LabelCap,
                        1f.ToString("+0.##;-0.##;0")));
            }

            lines.Append("\n" + formulaLine);
            explanation += lines.ToString();
        }
    }
}
