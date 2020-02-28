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
using HarmonyLib;
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

    public class Building_BioReactor : Building_Casket
    {
        /// <summary>
        /// 내부 캐릭터 드로우 좌표. 리액터 실좌표 중심으로 드로우.
        /// </summary>
        public Vector3 innerDrawOffset;
        public Vector3 waterDrawCenter;
        public Vector2 waterDrawSize;
        //static Vector3 waterDrawY = new Vector3(0, 0.3f, 0);
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

        public CompBioRefuelable compRefuelable;
        public CompForbiddable forbiddable;

        public bool IsContainingThingPawn
        {
            get
            {
                if(!HasAnyContents)
                {
                    return false;
                }
                Pawn pawn = ContainedThing as Pawn;
                if(pawn !=null)
                {
                    return true;
                }
                return false;
            }
        }
        public Pawn InnerPawn
        {
            get
            {
                if (!HasAnyContents)
                {
                    return null;
                }
                Pawn pawn = ContainedThing as Pawn;
                if (pawn != null)
                {
                    return pawn;
                }
                return null;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            compRefuelable = GetComp<CompBioRefuelable>();
            forbiddable = GetComp<CompForbiddable>();
            fillpct = 0;
            histolysisPct = 0;
            BioReactorDef reactorDef = def as BioReactorDef;
            if(reactorDef!=null)
            {
                innerDrawOffset = ((BioReactorDef)def).innerDrawOffset;
                waterDrawCenter = ((BioReactorDef)def).waterDrawCenter;
                waterDrawSize = ((BioReactorDef)def).waterDrawSize;
            }
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
            if (innerContainer.Count == 0)
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
            foreach (Gizmo gizmo2 in CopyPasteGizmosFor(compRefuelable.inputSettings))
            {
                yield return gizmo2;
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
            if(Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                BioReactorDef reactorDef = def as BioReactorDef;
                if (reactorDef != null)
                {
                    innerDrawOffset = ((BioReactorDef)def).innerDrawOffset;
                    waterDrawCenter = ((BioReactorDef)def).waterDrawCenter;
                    waterDrawSize = ((BioReactorDef)def).waterDrawSize;
                }
            }
            compRefuelable = GetComp<CompBioRefuelable>();
            forbiddable = GetComp<CompForbiddable>();
        }
        public override void PostMake()
        {
            base.PostMake();
        }
        public virtual void Histolysis()
        {
            if (HasAnyContents)
            {
                Pawn pawn = ContainedThing as Pawn;
                if (pawn != null)
                {
                    pawn.Rotation = Rot4.South;
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
                                p.needs.mood.thoughts.memories.TryGainMemory(BioReactorThoughtDef.KnowHistolysisHumanlikeCannibal, null);
                                p.needs.mood.thoughts.memories.TryGainMemory(BioReactorThoughtDef.KnowHistolysisHumanlikePsychopath, null);
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
                if (building_BioReactor != null && !building_BioReactor.forbiddable.Forbidden)
                {
                    if ((p.BodySize <= ((BioReactorDef)(building_BioReactor.def)).bodySizeMax) && (p.BodySize >= ((BioReactorDef)(building_BioReactor.def)).bodySizeMin))
                    {
                        return building_BioReactor;
                    }
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
            /*
             * 상태별 그래픽 UI 드로우
             * 
             */
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
                            DrawInnerThing(pawn, DrawPos + innerDrawOffset);
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
                            DrawInnerThing(pawn, DrawPos + innerDrawOffset);
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
                            DrawInnerThing(pawn, DrawPos + innerDrawOffset);
                            LiquidDraw(new Color(0.48f + (0.2f * histolysisPct), 1 - (0.7f * histolysisPct), 0.9f - (0.6f * histolysisPct), 0.3f + histolysisPct * 0.55f), 1);
                        }
                    }
                    break;
                case ReactorState.HistolysisEnding:
                    foreach (Thing t in innerContainer)
                    {
                        t.DrawAt(DrawPos + innerDrawOffset);
                        LiquidDraw(new Color(0.7f, 0.2f, 0.2f, 0.4f + (0.45f * histolysisPct)), 1);
                    }
                    break;
                case ReactorState.HistolysisDone:
                    foreach (Thing t in innerContainer)
                    {
                        t.DrawAt(DrawPos + innerDrawOffset);
                        LiquidDraw(new Color(0.7f, 0.3f, 0.3f, 0.4f), 1);
                    }
                    break;
            }
            //Graphic.Draw(GenThing.TrueCenter(Position, Rot4.South, def.size, 11.7f), Rot4.South, this, 0f);
            Comps_PostDraw();
        }
        public override void Print(SectionLayer layer)
        {
            //this.Graphic.Print(layer, this);
            Printer_Plane.PrintPlane(layer, GenThing.TrueCenter(Position, Rot4.South, def.size, 11.7f), Graphic.drawSize, Graphic.MatSingle, 0, false, null, null, 0.01f, 0f);
        }

        public virtual void LiquidDraw(Color color, float fillPct)
        {
            GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
            r.center = DrawPos + waterDrawCenter;
            r.size = waterDrawSize;
            r.fillPercent = fillPct;
            r.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(color, false);
            r.unfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0, 0, 0, 0), false);
            r.margin = 0f;
            Rot4 rotation = Rotation;
            rotation.Rotate(RotationDirection.Clockwise);
            r.rotation = rotation;
            GenDraw.DrawFillableBar(r);
        }
        public static MethodInfo pawnrender = AccessTools.Method(typeof(PawnRenderer), "RenderPawnInternal", new Type[]
            {
                typeof(Vector3),
                typeof(float),
                typeof(bool),
                typeof(Rot4),
                typeof(Rot4),
                typeof(RotDrawMode),
                typeof(bool),
                typeof(bool),
                typeof(bool)
            });
        public virtual void DrawInnerThing(Pawn pawn, Vector3 rootLoc)
        {
            pawnrender.Invoke(pawn.Drawer.renderer, new object[]
                    {
                        rootLoc,
                        0,
                        true,
                        Rot4.South,
                        Rot4.South,
                        RotDrawMode.Fresh,
                        false,
                        false,
                        false
                    });
        }

        public static IEnumerable<Gizmo> CopyPasteGizmosFor(StorageSettings s)
        {
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CopySettings", true),
                defaultLabel = "CommandCopyBioReactorSettingsLabel".Translate(),
                defaultDesc = "CommandCopyBioReactorSettingsDesc".Translate(),
                action = delegate ()
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                    Copy(s);
                },
                hotKey = KeyBindingDefOf.Misc4
            };
            Command_Action command_Action = new Command_Action();
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings", true);
            command_Action.defaultLabel = "CommandPasteBioReactorSettingsLabel".Translate();
            command_Action.defaultDesc = "CommandPasteBioReactorSettingsDesc".Translate();
            command_Action.action = delegate ()
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                PasteInto(s);
            };
            command_Action.hotKey = KeyBindingDefOf.Misc5;
            if (!HasCopiedSettings)
            {
                command_Action.Disable(null);
            }
            yield return command_Action;
            yield break;
        }

        private static StorageSettings clipboard = new StorageSettings();

        private static bool copied = false;

        public static bool HasCopiedSettings
        {
            get
            {
                return copied;
            }
        }

        public static void Copy(StorageSettings s)
        {
            clipboard.CopyFrom(s);
            copied = true;
        }

        public static void PasteInto(StorageSettings s)
        {
            s.CopyFrom(clipboard);
        }
    }

    public class BioReactorDef:ThingDef
    {
        /// <summary>
        /// 캐릭터 드로우 좌표. 리액터의 실좌표 중심을 기준으로 드로우.
        /// </summary>
        public Vector3 innerDrawOffset;
        /// <summary>
        /// 리액터 용액 드로우 중심 좌표. 리액터 실 좌표 중심을 기준으로 드로우
        /// </summary>
        public Vector3 waterDrawCenter;
        /// <summary>
        /// 리액터 용액 그래픽 넓이
        /// </summary>
        public Vector2 waterDrawSize;
        /// <summary>
        /// 수용 생명체 크기 최소 한도
        /// </summary>
        public float bodySizeMin;
        /// <summary>
        /// 수용 생명체 크기 최대 한도
        /// </summary>
        public float bodySizeMax;
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
            Harmony harmonyInstance = new Harmony("com.BioReactor.rimworld.mod");
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
        public static ThoughtDef KnowHistolysisHumanlikeCannibal;
        public static ThoughtDef KnowHistolysisHumanlikePsychopath;

        public static ThoughtDef LivingBattery;
    }
}