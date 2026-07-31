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
    public static class PherocoreBalanceIntegration
    {
        private const string LogPrefix = "[RedCrow Pherocores]";
        private const string GenelineGeneDefTypeName =
            "VanillaRacesExpandedInsector.GenelineGeneDef";
        private const string UnlockedGenesTypeName =
            "VanillaRacesExpandedInsector.WorldComponent_UnlockedGenes";
        private const string IngestJobTypeName =
            "VanillaRacesExpandedInsector.JobDriver_IngestPherocore";
        private const string UtilsTypeName =
            "VanillaRacesExpandedInsector.Utils";

        private sealed class BalanceEntry
        {
            public readonly string DefName;
            public readonly bool IsMutation;
            public readonly int Points;
            public readonly int Tier;

            public BalanceEntry(
                string defName,
                bool isMutation,
                int points,
                int tier)
            {
                DefName = defName;
                IsMutation = isMutation;
                Points = points;
                Tier = tier;
            }
        }

        private sealed class PoolBinding
        {
            public readonly int Tier;
            public readonly string DictionaryField;
            public readonly string CompleteField;
            public readonly string CoreName;

            public PoolBinding(
                int tier,
                string dictionaryField,
                string completeField,
                string coreName)
            {
                Tier = tier;
                DictionaryField = dictionaryField;
                CompleteField = completeField;
                CoreName = coreName;
            }
        }

        private static readonly BalanceEntry[] BalanceEntries =
        {
            new BalanceEntry("VRE_JellySacks", false, 1, 0),
            new BalanceEntry("RC_Mutation_MildPhotophobia", true, 1, 0),
            new BalanceEntry("RC_Mutation_HeavyCasteStride", true, 1, 0),
            new BalanceEntry("RC_Mutation_RavenousCrop", true, 3, 0),
            new BalanceEntry("RC_Mutation_PorousJellyReservoir", true, 3, 0),
            new BalanceEntry("RC_Evolution_EfficientCrop", false, 3, 0),
            new BalanceEntry("RC_Evolution_JellyConservation", false, 3, 0),
            new BalanceEntry("VRE_HypothermicHibernation", true, 2, 0),
            new BalanceEntry("VRE_OcelliEyes", true, 2, 0),
            new BalanceEntry("VRE_PorousSkin", true, 2, 0),
            new BalanceEntry("VRE_VestigialTubules", true, 2, 0),
            new BalanceEntry("VRE_VocalChitters", true, 2, 0),
            new BalanceEntry("RC_Mutation_BiologicalSickle", true, 1, 0),
            new BalanceEntry("RC_Mutation_BiologicalHandaxe", true, 1, 0),
            new BalanceEntry("RC_Mutation_BiologicalDiggingTools", true, 1, 0),
            new BalanceEntry("RC_Mutation_BiologicalHammer", true, 1, 0),
            new BalanceEntry("RC_Evolution_HiveAnimaResonance", false, 1, 0),
            new BalanceEntry("VRE_LowGreyMatter", true, 5, 0),
            new BalanceEntry("VRE_LowOctopamine", true, 5, 0),
            new BalanceEntry("VRE_ProteinDenaturation", true, 5, 0),
            new BalanceEntry("VRE_Parthenogenesis", false, 1, 0),
            new BalanceEntry("VRE_SensitiveBrainGoop", false, 1, 0),
            new BalanceEntry("VRE_SpawningSack", false, 1, 0),
            new BalanceEntry("RC_Evolution_Ambidexterity", false, 1, 1),
            new BalanceEntry("RC_Evolution_ScoutStride", false, 1, 1),
            new BalanceEntry("RC_Mutation_MeleeAptitude", true, 3, 1),
            new BalanceEntry("RC_Mutation_ShootingAptitude", true, 3, 1),
            new BalanceEntry("RC_Mutation_ArtisticAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_MedicineAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_SocialAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_IntellectualAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_JackOfAllTrades", true, 0, 1),
            new BalanceEntry("RC_Evolution_HivePsiResonator", false, 1, 1),
            new BalanceEntry("RC_Mutation_MiningAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_AnimalsAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_CookingAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_PlantsAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_CraftingAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_ConstructionAptitude", true, 2, 1),
            new BalanceEntry("RC_Mutation_SolarVulnerability", true, 2, 1),
            new BalanceEntry("RC_Evolution_StrongBack", false, 1, 1),
            new BalanceEntry("RC_Mutation_Cleaner", true, 0, 1),
            new BalanceEntry("RC_Mutation_DevouringCrop", true, 6, 1),
            new BalanceEntry("RC_Mutation_LeakingJellyReservoir", true, 6, 1),
            new BalanceEntry("RC_Evolution_ClosedDigestiveCycle", false, 6, 1),
            new BalanceEntry("RC_Evolution_SealedJellyReservoir", false, 6, 1),
            new BalanceEntry("RC_Evolution_CargoCarapace", false, 3, 1),
            new BalanceEntry("RC_Evolution_AcceleratedBroodMaturity", false, 3, 1),
            new BalanceEntry("RC_Mutation_HypertrophiedJellyAbdomen", true, 0, 1),
            new BalanceEntry("RC_Evolution_LongImagoCycle", false, 3, 2),
            new BalanceEntry("RC_Evolution_ForagerInstinct", false, 1, 2),
            new BalanceEntry("RC_Evolution_CuriosityMelee", false, 1, 2),
            new BalanceEntry("RC_Evolution_CuriosityIntellectual", false, 1, 2),
            new BalanceEntry("RC_Evolution_CuriositySocial", false, 1, 2),
            new BalanceEntry("RC_Evolution_CuriosityShooting", false, 1, 2),
            new BalanceEntry("RC_Evolution_CuriosityMedicine", false, 1, 2),
            new BalanceEntry("RC_Evolution_CarryingFolds", false, 1, 2),
            new BalanceEntry("RC_Evolution_CuriosityCooking", false, 2, 2),
            new BalanceEntry("RC_Evolution_CuriosityMining", false, 2, 2),
            new BalanceEntry("RC_Evolution_CuriosityPlants", false, 2, 2),
            new BalanceEntry("RC_Evolution_CuriosityConstruction", false, 2, 2),
            new BalanceEntry("RC_Evolution_CuriosityCrafting", false, 2, 2),
            new BalanceEntry("RC_Evolution_CuriosityArtistic", false, 2, 2),
            new BalanceEntry("RC_Evolution_CuriosityAnimals", false, 2, 2),
            new BalanceEntry("RC_Evolution_MineralMandibles", false, 2, 2),
            new BalanceEntry("RC_Evolution_CollectiveSensitivity", false, 2, 2),
            new BalanceEntry("RC_Mutation_ExternalNoiseCutoff", true, 5, 2),
            new BalanceEntry("RC_Mutation_SwarmSensoryCrown", true, 2, 2),
            new BalanceEntry("RC_Mutation_SmallThoracicArms", true, 2, 2),
            new BalanceEntry("RC_Mutation_SolarExhaustion", true, 3, 2),
            new BalanceEntry("RC_Mutation_MeleeEnhancement", true, 3, 2),
            new BalanceEntry("RC_Mutation_RangedEnhancement", true, 3, 2),
            new BalanceEntry("RC_Evolution_CaffeineRejection", false, 1, 3),
            new BalanceEntry("RC_Evolution_ChipfirRejection", false, 1, 3),
            new BalanceEntry("RC_Evolution_RoyalJellyRejection", false, 1, 3),
            new BalanceEntry("RC_Evolution_NaturalSymbiosis", false, 1, 3),
            new BalanceEntry("RC_Evolution_SwarmRunningImpulse", false, 3, 3),
            new BalanceEntry("RC_Evolution_DeepHiveResonance", false, 3, 3),
            new BalanceEntry("RC_Evolution_DulledPain", false, 3, 3),
            new BalanceEntry("RC_Evolution_AgelessImago", false, 3, 3),
            new BalanceEntry("RC_Mutation_ExposedNociceptors", true, 10, 3),
            new BalanceEntry("RC_Mutation_DorsalManipulators", true, 3, 3),
            new BalanceEntry("RC_Evolution_BroodHyperregeneration", false, 4, 4),
            new BalanceEntry("RC_Evolution_PainCutoff", false, 4, 4),
            new BalanceEntry("RC_Evolution_HunterBurst", false, 4, 4),
            new BalanceEntry("RC_Evolution_CompressedRestCycle", false, 4, 4),
            new BalanceEntry("RC_Evolution_UnityEuphoria", false, 4, 4),
            new BalanceEntry("RC_Evolution_EfficientHiveMetabolism", false, 4, 4),
            new BalanceEntry("RC_Evolution_HivePsyfocusRecycling", false, 4, 4),
            new BalanceEntry("RC_Evolution_HiveRegeneratorCells", false, 4, 4),
            new BalanceEntry("RC_Evolution_LarvalRebirth", false, 4, 4),
            new BalanceEntry("RC_Mutation_PelvicWalkingLimbs", true, 1, 4),
            new BalanceEntry("RC_Evolution_ArchiteNutrition", false, 10, 5),
            new BalanceEntry("RC_Evolution_ContinuousWakefulness", false, 10, 5),
            new BalanceEntry("RC_Evolution_EmotionalSilence", false, 10, 5),
            new BalanceEntry("RC_Evolution_SegmentRestoration", false, 5, 5),
            new BalanceEntry("RC_Evolution_UsurpationLarva", false, 5, 5),
            new BalanceEntry("RC_Evolution_HiveMemoryEgg", false, 5, 5),
            new BalanceEntry("RC_Evolution_PerfectImago", false, 5, 5),
            new BalanceEntry("RC_Mutation_DuplicateCerebellum", true, 1, 5)
        };

        private static readonly PoolBinding[] PoolBindings =
        {
            new PoolBinding(
                1,
                "sorne_pherocore_genes",
                "allSorneGenesUnlocked",
                "Sorne"),
            new PoolBinding(
                2,
                "nuchadus_pherocore_genes",
                "allNuchadusGenesUnlocked",
                "Nuchadus"),
            new PoolBinding(
                3,
                "chelis_pherocore_genes",
                "allChelisGenesUnlocked",
                "Chelis"),
            new PoolBinding(
                4,
                "kemia_pherocore_genes",
                "allKemiaGenesUnlocked",
                "Kemian"),
            new PoolBinding(
                5,
                "xanides_pherocore_genes",
                "allXanidesGenesUnlocked",
                "Xanides")
        };

        static PherocoreBalanceIntegration()
        {
            try
            {
                ApplyBalanceAndUnlockability();
                InstallPatches();
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Initialization failed:\n" +
                    exception);
            }
        }

        private static void ApplyBalanceAndUnlockability()
        {
            Type genelineType = AccessTools.TypeByName(
                GenelineGeneDefTypeName);
            if (genelineType == null)
            {
                Log.Error(
                    LogPrefix + " GenelineGeneDef type was not found.");
                return;
            }

            FieldInfo mutationField =
                AccessTools.Field(genelineType, "mutation");
            FieldInfo evolutionField =
                AccessTools.Field(genelineType, "evolution");
            FieldInfo unlockableField =
                AccessTools.Field(genelineType, "unlockable");

            if (mutationField == null ||
                evolutionField == null ||
                unlockableField == null)
            {
                Log.Error(
                    LogPrefix + " Required Geneline fields were not found.");
                return;
            }

            int applied = 0;
            List<string> missing = new List<string>();

            for (int index = 0;
                index < BalanceEntries.Length;
                index++)
            {
                BalanceEntry entry = BalanceEntries[index];
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    entry.DefName);
                if (gene == null ||
                    !genelineType.IsInstanceOfType(gene))
                {
                    missing.Add(entry.DefName);
                    continue;
                }

                mutationField.SetValue(
                    gene,
                    entry.IsMutation ? entry.Points : 0);
                evolutionField.SetValue(
                    gene,
                    entry.IsMutation ? 0 : entry.Points);
                unlockableField.SetValue(
                    gene,
                    entry.Tier > 0);
                applied++;
            }

            Log.Message(
                LogPrefix + " Applied points and unlockability to " +
                applied + " Geneline defs. Tier 0 remains available by " +
                "default; tiers 1-5 require pherocores.");

            if (missing.Count > 0)
            {
                Log.Warning(
                    LogPrefix + " Optional or missing defs skipped: " +
                    string.Join(", ", missing.ToArray()));
            }
        }

        private static void InstallPatches()
        {
            Harmony harmony = new Harmony(
                "RedCrow.InsectorTweaks.PherocoreBalanceIntegration");

            Type unlockedType = AccessTools.TypeByName(
                UnlockedGenesTypeName);
            MethodInfo finalizeInit = unlockedType == null
                ? null
                : AccessTools.Method(
                    unlockedType,
                    "FinalizeInit",
                    new[] { typeof(bool) });
            MethodInfo finalizePostfix = AccessTools.Method(
                typeof(PherocoreBalanceIntegration),
                "FinalizeInitPostfix");

            if (finalizeInit != null && finalizePostfix != null)
            {
                HarmonyMethod postfix =
                    new HarmonyMethod(finalizePostfix);
                postfix.priority = Priority.Last;
                harmony.Patch(
                    finalizeInit,
                    postfix: postfix);
            }
            else
            {
                Log.Warning(
                    LogPrefix + " WorldComponent_UnlockedGenes." +
                    "FinalizeInit was not found; pherocore pools were not " +
                    "extended.");
            }

            Type ingestType = AccessTools.TypeByName(
                IngestJobTypeName);
            MethodInfo classifier = ingestType == null
                ? null
                : AccessTools.Method(
                    ingestType,
                    "IsEvolutionOrMutation");
            MethodInfo classifierPrefix = AccessTools.Method(
                typeof(PherocoreBalanceIntegration),
                "IsEvolutionOrMutationPrefix");

            if (classifier != null && classifierPrefix != null)
            {
                harmony.Patch(
                    classifier,
                    prefix: new HarmonyMethod(classifierPrefix));
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void FinalizeInitPostfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                int added = 0;
                for (int index = 0;
                    index < PoolBindings.Length;
                    index++)
                {
                    added += EnsurePool(
                        __instance,
                        PoolBindings[index]);
                }

                ClearGeneListCache();

                if (added > 0)
                {
                    Log.Message(
                        LogPrefix + " Added " + added +
                        " missing RedCrow genes to saved pherocore pools.");
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Failed to extend pherocore pools:\n" +
                    exception);
            }
        }

        private static int EnsurePool(
            object component,
            PoolBinding binding)
        {
            Type componentType = component.GetType();
            FieldInfo dictionaryField = AccessTools.Field(
                componentType,
                binding.DictionaryField);
            FieldInfo completeField = AccessTools.Field(
                componentType,
                binding.CompleteField);

            if (dictionaryField == null || completeField == null)
            {
                Log.Warning(
                    LogPrefix + " Fields for " + binding.CoreName +
                    " pherocore were not found.");
                return 0;
            }

            IDictionary dictionary =
                dictionaryField.GetValue(component) as IDictionary;
            if (dictionary == null)
            {
                dictionary =
                    Activator.CreateInstance(
                        dictionaryField.FieldType) as IDictionary;
                dictionaryField.SetValue(component, dictionary);
            }

            if (dictionary == null)
            {
                return 0;
            }

            int added = 0;
            for (int index = 0;
                index < BalanceEntries.Length;
                index++)
            {
                BalanceEntry entry = BalanceEntries[index];
                if (entry.Tier != binding.Tier)
                {
                    continue;
                }

                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    entry.DefName);
                if (gene == null || dictionary.Contains(gene))
                {
                    continue;
                }

                dictionary.Add(gene, false);
                added++;
            }

            bool allUnlocked = true;
            foreach (DictionaryEntry pair in dictionary)
            {
                if (pair.Value is bool &&
                    !(bool)pair.Value)
                {
                    allUnlocked = false;
                    break;
                }
            }

            completeField.SetValue(component, allUnlocked);
            return added;
        }

        private static void ClearGeneListCache()
        {
            Type utilsType = AccessTools.TypeByName(UtilsTypeName);
            FieldInfo cacheField = utilsType == null
                ? null
                : AccessTools.Field(
                    utilsType,
                    "cachedGeneDefsInOrder");
            if (cacheField != null)
            {
                cacheField.SetValue(null, null);
            }
        }

        public static bool IsEvolutionOrMutationPrefix(
            object gene,
            ref string __result)
        {
            Def def = gene as Def;
            if (def == null)
            {
                return true;
            }

            for (int index = 0;
                index < BalanceEntries.Length;
                index++)
            {
                BalanceEntry entry = BalanceEntries[index];
                if (entry.DefName != def.defName)
                {
                    continue;
                }

                __result = entry.IsMutation
                    ? "mutation"
                    : "evolution";
                return false;
            }

            return true;
        }
    }
}
