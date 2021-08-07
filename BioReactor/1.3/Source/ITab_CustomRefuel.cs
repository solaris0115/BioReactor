using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.UIElements;
using Verse.AI;
using Verse;


namespace BioReactor
{
    public class ITab_CustomRefuel : ITab
    {
        private const float TopAreaHeight = 35f;

        private Vector2 scrollPosition = default(Vector2);

        private static readonly Vector2 WinSize = new Vector2(300f, 480f);

        private ThingFilterUI.UIState thingFilterState = new ThingFilterUI.UIState();

        public override void OnOpen()
        {
            base.OnOpen();
            this.thingFilterState.quickSearch.Reset();
        }

        private IStoreSettingsParent SelStoreSettingsParent
        {
            get
            {
                return ((ThingWithComps)SelObject).GetComp<CompBioRefuelable>();
            }
        }

        public override bool IsVisible
        {
            get
            {
                return SelStoreSettingsParent.StorageTabVisible;
            }
        }

        public ITab_CustomRefuel()
        {
            size = WinSize;
            labelKey = Translator.Translate("RefuelTab");
        }

        protected override void FillTab()
        {
            IStoreSettingsParent selStoreSettingsParent = SelStoreSettingsParent;
            StorageSettings storeSettings = selStoreSettingsParent.GetStoreSettings();
            Rect rect = GenUI.ContractedBy(new Rect(0f, 0f, WinSize.x, WinSize.y), 10f);
            GUI.BeginGroup(rect);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect)
            {
                height = 32f
            }, Translator.Translate("RefuelTitle"));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            ThingFilter thingFilter = null;
            if (selStoreSettingsParent.GetParentStoreSettings() != null)
            {
                thingFilter = selStoreSettingsParent.GetParentStoreSettings().filter;
            }
            Rect rect2 = new Rect(0f, 40f, rect.width, rect.height - 40f);
            ThingFilterUI.DoThingFilterConfigWindow(rect2,thingFilterState, storeSettings.filter, thingFilter, 8, null, null, false, null, null);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.StorageTab, KnowledgeAmount.FrameDisplayed);
            GUI.EndGroup();
        }
        public override void Notify_ClickOutsideWindow()
        {
            base.Notify_ClickOutsideWindow();
            this.thingFilterState.quickSearch.Unfocus();
        }
    }
}
