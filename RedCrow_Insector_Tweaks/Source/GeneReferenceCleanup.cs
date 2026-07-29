using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class GeneReferenceCleanup
    {
        private const string LogPrefix = "[RedCrow Gene Cleanup]";
        private const string GenelineGeneDefTypeName =
            "VanillaRacesExpandedInsector.GenelineGeneDef";

        private static readonly MethodInfo CheckForOverridesMethod =
            AccessTools.Method(
                typeof(Pawn_GeneTracker),
                "CheckForOverrides");

        private static readonly MethodInfo NotifyGenesChangedMethod =
            AccessTools.Method(
                typeof(Pawn_GeneTracker),
                "Notify_GenesChanged",
                new[] { typeof(GeneDef) });

        static GeneReferenceCleanup()
        {
            try
            {
                MethodInfo removeGene = AccessTools.Method(
                    typeof(Pawn_GeneTracker),
                    "RemoveGene",
                    new[] { typeof(Gene) });
                MethodInfo exposeData = AccessTools.Method(
                    typeof(Pawn_GeneTracker),
                    "ExposeData");
                MethodInfo removeGenePostfix = AccessTools.Method(
                    typeof(GeneReferenceCleanup),
                    "RemoveGenePostfix");
                MethodInfo exposeDataPostfix = AccessTools.Method(
                    typeof(GeneReferenceCleanup),
                    "ExposeDataPostfix");

                if (removeGene == null ||
                    exposeData == null ||
                    removeGenePostfix == null ||
                    exposeDataPostfix == null ||
                    CheckForOverridesMethod == null ||
                    NotifyGenesChangedMethod == null)
                {
                    Log.Error(
                        LogPrefix + " Patch installation failed: one or " +
                        "more RimWorld gene lifecycle methods were not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.GeneReferenceCleanup");

                HarmonyMethod removePostfix =
                    new HarmonyMethod(removeGenePostfix);
                removePostfix.priority = Priority.Last;
                harmony.Patch(removeGene, postfix: removePostfix);

                HarmonyMethod loadPostfix =
                    new HarmonyMethod(exposeDataPostfix);
                loadPostfix.priority = Priority.Last;
                harmony.Patch(exposeData, postfix: loadPostfix);

                Log.Message(
                    LogPrefix + " Patches installed for " +
                    "Pawn_GeneTracker.RemoveGene and ExposeData " +
                    "with postfix priority Priority.Last (" +
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
        public static void RemoveGenePostfix(
            Pawn_GeneTracker __instance,
            Gene gene)
        {
            if (__instance == null || !IsGenelineGene(gene))
            {
                return;
            }

            RepairRelations(
                __instance,
                gene.def,
                "removed Geneline gene " + gene.def.defName,
                logSuccessfulRecalculation: true);
        }

        [HarmonyPriority(Priority.Last)]
        public static void ExposeDataPostfix(
            Pawn_GeneTracker __instance)
        {
            if (__instance == null ||
                Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            RepairRelations(
                __instance,
                null,
                "post-load recovery",
                logSuccessfulRecalculation:
                    HasCurrentGenelineGene(__instance));
        }

        private static void RepairRelations(
            Pawn_GeneTracker tracker,
            GeneDef removedGeneDef,
            string reason,
            bool logSuccessfulRecalculation)
        {
            try
            {
                // Let RimWorld and the Insector CheckForOverrides postfix rebuild
                // every relation they know how to handle before touching a field.
                CheckForOverridesMethod.Invoke(tracker, null);

                List<Gene> genes = tracker.GenesListForReading;
                HashSet<Gene> currentGenes = new HashSet<Gene>(genes);
                int staleOverrides = ClearMissingOverrideReferences(
                    genes,
                    currentGenes);
                int staleTraitSuppressions = CountMissingTraitSuppressions(
                    tracker,
                    currentGenes);

                // HSK More Content makes every GeneDef randomChosen. A removed
                // gene can therefore keep its former conflicts inactive during
                // the first vanilla notification. Repeating the stock notify
                // after stale references are gone lets RimWorld choose a current
                // conflicting gene and rebuild the remaining relationships.
                if (removedGeneDef != null)
                {
                    NotifyGenesChangedMethod.Invoke(
                        tracker,
                        new object[] { removedGeneDef });
                    CheckForOverridesMethod.Invoke(tracker, null);
                }

                TraitSet traits = null;
                if (tracker.pawn != null &&
                    tracker.pawn.story != null)
                {
                    traits = tracker.pawn.story.traits;
                }

                if (traits != null)
                {
                    traits.RecalculateSuppression();
                }

                int restoredAbilities =
                    RestoreAbilitiesGrantedByCurrentGenes(
                        tracker,
                        removedGeneDef);

                int remainingOverrides =
                    CountMissingOverrideReferences(tracker);
                int remainingTraitSuppressions =
                    CountMissingTraitSuppressions(
                        tracker,
                        new HashSet<Gene>(
                            tracker.GenesListForReading));

                if (remainingOverrides != 0 ||
                    remainingTraitSuppressions != 0)
                {
                    Log.Error(
                        LogPrefix + " Pawn=" + PawnLabel(tracker) +
                        "; reason=" + reason +
                        "; cleanup incomplete: missing overrides=" +
                        remainingOverrides +
                        ", missing trait suppressions=" +
                        remainingTraitSuppressions + ".");
                    return;
                }

                if (staleOverrides != 0 ||
                    staleTraitSuppressions != 0 ||
                    logSuccessfulRecalculation)
                {
                    Log.Message(
                        LogPrefix + " Pawn=" + PawnLabel(tracker) +
                        "; reason=" + reason +
                        "; cleared missing overrides=" +
                        staleOverrides +
                        ", rebuilt trait suppressions=" +
                        staleTraitSuppressions +
                        ", restored shared abilities=" +
                        restoredAbilities + ".");
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Pawn=" + PawnLabel(tracker) +
                    "; reason=" + reason +
                    "; relation repair failed:\n" +
                    exception);
            }
        }

        private static int ClearMissingOverrideReferences(
            List<Gene> genes,
            HashSet<Gene> currentGenes)
        {
            int cleared = 0;

            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene != null &&
                    gene.overriddenByGene != null &&
                    !currentGenes.Contains(gene.overriddenByGene))
                {
                    gene.OverrideBy(null);
                    cleared++;
                }
            }

            return cleared;
        }

        private static int CountMissingOverrideReferences(
            Pawn_GeneTracker tracker)
        {
            List<Gene> genes = tracker.GenesListForReading;
            HashSet<Gene> currentGenes = new HashSet<Gene>(genes);
            int count = 0;

            for (int index = 0; index < genes.Count; index++)
            {
                Gene gene = genes[index];
                if (gene != null &&
                    gene.overriddenByGene != null &&
                    !currentGenes.Contains(gene.overriddenByGene))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountMissingTraitSuppressions(
            Pawn_GeneTracker tracker,
            HashSet<Gene> currentGenes)
        {
            TraitSet traits = null;
            if (tracker.pawn != null &&
                tracker.pawn.story != null)
            {
                traits = tracker.pawn.story.traits;
            }

            if (traits == null)
            {
                return 0;
            }

            int count = 0;
            List<Trait> allTraits = traits.allTraits;

            for (int index = 0; index < allTraits.Count; index++)
            {
                Trait trait = allTraits[index];
                Gene suppressor = trait == null
                    ? null
                    : trait.suppressedByGene;
                if (suppressor != null &&
                    !currentGenes.Contains(suppressor))
                {
                    count++;
                }
            }

            return count;
        }

        private static int RestoreAbilitiesGrantedByCurrentGenes(
            Pawn_GeneTracker tracker,
            GeneDef removedGeneDef)
        {
            if (removedGeneDef == null ||
                removedGeneDef.abilities == null ||
                removedGeneDef.abilities.Count == 0 ||
                tracker.pawn == null ||
                tracker.pawn.abilities == null)
            {
                return 0;
            }

            int restored = 0;
            List<Gene> genes = tracker.GenesListForReading;

            for (int abilityIndex = 0;
                abilityIndex < removedGeneDef.abilities.Count;
                abilityIndex++)
            {
                AbilityDef ability = removedGeneDef.abilities[abilityIndex];
                if (ability == null ||
                    tracker.pawn.abilities.GetAbility(
                        ability,
                        false) != null)
                {
                    continue;
                }

                bool stillGranted = false;
                for (int geneIndex = 0;
                    geneIndex < genes.Count;
                    geneIndex++)
                {
                    Gene gene = genes[geneIndex];
                    if (gene != null &&
                        gene.def != null &&
                        gene.Active &&
                        gene.def.abilities != null &&
                        gene.def.abilities.Contains(ability))
                    {
                        stillGranted = true;
                        break;
                    }
                }

                if (stillGranted)
                {
                    tracker.pawn.abilities.GainAbility(ability);
                    restored++;
                }
            }

            return restored;
        }

        private static bool HasCurrentGenelineGene(
            Pawn_GeneTracker tracker)
        {
            List<Gene> genes = tracker.GenesListForReading;

            for (int index = 0; index < genes.Count; index++)
            {
                if (IsGenelineGene(genes[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGenelineGene(Gene gene)
        {
            Type type = null;
            if (gene != null && gene.def != null)
            {
                type = gene.def.GetType();
            }

            while (type != null)
            {
                if (type.FullName == GenelineGeneDefTypeName)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static string PawnLabel(
            Pawn_GeneTracker tracker)
        {
            Pawn pawn = tracker == null
                ? null
                : tracker.pawn;
            if (pawn == null)
            {
                return "<null>";
            }

            return pawn.LabelShort + " (" + pawn.ThingID + ")";
        }
    }
}
