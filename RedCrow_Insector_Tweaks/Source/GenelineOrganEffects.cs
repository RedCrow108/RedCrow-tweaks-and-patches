using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    public sealed class RC_HungerGeneExtension : DefModExtension
    {
        public float hungerMultiplier = 1f;
        public float hungerAdditive;
    }

    [StaticConstructorOnStartup]
    public static class GenelineOrganEffects
    {
        private const string LogPrefix = "[RedCrow Organs]";
        private const string SurvivalToolsPackageId =
            "skyarkhangel.SurvivalToolsLite";

        private static readonly Dictionary<string, Dictionary<string, float>>
            BiologicalToolFactors =
                new Dictionary<string, Dictionary<string, float>>
                {
                    {
                        "RC_Mutation_BiologicalSickle",
                        new Dictionary<string, float>
                        {
                            { "PlantWorkSpeed", 1.30f }
                        }
                    },
                    {
                        "RC_Mutation_BiologicalDiggingTools",
                        new Dictionary<string, float>
                        {
                            { "PlantWorkSpeed", 1.30f },
                            { "MiningSpeed", 1.30f }
                        }
                    },
                    {
                        "RC_Mutation_BiologicalHandaxe",
                        new Dictionary<string, float>
                        {
                            { "PlantWorkSpeed", 1.30f },
                            { "PruningSpeed", 1.30f }
                        }
                    },
                    {
                        "RC_Mutation_BiologicalHammer",
                        new Dictionary<string, float>
                        {
                            { "ConstructionSpeed", 1.35f },
                            { "SmithingSpeed", 1.30f }
                        }
                    }
                };

        private static readonly bool SurvivalToolsActive;
        private static bool BiologicalToolsIntegratedWithSurvivalTools;

        static GenelineOrganEffects()
        {
            SurvivalToolsActive = ModsConfig.IsActive(
                SurvivalToolsPackageId);

            try
            {
                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.GenelineOrganEffects");

                PatchCoreEffects(harmony);
                PatchSurvivalTools(harmony);

                Log.Message(
                    LogPrefix +
                    " Hunger formula and biological-tool patches installed " +
                    "with Priority.Last. SurvivalToolsLite=" +
                    SurvivalToolsActive + ".");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" + exception);
            }
        }

        private static void PatchCoreEffects(Harmony harmony)
        {
            PatchPostfix(
                harmony,
                AccessTools.Method(
                    typeof(Need_Food),
                    "FoodFallPerTickAssumingCategory"),
                AccessTools.Method(
                    typeof(GenelineOrganEffects),
                    "FoodFallPerTickPostfix"));

            PatchPostfix(
                harmony,
                AccessTools.Method(typeof(Need_Food), "GetTipString"),
                AccessTools.Method(
                    typeof(GenelineOrganEffects),
                    "FoodTipPostfix"));

            PatchPostfix(
                harmony,
                AccessTools.Method(
                    typeof(StatWorker),
                    "GetValueUnfinalized"),
                AccessTools.Method(
                    typeof(GenelineOrganEffects),
                    "StatValuePostfix"));

            PatchPostfix(
                harmony,
                AccessTools.Method(
                    typeof(StatWorker),
                    "GetExplanationUnfinalized"),
                AccessTools.Method(
                    typeof(GenelineOrganEffects),
                    "StatExplanationPostfix"));
        }

        private static void PatchSurvivalTools(Harmony harmony)
        {
            if (!SurvivalToolsActive)
            {
                return;
            }

            Type statPartType = AccessTools.TypeByName(
                "SurvivalToolsLite.StatPart_SurvivalTool");
            if (statPartType == null)
            {
                Log.Warning(
                    LogPrefix +
                    " Survival Tools Lite is active, but its stat part " +
                    "was not found. Biological tools will use fallback " +
                    "stat bonuses.");
                return;
            }

            MethodInfo transform = AccessTools.Method(
                statPartType,
                "TransformValue");
            MethodInfo explanation = AccessTools.Method(
                statPartType,
                "ExplanationPart");
            MethodInfo prefix = AccessTools.Method(
                typeof(GenelineOrganEffects),
                "SurvivalToolTransformPrefix");
            MethodInfo postfix = AccessTools.Method(
                typeof(GenelineOrganEffects),
                "SurvivalToolTransformPostfix");
            MethodInfo explanationPostfix = AccessTools.Method(
                typeof(GenelineOrganEffects),
                "SurvivalToolExplanationPostfix");

            if (transform == null ||
                explanation == null ||
                prefix == null ||
                postfix == null ||
                explanationPostfix == null)
            {
                Log.Warning(
                    LogPrefix +
                    " Survival Tools Lite methods were not found. " +
                    "Biological tools will use fallback stat bonuses.");
                return;
            }

            HarmonyMethod prefixPatch = new HarmonyMethod(prefix);
            prefixPatch.priority = Priority.Last;
            HarmonyMethod postfixPatch = new HarmonyMethod(postfix);
            postfixPatch.priority = Priority.Last;
            HarmonyMethod explanationPatch =
                new HarmonyMethod(explanationPostfix);
            explanationPatch.priority = Priority.Last;

            harmony.Patch(
                transform,
                prefix: prefixPatch,
                postfix: postfixPatch);
            harmony.Patch(
                explanation,
                postfix: explanationPatch);
            BiologicalToolsIntegratedWithSurvivalTools = true;
        }

        private static void PatchPostfix(
            Harmony harmony,
            MethodInfo original,
            MethodInfo postfix)
        {
            if (original == null || postfix == null)
            {
                throw new MissingMethodException(
                    "Required organ-effect method was not found.");
            }

            HarmonyMethod patch = new HarmonyMethod(postfix);
            patch.priority = Priority.Last;
            harmony.Patch(original, postfix: patch);
        }

        [HarmonyPriority(Priority.Last)]
        public static void FoodFallPerTickPostfix(
            Pawn ___pawn,
            ref float __result)
        {
            float multiplier;
            float additive;
            float factor;
            if (!TryGetHungerFormula(
                    ___pawn,
                    out multiplier,
                    out additive,
                    out factor))
            {
                return;
            }

            __result *= factor;
            if (__result < 0f)
            {
                __result = 0f;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void FoodTipPostfix(
            Pawn ___pawn,
            ref string __result)
        {
            float multiplier;
            float additive;
            float factor;
            if (!TryGetHungerFormula(
                    ___pawn,
                    out multiplier,
                    out additive,
                    out factor))
            {
                return;
            }

            __result +=
                "\n" +
                "RC_HungerFormulaLine".Translate(
                    multiplier.ToString("0.##"),
                    additive.ToString("+0.##;-0.##;0"),
                    factor.ToString("0.##"));
        }

        [HarmonyPriority(Priority.Last)]
        public static void StatValuePostfix(
            StatRequest req,
            StatDef ___stat,
            ref float __result)
        {
            if (BiologicalToolsIntegratedWithSurvivalTools ||
                ___stat == null ||
                !(req.Thing is Pawn))
            {
                return;
            }

            Pawn pawn = (Pawn)req.Thing;
            Gene gene;
            float factor;
            if (TryGetBiologicalToolFactor(
                    pawn,
                    ___stat,
                    out gene,
                    out factor))
            {
                __result += factor - 1f;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void StatExplanationPostfix(
            StatRequest req,
            StatDef ___stat,
            ref string __result)
        {
            if (BiologicalToolsIntegratedWithSurvivalTools ||
                ___stat == null ||
                !(req.Thing is Pawn))
            {
                return;
            }

            Pawn pawn = (Pawn)req.Thing;
            Gene gene;
            float factor;
            if (!TryGetBiologicalToolFactor(
                    pawn,
                    ___stat,
                    out gene,
                    out factor))
            {
                return;
            }

            __result +=
                "\n" +
                "RC_BiologicalToolExplanation".Translate(
                    gene.LabelCap,
                    factor.ToString("0.##"));
        }

        public struct SurvivalToolState
        {
            public bool applies;
            public float baseValue;
            public float biologicalFactor;
            public string geneLabel;
        }

        [HarmonyPriority(Priority.Last)]
        public static void SurvivalToolTransformPrefix(
            object __instance,
            StatRequest req,
            ref float val,
            out SurvivalToolState __state)
        {
            __state = new SurvivalToolState();

            StatPart part = __instance as StatPart;
            Pawn pawn = req.Thing as Pawn;
            if (part == null || part.parentStat == null || pawn == null)
            {
                return;
            }

            Gene gene;
            float factor;
            if (!TryGetBiologicalToolFactor(
                    pawn,
                    part.parentStat,
                    out gene,
                    out factor))
            {
                return;
            }

            __state.applies = true;
            __state.baseValue = val;
            __state.biologicalFactor = factor;
            __state.geneLabel = gene.LabelCap;
        }

        [HarmonyPriority(Priority.Last)]
        public static void SurvivalToolTransformPostfix(
            ref float val,
            SurvivalToolState __state)
        {
            if (!__state.applies)
            {
                return;
            }

            float biologicalValue =
                __state.baseValue * __state.biologicalFactor;
            if (biologicalValue > val)
            {
                val = biologicalValue;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void SurvivalToolExplanationPostfix(
            object __instance,
            StatRequest req,
            ref string __result)
        {
            StatPart part = __instance as StatPart;
            Pawn pawn = req.Thing as Pawn;
            if (part == null || part.parentStat == null || pawn == null)
            {
                return;
            }

            Gene gene;
            float factor;
            if (!TryGetBiologicalToolFactor(
                    pawn,
                    part.parentStat,
                    out gene,
                    out factor))
            {
                return;
            }

            string biologicalLine =
                "RC_BiologicalToolExplanation".Translate(
                    gene.LabelCap,
                    factor.ToString("0.##"));
            if (__result.NullOrEmpty())
            {
                __result = biologicalLine;
            }
            else
            {
                __result += "\n" + biologicalLine;
            }
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

            if (!found)
            {
                return false;
            }

            factor = Math.Max(0f, multiplier + additive);
            return true;
        }

        private static bool TryGetBiologicalToolFactor(
            Pawn pawn,
            StatDef stat,
            out Gene matchingGene,
            out float factor)
        {
            matchingGene = null;
            factor = 1f;

            if (pawn == null || pawn.genes == null || stat == null)
            {
                return false;
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

                Dictionary<string, float> statFactors;
                float candidate;
                if (BiologicalToolFactors.TryGetValue(
                        gene.def.defName,
                        out statFactors) &&
                    statFactors.TryGetValue(stat.defName, out candidate) &&
                    candidate > factor)
                {
                    matchingGene = gene;
                    factor = candidate;
                }
            }

            return matchingGene != null;
        }
    }
}
