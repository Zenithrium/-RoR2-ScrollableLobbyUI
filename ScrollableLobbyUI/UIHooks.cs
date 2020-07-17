﻿using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API.Utils;
using RewiredConsts;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ScrollableLobbyUI
{
    internal static class UIHooks
    {
        internal static void LoadoutPanelControllerAwake(On.RoR2.UI.LoadoutPanelController.orig_Awake orig, LoadoutPanelController self)
        {
            orig(self);

            var uiLayerKey = self.GetComponentInParent<UILayerKey>();

            //Disabling buttons navigation if selected right panel,
            //so extremely long rows in loadout will not interfere in artifacts selection
            var loadoutHelper = uiLayerKey.gameObject.AddComponent<ButtonsNavigationController>();
            loadoutHelper.requiredTopLayer = uiLayerKey;
            loadoutHelper.loadoutPanel = self;

            //Adding container on top of LoadoutPanelController
            AddScrollPanel(self.transform, "LoadoutScrollPanel");
            //Adding container on top of SkillPanel
            var skillScrollPanel = AddScrollPanel(self.transform.parent.parent.Find("SkillPanel"), "LoadoutScrollPanel");

            //Adding scrolling with stick for skills overview
            var scrollHelper = skillScrollPanel.AddComponent<GamepadScrollRectHelper>();
            scrollHelper.requiredTopLayer = uiLayerKey;

            GameObject AddScrollPanel(Transform panel, string name)
            {
                var scrollPanel = new GameObject(name);
                scrollPanel.layer = 5;
                scrollPanel.transform.SetParent(panel.transform.parent, false);
                panel.transform.SetParent(scrollPanel.transform, false);

                scrollPanel.AddComponent<MPEventSystemLocator>();

                var scrollPanelMask = scrollPanel.AddComponent<RectMask2D>();

                var scrollPanelRect = scrollPanel.AddComponent<ConstrainedScrollRect>();
                scrollPanelRect.horizontal = false;
                scrollPanelRect.content = panel.GetComponent<RectTransform>();
                scrollPanelRect.scrollSensitivity = 30;
                scrollPanelRect.movementType = ScrollRect.MovementType.Clamped;
                scrollPanelRect.scrollConstraint = ConstrainedScrollRect.Constraint.OnlyScroll;

                //Adding ContentSizeFilter, otherwise childs would have been wrong size
                var panelContentSizeFilter = panel.gameObject.AddComponent<ContentSizeFitter>();
                panelContentSizeFilter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                panelContentSizeFilter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var scrollPanelRectTransform = scrollPanelRect.GetComponent<RectTransform>();
                scrollPanelRectTransform.pivot = new Vector2(0.5F, 1F);
                scrollPanelRectTransform.anchorMin = new Vector2(0, 0);
                scrollPanelRectTransform.anchorMax = new Vector2(1, 1);
                scrollPanelRectTransform.offsetMin = new Vector2(4, 4);
                scrollPanelRectTransform.offsetMax = new Vector2(4, 4);

                panel.GetComponent<RectTransform>().pivot = new Vector2(0.5F, 1);

                //Enabling Image component, so you can scroll from any point in panel
                var panelImage = panel.GetComponent<Image>();
                if (!panelImage)
                {
                    panelImage = panel.gameObject.AddComponent<Image>();
                }
                panelImage.enabled = true;
                panelImage.color = new Color(0, 0, 0, 0);
                panelImage.raycastTarget = true;

                return scrollPanel;
            }
        }

        internal static void LoadoutPanelControllerRowFinishSetup(On.RoR2.UI.LoadoutPanelController.Row.orig_FinishSetup orig, object self, bool addWIPIcons)
        {
            orig(self, addWIPIcons);

            var rowRectTransform = self.GetFieldValue<RectTransform>("rowPanelTransform");
            var buttonContainerTransform = self.GetFieldValue<RectTransform>("buttonContainerTransform");
            foreach (var button in buttonContainerTransform.GetComponentsInChildren<HGButton>())
            {
                //Scroll to selected row if it's not fully visible
                button.onSelect.AddListener(new UnityEngine.Events.UnityAction(() =>
                {
                    var buttonsScrollRect = button.GetComponentInParent<ConstrainedScrollRect>();
                    var rowsScrollRect = buttonsScrollRect.redirectConstrained;
                    var eventSystemLocator = rowsScrollRect.GetComponent<MPEventSystemLocator>();

                    if (!eventSystemLocator || !eventSystemLocator.eventSystem || eventSystemLocator.eventSystem.currentInputSource != MPEventSystem.InputSource.Gamepad)
                    {
                        return;
                    }

                    var rowsContentPanel = rowsScrollRect.content;

                    var rowPosition = (Vector2)rowsScrollRect.transform.InverseTransformPoint(rowRectTransform.position);
                    var rowsScrollHeight = rowsScrollRect.GetComponent<RectTransform>().rect.height;
                    var halfRowHeight = rowRectTransform.rect.height / 2;

                    if (rowPosition.y - halfRowHeight < -rowsScrollHeight)
                    {
                        rowsContentPanel.anchoredPosition = new Vector2(
                            rowsContentPanel.anchoredPosition.x,
                            -rowRectTransform.anchoredPosition.y - rowsScrollHeight + halfRowHeight);
                    }
                    else if (rowPosition.y + halfRowHeight > 0)
                    {
                        rowsContentPanel.anchoredPosition = new Vector2(
                            rowsContentPanel.anchoredPosition.x,
                            -rowRectTransform.anchoredPosition.y - halfRowHeight);
                    }

                    var buttonsContentPanel = buttonsScrollRect.content;
                    var buttonRectTransform = button.GetComponent<RectTransform>();

                    var buttonPosition = (Vector2)buttonsScrollRect.transform.InverseTransformPoint(buttonRectTransform.position);
                    var buttonsScrollWidth = buttonsScrollRect.GetComponent<RectTransform>().rect.width;
                    var buttonWidth = buttonRectTransform.rect.width;
                    var buttonsPadding = 8;

                    if (buttonPosition.x + buttonWidth + buttonsPadding > 0)
                    {
                        buttonsContentPanel.anchoredPosition = new Vector2(
                            -buttonRectTransform.anchoredPosition.x - buttonWidth + buttonsScrollWidth - buttonsPadding,
                            buttonsContentPanel.anchoredPosition.y);
                    }
                    else if (buttonPosition.x - buttonsPadding < -buttonsScrollWidth)
                    {
                        buttonsContentPanel.anchoredPosition = new Vector2(
                            -buttonRectTransform.anchoredPosition.x + buttonsPadding,
                            buttonsContentPanel.anchoredPosition.y);
                    }
                }));
            }
        }

        internal static void LoadoutPanelControllerRowCtor(On.RoR2.UI.LoadoutPanelController.Row.orig_ctor orig, object self, LoadoutPanelController owner, int bodyIndex, string titleToken)
        {
            orig(self, owner, bodyIndex, titleToken);

            //Disabling sorting override because it not work with mask
            var highlightRect = self.GetFieldValue<RectTransform>("choiceHighlightRect");
            highlightRect.GetComponent<RefreshCanvasDrawOrder>().enabled = false;
            highlightRect.GetComponent<Canvas>().overrideSorting = false;

            var buttonContainer = self.GetFieldValue<RectTransform>("buttonContainerTransform");
            var rowPanel = self.GetFieldValue<RectTransform>("rowPanelTransform");
            
            var rowHorizontalLayout = rowPanel.gameObject.AddComponent<HorizontalLayoutGroup>();

            var panel = rowPanel.Find("Panel");
            var slotLabel = rowPanel.Find("SlotLabel");
            
            var labelContainer = new GameObject();
            labelContainer.transform.SetParent(rowPanel, false);
            panel.SetParent(labelContainer.transform, false);
            slotLabel.SetParent(labelContainer.transform, false);

            var slotLabelRect = slotLabel.GetComponent<RectTransform>();
            slotLabelRect.anchoredPosition = new Vector2(0, 0);
            
            var labelContainerLayout = labelContainer.AddComponent<LayoutElement>();
            labelContainerLayout.minHeight = 0;
            labelContainerLayout.preferredHeight = 96;
            labelContainerLayout.minWidth = 128;
            
            var labelContainerRect = labelContainer.GetComponent<RectTransform>();
            labelContainerRect.anchorMin = new Vector2(0, 0);
            labelContainerRect.anchorMax = new Vector2(1, 1);
            labelContainerRect.pivot = new Vector2(0, 0F);

            var scrollPanel = new GameObject();
            scrollPanel.transform.SetParent(rowPanel, false);

            buttonContainer.SetParent(scrollPanel.transform, false);
            highlightRect.SetParent(scrollPanel.transform, false);

            var mask = scrollPanel.AddComponent<RectMask2D>();

            var scrollPanelLayout = scrollPanel.AddComponent<LayoutElement>();
            scrollPanelLayout.preferredWidth = 100000;

            var scrollRect = scrollPanel.AddComponent<ConstrainedScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.content = buttonContainer;
            scrollRect.scrollSensitivity = -30;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollConstraint = ConstrainedScrollRect.Constraint.OnlyDrag;
            scrollRect.redirectConstrained = rowPanel.GetComponentInParent<ConstrainedScrollRect>();

            var scrollPanelRectTransform = scrollPanel.GetComponent<RectTransform>();
            scrollPanelRectTransform.pivot = new Vector2(1, 0.5F);
            scrollPanelRectTransform.anchorMin = new Vector2(0, 0);
            scrollPanelRectTransform.anchorMax = new Vector2(1, 1);

            //Adding ContentSizeFilter, otherwise childs would have been wrong size
            var buttonContainerSizeFilter = buttonContainer.gameObject.AddComponent<ContentSizeFitter>();
            buttonContainerSizeFilter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            buttonContainerSizeFilter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            buttonContainer.pivot = new Vector2(0, 0.5F);

            buttonContainer.Find("Spacer").gameObject.SetActive(false);

            var buttonContainerHorizontalLayout = buttonContainer.GetComponent<HorizontalLayoutGroup>();
            buttonContainerHorizontalLayout.padding = new RectOffset(8, 8, 8, 8);
        }

        internal static void CharacterSelectControllerRebuildLocal(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(
                x => x.MatchLdcI4(0),
                x => x.MatchStloc(23),
                x => x.MatchBr(out var label),
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(0));
            c.Index += 4;
            var instructions = new List<Instruction>();
            for (var i = 0; i < 8; i++)
            {
                instructions.Add(c.Next);
                c.Index += 1;
            }

            c.Index += 1;
            foreach (var instuction in instructions)
            {
                c.Emit(instuction.OpCode, instuction.Operand);
            }

            var fieldInfo = typeof(CharacterSelectController).GetNestedType("StripDisplayData", BindingFlags.NonPublic).GetField("enabled");
            c.Emit(OpCodes.Ldfld, fieldInfo);
            c.EmitDelegate<Action<RectTransform, bool>>((skillStrip, enabled) =>
            {
                if (!enabled)
                {
                    return;
                }

                //Enabling LayoutElement to force skill row size;
                var layoutElement = skillStrip.GetComponent<LayoutElement>();
                layoutElement.enabled = true;
                layoutElement.minHeight = 0;
                layoutElement.preferredHeight = 96;
            });
        }
    }
}
