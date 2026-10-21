// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// A Screecher's shriek on the deafened player's own machine, the only place it can be felt: for as long as the
    /// "Ringing ears" status effect lasts (<see cref="ShriekStatus"/>: its icon and countdown in the HUD), the game's
    /// sound falls near-silent (<see cref="ShriekHush"/>) under a faint ringing (<see cref="ShriekRing"/>), and a weapon
    /// of Elemental or Blood Magic will not cast (<c>ShriekCastPatch</c> refuses the attack with a word to the player; a
    /// cast already looping is broken off). The status effect is the one clock: a second shriek extends it, death clears
    /// it with every other effect, and the moment it is gone this component gives the sound back and removes itself -
    /// as it also does when the player object goes, at logout or a scene change. While the game menu is open the sound
    /// passes as normal, so the volume settings can be heard. Lives on this machine's own player only.
    /// </summary>
    public sealed class ShriekDeafness : MonoBehaviour
    {
        private const string CastRefused = "Your ears ring - you cannot cast";

        /// <summary>Seconds between two refusal messages, since a held attack button asks again every physics step.</summary>
        private const float WarnEvery = 2f;

        /// <summary>True while this machine's player is deafened: the cast patch's one-bool gate for every other attack.</summary>
        public static bool Active { get; private set; }

        private readonly ShriekHush _hush = new ShriekHush();
        private Player _player = null!;
        private ShriekRing? _ring;
        private float _warnedAt = float.NegativeInfinity;
        private bool _ended;

        /// <summary>On the deafened player's own machine: deafen <paramref name="player"/>, or keep them deaf longer.</summary>
        public static void Apply(Player? player, float seconds)
        {
            // Ghost mode lives only on the player's own machine (the shrieker's owner reads it as off), so it is asked here.
            if (player == null || player.IsDead() || player.InGhostMode() || seconds <= 0f
                || ShriekStatus.Give(player, seconds) == null)
            {
                return;
            }
            ShriekDeafness deafness = player.GetComponent<ShriekDeafness>();
            if (deafness == null || deafness._ended) // one that ended this very frame is gone at its end: start afresh
            {
                deafness = player.gameObject.AddComponent<ShriekDeafness>();
            }
            deafness.BreakCast();
        }

        /// <summary>The cast patch: true, with a word to the player, when their attack is a spell they cannot cast now.</summary>
        public static bool RefusesCast(Humanoid caster)
        {
            ShriekDeafness deafness = caster.GetComponent<ShriekDeafness>();
            if (deafness == null || !IsSpell(caster.GetCurrentWeapon()))
            {
                return false;
            }
            deafness.Warn();
            return true;
        }

        private static bool IsSpell(ItemDrop.ItemData? weapon) =>
            weapon != null && (weapon.m_shared.m_skillType == Skills.SkillType.ElementalMagic
                || weapon.m_shared.m_skillType == Skills.SkillType.BloodMagic);

        private void Awake()
        {
            _player = GetComponent<Player>();
            Active = true;
            _ring = ShriekRing.Begin(transform);
        }

        private void Update() => Guard.Run("ShriekDeafness.Update", static self => self.Step(), this);

        private void Step()
        {
            StatusEffect? ringing = _player != null && !_player.IsDead() ? ShriekStatus.On(_player) : null;
            if (ringing == null)
            {
                End(); // worn off, or cleared by death
                Destroy(this);
                return;
            }
            float level = Menu.IsVisible() ? 1f : ShriekHush.Level(ringing.GetDuration(), ringing.GetRemaningTime());
            _hush.Hold(level);
            _ring?.Follow(level);
            BreakCast();
        }

        // A looping cast (a staff's stream) runs on without a new attack, so it is broken off rather than refused.
        private void BreakCast()
        {
            Attack? attack = _player != null ? _player.m_currentAttack : null;
            if (attack != null && attack.m_loopingAttack && !attack.IsDone() && IsSpell(attack.GetWeapon()))
            {
                attack.Abort();
            }
        }

        private void Warn()
        {
            if (Time.time - _warnedAt >= WarnEvery)
            {
                _warnedAt = Time.time;
                _player.Message(MessageHud.MessageType.Center, CastRefused);
            }
        }

        /// <summary>Gives the sound back and stops the ringing, once; a second call (its own OnDestroy) does nothing.</summary>
        private void End()
        {
            if (_ended)
            {
                return;
            }
            _ended = true;
            Active = false;
            _hush.Release();
            _ring?.End();
            _ring = null;
        }

        private void OnDestroy() => End();
    }
}
