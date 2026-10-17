using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Adds a Credits page to the in-game options menu.
    ///
    /// OptionsMenu drives its pages with two parallel arrays:
    ///
    ///   categories[]    : the page GameObjects, activated/deactivated on change
    ///   categoryKeys[]  : localization keys, resolved through LocalizationManager for
    ///                     the current/previous/next page labels
    ///
    /// ChangeCategory walks categories[] by index and indexes categoryKeys[] with the same
    /// value, so both have to grow together or the labels desync from the pages. Because
    /// both are internal to Assembly-CSharp they are extended reflectively.
    ///
    /// The page's own title key lives in Localization.json rather than being added at
    /// runtime: ModLocalization hands the game a snapshot dictionary at load, so an entry
    /// registered later would never be seen.
    /// </summary>
    internal static class OptionsCreditsPage
    {
        /// <summary>Must match the key added to Localization.json.</summary>
        private const string TitleKey = "SH20XX_Options_Credits";

        private const string CreditsImageFile = "BBPP_Credits.png";
        private const string CreditsTextFile = "Credits.txt";

        /// <summary>Localization key holding the credits body text.</summary>
        private const string CreditsKey = "SH20XX_Credits_Text";

        /// <summary>Guards against adding the page twice to the same menu.</summary>
        private static bool added;

        internal static void TryPatch(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(OptionsMenu), "Awake");
            if (target == null)
            {
                Plugin.Log.LogWarning("Credits page: OptionsMenu.Awake was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(OptionsCreditsPage), "AwakePostfix")));
                Plugin.Log.LogInfo("Credits page patch applied to OptionsMenu.Awake.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Credits page: could not patch Awake: " + e.Message);
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
                Plugin.Log.LogError("Credits page: could not add the page: " + e);
            }
        }

        private static void AddPage(OptionsMenu menu)
        {
            var categoriesField = AccessTools.Field(typeof(OptionsMenu), "categories");
            var keysField = AccessTools.Field(typeof(OptionsMenu), "categoryKeys");

            if (categoriesField == null || keysField == null)
            {
                Plugin.Log.LogWarning("Credits page: the category fields were not found.");
                return;
            }

            GameObject[] categories = categoriesField.GetValue(menu) as GameObject[];
            string[] keys = keysField.GetValue(menu) as string[];

            if (categories == null || categories.Length == 0)
            {
                Plugin.Log.LogWarning("Credits page: the options menu has no categories.");
                return;
            }

            if (keys == null || keys.Length != categories.Length)
            {
                Plugin.Log.LogWarning("Credits page: categories and categoryKeys are out of step (" +
                                      (categories == null ? 0 : categories.Length) + " vs " +
                                      (keys == null ? 0 : keys.Length) + "); not adding a page.");
                return;
            }

            // Re-adding on every Awake would keep growing the arrays.
            if (added || Array.IndexOf(keys, TitleKey) >= 0)
            {
                return;
            }

            GameObject page = BuildPage(categories[0]);
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
            Plugin.Log.LogInfo("Credits page: added as options page " + categories.Length +
                               " of " + newCategories.Length + ".");
        }

        /// <summary>
        /// Builds the page, cloned from an existing category so it inherits the menu's
        /// canvas, scale and anchors instead of guessing at them.
        /// </summary>
        private static GameObject BuildPage(GameObject template)
        {
            if (template == null)
            {
                return null;
            }

            GameObject page = UnityEngine.Object.Instantiate(template);
            page.name = "SH20XXCreditsPage";
            page.transform.SetParent(template.transform.parent, false);
            page.transform.localPosition = template.transform.localPosition;

            // Start hidden: ChangeCategory only activates the page it moves to, and the
            // clone must not appear on top of whichever page is showing.
            page.SetActive(false);

            // Strip the cloned contents: the template's controls are specific to its page.
            ClearChildren(page.transform);

            // Image on top, credits text underneath it.
            //
            // The cloned page anchors its children to the TOP-LEFT, not the centre, which
            // is why the artwork previously sat jammed into the corner. Every element here
            // sets its anchors explicitly so its position means what it looks like.
            Sprite image = LoadSprite(CreditsImageFile);
            if (image != null)
            {
                GameObject imageObject = new GameObject("CreditsImage");
                imageObject.transform.SetParent(page.transform, false);

                Image graphic = imageObject.AddComponent<Image>();
                graphic.sprite = image;
                graphic.raycastTarget = true;   // it is the button that reveals the credits

                RectTransform rect = graphic.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(image.rect.width, image.rect.height);
                rect.anchoredPosition3D = new Vector3(0f, -15f, 0f);

                // The names stay hidden until the logo is clicked, so the page opens as
                // just the artwork.
                RevealOnClick click = imageObject.AddComponent<RevealOnClick>();
                click.Bind(page.transform);
            }
            else
            {
                Plugin.Log.LogWarning("Credits page: '" + CreditsImageFile + "' could not be loaded.");
            }

            AddText(page.transform);

            return page;
        }

        /// <summary>
        /// Removes the cloned page's own contents, leaving an empty page.
        ///
        /// DestroyImmediate rather than Destroy: the clone is a copy of a settings page
        /// and its labels (Knox, Chaos Mode, ...) were still on screen for a frame before
        /// the deferred Destroy took effect.
        /// </summary>
        private static void ClearChildren(Transform page)
        {
            for (int i = page.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(page.GetChild(i).gameObject);
            }
        }

        private static void AddText(Transform page)
        {
            string credits = ReadCredits();
            if (string.IsNullOrEmpty(credits))
            {
                Plugin.Log.LogWarning("Credits page: no credits text was found, so only the artwork is shown.");
                return;
            }

            Plugin.Log.LogInfo("Credits page: showing " + credits.Length + " characters of credits.");

            try
            {
                // Small font on purpose: the list is ten lines and a larger size ran the
                // bottom entries off the page.
                TextMeshProUGUI text = UITextFactory.Create(
                    page, credits,
                    new Vector2(0f, -90f),
                    new Vector2(430f, 280f),
                    15f,
                    TextAlignmentOptions.Top);

                if (text == null)
                {
                    Plugin.Log.LogWarning("Credits page: the text could not be created.");
                    return;
                }

                // Same top-centre anchor as the artwork above it.
                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition3D = new Vector3(0f, -90f, 0f);

                // Hidden until the logo is clicked.
                text.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Credits page: could not add the text: " + e.Message);
            }
        }

        /// <summary>
        /// Reads the credits from Language/English/Credits.txt.
        ///
        /// The text file is the primary source rather than Localization.json, because the
        /// list of names is data rather than an interface string.
        /// </summary>
        private static string ReadCredits()
        {
            try
            {
                string path = Path.Combine(ModPaths.Root, "Language", "English", CreditsTextFile);
                if (File.Exists(path))
                {
                    string fromFile = File.ReadAllText(path);
                    if (!string.IsNullOrEmpty(fromFile))
                    {
                        Plugin.Log.LogInfo("Credits page: read " + fromFile.Length +
                                           " characters from " + CreditsTextFile + ".");
                        return fromFile;
                    }
                }
                else
                {
                    Plugin.Log.LogWarning("Credits page: '" + path + "' was not found; trying the json key.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Credits page: could not read " + CreditsTextFile + ": " + e.Message);
            }

            // Fallback so the page still has something to show if the file is missing.
            string fromJson = ModLocalization.Get(CreditsKey, null);
            if (!string.IsNullOrEmpty(fromJson))
            {
                Plugin.Log.LogInfo("Credits page: using the '" + CreditsKey + "' json entry instead.");
                return fromJson;
            }

            return null;
        }

        private static Sprite LoadSprite(string fileName)
        {
            try
            {
                string path = ModPaths.FindFile(fileName, ModPaths.Textures("UI"), ModPaths.Textures());
                if (path == null)
                {
                    return null;
                }

                Texture2D texture = AssetLoader.TextureFromFile(path);
                return texture == null ? null : AssetLoader.SpriteFromTexture2D(texture, 1f);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Credits page: could not load '" + fileName + "': " + e.Message);
                return null;
            }
        }
    }
}
