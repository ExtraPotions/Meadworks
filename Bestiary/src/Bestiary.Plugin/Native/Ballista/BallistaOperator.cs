// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Ballista/BallistaOperator.cs, Ballista/BallistaShot.cs).
// Changed by expDARE, 2026: Bestiary namespace; aimed and shot from the game turret's own body and eye; no foot walking.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>
    /// The holder's machine, which owns the ballista's ZDO while holding it: the aim follows the view within 45 degrees
    /// either side of where it was placed (90 in all), 20 down and 25 up; the reload runs by itself whenever the holder
    /// carries a Bone Missile (pull the string to the latch, then lay a missile, taken from the inventory as it is laid);
    /// attack shoots a laid missile (<see cref="Fire"/>).
    /// </summary>
    public static class BallistaOperator
    {
        public const float YawLimit = 45f, PitchDown = -20f, PitchUp = 25f;
        /// <summary>The fastest the view and the ballista turn.</summary>
        public const float YawRate = 240f;

        private const float CatchUp = 720f, PitchRate = 200f, Reach = 150f, WriteStep = 0.1f, Noise = 10f;
        private static int aimMask;

        public static void Tick(BallistaControl c, Player p)
        {
            if (c.Parts == null || !c.Net.IsOwner())
            {
                return;   // ownership is on its way from the old owner
            }
            Reload(c, p, c.State);
        }

        /// <summary>The holder's aim, each drawn frame: the yaw follows the view's own, the pitch lays the groove on what the crosshair is on.</summary>
        public static void Aim(BallistaControl c, Player p, float dt)
        {
            if (c.Parts == null || !c.Net.IsValid())
            {
                return;
            }
            float yaw = Mathf.Clamp(Mathf.DeltaAngle(c.transform.eulerAngles.y, p.m_lookYaw.eulerAngles.y), -YawLimit, YawLimit);
            c.Yaw = Mathf.MoveTowards(c.Yaw, yaw, CatchUp * dt);
            c.Pitch = Mathf.MoveTowards(c.Pitch, Elevation(c), PitchRate * dt);
            if (c.Net.IsOwner())
            {
                Write(c.Net.GetZDO(), BallistaKeys.Yaw, c.Yaw);
                Write(c.Net.GetZDO(), BallistaKeys.Pitch, c.Pitch);
            }
        }

        private static void Write(ZDO zdo, int key, float value)
        {
            if (Mathf.Abs(zdo.GetFloat(key) - value) > WriteStep)
            {
                zdo.Set(key, value);
            }
        }

        /// <summary>The pitch that lays the groove on what the crosshair is on, within the ballista's tilt.</summary>
        private static float Elevation(BallistaControl c)
        {
            if (GameCamera.instance == null)
            {
                return c.Pitch;
            }
            Transform camera = GameCamera.instance.transform;
            Vector3 pivot = c.Parts!.Body.position;
            Vector3 from = camera.position + camera.forward * Mathf.Max(0f, Vector3.Dot(pivot - camera.position, camera.forward) + 1f);
            aimMask = aimMask != 0 ? aimMask : LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle", "character", "character_net", "character_ghost");
            Vector3 target = Physics.Raycast(from, camera.forward, out RaycastHit hit, Reach, aimMask) ? hit.point : from + camera.forward * Reach;
            Vector3 run = target - pivot;
            float pitch = Mathf.Atan2(run.y, new Vector2(run.x, run.z).magnitude) * Mathf.Rad2Deg;
            return Mathf.Clamp(pitch, PitchDown, PitchUp);
        }

        /// <summary>Finishes an action that ran its time; else starts the next one when the holder has a missile.</summary>
        private static void Reload(BallistaControl c, Player p, in BallistaState s)
        {
            if (s.Busy)
            {
                return;
            }
            if (s.Act != Act.None)
            {
                Set(c, s.Settled, Act.None);
                return;
            }
            ItemDrop.ItemData? missile = s.Spring == Spring.Loaded || s.ShotAge < BallistaTimeline.Settle ? null : Missile(p);
            if (missile == null)
            {
                return;
            }
            if (s.Spring == Spring.Drawn)
            {
                p.GetInventory().RemoveItem(missile, 1);
            }
            Set(c, s.Spring, s.Spring == Spring.Slack ? Act.Pulling : Act.Loading);
            c.Net.GetZDO().Set(BallistaKeys.ActAt, BallistaKeys.Now());
        }

        /// <summary>Attack: shoot a laid missile; with nothing laid and nothing to lay, say so.</summary>
        public static void Trigger(BallistaControl c, Player p)
        {
            if (c.Parts == null || !c.Net.IsValid() || !c.Net.IsOwner())
            {
                return;
            }
            BallistaState s = c.State;
            if (s.Settled == Spring.Loaded && !s.Busy)
            {
                Fire(c, p);
            }
            else if (!s.Busy && Missile(p) == null)
            {
                p.Message(MessageHud.MessageType.Center, "$msg_ecp_bal_nomissiles");
            }
        }

        /// <summary>
        /// A shot, on the holder's machine: the flying missile starts at the turret's eye, along it, owned by the holder
        /// (their hit, their Crossbows skill), built from the missile's numbers as the game's ballista builds its shot; a
        /// networked object, so every peer sees it fly. The ZDO then says the string is slack and counts the shot.
        /// </summary>
        public static void Fire(BallistaControl c, Player p)
        {
            ItemDrop? missile = BallistaMissile.Item != null ? BallistaMissile.Item.GetComponent<ItemDrop>() : null;
            if (BallistaMissile.Shot == null || missile == null || c.Parts == null)
            {
                return;
            }
            Transform eye = c.Parts.Eye;
            GameObject shot = Object.Instantiate(BallistaMissile.Shot, eye.position, eye.rotation);
            ItemDrop.ItemData data = missile.m_itemData;
            shot.GetComponent<IProjectile>()?.Setup(p, eye.forward * data.m_shared.m_attack.m_projectileVel, Noise, Hit(data), null, data);
            ZDO zdo = c.Net.GetZDO();
            zdo.Set(BallistaKeys.Spring, (int)Spring.Slack);
            zdo.Set(BallistaKeys.Act, (int)Act.None);
            zdo.Set(BallistaKeys.Shots, zdo.GetInt(BallistaKeys.Shots) + 1);
            zdo.Set(BallistaKeys.ShotAt, BallistaKeys.Now());
        }

        private static HitData Hit(ItemDrop.ItemData missile)
        {
            ItemDrop.ItemData.SharedData shared = missile.m_shared;
            var hit = new HitData
            {
                m_toolTier = (short)shared.m_toolTier,
                m_pushForce = shared.m_attackForce,
                m_backstabBonus = shared.m_backstabBonus,
                m_staggerMultiplier = shared.m_attack.m_staggerMultiplier,
                m_blockable = shared.m_blockable,
                m_dodgeable = shared.m_dodgeable,
                m_skill = shared.m_skillType,
                m_hitType = HitData.HitType.PlayerHit,
                m_itemWorldLevel = (byte)Game.m_worldLevel,
            };
            hit.m_damage.Add(missile.GetDamage());
            return hit;
        }

        /// <summary>The owner, as the holder lets go: an action under way is done at once, and nobody holds it.</summary>
        public static void LetGo(BallistaControl c)
        {
            BallistaState s = c.State;
            if (s.Act != Act.None)
            {
                Set(c, s.Act == Act.Pulling ? Spring.Drawn : Spring.Loaded, Act.None);
            }
            c.Net.GetZDO().Set(BallistaKeys.User, 0L);
        }

        private static ItemDrop.ItemData? Missile(Player p) => p.GetInventory().GetAmmoItem(BallistaMissile.AmmoType);

        private static void Set(BallistaControl c, Spring spring, Act act)
        {
            ZDO zdo = c.Net.GetZDO();
            zdo.Set(BallistaKeys.Spring, (int)spring);
            zdo.Set(BallistaKeys.Act, (int)act);
        }
    }
}
