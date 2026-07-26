using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    public sealed class InsectorTweaksMod : Mod
    {
        public InsectorTweaksMod(ModContentPack content) : base(content)
        {
            new Harmony("RedCrow.InsectorTweaks").PatchAll();
        }
    }

    [HarmonyPatch]
    public static class QualityPatch
    {
        private static readonly GeneDef CraftingGene =
            DefDatabase<GeneDef>.GetNamedSilentFail(
                "RC_Mutation_CraftingAptitude");

        private static readonly GeneDef ConstructionGene =
            DefDatabase<GeneDef>.GetNamedSilentFail(
                "RC_Mutation_ConstructionAptitude");

        private static readonly GeneDef ArtisticGene =
            DefDatabase<GeneDef>.GetNamedSilentFail(
                "RC_Mutation_ArtisticAptitude");

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(QualityUtility),
                nameof(QualityUtility.GenerateQualityCreatedByPawn),
                new[] { typeof(Pawn), typeof(SkillDef) });
        }

        [HarmonyPostfix]
        public static void Postfix(
            Pawn pawn,
            SkillDef relevantSkill,
            ref QualityCategory __result)
        {
            if (pawn?.genes == null)
                return;

            if (relevantSkill == SkillDefOf.Artistic &&
                ArtisticGene != null &&
                pawn.genes.HasActiveGene(ArtisticGene))
            {
                __result = QualityCategory.Legendary;
                return;
            }

            if (relevantSkill == SkillDefOf.Crafting &&
                CraftingGene != null &&
                pawn.genes.HasActiveGene(CraftingGene))
            {
                __result = AddQuality(__result, 1);
                return;
            }

            if (relevantSkill == SkillDefOf.Construction &&
                ConstructionGene != null &&
                pawn.genes.HasActiveGene(ConstructionGene))
            {
                __result = AddQuality(__result, 1);
            }
        }

        private static QualityCategory AddQuality(
            QualityCategory quality,
            int levels)
        {
            return (QualityCategory)Math.Min(
                (int)quality + levels,
                (int)QualityCategory.Legendary);
        }
    }
}
