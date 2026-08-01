using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class EarlyWorkMaturity
    {
        private const string GeneDefName =
            "RC_Evolution_AcceleratedBroodMaturity";
        private const string LogPrefix =
            "[RedCrow Early Work Maturity]";

        static EarlyWorkMaturity()
        {
            try
            {
                MethodInfo target = AccessTools.Method(
                    typeof(LifeStageWorkSettings),
                    "IsDisabled");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(EarlyWorkMaturity),
                    "IsDisabledPostfix");

                if (target == null || postfixMethod == null)
                {
                    Log.Error(
                        LogPrefix + " LifeStageWorkSettings.IsDisabled " +
                        "could not be patched.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.EarlyWorkMaturity");
                HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                postfix.priority = Priority.Last;
                harmony.Patch(target, postfix: postfix);

                Log.Message(
                    LogPrefix + " Work-age thresholds advance by one " +
                    "juvenile work stage for active gene carriers.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void IsDisabledPostfix(
            LifeStageWorkSettings __instance,
            Pawn pawn,
            ref bool __result)
        {
            if (!__result ||
                __instance == null ||
                pawn == null ||
                pawn.ageTracker == null ||
                pawn.RaceProps == null ||
                pawn.DevelopmentalStage.Baby() ||
                !HasActiveGene(pawn))
            {
                return;
            }

            int previousUnlockAge = FindPreviousUnlockAge(
                pawn.RaceProps.lifeStageWorkSettings,
                __instance.minAge);
            if (previousUnlockAge > 0 &&
                pawn.ageTracker.AgeBiologicalYears >= previousUnlockAge)
            {
                __result = false;
            }
        }

        private static int FindPreviousUnlockAge(
            List<LifeStageWorkSettings> settings,
            int requiredAge)
        {
            if (settings == null || requiredAge <= 0)
            {
                return 0;
            }

            int previousAge = 0;
            for (int index = 0; index < settings.Count; index++)
            {
                LifeStageWorkSettings candidate = settings[index];
                if (candidate == null)
                {
                    continue;
                }

                int candidateAge = candidate.minAge;
                if (candidateAge > previousAge &&
                    candidateAge > 0 &&
                    candidateAge < requiredAge)
                {
                    previousAge = candidateAge;
                }
            }

            return previousAge;
        }

        private static bool HasActiveGene(Pawn pawn)
        {
            if (pawn.genes == null)
            {
                return false;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene != null &&
                    gene.Active &&
                    gene.def != null &&
                    gene.def.defName == GeneDefName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
