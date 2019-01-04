using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Harmony;
using UnityEngine;
using Verse.AI;
using Verse;


namespace BioReactor
{
    [DefOf]
    public static class CustomDefOf
    {
        public static WorkGiverDef CustomWorkRefuel;
    }

    public class WorkGiver_CustomRefuel : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                
                ThingRequest thingRequest = ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
                return thingRequest;
            }
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public virtual JobDef JobStandard
        {
            get
            {
                return JobDefOf.Refuel;
            }
        }

        public virtual JobDef JobAtomic
        {
            get
            {
                return JobDefOf.RefuelAtomic;
            }
        }

        public virtual bool CanRefuelThing(Thing t)
        {
            return !(t is Building_Turret);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return CanRefuelThing(t) && RefuelWorkGiverUtility.CanRefuel(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return RefuelWorkGiverUtility.RefuelJob(pawn, t, forced, JobStandard, JobAtomic);
        }
    }
    public class CompBioRefuelable : CompRefuelable, IStoreSettingsParent
    {
        public StorageSettings inputSettings;
        CompFlickable flickComp;
        Building_BioReactor bioReactor;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            flickComp = parent.GetComp<CompFlickable>();
            if (inputSettings == null)
            {
                inputSettings = new StorageSettings(this);
                if (parent.def.building.defaultStorageSettings != null)
                {
                    inputSettings.CopyFrom(parent.def.building.defaultStorageSettings);
                }
            }
            bioReactor = (Building_BioReactor)parent;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref inputSettings, "inputSettings");

        }
        public override void CompTick()
        {
            if (!Props.consumeFuelOnlyWhenUsed && (this.flickComp == null || this.flickComp.SwitchIsOn) && (bioReactor != null && (bioReactor.InnerPawn != null)))
            {
                ConsumeFuel(ConsumptionRatePerTick);
            }
        }
        private float ConsumptionRatePerTick
        {
            get
            {
                return Props.fuelConsumptionRate / 60000f;
            }
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
        }

        public StorageSettings GetStoreSettings()
        {
            return inputSettings;
        }
        public StorageSettings GetParentStoreSettings()
        {
            return parent.def.building.fixedStorageSettings;
        }
        public bool StorageTabVisible
        {
            get
            {
                return true;
            }
        }
        public ThingFilter FuelFilter
        {
            get
            {
                return inputSettings.filter;
            }
        }

    }


    public class ITab_CustomRefuel : ITab
    {
        private const float TopAreaHeight = 35f;

        private Vector2 scrollPosition = default(Vector2);

        private static readonly Vector2 WinSize = new Vector2(300f, 480f);

        private IStoreSettingsParent SelStoreSettingsParent
        {
            get
            {
                return ((ThingWithComps)SelObject).GetComp<CompBioRefuelable>();
            }
        }

        public override bool IsVisible
        {
            get
            {
                return SelStoreSettingsParent.StorageTabVisible;
            }
        }

        public ITab_CustomRefuel()
        {
            size = WinSize;
            labelKey = Translator.Translate("RefuelTab");
        }

        protected override void FillTab()
        {
            IStoreSettingsParent selStoreSettingsParent = SelStoreSettingsParent;
            StorageSettings storeSettings = selStoreSettingsParent.GetStoreSettings();
            Rect rect = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x, WinSize.y), 10f);
            GUI.BeginGroup(rect);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect)
            {
                height = 32f
            }, Translator.Translate("RefuelTitle"));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            ThingFilter thingFilter = null;
            if (selStoreSettingsParent.GetParentStoreSettings() != null)
            {
                thingFilter = selStoreSettingsParent.GetParentStoreSettings().filter;
            }
            Rect rect2 = new Rect(0f, 40f, rect.width, rect.height - 40f);
            ThingFilterUI.DoThingFilterConfigWindow(rect2, ref scrollPosition, storeSettings.filter, thingFilter, 8, null, null, false, null, null);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.StorageTab, KnowledgeAmount.FrameDisplayed);
            GUI.EndGroup();
        }
    }


    public static class RefuelWorkGiverUtility
    {
        public static bool CanRefuel(Pawn pawn, Thing t, bool forced = false)
        {
            CompBioRefuelable compRefuelable = t.TryGetComp<CompBioRefuelable>();
            if (compRefuelable == null || compRefuelable.IsFull)
            {
                return false;
            }
            bool flag = !forced;
            if (flag && !compRefuelable.ShouldAutoRefuelNow)
            {
                return false;
            }
            if (!t.IsForbidden(pawn))
            {
                LocalTargetInfo target = t;
                if (pawn.CanReserve(target, 1, -1, null, forced))
                {
                    if (t.Faction != pawn.Faction)
                    {
                        return false;
                    }
                    if (FindBestFuel(pawn, t) == null)
                    {
                        ThingFilter fuelFilter = t.TryGetComp<CompBioRefuelable>().FuelFilter;
                        JobFailReason.Is("NoFuelToRefuel".Translate(fuelFilter.Summary), null);
                        return false;
                    }
                    if (t.TryGetComp<CompBioRefuelable>().Props.atomicFueling && FindAllFuel(pawn, t) == null)
                    {
                        ThingFilter fuelFilter2 = t.TryGetComp<CompBioRefuelable>().FuelFilter;
                        JobFailReason.Is("NoFuelToRefuel".Translate(fuelFilter2.Summary), null);
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public static Job RefuelJob(Pawn pawn, Thing t, bool forced = false, JobDef customRefuelJob = null, JobDef customAtomicRefuelJob = null)
        {
            if (!t.TryGetComp<CompBioRefuelable>().Props.atomicFueling)
            {
                Thing t2 = FindBestFuel(pawn, t);
                return new Job(customRefuelJob ?? JobDefOf.Refuel, t, t2);
            }
            List<Thing> source = FindAllFuel(pawn, t);
            Job job = new Job(customAtomicRefuelJob ?? JobDefOf.RefuelAtomic, t);
            job.targetQueueB = (from f in source
                                select new LocalTargetInfo(f)).ToList();
            return job;
        }

        private static Thing FindBestFuel(Pawn pawn, Thing refuelable)
        {
            ThingFilter filter = refuelable.TryGetComp<CompBioRefuelable>().FuelFilter;
            Predicate<Thing> predicate = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false) && filter.Allows(x);
            IntVec3 position = pawn.Position;
            Map map = pawn.Map;
            ThingRequest bestThingRequest = filter.BestThingRequest;
            PathEndMode peMode = PathEndMode.ClosestTouch;
            TraverseParms traverseParams = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            Predicate<Thing> validator = predicate;
            return GenClosest.ClosestThingReachable(position, map, bestThingRequest, peMode, traverseParams, 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
        }

        private static List<Thing> FindAllFuel(Pawn pawn, Thing refuelable)
        {
            int quantity = refuelable.TryGetComp<CompBioRefuelable>().GetFuelCountToFullyRefuel();
            ThingFilter filter = refuelable.TryGetComp<CompBioRefuelable>().FuelFilter;
            Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false) && filter.Allows(x);
            IntVec3 position = refuelable.Position;
            Region region = position.GetRegion(pawn.Map, RegionType.Set_Passable);
            TraverseParms traverseParams = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            RegionEntryPredicate entryCondition = (Region from, Region r) => r.Allows(traverseParams, false);
            List<Thing> chosenThings = new List<Thing>();
            int accumulatedQuantity = 0;
            RegionProcessor regionProcessor = delegate (Region r)
            {
                List<Thing> list = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing = list[i];
                    if (validator(thing))
                    {
                        if (!chosenThings.Contains(thing))
                        {
                            if (ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn))
                            {
                                chosenThings.Add(thing);
                                accumulatedQuantity += thing.stackCount;
                                if (accumulatedQuantity >= quantity)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 99999, RegionType.Set_Passable);
            if (accumulatedQuantity >= quantity)
            {
                return chosenThings;
            }
            return null;
        }
    }
}
