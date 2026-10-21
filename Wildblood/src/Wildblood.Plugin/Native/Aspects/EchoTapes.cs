// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The tapes being recorded on this machine - one per Echoing boss it owns (<see cref="EchoBehaviour"/>) - found by
    /// the boss and by its animation sync, so the patches on the game's attack start and animation cues
    /// (<see cref="Patches.EchoAttackPatch"/>, <see cref="Patches.EchoCuePatch"/>) find the tape in one lookup, and cost
    /// one count check while none is recorded. The game sends a creature's cues from its owner only (the cue then travels
    /// to every machine), so recording here catches every cue. While an attack is being started its cues belong to the
    /// attack, not to the tape on their own: the echo's own start of that attack sends them again.
    /// </summary>
    internal static class EchoTapes
    {
        private static readonly Dictionary<Humanoid, EchoTape> ByBoss = new Dictionary<Humanoid, EchoTape>();
        private static readonly Dictionary<ZSyncAnimation, EchoTape> ByAnim = new Dictionary<ZSyncAnimation, EchoTape>();
        private static ZSyncAnimation? _startingAnim;
        private static EchoAttack? _starting;

        public static bool Any => ByBoss.Count > 0;

        public static void Join(Character boss, ZSyncAnimation anim, EchoTape tape)
        {
            if (boss is Humanoid humanoid)
            {
                ByBoss[humanoid] = tape;
            }
            ByAnim[anim] = tape;
        }

        // `is null` rather than Unity's ==: the boss may already read as destroyed here, and its entries must go.
        public static void Leave(Character? boss, ZSyncAnimation? anim)
        {
            if (boss is Humanoid humanoid)
            {
                ByBoss.Remove(humanoid);
            }
            if (anim is not null)
            {
                ByAnim.Remove(anim);
            }
        }

        /// <summary>One sample of the boss as it stands now.</summary>
        public static void Record(Character boss, ZSyncAnimation anim, EchoTape tape)
        {
            EchoParams.Read(anim, tape.NextParams);
            BaseAI ai = boss.GetBaseAI();
            Character? target = ai != null ? ai.GetTargetCreature() : null;
            tape.Commit(Time.time, new EchoPose(boss.transform.position, boss.transform.rotation, boss.GetLookDir(), target));
        }

        /// <summary>An animation cue sent to a creature: kept when it is a recorded boss's.</summary>
        public static void Cue(ZSyncAnimation anim, string name)
        {
            if (!ByAnim.TryGetValue(anim, out EchoTape tape))
            {
                return;
            }
            if (_starting != null && ReferenceEquals(anim, _startingAnim))
            {
                _starting.Cues.Add(name);
                return;
            }
            tape.Add(new EchoEvent { Time = Time.time, Trigger = name });
        }

        /// <summary>A recorded boss begins to start an attack: what it swings and the random state it starts with.</summary>
        public static void AttackStarting(Humanoid boss, bool secondary)
        {
            if (!ByBoss.ContainsKey(boss))
            {
                return;
            }
            ItemDrop.ItemData weapon = boss.GetCurrentWeapon();
            _starting = new EchoAttack { Weapon = weapon != null ? weapon.m_shared.m_name : "", Secondary = secondary,
                Seed = Random.state };
            _startingAnim = boss.m_zanim;
        }

        /// <summary>The start is over: a started attack goes on the tape; a refused one leaves nothing.</summary>
        public static void AttackStarted(Humanoid boss, bool started)
        {
            EchoAttack? attack = _starting;
            _starting = null;
            _startingAnim = null;
            if (attack != null && started && ByBoss.TryGetValue(boss, out EchoTape tape))
            {
                tape.Add(new EchoEvent { Time = Time.time, Attack = attack });
            }
        }
    }
}
