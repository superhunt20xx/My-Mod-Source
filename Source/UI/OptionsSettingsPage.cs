using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MTM101BaldAPI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Adds the mod's own settings page to the options menu, with a toggle for Baldi and
    /// one for the other NPCs.
    ///
    /// The page is added the same way as the credits page: OptionsMenu drives its pages
    /// with two parallel arrays (categories[] and categoryKeys[]), and ChangeCategory
    /// indexes both with the same value, so the two have to grow together.
    ///
    /// Each toggle is a clone of a vanilla one, which is what gives it the correct
    /// checkmark, hotspot and canvas state without reimplementing any of it.
    /// </summary>
    internal static class OptionsSettingsPage
    {
        /// <summary>Must match the keys in Localization.json.</summary>
        private const string TitleKey = "SH20XX_Options_ModSettings";
        private const string BaldiKey = "SH20XX_Options_AllowBaldi";
        private const string NpcsKey = "SH20XX_Options_AllowNpcs";

        private static bool added;

        internal static void TryPatch(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(OptionsMenu), "Awake");
            if (target == null)
            {
                Plugin.Log.LogWarning("Settings page: OptionsMenu.Awake was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(OptionsSettingsPage), "AwakePostfix")));
                Plugin.Log.LogInfo("Settings page patch applied to OptionsMenu.Awake.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings page: could not patch Awake: " + e.Message);
            }
        }

        private static void AwakePostfix(OptionsMenu __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                AddPage(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Settings page: could not add the page: " + e);
            }
        }

        private static void AddPage(OptionsMenu menu)
        {
            var categoriesField = AccessTools.Field(typeof(OptionsMenu), "categories");
            var keysField = AccessTools.Field(typeof(OptionsMenu), "categoryKeys");

            if (categoriesField == null || keysField == null)
            {
                Plugin.Log.LogWarning("Settings page: the category fields were not found.");
                return;
            }

            GameObject[] categories = categoriesField.GetValue(menu) as GameObject[];
            string[] keys = keysField.GetValue(menu) as string[];

            // Compare against the page the toggles actually live on, which is known to be
            // laid out correctly. The clone's own rect proved unreliable (pageSize 100x100
            // while its children landed 1800px off screen).
            var templateField = AccessTools.Field(typeof(OptionsMenu), "subtitlesToggle");
            MenuToggle liveToggle = templateField == null ? null : templateField.GetValue(menu) as MenuToggle;
            if (liveToggle == null)
            {
                liveToggle = menu.GetComponentInChildren<MenuToggle>(true);
            }

            if (liveToggle != null)
            {
                LogGeometryOf("live toggle template", liveToggle.transform);
            }

            if (categories != null && categories.Length > 0 && categories[0] != null)
            {
                LogGeometryOf("categories[0]", categories[0].transform);
            }

            if (categories == null || categories.Length == 0 || keys == null ||
                keys.Length != categories.Length)
            {
                Plugin.Log.LogWarning("Settings page: categories and categoryKeys are out of step; not adding a page.");
                return;
            }

            if (added || Array.IndexOf(keys, TitleKey) >= 0)
            {
                return;
            }

            // The toggles are cloned from a page that has them; the routine page has none.
            MenuToggle template = FindTemplateToggle(menu);
            GameObject page = BuildPage(categories[0], template);
            if (page == null)
            {
                return;
            }

            var newCategories = new GameObject[categories.Length + 1];
            Array.Copy(categories, newCategories, categories.Length);
            newCategories[categories.Length] = page;

            var newKeys = new string[keys.Length + 1];
            Array.Copy(keys, newKeys, keys.Length);
            newKeys[keys.Length] = TitleKey;

            categoriesField.SetValue(menu, newCategories);
            keysField.SetValue(menu, newKeys);

            added = true;
            Plugin.Log.LogInfo("Settings page: added as options page " + categories.Length +
                               " of " + newCategories.Length + ".");
        }

        /// <summary>Logs a transform's world box, for comparing page placement.</summary>
        private static void LogGeometryOf(string label, Transform target)
        {
            try
            {
                RectTransform rect = target as RectTransform;
                if (rect == null)
                {
                    Plugin.Log.LogInfo("Settings page: " + label + " has no RectTransform.");
                    return;
                }

                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Plugin.Log.LogInfo(string.Format(
                    "Settings page: {0} -> rectSize={1}, anchoredPos={2}, worldCorners=[{3},{4}]..[{5},{6}]",
                    label, rect.rect.size, rect.anchoredPosition,
                    corners[0].x.ToString("0"), corners[0].y.ToString("0"),
                    corners[2].x.ToString("0"), corners[2].y.ToString("0")));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings page: could not measure " + label + ": " + e.Message);
            }
        }

        /// <summary>
        /// Finds a live MenuToggle to clone. The options menu already holds one for the
        /// subtitles setting, so no prefab lookup is needed.
        /// </summary>
        private static MenuToggle FindTemplateToggle(OptionsMenu menu)
        {
            var field = AccessTools.Field(typeof(OptionsMenu), "subtitlesToggle");
            MenuToggle toggle = field == null ? null : field.GetValue(menu) as MenuToggle;

            if (toggle == null)
            {
                toggle = menu.GetComponentInChildren<MenuToggle>(true);
            }

            if (toggle == null)
            {
                Plugin.Log.LogWarning("Settings page: no MenuToggle was available to clone.");
            }

            return toggle;
        }

        private static GameObject BuildPage(GameObject template, MenuToggle toggleTemplate)
        {
            if (template == null)
            {
                return null;
            }

            GameObject page = UnityEngine.Object.Instantiate(template);
            page.name = "SH20XXSettingsPage";
            page.transform.SetParent(template.transform.parent, false);
            page.transform.localPosition = template.transform.localPosition;

            // Built while ACTIVE, then hidden.
            //
            // The clone inherits the template's inactive state, and children added to an
            // inactive parent stay inactive - which is exactly what the geometry log
            // showed (activeInHierarchy=False) while the checkboxes were still visible.
            // Unity also skips layout for inactive trees, so the positions measured there
            // were meaningless. ChangeCategory only calls SetActive, so activating the
            // page here and hiding it at the end is all that is needed.
            page.SetActive(true);

            // DestroyImmediate: a deferred Destroy leaves the cloned page's own labels
            // (Knoxum's Chaos Mode and friends) visible for a frame.
            for (int i = page.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(page.transform.GetChild(i).gameObject);
            }

            AddToggle(page.transform, toggleTemplate, BaldiKey, 30f,
                      ModSettings.AllowBaldi, OnBaldiChanged);
            AddToggle(page.transform, toggleTemplate, NpcsKey, -30f,
                      ModSettings.AllowNpcs, OnNpcsChanged);

            // The page is now complete; hide it so ChangeCategory controls when it shows.
            page.SetActive(false);

            return page;
        }

        /// <summary>
        /// Clones the template toggle under the page, labels it and wires the click.
        /// </summary>
        private static void AddToggle(Transform page, MenuToggle template, string labelKey,
                                      float y, bool initial, UnityAction onChanged)
        {
            if (template == null)
            {
                return;
            }

            MenuToggle toggle = UnityEngine.Object.Instantiate(template, page, false);
            toggle.name = "SH20XXToggle_" + labelKey;
            toggle.gameObject.SetActive(true);

            // The template is the subtitles toggle, so the clone arrives with its own
            // "瀛楀箷" caption and the TextLocalizer that produced it. Without removing both,
            // every clone on the page shows "瀛楀箷" - which is exactly what happened.
            StripInheritedLabels(toggle.transform);

            // A clone inherits the template's rect; anchoring it to the top-centre keeps
            // the y below meaningful regardless of the parent's own anchoring.
            var rect = toggle.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition3D = new Vector3(-150f, y, 0f);
            }

            toggle.Set(initial);

            // Measured after positioning: the clone is the reference the label is placed
            // against, because it demonstrably sits in the visible area.
            LogGeometryOf("cloned toggle " + labelKey, toggle.transform);

            // Clicking anywhere on the toggle flips it. OnRelease is what the vanilla
            // toggles use, so the clone behaves the same.
            StandardMenuButton button = toggle.GetComponentInChildren<StandardMenuButton>(true);
            if (button != null)
            {
                if (button.OnRelease == null)
                {
                    button.OnRelease = new UnityEvent();
                }

                button.OnRelease.AddListener(() =>
                {
                    toggle.Toggle();
                    onChanged();
                });
            }
            else
            {
                Plugin.Log.LogWarning("Settings page: the cloned toggle has no button, so '" +
                                      labelKey + "' cannot be clicked.");
            }

            AddLabel(toggle.transform, page, labelKey, y);
        }

        /// <summary>
        /// Removes the template toggle's own caption and its TextLocalizer.
        ///
        /// The clone carries a copy of the "瀛楀箷" label, and the localizer would keep
        /// that key applied over anything this page sets. DestroyImmediate is used because
        /// the deferred Destroy would leave the label on screen for a frame.
        /// </summary>
        private static void StripInheritedLabels(Transform toggle)
        {
            foreach (TextLocalizer localizer in toggle.GetComponentsInChildren<TextLocalizer>(true))
            {
                if (localizer != null && localizer.gameObject != toggle.gameObject)
                {
                    UnityEngine.Object.DestroyImmediate(localizer);
                }
            }

            // Any remaining text under the toggle is the inherited caption.
            foreach (TMP_Text text in toggle.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != null && text.transform != toggle)
                {
                    UnityEngine.Object.DestroyImmediate(text.gameObject);
                }
            }
        }

        /// <summary>Adds the localized caption to the left of a toggle.</summary>
        private static void AddLabel(Transform toggle, Transform page, string labelKey, float y)
        {
            try
            {
                // The text is looked up here rather than through a TextLocalizer: the
                // earlier version added one and it never showed, while the keys resolve
                // fine through the same call the item names use.
                string label = ModLocalization.Get(labelKey, labelKey);

                // Parented to the page and anchored to its centre, which is what puts the
                // caption beside the checkbox instead of far off to the left. The earlier
                // version anchored to the top-centre of a cloned page whose rect is only
                // 100x100, so the offset was scaled up and the text landed off screen.
                //
                // Placed to the LEFT of the toggle: the toggle sits at x=-150 and is 256
                // wide, so its left edge is near -278.
                TextMeshProUGUI text = UITextFactory.Create(
                    page, label,
                    new Vector2(-250f, y),
                    new Vector2(420f, 40f),
                    22f,
                    TextAlignmentOptions.MidlineRight);
                if (text == null)
                {
                    return;
                }

                // Same anchor as the toggle (top-centre), so both speak the same coordinate
                // system and these values mean what they look like.
                var labelRect = text.rectTransform;
                labelRect.anchorMin = new Vector2(0.5f, 1f);
                labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(1f, 0.5f);
                labelRect.anchoredPosition3D = new Vector3(-280f, y, 0f);

                Plugin.Log.LogInfo("Settings page: label '" + labelKey + "' = '" + label + "'.");
                LogGeometry(text, page, labelKey);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings page: could not add the label '" + labelKey + "': " + e.Message);
            }
        }

        /// <summary>
        /// Reports where a label actually ended up. The text was created with the right
        /// string but never appeared, so the position has to be measured rather than
        /// assumed - the options page anchors its children to the top-left and the canvas
        /// rect decides what is on screen at all.
        /// </summary>
        private static void LogGeometry(TMP_Text text, Transform page, string labelKey)
        {
            try
            {
                RectTransform rect = text.rectTransform;
                Canvas canvas = text.GetComponentInParent<Canvas>();

                // The corners are what actually decide visibility, because the parent's
                // rect may not be the size its anchors imply.
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                string box = string.Format("[{0},{1}]..[{2},{3}]",
                    corners[0].x.ToString("0"), corners[0].y.ToString("0"),
                    corners[2].x.ToString("0"), corners[2].y.ToString("0"));

                Plugin.Log.LogInfo(string.Format(
                    "Settings page: '{0}' geometry -> anchoredPos={1}, size={2}, " +
                    "activeInHierarchy={3}, canvas={4}, pageSize={5}, worldCorners={6}, " +
                    "localScale={7}, pageScale={8}",
                    labelKey,
                    rect.anchoredPosition, rect.sizeDelta,
                    text.gameObject.activeInHierarchy,
                    canvas == null ? "none" : canvas.name,
                    ((RectTransform)page).rect.size, box,
                    rect.localScale, page.localScale));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Settings page: could not measure the label: " + e.Message);
            }
        }

        private static void OnBaldiChanged()
        {
            ModSettings.AllowBaldi = ModSettings.AllowBaldi;
        }

        private static void OnNpcsChanged()
        {
            ModSettings.AllowNpcs = ModSettings.AllowNpcs;
        }
    }
}
