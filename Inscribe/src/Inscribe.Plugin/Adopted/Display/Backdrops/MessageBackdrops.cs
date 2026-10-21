// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Runtime;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The backdrop on the top-left message's icon ("Bronze sword x1" picked up, removed, dropped or broken). The message
    /// queue carries only a sprite, so while the game reports one of those four for an item (<see cref="_about"/>, set
    /// first among the prefixes, so another mod's or our own replacement of the message still counts) the message it
    /// queues with that item's icon is tagged with its tone. When the game puts a message up, a tagged one shows the
    /// backdrop and fades it with the icon. Only the local player's own messages are queued here.
    /// </summary>
    internal static class MessageBackdrops
    {
        private const float FadeSeconds = 4f;   // the game's own fade of the message icon

        private static readonly ConditionalWeakTable<MessageHud.MsgData, Tagged> Tags = new ConditionalWeakTable<MessageHud.MsgData, Tagged>();
        private static ItemDrop.ItemData? _about;
        private static MessageHud.MsgData? _shown;

        private sealed class Tagged
        {
            public ItemDrop.ItemData Item = null!;
        }

        private static void Begin(ItemDrop.ItemData item) => _about = item;

        private static void End() => _about = null;

        [HarmonyPatch]
        internal static class ItemMessagePatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> Targets()
            {
                yield return AccessTools.Method(typeof(Character), nameof(Character.ShowPickupMessage));
                yield return AccessTools.Method(typeof(Character), nameof(Character.ShowRemovedMessage));
                yield return AccessTools.Method(typeof(Humanoid), nameof(Humanoid.DropItem));
                yield return AccessTools.Method(typeof(Humanoid), nameof(Humanoid.DrainEquipedItemDurability));
            }

            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(ItemDrop.ItemData item) => Begin(item);

            [HarmonyPostfix]
            private static void Postfix() => End();
        }

        [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.ShowMessage))]
        internal static class QueuePatch
        {
            [HarmonyPrefix]
            private static void Prefix(MessageHud __instance, out int __state) => __state = __instance.m_msgQeue.Count;

            [HarmonyPostfix]
            private static void Postfix(MessageHud __instance, MessageHud.MessageType type, Sprite icon, int __state)
            {
                ItemDrop.ItemData? item = _about;
                if (item == null || type != MessageHud.MessageType.TopLeft || icon == null || __instance.m_msgQeue.Count <= __state)
                {
                    return;
                }
                if (icon == item.GetIcon() && InscribeModules.Active(InscribeModule.Display))
                {
                    Tag(Newest(__instance), item);
                }
            }
        }

        [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.UpdateMessage))]
        internal static class ShowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MessageHud __instance)
            {
                MessageHud.MsgData current = __instance.currentMsg;
                if (ReferenceEquals(current, _shown))
                {
                    if (!InscribeModules.Active(InscribeModule.Display)) IconBackdrop.HideVisuals(__instance.m_messageIcon);
                    return;
                }
                _shown = current;
                Show(__instance.m_messageIcon, current);
            }
        }

        private static void Show(Image icon, MessageHud.MsgData? message)
        {
            if (icon == null)
            {
                return;
            }
            if (message == null || message.m_icon == null || !Tags.TryGetValue(message, out Tagged tagged))
            {
                IconBackdrop.Hide(icon);
                return;
            }
            IconBackdrop.Set(icon, tagged.Item);
            IconBackdrop.Fade(icon, FadeSeconds);
        }

        private static MessageHud.MsgData? Newest(MessageHud hud)
        {
            MessageHud.MsgData? newest = null;
            foreach (MessageHud.MsgData message in hud.m_msgQeue)
            {
                newest = message;
            }
            return newest;
        }

        private static void Tag(MessageHud.MsgData? message, ItemDrop.ItemData item)
        {
            if (message == null)
            {
                return;
            }
            ItemDrop.ItemData snapshot = item.Clone();
            snapshot.m_customData = item.m_customData == null ? new Dictionary<string, string>()
                : new Dictionary<string, string>(item.m_customData);
            Tags.Remove(message);
            Tags.Add(message, new Tagged { Item = snapshot });
        }

        internal static void Shutdown()
        {
            Tags.Clear();
            _about = null;
            _shown = null;
        }
    }
}
