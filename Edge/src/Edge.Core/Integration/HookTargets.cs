// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the game members Edge patches or reads (design section 8), with their exact metadata signatures.
// The portable tests check every entry against assembly_valheim.dll (System.Reflection.Metadata, no game load).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Edge.Integration
{
    public enum HookKind { Method, Field }

    /// <summary>Wired: the plugin uses the member now. Pending: listed for a later native unit.</summary>
    public enum HookStatus { Wired, Pending }

    public sealed class HookTarget
    {
        internal HookTarget(string module, string type, string member, HookKind kind, string signature, HookStatus status, string purpose)
        {
            Module = module;
            Type = type;
            Member = member;
            Kind = kind;
            Signature = signature;
            Status = status;
            Purpose = purpose;
        }

        public string Module { get; }

        /// <summary>Metadata type name, nested types joined with <c>/</c> (for example <c>ItemDrop/ItemData</c>).</summary>
        public string Type { get; }

        public string Member { get; }

        public HookKind Kind { get; }

        /// <summary>Methods: comma-joined parameter types; fields: the field type. Metadata short names, nested with <c>/</c>, by-ref with <c>&amp;</c>.</summary>
        public string Signature { get; }

        public HookStatus Status { get; }

        public string Purpose { get; }

        public override string ToString() => Type + "." + Member + (Kind == HookKind.Method ? "(" + Signature + ")" : " : " + Signature);
    }

    public static class HookTargets
    {
        private static HookTarget M(string module, string type, string member, string parameters, HookStatus status, string purpose) =>
            new HookTarget(module, type, member, HookKind.Method, parameters, status, purpose);

        private static HookTarget F(string module, string type, string member, string fieldType, HookStatus status, string purpose) =>
            new HookTarget(module, type, member, HookKind.Field, fieldType, status, purpose);

        public static IReadOnlyList<HookTarget> All { get; } = Array.AsReadOnly(new[]
        {
            M("Slope", "Attack", "DoMeleeAttack", "", HookStatus.Wired, "prefix sets the local player's sweep, finalizer restores it (B1 to B3)"),
            F("Slope", "Attack", "m_attackHeight", "Single", HookStatus.Wired, "sweep start height"),
            F("Slope", "Attack", "m_attackOffset", "Single", HookStatus.Wired, "sweep forward offset"),
            F("Slope", "Attack", "m_maxYAngle", "Single", HookStatus.Wired, "Adaptive pitch limit"),
            F("Slope", "Attack", "m_character", "Humanoid", HookStatus.Wired, "attacker of the melee call"),
            M("Slope", "Character", "GetEyePoint", "", HookStatus.Wired, "look ray origin"),
            M("Slope", "Character", "GetLookDir", "", HookStatus.Wired, "look ray direction"),
            M("Slope", "Character", "GetCenterPoint", "", HookStatus.Wired, "aimed target vertical offset"),
            F("Slope", "Character", "m_lastGroundNormal", "Vector3", HookStatus.Wired, "ground slope for Adaptive"),

            M("Secondaries", "Attack", "OnAttackTrigger", "", HookStatus.Wired, "configured source trigger scope"),
            M("Secondaries", "Attack", "FireProjectileBurst", "", HookStatus.Wired, "ranged preset launch dispatcher"),
            M("Secondaries", "Projectile", "UpdateVisual", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Attack", "Start", "Humanoid,Rigidbody,ZSyncAnimation,CharacterAnimEvent,VisEquipment,ItemDrop/ItemData,Attack,Single,Single", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Character", "Awake", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Character", "GetFaction", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Character", "Damage", "HitData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Character", "UpdateWalking", "Single", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Character", "UpdateFlying", "Single", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Character", "UpdateSwimming", "Single", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "EnemyHud", "LateUpdate", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "TextsDialog", "UpdateTextsList", "", HookStatus.Wired, "append the Secondary Attacks Compendium page"),
            M("Secondaries", "TextsDialog", "ShowText", "TextsDialog/TextInfo", HookStatus.Wired, "refresh the Secondary Attacks page in the current language"),
            M("Secondaries", "Skills", "RaiseSkill", "Skills/SkillType,Single", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Humanoid", "GetTimeSinceLastAttack", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "CharacterAnimEvent", "CustomFixedUpdate", "Single", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ObjectDB", "Awake", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ObjectDB", "CopyOtherDB", "ObjectDB", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ZNetScene", "Awake", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ZNetView", "Awake", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "SpawnAbility", "Setup", "Character,Vector3,Single,HitData,ItemDrop/ItemData,ItemDrop/ItemData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "SpawnAbility", "SetupAoe", "Character,Vector3", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "SpawnAbility", "Spawn", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Tameable", "RPC_Command", "Int64,ZDOID,Boolean", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ZRoutedRpc", ".ctor", "Boolean", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ZDOMan", "HandleDestroyedZDO", "ZDOID", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ZDOMan", "Load", "BinaryReader,Version/World", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "ZDOMan", "LoadChunks", "String,FileHelpers/FileSource,Version/World", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "SEMan", "Internal_AddStatusEffect", "Int32,Boolean,Int32,Single,Int16", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "SEMan", "RemoveStatusEffect", "Int32,Boolean", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "SE_Shield", "OnDamaged", "HitData,Character", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "SE_Shield", "IsDone", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Attack", "DoAreaAttack", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Attack", "ProjectileAttackTriggered", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Humanoid", "IsBlocking", "", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "Destructible", "Damage", "HitData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "MineRock", "Damage", "HitData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "MineRock5", "Damage", "HitData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "TreeBase", "Damage", "HitData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "TreeLog", "Damage", "HitData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            M("Secondaries", "WearNTear", "Damage", "HitData", HookStatus.Wired, "pinned native execution and lifecycle adapter"),
            F("Secondaries", "ZRoutedRpc", "m_functions", "Dictionary`2<Int32,RoutedMethodBase>", HookStatus.Wired, "two routed registrations captured and removed only by exact identity"),
            M("Secondaries", "Character", "UseHealth", "Single", HookStatus.Wired, "source Blood Magic health delta award once through native RaiseSkill"),
            M("Secondaries", "Attack", "GetAttackHealth", "", HookStatus.Wired, "Blood Magic maximum health percentage cost"),
            M("Secondaries", "Attack", "GetAttackEitr", "", HookStatus.Wired, "secondary instance eitr cost"),
            M("Secondaries", "Player", "UpdateAttackBowDraw", "ItemDrop/ItemData,Single", HookStatus.Wired, "draw held secondary release"),
            M("Secondaries", "Player", "SetWeaponLoaded", "ItemDrop/ItemData", HookStatus.Wired, "persist reload state"),
            M("Secondaries", "Player", "Update", "", HookStatus.Wired, "hold repeat and sticky input"),
            M("Secondaries", "Player", "AlwaysRotateCamera", "", HookStatus.Wired, "burst aim"),
            M("Secondaries", "Player", "AddAdrenaline", "Single", HookStatus.Wired, "scoped attack adrenaline"),
            M("Secondaries", "Projectile", "Setup", "Character,Vector3,Single,HitData,ItemDrop/ItemData,ItemDrop/ItemData", HookStatus.Wired, "projectile attribution"),
            M("Secondaries", "Projectile", "OnHit", "Collider,Vector3,Boolean,Vector3", HookStatus.Wired, "ricochet piercing and bomb scopes"),
            M("Secondaries", "Aoe", "Setup", "Character,Vector3,Single,HitData,ItemDrop/ItemData,ItemDrop/ItemData", HookStatus.Wired, "bomb area visual scope"),
            M("Secondaries", "EffectList", "Create", "Vector3,Quaternion,Transform,Single,Int32,ZDOID", HookStatus.Wired, "bomb created effect scope"),
            M("Secondaries", "Humanoid", "StartAttack", "Character,Boolean", HookStatus.Wired, "secondary dispatch (D04, D05)"),
            F("Secondaries", "ItemDrop/ItemData/SharedData", "m_secondaryAttack", "Attack", HookStatus.Wired, "source prefab apply restore"),
            F("Secondaries", "ItemDrop/ItemData/SharedData", "m_attack", "Attack", HookStatus.Wired, "native source primary attack facts"),
            F("Secondaries", "ItemDrop/ItemData/SharedData", "m_skillType", "Skills/SkillType", HookStatus.Wired, "native source weapon facts"),
            F("Secondaries", "ItemDrop/ItemData/SharedData", "m_itemType", "ItemDrop/ItemData/ItemType", HookStatus.Wired, "native source weapon facts"),
            F("Secondaries", "ItemDrop/ItemData/SharedData", "m_ammoType", "String", HookStatus.Wired, "native source crossbow family rule"),
            M("Secondaries", "Player", "UpdateWeaponLoading", "ItemDrop/ItemData,Single", HookStatus.Wired, "reload weapons keep a loaded shot (A7)"),
            M("Secondaries", "KeyHints", "Awake", "", HookStatus.Wired, "owned native hint cells"),
            M("Secondaries", "KeyHints", "UpdateHints", "", HookStatus.Wired, "source keyboard/gamepad hint refresh"),
            M("Secondaries", "ItemDrop/ItemData", "GetTooltip", "ItemDrop/ItemData,Int32,Boolean,Single,Int32,Boolean", HookStatus.Wired, "source preset descriptions and native readonly costs"),

            M("Quickstep", "Player", "UpdateDodge", "Single", HookStatus.Wired, "quickstep state machine (D07b)"),
            M("Quickstep", "Player", "OnDeath", "", HookStatus.Wired, "controller cleanup on death"),
            M("Quickstep", "Player", "OnSneaking", "Single", HookStatus.Wired, "pause sneaking updates during the owned dash"),
            M("Quickstep", "Player", "GetDodgeStaminaUse", "", HookStatus.Wired, "60 % stamina share"),
            M("DodgeCancel", "Player", "SetControls", "Vector3,Boolean,Boolean,Boolean,Boolean,Boolean,Boolean,Boolean,Boolean,Boolean,Boolean,Boolean", HookStatus.Wired, "native block input edge and toggle result"),
            M("DodgeCancel", "Attack", "Stop", "", HookStatus.Wired, "native attack cleanup without refund"),
            F("DodgeCancel", "Player", "m_cachedFrame", "Int32", HookStatus.Wired, "invalidate attack animation cache"),
            F("DodgeCancel", "Character", "m_cachedAnimHashFrame", "Int32", HookStatus.Wired, "invalidate native animation-tag cache"),
            F("Quickstep", "Player", "m_dodgeInvincible", "Boolean", HookStatus.Wired, "dash invincibility"),
            F("Quickstep", "Player", "m_queuedDodgeTimer", "Single", HookStatus.Wired, "dodge press and roll handoff"),

            M("SwimReequip", "Humanoid", "UpdateEquipment", "Single", HookStatus.Wired, "scope only the vanilla swim hide call"),
            M("SwimReequip", "Humanoid", "HideHandItems", "Boolean,Boolean", HookStatus.Wired, "hide caused by swimming (D09b)"),
            M("SwimReequip", "Humanoid", "ShowHandItems", "Boolean,Boolean", HookStatus.Wired, "restore after the delay"),
            F("SwimReequip", "Humanoid", "m_hiddenLeftItem", "ItemDrop/ItemData", HookStatus.Wired, "hidden items"),
            F("SwimReequip", "Humanoid", "m_hiddenRightItem", "ItemDrop/ItemData", HookStatus.Wired, "hidden items"),
            M("SwimReequip", "Character", "IsSwimming", "", HookStatus.Wired, "swim state"),

            M("LastShield", "Humanoid", "EquipItem", "ItemDrop/ItemData,Boolean", HookStatus.Wired, "remember and bring back the shield (D09b)"),
            F("LastShield", "Humanoid", "m_leftItem", "ItemDrop/ItemData", HookStatus.Wired, "left hand empty check"),
            F("LastShield", "Humanoid", "m_rightItem", "ItemDrop/ItemData", HookStatus.Wired, "equipped weapon"),
            F("LastShield", "ItemDrop/ItemData", "m_customData", "Dictionary`2<String,String>", HookStatus.Wired, "shield identity key"),
            F("LastShield", "Player", "m_customData", "Dictionary`2<String,String>", HookStatus.Wired, "player selection key"),
            M("LoadedCrossbow", "Humanoid", "UnequipItem", "ItemDrop/ItemData,Boolean", HookStatus.Wired, "note a loaded crossbow before the game forgets it (ECR 3.21); EquipItem postfix shares the LastShield entry"),
            M("LoadedCrossbow", "Humanoid", "IsItemEquiped", "ItemDrop/ItemData", HookStatus.Wired, "reload only the weapon still in hand"),
            F("LoadedCrossbow", "Player", "m_weaponLoaded", "ItemDrop/ItemData", HookStatus.Wired, "the game's loaded weapon"),
            F("LoadedCrossbow", "Attack", "m_requiresReload", "Boolean", HookStatus.Wired, "reload weapons only"),
            M("Archery", "Attack", "UseAmmo", "ItemDrop/ItemData&", HookStatus.Wired, "postfix earns one recovery credit per arrow the local player paid for"),
            F("Archery", "Attack", "m_weapon", "ItemDrop/ItemData", HookStatus.Wired, "bow check of the paying attack"),
            F("Archery", "Projectile", "m_ammo", "ItemDrop/ItemData", HookStatus.Wired, "arrow type of a stopped projectile; OnHit prefix/postfix shares the Secondaries entry"),
            F("Archery", "Projectile", "m_owner", "Character", HookStatus.Wired, "only the local player's arrows are rolled"),
            F("Archery", "Projectile", "m_didHit", "Boolean", HookStatus.Wired, "one roll when the arrow stops"),
            M("Archery", "GameCamera", "UpdateCamera", "Single", HookStatus.Wired, "postfix narrows the field of view with the bow draw"),
            F("Archery", "GameCamera", "m_fov", "Single", HookStatus.Wired, "the game's field of view"),
            F("Archery", "GameCamera", "m_freeFly", "Boolean", HookStatus.Wired, "free-fly camera left alone"),
            F("Archery", "GameCamera", "m_camera", "Camera", HookStatus.Wired, "main camera"),
            F("Archery", "GameCamera", "m_skyCamera", "Camera", HookStatus.Wired, "sky camera kept in step"),
            M("Archery", "Character", "IsDrawingBow", "", HookStatus.Wired, "bow draw state"),
            M("Archery", "Humanoid", "GetAttackDrawPercentage", "", HookStatus.Wired, "bow draw progress"),
            F("Archery", "Hud", "m_crosshair", "Image", HookStatus.Wired, "draw indicator anchor"),
            M("Archery", "Attack", "GetProjectileSpawnPoint", "Vector3&,Vector3&", HookStatus.Wired, "Arrows To Crosshair aim direction"),
            M("Archery", "ItemDrop", "DropItem", "ItemDrop/ItemData,Int32,Vector3,Quaternion", HookStatus.Wired, "spawns the recovered arrow"),
        });
    }
}
