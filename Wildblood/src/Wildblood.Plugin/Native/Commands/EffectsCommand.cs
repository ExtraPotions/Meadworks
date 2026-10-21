// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// <c>wildblood effects &lt;text&gt;</c>: lists the loaded effect prefabs whose name contains that text and plays the
    /// first renderable one in front of the player, so a look can be judged by eye. This exists because prefab names are
    /// Unity asset references and cannot be read out of the decompiled game code - the only way to choose a good cloud or
    /// explosion is to see what the loaded game actually has. Every mutation's effect field is otherwise a guess that
    /// fails silently into a keyword fallback. It scans the prefabs named as effects (<c>fx_</c>, <c>vfx_</c>, <c>sfx_</c>),
    /// which is exactly the pool the effect resolver clones mutation visuals and sounds from.
    /// </summary>
    public static class EffectsCommand
    {
        private const int MaxList = 40;

        public static void Run(Terminal.ConsoleEventArgs args)
        {
            if (args.Length < 3)
            {
                CommandReply.Send(args, "usage: wildblood effects <text>");
                return;
            }
            Player player = Player.m_localPlayer;
            if (ZNetScene.instance == null || player == null)
            {
                CommandReply.Send(args, "wildblood effects: needs a loaded world and a local player.");
                return;
            }
            Report(args, args[2].ToLowerInvariant(), player);
        }

        private static void Report(Terminal.ConsoleEventArgs args, string text, Player player)
        {
            List<GameObject> matches = Matches(text);
            if (matches.Count == 0)
            {
                CommandReply.Send(args, $"wildblood effects: no loaded effect prefab contains '{text}'.");
                return;
            }
            CommandReply.Send(args, $"wildblood effects: {matches.Count} effect prefab(s) contain '{text}':");
            for (int i = 0; i < matches.Count && i < MaxList; i++)
            {
                CommandReply.Send(args, "  " + matches[i].name);
            }
            Play(args, matches, player);
        }

        private static List<GameObject> Matches(string text)
        {
            List<GameObject> found = new List<GameObject>();
            foreach (GameObject prefab in EffectResolver.Prefabs(ZNetScene.instance))
            {
                if (EffectResolver.IsEffectName(prefab) && prefab.name.ToLowerInvariant().Contains(text))
                {
                    found.Add(prefab);
                }
            }
            return found;
        }

        private static void Play(Terminal.ConsoleEventArgs args, List<GameObject> matches, Player player)
        {
            GameObject pick = FirstRenderable(matches) ?? matches[0];
            Vector3 pos = player.transform.position + player.transform.forward * 2f;
            CosmeticClone.Flash(pick, pos, 4f);
            CommandReply.Send(args, $"wildblood effects: playing '{pick.name}' in front of you.");
        }

        private static GameObject? FirstRenderable(List<GameObject> matches)
        {
            foreach (GameObject prefab in matches)
            {
                if (prefab.GetComponentInChildren<ParticleSystem>(true) != null
                    || prefab.GetComponentInChildren<Renderer>(true) != null)
                {
                    return prefab;
                }
            }
            return null;
        }
    }
}
