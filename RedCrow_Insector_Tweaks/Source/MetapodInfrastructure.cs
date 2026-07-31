using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    public enum RC_MetapodMode : byte
    {
        None,
        Usurpation,
        CorpseMemory,
        LarvalRebirth,
        PerfectImago
    }

    public sealed class RC_MetapodExtension : DefModExtension
    {
        public RC_MetapodMode mode;
        public int baseDurationTicks;
        public float fuelPerDay;
    }

    public sealed class RC_ArtificialPartRecord : IExposable
    {
        public HediffDef hediffDef;
        public BodyPartDef bodyPartDef;
        public string bodyPartPath;
        public List<BodyPartGroupDef> groups =
            new List<BodyPartGroupDef>();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref hediffDef, "hediffDef");
            Scribe_Defs.Look(ref bodyPartDef, "bodyPartDef");
            Scribe_Values.Look(ref bodyPartPath, "bodyPartPath");
            Scribe_Collections.Look(
                ref groups,
                "groups",
                LookMode.Def);

            if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                groups == null)
            {
                groups = new List<BodyPartGroupDef>();
            }
        }
    }

    public sealed class RC_GeneSnapshot : IExposable
    {
        public GeneDef geneDef;
        public bool xenogene;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref geneDef, "geneDef");
            Scribe_Values.Look(ref xenogene, "xenogene");
        }
    }

    public sealed class RC_MetapodTransformationData : IExposable
    {
        public RC_MetapodMode mode;
        public int baseDurationTicks;
        public float fuelPerDay;
        public int ticksSpent;
        public int startedAtTick = -1;
        public int comaStartedAtTick = -1;
        public int comaEndsAtTick = -1;
        public int cooldownStartedAtTick = -1;
        public bool costPaid;
        public bool transformationApplied;
        public ThingDef sourceRace;
        public PawnKindDef sourceKind;
        public XenotypeDef sourceXenotype;
        public string sourceXenotypeName;
        public XenotypeIconDef sourceXenotypeIcon;
        public bool sourceHybrid;
        public Faction sourceFaction;
        public Ideo sourceIdeo;
        public AbilityDef sourceAbility;
        public List<RC_GeneSnapshot> ordinaryGenes =
            new List<RC_GeneSnapshot>();
        public List<RC_ArtificialPartRecord> artificialParts =
            new List<RC_ArtificialPartRecord>();

        public static RC_MetapodTransformationData FromDef(ThingDef def)
        {
            RC_MetapodExtension extension =
                def.GetModExtension<RC_MetapodExtension>();
            return new RC_MetapodTransformationData
            {
                mode = extension != null
                    ? extension.mode
                    : RC_MetapodMode.None,
                baseDurationTicks = extension != null
                    ? extension.baseDurationTicks
                    : GenDate.TicksPerDay,
                fuelPerDay = extension != null
                    ? extension.fuelPerDay
                    : 0f,
                startedAtTick = Find.TickManager != null
                    ? Find.TickManager.TicksGame
                    : -1
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mode, "mode", RC_MetapodMode.None);
            Scribe_Values.Look(
                ref baseDurationTicks,
                "baseDurationTicks",
                GenDate.TicksPerDay);
            Scribe_Values.Look(ref fuelPerDay, "fuelPerDay");
            Scribe_Values.Look(ref ticksSpent, "ticksSpent");
            Scribe_Values.Look(ref startedAtTick, "startedAtTick", -1);
            Scribe_Values.Look(
                ref comaStartedAtTick,
                "comaStartedAtTick",
                -1);
            Scribe_Values.Look(ref comaEndsAtTick, "comaEndsAtTick", -1);
            Scribe_Values.Look(
                ref cooldownStartedAtTick,
                "cooldownStartedAtTick",
                -1);
            Scribe_Values.Look(ref costPaid, "costPaid");
            Scribe_Values.Look(
                ref transformationApplied,
                "transformationApplied");
            Scribe_Defs.Look(ref sourceRace, "sourceRace");
            Scribe_Defs.Look(ref sourceKind, "sourceKind");
            Scribe_Defs.Look(ref sourceXenotype, "sourceXenotype");
            Scribe_Values.Look(
                ref sourceXenotypeName,
                "sourceXenotypeName");
            Scribe_Defs.Look(
                ref sourceXenotypeIcon,
                "sourceXenotypeIcon");
            Scribe_Values.Look(ref sourceHybrid, "sourceHybrid");
            Scribe_References.Look(ref sourceFaction, "sourceFaction");
            Scribe_References.Look(ref sourceIdeo, "sourceIdeo");
            Scribe_Defs.Look(ref sourceAbility, "sourceAbility");
            Scribe_Collections.Look(
                ref ordinaryGenes,
                "ordinaryGenes",
                LookMode.Deep);
            Scribe_Collections.Look(
                ref artificialParts,
                "artificialParts",
                LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                artificialParts == null)
            {
                artificialParts = new List<RC_ArtificialPartRecord>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                ordinaryGenes == null)
            {
                ordinaryGenes = new List<RC_GeneSnapshot>();
            }
        }
    }

    public static class RC_BodyPartPathUtility
    {
        public static string GetPath(BodyPartRecord part)
        {
            if (part == null)
            {
                return null;
            }

            List<string> segments = new List<string>();
            BodyPartRecord current = part;
            while (current != null)
            {
                int ordinal = 0;
                if (current.parent != null)
                {
                    for (int index = 0;
                        index < current.parent.parts.Count;
                        index++)
                    {
                        BodyPartRecord sibling = current.parent.parts[index];
                        if (sibling == current)
                        {
                            break;
                        }

                        if (sibling.def == current.def)
                        {
                            ordinal++;
                        }
                    }
                }

                segments.Add(current.def.defName + "#" + ordinal);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        public static BodyPartRecord Resolve(BodyDef body, string path)
        {
            if (body == null || path.NullOrEmpty())
            {
                return null;
            }

            string[] segments = path.Split('/');
            BodyPartRecord current = body.corePart;
            if (!SegmentMatches(current, segments[0]))
            {
                return null;
            }

            for (int segmentIndex = 1;
                segmentIndex < segments.Length;
                segmentIndex++)
            {
                string defName;
                int ordinal;
                if (!ParseSegment(
                    segments[segmentIndex],
                    out defName,
                    out ordinal))
                {
                    return null;
                }

                int seen = 0;
                BodyPartRecord next = null;
                for (int childIndex = 0;
                    childIndex < current.parts.Count;
                    childIndex++)
                {
                    BodyPartRecord child = current.parts[childIndex];
                    if (child.def.defName != defName)
                    {
                        continue;
                    }

                    if (seen == ordinal)
                    {
                        next = child;
                        break;
                    }

                    seen++;
                }

                if (next == null)
                {
                    return null;
                }

                current = next;
            }

            return current;
        }

        private static bool SegmentMatches(
            BodyPartRecord part,
            string segment)
        {
            string defName;
            int ordinal;
            return part != null &&
                ParseSegment(segment, out defName, out ordinal) &&
                part.def.defName == defName &&
                ordinal == 0;
        }

        private static bool ParseSegment(
            string segment,
            out string defName,
            out int ordinal)
        {
            defName = null;
            ordinal = 0;
            if (segment.NullOrEmpty())
            {
                return false;
            }

            int separator = segment.LastIndexOf('#');
            if (separator <= 0 ||
                !int.TryParse(
                    segment.Substring(separator + 1),
                    out ordinal))
            {
                return false;
            }

            defName = segment.Substring(0, separator);
            return true;
        }
    }

    public static class RC_MetapodHealthUtility
    {
        private const string SolarConditionDefName =
            "RC_SolarStuporCondition";

        public static List<RC_ArtificialPartRecord>
            CaptureArtificialParts(Pawn pawn)
        {
            List<RC_ArtificialPartRecord> records =
                new List<RC_ArtificialPartRecord>();
            if (pawn == null || pawn.health == null)
            {
                return records;
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (!IsArtificialPart(hediff))
                {
                    continue;
                }

                records.Add(new RC_ArtificialPartRecord
                {
                    hediffDef = hediff.def,
                    bodyPartDef = hediff.Part != null
                        ? hediff.Part.def
                        : null,
                    bodyPartPath =
                        RC_BodyPartPathUtility.GetPath(hediff.Part),
                    groups = hediff.Part != null &&
                        hediff.Part.groups != null
                            ? hediff.Part.groups.ToList()
                            : new List<BodyPartGroupDef>()
                });
            }

            return records;
        }

        public static bool CanMapArtificialParts(
            IEnumerable<RC_ArtificialPartRecord> parts,
            BodyDef targetBody,
            out string missingPath)
        {
            missingPath = null;
            if (parts == null)
            {
                return true;
            }

            foreach (RC_ArtificialPartRecord part in parts)
            {
                if (part == null ||
                    ResolveCompatiblePart(
                        targetBody,
                        part) != null)
                {
                    continue;
                }

                missingPath = part.bodyPartPath;
                return false;
            }

            return true;
        }

        public static BodyPartRecord ResolveCompatiblePart(
            BodyDef body,
            RC_ArtificialPartRecord record)
        {
            if (body == null || record == null)
            {
                return null;
            }

            BodyPartRecord exact =
                RC_BodyPartPathUtility.Resolve(
                    body,
                    record.bodyPartPath);
            if (exact != null &&
                (record.bodyPartDef == null ||
                    exact.def == record.bodyPartDef))
            {
                return exact;
            }

            List<BodyPartRecord> candidates =
                body.AllParts.Where(
                    part =>
                        record.bodyPartDef != null &&
                        part.def == record.bodyPartDef)
                    .ToList();
            if (record.groups != null &&
                record.groups.Count > 0)
            {
                candidates = candidates.Where(
                    part =>
                        part.groups != null &&
                        part.groups.Any(
                            group => record.groups.Contains(group)))
                    .ToList();
            }

            return candidates.Count == 1
                ? candidates[0]
                : null;
        }

        public static void CleanForTransformation(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            List<Hediff> toRemove = new List<Hediff>();
            foreach (Hediff hediff in
                pawn.health.hediffSet.hediffs.ToList())
            {
                Hediff_MissingPart missingPart =
                    hediff as Hediff_MissingPart;
                if (missingPart != null &&
                    missingPart.Part != null &&
                    pawn.health.hediffSet
                        .PartOrAnyAncestorHasDirectlyAddedParts(
                            missingPart.Part))
                {
                    continue;
                }

                if (ShouldRemove(hediff))
                {
                    toRemove.Add(hediff);
                }
            }

            foreach (Hediff hediff in toRemove)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static bool ShouldRemove(Hediff hediff)
        {
            if (hediff == null ||
                hediff.def.defName == SolarConditionDefName ||
                IsArtificialPart(hediff))
            {
                return false;
            }

            if (hediff is Hediff_Injury ||
                hediff is Hediff_MissingPart)
            {
                return true;
            }

            string typeName = hediff.GetType().Name;
            if (typeName.IndexOf(
                    "Addiction",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf(
                    "Withdrawal",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf(
                    "Disease",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf(
                    "Infection",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf(
                    "Parasite",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return hediff.def.chronic ||
                (hediff.def.isBad &&
                    (hediff.def.tendable ||
                        hediff.TryGetComp<
                            HediffComp_Immunizable>() != null));
        }

        private static bool IsArtificialPart(Hediff hediff)
        {
            return hediff is Hediff_Implant;
        }
    }

    public class RC_MetapodBase : Building, IThingHolder
    {
        private ThingOwner innerContainer;
        private Effecter progressBarEffecter;
        private CompRefuelable refuelable;
        private float progressTicks;
        private bool releasingOccupant;

        public RC_MetapodTransformationData transformationData;

        public Pawn Occupant
        {
            get
            {
                return innerContainer != null
                    ? innerContainer.OfType<Pawn>().FirstOrDefault()
                    : null;
            }
        }

        public float Progress
        {
            get
            {
                int duration = DurationTicks;
                if (duration <= 0)
                {
                    return 1f;
                }

                float progress = progressTicks / duration;
                return Math.Max(0f, Math.Min(1f, progress));
            }
        }

        public int DurationTicks
        {
            get
            {
                EnsureTransformationData();
                return Math.Max(
                    1,
                    transformationData.baseDurationTicks);
            }
        }

        public int TicksRemaining
        {
            get
            {
                return Math.Max(
                    0,
                    (int)Math.Ceiling(DurationTicks - progressTicks));
            }
        }

        public int EstimatedTicksRemaining
        {
            get
            {
                float rate =
                    refuelable != null && refuelable.HasFuel
                        ? 5f
                        : 1f;
                return Math.Max(
                    0,
                    (int)Math.Ceiling(
                        TicksRemaining / rate));
            }
        }

        public override string Label
        {
            get
            {
                Pawn pawn = Occupant;
                return pawn != null
                    ? base.Label + ": " + pawn.LabelShort
                    : base.Label;
            }
        }

        public RC_MetapodBase()
        {
            innerContainer =
                new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public override void SpawnSetup(
            Map map,
            bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            refuelable = GetComp<CompRefuelable>();
            EnsureTransformationData();
        }

        public override void DeSpawn(
            DestroyMode mode = DestroyMode.Vanish)
        {
            CleanupEffects();
            base.DeSpawn(mode);
        }

        public override void Tick()
        {
            base.Tick();
            Pawn pawn = Occupant;
            if (pawn == null || !Spawned)
            {
                return;
            }

            EnsureTransformationData();
            float rate = refuelable != null && refuelable.HasFuel
                ? 5f
                : 1f;
            progressTicks =
                Math.Min(DurationTicks, progressTicks + rate);
            transformationData.ticksSpent++;
            DoProgressBar(Progress);

            if (progressTicks >= DurationTicks)
            {
                Complete();
            }
        }

        public bool TryAcceptPawn(
            Pawn pawn,
            RC_MetapodTransformationData data,
            out string reason,
            bool allowUnspawned = false)
        {
            reason = null;
            if (pawn == null)
            {
                reason = "RC_Metapod_NoPawn".Translate();
                return false;
            }

            if (Occupant != null)
            {
                reason = "RC_Metapod_Occupied".Translate();
                return false;
            }

            if (pawn.ParentHolder is RC_MetapodBase)
            {
                reason = "RC_Metapod_AlreadyContained".Translate();
                return false;
            }

            if (!pawn.Spawned && !allowUnspawned)
            {
                reason = "RC_Metapod_PawnUnavailable".Translate();
                return false;
            }

            transformationData =
                data ?? RC_MetapodTransformationData.FromDef(def);
            RC_MetapodExtension extension =
                def.GetModExtension<RC_MetapodExtension>();
            if (extension != null)
            {
                transformationData.mode = extension.mode;
                transformationData.baseDurationTicks =
                    extension.baseDurationTicks;
                transformationData.fuelPerDay =
                    extension.fuelPerDay;
            }
            if (transformationData.sourceRace == null)
            {
                transformationData.sourceRace = pawn.def;
            }
            if (transformationData.sourceXenotype == null &&
                pawn.genes != null)
            {
                transformationData.sourceXenotype =
                    pawn.genes.Xenotype;
            }
            if (transformationData.sourceFaction == null)
            {
                transformationData.sourceFaction = pawn.Faction;
            }
            if (transformationData.sourceIdeo == null &&
                pawn.ideo != null)
            {
                transformationData.sourceIdeo = pawn.Ideo;
            }
            if (transformationData.artificialParts == null ||
                transformationData.artificialParts.Count == 0)
            {
                transformationData.artificialParts =
                    RC_MetapodHealthUtility.CaptureArtificialParts(
                        pawn);
            }

            SetFaction(pawn.Faction);
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }
            if (!innerContainer.TryAddOrTransfer(pawn, false))
            {
                if (!pawn.Spawned)
                {
                    GenSpawn.Spawn(pawn, Position, Map);
                }
                reason = "RC_Metapod_TransferFailed".Translate();
                return false;
            }

            return true;
        }

        public override bool DeconstructibleBy(Faction faction)
        {
            return Occupant == null && base.DeconstructibleBy(faction);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.ShowDevGizmos && Occupant != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Near complete",
                    action = delegate
                    {
                        progressTicks = DurationTicks - 300;
                    }
                };
            }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(
                outChildren,
                GetDirectlyHeldThings());
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(
                ref innerContainer,
                "innerContainer",
                this);
            Scribe_Deep.Look(
                ref transformationData,
                "transformationData");
            Scribe_Values.Look(ref progressTicks, "progressTicks");
            Scribe_Values.Look(
                ref releasingOccupant,
                "releasingOccupant");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (innerContainer == null)
                {
                    innerContainer =
                        new ThingOwner<Thing>(
                            this,
                            false,
                            LookMode.Deep);
                }

                EnsureTransformationData();
                releasingOccupant = false;
            }
        }

        public override string GetInspectStringLowPriority()
        {
            string text = base.GetInspectStringLowPriority();
            Pawn pawn = Occupant;
            if (pawn == null)
            {
                return text;
            }

            if (!text.NullOrEmpty())
            {
                text += "\n";
            }

            text += "RC_Metapod_TimeRemaining".Translate(
                pawn.Named("PAWN"),
                GenDate.ToStringTicksToPeriod(
                    EstimatedTicksRemaining,
                    true,
                    false,
                    true,
                    true,
                    false));
            return text;
        }

        public override void Destroy(
            DestroyMode mode = DestroyMode.Vanish)
        {
            CleanupEffects();
            base.Destroy(mode);
            if (!releasingOccupant && innerContainer != null)
            {
                innerContainer.ClearAndDestroyContents(
                    DestroyMode.Vanish);
            }
        }

        private void EnsureTransformationData()
        {
            if (transformationData == null)
            {
                transformationData =
                    RC_MetapodTransformationData.FromDef(def);
            }
        }

        private void Complete()
        {
            Pawn pawn = Occupant;
            if (pawn == null || !Spawned)
            {
                return;
            }

            Pawn result =
                RC_PawnTransformationUtility.ApplyTransformation(
                    pawn,
                    transformationData);
            if (result == null)
            {
                return;
            }

            releasingOccupant = true;
            Thing dropped;
            innerContainer.TryDrop(
                result,
                ThingPlaceMode.Direct,
                out dropped);
            Destroy(DestroyMode.Vanish);
        }

        private void DoProgressBar(float progress)
        {
            if (progressBarEffecter == null)
            {
                progressBarEffecter =
                    EffecterDefOf.ProgressBar.Spawn();
            }

            progressBarEffecter.EffectTick(this, TargetInfo.Invalid);
            SubEffecter_ProgressBar child =
                progressBarEffecter.children[0]
                    as SubEffecter_ProgressBar;
            if (child != null &&
                child.mote != null &&
                !child.mote.Destroyed)
            {
                child.mote.progress = progress;
            }
        }

        private void CleanupEffects()
        {
            if (progressBarEffecter != null)
            {
                progressBarEffecter.Cleanup();
                progressBarEffecter = null;
            }
        }
    }
}
