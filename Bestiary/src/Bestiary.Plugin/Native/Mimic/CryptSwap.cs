// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;
using ExtraPotions.Bestiary.Fixes;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// Turns some crypt chests into mimics. The decision is the chest owner's, once, at the moment the game would first
    /// fill the chest (when the crypt is generated): a listed chest, at the settings' `Chance`, is marked in its ZDO and
    /// left empty. The swap waits until this machine owns the chest and the mimic prefab exists, then a mimic takes its
    /// place, facing the same way, and remembers which chest it was (for its loot). The chest is removed. If that swap
    /// is given up, the mark is cleared and the chest fills with its normal loot. A chest marked but not yet swapped
    /// (its owner left) is swapped by whoever owns it next.
    /// </summary>
    public static class CryptSwap
    {
        public const string PendingKey = "ecp_mimic_swap";
        public const string ChestKey = "ecp_mimic_chest";

        /// <summary>The pending key's hash, made once: every chest that loads anywhere asks for it.</summary>
        private static readonly int PendingHash = PendingKey.GetStableHashCode();

        /// <summary>Set while a given-up swap runs the chest's own fill, so that fill is not claimed again.</summary>
        internal static bool AllowFill;

        /// <summary>OWNER, first fill. True when the chest becomes a mimic, so it must not be filled.</summary>
        public static bool Claim(Container chest)
        {
            if (AllowFill || !MimicSettings.On || !IsListed(chest) || UnityEngine.Random.value >= MimicSettings.Chance)
            {
                return false;
            }
            chest.m_nview.GetZDO().Set(PendingHash, true);
            chest.gameObject.AddComponent<CryptSwapper>();
            return true;
        }

        /// <summary>Any machine, when a chest wakes: an owner finds a swap left pending and carries it out.</summary>
        public static void Resume(Container chest)
        {
            ZNetView nview = chest.m_nview;
            if (nview != null && nview.IsValid() && nview.IsOwner() && nview.GetZDO().GetBool(PendingHash)
                && chest.GetComponent<CryptSwapper>() == null)
            {
                chest.gameObject.AddComponent<CryptSwapper>();
            }
        }

        /// <summary>The pending mark is no longer a mimic. The chest can keep whatever it already holds.</summary>
        public static void Clear(ZDO zdo)
        {
            if (zdo != null)
            {
                zdo.Set(PendingHash, false);
            }
        }

        private static bool IsListed(Container chest) =>
            MimicSettings.Listed(Utils.GetPrefabName(chest.gameObject));
    }

    /// <summary>
    /// Swaps its chest for a mimic once this machine owns the chest and the prefab exists. Until then it stays
    /// enabled. A swap that is given up clears the mark and fills the chest instead of leaving it empty.
    /// </summary>
    public class CryptSwapper : MonoBehaviour
    {
        private static readonly MethodInfo? FillItems = AccessTools.Method(typeof(Container), "AddDefaultItems");
        private bool spawnFailed;

        private void Update() => Guard.Run("CryptSwapper.Swap", Step);

        private void Step()
        {
            ZNetView chest = GetComponent<ZNetView>();
            Container container = GetComponent<Container>();
            bool valid = chest != null && chest.IsValid();
            if (valid && HasItems(container))
            {
                if (chest!.IsOwner())
                {
                    CryptSwap.Clear(chest.GetZDO());
                }
                enabled = false;
                return;
            }
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(MimicPrefabs.Creature) : null;
            CryptSwapStep step = CryptSwapRules.Next(
                valid, valid && chest!.IsOwner(), prefab != null, MimicPrefabs.Settled, MimicSettings.On);
            if (step == CryptSwapStep.Retry)
            {
                return;
            }
            if (step == CryptSwapStep.Fill)
            {
                Fill(container, chest!);
                return;
            }
            Spawn(chest!, prefab!);
        }

        /// <summary>The chest already holds loot. Destroying it for a mimic would drop that roll.</summary>
        private static bool HasItems(Container? container)
        {
            if (container == null || container.m_inventory == null)
            {
                return false;
            }
            foreach (ItemDrop.ItemData item in container.m_inventory.GetAllItems())
            {
                if (item != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void Fill(Container? container, ZNetView chest)
        {
            if (container == null || FillItems == null)
            {
                return;
            }
            CryptSwap.AllowFill = true;
            try
            {
                FillItems.Invoke(container, null);
            }
            finally
            {
                CryptSwap.AllowFill = false;
            }
            CryptSwap.Clear(chest.GetZDO());
            enabled = false;
        }

        private void Spawn(ZNetView chest, GameObject prefab)
        {
            if (prefab.GetComponent<ZNetView>() == null)
            {
                RejectSpawn(chest, "the mimic prefab has no ZNetView");
                return;
            }
            GameObject mimic = Instantiate(prefab, transform.position, transform.rotation);
            ZNetView view = mimic.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || view.GetZDO() == null)
            {
                Destroy(mimic);
                RejectSpawn(chest, "the spawned mimic has no valid ZDO");
                return;
            }
            view.GetZDO().Set(CryptSwap.ChestKey, Utils.GetPrefabName(gameObject));
            enabled = false;
            ZNetScene.instance.Destroy(gameObject);
        }

        private void RejectSpawn(ZNetView chest, string reason)
        {
            if (spawnFailed) return;
            spawnFailed = true;
            enabled = false;
            Log.Warn("Crypt Mimic swap rejected once: " + reason + "; the chest keeps its normal loot.");
            var container = GetComponent<Container>();
            if (container != null && FillItems != null)
            {
                try { Fill(container, chest); }
                catch (Exception error)
                {
                    CryptSwap.Clear(chest.GetZDO());
                    Log.Warn("Crypt Mimic chest fill failed after spawn rejection: " + error.GetType().Name);
                }
            }
            else CryptSwap.Clear(chest.GetZDO());
        }
    }
}
