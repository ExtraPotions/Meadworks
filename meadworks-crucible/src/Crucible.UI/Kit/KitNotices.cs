// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public enum KitNoticeKind { TopLeft, Center, Unlock, Biome }

    /// <summary>One local notice delivery. A provider owns presentation; this service owns fallback and journaling.</summary>
    public sealed class KitNotice
    {
        public KitNoticeKind Kind { get; }
        public string RawText { get; }
        public string Title { get; }
        public int Amount { get; }
        public Sprite Icon { get; }
        public bool Log { get; }
        public bool ShowWhenHidden { get; }
        public bool Stinger { get; }
        public string Text => KitNotices.Localize(RawText);
        internal bool Consumed;

        public KitNotice(string text, KitNoticeKind kind = KitNoticeKind.TopLeft, string title = "", int amount = 0,
            Sprite icon = null, bool log = true, bool showWhenHidden = false, bool stinger = true)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (!Enum.IsDefined(typeof(KitNoticeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            RawText = text; Kind = kind; Title = title ?? ""; Amount = amount; Icon = icon;
            // Native unlocks always enter the message log; biome banners never do.
            Log = kind == KitNoticeKind.Unlock || kind != KitNoticeKind.Biome && log;
            ShowWhenHidden = kind == KitNoticeKind.Unlock || kind == KitNoticeKind.Biome || showWhenHidden;
            Stinger = stinger;
        }
    }

    public interface IKitNoticeProvider
    {
        /// <summary>True accepts presentation only. Do not journal, redispatch or display a declined notice.</summary>
        bool TryShow(ProductRegistration source, KitNotice notice);
    }

    public static class KitNotices
    {
        public const string CapabilityId = "meadworks.ui.notices";
        public static readonly System.Version ContractVersion = new System.Version(1, 0);
        static readonly CapabilityLink<IKitNoticeProvider> provider = new CapabilityLink<IKitNoticeProvider>(CapabilityId, ContractVersion);
        static bool dispatching;
        static class Native
        {
            internal static readonly Action<MessageHud, string> Journal = AccessTools.MethodDelegate<Action<MessageHud, string>>(
                AccessTools.Method(typeof(MessageHud), "AddLog", new[] { typeof(string) }));
        }

        /// <summary>Main thread. True means handled or intentionally hidden, never a display acknowledgement.</summary>
        public static bool Show(ProductRegistration source, KitNotice notice)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            if (!source.IsActive || notice.Consumed) return false;
            notice.Consumed = true;
            if (!MeadworksHeadless.AllowsUi || !CrucibleUiPlugin.Active || MessageHud.instance == null)
            {
                CrucibleUiPlugin.Log?.Info(source.Metadata.Name + ": " + notice.Text);
                return false;
            }
            if (Hud.IsUserHidden() && !notice.ShowWhenHidden) return true;
            if (!dispatching && CruciblePlugin.IsReady && provider.TryGet(CruciblePlugin.Services.Capabilities, out var current))
            {
                bool accepted = false;
                dispatching = true;
                try { accepted = current.TryShow(source, notice); }
                catch (Exception error) { CrucibleUiPlugin.Log?.Warning("Notice provider declined after " + error.GetType().Name); }
                finally { dispatching = false; }
                if (accepted)
                {
                    if (notice.Log && notice.Kind != KitNoticeKind.Biome)
                    {
                        string entry = notice.Kind == KitNoticeKind.Unlock ? notice.Title + ": " + notice.RawText : notice.Text;
                        try { if (MessageHud.instance != null) Native.Journal(MessageHud.instance, entry); }
                        catch (Exception error) { CrucibleUiPlugin.Log?.Warning("Accepted notice journal failed: " + error.GetType().Name); }
                    }
                    return true;
                }
            }
            try
            {
                switch (notice.Kind)
                {
                    case KitNoticeKind.TopLeft:
                    case KitNoticeKind.Center:
                        MessageHud.instance.ShowMessage(notice.Kind == KitNoticeKind.Center ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft,
                            notice.RawText, notice.Amount, notice.Icon, notice.ShowWhenHidden, notice.Log);
                        break;
                    case KitNoticeKind.Unlock:
                        MessageHud.instance.QueueUnlockMsg(notice.Icon, notice.Title, notice.RawText);
                        break;
                    case KitNoticeKind.Biome:
                        MessageHud.instance.ShowBiomeFoundMsg(notice.RawText, notice.Stinger);
                        break;
                }
                return true;
            }
            catch (Exception error)
            {
                CrucibleUiPlugin.Log?.Warning("Native notice unavailable: " + error.GetType().Name);
                return false;
            }
        }

        internal static string Localize(string value) => Localization.instance != null ? Localization.instance.Localize(value) : value;
    }
}
