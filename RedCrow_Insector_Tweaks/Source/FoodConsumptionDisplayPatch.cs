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

        static FoodConsumptionDisplayPatch()
        {
            try
            {
                StatWorker worker = StatDefOf.FoodConsumption.Worker;
                Type workerType = worker.GetType();

                MethodInfo valueMethod = AccessTools.Method(
                    workerType,
                    "GetValueUnfinalized",
                    new[] { typeof(StatRequest), typeof(bool) });
                MethodInfo explanationMethod = AccessTools.Method(
                    workerType,
                    "GetExplanationUnfinalized",
                    new[]
                    {
                        typeof(StatRequest),
                        typeof(ToStringNumberSense)
                    });

                if (valueMethod == null || explanationMethod == null)
                {
                    Log.Error(
                        LogPrefix +
                        " FoodConsumption worker methods were not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.FoodConsumptionDisplay");
                harmony.Patch(
                    valueMethod,
                    postfix: Last(
                        AccessTools.Method(
                            typeof(FoodConsumptionDisplayPatch),
                            "ValuePostfix")));
                harmony.Patch(
                    explanationMethod,
                    postfix: Last(
                        AccessTools.Method(
                            typeof(FoodConsumptionDisplayPatch),
                            "ExplanationPostfix")));

                Log.Message(
                    LogPrefix +
                    " FoodConsumption stat now mirrors the RedCrow " +
                    "hunger factor. Worker=" + workerType.FullName + ".");
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
            StatRequest req,
            StatDef ___stat,
            ref float __result)
        {
            Pawn pawn = req.Thing as Pawn;
            if (___stat != StatDefOf.FoodConsumption || pawn == null)
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

            __result = Math.Max(0f, __result * factor);
        }

        [HarmonyPriority(Priority.Last)]
        public static void ExplanationPostfix(
            StatRequest req,
            StatDef ___stat,
            ref string __result)
        {
            Pawn pawn = req.Thing as Pawn;
            if (___stat != StatDefOf.FoodConsumption || pawn == null)
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
