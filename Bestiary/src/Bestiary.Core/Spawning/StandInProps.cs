// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Bestiary.Spawning
{
    /// <summary>Game prefab names used as stand-in props. No game mesh is shipped with Bestiary.</summary>
    public static class StandInProps
    {
        private static readonly string[] None = Array.Empty<string>();
        private static readonly string[] Hook = { "PickaxeAntler", "AxeFlint", "FishingRod" };
        private static readonly string[] Plate = { "ShieldBronzeBuckler", "ShieldIronBuckler", "ArmorIronChest" };
        private static readonly string[] Lantern = { "Lantern_DN", "Lantern", "piece_dvergr_lantern" };

        public static string[] Names(string creature)
        {
            if (creature == KitCreatures.BogHook) return Hook;
            if (creature == KitCreatures.SlagCharred) return Plate;
            if (creature == KitCreatures.MarrowLantern) return Lantern;
            return None;
        }

        /// <summary>Props parented on the body. The Slag Charred wears three plates. The Spearman wears none. The others wear one.</summary>
        public static int Copies(string creature)
        {
            if (creature == KitCreatures.SlagCharred) return 3;
            if (creature == KitCreatures.Javelineer) return 0;
            return 1;
        }

        public const float HookMeters = 1.2f;
        public const float LanternMeters = 0.5f;
        public const float PlateMeters = 0.45f;

        /// <summary>Longest axis, in meters, once the bone scale is divided out.</summary>
        public static float Meters(string creature)
        {
            if (creature == KitCreatures.BogHook) return HookMeters;
            if (creature == KitCreatures.MarrowLantern) return LanternMeters;
            if (creature == KitCreatures.SlagCharred) return PlateMeters;
            return 1f;
        }

        /// <summary>Meters in creature space: right, up, forward.</summary>
        public readonly struct Grip
        {
            public readonly float Right;
            public readonly float Up;
            public readonly float Forward;

            public Grip(float right, float up, float forward)
            {
                Right = right;
                Up = up;
                Forward = forward;
            }
        }

        /// <summary>The item attach origin is the game's grip. The lantern sits a few centimeters into the palm. No hand: the right side of the torso.</summary>
        public static Grip HoldGrip(string creature, bool onBody)
        {
            if (onBody) return new Grip(0.35f, 1.3f, 0.25f);
            if (creature == KitCreatures.MarrowLantern) return new Grip(0.02f, -0.05f, 0.06f);
            return new Grip(0f, 0f, 0f);
        }

        /// <summary>Front, then left, then right, in meters out from the chest bone. Sides sit 0.79 m out and 0.12 m forward so the idle forearm clears the plate. The front plate stays on the chest. <paramref name="onBody"/> lifts them when the rig has no chest bone.</summary>
        public static Grip PlateGrip(int index, bool onBody)
        {
            var grip = index == 1 ? new Grip(-0.79f, 0.29f, 0.12f)
                : index == 2 ? new Grip(0.79f, 0.29f, 0.12f)
                : new Grip(0f, 0.31f, 0.30f);
            return onBody ? new Grip(grip.Right, grip.Up + 1.55f, grip.Forward) : grip;
        }

        /// <summary>First registered name, or null. When <paramref name="allowsUi"/> is false the lookup is not called.</summary>
        public static string? Choose(string creature, bool allowsUi, Func<string, bool> registered)
        {
            if (!allowsUi || registered == null) return null;
            var names = Names(creature);
            for (int i = 0; i < names.Length; i++)
                if (registered(names[i])) return names[i];
            return null;
        }
    }
}
