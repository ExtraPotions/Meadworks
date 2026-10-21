// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Object = UnityEngine.Object;

namespace ExtraPotions.Meadworks.OriginalArt
{
    internal static class OriginalItemArt
    {
        [Serializable] sealed class Catalog { public Entry[] assets; public Surface[] materials; }
        [Serializable] sealed class Entry { public string id; public string[] prefabs; }
        [Serializable] sealed class Surface { public string id, nativePrefab; }
        static AssetBundle bundle;
        static Dictionary<string, string> ids;
        static Dictionary<string, string> nativeMaterials;
        static readonly List<Material> ownedMaterials = new List<Material>();
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        static void Load()
        {
            if (bundle != null) return;
            var assembly = typeof(OriginalItemArt).Assembly;
            string platform = Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxServer ? "linux" : "windows";
            string resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".original-items." + platform, StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(resource))
            using (var memory = new MemoryStream())
            { stream.CopyTo(memory); bundle = AssetBundle.LoadFromMemory(memory.ToArray()); }
            if (bundle == null) throw new InvalidOperationException("Original item art bundle did not load: " + resource);
            using (var stream = assembly.GetManifestResourceStream("MeadWorks.OriginalItems.catalog.json"))
            using (var reader = new StreamReader(stream))
            {
                ids = new Dictionary<string, string>(StringComparer.Ordinal);
                var catalog = JsonConvert.DeserializeObject<Catalog>(reader.ReadToEnd());
                if (catalog == null || catalog.assets == null || catalog.assets.Length == 0) throw new InvalidDataException("Original item catalog contains no assets");
                nativeMaterials = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var surface in catalog.materials ?? Array.Empty<Surface>()) if (!string.IsNullOrEmpty(surface.nativePrefab)) nativeMaterials.Add(surface.id, surface.nativePrefab);
                foreach (var entry in catalog.assets)
                {
                    ids.Add(entry.id, entry.id);
                    foreach (string prefab in entry.prefabs ?? Array.Empty<string>()) if (prefab != entry.id) ids.Add(prefab, entry.id);
                }
            }
        }

        internal static Sprite Icon(string prefab)
        {
            if (ExtraPotions.Meadworks.Crucible.MeadworksHeadless.IsHeadless) return null;
            Load();
            if (!ids.TryGetValue(prefab, out string id)) throw new KeyNotFoundException("Original icon missing: " + prefab);
            return bundle.LoadAsset<Sprite>(id + "_icon") ?? throw new InvalidDataException("Original icon sprite missing: " + id);
        }

        internal static GameObject Model(ZNetScene scene, string prefab)
        {
            if (ExtraPotions.Meadworks.Crucible.MeadworksHeadless.IsHeadless) return null;
            var native = scene != null ? scene.GetPrefab("SerpentScale") ?? scene.GetPrefab("TrollHide") : null;
            if (native == null && ObjectDB.instance != null) native = ObjectDB.instance.GetItemPrefab("SerpentScale") ?? ObjectDB.instance.GetItemPrefab("TrollHide");
            return ModelCore(native, prefab, scene);
        }

        internal static GameObject Model(GameObject native, string prefab)
        {
            if (ExtraPotions.Meadworks.Crucible.MeadworksHeadless.IsHeadless) return null;
            return ModelCore(native, prefab, ZNetScene.instance);
        }

        static bool ActiveBelowRoot(Renderer renderer, Transform root)
        {
            for (var node = renderer.transform; node != root; node = node.parent)
                if (!node.gameObject.activeSelf) return false;
            return true;
        }

        internal static Material NativeBaseMaterial(GameObject source)
        {
            var material = source != null ? source.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.enabled && ActiveBelowRoot(renderer, source.transform))
                .SelectMany(renderer => renderer.sharedMaterials)
                .FirstOrDefault(candidate => candidate != null && candidate.shader != null &&
                    candidate.shader.name.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) < 0 && candidate.renderQueue <= 2500 &&
                    candidate.HasProperty("_MainTex") && candidate.GetTexture("_MainTex") != null) : null;
            if (material == null) throw new InvalidOperationException("Native opaque base material with albedo unavailable: " + (source != null ? source.name : "<null>"));
            return material;
        }

        static GameObject ModelCore(GameObject native, string prefab, ZNetScene scene)
        {
            Load();
            if (!ids.TryGetValue(prefab, out string id)) throw new KeyNotFoundException("Original model missing: " + prefab);
            if (models.TryGetValue(id, out var ready) && ready != null) return ready;
            var asset = bundle.LoadAsset<GameObject>(id) ?? throw new InvalidDataException("Original model prefab missing: " + id);
            var nativeMaterial = NativeBaseMaterial(native);
            var root = Object.Instantiate(asset); root.name = id; root.SetActive(false); Object.DontDestroyOnLoad(root);
            var dress = new Dictionary<Material, Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    if (dress.TryGetValue(original, out var cached)) return cached;
                    var nativeLook = nativeMaterial;
                    if (nativeMaterials.TryGetValue(original.name.Replace(" (Instance)", ""), out string sourceName))
                    {
                        var sourcePrefab = scene != null ? scene.GetPrefab(sourceName) : null;
                        if (sourcePrefab == null && ObjectDB.instance != null) sourcePrefab = ObjectDB.instance.GetItemPrefab(sourceName);
                        nativeLook = NativeBaseMaterial(sourcePrefab);
                    }
                    var material = new Material(nativeLook) { name = "MW_" + original.name };
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", original.mainTexture);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", original.color);
                    material.mainTextureScale = Vector2.one; material.mainTextureOffset = Vector2.zero;
                    if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
                    foreach (string map in new[] { "_MetallicGlossMap", "_EmissionMap", "_StyleTex" }) if (material.HasProperty(map)) material.SetTexture(map, null);
                    if (material.HasProperty("_UseStyles")) material.SetFloat("_UseStyles", 0f);
                    material.DisableKeyword("_USESTYLES_ON");
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", original.GetFloat("_Glossiness"));
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", original.GetFloat("_Metallic"));
                    if (material.HasProperty("_MetalGloss")) material.SetFloat("_MetalGloss", 0f);
                    if (material.HasProperty("_EmissionColor") && original.IsKeywordEnabled("_EMISSION"))
                    { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", original.GetColor("_EmissionColor")); }
                    else if (material.HasProperty("_EmissionColor"))
                    { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black); }
                    ownedMaterials.Add(material); dress.Add(original, material); return material;
                }).ToArray();
            }
            models[id] = root; return root;
        }

        internal static GameObject Instantiate(ZNetScene scene, string prefab, Transform parent)
        {
            var model = Model(scene, prefab);
            if (model == null) return null;
            var visual = Object.Instantiate(model, parent, false); visual.name = "meadworks_original_model"; visual.SetActive(true); return visual;
        }

        internal static GameObject Instantiate(GameObject nativeMaterialItem, string prefab, Transform parent)
        {
            var model = Model(nativeMaterialItem, prefab);
            if (model == null) return null;
            var visual = Object.Instantiate(model, parent, false); visual.name = "meadworks_original_model"; visual.SetActive(true); return visual;
        }

        internal static void Shutdown()
        {
            foreach (var model in models.Values) if (model != null) Object.Destroy(model);
            models.Clear();
            foreach (var material in ownedMaterials) if (material != null) Object.Destroy(material);
            ownedMaterials.Clear();
            if (bundle != null) bundle.Unload(false);
            bundle = null; ids = null; nativeMaterials = null;
        }
    }
}
