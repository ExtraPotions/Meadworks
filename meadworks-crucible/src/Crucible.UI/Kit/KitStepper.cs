using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// KX04: <c>- amount +</c> from two <see cref="KitButton"/>s (44 wide) and a numeric <see cref="KitTextField"/> (62
    /// wide), 6 apart, all as high as the product asks (the Craft button's height for Kindle). The amount is a Core
    /// <see cref="KitAmount"/>: a click steps one, Shift + click to the next ten, Ctrl + click to the minimum or the
    /// product's "most now" cap; the wheel does the same over each part; typed amounts apply on Enter or a click
    /// elsewhere and Escape restores. With <see cref="PadSteps"/> on (the product's panel has the gamepad), the D-pad's
    /// left and right press - and +, and a held D-pad repeats, switching to tens after quick repeats
    /// (<see cref="KitStepRepeat"/>). No tooltips. Null on a headless process (C10).
    /// </summary>
    public sealed class KitStepper
    {
        /// <summary>A held D-pad repeats after this long...</summary>
        private const float HoldDelay = 0.4f;
        /// <summary>...every this often (inside <see cref="KitStepRules.RepeatWindow"/>, so the repeats turn into tens).</summary>
        private const float HoldInterval = 0.1f;

        private static readonly List<KitStepper> steppers = new List<KitStepper>();

        private readonly KitStepRepeat repeat = new KitStepRepeat();
        private int heldDirection;
        private float nextRepeat;

        public RectTransform Root { get; }
        public Button Less { get; }
        public Button More { get; }
        public KitTextField Field { get; }
        public KitAmount Amount { get; }

        /// <summary>The D-pad's left and right step this stepper (set while the product's panel has the gamepad).</summary>
        public bool PadSteps { get; set; }

        public bool Shown => Root != null && Root.gameObject.activeInHierarchy;

        public int Value => Amount.Value;

        /// <summary>The amount changed (by a click, the wheel, the pad, typing or a lower cap).</summary>
        public event Action<int> Changed;

        private KitStepper(RectTransform root, Button less, Button more, KitTextField field, KitAmount amount)
        {
            Root = root;
            Less = less;
            More = more;
            Field = field;
            Amount = amount;
        }

        /// <summary>A stepper under <paramref name="parent"/>, top-left at <paramref name="at"/>, <paramref name="height"/> high; null headless.</summary>
        public static KitStepper Create(RectTransform parent, string name, Vector2 at, float height, int min = 1, int max = KitStepRules.Most)
        {
            if (!CrucibleUiPlugin.Active || parent == null)
                return null;
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = at;
            root.sizeDelta = new Vector2(KitStepperLayout.Width, height);
            KitAmount amount = new KitAmount(min, max);
            KitStepper made = null;
            Button less = KitButton.Create(root, "Crucible_step_less", "-", () => made?.Click(-1), Vector2.zero, new Vector2(KitStepperLayout.ButtonWidth, height));
            Button more = KitButton.Create(root, "Crucible_step_more", "+", () => made?.Click(1), new Vector2(KitStepperLayout.PlusX, 0f), new Vector2(KitStepperLayout.ButtonWidth, height));
            KitTextField field = KitTextField.Create(root, "Crucible_step_amount", KitFieldMode.Numeric, new Vector2(KitStepperLayout.FieldX, 0f), new Vector2(KitStepperLayout.FieldWidth, height));
            if (less == null || more == null || field == null)
            {
                KitUnity.Retire(go);
                return null;
            }
            made = new KitStepper(root, less, more, field, amount);
            field.Text = amount.Text;
            field.Ended += edit => made.Typed(edit);
            amount.Changed += value => made.Raise(value);
            KitWheel.Add(less.gameObject, made.Wheel);
            KitWheel.Add(more.gameObject, made.Wheel);
            KitWheel.Add(field.Input.gameObject, made.Wheel);
            made.Refresh();
            steppers.Add(made);
            return made;
        }

        /// <summary>The most allowed now (materials, free slots); the amount drops to it when above.</summary>
        public void Follow(int cap)
        {
            Amount.Follow(cap);
            Refresh();
        }

        public void Set(int value)
        {
            Amount.Set(value);
            Refresh();
        }

        public void Show(bool show)
        {
            if (Root != null && Root.gameObject.activeSelf != show)
                Root.gameObject.SetActive(show);
        }

        public void Destroy()
        {
            steppers.Remove(this);
            Field?.Destroy();
            if (Root != null)
                KitUnity.Retire(Root.gameObject);
        }

        private void Click(int direction) => Step(direction, KitClicks.Of(MeadworksInput.ShiftHeld, MeadworksInput.CtrlHeld), false);

        private void Wheel(int direction) => Click(direction);

        private void Step(int direction, KitClickKind kind, bool fast)
        {
            Amount.Step(direction, kind, fast);
            Refresh();
        }

        private void Typed(KitFieldEdit edit)
        {
            if (edit.Commit)
                Amount.Set(edit.Text);
            Refresh();
        }

        private void Raise(int value)
        {
            try
            {
                Changed?.Invoke(value);
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: stepper {Root?.name} change failed: {e}");
            }
        }

        /// <summary>The field shows the amount unless it is being typed; - and + only when they can step.</summary>
        private void Refresh()
        {
            if (Field != null && !Field.Focused && Field.Text != Amount.Text)
                Field.Text = Amount.Text;
            if (Less != null)
                Less.interactable = Amount.CanLess;
            if (More != null)
                More.interactable = Amount.CanMore;
        }

        private void Pad(float now)
        {
            if (!PadSteps || !Shown || !MeadworksInput.GamepadActive || KitTextField.Typing)
            {
                heldDirection = 0;
                return;
            }
            int pressed = MeadworksInput.PadDown(PadButton.DPadRight) ? 1 : MeadworksInput.PadDown(PadButton.DPadLeft) ? -1 : 0;
            if (pressed != 0)
            {
                heldDirection = pressed;
                nextRepeat = now + HoldDelay;
                Step(pressed, KitClickKind.Plain, repeat.Next(pressed, now, true));
                return;
            }
            if (heldDirection == 0)
                return;
            PadButton held = heldDirection > 0 ? PadButton.DPadRight : PadButton.DPadLeft;
            if (!MeadworksInput.PadHeld(held))
            {
                heldDirection = 0;
                return;
            }
            if (now < nextRepeat)
                return;
            nextRepeat = now + HoldInterval;
            Step(heldDirection, KitClickKind.Plain, repeat.Next(heldDirection, now, true));
        }

        /// <summary>Plugin Update: the pad, and the shown amount; destroyed steppers are forgotten.</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = steppers.Count - 1; i >= 0; i--)
            {
                KitStepper stepper = steppers[i];
                if (stepper.Root == null)
                {
                    steppers.RemoveAt(i);
                    continue;
                }
                try
                {
                    stepper.Pad(now);
                }
                catch (Exception e)
                {
                    CrucibleUiPlugin.Log?.Error("Crucible.UI: stepper pad failed: " + e);
                    stepper.PadSteps = false;
                }
            }
        }
    }
}
