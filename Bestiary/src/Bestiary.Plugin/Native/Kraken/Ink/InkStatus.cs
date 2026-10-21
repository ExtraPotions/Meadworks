// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// "Inked": the status effect a kraken's ink hit carries. The hit is an ordinary game hit, so the game applies the
    /// effect on the victim's own machine and already drops it for a dodged, blocked or parried hit. When it starts on
    /// the local player, or is renewed by another hit while it lasts, the screen is covered in ink for <see cref="Hold"/>
    /// seconds and then fades (<see cref="InkScreen"/>). A copy of the effect on anyone else draws nothing.
    /// <para>
    /// The one registered instance is kept in the game's status effect table (<c>ObjectDB.m_StatusEffects</c>), where
    /// the hit's hash is looked up: in the live database now, and in every database the game builds or copies later.
    /// </para>
    /// </summary>
    public class InkStatus : StatusEffect
    {
        /// <summary>The ScriptableObject's name; the game hashes it (<c>NameHash</c>) to find the effect.</summary>
        public const string Name = "ECP_KrakenInk";
        public const string WordName = "$se_ecp_inked";
        public const string WordTooltip = "$se_ecp_inked_tooltip";

        /// <summary>Seconds the ink takes to fade once the hold is over.</summary>
        public const float Fade = 0.7f;

        // The game treats a time to live of 0 as "forever", so the effect always keeps a little.
        private const float ShortestLife = 0.1f;

        private static InkStatus? registered;
        private static Sprite? sprite;
        private static bool patched;
        private static float hold = 1.5f;

        /// <summary>The hash a hit carries in <c>HitData.m_statusEffectHash</c>.</summary>
        public static int Hash { get; } = Name.GetStableHashCode();

        /// <summary>Seconds the screen stays covered; also the effect's time to live. Never below 0.</summary>
        public static float Hold
        {
            get => hold;
            set
            {
                hold = Mathf.Max(0f, value);
                if (registered != null)
                {
                    registered.m_ttl = Life;
                }
            }
        }

        private static float Life => Mathf.Max(hold, ShortestLife);

        /// <summary>
        /// Builds the effect (once) and keeps it in every ObjectDB: the live one now, later ones through postfixes on
        /// <c>ObjectDB.Awake</c> and <c>ObjectDB.CopyOtherDB</c>, patched once. Calling it again only updates the icon.
        /// </summary>
        public static void Register(Harmony harmony, Sprite? icon)
        {
            sprite = icon;
            Instance().m_icon = icon;
            Patch(harmony);
            if (ObjectDB.instance != null)
            {
                AddTo(ObjectDB.instance);
            }
        }

        public override void Setup(Character character)
        {
            base.Setup(character);
            Splash(character);
        }

        /// <summary>Hit again while inked: the game renews the effect, and the screen is splashed afresh.</summary>
        public override void ResetTime()
        {
            base.ResetTime();
            Splash(m_character);
        }

        private static void Splash(Character character)
        {
            if (character != null && character == Player.m_localPlayer)
            {
                SafeCall.Run("InkStatus splash", () => InkScreen.Splat(hold, Fade));
            }
        }

        /// <summary>The registered instance; built again if Unity destroyed it.</summary>
        private static InkStatus Instance()
        {
            if (registered != null)
            {
                return registered;
            }
            InkStatus made = CreateInstance<InkStatus>();
            made.name = Name;
            made.hideFlags = HideFlags.DontUnloadUnusedAsset;
            made.m_name = WordName;
            made.m_tooltip = WordTooltip;
            made.m_icon = sprite;
            made.m_ttl = Life;
            registered = made;
            return made;
        }

        private static void Patch(Harmony harmony)
        {
            if (patched)
            {
                return;
            }
            patched = true;
            var after = new HarmonyMethod(typeof(InkStatus), nameof(AfterBuilt));
            harmony.Patch(AccessTools.Method(typeof(ObjectDB), "Awake"), postfix: after);
            harmony.Patch(AccessTools.Method(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), postfix: after);
        }

        private static void AfterBuilt(ObjectDB __instance) =>
            SafeCall.Run("ObjectDB kraken ink", () => AddTo(__instance));

        /// <summary>Puts the effect in the table, or replaces a stale one of ours; never adds it twice.</summary>
        private static void AddTo(ObjectDB db)
        {
            InkStatus effect = Instance();
            db.m_StatusEffects.RemoveAll(se => se is InkStatus && !ReferenceEquals(se, effect));
            if (!db.m_StatusEffects.Contains(effect))
            {
                db.m_StatusEffects.Add(effect);
                Adapters.NativeRuntime.Status(db, effect);
            }
        }
    }
}
