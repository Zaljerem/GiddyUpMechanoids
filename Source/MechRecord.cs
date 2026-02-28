using Verse;


namespace GiddyUpMechanoids
{
    public class MechRecord : IExposable
    {
        public bool isSelected = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref isSelected, "isSelected", true);
        }
    }
}
