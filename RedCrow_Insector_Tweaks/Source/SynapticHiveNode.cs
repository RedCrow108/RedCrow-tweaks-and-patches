using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RedCrow.InsectorTweaks
{
    public enum RC_SynapticNetworkState
    {
        Offline,
        Unconscious,
        Critical,
        Stable,
        Enhanced,
        Saturated
    }

    public static class RC_SynapticNetworkMath
    {
        public const int UpdateInterval = 30000;

        public static float CapacityFor(int connectedPawns)
        {
            return 1000f + 1000f * Math.Max(0, connectedPawns);
        }

        public static float DailyCostPerPawn(float targetPercent)
        {
            if (targetPercent <= 0.5f)
            {
                return 20f;
            }

            return targetPercent <= 0.75f ? 30f : 40f;
        }

        public static RC_SynapticNetworkState StateFor(float percent)
        {
            if (percent < 0.11f)
            {
                return RC_SynapticNetworkState.Unconscious;
            }

            if (percent < 0.25f)
            {
                return RC_SynapticNetworkState.Critical;
            }

            if (percent < 0.51f)
            {
                return RC_SynapticNetworkState.Stable;
            }

            if (percent < 0.76f)
            {
                return RC_SynapticNetworkState.Enhanced;
            }

            return RC_SynapticNetworkState.Saturated;
        }

        public static float SeverityFor(RC_SynapticNetworkState state)
        {
            switch (state)
            {
                case RC_SynapticNetworkState.Unconscious:
                    return 0.01f;
                case RC_SynapticNetworkState.Critical:
                    return 0.11f;
                case RC_SynapticNetworkState.Stable:
                    return 0.25f;
                case RC_SynapticNetworkState.Enhanced:
                    return 0.51f;
                case RC_SynapticNetworkState.Saturated:
                    return 0.76f;
                default:
                    return 0f;
            }
        }
    }

    public class CompProperties_SynapticReservoir : CompProperties
    {
        public HediffDef linkedPawnHediff;
        public HediffDef networkHediff;
        public HediffDef deferredShockHediff;

        public CompProperties_SynapticReservoir()
        {
            compClass = typeof(CompSynapticReservoir);
        }
    }

    public class CompSynapticReservoir : ThingComp
    {
        private float currentJelly;
        private float targetPercent = 0.5f;
        private float fractionalConsumptionRemainder;
        private int lastLinkedPawnCount;
        private RC_SynapticNetworkState lastState =
            RC_SynapticNetworkState.Offline;
        private float cachedCapacity = 1000f;
        private bool lossHandled;

        private CompProperties_SynapticReservoir Props
        {
            get { return (CompProperties_SynapticReservoir)props; }
        }

        public float CurrentJelly
        {
            get { return currentJelly; }
        }

        public float TargetPercent
        {
            get { return targetPercent; }
            set { targetPercent = Mathf.Clamp01(value); }
        }

        public float FractionalConsumptionRemainder
        {
            get { return fractionalConsumptionRemainder; }
            set { fractionalConsumptionRemainder = Math.Max(0f, value); }
        }

        public int LastLinkedPawnCount
        {
            get { return lastLinkedPawnCount; }
        }

        public RC_SynapticNetworkState LastState
        {
            get { return lastState; }
        }

        public float CachedCapacity
        {
            get { return Math.Max(1f, cachedCapacity); }
        }

        public float FillPercent
        {
            get { return Mathf.Clamp01(currentJelly / CachedCapacity); }
        }

        public float DailyConsumption
        {
            get
            {
                return lastLinkedPawnCount *
                    RC_SynapticNetworkMath.DailyCostPerPawn(
                        targetPercent);
            }
        }

        public float TargetAmount
        {
            get { return targetPercent * CachedCapacity; }
        }

        public float RefillSpace
        {
            get
            {
                if (currentJelly >= CachedCapacity)
                {
                    return 0f;
                }

                return Math.Max(0f, TargetAmount - currentJelly);
            }
        }

        public bool IsOperational
        {
            get
            {
                if (parent == null || !parent.Spawned || parent.Destroyed)
                {
                    return false;
                }

                CompMaintainable maintainable =
                    parent.TryGetComp<CompMaintainable>();
                if (maintainable != null &&
                    maintainable.CurStage != MaintainableStage.Healthy)
                {
                    return false;
                }

                MapComponent_SynapticHiveNetwork controller =
                    parent.Map.GetComponent<
                        MapComponent_SynapticHiveNetwork>();
                return controller != null &&
                    controller.PrimaryNode == this;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                lossHandled = false;
            }
            MapComponent_SynapticHiveNetwork controller =
                parent.Map.GetComponent<MapComponent_SynapticHiveNetwork>();
            if (controller != null)
            {
                controller.RegisterNode(this, respawningAfterLoad);
            }
        }

        public override void PostDeSpawn(Map map)
        {
            MapComponent_SynapticHiveNetwork controller =
                map != null
                    ? map.GetComponent<MapComponent_SynapticHiveNetwork>()
                    : null;
            if (controller != null)
            {
                controller.UnregisterNode(this);
            }

            base.PostDeSpawn(map);
        }

        public override void PostDestroy(
            DestroyMode mode,
            Map previousMap)
        {
            if (!lossHandled &&
                (mode == DestroyMode.KillFinalize ||
                    mode == DestroyMode.Deconstruct))
            {
                lossHandled = true;
                MapComponent_SynapticHiveNetwork controller =
                    previousMap != null
                        ? previousMap.GetComponent<
                            MapComponent_SynapticHiveNetwork>()
                        : null;
                if (controller != null)
                {
                    controller.HandleNodeLoss(this);
                }
            }

            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(
                ref currentJelly,
                "currentJelly",
                0f);
            Scribe_Values.Look(
                ref targetPercent,
                "targetPercent",
                0.5f);
            Scribe_Values.Look(
                ref fractionalConsumptionRemainder,
                "fractionalConsumptionRemainder",
                0f);
            Scribe_Values.Look(
                ref lastLinkedPawnCount,
                "lastLinkedPawnCount",
                0);
            Scribe_Values.Look(
                ref lastState,
                "lastNetworkState",
                RC_SynapticNetworkState.Offline);
            Scribe_Values.Look(
                ref cachedCapacity,
                "cachedCapacity",
                1000f);
            Scribe_Values.Look(
                ref lossHandled,
                "lossHandled",
                false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                targetPercent = Mathf.Clamp01(targetPercent);
                currentJelly = Math.Max(0f, currentJelly);
                fractionalConsumptionRemainder =
                    Math.Max(0f, fractionalConsumptionRemainder);
                cachedCapacity = Math.Max(1000f, cachedCapacity);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Gizmo_SynapticReservoir(this);
        }

        public int AddJelly(int amount)
        {
            int accepted = Math.Min(
                Math.Max(0, amount),
                Mathf.FloorToInt(RefillSpace));
            currentJelly += accepted;
            return accepted;
        }

        public void Consume(float amount)
        {
            currentJelly = Math.Max(0f, currentJelly - amount);
        }

        public void UpdateCache(
            int pawnCount,
            float capacity,
            RC_SynapticNetworkState state)
        {
            lastLinkedPawnCount = Math.Max(0, pawnCount);
            cachedCapacity = Math.Max(1000f, capacity);
            lastState = state;
        }

        public string StateLabel
        {
            get
            {
                return ("RC_SynapticNode_State_" +
                    lastState).Translate();
            }
        }

        public HediffDef LinkedPawnHediff
        {
            get { return Props.linkedPawnHediff; }
        }

        public HediffDef NetworkHediff
        {
            get { return Props.networkHediff; }
        }

        public HediffDef DeferredShockHediff
        {
            get { return Props.deferredShockHediff; }
        }
    }

    public class Gizmo_SynapticReservoir : Gizmo_Slider
    {
        private readonly CompSynapticReservoir reservoir;

        public Gizmo_SynapticReservoir(
            CompSynapticReservoir reservoir)
        {
            this.reservoir = reservoir;
        }

        protected override float Width
        {
            get { return 240f; }
        }

        protected override float Target
        {
            get { return reservoir.TargetPercent; }
            set { reservoir.TargetPercent = value; }
        }

        protected override float ValuePercent
        {
            get { return reservoir.FillPercent; }
        }

        protected override bool IsDraggable
        {
            get { return true; }
        }

        protected override int Increments
        {
            get { return 100; }
        }

        protected override string Title
        {
            get
            {
                return "RC_SynapticNode_GizmoTitle".Translate();
            }
        }

        protected override string BarLabel
        {
            get
            {
                return reservoir.CurrentJelly.ToString("F0") +
                    " / " +
                    reservoir.CachedCapacity.ToString("F0");
            }
        }

        protected override IEnumerable<float> GetBarThresholds()
        {
            yield return 0.11f;
            yield return 0.25f;
            yield return 0.51f;
            yield return 0.76f;
        }

        protected override string GetTooltip()
        {
            return "RC_SynapticNode_GizmoDetails".Translate(
                reservoir.CurrentJelly.ToString("F0"),
                reservoir.CachedCapacity.ToString("F0"),
                reservoir.FillPercent.ToStringPercent(),
                reservoir.TargetPercent.ToStringPercent(),
                reservoir.DailyConsumption.ToString("F0"),
                reservoir.StateLabel,
                reservoir.LastLinkedPawnCount);
        }
    }

    public class MapComponent_SynapticHiveNetwork : MapComponent
    {
        private int nextUpdateTick = -1;
        private CompSynapticReservoir primaryNode;

        public MapComponent_SynapticHiveNetwork(Map map) : base(map)
        {
        }

        public CompSynapticReservoir PrimaryNode
        {
            get
            {
                if (primaryNode == null ||
                    primaryNode.parent == null ||
                    !primaryNode.parent.Spawned)
                {
                    primaryNode = FindPrimaryNode();
                }

                return primaryNode;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(
                ref nextUpdateTick,
                "nextSynapticUpdateTick",
                -1);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            primaryNode = FindPrimaryNode();
            Refresh(false);
            if (nextUpdateTick < Find.TickManager.TicksGame)
            {
                nextUpdateTick = Find.TickManager.TicksGame +
                    RC_SynapticNetworkMath.UpdateInterval;
            }
        }

        public override void MapComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (nextUpdateTick < 0)
            {
                nextUpdateTick = currentTick +
                    RC_SynapticNetworkMath.UpdateInterval;
                return;
            }

            if (currentTick < nextUpdateTick)
            {
                return;
            }

            nextUpdateTick = currentTick +
                RC_SynapticNetworkMath.UpdateInterval;
            Refresh(true);
        }

        public void RegisterNode(
            CompSynapticReservoir node,
            bool respawningAfterLoad)
        {
            if (primaryNode == null ||
                primaryNode.parent == null ||
                !primaryNode.parent.Spawned)
            {
                primaryNode = node;
            }

            if (!respawningAfterLoad)
            {
                Refresh(false);
            }
        }

        public void UnregisterNode(CompSynapticReservoir node)
        {
            if (primaryNode == node)
            {
                primaryNode = null;
                RemoveNetworkHediffFromAll();
            }
        }

        public void HandleNodeLoss(CompSynapticReservoir lostNode)
        {
            if (lostNode == null || primaryNode == lostNode)
            {
                primaryNode = null;
            }

            List<Pawn> linked = FindLinkedPawns(lostNode);
            HediffDef networkDef = lostNode != null
                ? lostNode.NetworkHediff
                : null;
            HediffDef deferredDef = lostNode != null
                ? lostNode.DeferredShockHediff
                : null;

            for (int i = 0; i < linked.Count; i++)
            {
                Pawn pawn = linked[i];
                RemoveHediff(pawn, networkDef);
                if (!TryStartBerserk(pawn) &&
                    deferredDef != null &&
                    !pawn.health.hediffSet.HasHediff(
                        deferredDef,
                        false))
                {
                    pawn.health.AddHediff(deferredDef);
                }
            }

            Messages.Message(
                "RC_SynapticNode_Destroyed".Translate(),
                MessageTypeDefOf.ThreatBig,
                false);
        }

        private void Refresh(bool consume)
        {
            CompSynapticReservoir node = PrimaryNode;
            if (node == null)
            {
                RemoveNetworkHediffFromAll();
                return;
            }

            List<Pawn> linked = FindLinkedPawns(node);
            float capacity =
                RC_SynapticNetworkMath.CapacityFor(linked.Count);

            if (consume && node.IsOperational)
            {
                float rawConsumption =
                    linked.Count *
                    RC_SynapticNetworkMath.DailyCostPerPawn(
                        node.TargetPercent) *
                    0.5f +
                    node.FractionalConsumptionRemainder;
                float wholeConsumption =
                    Mathf.Floor(rawConsumption);
                node.FractionalConsumptionRemainder =
                    rawConsumption - wholeConsumption;
                node.Consume(wholeConsumption);
            }

            RC_SynapticNetworkState state = node.IsOperational
                ? RC_SynapticNetworkMath.StateFor(
                    Mathf.Clamp01(node.CurrentJelly / capacity))
                : RC_SynapticNetworkState.Offline;
            RemoveNetworkHediffFromUnlinked(
                linked,
                node.NetworkHediff);
            node.UpdateCache(linked.Count, capacity, state);
            ApplyNetworkState(linked, node, state);
        }

        private void ApplyNetworkState(
            List<Pawn> linked,
            CompSynapticReservoir node,
            RC_SynapticNetworkState state)
        {
            HediffDef networkDef = node.NetworkHediff;
            if (networkDef == null)
            {
                return;
            }

            for (int i = 0; i < linked.Count; i++)
            {
                Pawn pawn = linked[i];
                if (state == RC_SynapticNetworkState.Offline)
                {
                    RemoveHediff(pawn, networkDef);
                    continue;
                }

                Hediff hediff = pawn.health.hediffSet
                    .GetFirstHediffOfDef(networkDef, false);
                float wantedSeverity =
                    RC_SynapticNetworkMath.SeverityFor(state);
                if (hediff == null)
                {
                    hediff = pawn.health.AddHediff(networkDef);
                    hediff.Severity = wantedSeverity;
                }
                else if (node.LastState != state ||
                    Math.Abs(hediff.Severity - wantedSeverity) > 0.001f)
                {
                    hediff.Severity = wantedSeverity;
                }
            }
        }

        private List<Pawn> FindLinkedPawns(
            CompSynapticReservoir node)
        {
            List<Pawn> result = new List<Pawn>();
            HediffDef linkedDef = node != null
                ? node.LinkedPawnHediff
                : DefDatabase<HediffDef>.GetNamedSilentFail(
                    "RC_SwarmConsumed");
            if (linkedDef == null)
            {
                return result;
            }

            IReadOnlyList<Pawn> pawns =
                map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null &&
                    !pawn.Dead &&
                    pawn.health != null &&
                    pawn.health.hediffSet.HasHediff(
                        linkedDef,
                        false))
                {
                    result.Add(pawn);
                }
            }

            return result;
        }

        private CompSynapticReservoir FindPrimaryNode()
        {
            ThingDef nodeDef =
                DefDatabase<ThingDef>.GetNamedSilentFail(
                    "RC_SynapticHiveNode");
            if (nodeDef == null)
            {
                return null;
            }

            List<Thing> nodes = map.listerThings.ThingsOfDef(nodeDef);
            for (int i = 0; i < nodes.Count; i++)
            {
                CompSynapticReservoir comp =
                    nodes[i].TryGetComp<CompSynapticReservoir>();
                if (comp != null && nodes[i].Spawned)
                {
                    return comp;
                }
            }

            return null;
        }

        private void RemoveNetworkHediffFromAll()
        {
            HediffDef networkDef =
                DefDatabase<HediffDef>.GetNamedSilentFail(
                    "RC_SynapticNetworkState");
            if (networkDef == null)
            {
                return;
            }

            IReadOnlyList<Pawn> pawns =
                map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                RemoveHediff(pawns[i], networkDef);
            }
        }

        private void RemoveNetworkHediffFromUnlinked(
            List<Pawn> linked,
            HediffDef networkDef)
        {
            if (networkDef == null)
            {
                return;
            }

            IReadOnlyList<Pawn> pawns =
                map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!linked.Contains(pawn))
                {
                    RemoveHediff(pawn, networkDef);
                }
            }
        }

        internal static void RemoveHediff(
            Pawn pawn,
            HediffDef hediffDef)
        {
            if (pawn == null ||
                pawn.health == null ||
                hediffDef == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet
                .GetFirstHediffOfDef(hediffDef, false);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        internal static bool TryStartBerserk(Pawn pawn)
        {
            if (pawn == null ||
                pawn.Dead ||
                pawn.Downed ||
                !pawn.Awake() ||
                pawn.mindState == null ||
                pawn.mindState.mentalStateHandler == null)
            {
                return false;
            }

            return pawn.mindState.mentalStateHandler
                .TryStartMentalState(
                    MentalStateDefOf.Berserk,
                    null,
                    true,
                    true);
        }
    }

    public class HediffCompProperties_DeferredSynapticShock :
        HediffCompProperties
    {
        public HediffCompProperties_DeferredSynapticShock()
        {
            compClass = typeof(HediffComp_DeferredSynapticShock);
        }
    }

    public class HediffComp_DeferredSynapticShock : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = parent.pawn;
            if (pawn == null ||
                pawn.IsHashIntervalTick(60) == false)
            {
                return;
            }

            if (MapComponent_SynapticHiveNetwork
                .TryStartBerserk(pawn))
            {
                pawn.health.RemoveHediff(parent);
            }
        }
    }

    public static class RC_SynapticNodeUtility
    {
        public static bool HasFunctionalNode(
            Map map,
            Faction faction)
        {
            if (map == null)
            {
                return false;
            }

            CompSynapticReservoir node = map.GetComponent<
                MapComponent_SynapticHiveNetwork>().PrimaryNode;
            return node != null &&
                node.parent.Faction == faction &&
                node.IsOperational;
        }

        public static bool AbilityAllowed(Pawn pawn)
        {
            return pawn != null &&
                pawn.Spawned &&
                HasFunctionalNode(pawn.Map, pawn.Faction);
        }
    }

    public class CompProperties_AbilityRequiresSynapticNode :
        CompProperties_AbilityEffect
    {
        public CompProperties_AbilityRequiresSynapticNode()
        {
            compClass =
                typeof(CompAbilityEffect_RequiresSynapticNode);
        }
    }

    public class CompAbilityEffect_RequiresSynapticNode :
        CompAbilityEffect
    {
        public override bool GizmoDisabled(out string reason)
        {
            if (!RC_SynapticNodeUtility.AbilityAllowed(parent.pawn))
            {
                reason =
                    "RC_SynapticNode_AbilityRequired".Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(
            LocalTargetInfo target,
            bool throwMessages = false)
        {
            if (!RC_SynapticNodeUtility.AbilityAllowed(parent.pawn))
            {
                if (throwMessages)
                {
                    Messages.Message(
                        "RC_SynapticNode_AbilityRequired".Translate(),
                        MessageTypeDefOf.RejectInput,
                        false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }

    public class PlaceWorker_OnlyOneSynapticHiveNode : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(
            BuildableDef checkingDef,
            IntVec3 loc,
            Rot4 rot,
            Map map,
            Thing thingToIgnore = null,
            Thing thing = null)
        {
            List<Thing> allThings = map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing candidate = allThings[i];
                if (candidate == thingToIgnore)
                {
                    continue;
                }

                if (candidate.def == checkingDef)
                {
                    return new AcceptanceReport(
                        "RC_SynapticNode_OnlyOne".Translate());
                }

                Blueprint_Build blueprint = candidate as Blueprint_Build;
                if (blueprint != null &&
                    blueprint.def.entityDefToBuild == checkingDef)
                {
                    return new AcceptanceReport(
                        "RC_SynapticNode_OnlyOne".Translate());
                }

                Frame frame = candidate as Frame;
                if (frame != null &&
                    frame.def.entityDefToBuild == checkingDef)
                {
                    return new AcceptanceReport(
                        "RC_SynapticNode_OnlyOne".Translate());
                }
            }

            return AcceptanceReport.WasAccepted;
        }
    }

    public static class RC_SynapticRefillUtility
    {
        public static Job TryMakeJob(
            Pawn pawn,
            Thing nodeThing,
            bool forced = false)
        {
            CompSynapticReservoir reservoir = nodeThing != null
                ? nodeThing.TryGetComp<CompSynapticReservoir>()
                : null;
            if (pawn == null ||
                reservoir == null ||
                !nodeThing.Spawned ||
                nodeThing.Faction != pawn.Faction ||
                reservoir.RefillSpace < 1f ||
                nodeThing.IsForbidden(pawn) ||
                !pawn.CanReserveAndReach(
                    nodeThing,
                    PathEndMode.Touch,
                    Danger.Deadly,
                    1,
                    -1,
                    null,
                    forced))
            {
                return null;
            }

            ThingDef jellyDef =
                DefDatabase<ThingDef>.GetNamedSilentFail(
                    "InsectJelly");
            if (jellyDef == null)
            {
                return null;
            }

            Thing jelly = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(jellyDef),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Deadly),
                9999f,
                delegate(Thing candidate)
                {
                    return candidate.stackCount > 0 &&
                        !candidate.IsForbidden(pawn) &&
                        pawn.CanReserve(
                            candidate,
                            1,
                            -1,
                            null,
                            forced);
                });
            if (jelly == null)
            {
                return null;
            }

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(
                "RC_RefillSynapticHiveNode");
            if (jobDef == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(jobDef, nodeThing, jelly);
            job.count = Math.Min(
                jelly.stackCount,
                Math.Max(1, Mathf.CeilToInt(reservoir.RefillSpace)));
            return job;
        }
    }

    public class WorkGiver_RefillSynapticHiveNode : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                ThingDef nodeDef = DefDatabase<ThingDef>
                    .GetNamedSilentFail("RC_SynapticHiveNode");
                return nodeDef != null
                    ? ThingRequest.ForDef(nodeDef)
                    : ThingRequest.ForGroup(ThingRequestGroup.Nothing);
            }
        }

        public override PathEndMode PathEndMode
        {
            get { return PathEndMode.Touch; }
        }

        public override bool HasJobOnThing(
            Pawn pawn,
            Thing t,
            bool forced = false)
        {
            return RC_SynapticRefillUtility.TryMakeJob(
                pawn,
                t,
                forced) != null;
        }

        public override Job JobOnThing(
            Pawn pawn,
            Thing t,
            bool forced = false)
        {
            return RC_SynapticRefillUtility.TryMakeJob(
                pawn,
                t,
                forced);
        }
    }

    public class JobDriver_RefillSynapticHiveNode : JobDriver
    {
        private const TargetIndex NodeIndex = TargetIndex.A;
        private const TargetIndex JellyIndex = TargetIndex.B;

        public override bool TryMakePreToilReservations(
            bool errorOnFailed)
        {
            return pawn.Reserve(
                    job.targetA,
                    job,
                    1,
                    -1,
                    null,
                    errorOnFailed) &&
                pawn.Reserve(
                    job.targetB,
                    job,
                    1,
                    job.count,
                    null,
                    errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(NodeIndex);
            this.FailOnDespawnedNullOrForbidden(JellyIndex);
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

    [StaticConstructorOnStartup]
    public static class RC_SynapticNetworkPatches
    {
        static RC_SynapticNetworkPatches()
        {
            try
            {
                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.SynapticHiveNetwork");
                harmony.Patch(
                    AccessTools.Method(
                        typeof(Thing),
                        "DeSpawn",
                        new[] { typeof(DestroyMode) }),
                    postfix: new HarmonyMethod(
                        typeof(RC_SynapticNetworkPatches),
                        "PawnDeSpawnPostfix"));
            }
            catch (Exception exception)
            {
                Log.Error(
                    "[RedCrow Synaptic Node] Patch installation failed:\n" +
                    exception);
            }
        }

        public static void PawnDeSpawnPostfix(Thing __instance)
        {
            Pawn pawn = __instance as Pawn;
            if (pawn == null)
            {
                return;
            }

            HediffDef networkDef =
                DefDatabase<HediffDef>.GetNamedSilentFail(
                    "RC_SynapticNetworkState");
            MapComponent_SynapticHiveNetwork.RemoveHediff(
                pawn,
                networkDef);
        }
    }
}
