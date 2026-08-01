using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class OriginalScoutStrideIntegration
    {
        private const string LogPrefix =
            "[RedCrow Scout Stride]";
        private const string FallbackDefName =
            "RC_Evolution_ScoutStride";
        private const string ComponentTypeName =
            "VanillaRacesExpandedInsector.GameComponent_UnlockedGenes";
        private const string GeneTypeName =
            "VanillaRacesExpandedInsector.GenelineGeneDef";

        static OriginalScoutStrideIntegration()
        {
            try
            {
                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.OriginalScoutStrideIntegration");
                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(OriginalScoutStrideIntegration),
                    "GameFinalizeInitPostfix");
                if (target == null || postfixMethod == null)
                {
                    Log.Error(
                        LogPrefix + " Game.FinalizeInit could not be patched.");
                    return;
                }

                HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                postfix.priority = Priority.Last;
                postfix.after = new[]
                {
                    "RedCrow.InsectorTweaks.PherocoreGameComponentHotfix",
                    "RedCrow.InsectorTweaks.PherocoreInteractionAndSynapticHotfix"
                };
                harmony.Patch(target, postfix: postfix);
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            try
            {
                GeneDef original = FindOriginalScoutStride();
                GeneDef fallback =
                    DefDatabase<GeneDef>.GetNamedSilentFail(FallbackDefName);
                GeneDef available = original ?? fallback;
                if (available == null)
                {
                    Log.Error(
                        LogPrefix + " Neither an upstream original nor the " +
                        "compatibility fallback was found.");
                    return;
                }

                SetStartingAvailability(available);
                if (fallback != null)
                {
                    SetStartingAvailability(fallback);
                }

                int removed = RemoveFromSornePool(original, fallback);
                ClearGeneListCache();

                string source = available.modContentPack == null
                    ? "<no-source>"
                    : available.modContentPack.PackageId;
                Log.Message(
                    LogPrefix + " Available from the start: " +
                    available.defName + " from " + source +
                    "; removed from Sorne=" + removed +
                    "; upstream original=" + (original != null) + ".");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Integration failed:\n" + exception);
            }
        }

        private static void SetStartingAvailability(GeneDef gene)
        {
            Type geneType = AccessTools.TypeByName(GeneTypeName);
            FieldInfo unlockableField = geneType == null
                ? null
                : AccessTools.Field(geneType, "unlockable");
            if (unlockableField != null &&
                geneType.IsInstanceOfType(gene))
            {
                unlockableField.SetValue(gene, false);
            }
        }

        private static int RemoveFromSornePool(
            GeneDef original,
            GeneDef fallback)
        {
            Type componentType = AccessTools.TypeByName(ComponentTypeName);
            FieldInfo instanceField = componentType == null
                ? null
                : AccessTools.Field(componentType, "Instance");
            object component = instanceField == null
                ? null
                : instanceField.GetValue(null);
            FieldInfo poolField = componentType == null
                ? null
                : AccessTools.Field(
                    componentType,
                    "sorne_pherocore_genes");
            FieldInfo allField = componentType == null
                ? null
                : AccessTools.Field(
                    componentType,
                    "allSorneGenesUnlocked");
            IDictionary pool = component == null || poolField == null
                ? null
                : poolField.GetValue(component) as IDictionary;
            if (pool == null)
            {
                Log.Warning(
                    LogPrefix + " Sorne pherocore pool was unavailable.");
                return 0;
            }

            List<object> staleKeys = new List<object>();
            foreach (DictionaryEntry pair in pool)
            {
                GeneDef candidate = pair.Key as GeneDef;
                if (candidate == null)
                {
                    continue;
                }

                if (candidate == original ||
                    candidate == fallback ||
                    candidate.defName == FallbackDefName ||
                    IsScoutStrideLabel(candidate.label) ||
                    IsScoutStrideLabel(candidate.LabelCap.ToString()))
                {
                    staleKeys.Add(pair.Key);
                }
            }

            for (int index = 0; index < staleKeys.Count; index++)
            {
                pool.Remove(staleKeys[index]);
            }

            bool allUnlocked = pool.Count > 0;
            foreach (DictionaryEntry pair in pool)
            {
                if (!(pair.Value is bool) || !(bool)pair.Value)
                {
                    allUnlocked = false;
                    break;
                }
            }
            if (allField != null)
            {
                allField.SetValue(component, allUnlocked);
            }

            return staleKeys.Count;
        }

        private static GeneDef FindOriginalScoutStride()
        {
            Type geneType = AccessTools.TypeByName(GeneTypeName);
            if (geneType == null)
            {
                return null;
            }

            List<GeneDef> genes =
                DefDatabase<GeneDef>.AllDefsListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                GeneDef gene = genes[index];
                if (gene == null ||
                    gene.defName == FallbackDefName ||
                    !geneType.IsInstanceOfType(gene) ||
                    IsRedCrowDef(gene))
                {
                    continue;
                }

                if (IsScoutStrideLabel(gene.label) ||
                    IsScoutStrideLabel(gene.LabelCap.ToString()))
                {
                    return gene;
                }
            }

            return null;
        }

        private static bool IsRedCrowDef(Def def)
        {
            return def.modContentPack != null &&
                string.Equals(
                    def.modContentPack.PackageId,
                    "redcrow.insectortweaks",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsScoutStrideLabel(string value)
        {
            string normalized =
                (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "бег дозорного" ||
                normalized.Contains("scout stride") ||
                normalized.Contains("scout run");
        }

        private static void ClearGeneListCache()
        {
            Type utilsType = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.Utils");
            FieldInfo cacheField = utilsType == null
                ? null
                : AccessTools.Field(utilsType, "cachedGeneDefsInOrder");
            if (cacheField != null &&
                cacheField.IsStatic &&
                !cacheField.IsInitOnly)
            {
                cacheField.SetValue(null, null);
            }
        }
    }
}
