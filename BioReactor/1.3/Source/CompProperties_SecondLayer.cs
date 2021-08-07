using Verse;
using UnityEngine;

namespace BioReactor
{
    internal class CompProperties_SecondLayer : CompProperties
    {
        public GraphicData graphicData = null;
        public Vector3 offset = new Vector3();

        public AltitudeLayer altitudeLayer = AltitudeLayer.MoteOverhead;

        public CompProperties_SecondLayer()
        {
            compClass = typeof(CompSecondLayer);
        }

        public float Altitude
        {
            get
            {
                return altitudeLayer.AltitudeFor();
            }
        }
    }
}