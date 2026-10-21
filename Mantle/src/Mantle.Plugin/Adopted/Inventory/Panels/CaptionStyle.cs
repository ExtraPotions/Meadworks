using TMPro;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// Sets a slot name's look again when the text first wakes. TextMeshPro gives a new text its default style (size 18,
    /// word wrap, no auto size) in its own Awake, which runs only when the text is first shown; the slot grid is built
    /// while the inventory is closed, so the look <see cref="SlotLabels"/> set at creation was overwritten ("Backpack"
    /// wrapped onto two lines, user 2026-10-07). Start runs after every Awake.
    /// </summary>
    public sealed class CaptionStyle : MonoBehaviour
    {
        private void Start()
        {
            TextMeshProUGUI label = GetComponent<TextMeshProUGUI>();
            if (label != null)
                SlotLabels.Style(label);
        }
    }
}
