// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Core/HerdCallout.cs and Core/FloatingText.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: typed owner-verified native animal RPC, exact owned handler cleanup and local C05 text.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public static class HerdCallout
    {
        public const string RpcName = "shepherd.herd_callout";
        public const float Range = 40;
        sealed class Lease
        {
            internal readonly WeakReference<ZNetView> View;
            internal readonly object Handler;
            internal Lease(ZNetView view, object handler) { View = new WeakReference<ZNetView>(view); Handler = handler; }
        }
        static readonly List<Lease> leases = new List<Lease>();
        static readonly int hash = RpcName.GetStableHashCode();

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
        static class Awake
        {
            [HarmonyPostfix]
            static void Postfix(Tameable __instance) => NativeHusbandry.Run("herd callout registration", () => Register(__instance));
        }
        public static void Register(Tameable tameable)
        {
            var view = tameable != null ? tameable.m_nview : null;
            if (!NativeHusbandry.Active || view == null || !view.IsValid()) return;
            leases.RemoveAll(p => !p.View.TryGetTarget(out var prior) || prior == null);
            if (view.m_functions.ContainsKey(hash)) return;
            view.Register<int>(RpcName, (sender, kind) => NativeHusbandry.Run("herd callout", () => Receive(tameable!, sender, kind)));
            leases.Add(new Lease(view, view.m_functions[hash]));
        }
        public static void Send(Procreation parent, bool strong, bool twins)
        {
            if (!NativeHusbandry.Active || parent.m_nview == null || !parent.m_nview.IsValid() || !parent.m_nview.IsOwner()) return;
            if (strong) parent.m_nview.InvokeRPC(ZNetView.Everybody, RpcName, 1);
            if (twins) parent.m_nview.InvokeRPC(ZNetView.Everybody, RpcName, 2);
        }
        static void Receive(Tameable tameable, long sender, int kind)
        {
            var view = tameable != null ? tameable.m_nview : null;
            if (!Plugin.AllowsUi || !NativeHusbandry.Active || !HusbandrySettings.ShowCallouts.Value || view == null || !view.IsValid()
                || sender != view.GetZDO().GetOwner() || (kind != 1 && kind != 2)) return;
            var camera = Utils.GetMainCamera();
            if (camera == null || DamageText.instance == null || Hud.IsUserHidden()) return;
            var character = tameable!.m_character;
            Vector3 top = character != null ? character.GetTopPoint() : tameable.transform.position + Vector3.up;
            Vector3 position = top + Vector3.up * (kind == 2 ? .9f : .3f);
            float distance = Vector3.Distance(camera.transform.position, position);
            if (distance <= Range) DamageText.instance.AddInworldText(DamageText.TextType.Bonus, position, distance,
                ShepherdLocalization.Text(kind == 1 ? "callout_strong" : "callout_twins"), false);
        }
        public static void Clear()
        {
            foreach (var lease in leases)
                if (lease.View.TryGetTarget(out var view) && view != null && view.m_functions.TryGetValue(hash, out var handler)
                    && ReferenceEquals(handler, lease.Handler)) view.m_functions.Remove(hash);
            leases.Clear();
        }
    }
}
