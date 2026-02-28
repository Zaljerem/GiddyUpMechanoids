using System.Collections.Generic;
using Verse;

namespace GiddyUpMechanoids
{
    public class MechSelectionDict : IExposable
    {
        public Dictionary<string, MechRecord> values = new();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref values, "values",
                LookMode.Value, LookMode.Deep);
        }
    }

}
