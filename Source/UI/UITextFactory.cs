using System;
using TMPro;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Creates TextMeshPro labels for the mod's options pages.
    ///
    /// Built by hand rather than through MTM101BaldAPI's UIHelpers.CreateText: that helper
    /// applies its own scaling for its own layout scheme, and on the cloned options pages
    /// the result was rendered thousands of units wide, off screen.
    ///
    /// Every value that affects layout is therefore set explicitly - anchors, pivot, size
    /// delta and scale - instead of being inherited from whatever the parent happens to be.
    /// </summary>
    internal static class UITextFactory
    {
        /// <summary>
        /// Creates a label under an existing element.
        ///
        /// The parent should be something already known to be on screen (a cloned toggle,
        /// the credits artwork). Parenting to a visible object means the two share one
        /// coordinate system, which is what makes the position trustworthy.
        /// </summary>
        internal static TextMeshProUGUI Create(Transform parent, string content,
                                               Vector2 anchoredPosition, Vector2 size,
                                               float fontSize,
                                               TextAlignmentOptions alignment)
        {
            if (parent == null)
            {
                Plugin.Log.LogWarning("Text factory: no parent was given.");
                return null;
            }

            try
            {
                GameObject go = new GameObject("SH20XXText");
                go.transform.SetParent(parent, false);

                TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
                text.text = content;
                text.fontSize = fontSize;
                text.alignment = alignment;
                text.color = Color.black;
                text.raycastTarget = false;
                text.enableWordWrapping = true;

                // Reuse the font that is already rendering in this menu, so no asset name
                // has to be guessed.
                TMP_Text template = FindFontTemplate(parent);
                if (template != null)
                {
                    text.font = template.font;
                    text.fontSharedMaterial = template.fontSharedMaterial;
                }

                RectTransform rect = text.rectTransform;
                rect.localScale = Vector3.one;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);

                return text;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Text factory: could not create text: " + e.Message);
                return null;
            }
        }

        /// <summary>Finds an already-rendering TMP_Text to copy the font from.</summary>
        private static TMP_Text FindFontTemplate(Transform parent)
        {
            try
            {
                Canvas canvas = parent.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] != null && texts[i].font != null &&
                            !texts[i].transform.IsChildOf(parent))
                        {
                            return texts[i];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Text factory: could not find a template font: " + e.Message);
            }

            return null;
        }
    }
}
