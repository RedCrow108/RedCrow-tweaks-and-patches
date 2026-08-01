using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class UpstreamInsectorFallbackDiagnostics
    {
        private const string LogPrefix =
            "[RedCrow Pherocore Upstream]";
        private const string UpstreamPackageId =
            "CarbineAction.HSK.VRE.Insector";
        private const string ComponentTypeName =
            "VanillaRacesExpandedInsector.GameComponent_UnlockedGenes";

        private static readonly string[] OriginalGeneDefNames =
        {
            "VRE_SwarmSynapse",
            "VRE_RoyalJellyInjector",
            "VRE_Microsized",
            "VRE_Colossal",
            "VRE_PyroResistantChitin",
            "VRE_FlameGlands",
            "VRE_ChemfuelSacks",
            "VRE_Pyrophiliac",
            "VRE_LocustWings",
            "VRE_InsectRostrum",
            "VRE_InsectVolatile",
            "VRE_EcdysoneOverdrive",
            "VRE_AcidGlands",
            "VRE_InfraredSensors",
            "VRE_AcidBurstSack",
            "VRE_SolidGreyMatter",
            "VRE_MineralRichInsectskin",
            "VRE_ChargerClaws",
            "VRE_HardLockedJoints",
            "VRE_PassiveInsect"
        };

        private sealed class PoolInfo
        {
            public readonly string Name;
            public readonly string Field;

            public PoolInfo(string name, string field)
            {
                Name = name;
                Field = field;
            }
        }

        private static readonly PoolInfo[] Pools =
        {
            new PoolInfo("Sorne", "sorne_pherocore_genes"),
            new PoolInfo("Nuchadus", "nuchadus_pherocore_genes"),
            new PoolInfo("Chelis", "chelis_pherocore_genes"),
            new PoolInfo("Kemian", "kemia_pherocore_genes"),
            new PoolInfo("Xanides", "xanides_pherocore_genes")
        };

        static UpstreamInsectorFallbackDiagnostics()
        {
            try
            {
                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.UpstreamInsectorFallbackDiagnostics");
                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(UpstreamInsectorFallbackDiagnostics),
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
                    "RedCrow.InsectorTweaks.PherocoreBalanceIntegration",
                    "RedCrow.InsectorTweaks.PherocoreInteractionAndSynapticHotfix",
                    "RedCrow.InsectorTweaks.OriginalScoutStrideIntegration"
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
                ModContentPack upstream =
                    LoadedModManager.RunningModsListForReading
                        .FirstOrDefault(
                            pack => string.Equals(
                                pack.PackageId,
                                UpstreamPackageId,
                                StringComparison.OrdinalIgnoreCase));

                int found = 0;
                int reassigned = 0;
                List<string> missing = new List<string>();
                for (int index = 0;
                    index < OriginalGeneDefNames.Length;
                    index++)
                {
                    string defName = OriginalGeneDefNames[index];
                    GeneDef gene =
                        DefDatabase<GeneDef>.GetNamedSilentFail(defName);
                    if (gene == null)
                    {
                        missing.Add(defName);
                        continue;
                    }

                    found++;
                    if (upstream != null && gene.modContentPack != upstream)
                    {
                        gene.modContentPack = upstream;
                        reassigned++;
                    }
                }

                Log.Message(
                    LogPrefix + " Original HSK Insector genes: found=" +
                    found + "/" + OriginalGeneDefNames.Length +
                    ", source reassigned=" + reassigned +
                    ", upstream pack=" +
                    (upstream == null ? "not found" : upstream.PackageId) +
                    ".");

                if (missing.Count > 0)
                {
                    Log.Warning(
                        LogPrefix + " Missing original genes: " +
                        string.Join(", ", missing.ToArray()) + ".");
                }

                LogPoolContents();
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Final diagnostics failed:\n" +
                    exception);
            }
        }

        private static void LogPoolContents()
        {
            Type componentType = AccessTools.TypeByName(ComponentTypeName);
            if (componentType == null)
            {
                Log.Warning(
                    LogPrefix + " " + ComponentTypeName +
                    " was not found for pool diagnostics.");
                return;
            }

            FieldInfo instanceField = AccessTools.Field(
                componentType,
                "Instance");
            object component = instanceField == null
                ? null
                : instanceField.GetValue(null);
            if (component == null)
            {
                Log.Warning(
                    LogPrefix + " Game component instance was not available " +
                    "for pool diagnostics.");
                return;
            }

            for (int poolIndex = 0;
                poolIndex < Pools.Length;
                poolIndex++)
            {
                PoolInfo pool = Pools[poolIndex];
                FieldInfo field = AccessTools.Field(
                    componentType,
                    pool.Field);
                IDictionary dictionary = field == null
                    ? null
                    : field.GetValue(component) as IDictionary;
                if (dictionary == null)
                {
                    Log.Warning(
                        LogPrefix + " " + pool.Name +
                        " dictionary was unavailable.");
                    continue;
                }

                List<string> entries = new List<string>();
                foreach (DictionaryEntry pair in dictionary)
                {
                    Def def = pair.Key as Def;
                    string defName = def == null
                        ? "<null>"
                        : def.defName;
                    string packageId =
                        def == null || def.modContentPack == null
                            ? "<no-source>"
                            : def.modContentPack.PackageId;
                    bool unlocked = pair.Value is bool && (bool)pair.Value;
                    entries.Add(
                        defName + "@" + packageId + "=" +
                        (unlocked ? "open" : "closed"));
                }

                entries.Sort(StringComparer.Ordinal);
                Log.Message(
                    LogPrefix + " " + pool.Name + " pool (" +
                    dictionary.Count + "): " +
                    string.Join("; ", entries.ToArray()));
            }
        }
    }
}