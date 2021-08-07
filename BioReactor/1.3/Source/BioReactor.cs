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
    public class Building_BioReactor : Building_Casket,ISuspendableThingHolder
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
                if (!myPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
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
            PawnRenderFlags drawFlags = PawnRenderFlags.Cache;
            drawFlags |= PawnRenderFlags.Clothes;
            drawFlags |= PawnRenderFlags.Headgear;
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
                            DrawInnerThing(pawn, DrawPos + innerDrawOffset, drawFlags);
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
                            DrawInnerThing(pawn, DrawPos + innerDrawOffset, drawFlags);
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
                            DrawInnerThing(pawn, DrawPos + innerDrawOffset, drawFlags);
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
                typeof(RotDrawMode),
                typeof(PawnRenderFlags)
            });
        public virtual void DrawInnerThing(Pawn pawn, Vector3 rootLoc, PawnRenderFlags renderFlags)
        {
            pawnrender.Invoke(pawn.Drawer.renderer, new object[]
                    {
                        rootLoc,
                        0,
                        true,
                        Rot4.South,
                        RotDrawMode.Fresh,
                        renderFlags
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

        bool ISuspendableThingHolder.IsContentsSuspended
        {
            get
            {
                return true;
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

}