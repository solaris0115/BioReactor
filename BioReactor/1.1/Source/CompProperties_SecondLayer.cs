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