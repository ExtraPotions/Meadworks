// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Counts the live members of each Splintering cascade, keyed by the cascade's root. Only consulted when the
    /// (off-by-default) live-descendant cap is on, to decide whether a further split is truncated. Counts are kept
    /// on the owner where splits happen; a miscount only ever loosens or tightens the cap by a few, never crashes.
    /// <para>
    /// A copy is counted by the machine that spawned it, through a <see cref="DescendantMark"/> riding the copy there, and
    /// counted out exactly once: when it dies on this machine, or else when it leaves this machine any other way -
    /// despawned, unloaded with its zone, or taken down when the world is left - so no entry outlives the copies it
    /// counts. A copy that dies on another machine (it changed owner) is counted out here as it unloads; its new owner
    /// never counted it, so its death there takes nothing off that machine's counts. Leaving the world clears it all.
    /// </para>
    /// </summary>
    public static class DescendantRegistry
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        /// <summary>Counts a copy this machine just spawned into its cascade, until it dies or goes.</summary>
        public static void Register(string root, GameObject copy)
        {
            if (string.IsNullOrEmpty(root))
            {
                return;
            }
            Counts.TryGetValue(root, out int count);
            Counts[root] = count + 1;
            copy.AddComponent<DescendantMark>().Root = root;
        }

        /// <summary>A creature died here: if this machine counted it, it is counted out now rather than as it goes.</summary>
        public static void Died(Character? creature)
        {
            DescendantMark? mark = creature is not null ? creature.GetComponent<DescendantMark>() : null;
            if (mark != null)
            {
                mark.Release();
            }
        }

        public static void Unregister(string root)
        {
            if (string.IsNullOrEmpty(root) || !Counts.TryGetValue(root, out int count))
            {
                return;
            }
            if (count <= 1)
            {
                Counts.Remove(root);
            }
            else
            {
                Counts[root] = count - 1;
            }
        }

        public static int Count(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                return 0;
            }
            Counts.TryGetValue(root, out int count);
            return count;
        }

        /// <summary>The world is being left: every count goes with it.</summary>
        public static void Clear() => Counts.Clear();
    }

    /// <summary>
    /// Rides a Splintering copy on the machine that spawned and counted it, and counts it out of its cascade once:
    /// at its death there, or when it is destroyed here any other way (despawned, unloaded, the world left).
    /// </summary>
    internal sealed class DescendantMark : MonoBehaviour
    {
        public string Root = "";

        public void Release()
        {
            string root = Root;
            Root = "";
            DescendantRegistry.Unregister(root);
        }

        private void OnDestroy() => Release();
    }
}
