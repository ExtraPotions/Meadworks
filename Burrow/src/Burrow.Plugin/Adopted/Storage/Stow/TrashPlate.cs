using System;
using Burrow.Core;
using ExtraPotions.Burrow.Embedded.PatchGuard;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Burrow.Stow
{
    /// <summary>
    /// A clickable copy of the armour plate with an iron bin icon (the game's hammer in salvage mode, see
    /// <see cref="Burrow.Integration.TrashCanMode"/>) and no caption. Mantle dresses it with the
    /// same compact frame as armour and weight. The native Trash name and armor_icon child remain available
    /// to other inventory mods. Without a column owner, creation still requires room between the vanilla plates.
    /// </summary>
    public static class TrashPlate
    {
        private const string Name = "Trash";
        private const string Icon = "armor_icon";
        /// <summary>Under the armour plate, where trash can mods have always put theirs.</summary>
        private const float Below = 78f;

        public static bool TryCreate(InventoryGui gui, Action onClick)
        {
            RectTransform armor = gui.m_player.Find("Armor") as RectTransform;
            RectTransform weight = gui.m_player.Find("Weight") as RectTransform;
            if (armor == null || weight == null || gui.m_player.Find(Name) != null
                || (!PackPanelLink.LaysOutInventory && !Fits(gui.m_player, armor, weight)))
            {
                Plugin.Log.LogInfo("trash can stays in the button row: a game plate is missing, a Trash exists, or there is no room under the armor");
                return false;
            }
            GameObject go = UnityEngine.Object.Instantiate(armor.gameObject, armor.parent);
            go.name = Name;
            go.SetActive(true);
            go.transform.SetSiblingIndex(armor.GetSiblingIndex() + 1);
            ((RectTransform)go.transform).anchoredPosition = armor.anchoredPosition - new Vector2(0f, Below);
            Image bin = Dress(go);
            if (bin == null)
            {
                UnityEngine.Object.Destroy(go);
                return false;
            }
            MakeButton(go, bin, onClick);
            Burrow.Integration.TrashCanMode.Track(go, bin);
            return true;
        }

        /// <summary>Turns <paramref name="go"/> into the trash button: the bin lights up on hover and press.</summary>
        public static void MakeButton(GameObject go, Image bin, Action onClick)
        {
            Button button = go.GetComponent<Button>();
            if (button == null)
                button = go.AddComponent<Button>();
            button.targetGraphic = bin;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1f, 0.55f, 0.45f, 1f);
            colours.selectedColor = colours.normalColor;   // a click selects the button; it must not stay red after
            colours.pressedColor = new Color(1f, 0.3f, 0.2f, 1f);
            button.colors = colours;
            button.onClick.AddListener(() => Guard.Run("trash can", onClick));
        }

        /// <summary>Room for a plate between the two: the weight plate's centre a plate's height below the new plate's.</summary>
        private static bool Fits(RectTransform panel, RectTransform armor, RectTransform weight)
        {
            float gap = CentreY(panel, armor) - CentreY(panel, weight);
            return gap >= Below + armor.rect.height;
        }

        private static float CentreY(RectTransform panel, RectTransform plate) =>
            panel.InverseTransformPoint(plate.TransformPoint(plate.rect.center)).y;

        /// <summary>
        /// The copy's shield becomes the bin and its number is hidden; every behaviour it copied from the armour plate goes,
        /// so nothing updates it or answers for the armour. Null without an icon.
        /// </summary>
        private static Image Dress(GameObject go)
        {
            foreach (Behaviour behaviour in go.GetComponents<Behaviour>())
                UnityEngine.Object.DestroyImmediate(behaviour);
            Image bin = go.transform.Find(Icon) is Transform icon ? icon.GetComponent<Image>() : null;
            if (bin == null)
                return null;
            bin.sprite = StowSprites.Bin;
            bin.preserveAspect = true;
            bin.color = Color.white;
            foreach (TMP_Text word in go.GetComponentsInChildren<TMP_Text>(true))
                word.gameObject.SetActive(false);
            return bin;
        }
    }
}
