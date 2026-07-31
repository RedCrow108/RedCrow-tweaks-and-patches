using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class PherocoreInteractionAndSynapticHotfix
    {
        private const string LogPrefix =
            "[RedCrow Pherocore Follow-up]";
        private const string ComponentTypeName =
            "VanillaRacesExpandedInsector.GameComponent_UnlockedGenes";
        private const string GeneTypeName =
            "VanillaRacesExpandedInsector.GenelineGeneDef";
        private const string SynapticGeneDefName =
            "RC_Evolution_HiveSynapticNode";

        private static Type componentType;
        private static bool successLogged;

        static PherocoreInteractionAndSynapticHotfix()
        {
            try
            {
                componentType = AccessTools.TypeByName(ComponentTypeName);
                if (componentType == null)
                {
                    Log.Error(
                        LogPrefix + " " + ComponentTypeName +
                        " was not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.PherocoreInteractionAndSynapticHotfix");

                PatchAllDeclared(
                    harmony,
                    componentType,
                    "FinalizeInit",
                    "ComponentFinalizeInitPostfix");
                PatchAllDeclared(
                    harmony,
                    componentType,
                    "ExposeData",
                    "ComponentExposeDataPostfix");

                MethodInfo gameFinalize = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo gamePostfix = AccessTools.Method(
                    typeof(PherocoreInteractionAndSynapticHotfix),
                    "GameFinalizeInitPostfix");
                if (gameFinalize != null && gamePostfix != null)
                {
                    HarmonyMethod postfix = new HarmonyMethod(gamePostfix);
                    postfix.priority = Priority.Last;
                    postfix.after = new[]
                    {
                        "RedCrow.InsectorTweaks.PherocoreBalanceIntegration"
                    };
                    harmony.Patch(gameFinalize, postfix: postfix);
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        private static void PatchAllDeclared(
            Harmony harmony,
            Type declaringType,
            string methodName,
            string postfixName)
        {
            MethodInfo postfixMethod = AccessTools.Method(
                typeof(PherocoreInteractionAndSynapticHotfix),
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
            for (int index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name != methodName)
                {
                    continue;
                }

                HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                postfix.priority = Priority.Last;
                postfix.after = new[]
                {
                    "RedCrow.InsectorTweaks.PherocoreBalanceIntegration"
                };
                harmony.Patch(methods[index], postfix: postfix);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void ComponentFinalizeInitPostfix(object __instance)
        {
            EnsureSynapticEvolution(__instance, "component FinalizeInit");
        }

        [HarmonyPriority(Priority.Last)]
        public static void ComponentExposeDataPostfix(object __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureSynapticEvolution(
                    __instance,
                    "component ExposeData/PostLoadInit");
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            EnsureSynapticEvolution(
                GetComponentInstance(),
                "Game.FinalizeInit");
        }

        private static object GetComponentInstance()
        {
            if (componentType == null)
            {
                return null;
            }

            FieldInfo instanceField = AccessTools.Field(
                componentType,
                "Instance");
            if (instanceField != null && instanceField.IsStatic)
            {
                object instance = instanceField.GetValue(null);
                if (instance != null)
                {
                    return instance;
                }
            }

            return null;
        }

        private static void EnsureSynapticEvolution(
            object component,
            string source)
        {
            if (component == null ||
                componentType == null ||
                !componentType.IsInstanceOfType(component))
            {
                return;
            }

            try
            {
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    SynapticGeneDefName);
                if (gene == null)
                {
                    Log.Error(
                        LogPrefix + " " + SynapticGeneDefName +
                        " was not found.");
                    return;
                }

                Type geneType = AccessTools.TypeByName(GeneTypeName);
                FieldInfo unlockableField = geneType == null
                    ? null
                    : AccessTools.Field(geneType, "unlockable");
                if (unlockableField != null &&
                    geneType.IsInstanceOfType(gene) &&
                    !(bool)unlockableField.GetValue(gene))
                {
                    unlockableField.SetValue(gene, true);
                }

                FieldInfo dictionaryField = AccessTools.Field(
                    componentType,
                    "kemia_pherocore_genes");
                FieldInfo completeField = AccessTools.Field(
                    componentType,
                    "allKemiaGenesUnlocked");
                IDictionary dictionary = dictionaryField == null
                    ? null
                    : dictionaryField.GetValue(component) as IDictionary;

                if (dictionary == null)
                {
                    Log.Error(
                        LogPrefix + " Kemian pherocore dictionary was " +
                        "not available.");
                    return;
                }

                bool added = false;
                if (!dictionary.Contains(gene))
                {
                    dictionary.Add(gene, false);
                    added = true;
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

                if (completeField != null)
                {
                    completeField.SetValue(component, allUnlocked);
                }

                ClearGeneListCache();

                if (added || !successLogged)
                {
                    successLogged = true;
                    Log.Message(
                        LogPrefix + " Synaptic hive evolution synchronized " +
                        "with Kemian tier 4 from " + source +
                        "; added=" + added +
                        ", pool count=" + dictionary.Count + ".");
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Synchronization failed from " + source +
                    ":\n" + exception);
            }
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
