using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Reveals the credits text when the logo is clicked.
    ///
    /// The game's cursor only interacts with objects that carry a real UI event handler,
    /// so this implements IPointerClickHandler rather than relying on the Image alone.
    /// A click on the image toggles the names, so clicking again hides them.
    /// </summary>
    internal sealed class RevealOnClick : MonoBehaviour, IPointerClickHandler
    {
        private Transform page;

        /// <summary>Binds the page whose credits text should be shown.</summary>
        internal void Bind(Transform pageTransform)
        {
            page = pageTransform;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            try
            {
                Transform target = FindText();
                if (target == null)
                {
                    return;
                }

                bool showing = !target.gameObject.activeSelf;
                target.gameObject.SetActive(showing);
                Plugin.Log.LogInfo("Credits page: names " + (showing ? "shown." : "hidden."));
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Credits page: could not toggle the names: " + e.Message);
            }
        }

        /// <summary>
        /// Finds the credits text under the page. It is the only TextMeshPro element the
        /// credits page creates.
        /// </summary>
        private Transform FindText()
        {
            if (page == null)
            {
                return null;
            }

            var texts = page.GetComponentsInChildren<TMPro.TMP_Text>(true);
            return texts != null && texts.Length > 0 ? texts[0].transform : null;
        }
    }
}
