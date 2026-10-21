// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        static readonly FieldInfo feedbackInstance = typeof(Feedback).GetField("m_instance", BindingFlags.Static | BindingFlags.NonPublic);
        static FeedbackSurface feedback;
        static Feedback NativeFeedback()
        {
            var host = MeadworksHeadless.AllowsUi ? feedbackInstance?.GetValue(null) as Feedback : null;
            var startup = FejdStartup.instance;
            return host != null && startup != null && startup.m_feedbackPrefab != null
                && startup.m_feedbackPrefab.GetComponent<Feedback>() != null && host.transform.parent == startup.transform
                && Native(host.transform, host.gameObject) ? host : null;
        }
        static Image FeedbackBody(Feedback host)
        {
            if (host == null) return null;
            Transform panel = null;
            foreach (Transform child in host.transform) if (child.name == "panel") { if (panel != null) return null; panel = child; }
            return panel != null ? panel.GetComponent<Image>() : null;
        }
        static bool FeedbackBindings(Feedback host)
        {
            var root = host.gameObject;
            var body = FeedbackBody(host);
            if (body == null || !Native(body.transform, root) || host.m_subject == null || host.m_text == null || host.m_sendButton == null
                || host.m_catBug == null || host.m_catFeedback == null || host.m_catIdea == null) return false;
            foreach (var control in new Selectable[] { host.m_subject, host.m_text, host.m_sendButton, host.m_catBug, host.m_catFeedback, host.m_catIdea })
                if (!control.transform.IsChildOf(body.transform) || !Native(control.transform, root)) return false;
            var prefab = FejdStartup.instance.m_feedbackPrefab.GetComponent<Feedback>();
            var original = new Selectable[] { prefab.m_subject, prefab.m_text, prefab.m_sendButton, prefab.m_catBug, prefab.m_catFeedback, prefab.m_catIdea };
            var live = new Selectable[] { host.m_subject, host.m_text, host.m_sendButton, host.m_catBug, host.m_catFeedback, host.m_catIdea };
            for (int i = 0; i < live.Length; i++)
                if (original[i] == null || live[i].GetType() != original[i].GetType()
                    || FeedbackPath(live[i].transform, host.transform) != FeedbackPath(original[i].transform, prefab.transform)) return false;
            return true;
        }
        static string FeedbackPath(Transform at, Transform root)
        {
            string path = "";
            for (int depth = 0; at != null && at != root && depth < 16; at = at.parent, depth++) path = at.name + "/" + path;
            return at == root ? path : null;
        }
        static void TickFeedback()
        {
            var host = NativeFeedback();
            if (feedback != null && feedback.Host != host) { feedback.Dispose(); feedback = null; }
            if (feedback == null && host != null && host.gameObject.activeInHierarchy && FeedbackBindings(host))
            {
                var body = FeedbackBody(host);
                if (body.enabled && body.gameObject.activeInHierarchy && body.sprite != null && body.fillCenter
                    && body.color.a > 0 && body.canvasRenderer.GetAlpha() > 0 && !owned.Contains(body)
                    && !KitSkin.Owns(body) && !KitNativeDialogs.Owns(body) && GamePanelTheme.TryRelease(body)) feedback = new FeedbackSurface(host, body);
            }
            feedback?.Apply(On);
        }
        static void ShutdownFeedback() { feedback?.Dispose(); feedback = null; }
        static bool ReservesFeedback(Image image)
        {
            var host = NativeFeedback();
            return host != null && image != null && Native(image.transform, host.gameObject);
        }
        internal static object FeedbackStatus => feedback?.Status;
        sealed class FeedbackSurface : IDisposable
        {
            internal readonly Feedback Host;
            readonly Image body;
            readonly Surface look;
            readonly Color tint;
            readonly Sprite original;
            readonly Material material;
            readonly Image.Type type;
            readonly float ppu;
            readonly Selectable[] bindings;
            Sprite assigned;
            Material assignedMaterial;
            Image.Type assignedType;
            float assignedPpu;
            bool applied, yielded;
            internal object Status => new { present = Host != null, visible = Host != null && Host.gameObject.activeInHierarchy,
                backingApplied = applied, yielded, backing = "panel", images = look.Images, buttons = look.Buttons,
                switches = look.Switches, labels = look.Labels, scope = "Native69 exact Feedback instance; root shade/content/callbacks unchanged" };
            internal FeedbackSurface(Feedback host, Image image)
            {
                Host = host; body = image; tint = image.color; original = image.sprite; material = image.material;
                type = image.type; ppu = image.pixelsPerUnitMultiplier;
                bindings = new Selectable[] { host.m_subject, host.m_text, host.m_sendButton, host.m_catBug, host.m_catFeedback, host.m_catIdea };
                look = new Surface(host.gameObject);
            }
            bool CurrentBindings() => Host != null && bindings[0] == Host.m_subject && bindings[1] == Host.m_text
                && bindings[2] == Host.m_sendButton && bindings[3] == Host.m_catBug && bindings[4] == Host.m_catFeedback && bindings[5] == Host.m_catIdea;
            internal void Apply(bool on)
            {
                if (yielded) return;
                if (NativeFeedback() != Host || !CurrentBindings() || FeedbackBody(Host) != body || !Native(body.transform, Host.gameObject) || body.color != tint
                    || applied && (body.sprite != assigned || body.material != assignedMaterial || body.type != assignedType || !Mathf.Approximately(body.pixelsPerUnitMultiplier, assignedPpu))
                    || !applied && (body.sprite != original || body.material != material || body.type != type || !Mathf.Approximately(body.pixelsPerUnitMultiplier, ppu)))
                { Dispose(); yielded = true; return; }
                look.Refresh(on, dirty);
                applied = on && body.sprite == KitAppearance.Sprite(KitSurfaceRole.Window) && body.sprite != null;
                if (applied) { assigned = body.sprite; assignedMaterial = body.material; assignedType = body.type; assignedPpu = body.pixelsPerUnitMultiplier; }
            }
            public void Dispose() { look.Dispose(); applied = false; }
        }
    }
}
