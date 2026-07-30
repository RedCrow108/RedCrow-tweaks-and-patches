using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class HiveInsectFilthPatch
    {
        private const string HarmonyId =
            "RedCrow.InsectorTweaks.HiveInsectFilth";

        static HiveInsectFilthPatch()
        {
            MethodInfo original = AccessTools.Method(
                typeof(StatExtension),
                "GetStatValue",
                new[]
                {
                    typeof(Thing),
                    typeof(StatDef),
                    typeof(bool),
                    typeof(int)
                });
            MethodInfo postfix = AccessTools.Method(
                typeof(
                    StatExtension_GetStatValue_HiveInsectFilthPatch),
                "Postfix");
            if (original == null || postfix == null)
            {
                Log.Error(
                    "[RedCrow Hive Filth] Required FilthRate " +
                    "patch method was not found.");
                return;
            }

            HarmonyMethod postfixPatch =
                new HarmonyMethod(postfix);
            postfixPatch.priority = Priority.Last;
            new Harmony(HarmonyId).Patch(
                original,
                postfix: postfixPatch);
            Log.Message(
                "[RedCrow Hive Filth] Regular animal filth/slime " +
                "FilthRate override " +
                "installed for " +
                RC_HiveInsectFilthUtility.TargetCount +
                " explicit PawnKindDef names.");
        }
    }

    public static class RC_HiveInsectFilthUtility
    {
        private static readonly HashSet<string>
            TargetPawnKindDefNames =
                new HashSet<string>(
                    new[]
                    {
                        "Megascarab",
                        "Spelopede",
                        "Megaspider",
                        "VFEI2_Megapede",
                        "VFEI2_Queen",
                        "VFEI2_Swarmling",
                        "VFEI2_Boomtick",
                        "VFEI2_Hellbeetle",
                        "VFEI2_Fuelmite",
                        "VFEI2_Macrofly",
                        "VFEI2_Megawasp",
                        "VFEI2_Gigalocust",
                        "VFEI2_Megathrips",
                        "VFEI2_Venomite",
                        "VFEI2_Acidspitter",
                        "VFEI2_Durapod",
                        "VFEI2_Tankroach",
                        "VFEI2_Ironclad",
                        "AA_MammothWorm",
                        "AA_MegaLouse",
                        "AA_Ravager",
                        "AA_BlackScarab",
                        "AA_BlackSpelopede",
                        "AA_BlackSpider",
                        "VFEI2_BlackQueen",
                        "VFEI2_BlackSwarmling"
                    },
                    StringComparer.Ordinal);

        public static int TargetCount
        {
            get
            {
                return TargetPawnKindDefNames.Count;
            }
        }

        public static bool IsTarget(Pawn pawn)
        {
            return pawn != null &&
                pawn.kindDef != null &&
                pawn.RaceProps != null &&
                pawn.RaceProps.Animal &&
                TargetPawnKindDefNames.Contains(
                    pawn.kindDef.defName);
        }
    }

    public static class
        StatExtension_GetStatValue_HiveInsectFilthPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(
            Thing thing,
            StatDef stat,
            ref float __result)
        {
            if (stat == StatDefOf.FilthRate &&
                RC_HiveInsectFilthUtility.IsTarget(
                    thing as Pawn))
            {
                __result = 0f;
            }
        }
    }
}
