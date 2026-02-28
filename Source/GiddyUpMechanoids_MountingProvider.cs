using RimWorld;
using System.Collections.Generic;
using Verse;
using WhatTheHack;

namespace GiddyUpMechanoids
{
    public class GiddyUpMechanoids_MountingProvider : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => false;
        protected override bool Multiselect => false;
        protected override bool CanSelfTarget => false;
        protected override bool RequiresManipulation => true;
        protected override bool MechanoidCanDo => true;



        public override bool TargetPawnValid(Pawn targetPawn, FloatMenuContext context)
        {
            if (!base.TargetPawnValid(targetPawn, context))
                 return false;

            if (!targetPawn.IsMechanoid())
                return false;

            if (!targetPawn.IsHacked())
                return false;

            //Log.Message("TargetPawnValid");
            return true;
          
        }

        public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
        {
            //Log.Message("[GiddyUp-Mechs] GetOptions fired. ClickedCell=" + context.ClickedCell);

            Pawn pawn = context.FirstSelectedPawn;
            if (pawn == null)
            {
                //Log.Message("[GiddyUp-Mechs] ERROR: FirstSelectedPawn is NULL.");
                yield break;
            }

            //Log.Message("[GiddyUp-Mechs] FirstSelectedPawn = " + pawn + " (Drafted=" + pawn.Drafted + ")");

            IEnumerable<LocalTargetInfo> targets = GenUI.TargetsAt(
                context.ClickedCell.ToVector3Shifted(),
                TargetingParameters.ForAttackAny(),
                true
            );

            //Log.Message("[GiddyUp-Mechs] Searching targets at cell. targets.Count()=" + targets.Count());

            foreach (var t in targets)
            {
                if (t.Thing is not Pawn target)
                {
                    //Log.Message("[GiddyUp-Mechs] Skipping target because it is not a Pawn: " + t.Thing);
                    continue;
                }

                //Log.Message("[GiddyUp-Mechs] Found target Pawn: " + target);

                // 1 — Is mechanoid?
                if (!target.IsMechanoid())
                {
                    //Log.Message("[GiddyUp-Mechs] Reject target — not a mechanoid.");
                    continue;
                }

                // 2 — Hacked?
                if (!target.IsHacked())
                {
                    //Log.Message("[GiddyUp-Mechs] Reject target — mech is NOT hacked.");
                    continue;
                }

                //Log.Message("[GiddyUp-Mechs] Target pawn IS a hacked mechanoid.");               

                // 3 — Mounted turret?
                bool hasMountedTurret = target.health.hediffSet.HasHediff(WTH_DefOf.WTH_MountedTurret);
                //Log.Message("[GiddyUp-Mechs] Target has MountedTurret hediff? " + hasMountedTurret);

                if (hasMountedTurret)
                {
                    //Log.Message("[GiddyUp-Mechs] Reject — mech has a mounted turret.");
                    yield return new FloatMenuOption("GU_BME_Reason_Turret".Translate(), null);
                    yield break;
                }

                // 4 — Has GiddyUp module?
                bool hasGiddyUpModule = target.health.hediffSet.HasHediff(GU_Mech_DefOf.GU_Mech_GiddyUpModule);
                //Log.Message("[GiddyUp-Mechs] Target has GiddyUp module? " + hasGiddyUpModule);

                if (!hasGiddyUpModule)
                {
                    //Log.Message("[GiddyUp-Mechs] Reject — mech lacks GiddyUpModule.");
                    yield return new FloatMenuOption("GU_BME_Reason_NoModule".Translate(), null);
                    yield break;
                }

                // 5 — Check "mountable in mod options" for the target mech
                bool allowedMount = GiddyUpMechanoidsMod.IsAllowedInModOptions(target.def.defName);
                //Log.Message("[GiddyUp-Mechs] Target mech allowed in mod options? " + allowedMount);

                if (!allowedMount)
                {
                    //Log.Message("[GiddyUp-Mechs] Reject — mech not allowed in mod options.");
                    yield return new FloatMenuOption("GUC_NotInModOptions".Translate(), null);
                    yield break;
                }

                // 6 — Activated?
                bool activated = target.IsActivated();
                //Log.Message("[GiddyUp-Mechs] Target mech IsActivated? " + activated);

                if (!activated)
                {

                    //Log.Message("[GiddyUp-Mechs] Reject — mech is NOT activated.");
                    yield return new FloatMenuOption("GU_BME_Reason_NotActivated".Translate(), null);
                    yield break;
                }

                //Log.Message("[GiddyUp-Mechs] All preliminary conditions PASSED. Attempting to add mount options.");

                // 7 — Build float menu options via our utility
                var list = new List<FloatMenuOption>();

                try
                {
                    //Log.Message("[GiddyUp-Mechs] Calling Utility.AddMountingOptionsMech...");
                    Utility.AddMountingOptionsMech(target, pawn, list);
                    //Log.Message("[GiddyUp-Mechs] Utility.AddMountingOptionsMech returned list count = " + list.Count);
                }
                catch (System.Exception ex)
                {
                    Log.Error("[GiddyUp-Mechs] ERROR inside AddMountingOptionsMech: " + ex);
                }

                // 8 — Yield results
                if (list.Count == 0)
                {
                    //Log.Message("[GiddyUp-Mechs] WARNING — AddMountingOptionsMech produced ZERO menu options.");
                }

                foreach (var o in list)
                {
                    //Log.Message("[GiddyUp-Mechs] Yielding FloatMenuOption: " + o.Label);
                    yield return o;
                }
            }
        }

    }
}
