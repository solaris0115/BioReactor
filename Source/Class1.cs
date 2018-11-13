using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace BioReactor
{
    public class CompBioPowerPlant : CompPowerPlant
	{
        public Building_Casket building_BioReactor;

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
            building_BioReactor = (Building_Casket)parent; 
		}

		public override void CompTick()
		{
			base.CompTick();
			this.UpdateDesiredPowerOutput();
		}

		public new void UpdateDesiredPowerOutput()
		{
			if ( (building_BioReactor!=null && !building_BioReactor.HasAnyContents) || (this.breakdownableComp != null && this.breakdownableComp.BrokenDown) || (this.refuelableComp != null && !this.refuelableComp.HasFuel) || (this.flickableComp != null && !this.flickableComp.SwitchIsOn) || !base.PowerOn)
			{
				base.PowerOutput = 0f;
			}
			else
			{
				base.PowerOutput = this.DesiredPowerOutput;
			}
		}
    }
    public class CompBioRefuelable : CompRefuelable
    {

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
                this.Graphic.Draw(GenThing.TrueCenter(this.parent.Position, this.parent.Rotation, this.parent.def.size, Props.Altitude)+ offset, this.parent.Rotation, this.parent, 0f);
                return;
            }

        }
    }

    public class Building_BioReactor : Building_CryptosleepCasket
    {
        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (base.TryAcceptThing(thing, allowSpecialEffects))
            {
                if (allowSpecialEffects)
                {
                    SoundDefOf.CryptosleepCasket_Accept.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
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
            yield break;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo c in base.GetGizmos())
            {
                yield return c;
            }
            if (base.Faction == Faction.OfPlayer && this.innerContainer.Count > 0 && this.def.building.isPlayerEjectable)
            {
                Command_Action eject = new Command_Action();
                eject.action = new Action(EjectContents);
                eject.defaultLabel = "CommandPodEject".Translate();
                eject.defaultDesc = "CommandPodEjectDesc".Translate();
                if (this.innerContainer.Count == 0)
                {
                    eject.Disable("CommandPodEjectFailEmpty".Translate());
                }
                eject.hotKey = KeyBindingDefOf.Misc1;
                eject.icon = ContentFinder<Texture2D>.Get("UI/Commands/PodEject", true);
                yield return eject;
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
            base.EjectContents();
        }

        public override void Draw()
        {
            foreach (Pawn t in innerContainer)
            {
                DrawInnerThing(t, DrawPos + new Vector3(0, -0.05f, 0.65f), 1.7f, true, Rot4.South, Rot4.South, RotDrawMode.Fresh, false, false);
                GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
                r.center = this.DrawPos + new Vector3(0, -0.02f, 0.65f);
                r.size = new Vector2(1.6f, 1.2f);
                r.fillPercent = 1;
                r.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color32(123, 255, 233, 75), false);
                r.unfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color32(0, 0, 0, 0), false);
                r.margin = 0.15f;
                Rot4 rotation = Rotation;
                rotation.Rotate(RotationDirection.Clockwise);
                r.rotation = rotation;
                GenDraw.DrawFillableBar(r);
            }
            base.Draw();
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

}
