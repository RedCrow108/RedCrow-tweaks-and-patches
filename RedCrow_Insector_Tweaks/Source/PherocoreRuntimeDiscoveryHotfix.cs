using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class PherocoreRuntimeDiscoveryHotfix
    {
        private const string LogPrefix =
            "[RedCrow Pherocores Runtime]";

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
                OriginalGenes = originalGenes;
            }
        }

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

        private static Type unlockedGenesType;
        private static Type genelineGeneDefType;
        private static bool synchronizationLogged;
        private static bool staleOwnershipLogged;

        static PherocoreRuntimeDiscoveryHotfix()
        {
            try
            {
                unlockedGenesType = ResolveUnlockedGenesType();
                genelineGeneDefType = ResolveGenelineGeneDefType();

                if (unlockedGenesType == null)
                {
                    Log.Error(
                        LogPrefix + " Could not discover the pherocore " +
                        "WorldComponent by its saved-pool fields. No pool " +
                        "changes were applied.");
                    return;
                }

                Log.Message(
                    LogPrefix + " Discovered " +
                    unlockedGenesType.FullName + " in assembly " +
                    unlockedGenesType.Assembly.GetName().Name + ".");

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.PherocoreRuntimeDiscoveryHotfix");

                PatchDeclaredMethod(
                    harmony,
                    unlockedGenesType,
                    "FinalizeInit",
                    "ComponentFinalizeInitPostfix");
                PatchDeclaredMethod(
                    harmony,
                    unlockedGenesType,
                    "ExposeData",
                    "ComponentExposeDataPostfix");

                MethodInfo gameFinalize = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo gamePostfix = AccessTools.Method(
                    typeof(PherocoreRuntimeDiscoveryHotfix),
                    "GameFinalizeInitPostfix");
                if (gameFinalize != null && gamePostfix != null)
                {
                    HarmonyMethod postfix = new HarmonyMethod(gamePostfix);
                    postfix.priority = Priority.Last;
                    harmony.Patch(gameFinalize, postfix: postfix);
                }
                else
                {
                    Log.Error(
                        LogPrefix + " Verse.Game.FinalizeInit could not " +
                        "be patched.");
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        private static void PatchDeclaredMethod(
            Harmony harmony,
            Type declaringType,
            string methodName,
            string postfixName)
        {
            MethodInfo postfixMethod = AccessTools.Method(
                typeof(PherocoreRuntimeDiscoveryHotfix),
                postfixName);
            if (postfixMethod == null)
            {
                return;
            }

            MethodInfo[] methods = declaringType.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            int patched = 0;
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name != methodName)
                {
                    continue;
                }

                HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                postfix.priority = Priority.Last;
                harmony.Patch(method, postfix: postfix);
                patched++;
            }

            Log.Message(
                LogPrefix + " Patched " + patched + " declared " +
                declaringType.FullName + "." + methodName +
                " overload(s).");
        }

        private static Type ResolveUnlockedGenesType()
        {
            Type exact = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.WorldComponent_UnlockedGenes");
            if (HasPoolSignature(exact))
            {
                return exact;
            }

            Type[] types = GetAllLoadedTypes();
            for (int index = 0; index < types.Length; index++)
            {
                Type candidate = types[index];
                if (candidate == null ||
                    !typeof(WorldComponent).IsAssignableFrom(candidate))
                {
                    continue;
                }

                if (HasPoolSignature(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool HasPoolSignature(Type type)
        {
            return type != null &&
                AccessTools.Field(type, "sorne_pherocore_genes") != null &&
                AccessTools.Field(type, "nuchadus_pherocore_genes") != null &&
                AccessTools.Field(type, "chelis_pherocore_genes") != null &&
                AccessTools.Field(type, "kemia_pherocore_genes") != null &&
                AccessTools.Field(type, "xanides_pherocore_genes") != null;
        }

        private static Type ResolveGenelineGeneDefType()
        {
            Type exact = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.GenelineGeneDef");
            if (HasGenelineSignature(exact))
            {
                return exact;
            }

            Type[] types = GetAllLoadedTypes();
            for (int index = 0; index < types.Length; index++)
            {
                Type candidate = types[index];
                if (candidate == null ||
                    !typeof(GeneDef).IsAssignableFrom(candidate))
                {
                    continue;
                }

                if (HasGenelineSignature(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool HasGenelineSignature(Type type)
        {
            return type != null &&
                AccessTools.Field(type, "mutation") != null &&
                AccessTools.Field(type, "evolution") != null &&
                AccessTools.Field(type, "unlockable") != null;
        }

        private static Type[] GetAllLoadedTypes()
        {
            List<Type> result = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0;
                assemblyIndex < assemblies.Length;
                assemblyIndex++)
            {
                try
                {
                    result.AddRange(assemblies[assemblyIndex].GetTypes());
                }
                catch (ReflectionTypeLoadException exception)
                {
                    if (exception.Types == null)
                    {
                        continue;
                    }

                    for (int typeIndex = 0;
                        typeIndex < exception.Types.Length;
                        typeIndex++)
                    {
                        if (exception.Types[typeIndex] != null)
                        {
                            result.Add(exception.Types[typeIndex]);
                        }
                    }
                }
                catch
                {
                    // Optional assemblies can fail reflection. They are not
                    // relevant unless they contain the pherocore component.
                }
            }

            return result.ToArray();
        }

        [HarmonyPriority(Priority.Last)]
        public static void ComponentFinalizeInitPostfix(object __instance)
        {
            Synchronize(__instance, "component FinalizeInit");
        }

        [HarmonyPriority(Priority.Last)]
        public static void ComponentExposeDataPostfix(object __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Synchronize(__instance, "component ExposeData/PostLoadInit");
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            Synchronize(
                FindUnlockedGenesComponent(),
                "Game.FinalizeInit");
        }

        private static object FindUnlockedGenesComponent()
        {
            if (unlockedGenesType == null)
            {
                return null;
            }

            FieldInfo instanceField = AccessTools.Field(
                unlockedGenesType,
                "Instance");
            if (instanceField != null && instanceField.IsStatic)
            {
                object instance = instanceField.GetValue(null);
                if (instance != null)
                {
                    return instance;
                }
            }

            World world = Find.World;
            if (world == null)
            {
                return null;
            }

            MethodInfo[] methods = world.GetType().GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name != "GetComponent" ||
                    !method.IsGenericMethodDefinition ||
                    method.GetGenericArguments().Length != 1 ||
                    method.GetParameters().Length != 0)
                {
                    continue;
                }

                try
                {
                    object component = method
                        .MakeGenericMethod(unlockedGenesType)
                        .Invoke(world, null);
                    if (component != null)
                    {
                        return component;
                    }
                }
                catch
                {
                    // Fall through to reflective collection scan.
                }
            }

            FieldInfo[] fields = world.GetType().GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                IEnumerable values = fields[index].GetValue(world)
                    as IEnumerable;
                if (values == null || values is string)
                {
                    continue;
                }

                foreach (object value in values)
                {
                    if (value != null &&
                        unlockedGenesType.IsInstanceOfType(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        private static void Synchronize(object component, string source)
        {
            if (component == null ||
                unlockedGenesType == null ||
                !unlockedGenesType.IsInstanceOfType(component))
            {
                if (!synchronizationLogged)
                {
                    Log.Warning(
                        LogPrefix + " No pherocore component instance was " +
                        "available at " + source + ".");
                }
                return;
            }

            try
            {
                int originalsCorrected =
                    EnsureOriginalUnlockabilityAndCheckOwnership();
                int added = 0;
                int total = 0;

                for (int index = 0;
                    index < PoolBindings.Length;
                    index++)
                {
                    PoolBinding binding = PoolBindings[index];
                    added += EnsurePool(component, binding);
                    total += GetPoolCount(component, binding);
                }

                ClearGeneListCache();
                synchronizationLogged = true;
                Log.Message(
                    LogPrefix + " Pools synchronized from " + source +
                    ": added=" + added +
                    ", original unlockability corrected=" +
                    originalsCorrected +
                    ", total entries=" + total +
                    ". Existing true/false states were preserved.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Pool synchronization failed from " +
                    source + ":\n" + exception);
            }
        }

        private static int EnsureOriginalUnlockabilityAndCheckOwnership()
        {
            if (genelineGeneDefType == null)
            {
                return 0;
            }

            FieldInfo unlockableField = AccessTools.Field(
                genelineGeneDefType,
                "unlockable");
            if (unlockableField == null)
            {
                return 0;
            }

            int corrected = 0;
            List<string> staleOwnedGenes = new List<string>();
            for (int poolIndex = 0;
                poolIndex < PoolBindings.Length;
                poolIndex++)
            {
                string[] genes = PoolBindings[poolIndex].OriginalGenes;
                for (int geneIndex = 0;
                    geneIndex < genes.Length;
                    geneIndex++)
                {
                    GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                        genes[geneIndex]);
                    if (gene == null ||
                        !genelineGeneDefType.IsInstanceOfType(gene))
                    {
                        continue;
                    }

                    if (!(bool)unlockableField.GetValue(gene))
                    {
                        unlockableField.SetValue(gene, true);
                        corrected++;
                    }

                    if (IsOwnedByRedCrow(gene))
                    {
                        staleOwnedGenes.Add(gene.defName);
                    }
                }
            }

            if (staleOwnedGenes.Count > 0 && !staleOwnershipLogged)
            {
                staleOwnershipLogged = true;
                Log.Error(
                    LogPrefix + " Stale copied Insectoids 2 defs are still " +
                    "present in the installed RedCrow folder: " +
                    string.Join(", ", staleOwnedGenes.ToArray()) +
                    ". Delete the entire RedCrow_Insector_Tweaks folder " +
                    "before installing this build; extracting over the old " +
                    "folder cannot remove obsolete files.");
            }

            return corrected;
        }

        private static bool IsOwnedByRedCrow(Def def)
        {
            if (def == null || def.modContentPack == null)
            {
                return false;
            }

            string packageId = null;
            PropertyInfo packageProperty = AccessTools.Property(
                def.modContentPack.GetType(),
                "PackageId");
            if (packageProperty != null)
            {
                packageId = packageProperty.GetValue(
                    def.modContentPack,
                    null) as string;
            }

            if (packageId == null)
            {
                FieldInfo packageField = AccessTools.Field(
                    def.modContentPack.GetType(),
                    "packageId");
                if (packageField != null)
                {
                    packageId = packageField.GetValue(
                        def.modContentPack) as string;
                }
            }

            return packageId != null &&
                packageId.IndexOf(
                    "RedCrow.InsectorTweaks",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int EnsurePool(
            object component,
            PoolBinding binding)
        {
            FieldInfo dictionaryField = AccessTools.Field(
                unlockedGenesType,
                binding.DictionaryField);
            FieldInfo completeField = AccessTools.Field(
                unlockedGenesType,
                binding.CompleteField);
            if (dictionaryField == null || completeField == null)
            {
                Log.Error(
                    LogPrefix + " Missing saved fields for " +
                    binding.CoreName + ".");
                return 0;
            }

            IDictionary dictionary = dictionaryField.GetValue(component)
                as IDictionary;
            if (dictionary == null)
            {
                dictionary = Activator.CreateInstance(
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
                added += AddIfMissing(
                    dictionary,
                    binding.OriginalGenes[index]);
            }

            foreach (KeyValuePair<string, int> entry in GetBalanceTiers())
            {
                if (entry.Value == binding.Tier)
                {
                    added += AddIfMissing(dictionary, entry.Key);
                }
            }

            bool allUnlocked = dictionary.Count > 0;
            foreach (DictionaryEntry pair in dictionary)
            {
                if (!(pair.Value is bool) || !(bool)pair.Value)
                {
                    allUnlocked = false;
                    break;
                }
            }

            completeField.SetValue(component, allUnlocked);
            return added;
        }

        private static Dictionary<string, int> GetBalanceTiers()
        {
            Dictionary<string, int> result =
                new Dictionary<string, int>();
            FieldInfo entriesField = AccessTools.Field(
                typeof(PherocoreBalanceIntegration),
                "BalanceEntries");
            Array entries = entriesField == null
                ? null
                : entriesField.GetValue(null) as Array;
            if (entries == null)
            {
                return result;
            }

            foreach (object entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                Type entryType = entry.GetType();
                FieldInfo defNameField = AccessTools.Field(
                    entryType,
                    "DefName");
                FieldInfo tierField = AccessTools.Field(
                    entryType,
                    "Tier");
                if (defNameField == null || tierField == null)
                {
                    continue;
                }

                string defName = defNameField.GetValue(entry) as string;
                int tier = (int)tierField.GetValue(entry);
                if (!string.IsNullOrEmpty(defName) && tier > 0)
                {
                    result[defName] = tier;
                }
            }

            return result;
        }

        private static int AddIfMissing(
            IDictionary dictionary,
            string defName)
        {
            GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
            if (gene == null || dictionary.Contains(gene))
            {
                return 0;
            }

            dictionary.Add(gene, false);
            return 1;
        }

        private static int GetPoolCount(
            object component,
            PoolBinding binding)
        {
            FieldInfo dictionaryField = AccessTools.Field(
                unlockedGenesType,
                binding.DictionaryField);
            IDictionary dictionary = dictionaryField == null
                ? null
                : dictionaryField.GetValue(component) as IDictionary;
            return dictionary == null ? 0 : dictionary.Count;
        }

        private static void ClearGeneListCache()
        {
            Type[] types = GetAllLoadedTypes();
            for (int index = 0; index < types.Length; index++)
            {
                Type type = types[index];
                if (type == null)
                {
                    continue;
                }

                FieldInfo cache = AccessTools.Field(
                    type,
                    "cachedGeneDefsInOrder");
                if (cache != null && cache.IsStatic)
                {
                    cache.SetValue(null, null);
                    return;
                }
            }
        }
    }
}
