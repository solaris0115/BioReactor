using RimWorld;
using Verse;


namespace BioReactor
{
    public class CompBioPowerPlant : CompPowerPlant
    {
        public Building_BioReactor building_BioReactor;
        public CompRefuelable compRefuelable;

        protected override float DesiredPowerOutput
        {
            get
            {
                return -Props.basePowerConsumption;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            building_BioReactor = (Building_BioReactor)parent;
            compRefuelable = parent.GetComp<CompRefuelable>();
        }

        public override void CompTick()
        {
            base.CompTick();
            this.UpdateDesiredPowerOutput();
        }

        public new void UpdateDesiredPowerOutput()
        {
            if ((building_BioReactor != null && !(building_BioReactor.state == Building_BioReactor.ReactorState.Full)) || (breakdownableComp != null && breakdownableComp.BrokenDown) || (refuelableComp != null && !refuelableComp.HasFuel) || (this.flickableComp != null && !this.flickableComp.SwitchIsOn) || !base.PowerOn)
            {
                PowerOutput = 0f;
            }
            else
            {
                Pawn pawn = building_BioReactor.ContainedThing as Pawn;
                if (pawn != null)
                {
                    if (pawn.Dead || (pawn.RaceProps.FleshType == FleshTypeDefOf.Mechanoid))
                    {
                        PowerOutput = 0;
                        return;
                    }
                    if ((pawn.RaceProps.Humanlike))
                    {
                        PowerOutput = DesiredPowerOutput;
                    }
                    else
                    {
                        PowerOutput = DesiredPowerOutput * 0.50f;
                    }
                    PowerOutput *= pawn.BodySize;
                }
            }
        }
    }
}
