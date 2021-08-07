using RimWorld;
using Verse;


namespace BioReactor
{
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

            CompMapRefuelable component = parent.Map.GetComponent<CompMapRefuelable>();
            if (component == null)
            {
                return;
            }
            component.comps.Add(this);
        }
        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            CompMapRefuelable component = map.GetComponent<CompMapRefuelable>();
            if (component == null)
            {
                return;
            }
            component.comps.Remove(this);
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
}
