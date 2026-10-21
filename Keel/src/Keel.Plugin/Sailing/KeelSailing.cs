// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Sailing skill's CS1 registration (design B1). The icon is GrindstoneSkills' skill_sailing.png
// (AI-generated, GPL-3.0-or-later), embedded unchanged under the ValheimMods notice (user decision 2026-10-08); if it
// cannot be decoded, the game's own Karve, Longship or Raft piece icon is used (read at run time, never shipped).
using System;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Sailing;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    public static class KeelSailing
    {
        static readonly string[] IconShips = { "Karve", "VikingShip", "Raft" };
        static MeadworksSkill? skill;
        static Sprite? embedded;
        static bool triedEmbedded;

        /// <summary>The registered skill; null when GrindstoneSkills' Sailing runs (step-aside) or before Start.</summary>
        public static MeadworksSkill? Skill => skill;

        /// <summary>
        /// Registers Sailing through CS1 with the GrindstoneSkills import, unless GrindstoneSkills' own Sailing runs: then
        /// Keel registers nothing, so GrindstoneSkills keeps its levels (design 9.1).
        /// </summary>
        public static void Register(MeadworksLog log)
        {
            if (skill != null) return;
            if (KeelModules.StepAside.SteppedAside(KeelModule.Sailing))
            {
                log.Info("Sailing skill not registered: GrindstoneSkills runs its own Sailing.");
                return;
            }
            var definition = new SkillDefinition(Plugin.Guid, SailingSkill.Identity, SailingSkill.Name, SailingSkill.Description,
                importFrom: new[] { SailingSkill.ImportFrom }, textKey: SailingSkill.TextKey);
            if (definition.Id.Value != SailingSkill.GameType)
                throw new InvalidOperationException("CS1 gives Sailing the number " + definition.Id.Value + ", Keel pinned " + SailingSkill.GameType + ".");
            skill = MeadworksSkills.Register(definition, ShipIcon, KeelText.Translations);
        }

        /// <summary>The local player's Sailing level as CS1 reports it (modifiers included); 0 without a skill or player.</summary>
        public static float Level(Player? player) =>
            skill == null || player == null ? 0f : MeadworksSkills.EffectiveLevel(player, (Skills.SkillType)SailingSkill.GameType);

        public static void Dispose()
        {
            skill?.Dispose();
            skill = null;
            if (embedded != null)
            {
                var texture = embedded.texture;
                UnityEngine.Object.Destroy(embedded);
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            embedded = null;
            triedEmbedded = false;
        }

        static Sprite? ShipIcon()
        {
            if (!triedEmbedded)
            {
                triedEmbedded = true;
                embedded = EmbeddedIcon();
            }
            if (embedded != null) return embedded;
            var scene = ZNetScene.instance;
            if (scene == null) return null;
            foreach (var name in IconShips)
            {
                var prefab = scene.GetPrefab(name);
                var piece = prefab != null ? prefab.GetComponent<Piece>() : null;
                if (piece != null && piece.m_icon != null) return piece.m_icon;
            }
            return null;
        }

        /// <summary>The adopted icon from Keel.dll, decoded by the game's PNG decoder; null when it is missing or invalid.</summary>
        static Sprite? EmbeddedIcon()
        {
            try
            {
                using var stream = typeof(KeelSailing).Assembly.GetManifestResourceStream(SailingSkill.IconResource);
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0) return null;
                    read += n;
                }
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "keel_skill_sailing" };
                if (!ImageConversion.LoadImage(texture, bytes, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = "keel_skill_sailing";
                return sprite;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}