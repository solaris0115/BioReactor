using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace BioReactor
{
    public class CompMapRefuelable : MapComponent
    {
        public readonly List<CompBioRefuelable> comps = new List<CompBioRefuelable>();
        public CompMapRefuelable(Map map) : base(map)
        {
        }
    }
}
