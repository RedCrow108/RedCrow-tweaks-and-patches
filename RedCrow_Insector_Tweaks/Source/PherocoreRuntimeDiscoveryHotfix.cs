using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class PherocoreRuntimeDiscoveryHotfix
    {
        private const string LogPrefix =
            "[RedCrow Pherocores Runtime]";
        private const string ComponentTypeName =
            "VanillaRacesExpandedInsector.GameComponent_UnlockedGenes";

        private static readonly Type ComponentType;
        private static readonly MethodInfo ExtendPoolsMethod;

        static PherocoreRuntimeDiscoveryHotfix()
        {
            try
            {
                ComponentType = AccessTools.TypeByName(ComponentTypeName);
                ExtendPoolsMethod = AccessTools.Method(
                    typeof(PherocoreBalanceIntegration),
                    "ExtendPherocorePools");

                if (ComponentType == null)
                {
                    Log.Error(
                        LogPrefix + " " + ComponentTypeName +
                        " was not found in the loaded Insector assembly.");
                    return;
                }

                if (!typeof(GameComponent).IsAssignableFrom(ComponentType))
                {
                    Log.Error(
                        LogPrefix + " " + ComponentType.FullName +
                        " is not a Verse.GameComponent.");
                    return;
                }

                if (ExtendPoolsMethod == null)
                {
                    Log.Error(
                        LogPrefix + " PherocoreBalanceIntegration." +
                        "ExtendPherocorePools was not found.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.PherocoreGameComponentHotfix");

                int finalizeCount = PatchDeclaredMethods(
                    harmony,
                    ComponentType,
                    "FinalizeInit",
                    "ComponentFinalizeInitPostfix");
                int exposeCount = PatchDeclaredMethods(
                    harmony,
                    ComponentType,
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

                Log.Message(
                    LogPrefix + " Bound to " + ComponentType.FullName +
                    " in " + ComponentType.Assembly.GetName().Name +
                    "; patched FinalizeInit=" + finalizeCount +
                    ", ExposeData=" + exposeCount +
                    ", Game.FinalizeInit=" +
                    (gameFinalize != null && gamePostfix != null) + ".");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        private static int PatchDeclaredMethods(
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
                return 0;
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

            return patched;
        }

        [HarmonyPriority(Priority.Last)]
        public static void ComponentFinalizeInitPostfix(object __instance)
        {
            ExtendPools(
                __instance,
                "GameComponent_UnlockedGenes.FinalizeInit");
        }

        [HarmonyPriority(Priority.Last)]
        public static void ComponentExposeDataPostfix(object __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ExtendPools(
                    __instance,
                    "GameComponent_UnlockedGenes.ExposeData/PostLoadInit");
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            ExtendPools(
                FindComponent(),
                "Game.FinalizeInit");
        }

        private static void ExtendPools(object component, string source)
        {
            if (component == null ||
                ComponentType == null ||
                !ComponentType.IsInstanceOfType(component) ||
                ExtendPoolsMethod == null)
            {
                Log.Warning(
                    LogPrefix + " The pherocore GameComponent instance " +
                    "was unavailable at " + source + ".");
                return;
            }

            try
            {
                ExtendPoolsMethod.Invoke(
                    null,
                    new[] { component, source });
            }
            catch (TargetInvocationException exception)
            {
                Log.Error(
                    LogPrefix + " Pool synchronization failed:\n" +
                    (exception.InnerException ?? exception));
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Pool synchronization failed:\n" +
                    exception);
            }
        }

        private static object FindComponent()
        {
            if (ComponentType == null)
            {
                return null;
            }

            FieldInfo instanceField = AccessTools.Field(
                ComponentType,
                "Instance");
            if (instanceField != null && instanceField.IsStatic)
            {
                object instance = instanceField.GetValue(null);
                if (instance != null)
                {
                    return instance;
                }
            }

            Game game = Current.Game;
            if (game == null)
            {
                return null;
            }

            MethodInfo[] methods = game.GetType().GetMethods(
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
                    return method
                        .MakeGenericMethod(ComponentType)
                        .Invoke(game, null);
                }
                catch
                {
                    // Try another overload if RimWorld adds one.
                }
            }

            return null;
        }
    }
}
