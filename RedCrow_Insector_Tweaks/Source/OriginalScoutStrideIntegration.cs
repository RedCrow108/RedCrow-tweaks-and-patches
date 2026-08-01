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
            "[RedCrow Original Scout Stride]";
        private const string RemovedDefName =
            "RC_Evolution_ScoutStride";
        private const string ComponentTypeName =
            "VanillaRacesExpandedInsector.GameComponent_UnlockedGenes";
        private const string GeneTypeName =
            "VanillaRacesExpandedInsector.GenelineGeneDef";

        private static bool logged;

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
                        LogPrefix +
                        " Game.FinalizeInit could not be patched.");
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
                if (original == null)
                {
                    Log.Warning(
                        LogPrefix +
                        " Original evolution was not found. Checked labels " +
                        "and the +0.2 MoveSpeed signature.");
                    return;
                }

                Type geneType = AccessTools.TypeByName(GeneTypeName);
                FieldInfo unlockableField = geneType == null
                    ? null
                    : AccessTools.Field(geneType, "unlockable");
                if (unlockableField != null &&
                    geneType.IsInstanceOfType(original))
                {
                    unlockableField.SetValue(original, true);
                }

                Type componentType =
                    AccessTools.TypeByName(ComponentTypeName);
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
                        LogPrefix +
                        " Sorne pherocore pool was not available.");
                    return;
                }

                bool migratedUnlockedState = false;
                List<object> staleKeys = new List<object>();
                foreach (DictionaryEntry pair in pool)
                {
                    Def def = pair.Key as Def;
                    if (def == null ||
                        def.defName != RemovedDefName)
                    {
                        continue;
                    }

                    if (pair.Value is bool && (bool)pair.Value)
                    {
                        migratedUnlockedState = true;
                    }
                    staleKeys.Add(pair.Key);
                }

                for (int index = 0;
                    index < staleKeys.Count;
                    index++)
                {
                    pool.Remove(staleKeys[index]);
                }

                if (pool.Contains(original))
                {
                    if (migratedUnlockedState)
                    {
                        pool[original] = true;
                    }
                }
                else
                {
                    pool.Add(original, migratedUnlockedState);
                }

                bool allUnlocked = pool.Count > 0;
                foreach (DictionaryEntry pair in pool)
                {
                    if (!(pair.Value is bool) ||
                        !(bool)pair.Value)
                    {
                        allUnlocked = false;
                        break;
                    }
                }

                if (allField != null)
                {
                    allField.SetValue(component, allUnlocked);
                }

                ClearGeneListCache();

                if (!logged)
                {
                    logged = true;
                    string source =
                        original.modContentPack == null
                            ? "<no-source>"
                            : original.modContentPack.PackageId;
                    Log.Message(
                        LogPrefix + " Using original " +
                        original.defName + " from " + source +
                        "; gated by Sorne, migrated unlocked=" +
                        migratedUnlockedState +
                        ", pool count=" + pool.Count + ".");
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Integration failed:\n" +
                    exception);
            }
        }

        private static GeneDef FindOriginalScoutStride()
        {
            Type geneType = AccessTools.TypeByName(GeneTypeName);
            if (geneType == null)
            {
                return null;
            }

            GeneDef movementFallback = null;
            List<GeneDef> genes =
                DefDatabase<GeneDef>.AllDefsListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                GeneDef gene = genes[index];
                if (gene == null ||
                    gene.defName == RemovedDefName ||
                    !geneType.IsInstanceOfType(gene) ||
                    IsRedCrowDef(gene) ||
                    !IsEvolution(gene, geneType))
                {
                    continue;
                }

                string rawLabel = gene.label ?? string.Empty;
                string translatedLabel = gene.LabelCap.ToString();
                if (IsScoutStrideLabel(rawLabel) ||
                    IsScoutStrideLabel(translatedLabel))
                {
                    return gene;
                }

                if (movementFallback == null &&
                    HasMoveSpeedOffset(gene, 0.2f))
                {
                    movementFallback = gene;
                }
            }

            return movementFallback;
        }

        private static bool IsEvolution(
            GeneDef gene,
            Type geneType)
        {
            FieldInfo evolutionField =
                AccessTools.Field(geneType, "evolution");
            if (evolutionField == null)
            {
                return true;
            }

            object value = evolutionField.GetValue(gene);
            return value is int && (int)value > 0;
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

        private static bool HasMoveSpeedOffset(
            GeneDef gene,
            float expected)
        {
            if (gene.statOffsets == null)
            {
                return false;
            }

            for (int index = 0;
                index < gene.statOffsets.Count;
                index++)
            {
                StatModifier modifier = gene.statOffsets[index];
                if (modifier == null ||
                    modifier.stat == null ||
                    modifier.stat.defName != "MoveSpeed")
                {
                    continue;
                }

                if (Math.Abs(modifier.value - expected) < 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearGeneListCache()
        {
            Type utilsType = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.Utils");
            FieldInfo cacheField = utilsType == null
                ? null
                : AccessTools.Field(
                    utilsType,
                    "cachedGeneDefsInOrder");
            if (cacheField != null && cacheField.IsStatic)
            {
                cacheField.SetValue(null, null);
            }
        }
    }
}