// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Display/DefenseEffects.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The HUD icons of Defense's timed states, on the local player's own client: Riposte armed (seconds left), Shield
    /// Wall (sheltered), Hardened (stacks), Last Stand (invulnerable, seconds left), Last Stand recovering (its
    /// cooldown) and Desperation. Each is a <see cref="DefenseStatus"/> added to the player's SEMan while its state
    /// holds and gone as soon as it ends. They are added four times a second, and at once by the feature that just
    /// started one (<see cref="Refresh"/>), so a death that cleared them brings back those still running. Icons are item
    /// icons from the game's own items, else the Defense icon.
    /// </summary>
    public static class DefenseEffects
    {
        private const float Interval = 0.25f;

        private static readonly DefenseEffect[] Effects =
        {
            new DefenseEffect("saga.defense_riposte", "gs_6e1bdc294002f23d", "SwordIron",
                () => Riposte.ArmedFor > 0f, () => Seconds(Riposte.ArmedFor)),
            new DefenseEffect("saga.defense_shieldwall", "gs_8d61ed61f25318cf", "ShieldWood",
                () => ShieldWall.Sheltered(Player.m_localPlayer), null),
            new DefenseEffect("saga.defense_hardened", "gs_c590bc54b8fa963d", "ArmorIronChest",
                () => Hardened.Stacks > 0, () => $"{Hardened.Stacks}x"),
            new DefenseEffect("saga.defense_laststand", "gs_cfe83e821b0faa83", "HelmetDrake",
                () => LastStand.Invulnerable, () => Seconds(LastStand.InvulnerableFor)),
            new DefenseEffect("saga.defense_recovering", "gs_dfeb0639556b3979", "HelmetDrake",
                () => LastStand.Unlocked && LastStand.CooldownLeft > 0f, () => StatusEffect.GetTimeString(LastStand.CooldownLeft), true),
            new DefenseEffect("saga.defense_desperation", "gs_1dc26477ad0fa0c9", "Bloodbag",
                () => Desperation.Holds(Player.m_localPlayer), null),
        };

        private static float timer;

        /// <summary>Every frame for the local player (<see cref="LocalPlayerTick"/>).</summary>
        public static void Tick(float dt)
        {
            timer += dt;
            if (timer < Interval)
                return;
            timer = 0f;
            HookGuard.Run("defense effects", Refresh);
        }

        /// <summary>Adds the icon of every state that holds and has none yet.</summary>
        public static void Refresh()
        {
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || !DefenseSkill.Active)
                return;
            SEMan seman = player.GetSEMan();
            foreach (DefenseEffect effect in Effects)
            {
                if (effect.Showing()) {
                    var status = seman.GetStatusEffect(effect.Hash) ?? seman.AddStatusEffect(effect.Template());
                    if (status != null) status.m_name = effect.Title;
                }
            }
        }

        public static void Shutdown(Player player) { foreach (var effect in Effects) { if (player != null) player.GetSEMan().RemoveStatusEffect(effect.Hash); effect.Dispose(); } timer = 0; }

        private static string Seconds(float seconds) => $"{Mathf.CeilToInt(seconds)}s";

        private sealed class DefenseEffect
        {
            private readonly string id;
            private readonly string title;
            private readonly string iconItem;
            private readonly Func<string> label;
            private readonly bool cooldown;
            private DefenseStatus template;

            public DefenseEffect(string id, string title, string iconItem, Func<bool> showing, Func<string> label, bool cooldown = false)
            {
                this.id = id;
                this.title = title;
                this.iconItem = iconItem;
                Showing = showing;
                this.label = label;
                this.cooldown = cooldown;
                Hash = id.GetStableHashCode();
            }

            public Func<bool> Showing { get; }
            public int Hash { get; }
            public string Title => SagaText.Text(title);

            /// <summary>The effect SEMan clones, made once; ScriptableObjects outlive scene loads.</summary>
            public DefenseStatus Template()
            {
                if (template != null)
                    return template;
                template = ScriptableObject.CreateInstance<DefenseStatus>();
                template.name = id;
                template.m_name = SagaText.Text(title);
                template.m_icon = Icon();
                template.m_cooldownIcon = cooldown;
                template.Showing = Showing;
                template.Label = label;
                return template;
            }

            public void Dispose() { if (template != null) UnityEngine.Object.Destroy(template); template = null; }

            private Sprite Icon()
            {
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(iconItem) : null;
                Sprite[] icons = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons : null;
                return icons != null && icons.Length > 0 && icons[0] != null ? icons[0] : DefenseIcon.Find();
            }
        }
    }
}
