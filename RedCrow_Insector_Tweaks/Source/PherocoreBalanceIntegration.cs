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
            public readonly string[] OriginalGenes;

            public PoolBinding(
                int tier,
                string dictionaryField,
                string completeField,
                string coreName,
                params string[] originalGenes)
            {
                Tier = tier;
                DictionaryField = dictionaryField;
                CompleteField = completeField;
                CoreName = coreName;
                OriginalGenes = originalGenes ?? new string[0];
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
                "Sorne",
                "VRE_SwarmSynapse",
                "VRE_RoyalJellyInjector",
                "VRE_Microsized",
                "VRE_Colossal"),
            new PoolBinding(
                2,
                "nuchadus_pherocore_genes",
                "allNuchadusGenesUnlocked",
                "Nuchadus",
                "VRE_PyroResistantChitin",
                "VRE_FlameGlands",
                "VRE_ChemfuelSacks",
                "VRE_Pyrophiliac"),
            new PoolBinding(
                3,
                "chelis_pherocore_genes",
                "allChelisGenesUnlocked",
                "Chelis",
                "VRE_LocustWings",
                "VRE_InsectRostrum",
                "VRE_InsectVolatile",
                "VRE_EcdysoneOverdrive"),
            new PoolBinding(
                4,
                "kemia_pherocore_genes",
                "allKemiaGenesUnlocked",
                "Kemian",
                "VRE_AcidGlands",
                "VRE_InfraredSensors",
                "VRE_AcidBurstSack",
                "VRE_SolidGreyMatter"),
            new PoolBinding(
                5,
                "xanides_pherocore_genes",
                "allXanidesGenesUnlocked",
                "Xanides",
                "VRE_MineralRichInsectskin",
                "VRE_ChargerClaws",
                "VRE_HardLockedJoints",
                "VRE_PassiveInsect")
        };

        private static bool successfulExtensionLogged;

        static PherocoreBalanceIntegration()
        {
            try
            {
                ValidateBalanceAndUnlockability();
                InstallPatches();
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Initialization failed:\n" +
                    exception);
            }
        }

        private static void ValidateBalanceAndUnlockability()
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

            int validated = 0;
            List<string> missing = new List<string>();
            List<string> mismatches = new List<string>();

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

                int actualMutation =
                    (int)mutationField.GetValue(gene);
                int actualEvolution =
                    (int)evolutionField.GetValue(gene);
                bool actualUnlockable =
                    (bool)unlockableField.GetValue(gene);

                int expectedMutation =
                    entry.IsMutation ? entry.Points : 0;
                int expectedEvolution =
                    entry.IsMutation ? 0 : entry.Points;
                bool expectedUnlockable = entry.Tier > 0;

                if (actualMutation != expectedMutation ||
                    actualEvolution != expectedEvolution ||
                    actualUnlockable != expectedUnlockable)
                {
                    mismatches.Add(
                        entry.DefName +
                        " expected M=" + expectedMutation +
                        ", E=" + expectedEvolution +
                        ", unlockable=" + expectedUnlockable +
                        "; actual M=" + actualMutation +
                        ", E=" + actualEvolution +
                        ", unlockable=" + actualUnlockable);
                    continue;
                }

                validated++;
            }

            Log.Message(
                LogPrefix + " Validated points and unlockability for " +
                validated + " Geneline defs. Tier 0 is available by " +
                "default; tiers 1-5 require pherocores.");

            if (missing.Count > 0)
            {
                Log.Warning(
                    LogPrefix + " Optional or missing defs skipped: " +
                    string.Join(", ", missing.ToArray()));
            }

            if (mismatches.Count > 0)
            {
                Log.Error(
                    LogPrefix + " XML balance validation failed:\n" +
                    string.Join("\n", mismatches.ToArray()));
            }
        }

        private static void InstallPatches()
        {
            Harmony harmony = new Harmony(
                "RedCrow.InsectorTweaks.PherocoreBalanceIntegration");

            Type unlockedType = AccessTools.TypeByName(
                UnlockedGenesTypeName);
            MethodInfo lifecyclePostfix = AccessTools.Method(
                typeof(PherocoreBalanceIntegration),
                "FinalizeInitPostfix");
            MethodInfo exposePostfix = AccessTools.Method(
                typeof(PherocoreBalanceIntegration),
                "ExposeDataPostfix");
            MethodInfo gamePostfix = AccessTools.Method(
                typeof(PherocoreBalanceIntegration),
                "GameFinalizeInitPostfix");

            bool lifecyclePatched = false;
            MethodInfo finalizeInit = FindInstanceMethod(
                unlockedType,
                "FinalizeInit");
            if (finalizeInit != null && lifecyclePostfix != null)
            {
                HarmonyMethod postfix =
                    new HarmonyMethod(lifecyclePostfix);
                postfix.priority = Priority.Last;
                harmony.Patch(finalizeInit, postfix: postfix);
                lifecyclePatched = true;
                Log.Message(
                    LogPrefix + " Patched " +
                    MethodDescription(finalizeInit) + ".");
            }

            MethodInfo exposeData = FindInstanceMethod(
                unlockedType,
                "ExposeData");
            if (exposeData != null && exposePostfix != null)
            {
                HarmonyMethod postfix =
                    new HarmonyMethod(exposePostfix);
                postfix.priority = Priority.Last;
                harmony.Patch(exposeData, postfix: postfix);
                lifecyclePatched = true;
            }

            MethodInfo gameFinalize = FindInstanceMethod(
                typeof(Game),
                "FinalizeInit");
            if (gameFinalize != null && gamePostfix != null)
            {
                HarmonyMethod postfix =
                    new HarmonyMethod(gamePostfix);
                postfix.priority = Priority.Last;
                harmony.Patch(gameFinalize, postfix: postfix);
                lifecyclePatched = true;
            }

            if (!lifecyclePatched)
            {
                Log.Error(
                    LogPrefix + " No compatible world/game lifecycle " +
                    "method was found; pherocore pools cannot be extended.");
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

        private static MethodInfo FindInstanceMethod(
            Type type,
            string name)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo best = null;
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name != name)
                {
                    continue;
                }

                if (method.DeclaringType == type &&
                    method.GetParameters().Length == 0)
                {
                    return method;
                }

                if (method.DeclaringType == type || best == null)
                {
                    best = method;
                }
            }

            return best;
        }

        private static string MethodDescription(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string[] names = new string[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                names[index] = parameters[index].ParameterType.Name;
            }

            return method.DeclaringType.FullName + "." +
                method.Name + "(" + string.Join(", ", names) + ")";
        }

        [HarmonyPriority(Priority.Last)]
        public static void FinalizeInitPostfix(object __instance)
        {
            ExtendPherocorePools(
                __instance,
                "WorldComponent_UnlockedGenes.FinalizeInit");
        }

        [HarmonyPriority(Priority.Last)]
        public static void ExposeDataPostfix(object __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ExtendPherocorePools(
                    __instance,
                    "WorldComponent_UnlockedGenes.ExposeData/PostLoadInit");
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            Type unlockedType = AccessTools.TypeByName(
                UnlockedGenesTypeName);
            FieldInfo instanceField = unlockedType == null
                ? null
                : AccessTools.Field(unlockedType, "Instance");
            object component = instanceField == null
                ? null
                : instanceField.GetValue(null);
            ExtendPherocorePools(
                component,
                "Game.FinalizeInit");
        }

        private static void ExtendPherocorePools(
            object component,
            string source)
        {
            if (component == null)
            {
                return;
            }

            try
            {
                int correctedOriginals =
                    EnsureOriginalPherocoreUnlockability();
                int added = 0;
                int total = 0;
                for (int index = 0;
                    index < PoolBindings.Length;
                    index++)
                {
                    PoolBinding binding = PoolBindings[index];
                    added += EnsurePool(component, binding);
                    total += PoolCount(component, binding);
                }

                ClearGeneListCache();

                if (added > 0 ||
                    correctedOriginals > 0 ||
                    !successfulExtensionLogged)
                {
                    successfulExtensionLogged = true;
                    Log.Message(
                        LogPrefix + " Pherocore pools synchronized from " +
                        source + ": added=" + added +
                        ", original unlockability corrected=" +
                        correctedOriginals + ", total entries=" + total +
                        ". Existing unlocked states were preserved.");
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Failed to extend pherocore pools from " +
                    source + ":\n" + exception);
            }
        }

        private static int EnsureOriginalPherocoreUnlockability()
        {
            Type genelineType = AccessTools.TypeByName(
                GenelineGeneDefTypeName);
            FieldInfo unlockableField = genelineType == null
                ? null
                : AccessTools.Field(genelineType, "unlockable");
            if (unlockableField == null)
            {
                return 0;
            }

            int corrected = 0;
            for (int poolIndex = 0;
                poolIndex < PoolBindings.Length;
                poolIndex++)
            {
                string[] originalGenes =
                    PoolBindings[poolIndex].OriginalGenes;
                for (int geneIndex = 0;
                    geneIndex < originalGenes.Length;
                    geneIndex++)
                {
                    GeneDef gene =
                        DefDatabase<GeneDef>.GetNamedSilentFail(
                            originalGenes[geneIndex]);
                    if (gene == null ||
                        !genelineType.IsInstanceOfType(gene))
                    {
                        continue;
                    }

                    if (!(bool)unlockableField.GetValue(gene))
                    {
                        unlockableField.SetValue(gene, true);
                        corrected++;
                    }
                }
            }

            return corrected;
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
                index < binding.OriginalGenes.Length;
                index++)
            {
                added += AddGeneIfMissing(
                    dictionary,
                    binding.OriginalGenes[index]);
            }

            for (int index = 0;
                index < BalanceEntries.Length;
                index++)
            {
                BalanceEntry entry = BalanceEntries[index];
                if (entry.Tier != binding.Tier)
                {
                    continue;
                }

                added += AddGeneIfMissing(
                    dictionary,
                    entry.DefName);
            }

            bool allUnlocked = dictionary.Count > 0;
            foreach (DictionaryEntry pair in dictionary)
            {
                if (!(pair.Value is bool) ||
                    !(bool)pair.Value)
                {
                    allUnlocked = false;
                    break;
                }
            }

            completeField.SetValue(component, allUnlocked);
            return added;
        }

        private static int AddGeneIfMissing(
            IDictionary dictionary,
            string defName)
        {
            GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                defName);
            if (gene == null || dictionary.Contains(gene))
            {
                return 0;
            }

            dictionary.Add(gene, false);
            return 1;
        }

        private static int PoolCount(
            object component,
            PoolBinding binding)
        {
            FieldInfo dictionaryField = AccessTools.Field(
                component.GetType(),
                binding.DictionaryField);
            IDictionary dictionary = dictionaryField == null
                ? null
                : dictionaryField.GetValue(component) as IDictionary;
            return dictionary == null ? 0 : dictionary.Count;
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
