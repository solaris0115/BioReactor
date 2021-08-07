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
    [DefOf]
    public static class Bio_JobDefOf
    {
        public static JobDef CarryToBioReactor;
        public static JobDef EnterBioReactor;
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

    [DefOf]
    public static class CustomDefOf
    {
        public static WorkGiverDef CustomWorkRefuel;
    }

}