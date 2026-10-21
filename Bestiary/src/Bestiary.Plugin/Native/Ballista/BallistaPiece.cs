// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Ballista/BallistaPiece.cs, Ballista/BallistaMissile.cs).
// Changed by expDARE, 2026: Bestiary namespace; no bundle model: the game ballista's own model and the game's own unused
// bone missile, bleached to bone at run time (BoneDye); the turret's parts and effects read off its Turret.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Runtime.Prefabs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>
    /// The Bone Ballista piece: a copy of the game's ballista (`piece_turret`), so it keeps its model, placement, its tab
    /// in the hammer (Misc), the station it is built at, its wear, hit and destroy effects and its player-base area. The
    /// game's turret brain goes (it aimed and shot by itself) and the player's hold takes its place
    /// (<see cref="BallistaControl"/>, drawn by <see cref="BallistaLook"/>); its range marker goes. Its parts (the neck
    /// that turns, the body that tilts, the eye it shoots from, the armed and unarmed bodies, the bone missile in the
    /// groove) are read off the turret before it goes. Bleached to bone, icon too. Half the game ballista's health.
    /// </summary>
    public static class BallistaPiece
    {
        public const string PrefabName = "ECP_BoneBallista";
        public const string Word = "piece_ecp_boneballista";
        public const string GamePiece = "piece_turret";
        private const float Health = 200f;

        public static GameObject? Prefab { get; private set; }

        public static void Build(ZNetScene scene)
        {
            GameObject? game = scene.GetPrefab(GamePiece);
            if (game == null || game.GetComponent<Turret>() == null || game.GetComponent<Piece>() == null)
            {
                Log.Error($"Bone ballista not built: the game has no {GamePiece} with its turret.");
                return;
            }
            GameObject piece = PrefabBench.Copy(game, PrefabName);
            Turret turret = piece.GetComponent<Turret>();
            Transform root = piece.transform;
            BallistaParts.BodyPath = BallistaParts.PathOf(root, turret.m_turretBody.transform);
            BallistaParts.NeckPath = BallistaParts.PathOf(root, turret.m_turretNeck.transform);
            BallistaParts.EyePath = BallistaParts.PathOf(root, turret.m_eye.transform);
            BallistaParts.ArmedPath = BallistaParts.PathOf(root, turret.m_turretBodyArmed != null ? turret.m_turretBodyArmed.transform : null);
            BallistaParts.UnarmedPath = BallistaParts.PathOf(root, turret.m_turretBodyUnarmed != null ? turret.m_turretBodyUnarmed.transform : null);
            Turret.AmmoType[] ammo = turret.m_allowedAmmo.Where(a => a.m_visual != null).ToArray();
            GameObject? laid = ammo.Where(a => a.m_ammo != null && a.m_ammo.name == BallistaMissile.GameItem).Select(a => a.m_visual).FirstOrDefault()
                ?? ammo.Select(a => a.m_visual).FirstOrDefault();
            BallistaParts.LaidPath = BallistaParts.PathOf(root, laid != null ? laid.transform : null);
            foreach (Turret.AmmoType each in ammo)
            {
                each.m_visual.SetActive(false);
            }
            (BallistaLook.Fire, BallistaLook.Latch, BallistaLook.Laid) = (turret.m_shootEffect, turret.m_reloadEffect, turret.m_addAmmoEffect);
            if (turret.m_marker != null && turret.m_marker.gameObject != piece)
            {
                Object.DestroyImmediate(turret.m_marker.gameObject);
            }
            Object.DestroyImmediate(turret);
            piece.AddComponent<BallistaControl>();
            piece.AddComponent<BallistaLook>();
            BoneDye.Dress(piece);
            Piece described = piece.GetComponent<Piece>();
            described.m_name = "$" + Word;
            described.m_description = "$" + Word + "_description";
            described.m_enabled = true;
            described.m_icon = BoneDye.Icon(described.m_icon);
            WearNTear? wear = piece.GetComponent<WearNTear>();
            if (wear != null)
            {
                wear.m_health = Health;
            }
            Prefab = piece;
        }
    }

    /// <summary>
    /// The Bone Missile, the Bone Ballista's ammunition, and its flight: a copy of the game's own unused bone missile
    /// (`TurretBoltBone`: a ballista missile with no name or recipe in the game) and its projectile, with an ammo type of
    /// its own, so only the Bone Ballista takes it (the game's ballista keeps its own list). It hits blunt, as bone does.
    /// Lighter than a wooden missile; the Crossbows skill rises with its hits. Without the game's bone missile, its
    /// wooden one is copied and bleached to bone.
    /// </summary>
    public static class BallistaMissile
    {
        public const string ItemName = "ECP_BoneMissile", ShotName = "ECP_BoneMissile_projectile";
        public const string Word = "item_ecp_bonemissile";
        public const string AmmoType = "$ammo_ecp_bonemissile";
        public const string GameItem = "TurretBoltBone", WoodItem = "TurretBoltWood";
        public const float Damage = 50f, Force = 40f, Speed = 45f;

        public static GameObject? Item { get; private set; }
        public static GameObject? Shot { get; private set; }

        public static void Build(ZNetScene scene)
        {
            GameObject? bone = scene.GetPrefab(GameItem), wood = scene.GetPrefab(WoodItem);
            GameObject? game = bone != null && bone.GetComponent<ItemDrop>() != null ? bone : wood;
            GameObject? flight = game?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_attack.m_attackProjectile;
            if (game == null || flight == null || flight.GetComponent<Projectile>() == null)
            {
                Log.Error($"Bone missiles not built: the game has no {GameItem} or {WoodItem} with its projectile.");
                return;
            }
            Shot = PrefabBench.Copy(flight, ShotName);
            Item = PrefabBench.Copy(game, ItemName);
            if (game != bone)
            {
                BoneDye.Dress(Shot);
                BoneDye.Dress(Item);
            }
            ItemDrop drop = Item.GetComponent<ItemDrop>();
            drop.m_itemData.m_dropPrefab = Item;
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            (shared.m_name, shared.m_description, shared.m_ammoType) = ("$" + Word, "$" + Word + "_description", AmmoType);
            shared.m_damages = new HitData.DamageTypes { m_blunt = Damage };
            (shared.m_attackForce, shared.m_skillType, shared.m_weight) = (Force, Skills.SkillType.Crossbows, 0.3f);
            shared.m_attack.m_attackProjectile = Shot;
            shared.m_attack.m_projectileVel = Speed;
            if (shared.m_icons == null || shared.m_icons.Length == 0 || shared.m_icons[0] == null || game != bone)
            {
                Sprite? woodIcon = wood?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_icons?.FirstOrDefault();
                Sprite? icon = BoneDye.Icon(woodIcon);
                shared.m_icons = icon != null ? new[] { icon } : shared.m_icons;
            }
        }
    }
}
