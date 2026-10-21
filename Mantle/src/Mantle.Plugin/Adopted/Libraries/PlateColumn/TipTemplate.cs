using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
{
    /// <summary>
    /// The box every column tooltip shows, made once: a copy of the game's inventory item tooltip prefab (the one every
    /// item slot uses: a dark panel, its first child, holding a Topic and a Text) with a thin gold border on the panel.
    /// The copy sits under an inactive holder kept across scene loads, so it never shows or wakes itself; what is made
    /// from it (<see cref="TipBox"/>, or the game's <c>UITooltip</c> given it by <see cref="Column.DressTooltip"/>)
    /// comes out active with the border. Made again if something destroyed it.
    /// </summary>
    internal static class TipTemplate
    {
        private static readonly Color BorderColour = new Color(0.85f, 0.66f, 0.36f, 0.9f);
        private static readonly Vector2 BorderWidth = new Vector2(2f, -2f);

        private static GameObject? template;

        public static GameObject? For(InventoryGui gui)
        {
            if (template != null)
            {
                return template;
            }
            GameObject? prefab = GamePrefab(gui);
            if (prefab == null)
            {
                return null;
            }
            GameObject holder = new GameObject("PlateColumn_tiptemplates");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
            template = Object.Instantiate(prefab, holder.transform, false);
            template.name = "PlateColumn_tip";
            if (template.transform.childCount > 0)
            {
                AddBorder(template.transform.GetChild(0));
            }
            return template;
        }

        private static void AddBorder(Transform panel)
        {
            if (panel.GetComponent<Graphic>() == null)
            {
                return;
            }
            Outline border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = BorderColour;
            border.effectDistance = BorderWidth;
        }

        /// <summary>The item slots' tooltip prefab (a bordered box with topic and text), else any tooltip prefab the panel uses.</summary>
        private static GameObject? GamePrefab(InventoryGui gui)
        {
            GameObject? element = gui.m_playerGrid != null ? gui.m_playerGrid.m_elementPrefab : null;
            UITooltip? item = element != null ? element.GetComponent<UITooltip>() : null;
            if (item != null && item.m_tooltipPrefab != null)
            {
                return item.m_tooltipPrefab;
            }
            foreach (UITooltip other in gui.GetComponentsInChildren<UITooltip>(true))
            {
                if (other.m_tooltipPrefab != null)
                {
                    return other.m_tooltipPrefab;
                }
            }
            return null;
        }
    }
}
