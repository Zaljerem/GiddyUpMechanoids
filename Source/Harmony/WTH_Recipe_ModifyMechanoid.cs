using HarmonyLib;
using Verse;
using WhatTheHack.Recipes;

namespace GiddyUpMechanoids
{
    [HarmonyPatch(typeof(Recipe_ModifyMechanoid), "CanApplyOn")]
    class WTH_Recipe_ModifyMechanoid_CanApplyOn
    {
        static void Postfix(Recipe_ModifyMechanoid __instance, Pawn pawn, ref string reason, ref bool __result)
        {
            if (__instance.recipe == GU_Mech_DefOf.GU_Mech_InstallGiddyUpModule && !GiddyUpMechanoidsMod.IsAllowedInModOptions(pawn.def.defName))
            {
                reason = "GU_BME_Reason_NotAllowed".Translate();
                __result = false;
            }
        }
    }
    [HarmonyPatch(typeof(Recipe_ModifyMechanoid), "IsValidPawn")]
    class WTH_Recipe_ModifyMechanoid_IsValidPawn
    {
        static void Postfix(Recipe_ModifyMechanoid __instance, Pawn pawn, ref bool __result)
        {
            if (!__result)
            {
                Log.Message("IsValidPawn is false");
            }
        }
    }
}
