using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class RC_SynapticRefillJobHotfix
    {
        private const TargetIndex NodeIndex = TargetIndex.A;
        private const TargetIndex JellyIndex = TargetIndex.B;

        static RC_SynapticRefillJobHotfix()
        {
            try
            {
                MethodInfo makeNewToils = AccessTools.Method(
                    typeof(JobDriver_RefillSynapticHiveNode),
                    "MakeNewToils");
                MethodInfo prefix = AccessTools.Method(
                    typeof(RC_SynapticRefillJobHotfix),
                    "MakeNewToilsPrefix");

                if (makeNewToils == null || prefix == null)
                {
                    Log.Error(
                        "[RedCrow Synaptic Refill] Could not install the " +
                        "refill-job replacement.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.SynapticRefillJobHotfix");
                harmony.Patch(
                    makeNewToils,
                    prefix: new HarmonyMethod(prefix));

                Log.Message(
                    "[RedCrow Synaptic Refill] Refill-job replacement " +
                    "installed. Carried jelly may despawn without failing " +
                    "the active job.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    "[RedCrow Synaptic Refill] Patch installation failed:\n" +
                    exception);
            }
        }

        public static bool MakeNewToilsPrefix(
            JobDriver_RefillSynapticHiveNode __instance,
            ref IEnumerable<Toil> __result)
        {
            __result = MakeReplacementToils(__instance);
            return false;
        }

        private static IEnumerable<Toil> MakeReplacementToils(
            JobDriver_RefillSynapticHiveNode driver)
        {
            driver.FailOnDespawnedNullOrForbidden(NodeIndex);

            // A hauled Thing is deliberately despawned while it is carried.
            // The former Despawned fail condition therefore aborted the job
            // immediately after pickup and the work giver created it again.
            driver.FailOnDestroyedNullOrForbidden(JellyIndex);

            yield return Toils_Goto.GotoThing(
                JellyIndex,
                PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(
                JellyIndex,
                false,
                false,
                false,
                false);
            yield return Toils_Goto.GotoThing(
                NodeIndex,
                PathEndMode.Touch);

            Toil wait = Toils_General.Wait(120, NodeIndex);
            wait.WithProgressBarToilDelay(NodeIndex);
            yield return wait;

            Toil refill = new Toil();
            refill.initAction = delegate
            {
                Pawn actor = refill.actor;
                Thing node = actor.CurJob.targetA.Thing;
                Thing carried = actor.carryTracker.CarriedThing;
                CompSynapticReservoir reservoir = node != null
                    ? node.TryGetComp<CompSynapticReservoir>()
                    : null;
                if (reservoir == null || carried == null)
                {
                    return;
                }

                int accepted = reservoir.AddJelly(
                    Math.Min(carried.stackCount, actor.CurJob.count));
                if (accepted <= 0)
                {
                    return;
                }

                Thing consumed = carried.SplitOff(accepted);
                consumed.Destroy(DestroyMode.Vanish);
            };
            refill.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return refill;
        }
    }
}
