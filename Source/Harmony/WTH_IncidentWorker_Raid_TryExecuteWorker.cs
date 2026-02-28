using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;
using WhatTheHack;
using GiddyUp;

namespace GiddyUpMechanoids
{
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_Raid_PostProcessSpawned
    {
        private const string LOG_POSTFIX = "[GiddyUpMechanoids] ";

        public static void Postfix(IncidentWorker_Raid __instance, IncidentParms parms, List<Pawn> pawns)
        {
            Log.Message(LOG_POSTFIX + "Postfix triggered.");

            if (parms == null)
            {
                Log.Warning(LOG_POSTFIX + "IncidentParms is NULL.");
                return;
            }

            PawnsArrivalModeDef mode = parms.raidArrivalMode;
            Log.Message(LOG_POSTFIX + $"RaidArrivalMode: {mode}");

            if (mode == PawnsArrivalModeDefOf.EdgeWalkIn
    || mode == PawnsArrivalModeDefOf.EdgeWalkInGroups
    || mode == PawnsArrivalModeDefOf.EdgeWalkInDarkness)
            {
                // valid mode
            }
            else
            {
                Log.Message(LOG_POSTFIX + "RaidArrivalMode is not EdgeWalkIn, EdgeWalkInGroups or EdgeWalkInDarkness. Exiting.");
                return;
            }

            if (pawns == null || pawns.Count == 0)
            {
                Log.Warning(LOG_POSTFIX + "Pawns list is null or empty.");
                return;
            }

            Log.Message(LOG_POSTFIX + $"Total pawns arrived: {pawns.Count}");

            var storage = Find.World?.GetComponent<ExtendedDataStorage>();

            if (storage == null)
            {
                Log.Warning(LOG_POSTFIX + "ExtendedDataStorage is NULL.");
                return;
            }

            var mechs = pawns.Where(p => p.IsHacked()).ToList();
            Log.Message(LOG_POSTFIX + $"Hacked mechs found: {mechs.Count}");

            var humanlikes = pawns
                .Where(h =>
                {
                    var pdata = Utility.GetGUData_Reflect(h);
                    bool hasMount = pdata?.mount != null;

                    Log.Message(LOG_POSTFIX +
                        $"Checking humanlike: {h.LabelShort} | Humanlike={h.RaceProps.Humanlike} | AlreadyMounted={hasMount}");

                    return h.RaceProps.Humanlike && !hasMount;
                })
                .ToList();

            Log.Message(LOG_POSTFIX + $"Eligible humanlike riders: {humanlikes.Count}");

            foreach (var mech in mechs)
            {
                Log.Message(LOG_POSTFIX + $"Evaluating mech: {mech.LabelShort}");

                float chance = GiddyUpMechanoidsMod.Settings.mountChance / 100f;
                bool roll = Rand.Chance(chance);
                bool allowed = GiddyUpMechanoidsMod.IsAllowedInModOptions(mech.def.defName);               

                Log.Message(LOG_POSTFIX +
                    $"Mount roll={roll} (chance={chance}) | AllowedInSettings={allowed}");

                if (roll && allowed)
                {
                    if (humanlikes.Count > 0)
                    {
                        var rider = humanlikes[0];
                        humanlikes.RemoveAt(0);

                        Log.Message(LOG_POSTFIX +
                            $"Assigning rider {rider.LabelShort} to mech {mech.LabelShort}");

                        AssignRider(mech, rider);
                    }
                    else
                    {
                        Log.Message(LOG_POSTFIX + "No eligible humanlikes remaining.");
                    }
                }
            }
        }

        private static void AssignRider(Pawn mech, Pawn rider)
        {
            if (mech == null || rider == null)
            {
                Log.Warning(LOG_POSTFIX + "AssignRider received null pawn.");
                return;
            }

            Log.Message(LOG_POSTFIX + $"AssignRider start: {rider.LabelShort} -> {mech.LabelShort}");

            var riderData = Utility.GetGUData_Reflect(rider);
            var mechData = Utility.GetGUData_Reflect(mech);

            if (riderData == null || mechData == null)
            {
                Log.Warning(LOG_POSTFIX + "Failed to retrieve GiddyUp ExtendedPawnData via reflection.");
                return;
            }

            // Safety checks
            if (riderData.mount != null)
            {
                Log.Message(LOG_POSTFIX + $"{rider.LabelShort} already mounted. Skipping.");
                return;
            }

            if (mechData.reservedBy != null)
            {
                Log.Message(LOG_POSTFIX + $"{mech.LabelShort} already reserved. Skipping.");
                return;
            }

            // Link both sides
            riderData.mount = mech;
            riderData.reservedMount = mech;
            mechData.reservedBy = rider;

            Log.Message(LOG_POSTFIX + "Linked ExtendedPawnData mount fields.");

            if (!mech.health.hediffSet.HasHediff(GU_Mech_DefOf.GU_Mech_GiddyUpModule))
            {
                mech.health.AddHediff(GU_Mech_DefOf.GU_Mech_GiddyUpModule);
                Log.Message(LOG_POSTFIX + "Added GiddyUp module hediff.");
            }

            //Utility.ForceMountMech(rider, mech, riderData);

            //var job = new Job(GU_Mech_DefOf.Mounted, rider) { count = 1 };
            //mech.jobs?.StartJob(job, JobCondition.InterruptForced);


            // ------------------------------------------------------------------
            // Create & issue Mounted job to MECH
            // ------------------------------------------------------------------
            Job mountedJob = JobMaker.MakeJob(ResourceBank.JobDefOf.Mounted, rider);
            mountedJob.count = 1;

            bool mechAccepted = mech.jobs?.TryTakeOrderedJob(mountedJob, JobTag.Misc) ?? false;
            if (!mechAccepted)
            {
                // Fallback: force start
                mech.jobs?.StartJob(mountedJob, JobCondition.InterruptForced, cancelBusyStances: true);
            }

            // ------------------------------------------------------------------
            // Create & issue Mount job to RIDER
            // ------------------------------------------------------------------
            Job mountJob = JobMaker.MakeJob(ResourceBank.JobDefOf.Mount, mech);
            mountJob.count = 1;

            rider.jobs?.TryTakeOrderedJob(mountJob, JobTag.Misc);

            // ------------------------------------------------------------------
            // Set mount link NOW (after jobs queued)
            //    - The correct time to set this is right after jobs are issued.
            //    - Required so JobDriver_Mount sees consistent mount state.
            // ------------------------------------------------------------------
            riderData.mount = mech;

            Log.Message(LOG_POSTFIX + "Started Mounted job.");

            //GiddyUp.TextureUtility.SetDrawOffset(rider.ageTracker.CurKindLifeStage);
        }

    }
}
