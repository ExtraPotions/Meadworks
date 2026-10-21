using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace Mantle.Layout
{
    /// <summary>
    /// BiomeLords' Featherweight blessing (the Faller Valkyrie lord) gives extra inventory rows while it is active and
    /// takes them away when the player switches to another blessing; it has no duration and BiomeLords puts it back
    /// after a death. BiomeLords sizes the inventory itself, from the game's 4 rows (or the bought ones) plus its rows,
    /// so on its own it takes Mantle's rows, backpack cells and slots for leftover blessing rows and moves them into a
    /// crate. While Mantle lays out the inventory, <see cref="BiomeLordsPatches"/> stop that and the blessing's rows
    /// become main rows of the layout instead: they come with the blessing, and when it goes, what was in them moves to
    /// free main cells and the rest into BiomeLords' crate, as BiomeLords does. The slots are never touched. Found by
    /// reflection (Mantle loads after BiomeLords, a soft dependency); absent, nothing changes.
    /// </summary>
    public static class BiomeLordsLink
    {
        public const string Guid = "com.taeguk.BiomeLords";
        private const string FeatherweightName = "BiomeLords.Util.FeatherweightInventory";

        private static bool looked;
        private static Type featherweight;
        private static MethodInfo hasBlessing;
        private static MethodInfo baseHeight;
        private static MethodInfo spillToCrate;
        private static PropertyInfo extraRows;
        private static bool collapsed;

        /// <summary>True while a resize BiomeLords asked for is applied as the layout: its overflow goes to the crate.</summary>
        public static bool Spilling { get; private set; }

        /// <summary>Mantle lays out the local player's inventory, so BiomeLords must not size or fill its rows.</summary>
        public static bool OwnsInventory => Player.m_localPlayer != null && LayoutApply.Wanted(Player.m_localPlayer);

        /// <summary>One of Featherweight's methods, or null without BiomeLords.</summary>
        public static MethodInfo Method(string name)
        {
            Bind();
            MethodInfo method = featherweight != null ? AccessTools.Method(featherweight, name) : null;
            if (featherweight != null && method == null)
                Plugin.Log.LogWarning($"BiomeLords is installed but its Featherweight has no {name}; its rows may disturb Mantle's slots");
            return method;
        }

        /// <summary>A new character: nothing is being taken away.</summary>
        public static void Reset() => collapsed = false;

        /// <summary>The blessing's rows for the local player: BiomeLords' extra rows while Featherweight is active and not being taken away.</summary>
        public static int BlessingRows(Player player)
        {
            if (collapsed || player != Player.m_localPlayer || !Bind())
                return 0;
            try
            {
                return (bool)hasBlessing.Invoke(null, new object[] { player }) ? Math.Max(0, (int)extraRows.GetValue(null)) : 0;
            }
            catch (Exception e)
            {
                Fail(e);
                return 0;
            }
        }

        /// <summary>
        /// BiomeLords asks for <paramref name="target"/> rows: more than its base grants the blessing's rows, the base or
        /// less takes them away. The layout is applied instead; false (BiomeLords sizes as usual) when Mantle leaves the
        /// inventory alone.
        /// </summary>
        public static bool TakeOver(Player player, int target)
        {
            if (player != Player.m_localPlayer || !LayoutApply.Wanted(player))
                return false;
            collapsed = !Bind() || target <= Base(player);
            Spilling = true;
            try
            {
                LayoutApply.Apply(player, dropOverflow: true);
            }
            finally
            {
                Spilling = false;
            }
            return true;
        }

        /// <summary>The overflow of a resize BiomeLords asked for goes into its crate with its message; false when it cannot.</summary>
        public static bool Spill(Player player, List<ItemDrop.ItemData> overflow)
        {
            if (!Spilling || spillToCrate == null || overflow.Count == 0)
                return false;
            try
            {
                spillToCrate.Invoke(null, new object[] { player, player.GetInventory(), overflow });
                return true;
            }
            catch (Exception e)
            {
                Fail(e);
                overflow.RemoveAll(item => !player.GetInventory().ContainsItem(item));   // drop only what the crate did not take
                return false;
            }
        }

        private static int Base(Player player)
        {
            try
            {
                return (int)baseHeight.Invoke(null, new object[] { player });
            }
            catch (Exception e)
            {
                Fail(e);
                return int.MaxValue;
            }
        }

        private static bool Bind()
        {
            if (!looked)
            {
                looked = true;
                featherweight = Chainloader.PluginInfos.TryGetValue(Guid, out BepInEx.PluginInfo info) && info.Instance != null
                    ? info.Instance.GetType().Assembly.GetType(FeatherweightName) : null;
                hasBlessing = featherweight != null ? AccessTools.Method(featherweight, "HasBlessing", new[] { typeof(Player) }) : null;
                baseHeight = featherweight != null ? AccessTools.Method(featherweight, "BaseHeight", new[] { typeof(Player) }) : null;
                spillToCrate = featherweight != null ? AccessTools.Method(featherweight, "SpillToCrate") : null;
                extraRows = featherweight != null ? AccessTools.Property(featherweight, "ExtraRows") : null;
            }
            return hasBlessing != null && baseHeight != null && extraRows != null;
        }

        private static void Fail(Exception e)
        {
            Plugin.Log.LogWarning($"BiomeLords' Featherweight failed, its rows are left out of the layout: {e.InnerException?.Message ?? e.Message}");
            hasBlessing = null;
        }
    }
}
