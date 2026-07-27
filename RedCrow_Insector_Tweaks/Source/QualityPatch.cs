using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class QualityPatch
    {
        private const string LogPrefix = "[RedCrow Quality]";

        static QualityPatch()
        {
            try
            {
                MethodInfo original = AccessTools.Method(
                    typeof(QualityUtility),
                    "GenerateQualityCreatedByPawn",
                    new[] { typeof(Pawn), typeof(SkillDef) });
                MethodInfo postfix = AccessTools.Method(
                    typeof(QualityPatch),
                    "Postfix");

                if (original == null || postfix == null)
                {
                    Log.Error(
                        LogPrefix + " Patch installation failed: " +
                        "target or postfix method was not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.Quality");
                HarmonyMethod postfixMethod = new HarmonyMethod(postfix);
                postfixMethod.priority = Priority.Last;
                harmony.Patch(original, postfix: postfixMethod);

                Log.Message(
                    LogPrefix + " Patch installed for " +
                    original.DeclaringType.FullName + "." + original.Name +
                    " with postfix priority Priority.Last (" +
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
        public static void Postfix(
            Pawn pawn,
            SkillDef relevantSkill,
            ref QualityCategory __result)
        {
            if (pawn == null || pawn.genes == null || relevantSkill == null)
            {
                Log.Message(
                    LogPrefix + " Postfix skipped: pawn, genes, or " +
                    "relevant skill is unavailable.");
                return;
            }

            if (relevantSkill == SkillDefOf.Artistic &&
                HasActiveGene(pawn, "RC_Mutation_ArtisticAptitude"))
            {
                QualityCategory previous = __result;
                __result = QualityCategory.Legendary;
                LogQualityChange(
                    pawn,
                    relevantSkill,
                    "RC_Mutation_ArtisticAptitude",
                    previous,
                    __result);
                return;
            }

            if (relevantSkill == SkillDefOf.Crafting &&
                HasActiveGene(pawn, "RC_Mutation_CraftingAptitude"))
            {
                QualityCategory previous = __result;
                __result = AddQuality(__result, 1);
                LogQualityChange(
                    pawn,
                    relevantSkill,
                    "RC_Mutation_CraftingAptitude",
                    previous,
                    __result);
                return;
            }

            if (relevantSkill == SkillDefOf.Construction &&
                HasActiveGene(pawn, "RC_Mutation_ConstructionAptitude"))
            {
                QualityCategory previous = __result;
                __result = AddQuality(__result, 1);
                LogQualityChange(
                    pawn,
                    relevantSkill,
                    "RC_Mutation_ConstructionAptitude",
                    previous,
                    __result);
            }
        }

        private static bool HasActiveGene(Pawn pawn, string defName)
        {
            GeneDef geneDef =
                DefDatabase<GeneDef>.GetNamedSilentFail(defName);

            if (geneDef == null)
            {
                Log.Warning(
                    LogPrefix + " Gene definition was not found: " +
                    defName + ".");
                return false;
            }

            return pawn.genes.HasActiveGene(geneDef);
        }

        private static QualityCategory AddQuality(
            QualityCategory quality,
            int levels)
        {
            return (QualityCategory)Math.Min(
                (int)quality + levels,
                (int)QualityCategory.Legendary);
        }

        private static void LogQualityChange(
            Pawn pawn,
            SkillDef relevantSkill,
            string geneDefName,
            QualityCategory previous,
            QualityCategory current)
        {
            Log.Message(
                LogPrefix + " Pawn=" + pawn.LabelShort +
                "; skill=" + relevantSkill.defName +
                "; gene=" + geneDefName +
                "; quality=" + previous + " -> " + current + ".");
        }
    }
}
