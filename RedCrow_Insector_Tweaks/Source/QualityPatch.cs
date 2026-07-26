using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            var harmony = new Harmony("RedCrow.InsectorTweaks.Quality");
            MethodInfo original = AccessTools.Method(
                typeof(QualityUtility),
                "GenerateQualityCreatedByPawn",
                new[] { typeof(Pawn), typeof(SkillDef) });
            MethodInfo postfix = AccessTools.Method(typeof(Core), nameof(Postfix));
            harmony.Patch(original, postfix: new HarmonyMethod(postfix));
        }

        public static void Postfix(Pawn pawn, SkillDef relevantSkill,
            ref QualityCategory __result)
        {
            if (pawn?.genes == null)
                return;

            if (relevantSkill == SkillDefOf.Artistic &&
                HasActiveGene(pawn, "RC_Mutation_ArtisticAptitude"))
            {
                __result = QualityCategory.Legendary;
                return;
            }

            if (relevantSkill == SkillDefOf.Crafting &&
                HasActiveGene(pawn, "RC_Mutation_CraftingAptitude"))
            {
                __result = AddQuality(__result, 1);
                return;
            }

            if (relevantSkill == SkillDefOf.Construction &&
                HasActiveGene(pawn, "RC_Mutation_ConstructionAptitude"))
            {
                __result = AddQuality(__result, 1);
            }
        }

        private static bool HasActiveGene(Pawn pawn, string defName)
        {
            GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
            return gene != null && pawn.genes.HasActiveGene(gene);
        }

        private static QualityCategory AddQuality(QualityCategory quality, int levels)
        {
            return (QualityCategory)Math.Min(
                (int)QualityCategory.Legendary,
                (int)quality + levels);
        }
    }
}
