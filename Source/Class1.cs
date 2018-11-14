using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;
using Harmony;
using RimWorld.Planet;

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
                return -base.Props.basePowerConsumption;
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
            if ((building_BioReactor != null && !(building_BioReactor.state == Building_BioReactor.ReactorState.Full)) || (this.breakdownableComp != null && this.breakdownableComp.BrokenDown) || (this.refuelableComp != null && !this.refuelableComp.HasFuel) || (this.flickableComp != null && !this.flickableComp.SwitchIsOn) || !base.PowerOn)
            {
                base.PowerOutput = 0f;
            }
            else
            {
                
                Pawn pawn = building_BioReactor.ContainedThing as Pawn;
                if (pawn != null)
                {
                    if (pawn.Dead||(pawn.RaceProps.FleshType == FleshTypeDefOf.Mechanoid))
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
                        PowerOutput = this.DesiredPowerOutput * 0.75f;
                    }
                }
            }
        }
    }
    internal class CompProperties_SecondLayer : CompProperties
    {
        public GraphicData graphicData = null;
        public Vector3 offset = new Vector3();

        public AltitudeLayer altitudeLayer = AltitudeLayer.MoteOverhead;

        public float Altitude
        {
            get
            {
                return this.altitudeLayer.AltitudeFor();
            }
        }

        public CompProperties_SecondLayer()
        {
            this.compClass = typeof(CompSecondLayer);
        }
    }
    internal class CompSecondLayer : ThingComp
    {
        private Graphic graphicInt;
        public Vector3 offset;

        public CompProperties_SecondLayer Props
        {
            get
            {
                return (CompProperties_SecondLayer)this.props;
            }
        }

        public virtual Graphic Graphic
        {
            get
            {
                if (this.graphicInt == null)
                {
                    if (this.Props.graphicData == null)
                    {
                        Log.ErrorOnce(this.parent.def + "BioReactor - has no SecondLayer graphicData but we are trying to access it.", 764532, false);
                        return BaseContent.BadGraphic;
                    }
                    this.graphicInt = this.Props.graphicData.GraphicColoredFor(this.parent);
                    offset = this.Props.offset;
                }
                return this.graphicInt;
            }
        }

        public override void PostDraw()
        {
            if (parent.Rotation == Rot4.South)
            {
                this.Graphic.Draw(GenThing.TrueCenter(this.parent.Position, this.parent.Rotation, this.parent.def.size, Props.Altitude) + offset, this.parent.Rotation, this.parent, 0f);
                return;
            }

        }
    }
    /*public class CompBioGlower : CompGlower
      {
          public override void ReceiveCompSignal(string signal)
          {
              if (signal == "PowerTurnedOn" || signal == "PowerTurnedOff" || signal == "FlickedOn" || signal == "FlickedOff" || signal == "Refueled" || signal == "RanOutOfFuel" || signal == "ScheduledOn" || signal == "ScheduledOff")
              {
                  this.UpdateLit(this.parent.Map);
              }
          }
      }
      public class CompProperties_BioGlower : CompProperties_Glower
      {
          public ColorInt glowColor2 = new ColorInt(255, 255, 255, 0) * 1.45f;

          public CompProperties_BioGlower()
          {
              this.compClass = typeof(CompBioGlower);
          }
      }*/

    public class Building_BioReactor : Building_Casket
    {
        public enum ReactorState
        {
            Empty,//none
            StartFilling,//animating Filling
            Full,//Just Drawing
            HistolysisStating,//Start Animating and Changing Color
            HistolysisEnding,
            HistolysisDone//Just Drawing
        }
        public ReactorState state = ReactorState.Empty;
        public float fillpct;
        public float histolysisPct = 0;

        public CompRefuelable compRefuelable;


        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            compRefuelable = GetComp<CompRefuelable>();
            fillpct = 0;
            histolysisPct = 0;
        }

        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (base.TryAcceptThing(thing, allowSpecialEffects))
            {
                if (allowSpecialEffects)
                {
                    SoundDefOf.CryptosleepCasket_Accept.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
                }
                state = ReactorState.StartFilling;
                Pawn pawn = thing as Pawn;
                if(pawn !=null && pawn.RaceProps.Humanlike)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(BioReactorThoughtDef.LivingBattery, null);
                }
                return true;
            }
            return false;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
        {
            foreach (FloatMenuOption o in base.GetFloatMenuOptions(myPawn))
            {
                yield return o;
            }
            if (this.innerContainer.Count == 0)
            {
                if (!myPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly, false, TraverseMode.ByPawn))
                {
                    FloatMenuOption failer = new FloatMenuOption("CannotUseNoPath".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    yield return failer;
                }
                else
                {
                    JobDef jobDef = Bio_JobDefOf.EnterBioReactor;
                    string jobStr = "EnterBioReactor".Translate();
                    Action jobAction = delegate ()
                    {
                        Job job = new Job(jobDef, this);
                        myPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    };
                    yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(jobStr, jobAction, MenuOptionPriority.Default, null, null, 0f, null, null), myPawn, this, "ReservedBy");
                }
            }
            yield break;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo c in base.GetGizmos())
            {
                yield return c;
            }
            if (HasAnyContents)
            {
                Pawn pawn = ContainedThing as Pawn;
                if (pawn != null)
                {
                    if (pawn.RaceProps.FleshType == FleshTypeDefOf.Normal || pawn.RaceProps.FleshType == FleshTypeDefOf.Insectoid)
                    {
                        if (state == ReactorState.Full)
                        {
                            yield return new Command_Action
                            {
                                defaultLabel = "Histolysis".Translate(),
                                defaultDesc = "HistolysisDesc".Translate(),
                                icon = ContentFinder<Texture2D>.Get("UI/Commands/Histolysis", true),
                                action = delegate ()
                                {
                                    BioReactorSoundDef.Drowning.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
                                    state = ReactorState.HistolysisStating;
                                }
                            };
                        }
                    }                    

                }
            }
            yield break;
        }

        public override void EjectContents()
        {
            ThingDef filth_Slime = ThingDefOf.Filth_Slime;
            foreach (Thing thing in ((IEnumerable<Thing>)this.innerContainer))
            {
                Pawn pawn = thing as Pawn;
                if (pawn != null)
                {
                    PawnComponentsUtility.AddComponentsForSpawn(pawn);
                    pawn.filth.GainFilth(filth_Slime);
                    if (pawn.RaceProps.IsFlesh)
                    {
                        pawn.health.AddHediff(HediffDefOf.CryptosleepSickness, null, null, null);
                    }
                }
            }
            if (!base.Destroyed)
            {
                SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map, false), MaintenanceType.None));
            }
            state = ReactorState.Empty;
            base.EjectContents();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<ReactorState>(ref state, "state");
            Scribe_Values.Look<float>(ref fillpct, "fillpct");
            Scribe_Values.Look<float>(ref histolysisPct, "histolysisPct");
        }
        public virtual void Histolysis()
        {
            if (HasAnyContents)
            {
                Pawn pawn = ContainedThing as Pawn;
                if (pawn != null)
                {
                    compRefuelable.Refuel(35);
                    DamageInfo d = new DamageInfo();
                    d.Def = DamageDefOf.Burn;
                    d.SetAmount(1000);
                    pawn.Kill(d);
                    try
                    {
                        CompRottable compRottable = ContainedThing.TryGetComp<CompRottable>();
                        if (compRottable != null)
                        {
                            compRottable.RotProgress += 600000f;
                        }
                        MakeFuel();
                    }
                    catch (Exception ee)
                    {
                        Log.Message("Rot Error" + ee);
                    }
                    if (pawn.RaceProps.Humanlike)
                    {
                        foreach (Pawn p in this.Map.mapPawns.SpawnedPawnsInFaction(Faction))
                        {
                            if (p.needs != null && p.needs.mood != null && p.needs.mood.thoughts != null)
                            {
                                p.needs.mood.thoughts.memories.TryGainMemory(BioReactorThoughtDef.KnowHistolysisHumanlike, null);
                            }
                        }
                    }
                }
            }
        }
        public void MakeFuel()
        {
            ThingDef stuff = GenStuff.RandomStuffFor(ThingDefOf.Chemfuel);
            Thing thing = ThingMaker.MakeThing(ThingDefOf.Chemfuel, stuff);
            thing.stackCount = 35;
            GenPlace.TryPlaceThing(thing, Position, Find.CurrentMap, ThingPlaceMode.Near, null, null);
        }

        public static Building_BioReactor FindBioReactorFor(Pawn p, Pawn traveler, bool ignoreOtherReservations = false)
        {
            IEnumerable<ThingDef> enumerable = from def in DefDatabase<ThingDef>.AllDefs
                                               where typeof(Building_BioReactor).IsAssignableFrom(def.thingClass)
                                               select def;
            foreach (ThingDef singleDef in enumerable)
            {
                Building_BioReactor building_BioReactor = (Building_BioReactor)GenClosest.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForDef(singleDef), PathEndMode.InteractionCell, TraverseParms.For(traveler, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, delegate (Thing x)
                {
                    bool result;
                    if (!((Building_BioReactor)x).HasAnyContents)
                    {
                        Pawn traveler2 = traveler;
                        LocalTargetInfo target = x;
                        bool ignoreOtherReservations2 = ignoreOtherReservations;
                        result = traveler2.CanReserve(target, 1, -1, null, ignoreOtherReservations2);
                    }
                    else
                    {
                        result = false;
                    }
                    return result;
                }, null, 0, -1, false, RegionType.Set_Passable, false);
                if (building_BioReactor != null)
                {
                    return building_BioReactor;
                }
            }
            return null;
        }

        public override void Tick()
        {
            base.Tick();
            switch (state)
            {
                case ReactorState.Empty:
                    break;
                case ReactorState.StartFilling:
                    fillpct += 0.01f;
                    if (fillpct >= 1)
                    {
                        state = ReactorState.Full;
                        fillpct = 0;
                        BioReactorSoundDef.Drowning.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
                    }
                    break;
                case ReactorState.Full:
                    break;
                case ReactorState.HistolysisStating:
                    histolysisPct += 0.005f;
                    if (histolysisPct >= 1)
                    {
                        state = ReactorState.HistolysisEnding;
                        Histolysis();
                    }
                    break;
                case ReactorState.HistolysisEnding:
                    histolysisPct -= 0.01f;
                    if (histolysisPct <= 0)
                    {
                        histolysisPct = 0;
                        state = ReactorState.HistolysisDone;
                    }
                    break;
                case ReactorState.HistolysisDone:
                    break;
            }
        }

        public override void Draw()
        {
            switch (state)
            {
                case ReactorState.Empty:
                    break;
                case ReactorState.StartFilling:
                    foreach (Thing t in innerContainer)
                    {
                        Pawn pawn = t as Pawn;
                        if (pawn != null)
                        {
                            DrawInnerThing(pawn, DrawPos + new Vector3(0, -0.05f, 0.65f), 1.7f, true, Rot4.South, Rot4.South, RotDrawMode.Fresh, false, false);
                            LiquidDraw(new Color32(123, 255, 233, 75), fillpct);
                        }
                    }
                    break;
                case ReactorState.Full:
                    foreach (Thing t in innerContainer)
                    {
                        Pawn pawn = t as Pawn;
                        if (pawn != null)
                        {
                            DrawInnerThing(pawn, DrawPos + new Vector3(0, -0.05f, 0.65f), 1.7f, true, Rot4.South, Rot4.South, RotDrawMode.Fresh, false, false);
                            LiquidDraw(new Color32(123, 255, 233, 75), 1);
                        }
                    }
                    break;
                case ReactorState.HistolysisStating:
                    foreach (Thing t in innerContainer)
                    {
                        Pawn pawn = t as Pawn;
                        if (pawn != null)
                        {
                            DrawInnerThing(pawn, DrawPos + new Vector3(0, -0.05f, 0.65f), 1.7f, true, Rot4.South, Rot4.South, RotDrawMode.Fresh, false, false);
                            LiquidDraw(new Color(0.48f + (0.2f * histolysisPct), 1 - (0.7f * histolysisPct), 0.9f - (0.6f * histolysisPct), 0.3f + histolysisPct * 0.55f), 1);
                        }
                    }
                    break;
                case ReactorState.HistolysisEnding:
                    foreach (Thing t in innerContainer)
                    {
                        t.DrawAt(DrawPos + new Vector3(0, -0.05f, 0.65f));
                        LiquidDraw(new Color(0.7f, 0.2f, 0.2f, 0.4f + (0.45f * histolysisPct)), 1);
                    }
                    break;
                case ReactorState.HistolysisDone:
                    foreach (Thing t in innerContainer)
                    {
                        t.DrawAt(DrawPos + new Vector3(0, -0.05f, 0.65f));
                        LiquidDraw(new Color(0.7f, 0.3f, 0.3f, 0.4f), 1);
                    }
                    break;
            }
            base.Draw();
        }
        public virtual void LiquidDraw(Color color, float fillPct)
        {
            GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
            r.center = this.DrawPos + new Vector3(0, -0.02f, 0.65f);
            r.size = new Vector2(1.6f, 1.18f);
            r.fillPercent = fillPct;
            r.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(color, false);
            r.unfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0, 0, 0, 0), false);
            r.margin = 0f;
            Rot4 rotation = Rotation;
            rotation.Rotate(RotationDirection.Clockwise);
            r.rotation = rotation;
            GenDraw.DrawFillableBar(r);
        }
        public virtual void DrawInnerThing(Pawn pawn, Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump)
        {
            PawnGraphicSet graphics = pawn.Drawer.renderer.graphics;
            PawnRenderer renderer = pawn.Drawer.renderer;
            if (!graphics.AllResolved)
            {
                graphics.ResolveAllGraphics();
            }
            Quaternion quaternion = Quaternion.AngleAxis(angle, Vector3.up);
            Mesh mesh = null;
            if (renderBody)
            {
                Vector3 loc = rootLoc;
                loc.y += 0.0078125f;
                if (bodyDrawType == RotDrawMode.Dessicated && !pawn.RaceProps.Humanlike && graphics.dessicatedGraphic != null && !portrait)
                {
                    graphics.dessicatedGraphic.Draw(loc, bodyFacing, pawn, angle);
                }
                else
                {
                    if (pawn.RaceProps.Humanlike)
                    {
                        mesh = MeshPool.humanlikeBodySet.MeshAt(bodyFacing);
                    }
                    else
                    {
                        mesh = graphics.nakedGraphic.MeshAt(bodyFacing);
                    }
                    List<Material> list = graphics.MatsBodyBaseAt(bodyFacing, bodyDrawType);
                    for (int i = 0; i < list.Count; i++)
                    {
                        Material damagedMat = graphics.flasher.GetDamagedMat(list[i]);
                        GenDraw.DrawMeshNowOrLater(mesh, loc, quaternion, damagedMat, portrait);
                        loc.y += 0.00390625f;
                    }
                }
            }
            Vector3 vector = rootLoc;
            Vector3 a = rootLoc;
            if (bodyFacing != Rot4.North)
            {
                a.y += 0.02734375f;
                vector.y += 0.0234375f;
            }
            else
            {
                a.y += 0.0234375f;
                vector.y += 0.02734375f;
            }
            if (graphics.headGraphic != null)
            {
                Vector3 b = quaternion * renderer.BaseHeadOffsetAt(headFacing);
                Material material = graphics.HeadMatAt(headFacing, bodyDrawType, headStump);
                if (material != null)
                {
                    Mesh mesh2 = MeshPool.humanlikeHeadSet.MeshAt(headFacing);
                    GenDraw.DrawMeshNowOrLater(mesh2, a + b, quaternion, material, portrait);
                }
                Vector3 loc2 = rootLoc + b;
                loc2.y += 0.03125f;
                bool flag = false;
                if (!portrait || !Prefs.HatsOnlyOnMap)
                {
                    Mesh mesh3 = graphics.HairMeshSet.MeshAt(headFacing);
                    List<ApparelGraphicRecord> apparelGraphics = graphics.apparelGraphics;
                    for (int j = 0; j < apparelGraphics.Count; j++)
                    {
                        if (apparelGraphics[j].sourceApparel.def.apparel.LastLayer == ApparelLayerDefOf.Overhead)
                        {
                            if (!apparelGraphics[j].sourceApparel.def.apparel.hatRenderedFrontOfFace)
                            {
                                flag = true;
                                Material material2 = apparelGraphics[j].graphic.MatAt(bodyFacing, null);
                                material2 = graphics.flasher.GetDamagedMat(material2);
                                GenDraw.DrawMeshNowOrLater(mesh3, loc2, quaternion, material2, portrait);
                            }
                            else
                            {
                                Material material3 = apparelGraphics[j].graphic.MatAt(bodyFacing, null);
                                material3 = graphics.flasher.GetDamagedMat(material3);
                                Vector3 loc3 = rootLoc + b;
                                loc3.y += ((!(bodyFacing == Rot4.North)) ? 0.03515625f : 0.00390625f);
                                GenDraw.DrawMeshNowOrLater(mesh3, loc3, quaternion, material3, portrait);
                            }
                        }
                    }
                }
                if (!flag && bodyDrawType != RotDrawMode.Dessicated && !headStump)
                {
                    Mesh mesh4 = graphics.HairMeshSet.MeshAt(headFacing);
                    Material mat = graphics.HairMatAt(headFacing);
                    GenDraw.DrawMeshNowOrLater(mesh4, loc2, quaternion, mat, portrait);
                }
            }
            if (renderBody)
            {
                for (int k = 0; k < graphics.apparelGraphics.Count; k++)
                {
                    ApparelGraphicRecord apparelGraphicRecord = graphics.apparelGraphics[k];
                    if (apparelGraphicRecord.sourceApparel.def.apparel.LastLayer == ApparelLayerDefOf.Shell)
                    {
                        Material material4 = apparelGraphicRecord.graphic.MatAt(bodyFacing, null);
                        material4 = graphics.flasher.GetDamagedMat(material4);
                        GenDraw.DrawMeshNowOrLater(mesh, vector, quaternion, material4, portrait);
                    }
                }
            }
            if (!portrait && pawn.RaceProps.Animal && pawn.inventory != null && pawn.inventory.innerContainer.Count > 0 && graphics.packGraphic != null)
            {
                Graphics.DrawMesh(mesh, vector, quaternion, graphics.packGraphic.MatAt(bodyFacing, null), 0);
            }
            if (!portrait)
            {
                if (pawn.apparel != null)
                {
                    List<Apparel> wornApparel = pawn.apparel.WornApparel;
                    for (int l = 0; l < wornApparel.Count; l++)
                    {
                        wornApparel[l].DrawWornExtras();
                    }
                }
                Vector3 bodyLoc = rootLoc;
                bodyLoc.y += 0.04296875f;
            }
        }
    }

    public class JobDriver_CarryToBioReactor : JobDriver
    {
        private const TargetIndex TakeeInd = TargetIndex.A;

        private const TargetIndex DropPodInd = TargetIndex.B;

        protected Pawn Takee
        {
            get
            {
                return (Pawn)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        protected Building_BioReactor DropPod
        {
            get
            {
                return (Building_BioReactor)this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = this.Takee;
            Job job = this.job;
            bool result;
            if (pawn.Reserve(target, job, 1, -1, null, errorOnFailed))
            {
                pawn = this.pawn;
                target = this.DropPod;
                job = this.job;
                result = pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            }
            else
            {
                result = false;
            }
            return result;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnAggroMentalState(TargetIndex.A);
            this.FailOn(() => !this.DropPod.Accepts(this.Takee));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOn(() => this.DropPod.GetDirectlyHeldThings().Count > 0).FailOn(() => !this.Takee.Downed).FailOn(() => !this.pawn.CanReach(this.Takee, PathEndMode.OnCell, Danger.Deadly, false, TraverseMode.ByPawn)).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);
            Toil prepare = Toils_General.Wait(500, TargetIndex.None);
            prepare.FailOnCannotTouch(TargetIndex.B, PathEndMode.InteractionCell);
            prepare.WithProgressBarToilDelay(TargetIndex.B, false, -0.5f);
            yield return prepare;
            yield return new Toil
            {
                initAction = delegate ()
                {
                    this.DropPod.TryAcceptThing(this.Takee, true);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield break;
        }

        public override object[] TaleParameters()
        {
            return new object[]
            {
                this.pawn,
                this.Takee
            };
        }
    }

    public class JobDriver_EnterBioReactor : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo targetA = this.job.targetA;
            Job job = this.job;
            return pawn.Reserve(targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil prepare = Toils_General.Wait(500, TargetIndex.None);
            prepare.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            prepare.WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return prepare;
            Toil enter = new Toil();
            enter.initAction = delegate ()
            {
                Pawn actor = enter.actor;
                Building_BioReactor pod = (Building_BioReactor)actor.CurJob.targetA.Thing;
                Action action = delegate ()
                {
                    actor.DeSpawn(DestroyMode.Vanish);
                    pod.TryAcceptThing(actor, true);
                };
                if (!pod.def.building.isPlayerEjectable)
                {
                    int freeColonistsSpawnedOrInPlayerEjectablePodsCount = this.Map.mapPawns.FreeColonistsSpawnedOrInPlayerEjectablePodsCount;
                    if (freeColonistsSpawnedOrInPlayerEjectablePodsCount <= 1)
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CasketWarning".Translate(actor.Named("PAWN")).AdjustedFor(actor, "PAWN"), action, false, null));
                    }
                    else
                    {
                        action();
                    }
                }
                else
                {
                    action();
                }
            };
            enter.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enter;
            yield break;
        }
    }

    [DefOf]
    public static class Bio_JobDefOf
    {
        public static JobDef CarryToBioReactor;

        public static JobDef EnterBioReactor;
    }

    [StaticConstructorOnStartup]
    internal static class BioReactorPatches
    {
        static BioReactorPatches()
        {
            HarmonyInstance harmonyInstance = HarmonyInstance.Create("com.BioReactor.rimworld.mod");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap)), HarmonyPatch("AddHumanlikeOrders")]
    internal class FloatMenuMakerMapPatches
    {
        [HarmonyPrefix]
        static bool Prefix_AddHumanlikeOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                foreach (LocalTargetInfo localTargetInfo3 in GenUI.TargetsAt(clickPos, TargetingParameters.ForRescue(pawn), true))
                {
                    LocalTargetInfo localTargetInfo4 = localTargetInfo3;
                    Pawn victim = (Pawn)localTargetInfo4.Thing;
                    if (victim.Downed && pawn.CanReserveAndReach(victim, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true) && Building_BioReactor.FindBioReactorFor(victim, pawn, true) != null)
                    {
                        string text4 = "CarryToBioReactor".Translate(localTargetInfo4.Thing.LabelCap, localTargetInfo4.Thing);
                        JobDef jDef = Bio_JobDefOf.CarryToBioReactor;
                        Action action3 = delegate ()
                        {
                            Building_BioReactor building_BioReactor = Building_BioReactor.FindBioReactorFor(victim, pawn, false);
                            if (building_BioReactor == null)
                            {
                                building_BioReactor = Building_BioReactor.FindBioReactorFor(victim, pawn, true);
                            }
                            if (building_BioReactor == null)
                            {
                                Messages.Message("CannotCarryToBioReactor".Translate() + ": " + "NoBioReactor".Translate(), victim, MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            Job job = new Job(jDef, victim, building_BioReactor);
                            job.count = 1;
                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        };
                        string label = text4;
                        Action action2 = action3;
                        Pawn revalidateClickTarget = victim;
                        opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action2, MenuOptionPriority.Default, null, revalidateClickTarget, 0f, null, null), pawn, victim, "ReservedBy"));
                    }
                }
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(ThingOwnerUtility))]
    [HarmonyPatch("ContentsSuspended")]
    internal class ThingOwnerUtilityPatches
    {
        [HarmonyPrefix]
        public static bool Prefix_ContentsSuspended(ref bool __result, IThingHolder holder)
        {
            while (holder != null)
            {
                if (holder is Building_BioReactor || holder is Building_CryptosleepCasket || holder is ImportantPawnComp)
                {
                    __result = true;
                    return false;
                }
                holder = holder.ParentHolder;
            }
            __result = false;
            return false;
        }
    }

    [DefOf]
    public static class BioReactorSoundDef
    {
        public static SoundDef Drowning;
    }
    [DefOf]
    public static class BioReactorThoughtDef
    {
        public static ThoughtDef KnowHistolysisHumanlike;

        public static ThoughtDef LivingBattery;
    }
}