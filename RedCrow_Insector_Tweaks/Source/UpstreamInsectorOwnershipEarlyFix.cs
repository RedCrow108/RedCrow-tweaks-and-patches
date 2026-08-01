using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class UpstreamInsectorOwnershipEarlyFix
    {
        private const string LogPrefix =
            "[RedCrow Upstream Ownership]";
        private const string UpstreamPackageId =
            "CarbineAction.HSK.VRE.Insector";

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

        static UpstreamInsectorOwnershipEarlyFix()
        {
            try
            {
                AssignOwnership("static startup");
                LongEventHandler.ExecuteWhenFinished(
                    delegate { AssignOwnership("long-event completion"); });

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.UpstreamInsectorOwnershipEarlyFix");
                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(UpstreamInsectorOwnershipEarlyFix),
                    "GameFinalizeInitPostfix");
                if (target != null && postfixMethod != null)
                {
                    HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                    postfix.priority = Priority.First;
                    harmony.Patch(target, postfix: postfix);
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Installation failed:\n" + exception);
            }
        }

        [HarmonyPriority(Priority.First)]
        public static void GameFinalizeInitPostfix()
        {
            AssignOwnership("Game.FinalizeInit");
        }

        private static void AssignOwnership(string source)
        {
            ModContentPack upstream =
                LoadedModManager.RunningModsListForReading.FirstOrDefault(
                    pack => string.Equals(
                        pack.PackageId,
                        UpstreamPackageId,
                        StringComparison.OrdinalIgnoreCase));
            if (upstream == null)
            {
                Log.Warning(
                    LogPrefix + " Upstream package was not found at " +
                    source + ".");
                return;
            }

            int found = 0;
            int changed = 0;
            for (int index = 0;
                index < OriginalGeneDefNames.Length;
                index++)
            {
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    OriginalGeneDefNames[index]);
                if (gene == null)
                {
                    continue;
                }

                found++;
                if (gene.modContentPack != upstream)
                {
                    gene.modContentPack = upstream;
                    changed++;
                }
            }

            ClearGeneListCache();
            Log.Message(
                LogPrefix + " Ownership synchronized at " + source +
                ": found=" + found + "/" +
                OriginalGeneDefNames.Length + ", changed=" + changed +
                ", source=" + upstream.PackageId + ".");
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
