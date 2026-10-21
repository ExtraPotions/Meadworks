// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// <c>attack_speed</c> (Quickened) and <c>cast_speed</c> (Swift Casting):
    /// the local player's swing or cast with this weapon plays X% faster. A swing's timing in Valheim is its animation:
    /// the hit, the chain window and the end of the attack are animation events, so a faster animator is a faster
    /// attack; a burst attack's interval (staffs) is divided by the same factor. Heavy Hand (<c>heavy_hand</c>, X%) is
    /// the other side of its stagger bonus: the factor drops by X/3 %, and never goes below half speed.
    /// <para>
    /// Multiplayer: the attacker's own client owns its player, and ZSyncAnimation writes the owner's animator speed to
    /// the player ZDO, which every other client applies, so everyone sees the same swing speed. The game resets the
    /// speed to 1 when no attack plays (CharacterAnimEvent's fixed update, after which this applies the factor once the
    /// attack state is entered) and an attack clip may set its own speed by an animation event (scaled here too). A
    /// hit's freeze frame keeps the scaled speed to come back to. The factor is fixed at the swing's start.
    /// </para>
    /// </summary>
    internal static class SwingSpeed
    {
        private const float MinFactor = 0.5f;

        private static Attack? _attack;
        private static float _factor = 1f;
        private static bool _applied;

        /// <summary>Attack.Start succeeded for the local player's swing (the per-swing clone).</summary>
        public static void OnStart(Attack attack, ItemDrop.ItemData weapon)
        {
            _factor = Factor(ItemLocalCache.Get(weapon));
            _attack = _factor != 1f ? attack : null;
            _applied = false;
            if (_attack != null && attack.m_burstInterval > 0f)
            {
                attack.m_burstInterval /= _factor;
            }
        }

        // Quickened and Swift Casting speed the swing up; Heavy Hand slows it by a third of its share.
        private static float Factor(ItemLocalSums? sums)
        {
            if (sums == null)
            {
                return 1f;
            }
            float faster = UnityEngine.Mathf.Max(0f, sums.Get(EffectKind.AttackSpeed) + sums.Get(EffectKind.CastSpeed));
            return UnityEngine.Mathf.Max(MinFactor, 1f + faster - sums.Get(EffectKind.HeavyHand) / 3f);
        }

        /// <summary>After the anim event's fixed update: the swing's animation has begun, speed it up once.</summary>
        public static void AfterFixedUpdate(CharacterAnimEvent anim)
        {
            if (_attack == null || _applied || !IsLocal(anim))
            {
                return;
            }
            Player player = Player.m_localPlayer;
            if (!ReferenceEquals(player.m_currentAttack, _attack) || _attack.IsDone())
            {
                _attack = null;
                return;
            }
            if (player.InAttack())
            {
                SetSpeed(anim, _factor);
                _applied = true;
            }
        }

        /// <summary>An attack clip's own speed event while the sped-up swing plays: scale it too.</summary>
        public static void ScaleEvent(CharacterAnimEvent anim, ref float speed)
        {
            if (_applied && _attack != null && IsLocal(anim) && !_attack.IsDone()
                && ReferenceEquals(Player.m_localPlayer.m_currentAttack, _attack))
            {
                speed *= _factor;
            }
        }

        // During a hit's freeze frame the game restores the saved speed afterwards: set that one instead.
        private static void SetSpeed(CharacterAnimEvent anim, float speed)
        {
            if (anim.m_pauseTimer > 0f)
            {
                anim.m_pauseSpeed = speed;
            }
            else
            {
                anim.m_animator.speed = speed;
            }
        }

        private static bool IsLocal(CharacterAnimEvent anim)
        {
            Player? player = Player.m_localPlayer;
            return player != null && ReferenceEquals(anim.m_character, player);
        }
    }

    /// <summary>The local player's swing starts: decide its speed and publish its penetration (<see cref="Penetration"/>).</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    internal static class SwingStartPatch
    {
        private static void Postfix(Attack __instance, Humanoid character, ItemDrop.ItemData weapon, bool __result)
        {
            Player? player = Player.m_localPlayer;
            if (!__result || weapon == null || player == null || !ReferenceEquals(character, player))
            {
                return;
            }
            SwingSpeed.OnStart(__instance, weapon);
            Penetration.Publish(player, weapon);
        }
    }

    [HarmonyPatch]
    internal static class SwingAnimationPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.CustomFixedUpdate))]
        private static void FixedUpdate(CharacterAnimEvent __instance) => SwingSpeed.AfterFixedUpdate(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.Speed))]
        private static void SpeedEvent(CharacterAnimEvent __instance, ref float speedScale) => SwingSpeed.ScaleEvent(__instance, ref speedScale);
    }
}
