using RimWorld;
using Verse;
using UnityEngine;

namespace BioReactor
{
    internal class CompSecondLayer : ThingComp
    {
        private Graphic graphicInt;
        public Vector3 offset;

        public virtual Graphic Graphic
        {
            get
            {
                if (graphicInt == null)
                {
                    if (Props.graphicData == null)
                    {
                        Log.ErrorOnce(this.parent.def + "BioReactor - has no SecondLayer graphicData but we are trying to access it.", 764532, false);
                        return BaseContent.BadGraphic;
                    }
                    graphicInt = Props.graphicData.GraphicColoredFor(this.parent);
                    offset = Props.offset;
                }
                return graphicInt;
            }
        }

        public CompProperties_SecondLayer Props
        {
            get
            {
                return (CompProperties_SecondLayer)this.props;
            }
        }
        
        public override void PostDraw()
        {
            if (parent.Rotation == Rot4.South)
            {
                Graphic.Draw(GenThing.TrueCenter(this.parent.Position, this.parent.Rotation, this.parent.def.size, Props.Altitude) + offset, this.parent.Rotation, this.parent, 0f);
                return;
            }
        }
    }

}