// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; a Protected Prefabs creature spawns plain;
// native modifiers can be forced alongside the traits.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// <c>wildblood spawn &lt;prefab&gt; &lt;stars&gt; [mutation...]</c>: makes exactly that creature and bypasses EVERYTHING -
    /// the star distribution, the mutation chances, <c>max mutations</c> and the glyph limit - so a rule can be checked
    /// in ten seconds. A boss takes one aspect word instead of mutations (<c>wildblood spawn Bonemass 2 Twin</c>), and its
    /// twin or phantom copies arrive with it exactly as they would from the altar. Bountiful also takes the aspect words
    /// after it as its extras (<c>wildblood spawn gd_king 2 Bountiful Enraged Mending</c>), or rolls them from the boss's
    /// rotation as the altar would when none follow. Native modifiers ride along as <c>mod:&lt;key&gt;</c>, or as a bare word that
    /// is not a mutation (<c>wildblood spawn Troll 3 Thieving mod:Swift Armored</c>); see <see cref="SpawnModifiers"/>. It instantiates the prefab (the machine that runs this owns it), then hands the exact traits to
    /// its controller before the controller resolves, which writes them to the ZDO as a fresh roll. A typo suggests near
    /// matches rather than failing silently.
    /// </summary>
    public static class SpawnCommand
    {
        public static void Run(Terminal.ConsoleEventArgs args)
        {
            if (args.Length < 4)
            {
                CommandReply.Send(args, "usage: wildblood spawn <prefab> <stars> [mutation... | boss aspect | Bountiful [aspect...]] [mod:<modifier>...]");
                return;
            }
            GameObject? prefab = Lookup(args[2]);
            if (prefab == null)
            {
                CommandReply.Send(args, Suggest(args[2]));
                return;
            }
            if (!int.TryParse(args[3], out int stars) || stars < 0)
            {
                CommandReply.Send(args, $"wildblood spawn: '{args[3]}' is not a star count (0 or more).");
                return;
            }
            if (!ExtraPotions.Wildblood.Stars.ProtectedPrefabs.AllowsSpawn(
                    ExtraPotions.Wildblood.Runtime.ProtectedCreatures.IsProtected(prefab), stars, args.Length - 4))
            {
                CommandReply.Send(args, $"wildblood spawn: {prefab.name} is on the Protected Prefabs list and spawns only plain, at 0 stars with no traits.");
                return;
            }
            bool boss = prefab.GetComponent<Character>().IsBoss();
            List<string> words = SpawnModifiers.Split(args, 4, boss, out List<string> modifiers);
            CreatureTraits? traits = boss ? BossTraits(args, words, prefab.name, stars) : CreatureTraitsFrom(args, words, stars);
            if (traits != null)
            {
                Place(args, prefab, traits, modifiers);
            }
        }

        // A boss takes one aspect word, `none` for the plain fight; leaving it out is the plain fight too. Only Bountiful
        // takes more: the words after it are its extras, and with none it rolls them from the boss's rotation.
        private static CreatureTraits? BossTraits(Terminal.ConsoleEventArgs args, List<string> words, string bossPrefab, int stars)
        {
            Aspect? aspect = words.Count > 0 ? AspectCatalog.FromName(words[0]) : Aspect.None;
            if (aspect == null)
            {
                CommandReply.Send(args, $"wildblood spawn: '{words[0]}' is not a boss aspect. Use none, "
                    + string.Join(", ", System.Array.ConvertAll(AspectCatalog.InOrder, AspectCatalog.Word)) + ".");
                return null;
            }
            CreatureTraits traits = new CreatureTraits(stars, aspect.Value);
            if (aspect == Aspect.Bountiful)
            {
                traits.ExtraAspects = words.Count > 1 ? NamedExtras(args, words) : AspectRoller.RollExtras(bossPrefab);
            }
            else if (words.Count > 1)
            {
                CommandReply.Send(args, "wildblood spawn: only Bountiful carries more than one aspect; the rest are ignored.");
            }
            return traits;
        }

        // Named extras ride beside Bountiful whatever the rules say; a word that cannot be one is warned and skipped.
        private static int NamedExtras(Terminal.ConsoleEventArgs args, List<string> words)
        {
            int mask = 0;
            for (int i = 1; i < words.Count; i++)
            {
                Aspect? extra = AspectCatalog.FromName(words[i]);
                string? refusal = Refusal(extra, mask);
                if (refusal != null)
                {
                    CommandReply.Send(args, $"wildblood spawn: '{words[i]}' skipped - {refusal}.");
                    continue;
                }
                mask |= 1 << (int)extra!.Value;
            }
            return mask;
        }

        // Why a named word cannot ride beside Bountiful; null when it can.
        private static string? Refusal(Aspect? extra, int mask) => extra switch
        {
            null => "not a boss aspect",
            Aspect.None or Aspect.Bountiful => "not an aspect Bountiful can carry",
            Aspect named when AspectRoller.BringsBodies(named) && AspectRoller.HoldsBodies(mask)
                => "a boss carries at most one of Twin, Tethered and Phantom",
            _ => null,
        };

        private static CreatureTraits CreatureTraitsFrom(Terminal.ConsoleEventArgs args, List<string> words, int stars)
        {
            CreatureTraits traits = new CreatureTraits(stars, 0);
            foreach (Mutation m in ParseMutations(args, words))
            {
                traits.Add(m);
            }
            return traits;
        }

        private static GameObject? Lookup(string name)
        {
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            return prefab != null && prefab.GetComponent<Character>() != null ? prefab : null;
        }

        // Runs on the machine that will OWN the new creature (it instantiates it); its controller forces the traits.
        private static void Place(Terminal.ConsoleEventArgs args, GameObject prefab, CreatureTraits traits, List<string> modifiers)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                CommandReply.Send(args, "wildblood spawn: needs a local player to place the creature.");
                return;
            }
            Vector3 pos = player.transform.position + player.transform.forward * 2.5f;
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
            Force(args, go, traits, pos, modifiers);
        }

        private static void Force(Terminal.ConsoleEventArgs args, GameObject go, CreatureTraits traits, Vector3 pos, List<string> modifiers)
        {
            EliteController? controller = go.GetComponent<EliteController>();
            if (controller == null && ExtraPotions.Wildblood.Runtime.ProtectedCreatures.IsProtected(go))
            {
                CommandReply.Send(args, $"wildblood spawn: {go.name} (Protected Prefabs) as the game makes it.");
                return;
            }
            if (controller == null)
            {
                CommandReply.Send(args, "wildblood spawn: that prefab is not a creature this mod can mark.");
                return;
            }
            controller.ForceTraits(traits, Heightmap.FindBiome(pos));
            CommandReply.Send(args, $"wildblood spawn: {go.name} at {traits.Stars} star(s){Words(traits)}.");
            SpawnModifiers.ApplyWhenResolved(args, controller, modifiers);
        }

        // Named mutations are applied whether or not the rules would ever have given them; an unknown word is warned and
        // skipped rather than aborting the spawn.
        private static List<Mutation> ParseMutations(Terminal.ConsoleEventArgs args, List<string> words)
        {
            List<Mutation> muts = new List<Mutation>();
            foreach (string word in words)
            {
                Mutation? m = MutationCatalog.FromName(word);
                if (m.HasValue)
                {
                    muts.Add(m.Value);
                }
                else
                {
                    CommandReply.Send(args, $"wildblood spawn: unknown mutation '{word}', skipped.");
                }
            }
            return muts;
        }

        private static string Words(CreatureTraits traits)
        {
            List<string> words = new List<string>();
            foreach (Aspect aspect in traits.Aspects())
            {
                words.Add(AspectCatalog.Word(aspect));
            }
            foreach (Mutation m in traits.Active())
            {
                words.Add(MutationCatalog.Word(m));
            }
            return words.Count == 0 ? "" : " [" + string.Join(", ", words) + "]";
        }

        private static string Suggest(string typed)
        {
            List<string> near = new List<string>();
            string lower = typed.ToLowerInvariant();
            foreach (GameObject p in ZNetScene.instance.m_prefabs)
            {
                if (p != null && p.name.ToLowerInvariant().Contains(lower) && p.GetComponent<Character>() != null)
                {
                    near.Add(p.name);
                    if (near.Count >= 8) { break; }
                }
            }
            return near.Count > 0
                ? $"wildblood spawn: no creature prefab '{typed}'. Did you mean: {string.Join(", ", near)}?"
                : $"wildblood spawn: no creature prefab '{typed}'.";
        }
    }
}
