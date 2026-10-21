using System;
using System.Collections.Generic;
using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// KX01: a single-line kit text field on the stat sheet's dark rounded inset (<see cref="RoundedFill"/>,
    /// <see cref="KitInputLayout"/>), in the game's font. It is the game's own input field type (<c>GuiInputField</c>), so
    /// Ctrl + Backspace deletes a word and in Steam's Big Picture and on the Steam Deck <see cref="Focus"/> opens Steam's
    /// keyboard. Modes (<see cref="KitFieldMode"/>): Search (40 characters, Escape clears, Enter keeps), Numeric (4
    /// digits, a click selects all, Enter or a click elsewhere applies, Escape restores) and Text (Almanac AX04: commit on
    /// Enter or defocus, Escape restores, a product validator tints a rejected value). Changes arrive debounced
    /// (<see cref="KitInputLayout.Debounce"/>). While a kit field is focused, and the frame after, it is a C07 typing
    /// window, the game's Use and Inventory presses are dropped, Escape only leaves the field and pad B or Y leaves it
    /// instead of closing the inventory (<see cref="KitTypingRules"/>); <see cref="ReportChat"/> also makes the game's
    /// <c>Chat.HasFocus</c> true for mods that only ask the chat. Crucible-authored; null on a headless process (C10).
    /// </summary>
    public sealed partial class KitTextField
    {
        private static readonly List<KitTextField> fields = new List<KitTextField>();
        private static bool registered;
        private static bool wasTyping;

        private readonly KitDebounce debounce = new KitDebounce();
        private readonly TMP_InputField.OnValidateInput validate;
        private readonly Image fill;
        private readonly TMP_Text text;
        private string before = "";
        private bool valid = true;

        public KitFieldMode Mode { get; }
        public GuiInputField Input { get; }
        public RectTransform Rect { get; }

        /// <summary>The game's <c>Chat.HasFocus</c> reports true while this field is focused (off by default).</summary>
        public bool ReportChat { get; set; }

        /// <summary>A product check of the text; a rejected value shows in the warning tint (AX04). Null: always valid.</summary>
        public Func<string, bool> Validator { get; set; }

        /// <summary>The text changed and typing paused (debounced), or a search was cleared with Escape (at once).</summary>
        public event Action<string> Changed;

        /// <summary>An edit ended: Enter, a click elsewhere, Escape, or Steam's keyboard handing its text over.</summary>
        public event Action<KitFieldEdit> Ended;

        public bool Focused => Input != null && Input.isFocused;

        public bool Valid => valid;

        public string Text
        {
            get => Input != null ? Input.text : "";
            set
            {
                if (Input == null)
                    return;
                Input.SetTextWithoutNotify(KitFieldRules.Clip(Mode, value));
                debounce.Cancel();
                Validate();
            }
        }

        /// <summary>A kit field is focused now or was at the end of the last frame.</summary>
        public static bool Typing => wasTyping || AnyFocused;

        public static bool AnyFocused
        {
            get
            {
                foreach (KitTextField item in fields)
                    if (item.Focused)
                        return true;
                return false;
            }
        }

        private KitTextField(KitFieldMode mode, RectTransform rect, GuiInputField input, Image fill, TMP_Text text)
        {
            Mode = mode;
            Rect = rect;
            Input = input;
            this.fill = fill;
            this.text = text;
            validate = (current, index, added) => KitFieldRules.Accept(Mode, added);
        }

        /// <summary>
        /// A field under <paramref name="parent"/>, top-left at <paramref name="at"/>; null headless. The placeholder
        /// defaults to the kit's "Search" in search mode; <paramref name="keyboardTitle"/> titles Steam's keyboard.
        /// </summary>
        public static KitTextField Create(RectTransform parent, string name, KitFieldMode mode, Vector2 at, Vector2 size, string placeholder = null, string keyboardTitle = null)
        {
            if (!CrucibleUiPlugin.Active || parent == null)
                return null;
            Register();
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, at, size);
            Image image = go.GetComponent<Image>();
            image.sprite = RoundedFill.Sprite;
            image.type = Image.Type.Sliced;
            image.color = KitUnity.Colour(KitInputLayout.Fill);

            RectTransform area = TextArea(rect);
            TextAlignmentOptions alignment = mode == KitFieldMode.Numeric ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            TMP_Text words = KitCanvas.Text(area, "Text", "", KitInputLayout.TextSize, KitUnity.Colour(KitInputLayout.Text), alignment, false);
            Stretch(words.rectTransform);
            words.richText = false;
            words.raycastTarget = false;
            string hint = placeholder ?? (mode == KitFieldMode.Search ? KitText.Text(KitText.Search, CrucibleUiPlugin.Language) : "");
            TMP_Text shown = KitCanvas.Text(area, "Placeholder", KitLocalize.Text(hint), KitInputLayout.TextSize, KitUnity.Colour(KitInputLayout.Placeholder), alignment, false);
            Stretch(shown.rectTransform);
            shown.fontStyle = FontStyles.Italic;

            GuiInputField input = go.AddComponent<GuiInputField>();
            input.textViewport = area;
            input.textComponent = words;
            input.placeholder = shown;
            input.targetGraphic = image;
            KitTextField field = new KitTextField(mode, rect, input, image, words);
            field.Configure(keyboardTitle ?? hint);
            if(mode == KitFieldMode.Password) {
                TrackPassword(field);
                go.AddComponent<KitPasswordLifetime>().Field = field;
            }
            go.SetActive(true);
            fields.Add(field);
            return field;
        }

        /// <summary>
        /// Puts the cursor into the field, or opens Steam's keyboard where the game would; a pad player without one gets
        /// the kit's fallback message (AX16) and the field stays focused for a connected keyboard.
        /// </summary>
        public KitKeyboardOutcome Focus()
        {
            if (Input == null || !Input.interactable || !Input.gameObject.activeInHierarchy)
                return KitKeyboardOutcome.Focused;
            bool opened = false;
            try
            {
                opened = Input.OpenKeyboard();
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Warning("Crucible.UI: text field keyboard failed: " + e.GetType().Name);
                Input.ActivateInputField();
            }
            KitKeyboardOutcome outcome = KitKeyboard.Decide(MeadworksInput.GamepadActive, opened);
            if (outcome == KitKeyboardOutcome.Unavailable && MeadworksHeadless.Channel == NoticeChannel.Hud)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, KitText.Text(KitText.KeyboardUnavailable, CrucibleUiPlugin.Language));
            return outcome;
        }

        /// <summary>Lets go of the focus (the text is kept); nothing is taken from a selection already moving elsewhere.</summary>
        public void Release()
        {
            EventSystem system = EventSystem.current;
            if (Input != null && system != null && !system.alreadySelecting && system.currentSelectedGameObject == Input.gameObject)
                system.SetSelectedGameObject(null);
        }

        /// <summary>Empties the field and reports the change at once (Clear Search On Close).</summary>
        public void Clear()
        {
            if (Input == null || Input.text.Length == 0)
                return;
            Text = "";
            Raise("");
        }

        public void SetUsable(bool usable)
        {
            if (Input != null)
                Input.interactable = usable;
        }

        /// <summary>A blocked option (C03) makes the field read-only and dimmed, its tooltip giving the reason.</summary>
        public KitControlDecision Block(BlockedOption option, bool usable = true)
        {
            KitControlDecision decision = KitControls.Decide(usable, option, CrucibleUiPlugin.Language);
            SetUsable(decision.Interactable);
            if (text != null)
                text.alpha = decision.Dimmed ? KitCells.BlockedAlpha : 1f;
            if (Input != null)
                KitTooltip.Set(Input.gameObject, decision.TooltipTopic, decision.Tooltip);
            return decision;
        }

        public void Destroy()
        {
            if(Mode == KitFieldMode.Password) ClosePassword();
            fields.Remove(this);
            if (Rect != null)
                KitUnity.Retire(Rect.gameObject);
        }

        private static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static RectTransform TextArea(RectTransform parent)
        {
            GameObject go = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(KitInputLayout.InsetX, KitInputLayout.InsetY);
            rect.offsetMax = new Vector2(-KitInputLayout.InsetX, -KitInputLayout.InsetY);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
