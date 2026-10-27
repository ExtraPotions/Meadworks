// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Wildblood.Runtime {
    /// <summary>
    /// Recovers ZNetScene when RemoveObjects throws on a dead entry: a network view destroyed with Object.Destroy (or with
    /// a destroyed parent) instead of ZNetScene.Destroy stays in m_instances, and vanilla then throws on it every frame
    /// for the rest of the session. Only after such a throw, the dead entries are removed as vanilla would have removed
    /// them (an owned, non-persistent ZDO is destroyed with its object) and each prefab is named once in the log.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "RemoveObjects")]
    internal static class SceneInstanceGuard {
        static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
            AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");
        static readonly List<KeyValuePair<ZDO, ZNetView>> dead = new List<KeyValuePair<ZDO, ZNetView>>();
        static readonly HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);

        static Exception? Finalizer(ZNetScene __instance, Exception? __exception) {
            if (!(__exception is NullReferenceException) || __instance == null) return __exception;
            try {
                var instances = Instances(__instance);
                dead.Clear();
                foreach (var pair in instances)
                    if (pair.Value == null || pair.Value.GetZDO() == null) dead.Add(pair);
                if (dead.Count == 0) return __exception;
                foreach (var pair in dead) {
                    var zdo = pair.Key;
                    instances.Remove(zdo);
                    if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value.gameObject);
                    var prefab = __instance.GetPrefab(zdo.GetPrefab());
                    var name = prefab != null ? prefab.name : zdo.GetPrefab().ToString();
                    if (named.Add(name))
                        Plugin.NativeLogger?.LogWarning($"Removed a destroyed {name} network object left in the scene by another destroy path.");
                    var zdos = ZDOMan.instance;
                    if (zdos != null && zdos.GetZDO(zdo.m_uid) == zdo && zdo.IsOwner() && !zdo.Persistent) zdos.DestroyZDO(zdo);
                }
                return null;
            } catch (Exception error) {
                Plugin.NativeLogger?.LogWarning("Scene recovery failed: " + error.GetType().Name);
                return __exception;
            } finally {
                dead.Clear();
            }
        }
    }
}
