using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class HypothermicHibernationMetabolismFix
    {
        private const string LogPrefix =
            "[RedCrow Hypothermic Hibernation]";
        private const string HypothermicDefName =
            "VRE_HypothermicHibernation";
        private const string TemperatureExclusionTag =
            "MinTemperature";

        static HypothermicHibernationMetabolismFix()
        {
            try
            {
                GeneDef hypothermicDef =
                    DefDatabase<GeneDef>.GetNamedSilentFail(
                        HypothermicDefName);
                if (hypothermicDef == null)
                {
                    Log.Warning(
                        LogPrefix + " GeneDef was not found; fix skipped.");
                    return;
                }

                int removedTags = 0;
                if (hypothermicDef.exclusionTags != null)
                {
                    removedTags = hypothermicDef.exclusionTags.RemoveAll(
                        tag => tag == TemperatureExclusionTag);
                }

                MethodInfo checkForOverrides = AccessTools.Method(
                    typeof(Pawn_GeneTracker),
                    "CheckForOverrides");
                MethodInfo exposeData = AccessTools.Method(
                    typeof(Pawn_GeneTracker),
                    "ExposeData");
                MethodInfo checkPostfix = AccessTools.Method(
                    typeof(HypothermicHibernationMetabolismFix),
                    "CheckForOverridesPostfix");
                MethodInfo exposePostfix = AccessTools.Method(
                    typeof(HypothermicHibernationMetabolismFix),
                    "ExposeDataPostfix");

                if (checkForOverrides == null ||
                    exposeData == null ||
                    checkPostfix == null ||
                    exposePostfix == null)
                {
                    Log.Error(
                        LogPrefix + " Lifecycle patch installation failed: " +
                        "one or more gene tracker methods were not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks." +
                    "HypothermicHibernationMetabolismFix");

                HarmonyMethod checkPatch =
                    new HarmonyMethod(checkPostfix);
                checkPatch.priority = Priority.Last;
                harmony.Patch(
                    checkForOverrides,
                    postfix: checkPatch);

                HarmonyMethod exposePatch =
                    new HarmonyMethod(exposePostfix);
                exposePatch.priority = Priority.Last;
                harmony.Patch(
                    exposeData,
                    postfix: exposePatch);

                bool tagStillPresent =
                    hypothermicDef.exclusionTags != null &&
                    hypothermicDef.exclusionTags.Contains(
                        TemperatureExclusionTag);

                Log.Message(
                    LogPrefix +
                    " MinTemperature conflict removed=" +
                    removedTags +
                    "; still present=" +
                    tagStillPresent +
                    "; saved override cleanup installed.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Fix installation failed:\n" +
                    exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void CheckForOverridesPostfix(
            Pawn_GeneTracker __instance)
        {
            ClearInvalidHypothermicOverrides(
                __instance,
                "override recalculation");
        }

        [HarmonyPriority(Priority.Last)]
        public static void ExposeDataPostfix(
            Pawn_GeneTracker __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            ClearInvalidHypothermicOverrides(
                __instance,
                "post-load recovery");
        }

        private static void ClearInvalidHypothermicOverrides(
            Pawn_GeneTracker tracker,
            string reason)
        {
            if (tracker == null)
            {
                return;
            }

            List<Gene> genes = tracker.GenesListForReading;
            int cleared = 0;

            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                Gene suppressor = gene == null
                    ? null
                    : gene.overriddenByGene;

                if (gene == null ||
                    gene.def == null ||
                    suppressor == null ||
                    suppressor.def == null ||
                    suppressor.def.defName != HypothermicDefName)
                {
                    continue;
                }

                if (SharesExclusionTag(gene.def, suppressor.def))
                {
                    continue;
                }

                gene.OverrideBy(null);
                cleared++;
            }

            if (cleared == 0)
            {
                return;
            }

            Pawn pawn = tracker.pawn;
            string pawnLabel = pawn == null
                ? "<null>"
                : pawn.LabelShort + " (" + pawn.ThingID + ")";

            Log.Message(
                LogPrefix +
                " Pawn=" + pawnLabel +
                "; reason=" + reason +
                "; cleared invalid saved overrides=" +
                cleared + ".");
        }

        private static bool SharesExclusionTag(
            GeneDef first,
            GeneDef second)
        {
            if (first == null ||
                second == null ||
                first.exclusionTags == null ||
                second.exclusionTags == null)
            {
                return false;
            }

            for (int firstIndex = 0;
                firstIndex < first.exclusionTags.Count;
                firstIndex++)
            {
                string tag = first.exclusionTags[firstIndex];
                if (!tag.NullOrEmpty() &&
                    second.exclusionTags.Contains(tag))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
