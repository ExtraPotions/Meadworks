using System;
using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>KX01 field wiring: the game field's events, edit ends from <see cref="KitFieldRules"/>, validation and debounce.</summary>
    public sealed partial class KitTextField
    {
        private void Configure(string keyboardTitle)
        {
            if(Mode == KitFieldMode.Password) {
                Input.contentType = TMP_InputField.ContentType.Password;
                Input.asteriskChar = '*';
                Input.VirtualKeyboardStateChange += PasswordKeyboardState;
            }
            Input.lineType = TMP_InputField.LineType.SingleLine;
            Input.characterLimit = KitFieldRules.Limit(Mode);
            Input.onValidateInput = validate;
            Input.onFocusSelectAll = KitFieldRules.SelectAllOnFocus(Mode);
            Input.richText = false;
            Input.customCaretColor = true;
            Input.caretColor = KitUnity.Colour(KitInputLayout.Caret);
            Input.caretWidth = KitInputLayout.CaretWidth;
            Input.selectionColor = KitUnity.Colour(KitInputLayout.Selection);
            Input.transition = Selectable.Transition.None;
            Input.navigation = new Navigation { mode = Navigation.Mode.None };
            Input.VirtualKeyboardTitle = keyboardTitle ?? "";
            Input.onValueChanged = new TMP_InputField.OnChangeEvent();
            Input.onEndEdit = new TMP_InputField.SubmitEvent();
            Input.onSelect = new TMP_InputField.SelectionEvent();
            Input.OnInputSubmit = new OnInputSubmitEvent();
            Input.onSelect.AddListener(current => Guard("select", () => before = Mode == KitFieldMode.Password ? "" : current ?? ""));
            Input.onValueChanged.AddListener(current => Guard("change", () =>
            {
                if(PasswordClosed) { ClearPassword(); return; }
                debounce.Touch(Time.unscaledTime);
                Validate();
            }));
            Input.onEndEdit.AddListener(current => Guard("end", () => End(current, Input.wasCanceled)));
            // Steam's keyboard hands its text over through the game's submit event, not end-edit; Enter in a focused
            // field raises it too and end-edit follows, so only an unfocused field takes it here.
            Input.OnInputSubmit.AddListener(current => Guard("submit", () =>
            {
                if (!Input.isFocused)
                    End(Input.text, false);
            }));
            Input.SetTextWithoutNotify("");
        }

        private void End(string current, bool cancelled)
        {
            if(PasswordClosed) { ClearPassword(); return; }
            KitFieldEdit edit = KitFieldRules.End(Mode, current, before, cancelled);
            if (Input.text != edit.Text)
                Input.SetTextWithoutNotify(edit.Text);
            Validate();
            if (edit.Cleared || edit.Restored)
                debounce.Cancel();
            if (edit.Cleared)
                Raise(edit.Text);
            before = Mode == KitFieldMode.Password ? "" : edit.Text;
            Ended?.Invoke(edit);
            if (edit.Release)
                Release();
        }

        private void Validate()
        {
            bool now = Validator == null || SafeValid(Input.text);
            if (now == valid)
                return;
            valid = now;
            if (fill != null)
                fill.color = KitUnity.Colour(KitInputLayout.FillColour(valid));
            if (text != null)
                text.color = KitUnity.Colour(KitInputLayout.TextColour(valid));
        }

        private bool SafeValid(string value)
        {
            try
            {
                return Validator(value ?? "");
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Warning("Crucible.UI: text field validator failed: " + e.GetType().Name);
                return false;
            }
        }

        private void Raise(string value)
        {
            try
            {
                Changed?.Invoke(value);
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error(Mode == KitFieldMode.Password
                    ? "Crucible.UI: password field change failed: " + e.GetType().Name
                    : $"Crucible.UI: text field {Rect?.name} change failed: {e}");
            }
        }

        private void Update(float now)
        {
            // The game's field adds its own validator when it starts, which would let other characters past the mode.
            if (Input.onValidateInput != validate)
                Input.onValidateInput = validate;
            if (debounce.Due(now))
                Raise(Input.text);
            FitIdle();
        }

        private bool? idleFit;

        /// <summary>
        /// A Text field at rest shows its whole value inside the box: it wraps and shrinks (down to the kit's caption
        /// minimum) instead of running past the edge, which longer translations and value lists did. While focused it is
        /// the game's single-line field again, full size and scrolling with the caret.
        /// </summary>
        private void FitIdle()
        {
            if (Mode != KitFieldMode.Text || text == null)
                return;
            bool idle = !Focused;
            if (idleFit == idle)
                return;
            idleFit = idle;
            KitCanvas.HangulWordBreaks();
            text.enableAutoSizing = idle;
            text.fontSizeMin = KitButtonText.MinSize;
            text.fontSizeMax = KitInputLayout.TextSize;
            text.fontSize = KitInputLayout.TextSize;
            text.textWrappingMode = idle ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error(Mode == KitFieldMode.Password
                    ? "Crucible.UI: password field " + what + " failed: " + e.GetType().Name
                    : $"Crucible.UI: text field {what} failed: {e}");
            }
        }
    }
}
