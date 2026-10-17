using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Shows a random line from splashes.txt on the main menu, above the version number.
    ///
    /// The lines are read once and kept in memory, and one is picked each time the menu
    /// appears - the Minecraft title screen behaviour the file is copied from.
    ///
    /// The text is parented to the version label and positioned relative to it, rather
    /// than placed at an absolute point on the menu. That is deliberate: the version label
    /// is known to be laid out correctly, so anchoring to it means this does not depend on
    /// the menu's own rect or anchoring, which is what made earlier label placement fail.
    /// </summary>
    internal static class MenuSplash
    {
        private const string FileName = "splashes.txt";

        /// <summary>
        /// Name of the child that is created, so a reload can replace the previous one.
        /// </summary>
        private const string ObjectName = "SH20XXSplash";

        private static readonly List<string> lines = new List<string>();

        private static bool loaded;

        internal static void TryPatch(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(MainMenu), "Start");
            if (target == null)
            {
                Plugin.Log.LogWarning("Menu splash: MainMenu.Start was not found.");
                return;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(MenuSplash), "StartPostfix")));
                Plugin.Log.LogInfo("Menu splash patch applied to MainMenu.Start.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Menu splash: could not patch Start: " + e.Message);
            }
        }

        private static void StartPostfix(MainMenu __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                Show(__instance.transform);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Menu splash: could not show a splash: " + e.Message);
            }
        }

        private static void Show(Transform menu)
        {
            Load();

            if (lines.Count == 0)
            {
                return;
            }

            // Anchored to the version label: it is the element this belongs above, and it
            // is guaranteed to be positioned correctly.
            Transform version = menu.Find("Version");
            if (version == null)
            {
                Plugin.Log.LogWarning("Menu splash: the 'Version' label was not found under Menu.");
                return;
            }

            // A reload creates a second menu, so drop the previous splash with it. It now
            // lives under the menu root rather than under the version label.
            Transform previous = menu.Find(ObjectName);
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }

            string line = lines[UnityEngine.Random.Range(0, lines.Count)];

            // Font size is taken from the version label itself, then halved. The measured
            // mapping is world = local x 5, so a font that suits the 100-wide version label
            // is far too large for a single line of splash text.
            float fontSize = 8f;
            TMP_Text versionText = version.GetComponent<TMP_Text>();
            if (versionText != null && versionText.fontSize > 0f)
            {
                fontSize = versionText.fontSize * 0.45f;
            }

            // Parented to the MENU, not to the version label.
            //
            // The version label's own rect is 100x50 local while the menu is where the
            // layout actually lives, and world = local x 5 in this space (confirmed by the
            // log: the version label's local 144..244 became world 720..1220). Working in
            // menu space is therefore predictable: local x 0 is the horizontal centre.
            Transform root = version.parent != null ? version.parent : version;

            TextMeshProUGUI text = UITextFactory.Create(
                root, line,
                Vector2.zero,
                new Vector2(200f, 40f),
                fontSize,
                TextAlignmentOptions.Center);

            if (text == null)
            {
                return;
            }

            text.gameObject.name = ObjectName;
            text.color = new Color(1f, 0.85f, 0.2f);   // gold, so it stands out from the version

            // Directly below the version label, using the version label's own x.
            //
            // This is measured, not guessed. Mapping the previous splash's position to the
            // screen (world (0,120) landed near pixel (700,480)) together with the version
            // label (world (970,400) near pixel (1300,275)) gives the affine mapping, and
            // the requested spot works out at world (985,106) - which is local (197,21),
            // i.e. the version's own x and 60 units lower.
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            RectTransform versionRect = version as RectTransform;
            Vector3 versionPosition = versionRect != null
                ? versionRect.anchoredPosition3D
                : Vector3.zero;
            rect.anchoredPosition3D = new Vector3(
                versionPosition.x,
                versionPosition.y - 60f,
                versionPosition.z);

            rect.sizeDelta = new Vector2(140f, 30f);

            text.alignment = TextAlignmentOptions.Center;

            // Overflow rather than Truncate: truncation drops a line entirely when it does
            // not fit, which looks identical to the text failing to render.
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            // Force the mesh to be built now against the final rect. TMP generates its
            // geometry on enable/update, so a rect changed in the same frame could leave
            // the mesh empty - which is what made earlier attempts invisible.
            text.ForceMeshUpdate();

            Plugin.Log.LogInfo(string.Format(
                "Menu splash: text state -> font={0}, fontSize={1}, color={2}, chars={3}, " +
                "box={4}, overflow={5}",
                text.font == null ? "NULL" : text.font.name,
                text.fontSize, text.color, line.Length,
                rect.sizeDelta, text.overflowMode));

            LogBox("version label", version);
            LogBox("splash", text.transform);

            Plugin.Log.LogInfo("Menu splash: showing \"" + line + "\" (" + lines.Count + " line(s) loaded).");
        }

        /// <summary>Logs a transform's world box, so a misplacement is measurable.</summary>
        private static void LogBox(string label, Transform target)
        {
            try
            {
                RectTransform rect = target as RectTransform;
                if (rect == null)
                {
                    Plugin.Log.LogInfo("Menu splash: " + label + " has no RectTransform.");
                    return;
                }

                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Plugin.Log.LogInfo(string.Format(
                    "Menu splash: {0} -> rectSize={1}, anchoredPos={2}, localScale={3}, " +
                    "active={4}, canvas={5}, worldCorners=[{6},{7}]..[{8},{9}]",
                    label, rect.rect.size, rect.anchoredPosition, rect.localScale,
                    rect.gameObject.activeInHierarchy,
                    rect.GetComponentInParent<Canvas>() == null ? "none" : "ok",
                    corners[0].x.ToString("0"), corners[0].y.ToString("0"),
                    corners[2].x.ToString("0"), corners[2].y.ToString("0")));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Menu splash: could not measure " + label + ": " + e.Message);
            }
        }

        /// <summary>
        /// Reads splashes.txt once. Blank lines are skipped, and a missing file leaves the
        /// list empty so nothing is drawn rather than showing an error on the title screen.
        /// </summary>
        private static void Load()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            try
            {
                string path = Path.Combine(ModPaths.Root, FileName);
                if (!File.Exists(path))
                {
                    Plugin.Log.LogWarning("Menu splash: '" + path + "' was not found.");
                    return;
                }

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length > 0)
                    {
                        lines.Add(line);
                    }
                }

                Plugin.Log.LogInfo("Menu splash: loaded " + lines.Count + " line(s) from " + FileName + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Menu splash: could not read " + FileName + ": " + e.Message);
            }
        }
    }
}
