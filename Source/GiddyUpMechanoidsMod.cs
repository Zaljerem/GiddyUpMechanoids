using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GiddyUpMechanoids
{
    public class GiddyUpMechanoidsMod : Mod
    {
        public static GiddyUpMechanoidsModSettings Settings;

        public static GiddyUpMechanoidsMod Instance;        

        private Vector2 scrollPos = Vector2.zero;
        
        public GiddyUpMechanoidsMod(ModContentPack content) : base(content)
        {
            Instance = this;
          
            Settings = GetSettings<GiddyUpMechanoidsModSettings>();
           
            //new Harmony("zal.giddyupmechanoids").PatchAll();

            // Initialize after defs loaded
            LongEventHandler.ExecuteWhenFinished(DefsLoaded);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);


            listing.TextFieldNumericLabeledWithTooltip(
           "GU_BME_MountChance_Title".Translate(),
           "GU_BME_MountChance_Description".Translate(),
           ref Settings.mountChance,
           ref Settings.mountChanceBuffer,
           1,
           100);
           
            listing.Gap();

            listing.CheckboxLabeled("GUM_DisCarCap".Translate(), ref GiddyUpMechanoidsMod.Settings.disregardCarryingCapacity, "GUM_DisCarCapText".Translate());
         
            //listing.Label("Body Size Filter: " + Settings.bodySizeFilter.ToString("0.00"));
            //Settings.bodySizeFilter = listing.Slider(Settings.bodySizeFilter, 0f, 5f);
            listing.GapLine();

            listing.Label("GUM_AllowedMechs".Translate());

            listing.End();

            // Scrollable mech list
            Rect outRect = new(inRect.x, inRect.y + 120, inRect.width, inRect.height - 120);
            Rect viewRect = new(0, 0, inRect.width - 20, GetMechDefs().Count * 30f);

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            float y = 0f;
            foreach (var mech in GetMechDefs())
            {
                var rect = new Rect(0, y, viewRect.width, 30f);

                bool selected = Settings.mechSelector.values[mech.defName].isSelected;

                Widgets.CheckboxLabeled(rect,
                    label: mech.label.CapitalizeFirst(),
                    checkOn: ref selected);

                Settings.mechSelector.values[mech.defName].isSelected = selected;

                y += 30f;
            }

            Widgets.EndScrollView();
        }


        public override string SettingsCategory() => "GUM_ModName".Translate();

        /// <summary>        
        /// Mirrors the old ModBase.DefsLoaded entrypoint.
        /// </summary>
         private void DefsLoaded()
         {

            new Harmony("zal.giddyupmechanoids").PatchAll();

            List<ThingDef> mechDefs = GetMechDefs();
        
        // Initialize dictionary entries if missing
            foreach (var mech in mechDefs)
             {
                if (!Settings.mechSelector.values.ContainsKey(mech.defName))
                {
                    Settings.mechSelector.values.Add(
                       mech.defName,
                       new MechRecord { isSelected = true }
                   );
               }
           }
         }
               


        private static List<ThingDef> GetMechDefs()
        {
            Predicate<ThingDef> isLiveMech = td => td.race != null
                                                   && td.race.IsMechanoid
                                                   && !td.IsCorpse; 

            return DefDatabase<ThingDef>.AllDefs
                .Where(td => isLiveMech(td))
                .ToList();
        }


        public static bool IsAllowedInModOptions(string defName)
        {
            if (GiddyUpMechanoidsMod.Settings.mechSelector.values
                .TryGetValue(defName, out MechRecord rec))
            {
                return rec.isSelected;
            }

            return false;
        }

    }
}
