using GiddyUp;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static GiddyUp.IsMountableUtility;

namespace GiddyUpMechanoids
{
    public static class Utility
    {
        private static readonly HashSet<JobDef> busyJobs = new()
        {
            ResourceBank.JobDefOf.Mounted,
            JobDefOf.LayEgg,
            JobDefOf.Nuzzle,
            JobDefOf.Lovin,
            JobDefOf.Vomit,
            JobDefOf.Wait_Downed
        };

        #region Reflection Helpers

        private static MethodInfo getGUDataMethod;
        private static readonly Type MountUtilityType = AccessTools.TypeByName("MountUtility");

        public static ExtendedPawnData GetGUData_Reflect(Pawn pawn)
        {
            if (pawn == null) return null;

            if (getGUDataMethod == null)
            {
                var storageUtilType = AccessTools.TypeByName("GiddyUp.StorageUtility");
                if (storageUtilType == null)
                {
                    Log.Error("[GiddyUpMechanoids] Could not find GiddyUp.StorageUtility");
                    return null;
                }

                getGUDataMethod = AccessTools.Method(storageUtilType, "GetGUData", new[] { typeof(Pawn) });
                if (getGUDataMethod == null)
                {
                    Log.Error("[GiddyUpMechanoids] Could not reflect GetGUData(Pawn)");
                    return null;
                }
            }

            return getGUDataMethod.Invoke(null, new object[] { pawn }) as ExtendedPawnData;
        }
                

        #endregion

        #region Mounting Checks

        public static bool IsMountableMech(this Pawn mech, out Reason reason, Pawn rider, bool checkState = true, bool checkFaction = false)
        {
            reason = Reason.CanMount;

            if (checkFaction && mech.Faction != rider.Faction)
            {
                reason = Reason.WrongFaction;
                return false;
            }

            if (checkState)
            {
                if (busyJobs.Contains(mech.CurJobDef) && mech.CurJobDef != ResourceBank.JobDefOf.Mounted)
                {
                    reason = Reason.IsBusy;
                    return false;
                }

                var mechData = GetGUData_Reflect(mech);
                if (mechData.reservedBy != null && (mechData.reservedBy.CurJobDef != ResourceBank.JobDefOf.Mount || GetGUData_Reflect(mechData.reservedBy)?.reservedMount != mech))
                {
                    reason = Reason.MountedByAnother;
                    return false;
                }

                if (mech.roping?.IsRopedByPawn == true)
                {
                    reason = Reason.IsRoped;
                    return false;
                }

                var lord = mech.GetLord();
                if (lord != null && lord.LordJob is LordJob_FormAndSendCaravan && mechData.reservedBy != rider)
                {
                    reason = Reason.IsBusyWithCaravan;
                    return false;
                }

                if (mech.Dead || mech.Downed || mech.InMentalState || !mech.Spawned
                    || (mech.health != null && mech.health.summaryHealth.SummaryHealthPercent < ModSettings_GiddyUp.injuredThreshold)
                    || mech.health.HasHediffsNeedingTend()
                    || mech.HasAttachment(ThingDefOf.Fire))
                {
                    reason = Reason.IsPoorCondition;
                    return false;
                }
            }

            if (rider != null && rider.IsTooHeavy(mech))
            //if (rider != null && Utility.IsTooHeavy(rider, mech))
            {
                reason = Reason.TooHeavy;
                return false;
            }

            return true;
        }

        #endregion

        #region FloatMenu

        public static bool AddMountingOptionsMech(Pawn mech, Pawn rider, List<FloatMenuOption> opts)
        {
            if (mech == null || rider == null || opts == null) return false;

            var riderData = GetGUData_Reflect(rider);
            var mechData = GetGUData_Reflect(mech);

            if (riderData == null || mechData == null) return false;

            // Already mounted → provide dismount option
            if (mech == riderData.mount)
            {
                return opts.GenerateFloatMenuOption("GUC_Dismount".Translate(), true, () =>
                {
                    DismountMech(rider, mech, riderData);
                });
            }

            if (!rider.IsCapableOfRiding(out var riderReason))
            {
                return opts.GenerateFloatMenuOption("GUC_RiderCannotMount".Translate(), true, () => { });
            }

            bool isMountable = mech.IsMountableMech(out var mechReason, rider, true, true);

            if (!isMountable)
            {
                string reasonText = mechReason switch
                {
                    Reason.IsBusy => "GUC_AnimalBusy".Translate(),
                    Reason.IsRoped => "GUC_IsRoped".Translate(),
                    Reason.IsPoorCondition => "GUC_IsPoorCondition".Translate(),
                    Reason.TooHeavy => "GUC_TooHeavy".Translate(),
                    _ => "GUC_CannotMount".Translate()
                };

                return opts.GenerateFloatMenuOption(reasonText, true, () => { });
            }

            string menuText = riderData.mount == null ? "GUC_Mount".Translate() : "GUC_SwitchMount".Translate();

            return opts.GenerateFloatMenuOption(menuText, true, () =>
            {
                // Dismount old mount if present
                if (riderData.mount != null)
                {
                    DismountMech(rider, riderData.mount, riderData);
                }

                // Force mount new mech
                ForceMountMech(rider, mech, riderData);
            });
        }

        private static bool GenerateFloatMenuOption(this List<FloatMenuOption> list, string text, bool prefixType = false, Action action = null)
        {
            if (!prefixType) text = "GUC_CannotMount".Translate() + ": " + text;
            if (action == null) action = () => { }; // safe no-op
            list.Add(new FloatMenuOption(text, action, MenuOptionPriority.Low));
            return true;
        }


        #endregion

        #region Mount Logic


        public static void ForceMountMech(Pawn rider, Pawn mech, ExtendedPawnData riderData)
        {
            if (rider == null || mech == null || riderData == null)
                return;

            if (rider.Map != mech.Map)
                return;

            var mechData = GetGUData_Reflect(mech);
            if (mechData == null)
            {
                Log.Error("[GiddyUpMechanoids] mechData null");
                return;
            }

            // ------------------------------------------------------------
            // 1. Break ropes (GU does this first)
            // ------------------------------------------------------------
            var rope = mech.roping;
            if (rope != null && rope.IsRoped)
                rope.BreakAllRopes();

            // ------------------------------------------------------------
            // 2. Reserve relationship (like GU core)
            // ------------------------------------------------------------
            riderData.reservedMount = mech;
            mechData.reservedBy = rider;

            // ------------------------------------------------------------
            // 3. Stop mech movement only (do NOT give Mounted job)
            // ------------------------------------------------------------
            mech.pather?.StopDead();

            // DO NOT StopAll() unless absolutely necessary.
            // That can break GU's internal expectations.

            // ------------------------------------------------------------
            // 4. Inject Mount job to rider (same pattern as GoMount Inject)
            // ------------------------------------------------------------
            Job mountJob = new Job(ResourceBank.JobDefOf.Mount, mech)
            {
                count = 1
            };

            rider.jobs?.StartJob(
                mountJob,
                JobCondition.InterruptOptional,
                resumeCurJobAfterwards: false,
                cancelBusyStances: false,
                keepCarryingThingOverride: true
            );

            // ------------------------------------------------------------
            // IMPORTANT:
            // Do NOT:
            //   - Set riderData.mount
            //   - Issue Mounted job to mech
            //   - Update ExtendedDataStorage.isMounted
            //
            // GU handles all of that inside Mount()
            // ------------------------------------------------------------
        }




        public static void DismountMech(Pawn rider, Pawn mech, ExtendedPawnData riderData)
        {
            if (rider == null || mech == null || riderData == null)
                return;

            //Log.Message($"[Giddy-Up] DismountMech invoked: {rider} → {mech}");
            //Log.Message($"[Giddy-Up] MissingJoy BEFORE dismount: {CountPawnsMissingJoy()}");

            var mechData = GetGUData_Reflect(mech);

            // ------------------------------------------------------------------
            // 1. Authoritative unmount state changes (mirrors Giddy-Up Dismount)
            // ------------------------------------------------------------------
            riderData.mount = null;
            if (mechData != null)
                mechData.mount = null;

            // Reservation pair reset
            riderData.reservedMount = null;
            if (mechData != null)
                mechData.reservedBy = null;

            // Remove from isMounted list exactly as Giddy-Up does
            try
            {
                var storageType = AccessTools.TypeByName("ExtendedDataStorage");
                if (storageType != null)
                {
                    var worldComp = Find.World.GetComponent(storageType);
                    var isMountedField = AccessTools.Field(storageType, "isMounted");
                    if (worldComp != null && isMountedField != null)
                    {
                        var list = isMountedField.GetValue(worldComp) as IList<int>;
                        list?.Remove(rider.thingIDNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[GiddyUpMechanoids] Non-fatal ExtendedDataStorage.isMounted removal failure: " + ex);
            }

            // ------------------------------------------------------------------
            // 2. Reset mech tweener + pathing (Giddy-Up uses EXACT lines below)
            // ------------------------------------------------------------------
            mech.Drawer.tweener = new PawnTweener(mech);
            mech.pather.ResetToCurrentPosition();

            // Duty re-focus for NPC mechs
            if (!rider.Faction.def.isPlayer && mech.mindState?.duty != null)
            {
                mech.mindState.duty.focus = new LocalTargetInfo(mech.Position);
            }

            // ------------------------------------------------------------------
            // 3. Clean up queued jobs (Mounted / Wait)
            // ------------------------------------------------------------------
            if (mech.jobs?.jobQueue != null)
            {
                for (int i = mech.jobs.jobQueue.Count - 1; i >= 0; i--)
                {
                    var elem = mech.jobs.jobQueue[i];
                    if (elem?.job != null &&
                        (elem.job.def == ResourceBank.JobDefOf.Mounted ||
                         elem.job.def == JobDefOf.Wait))
                    {
                        mech.jobs.jobQueue.Extract(elem.job);
                    }
                }
            }

            // End any active Mounted/Wait job
            if (mech.jobs?.curJob != null &&
                (mech.jobs.curJob.def == ResourceBank.JobDefOf.Mounted ||
                 mech.jobs.curJob.def == JobDefOf.Wait))
            {
                mech.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            }

            // Give mech a sane idle job
            mech.jobs?.StartJob(
                JobMaker.MakeJob(JobDefOf.Wait, mech.Position),
                JobCondition.InterruptOptional);

            // ------------------------------------------------------------------
            // 4. End rider's Mount job if still running
            // ------------------------------------------------------------------
            if (rider.jobs?.curJob != null &&
                rider.jobs.curJob.def == ResourceBank.JobDefOf.Mount &&
                rider.jobs.curJob.targetA == mech)
            {
                rider.jobs.EndCurrentJob(JobCondition.Succeeded, true);
            }

            // ------------------------------------------------------------------
            // 5. (Optional) WaitForRider logic if you want to emulate GU
            // ------------------------------------------------------------------
            // Disabled by default (mechs probably shouldn't do this)
            // Uncomment if needed:
            /*
            if (ModSettings_GiddyUp.rideAndRollEnabled &&
                !rider.Drafted &&
                rider.Faction.def.isPlayer &&
                mechData?.reservedBy != null &&
                waitForRider)
            {
                mech.jobs.jobQueue.EnqueueFirst(
                    new Job(ResourceBank.JobDefOf.WaitForRider, mechData.reservedBy)
                    {
                        expiryInterval = ModSettings_GiddyUp.waitForRiderTimer,
                        checkOverrideOnExpire = true,
                        followRadius = 8f,
                        locomotionUrgency = LocomotionUrgency.Walk
                    });
            }
            */

            //Log.Message($"[Giddy-Up] Dismount complete; MissingJoy AFTER dismount: {CountPawnsMissingJoy()}");
        }


        #endregion


        public static bool IsTooHeavy(Pawn rider, Pawn mech)
        {
            if (GiddyUpMechanoidsMod.Settings.disregardCarryingCapacity)
            {
                return false;
            }
            return rider.GetStatValue(StatDefOf.Mass) > mech.GetStatValue(StatDefOf.CarryingCapacity);
        }

    }
}
