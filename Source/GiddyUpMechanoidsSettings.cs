
using Verse;

namespace GiddyUpMechanoids
{
    public class GiddyUpMechanoidsModSettings : ModSettings
    {
        public int mountChance = 40;
        public string mountChanceBuffer;
        //public float bodySizeFilter = 1.01f;
        public bool disregardCarryingCapacity = false;

        public MechSelectionDict mechSelector = new();

        public void ClearBuffers()
        {

            mountChanceBuffer = "";
           
        }


        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref mountChance, "mountChance", 40);
            //Scribe_Values.Look(ref bodySizeFilter, "bodySizeFilter", 1.01f);
            Scribe_Values.Look(ref disregardCarryingCapacity, "disregardCarryingCapacity", false);

            Scribe_Deep.Look(ref mechSelector, "mechSelector");

            if (mechSelector == null)
                mechSelector = new MechSelectionDict();
        }
    }

}
