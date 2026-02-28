using HarmonyLib;
using Verse;


namespace GiddyUpMechanoids
{
    [HarmonyPatch(typeof(GiddyUp.IsMountableUtility), "IsTooHeavy")]
    public static class Patch_IsTooHeavy
    {
        private const string LOG_PREFIX = "[GiddyUpMechanoids] ";

        public static bool Prefix(Pawn rider, Pawn animal, ref bool __result)
        {
            if (rider == null || animal == null)
                return true; // run original

            // If not a mech, let vanilla logic handle it
            if (!animal.RaceProps.IsMechanoid)
            {
                return true; // run original
            }

            // It is a mech
            if (GiddyUpMechanoidsMod.Settings.disregardCarryingCapacity)
            {
                Log.Message(LOG_PREFIX +
                    $"Bypassing carrying capacity check for mech {animal.LabelShort} and rider {rider.LabelShort}");

                __result = false; // NOT too heavy
                return false;     // skip original
            }

            Log.Message(LOG_PREFIX +
                    $"Carrying capacity check for mech {animal.LabelShort} and rider {rider.LabelShort}");

            // Otherwise, use original logic
            return true;
        }
    }
}
